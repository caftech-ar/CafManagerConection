using System.Net;
using System.Net.Sockets;
using CafManagerConection.Domain.Connections;
using CafManagerConection.Domain.Credentials;
using Renci.SshNet;

namespace CafManagerConection.Ssh;

public sealed record TunnelStatus(
    Guid TunnelId,
    string Name,
    int LocalPort,
    string RemoteHost,
    int RemotePort,
    bool IsActive,
    string? FailureMessage);

/// <summary>Levanta y detiene los reenvíos de puerto local de una conexión (FR-088 a FR-093).</summary>
public sealed class TunnelHost : IAsyncDisposable
{
    private readonly SshSessionRequest _request;
    private readonly IHostKeyVerifier _verifier;
    private readonly StoredCredential? _credential;

    private readonly Dictionary<Guid, ForwardedPortLocal> _activos = [];

    private readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, string> _fallos = [];

    private SshClient? _client;

    public TunnelHost(
        SshSessionRequest request, IHostKeyVerifier verifier, StoredCredential? credential)
    {
        _request = request;
        _verifier = verifier;
        _credential = credential;
    }

    public event EventHandler<TunnelStatus>? StatusChanged;

    public bool IsActive(Guid tunnelId) =>
        _activos.TryGetValue(tunnelId, out var p) && p.IsStarted;

    public TunnelStatus StatusOf(SshTunnel tunnel) => new(
        tunnel.Id,
        tunnel.Name,
        tunnel.LocalPort,
        tunnel.RemoteHost,
        tunnel.RemotePort,
        IsActive(tunnel.Id),
        _fallos.GetValueOrDefault(tunnel.Id));

    /// <summary>Levanta un túnel. Devuelve el mensaje de error, o <c>null</c> si quedó activo.</summary>
    public async Task<string?> StartAsync(SshTunnel tunnel, CancellationToken ct = default)
    {
        if (IsActive(tunnel.Id))
        {
            return null;
        }

        _fallos.TryRemove(tunnel.Id, out _);

        if (PuertoOcupado(tunnel.LocalPort))
        {
            return Fallar(tunnel,
                $"El puerto local {tunnel.LocalPort} ya está en uso por otro programa.");
        }

        try
        {
            if (_client is not { IsConnected: true })
            {
                var sesion = new SshSession(_request, _verifier);
                _client = sesion.CreateClientForCommands(_credential);
                await Task.Run(() => _client.Connect(), ct).ConfigureAwait(false);
            }

            var puerto = new ForwardedPortLocal(
                "127.0.0.1",
                (uint)tunnel.LocalPort,
                tunnel.RemoteHost,
                (uint)tunnel.RemotePort);

            puerto.Exception += (_, e) => _fallos[tunnel.Id] = e.Exception.Message;

            _client.AddForwardedPort(puerto);
            puerto.Start();

            _activos[tunnel.Id] = puerto;
            StatusChanged?.Invoke(this, StatusOf(tunnel));

            return null;
        }
        catch (Exception ex)
        {
            return Fallar(tunnel, TraducirFallo(ex, tunnel.LocalPort));
        }
    }

    public void Stop(SshTunnel tunnel)
    {
        if (!_activos.TryGetValue(tunnel.Id, out var puerto))
        {
            return;
        }

        try
        {
            if (puerto.IsStarted)
            {
                puerto.Stop();
            }

            _client?.RemoveForwardedPort(puerto);
            puerto.Dispose();
        }
        catch (Exception)
        {
        }
        finally
        {
            _activos.Remove(tunnel.Id);
            StatusChanged?.Invoke(this, StatusOf(tunnel));
        }
    }

    /// <summary>Levanta los túneles marcados para arrancar solos (FR-091); si uno falla, los demás se levantan igual.</summary>
    public async Task<IReadOnlyList<string>> StartAutoAsync(
        IEnumerable<SshTunnel> tunnels, CancellationToken ct = default)
    {
        var errores = new List<string>();

        foreach (var t in tunnels.Where(t => t.AutoStart))
        {
            if (await StartAsync(t, ct).ConfigureAwait(false) is { } error)
            {
                errores.Add($"{t.Name}: {error}");
            }
        }

        return errores;
    }

    private static bool PuertoOcupado(int puerto)
    {
        try
        {
            using var listener = new TcpListener(IPAddress.Loopback, puerto);
            listener.Start();
            listener.Stop();
            return false;
        }
        catch (SocketException)
        {
            return true;
        }
    }

    private static string TraducirFallo(Exception ex, int puertoLocal) => ex switch
    {
        SocketException { SocketErrorCode: SocketError.AddressAlreadyInUse } =>
            $"El puerto local {puertoLocal} ya está en uso por otro programa.",

        SocketException =>
            "No se pudo alcanzar el servidor para establecer el túnel.",

        Renci.SshNet.Common.SshAuthenticationException =>
            "El servidor rechazó las credenciales al abrir el túnel.",

        _ => $"No se pudo levantar el túnel: {ex.Message}",
    };

    private string Fallar(SshTunnel tunnel, string mensaje)
    {
        _fallos[tunnel.Id] = mensaje;
        StatusChanged?.Invoke(this, StatusOf(tunnel));
        return mensaje;
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var puerto in _activos.Values.ToList())
        {
            try
            {
                if (puerto.IsStarted)
                {
                    puerto.Stop();
                }

                puerto.Dispose();
            }
            catch (Exception)
            {
            }
        }

        _activos.Clear();

        try
        {
            if (_client is { IsConnected: true })
            {
                await Task.Run(() => _client.Disconnect()).ConfigureAwait(false);
            }

            _client?.Dispose();
            _client = null;
        }
        catch (Exception)
        {
        }
    }
}
