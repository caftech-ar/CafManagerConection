using System.Diagnostics;
using System.Text;
using CafManagerConection.Domain.Connections;
using CafManagerConection.Domain.Credentials;
using CafManagerConection.Domain.Sessions;
using CafManagerConection.Domain.Settings;
using CafManagerConection.UseCases.Abstractions;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace CafManagerConection.Ssh;

public sealed record SshSessionRequest(
    Guid ConnectionId,
    string Host,
    int Port,
    string UserName,
    SshAuthMethod AuthMethod,
    string? PrivateKeyPath,
    string? KnownHostFingerprint,
    int KeepAliveSeconds,
    int InitialColumns,
    int InitialRows,
    int TimeoutSeconds,
    string? CertificatePath = null);

public enum HostKeyDecision
{
    Accept,
    AcceptAndRemember,
    Reject,
}

public interface IHostKeyVerifier
{
    /// <param name="fingerprint">Formato <c>SHA256:base64</c>, igual al de OpenSSH.</param>
    HostKeyDecision Verify(Guid connectionId, string host, string fingerprint, string? known);
}

public sealed class SshSession : IAsyncDisposable
{
    private const int IntentosMaximosDeContraseña = 3;

    private readonly SshSessionRequest _request;
    private readonly IHostKeyVerifier _verifier;
    private readonly IAppLogger? _logger;
    private readonly IInteractivePasswordPrompt? _interactivePasswordPrompt;
    private readonly IRegistroDeTrazas? _trazas;
    private readonly string _servidor;

    private SshClient? _client;
    private ShellStream? _shell;
    private CancellationTokenSource? _cts;
    private Task? _reader;

    private string? _acceptedFingerprint;
    private bool _rememberFingerprint;

    private bool _preguntoPorTeclado;

    private string? _contraseñaInteractiva;

    private long _recibidos;
    private long _enviados;

    private SondaDeSudo? _sonda;
    private Task<ResultadoDeSondeo?>? _sondeo;

    public SshSession(
        SshSessionRequest request,
        IHostKeyVerifier verifier,
        IAppLogger? logger = null,
        IInteractivePasswordPrompt? interactivePasswordPrompt = null,
        IRegistroDeTrazas? trazas = null,
        string? servidor = null)
    {
        _request = request;
        _verifier = verifier;
        _logger = logger;
        _interactivePasswordPrompt = interactivePasswordPrompt;
        _trazas = trazas;
        _servidor = servidor is { Length: > 0 } ? servidor : request.Host;
    }

    private void Anotar(
        TipoDeTraza tipo,
        string enviado,
        int? codigo,
        TimeSpan tardo,
        string salida,
        string error = "")
    {
        if (_trazas is not { Activo: true })
        {
            return;
        }

        _trazas.Anotar(new EntradaDeTraza(
            DateTimeOffset.Now,
            _request.ConnectionId,
            _servidor,
            tipo,
            enviado,
            codigo,
            tardo,
            salida,
            error));
    }

    /// <summary>El detalle que muestra <c>ssh -v</c>: qué se acordó con el servidor.</summary>
    private static string Negociado(ConnectionInfo info)
    {
        var partes = new List<string>(5)
        {
            $"kex={Ono(info.CurrentKeyExchangeAlgorithm)}",
            $"clave-de-host={Ono(info.CurrentHostKeyAlgorithm)}",
            $"cifrado={Ono(info.CurrentServerEncryption)}",
            $"mac={Ono(info.CurrentServerHmacAlgorithm)}",
        };

        if (Ono(info.CurrentServerCompressionAlgorithm) is { Length: > 0 } compresion
            && compresion != "none")
        {
            partes.Add($"compresion={compresion}");
        }

        return string.Join(" · ", partes);

        static string Ono(string? valor) => valor is { Length: > 0 } ? valor : "?";
    }

    public Guid ConnectionId => _request.ConnectionId;

    public StoredCredential? CredencialEfectiva =>
        _contraseñaInteractiva is { } escrita
            ? new StoredCredential(
                string.IsNullOrWhiteSpace(_request.UserName) ? string.Empty : _request.UserName,
                null,
                escrita)
            : null;

    public SessionState State { get; private set; } = SessionState.Disconnected;

    public SessionFailure? Failure { get; private set; }

    public string? FingerprintToRemember => _rememberFingerprint ? _acceptedFingerprint : null;

    public event EventHandler<SessionStateChanged>? StateChanged;

    public event EventHandler<ReadOnlyMemory<byte>>? DataReceived;

    public long BytesReceived => Interlocked.Read(ref _recibidos);

    public long BytesSent => Interlocked.Read(ref _enviados);

    /// <summary>Conecta. No lanza ante un fallo previsto: pasa a <see cref="SessionState.Error"/> con su motivo (FR-054).</summary>
    public async Task ConnectAsync(StoredCredential? credential, CancellationToken ct = default)
    {
        SetState(SessionState.Connecting);

        ConnectionInfo? info = null;

        try
        {
            info = BuildConnectionInfo(credential);
            _client = new SshClient(info);
            _client.HostKeyReceived += OnHostKeyReceived;

            if (_request.KeepAliveSeconds > 0)
            {
                _client.KeepAliveInterval = TimeSpan.FromSeconds(_request.KeepAliveSeconds);
            }

            Anotar(
                TipoDeTraza.Conexion,
                $"ssh {info.Username}@{_request.Host}:{_request.Port}",
                null,
                TimeSpan.Zero,
                $"abriendo · autenticación por {Metodo(credential)}"
                + (_request.KeepAliveSeconds > 0
                    ? $" · keep-alive cada {_request.KeepAliveSeconds} s"
                    : string.Empty));

            var reloj = Stopwatch.StartNew();
            await Task.Run(() => _client.Connect(), ct).ConfigureAwait(false);
            _logger?.WorkCompleted(_request.ConnectionId, RemoteWork.Handshake, reloj.Elapsed);

            var saludo = reloj.Elapsed;

            Anotar(
                TipoDeTraza.Conexion,
                $"ssh {info.Username}@{_request.Host}:{_request.Port}",
                0,
                saludo,
                $"autenticado · {Negociado(info)}"
                + (_acceptedFingerprint is { } huella ? $" · host {huella}" : string.Empty));

            reloj.Restart();
            _shell = _client.CreateShellStream(
                "xterm-256color",
                (uint)_request.InitialColumns,
                (uint)_request.InitialRows,
                0,
                0,
                4096);
            _logger?.WorkCompleted(_request.ConnectionId, RemoteWork.ShellChannel, reloj.Elapsed);

            _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _reader = Task.Run(() => ReadLoopAsync(_cts.Token), CancellationToken.None);

            Anotar(
                TipoDeTraza.Conexion,
                $"canal interactivo xterm-256color {_request.InitialColumns}x{_request.InitialRows}",
                0,
                reloj.Elapsed,
                $"sesión lista · saludo {saludo.TotalMilliseconds:0} ms");

            SetState(SessionState.Connected);

            ArrancarSondeoDeSudo();
        }
        catch (Exception ex) when (HayQueReintentarConContraseña(
            ex,
            _preguntoPorTeclado,
            credential?.HasSecret == true,
            _interactivePasswordPrompt is not null))
        {
            await ReintentarConContraseñaAsync(credential, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            if (EsCancelacionDePedidoDeContraseña(ex))
            {
                try
                {
                    _client?.Dispose();
                }
                catch (Exception)
                {
                }

                _client = null;
                SetState(SessionState.Disconnected);
                return;
            }

            var fallo = Map(ex, info);

            Anotar(
                TipoDeTraza.Cierre,
                $"ssh {_request.UserName}@{_request.Host}:{_request.Port}",
                -1,
                TimeSpan.Zero,
                fallo.UserMessage,
                ex.Message);

            Fail(fallo);
        }
    }

    private string Metodo(StoredCredential? credential) =>
        _request.AuthMethod == SshAuthMethod.PrivateKey
            ? $"clave privada ({System.IO.Path.GetFileName(_request.PrivateKeyPath)})"
            : credential?.HasSecret == true
                ? "contraseña guardada"
                : _contraseñaInteractiva is not null
                    ? "contraseña escrita en la consola"
                    : "contraseña pedida en la consola";

    private async Task ReintentarConContraseñaAsync(
        StoredCredential? credential, CancellationToken ct)
    {
        try
        {
            _client?.Dispose();
        }
        catch (Exception)
        {
        }

        _client = null;

        var usuario = string.IsNullOrWhiteSpace(_request.UserName)
            ? credential?.UserName ?? string.Empty
            : _request.UserName;

        var contraseña = await _interactivePasswordPrompt!
            .PedirAsync(usuario, _request.Host, 1, null, ct)
            .ConfigureAwait(false);

        if (contraseña is null)
        {
            SetState(SessionState.Disconnected);
            return;
        }

        _contraseñaInteractiva = contraseña;

        await ConnectAsync(credential, ct).ConfigureAwait(false);
    }

    private static bool EsCancelacionDePedidoDeContraseña(Exception ex) =>
        ex is InteractivePasswordCancelledException
        || ex.InnerException is InteractivePasswordCancelledException;

    /// <summary>La contraseña de <c>sudo</c> de esta sesión: nace vacía y se pisa con ceros al cerrarla, así que reabrir la misma conexión la vuelve a pedir (FR-184e, reglas 4 y 5).</summary>
    public ContrasenaDeSudoDeSesion ContrasenaDeSudo { get; } = new();

    /// <summary>Lo que dio el sondeo de <c>sudo</c> de esta sesión, o null si todavía no contestó.</summary>
    public ResultadoDeSondeo? EscaladaDeSudo => _sonda?.Sondeado;

    /// <summary>Cuántos <c>sudo</c> de sondeo se ejecutaron en esta sesión. SC-051 cuenta que sea uno.</summary>
    public int SondeosDeSudo => _sonda?.Sondeos ?? 0;

    /// <summary>El resultado del único sondeo de la sesión; null cuando no se pudo preguntar (FR-184c).</summary>
    public Task<ResultadoDeSondeo?> SondearSudoAsync()
    {
        ArrancarSondeoDeSudo();

        return _sondeo ?? Task.FromResult<ResultadoDeSondeo?>(null);
    }

    private void ArrancarSondeoDeSudo()
    {
        if (_sondeo is not null || _client is not { IsConnected: true })
        {
            return;
        }

        _sonda ??= new SondaDeSudo(EjecutarParaSondearAsync);
        _sondeo = SondearSinPropagarAsync();
    }

    private async Task<ResultadoDeSondeo?> SondearSinPropagarAsync()
    {
        var reloj = Stopwatch.StartNew();

        try
        {
            var resultado = await _sonda!.SondearAsync().ConfigureAwait(false);

            Anotar(
                TipoDeTraza.Escalada,
                SondaDeSudo.Comando,
                0,
                reloj.Elapsed,
                $"escalada: {Dicho(resultado)}");

            return resultado;
        }
        catch (Exception ex)
        {
            Anotar(TipoDeTraza.Escalada, SondaDeSudo.Comando, 0, reloj.Elapsed, ex.Message);

            return null;
        }
    }

    private static string Dicho(ResultadoDeSondeo resultado) => resultado switch
    {
        ResultadoDeSondeo.SinContrasena => "sudo sin contraseña",
        ResultadoDeSondeo.PideContrasena => "sudo pide contraseña",
        _ => "el usuario no puede escalar",
    };

    private async Task<CommandResult> EjecutarParaSondearAsync(
        string comando, int timeoutSeconds, CancellationToken ct)
    {
        if (_client is not { IsConnected: true } cliente)
        {
            return new CommandResult(-1, string.Empty, "La sesión no está conectada.");
        }

        using var cmd = cliente.CreateCommand(comando);
        cmd.CommandTimeout = TimeSpan.FromSeconds(timeoutSeconds);

        using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limite.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        var salida = await Task.Run(() => cmd.Execute(), limite.Token).ConfigureAwait(false);

        return new CommandResult(cmd.ExitStatus ?? 0, salida, cmd.Error ?? string.Empty);
    }

    internal SshClient CreateClientForCommands(StoredCredential? credential)
    {
        var client = new SshClient(BuildConnectionInfo(credential));
        client.HostKeyReceived += OnHostKeyReceived;

        return client;
    }

    internal SftpClient CreateSftpClient(StoredCredential? credential)
    {
        var client = new SftpClient(BuildConnectionInfo(credential));
        client.HostKeyReceived += OnHostKeyReceived;

        return client;
    }

    private ConnectionInfo BuildConnectionInfo(StoredCredential? credential)
    {
        var usuario = string.IsNullOrWhiteSpace(_request.UserName)
            ? credential?.UserName ?? string.Empty
            : _request.UserName;

        AuthenticationMethod metodo;

        if (_request.AuthMethod == SshAuthMethod.PrivateKey)
        {
            if (string.IsNullOrWhiteSpace(_request.PrivateKeyPath))
            {
                throw new PrivateKeyMissingException("No se indicó la ruta de la clave privada.");
            }

            if (!File.Exists(_request.PrivateKeyPath))
            {
                throw new PrivateKeyMissingException(
                    $"No se encontró la clave privada en {_request.PrivateKeyPath}.");
            }

            if (!string.IsNullOrWhiteSpace(_request.CertificatePath)
                && !File.Exists(_request.CertificatePath))
            {
                throw new CertificateMissingException(
                    $"No se encontró el certificado en {_request.CertificatePath}.");
            }

            var passphrase = credential?.HasSecret == true ? credential.RevealSecret() : null;

            PrivateKeyFile archivo;

            if (!string.IsNullOrWhiteSpace(_request.CertificatePath))
            {
                try
                {
                    archivo = new PrivateKeyFile(
                        _request.PrivateKeyPath, passphrase, _request.CertificatePath);
                }
                catch (ArgumentException ex)
                {
                    throw new CertificateMismatchException(
                        $"El certificado de {_request.CertificatePath} no corresponde a la "
                        + "clave privada configurada.", ex);
                }
            }
            else
            {
                archivo = string.IsNullOrEmpty(passphrase)
                    ? new PrivateKeyFile(_request.PrivateKeyPath)
                    : new PrivateKeyFile(_request.PrivateKeyPath, passphrase);
            }

            metodo = new PrivateKeyAuthenticationMethod(usuario, archivo);
        }
        else if (credential?.HasSecret == true)
        {
            metodo = new PasswordAuthenticationMethod(usuario, credential.RevealSecret());
        }
        else if (_contraseñaInteractiva is { } yaEscrita)
        {
            metodo = new PasswordAuthenticationMethod(usuario, yaEscrita);
        }
        else
        {
            metodo = BuildInteractiveAuthenticationMethod(usuario);
        }

        return new ConnectionInfo(_request.Host, _request.Port, usuario, metodo)
        {
            Timeout = TimeSpan.FromSeconds(_request.TimeoutSeconds),
        };
    }

    internal static bool HayQueReintentarConContraseña(
        Exception ex, bool preguntoPorTeclado, bool haySecretoGuardado, bool hayQuienPregunte) =>
        ex is Renci.SshNet.Common.SshAuthenticationException
        && !preguntoPorTeclado
        && !haySecretoGuardado
        && hayQuienPregunte;

    private AuthenticationMethod BuildInteractiveAuthenticationMethod(string usuario)
    {
        if (_interactivePasswordPrompt is not { } prompt)
        {
            return new PasswordAuthenticationMethod(usuario, string.Empty);
        }

        var metodo = new KeyboardInteractiveAuthenticationMethod(usuario);
        var intento = 0;
        string? errorPrevio = null;

        metodo.AuthenticationPrompt += (_, e) =>
        {
            intento++;

            if (intento > IntentosMaximosDeContraseña)
            {
                throw new TooManyPasswordAttemptsException(IntentosMaximosDeContraseña);
            }

            foreach (var pedido in e.Prompts)
            {
                if (pedido.IsEchoed)
                {
                    continue;
                }

                var respuesta = prompt
                    .PedirAsync(usuario, _request.Host, intento, errorPrevio, CancellationToken.None)
                    .ConfigureAwait(false)
                    .GetAwaiter()
                    .GetResult();

                if (respuesta is null)
                {
                    throw new InteractivePasswordCancelledException();
                }

                pedido.Response = respuesta;

                _preguntoPorTeclado = true;
                _contraseñaInteractiva = respuesta;

                errorPrevio = "Contraseña incorrecta.";
            }
        };

        return metodo;
    }

    /// <summary>Verificación de la clave del host: ocurre en el intercambio de claves, antes de enviar ninguna credencial (FR-022).</summary>
    private void OnHostKeyReceived(object? sender, HostKeyEventArgs e)
    {
        var fingerprint = "SHA256:" + e.FingerPrintSHA256;

        if (_acceptedFingerprint == fingerprint)
        {
            e.CanTrust = true;
            return;
        }

        if (HostKeyPolicy.YaEsConocida(fingerprint, _request.KnownHostFingerprint))
        {
            _rememberFingerprint = false;
            _acceptedFingerprint = fingerprint;
            e.CanTrust = true;
            return;
        }

        var decision = _verifier.Verify(
            _request.ConnectionId, _request.Host, fingerprint, _request.KnownHostFingerprint);

        _rememberFingerprint = decision == HostKeyDecision.AcceptAndRemember;
        e.CanTrust = decision != HostKeyDecision.Reject;

        if (e.CanTrust)
        {
            _acceptedFingerprint = fingerprint;
        }
    }

    private async Task ReadLoopAsync(CancellationToken ct)
    {
        var buffer = new byte[8192];

        try
        {
            while (!ct.IsCancellationRequested && _shell is { } shell)
            {
                var leidos = await shell.ReadAsync(buffer, ct).ConfigureAwait(false);

                if (leidos <= 0)
                {
                    break;
                }

                Interlocked.Add(ref _recibidos, leidos);

                DataReceived?.Invoke(this, buffer.AsMemory(0, leidos).ToArray());
            }

            if (!ct.IsCancellationRequested)
            {
                Anotar(
                    TipoDeTraza.Cierre,
                    $"ssh {_request.UserName}@{_request.Host}:{_request.Port}",
                    -1,
                    TimeSpan.Zero,
                    $"el servidor cerró la sesión · recibidos {BytesReceived} B · "
                    + $"enviados {BytesSent} B");

                Fail(new SessionFailure(
                    SessionFailureReason.UnexpectedDisconnect,
                    "El servidor cerró la sesión.",
                    "Podés reconectar desde la pestaña."));
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            var fallo = Map(ex);

            Anotar(
                TipoDeTraza.Cierre,
                $"ssh {_request.UserName}@{_request.Host}:{_request.Port}",
                -1,
                TimeSpan.Zero,
                $"sesión interrumpida · {fallo.UserMessage}",
                ex.Message);

            Fail(fallo);
        }
    }

    public void Send(ReadOnlySpan<byte> data)
    {
        if (_shell is not { } shell || State != SessionState.Connected)
        {
            return;
        }

        try
        {
            shell.Write(data.ToArray(), 0, data.Length);
            shell.Flush();

            Interlocked.Add(ref _enviados, data.Length);
        }
        catch (Exception ex)
        {
            Fail(Map(ex));
        }
    }

    public void Resize(int columns, int rows)
    {
        if (_shell is not { } shell || State != SessionState.Connected)
        {
            return;
        }

        try
        {
            shell.ChangeWindowSize((uint)columns, (uint)rows, 0, 0);
        }
        catch (Exception)
        {
        }
    }

    public async Task DisconnectAsync()
    {
        if (State == SessionState.Connected)
        {
            Anotar(
                TipoDeTraza.Cierre,
                $"ssh {_request.UserName}@{_request.Host}:{_request.Port}",
                0,
                TimeSpan.Zero,
                $"cerrada por el usuario · recibidos {BytesReceived} B · enviados {BytesSent} B");
        }

        if (_cts is { } cts)
        {
            await cts.CancelAsync().ConfigureAwait(false);
        }

        try
        {
            _shell?.Close();
            _shell?.Dispose();
        }
        catch (Exception)
        {
        }

        _shell = null;

        if (_reader is { } reader)
        {
            await Task.WhenAny(reader, Task.Delay(2000)).ConfigureAwait(false);
        }

        try
        {
            if (_client is { IsConnected: true })
            {
                _client.Disconnect();
            }
        }
        catch (Exception)
        {
        }

        ContrasenaDeSudo.Cerrar();

        SetState(SessionState.Disconnected);
    }

    /// <summary>Traduce la excepción a una causa del conjunto cerrado (FR-051).</summary>
    internal static SessionFailure Map(Exception ex, ConnectionInfo? info = null) => ex switch
    {
        PrivateKeyMissingException e => new SessionFailure(
            SessionFailureReason.PrivateKeyNotFound,
            e.Message,
            "Corregí la ruta de la clave en la conexión."),

        CertificateMissingException e => new SessionFailure(
            SessionFailureReason.CertificateNotFound,
            e.Message,
            "Corregí la ruta del certificado en la conexión."),

        CertificateMismatchException e => new SessionFailure(
            SessionFailureReason.CertificateMismatch,
            e.Message,
            "Verificá que el certificado esté firmado sobre esta clave privada, o quitalo si "
            + "no corresponde."),

        TooManyPasswordAttemptsException e => new SessionFailure(
            SessionFailureReason.AuthenticationRejected,
            e.Message,
            "Volvé a intentar y escribila con cuidado, o guardala para no tener que tipearla "
            + "de nuevo."),

        SshAuthenticationException e when EsPassphrase(e) => new SessionFailure(
            SessionFailureReason.BadPassphrase,
            "La passphrase no desbloquea la clave privada.",
            "Volvé a cargar la passphrase en la conexión."),

        SshAuthenticationException e when EsSinMetodoUtilizable(e) => new SessionFailure(
            SessionFailureReason.AuthenticationRejected,
            "El servidor no acepta autenticación por contraseña.",
            "Configurá esta conexión con clave privada, o habilitá la contraseña en el servidor."),

        SshAuthenticationException => new SessionFailure(
            SessionFailureReason.AuthenticationRejected,
            "El servidor rechazó las credenciales.",
            "Revisá el usuario y la contraseña de la conexión."),

        SshConnectionException e when EsFalloDeNegociacion(e) => MapFalloDeNegociacion(e, info),

        SshConnectionException e when e.Message.Contains("host key", StringComparison.OrdinalIgnoreCase)
            => new SessionFailure(
                SessionFailureReason.HostKeyMismatch,
                "La identidad del servidor no coincide con la conocida.",
                "No se envió ninguna credencial. Verificá el servidor antes de continuar."),

        System.Net.Sockets.SocketException => new SessionFailure(
            SessionFailureReason.HostUnreachable,
            "No se pudo alcanzar el servidor.",
            "Verificá el host, el puerto y la conectividad de red."),

        SshOperationTimeoutException or TimeoutException => new SessionFailure(
            SessionFailureReason.Timeout,
            "Se agotó el tiempo de espera al conectar.",
            "El servidor puede estar apagado o un firewall bloqueando el puerto."),

        Renci.SshNet.Common.ProxyException => new SessionFailure(
            SessionFailureReason.HostUnreachable,
            "No se pudo alcanzar el servidor.",
            "Verificá el host y el puerto."),

        ObjectDisposedException => new SessionFailure(
            SessionFailureReason.UnexpectedDisconnect,
            "La sesión se cerró.",
            "Podés reconectar desde la pestaña."),

        _ => new SessionFailure(
            SessionFailureReason.Other,
            "No se pudo establecer la sesión SSH.",
            "Revisá los datos de la conexión.",
            ex.GetType().Name),
    };

    private static bool EsSinMetodoUtilizable(SshAuthenticationException ex) =>
        ex.Message.Contains("no suitable authentication method", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("No suitable authentication", StringComparison.OrdinalIgnoreCase);

    private static bool EsPassphrase(SshAuthenticationException ex) =>
        ex.Message.Contains("passphrase", StringComparison.OrdinalIgnoreCase) ||
        ex.InnerException is System.Security.Cryptography.CryptographicException;

    // El texto son literales fijos de SSH.NET 2026.0.0 (ServiceFactory.CreateKeyExchange y KeyExchange): no hay tipo de excepción propio.
    private static bool EsFalloDeNegociacion(SshConnectionException ex) =>
        ex.Message.StartsWith("No matching ", StringComparison.Ordinal);

    private static SessionFailure MapFalloDeNegociacion(SshConnectionException ex, ConnectionInfo? info)
    {
        var (categoria, ofrecidoPorEsteCliente) = ex.Message switch
        {
            var m when m.Contains("key exchange algorithm", StringComparison.Ordinal) =>
                ("el algoritmo de intercambio de claves", info?.KeyExchangeAlgorithms.Keys),

            var m when m.Contains("host key algorithm", StringComparison.Ordinal) =>
                ("el algoritmo de clave de host", info?.HostKeyAlgorithms.Keys),

            var m when m.Contains("encryption algorithm", StringComparison.Ordinal) =>
                ("el algoritmo de cifrado", info?.Encryptions.Keys),

            var m when m.Contains("MAC algorithm", StringComparison.Ordinal) =>
                ("el algoritmo de MAC (verificación de integridad)", info?.HmacAlgorithms.Keys),

            var m when m.Contains("compression algorithm", StringComparison.Ordinal) =>
                ("el algoritmo de compresión", info?.CompressionAlgorithms.Keys),

            _ => ("un algoritmo de la conexión", null),
        };

        var mensaje = new StringBuilder(
            $"No se pudo acordar con el servidor {categoria}: no comparten ningún algoritmo en "
            + "común para eso.");

        var inicioOferta = ex.Message.IndexOf("(server offers ", StringComparison.Ordinal);

        if (inicioOferta >= 0)
        {
            var oferta = ex.Message[(inicioOferta + "(server offers ".Length)..].TrimEnd(')');
            mensaje.Append($" El servidor ofrece: {oferta}.");
        }

        if (ofrecidoPorEsteCliente is not null)
        {
            mensaje.Append($" Este cliente ofrece: {string.Join(", ", ofrecidoPorEsteCliente)}.");
        }

        return new SessionFailure(
            SessionFailureReason.AlgorithmNegotiationFailed,
            mensaje.ToString(),
            "El servidor suele ser viejo: revisá si tiene algoritmos más nuevos para habilitar, "
            + "o si hace falta permitir uno de los que ya ofrece.",
            ex.Message);
    }

    private void SetState(SessionState state)
    {
        State = state;
        StateChanged?.Invoke(this, new SessionStateChanged(state, Failure));
    }

    private void Fail(SessionFailure failure)
    {
        Failure = failure;
        State = SessionState.Error;
        StateChanged?.Invoke(this, new SessionStateChanged(SessionState.Error, failure));
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await DisconnectAsync().ConfigureAwait(false);
        }
        finally
        {
            ContrasenaDeSudo.Dispose();
        }

        _cts?.Dispose();
        _client?.Dispose();
        _client = null;
    }
}

public sealed class PrivateKeyMissingException : Exception
{
    public PrivateKeyMissingException(string message)
        : base(message)
    {
    }
}

public sealed class CertificateMissingException : Exception
{
    public CertificateMissingException(string message)
        : base(message)
    {
    }
}

public sealed class CertificateMismatchException : Exception
{
    public CertificateMismatchException(string message, Exception inner)
        : base(message, inner)
    {
    }
}

public interface IInteractivePasswordPrompt
{
    Task<string?> PedirAsync(
        string userName, string host, int intento, string? errorPrevio, CancellationToken ct);
}

public sealed class InteractivePasswordCancelledException : Exception
{
    public InteractivePasswordCancelledException()
        : base("El usuario canceló el pedido de contraseña.")
    {
    }
}

public sealed class TooManyPasswordAttemptsException : Exception
{
    public TooManyPasswordAttemptsException(int intentosMaximos)
        : base($"Se agotaron los {intentosMaximos} intentos de contraseña.")
    {
    }
}
