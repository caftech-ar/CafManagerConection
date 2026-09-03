using CafManagerConection.Domain.Connections;
using CafManagerConection.Domain.Sessions;
using CafManagerConection.UseCases.Abstractions;

namespace CafManagerConection.UseCases.Sessions;

public sealed class SessionManager
{
    private readonly SessionRegistry _registro;
    private readonly Func<Guid, CancellationToken, Task<ConnectionRecord?>> _buscarConexion;
    private readonly ISessionHost _host;
    private readonly IAppLogger? _logger;
    private readonly IConnectionHistoryRepository? _historial;
    private readonly TimeProvider _reloj;

    private readonly Dictionary<Guid, Seguimiento> _sesiones = [];

    private sealed class Seguimiento(ISessionSurface superficie, Guid conexion, DateTimeOffset inicio)
    {
        public ISessionSurface Superficie { get; } = superficie;

        public Guid Conexion { get; } = conexion;

        public DateTimeOffset Inicio { get; } = inicio;

        public bool LlegoAConectar { get; set; }

        public SessionFailure? UltimoFallo { get; set; }

        public bool Anotada { get; set; }
    }

    private readonly Lock _candado = new();

    public SessionManager(
        SessionRegistry registro,
        Func<Guid, CancellationToken, Task<ConnectionRecord?>> buscarConexion,
        ISessionHost host,
        IAppLogger? logger = null,
        IConnectionHistoryRepository? historial = null,
        TimeProvider? reloj = null)
    {
        _registro = registro;
        _buscarConexion = buscarConexion;
        _host = host;
        _logger = logger;
        _historial = historial;
        _reloj = reloj ?? TimeProvider.System;
    }

    public IReadOnlyList<SessionInfo> ActiveSessions => _registro.ActiveSessions;

    public int CountForConnection(Guid connectionId) => _registro.CountForConnection(connectionId);

    /// <summary>Abre una sesión; sin <paramref name="forceNew"/> trae al frente la que ya está (FR-044a).</summary>
    public async Task<OperationResult<Guid>> OpenAsync(
        Guid connectionId, bool forceNew = false, CancellationToken ct = default)
    {
        if (!forceNew && _registro.FirstForConnection(connectionId) is { } abierta)
        {
            if (Seguir(abierta.SessionId) is { } yaAbierta)
            {
                yaAbierta.Superficie.Activate();
            }

            return new OperationResult<Guid>(true, abierta.SessionId, null);
        }

        ConnectionRecord? registro;

        try
        {
            registro = await _buscarConexion(connectionId, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger?.TechnicalError("No se pudo leer la conexión al abrir la sesión", ex);
            return new OperationResult<Guid>(false, default, "No se pudo leer la conexión.");
        }

        if (registro is null)
        {
            return new OperationResult<Guid>(
                false, default, "La conexión ya no existe.");
        }

        var idSesion = Guid.NewGuid();
        ISessionSurface sesion;

        try
        {
            sesion = _host.Create(idSesion, registro);
        }
        catch (Exception ex)
        {
            _logger?.TechnicalError("No se pudo crear la sesión", ex);
            return new OperationResult<Guid>(false, default, "No se pudo crear la sesión.");
        }

        var ahora = _reloj.GetUtcNow();
        var seguimiento = new Seguimiento(sesion, connectionId, ahora);

        lock (_candado)
        {
            _sesiones[idSesion] = seguimiento;
        }

        _registro.Register(idSesion, connectionId, registro.Connection.Name, ahora);

        sesion.StateChanged += (_, cambio) =>
        {
            _registro.UpdateState(idSesion, cambio.State);

            if (cambio.State == SessionState.Connected)
            {
                seguimiento.LlegoAConectar = true;
            }

            if (cambio.Failure is not null)
            {
                seguimiento.UltimoFallo = cambio.Failure;
            }
        };

        try
        {
            await sesion.ConnectAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger?.TechnicalError($"Falló la conexión de la sesión {idSesion}", ex);
        }

        if (!seguimiento.LlegoAConectar && sesion.State == SessionState.Error)
        {
            await Anotar(seguimiento, ConnectionOutcome.Failed, null).ConfigureAwait(false);
        }

        return new OperationResult<Guid>(true, idSesion, null);
    }

    public async Task<OperationResult> ReconnectAsync(Guid sessionId, CancellationToken ct = default)
    {
        if (Seguir(sessionId) is not { } seguimiento)
        {
            return new OperationResult(false, "La sesión ya no está abierta.");
        }

        var sesion = seguimiento.Superficie;

        if (sesion.State is SessionState.Connected or SessionState.Connecting)
        {
            return new OperationResult(false, "La sesión ya está conectada.");
        }

        _registro.UpdateState(sessionId, SessionState.Connecting);

        try
        {
            await sesion.ConnectAsync(ct).ConfigureAwait(false);
            return new OperationResult(true, null);
        }
        catch (Exception ex)
        {
            _logger?.TechnicalError($"Falló la reconexión de la sesión {sessionId}", ex);
            _registro.UpdateState(sessionId, SessionState.Error);
            return new OperationResult(false, "No se pudo reconectar.");
        }
    }

    public async Task CloseAsync(Guid sessionId)
    {
        Seguimiento? seguimiento;

        lock (_candado)
        {
            _sesiones.Remove(sessionId, out seguimiento);
        }

        if (seguimiento is null)
        {
            _registro.Unregister(sessionId);
            return;
        }

        Soltar(sessionId, seguimiento.Superficie);
        _registro.Unregister(sessionId);

        await AnotarElCierre(seguimiento).ConfigureAwait(false);
    }

    public void Close(Guid sessionId) => _ = CloseAsync(sessionId);

    // FR-054: cada cierre va aislado, una sesión que explota no puede colgar el cierre de las demás.
    public void CloseAll()
    {
        KeyValuePair<Guid, Seguimiento>[] vivas;

        lock (_candado)
        {
            vivas = [.. _sesiones];
            _sesiones.Clear();
        }

        foreach (var (id, seguimiento) in vivas)
        {
            Soltar(id, seguimiento.Superficie);
            _registro.Unregister(id);
        }

        foreach (var (_, seguimiento) in vivas)
        {
            AnotarElCierre(seguimiento).GetAwaiter().GetResult();
        }
    }

    private Seguimiento? Seguir(Guid sessionId)
    {
        lock (_candado)
        {
            return _sesiones.GetValueOrDefault(sessionId);
        }
    }

    private Task AnotarElCierre(Seguimiento seguimiento)
    {
        if (seguimiento.Anotada)
        {
            return Task.CompletedTask;
        }

        if (seguimiento.LlegoAConectar)
        {
            var duracion = _reloj.GetUtcNow() - seguimiento.Inicio;
            return Anotar(seguimiento, ConnectionOutcome.Success, (int)duracion.TotalSeconds);
        }

        return Anotar(
            seguimiento,
            seguimiento.UltimoFallo is null ? ConnectionOutcome.Cancelled : ConnectionOutcome.Failed,
            null);
    }

    private async Task Anotar(Seguimiento seguimiento, ConnectionOutcome resultado, int? segundos)
    {
        seguimiento.Anotada = true;

        if (_historial is null)
        {
            return;
        }

        try
        {
            await _historial.AddAsync(new ConnectionHistoryEntry(
                Guid.NewGuid(),
                seguimiento.Conexion,
                seguimiento.Inicio,
                resultado,
                seguimiento.UltimoFallo?.Reason,
                segundos)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger?.TechnicalError("anotar el historial de conexión", ex);
        }
    }

    private void Soltar(Guid sessionId, ISessionSurface? sesion)
    {
        if (sesion is null)
        {
            return;
        }

        try
        {
            sesion.Dispose();
        }
        catch (Exception ex)
        {
            _logger?.TechnicalError($"Falló el cierre de la sesión {sessionId}", ex);
        }
    }
}
