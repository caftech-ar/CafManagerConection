using System.Diagnostics;
using System.Text;
using CafManagerConection.Domain.Connections;
using CafManagerConection.Domain.Credentials;
using CafManagerConection.Platform;
using CafManagerConection.UseCases.Abstractions;
using Renci.SshNet;

namespace CafManagerConection.Ssh;

public sealed record CommandResult(int ExitCode, string Output, string Error)
{
    public bool Success => ExitCode == 0;

    // supervisorctl escribe su fallo de permisos en la salida estándar (error: <class 'PermissionError'>, [Errno 13]), no en la de error.
    public bool LooksLikePermissionDenied => MencionaFaltaDePermiso(Error)
                                             || MencionaFaltaDePermiso(Output);

    // supervisorctl status termina con estado 3 cuando hay procesos caídos, y eso no es un fallo de permisos.
    public bool NeedsSudoPassword =>
        Error.Contains("a password is required", StringComparison.OrdinalIgnoreCase) ||
        Error.Contains("no tty present", StringComparison.OrdinalIgnoreCase) ||
        Error.Contains("a terminal is required", StringComparison.OrdinalIgnoreCase) ||
        Error.Contains("askpass", StringComparison.OrdinalIgnoreCase);

    private static bool MencionaFaltaDePermiso(string texto) =>
        texto.Contains("permission denied", StringComparison.OrdinalIgnoreCase) ||

        texto.Contains("PermissionError", StringComparison.Ordinal) ||
        texto.Contains("[Errno 13]", StringComparison.Ordinal) ||
        texto.Contains("operation not permitted", StringComparison.OrdinalIgnoreCase) ||
        texto.Contains("must be root", StringComparison.OrdinalIgnoreCase) ||
        texto.Contains("dial unix /var/run/docker.sock", StringComparison.OrdinalIgnoreCase);
}

public sealed class SshCommandRunner : IAsyncDisposable, IPlatformLogStreamer
{
    private readonly SshSessionRequest _request;
    private readonly IHostKeyVerifier _verifier;
    private readonly StoredCredential? _credential;
    private readonly IAppLogger? _logger;
    private readonly IRegistroDeTrazas? _trazas;
    private readonly string _servidor;

    private readonly ContrasenaDeSudoDeSesion? _contrasenaDeSudo;
    private readonly OrdenDelReintentoDeSudo? _ordenDelReintento;

    private SshClient? _client;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public SshCommandRunner(
        SshSessionRequest request,
        IHostKeyVerifier verifier,
        StoredCredential? credential,
        IAppLogger? logger = null,
        IRegistroDeTrazas? trazas = null,
        string? servidor = null,
        ContrasenaDeSudoDeSesion? contrasenaDeSudo = null,
        IPedidoDeContrasenaDeSudo? pedidoDeContrasenaDeSudo = null)
    {
        _request = request;
        _verifier = verifier;
        _credential = credential;
        _logger = logger;
        _trazas = trazas;
        _servidor = servidor is { Length: > 0 }
            ? servidor
            : $"{request.UserName}@{request.Host}";

        _contrasenaDeSudo = contrasenaDeSudo;

        _ordenDelReintento = new OrdenDelReintentoDeSudo(
            ConLaContrasenaDeLaConexionAsync,
            ConUnaContrasenaAsync,
            () => _credential is { HasSecret: true },
            contrasenaDeSudo,
            pedidoDeContrasenaDeSudo,
            _servidor,
            request.UserName);
    }

    public bool IsConnected => _client is { IsConnected: true };

    public Exception? LastError { get; private set; }

    public async Task<bool> ConnectAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            if (IsConnected)
            {
                return true;
            }

            LastError = null;

            var sesion = new SshSession(_request, _verifier);
            _client = sesion.CreateClientForCommands(_credential);

            var reloj = Stopwatch.StartNew();

            try
            {
                await Task.Run(() => _client.Connect(), ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Anotar(TipoDeTraza.Conexion, Apertura(), -1, reloj.Elapsed, string.Empty, ex.Message);
                throw;
            }

            _logger?.WorkCompleted(
                _request.ConnectionId, RemoteWork.AuxiliaryHandshake, reloj.Elapsed);

            Anotar(
                TipoDeTraza.Conexion,
                Apertura(),
                0,
                reloj.Elapsed,
                $"autenticado por {_request.AuthMethod}",
                string.Empty);

            return _client.IsConnected;
        }
        catch (Exception ex)
        {
            LastError = ex;
            return false;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Ejecuta un comando con tiempo límite. Nunca lanza: devuelve el resultado con el error.</summary>
    public async Task<CommandResult> RunAsync(
        string command, int timeoutSeconds, CancellationToken ct = default)
    {
        command = command.ReplaceLineEndings("\n");

        if (!IsConnected && !await ConnectAsync(ct).ConfigureAwait(false))
        {
            Anotar(
                TipoDeTraza.Comando,
                command,
                -1,
                TimeSpan.Zero,
                string.Empty,
                LastError?.Message ?? "No hay conexión con el servidor.");

            return new CommandResult(-1, string.Empty, "No hay conexión con el servidor.");
        }

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        var reloj = Stopwatch.StartNew();

        try
        {
            using var cmd = _client!.CreateCommand(command);
            cmd.CommandTimeout = TimeSpan.FromSeconds(timeoutSeconds);

            using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
            limite.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

            var salida = await Task.Run(() => cmd.Execute(), limite.Token).ConfigureAwait(false);

            var salidaNormalizada = salida.ReplaceLineEndings("\n");

            return Anotado(
                command,
                reloj.Elapsed,
                new CommandResult(cmd.ExitStatus ?? 0, salidaNormalizada, cmd.Error ?? string.Empty));
        }
        catch (OperationCanceledException)
        {
            return Anotado(
                command,
                reloj.Elapsed,
                new CommandResult(
                    -1, string.Empty, $"El comando excedió los {timeoutSeconds} segundos."));
        }
        catch (Exception ex)
        {
            return Anotado(
                command, reloj.Elapsed, new CommandResult(-1, string.Empty, ex.Message));
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<CommandResult> RunWithSudoFallbackAsync(
        string command, int timeoutSeconds, CancellationToken ct = default)
    {
        var primero = await RunAsync(command, timeoutSeconds, ct).ConfigureAwait(false);

        if (primero.Success || !primero.LooksLikePermissionDenied)
        {
            return primero;
        }

        var sinPreguntar = await RunAsync($"sudo -n {Envuelto(command)}", timeoutSeconds, ct)
            .ConfigureAwait(false);

        if (sinPreguntar.Success || !sinPreguntar.NeedsSudoPassword)
        {
            return sinPreguntar;
        }

        var conContrasena = _ordenDelReintento is null
            ? null
            : await _ordenDelReintento.IntentarAsync(command, timeoutSeconds, ct)
                .ConfigureAwait(false);

        if (conContrasena is null)
        {
            return sinPreguntar;
        }

        return conContrasena.Success
            ? conContrasena
            : Anotado(VerboDeEscalada(command), TimeSpan.Zero, conContrasena, TipoDeTraza.Escalada);
    }

    /// <summary>Prepara un comando para que <c>sudo</c> lo alcance entero y no sólo al primer tramo.</summary>
    private static string Envuelto(string command) => ShellPosix.ComoUnSoloComando(command);

    /// <summary>Lo que se anota de una escalada: el verbo y el resultado, nunca el secreto que fue por la entrada estándar (FR-184e, regla 3).</summary>
    private static string VerboDeEscalada(string command) =>
        $"sudo -S -k -p '' {Envuelto(command)}"
        + "\n« contraseña por la entrada estándar — no se registra »";

    private async Task<IntentoDeEscalada> ConLaContrasenaDeLaConexionAsync(
        string command, int timeoutSeconds, CancellationToken ct)
    {
        if (_credential is not { HasSecret: true } credencial)
        {
            return new IntentoDeEscalada(
                new CommandResult(-1, string.Empty, "La conexión no tiene contraseña."), false);
        }

        var copia = credencial.Secret.ToArray();

        try
        {
            return await ConUnaContrasenaAsync(command, timeoutSeconds, copia, ct)
                .ConfigureAwait(false);
        }
        finally
        {
            Array.Clear(copia);
        }
    }

    // La contraseña va por la entrada estándar y nunca en la línea de comando: ahí queda visible en el ps del servidor.
    private async Task<IntentoDeEscalada> ConUnaContrasenaAsync(
        string command, int timeoutSeconds, ReadOnlyMemory<char> contrasena, CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        var reloj = Stopwatch.StartNew();

        var elevado = $"sudo -S -k -p '' {Envuelto(command)}";
        var enviado = VerboDeEscalada(command);

        try
        {
            using var cmd = _client!.CreateCommand(elevado);
            cmd.CommandTimeout = TimeSpan.FromSeconds(timeoutSeconds);

            using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
            limite.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

            // BeginExecute abre el canal en este hilo. Con Execute() en un hilo del pool,
            // CreateInputStream corría antes y moría con «The input stream can be used only
            // during execution» (SshCommand.cs de SSH.NET 2026.0.0 lo tira si el canal es null).
            var ejecucion = cmd.BeginExecute();

            using (var entrada = cmd.CreateInputStream())
            using (var escritor = new StreamWriter(entrada, new UTF8Encoding(false)))
            {
                await escritor.WriteLineAsync(contrasena, ct).ConfigureAwait(false);
                await escritor.FlushAsync(ct).ConfigureAwait(false);
            }

            var salida = await Task.Run(() => cmd.EndExecute(ejecucion), limite.Token)
                .ConfigureAwait(false);
            var crudo = cmd.Error ?? string.Empty;

            var resultado = SinElSecreto.En(
                new CommandResult(cmd.ExitStatus ?? 0, salida, LimpiarSudo(crudo)),
                contrasena.Span);

            return new IntentoDeEscalada(
                Anotado(enviado, reloj.Elapsed, resultado, TipoDeTraza.Escalada),
                SudoRechazoLaContrasena(crudo));
        }
        catch (OperationCanceledException)
        {
            return new IntentoDeEscalada(
                Anotado(
                    enviado,
                    reloj.Elapsed,
                    new CommandResult(
                        -1, string.Empty, $"El comando excedió los {timeoutSeconds} segundos."),
                    TipoDeTraza.Escalada),
                false);
        }
        catch (Exception ex)
        {
            return new IntentoDeEscalada(
                Anotado(
                    enviado,
                    reloj.Elapsed,
                    new CommandResult(
                        -1, string.Empty, SinElSecreto.En(ex.Message, contrasena.Span)),
                    TipoDeTraza.Escalada),
                false);
        }
        finally
        {
            _gate.Release();
        }
    }

    internal static bool SudoRechazoLaContrasena(string? error)
    {
        var texto = error ?? string.Empty;

        return texto.Contains("try again", StringComparison.OrdinalIgnoreCase)
               || texto.Contains("incorrect password", StringComparison.OrdinalIgnoreCase)
               || texto.Contains("authentication failure", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Texto con el que se describe la apertura del canal en la traza.</summary>
    private string Apertura() =>
        $"abrir canal de comandos → {_request.UserName}@{_request.Host}:{_request.Port}";

    /// <summary>Anota el resultado en la traza y lo devuelve, para poder envolver los <c>return</c>.</summary>
    private CommandResult Anotado(
        string enviado, TimeSpan tardo, CommandResult resultado, TipoDeTraza? tipo = null)
    {
        Anotar(
            tipo ?? (enviado.StartsWith("sudo ", StringComparison.Ordinal)
                ? TipoDeTraza.Escalada
                : TipoDeTraza.Comando),
            enviado,
            resultado.ExitCode,
            tardo,
            resultado.Output,
            resultado.Error);

        return resultado;
    }

    private void Anotar(
        TipoDeTraza tipo,
        string enviado,
        int? codigo,
        TimeSpan tardo,
        string salida,
        string error)
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

    private static string LimpiarSudo(string? error)
    {
        var texto = (error ?? string.Empty).Trim();

        if (SudoRechazoLaContrasena(texto))
        {
            return "sudo rechazó la contraseña en este servidor.";
        }

        if (texto.Contains("is not in the sudoers", StringComparison.OrdinalIgnoreCase))
        {
            return "El usuario no tiene permiso de sudo en este servidor.";
        }

        return texto;
    }

    /// <summary>Abre <paramref name="command"/> en una conexión SSH propia y va entregando líneas mientras corre: sirve igual a <c>docker logs -f</c> y a <c>tail -F</c> (FR-150, FR-185).</summary>
    async Task<IAsyncDisposable> IPlatformLogStreamer.SeguirAsync(
        string command, Action<string> onLinea, Action<string?> onCerrado, CancellationToken ct)
    {
        command = command.ReplaceLineEndings("\n");

        var sesion = new SshSession(_request, _verifier);
        var cliente = sesion.CreateClientForCommands(_credential);
        cliente.KeepAliveInterval = TimeSpan.FromSeconds(20);

        var reloj = Stopwatch.StartNew();

        try
        {
            await Task.Run(() => cliente.Connect(), ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            cliente.Dispose();

            Anotar(
                TipoDeTraza.Conexion,
                "abrir canal de registro en vivo",
                -1,
                reloj.Elapsed,
                string.Empty,
                ex.Message);

            throw new InvalidOperationException(
                "No se pudo abrir el canal de registro en vivo.", ex);
        }

        Anotar(
            TipoDeTraza.Conexion,
            $"abrir canal de registro en vivo → {command}",
            0,
            reloj.Elapsed,
            string.Empty,
            string.Empty);

        var comando = cliente.CreateCommand(command);

        comando.BeginExecute();

        var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var lector = Task.Run(
            () => LeerRegistroAsync(comando, onLinea, onCerrado, cts.Token), CancellationToken.None);

        return new CanalDeRegistro(cliente, comando, cts, lector, this);
    }

    private static async Task LeerRegistroAsync(
        SshCommand comando, Action<string> onLinea, Action<string?> onCerrado, CancellationToken ct)
    {
        string? motivo = null;
        var pidieronCerrar = false;

        try
        {
            using var lector = new StreamReader(
                comando.OutputStream, Encoding.UTF8, false, 4096, leaveOpen: true);

            while (true)
            {
                var linea = await lector.ReadLineAsync(ct).ConfigureAwait(false);

                if (linea is null)
                {
                    motivo = "El servidor cerró el canal: dejó de llegar el registro.";
                    break;
                }

                onLinea(linea);
            }
        }
        catch (OperationCanceledException)
        {
            pidieronCerrar = true;
        }
        catch (ObjectDisposedException)
        {
            pidieronCerrar = true;
        }
        catch (IOException ex)
        {
            motivo = ct.IsCancellationRequested
                ? null
                : $"El canal de registro se cortó: {ex.Message}";
            pidieronCerrar = ct.IsCancellationRequested;
        }
        catch (Exception ex)
        {
            motivo = $"El canal de registro se cortó: {ex.Message}";
        }

        if (!pidieronCerrar)
        {
            onCerrado(motivo);
        }
    }

    /// <summary>Manija de un canal de registro en vivo: desecharla es lo único que corta el <c>docker logs -f</c> o el <c>tail -F</c> del lado del servidor.</summary>
    private sealed class CanalDeRegistro(
        SshClient cliente,
        SshCommand comando,
        CancellationTokenSource cts,
        Task lector,
        SshCommandRunner dueno) : IAsyncDisposable
    {
        private int _cerrado;

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _cerrado, 1) != 0)
            {
                return;
            }

            await cts.CancelAsync().ConfigureAwait(false);

            try
            {
                if (cliente.IsConnected)
                {
                    cliente.Disconnect();
                }
            }
            catch (Exception)
            {
            }

            try
            {
                await lector.ConfigureAwait(false);
            }
            catch (Exception)
            {
            }

            dueno.Anotar(
                TipoDeTraza.Cierre, "cerrar canal de registro en vivo", 0, TimeSpan.Zero,
                string.Empty, string.Empty);

            comando.Dispose();
            cliente.Dispose();
            cts.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);

        try
        {
            if (_client is { IsConnected: true })
            {
                _client.Disconnect();

                Anotar(
                    TipoDeTraza.Cierre,
                    "cerrar canal de comandos",
                    0,
                    TimeSpan.Zero,
                    string.Empty,
                    string.Empty);
            }

            _client?.Dispose();
            _client = null;
        }
        catch (Exception)
        {
        }
        finally
        {
            _contrasenaDeSudo?.Cerrar();
            _gate.Release();
            _gate.Dispose();
        }
    }
}

/// <summary>Quita de un texto la contraseña que se le pasó a <c>sudo</c>, antes de que llegue a la traza, al registro o a un mensaje de error (FR-184e, regla 3).</summary>
internal static class SinElSecreto
{
    public const string Omitido = "«contraseña omitida»";

    public static CommandResult En(CommandResult resultado, ReadOnlySpan<char> secreto) =>
        secreto.IsEmpty
            ? resultado
            : resultado with
            {
                Output = En(resultado.Output, secreto),
                Error = En(resultado.Error, secreto),
            };

    // Se recorre por spans y no con string.Replace(new string(secreto), ...): materializar el secreto en una cadena lo deja en el montón sin poder pisarlo.
    public static string En(string texto, ReadOnlySpan<char> secreto)
    {
        if (secreto.IsEmpty || texto.Length < secreto.Length)
        {
            return texto;
        }

        var resto = texto.AsSpan();

        if (resto.IndexOf(secreto, StringComparison.Ordinal) < 0)
        {
            return texto;
        }

        var limpio = new StringBuilder(texto.Length);

        while (true)
        {
            var donde = resto.IndexOf(secreto, StringComparison.Ordinal);

            if (donde < 0)
            {
                limpio.Append(resto);
                return limpio.ToString();
            }

            limpio.Append(resto[..donde]).Append(Omitido);
            resto = resto[(donde + secreto.Length)..];
        }
    }
}

internal readonly record struct IntentoDeEscalada(
    CommandResult Resultado, bool ContrasenaRechazada);

/// <summary>El orden que fija FR-184e: primero la contraseña de la conexión, después —una sola vez por sesión— la que escriba el usuario, y si ninguna sirve la escalada queda imposible.</summary>
internal sealed class OrdenDelReintentoDeSudo(
    Func<string, int, CancellationToken, Task<IntentoDeEscalada>> conLaDeLaConexion,
    Func<string, int, ReadOnlyMemory<char>, CancellationToken, Task<IntentoDeEscalada>>
        conUnaContrasena,
    Func<bool> hayContrasenaDeLaConexion,
    ContrasenaDeSudoDeSesion? deSesion,
    IPedidoDeContrasenaDeSudo? pedido,
    string servidor,
    string usuario)
{
    public const string SudoLaRechazo =
        "sudo rechazó la contraseña: la escalada queda imposible y no se vuelve a pedir en esta "
        + "sesión, porque repetir una contraseña equivocada bloquea la cuenta.";

    public const string SeCancelo =
        "Sin la contraseña de sudo la escalada queda imposible. No se vuelve a pedir en esta "
        + "sesión: cerrá y volvé a abrir la conexión para que se pida de nuevo.";

    private readonly SemaphoreSlim _puertaDelPedido = new(1, 1);

    private bool _laDeLaConexionYaFallo;
    private string? _motivo;

    /// <returns><c>null</c> cuando no quedaba nada por probar, y el resultado —o el motivo de que la escalada sea imposible— cuando sí.</returns>
    public async Task<CommandResult?> IntentarAsync(
        string command, int timeoutSeconds, CancellationToken ct)
    {
        if (_motivo is { } yaSabido)
        {
            return new CommandResult(-1, string.Empty, yaSabido);
        }

        for (var vuelta = 0; vuelta < 3; vuelta++)
        {
            if (deSesion is { Tiene: true } guardada)
            {
                var prestada = guardada.Prestada();

                var intento = await conUnaContrasena(command, timeoutSeconds, prestada, ct)
                    .ConfigureAwait(false);

                if (intento.Resultado.Success || !intento.ContrasenaRechazada)
                {
                    return SinElSecreto.En(intento.Resultado, prestada.Span);
                }

                guardada.Descartar();

                return NoSePudo(SudoLaRechazo);
            }

            if (hayContrasenaDeLaConexion() && !_laDeLaConexionYaFallo)
            {
                var intento = await conLaDeLaConexion(command, timeoutSeconds, ct)
                    .ConfigureAwait(false);

                if (intento.Resultado.Success || !intento.ContrasenaRechazada)
                {
                    return intento.Resultado;
                }

                _laDeLaConexionYaFallo = true;
                continue;
            }

            if (deSesion is null || pedido is null)
            {
                return null;
            }

            await PedirUnaSolaVezAsync(ct).ConfigureAwait(false);

            if (!deSesion.Tiene)
            {
                return NoSePudo(SeCancelo);
            }
        }

        return null;
    }

    // La puerta es lo que hace que dos paneles escalando a la vez no abran dos ventanas ni gasten dos intentos de sudo.
    private async Task PedirUnaSolaVezAsync(CancellationToken ct)
    {
        await _puertaDelPedido.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            if (deSesion is not { } destino || pedido is null || destino.Tiene || destino.YaSePidio)
            {
                return;
            }

            destino.MarcarPedida();

            if (!await pedido.PedirAsync(servidor, usuario, destino, ct).ConfigureAwait(false))
            {
                destino.Descartar();
            }
        }
        finally
        {
            _puertaDelPedido.Release();
        }
    }

    private CommandResult NoSePudo(string porque)
    {
        _motivo = porque;

        return new CommandResult(-1, string.Empty, porque);
    }
}
