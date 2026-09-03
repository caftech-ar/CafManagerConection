using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using CafManagerConection.App.Bootstrap;
using CafManagerConection.App.Panels;
using CafManagerConection.App.Services;
using CafManagerConection.Domain.Connections;
using CafManagerConection.Domain.Credentials;
using CafManagerConection.Domain.Sessions;
using CafManagerConection.Platform;
using CafManagerConection.Rdp;
using CafManagerConection.Ssh;
using CafManagerConection.Terminal;
using CafManagerConection.UseCases.Abstractions;
using CafManagerConection.UseCases.Connections;
using CafManagerConection.UseCases.Inheritance;

namespace CafManagerConection.App.Views;

/// <summary>Una sesión abierta: el terminal o el cliente RDP, con los paneles del servidor al costado.</summary>
[SupportedOSPlatform("windows")]
public partial class SessionView : UserControl, IHostKeyVerifier, IDisposable
{
    private const double AnchoPanel = 460;

    private readonly CompositionRoot _root;
    private readonly ConnectionRecord _registro;
    private readonly Dictionary<TipoPanel, FrameworkElement> _paneles = [];

    /// <summary>Ancho recordado de cada panel, en memoria para no consultar la base al abrirlo.</summary>
    private Dictionary<string, double> _anchosDePanel = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<TipoPanel, ToggleButton> _accesos = [];

    private SshSession? _ssh;
    private RdpSession? _rdp;
    private TerminalControl? _terminal;
    private SshCommandRunner? _comandos;
    private TunnelHost? _tuneles;
    private PlatformInventory? _inventario;
    private SshSessionRequest? _peticionSsh;
    private StoredCredential? _credencial;
    private StatusPanel? _panelEstado;

    /// <summary>Lo que se está tipeando ahora mismo para el pedido de contraseña, o null cuando no hay ningún pedido en curso.</summary>
    private EntradaDeContrasenaInteractiva? _entradaDeContraseña;

    /// <summary>A quién avisarle cuando el pedido de contraseña en curso termine.</summary>
    private TaskCompletionSource<string?>? _tcsContraseña;

    /// <summary>La contraseña que se acaba de escribir a mano, a la espera de saber si la conexión funcionó para poder ofrecer guardarla. Se borra de memoria apenas se usa, sea cual sea el desenlace.</summary>
    private char[]? _contraseñaInteractivaPendiente;

    private string? _usuarioInteractivoPendiente;

    /// <summary>Conexión SFTP del panel de Archivos, cuando se abrió alguna vez.</summary>
    private RemoteFileSession? _archivos;
    private TipoPanel? _abierto;

    /// <summary>Si hay un panel armándose. Cierra la puerta al doble clic.</summary>
    private bool _abriendo;

    /// <summary>Si hay una reconexión en curso. Un segundo clic no arranca otra.</summary>
    private bool _reconectando;
    private bool _dispuesto;

    /// <summary>El contenedor de WinForms que aloja el control de RDP; es lo que se mueve entre la pestaña y la ventana propia.</summary>
    private System.Windows.Forms.Control? _contenedorRdp;

    private VentanaDeSesion? _ventanaPropia;
    private RecorteDeLaVentana? _recorte;

    /// <summary>La identidad de Windows ya falló en esta pestaña: el próximo intento pide credenciales (FR-186).</summary>
    private bool _identidadDeWindowsDescartada;

    /// <summary>Espera a que el tamaño se asiente antes de pedirle al servidor la resolución nueva.</summary>
    private readonly DispatcherTimer _demoraDeResolucion =
        new() { Interval = TimeSpan.FromMilliseconds(400) };

    /// <summary>Demora antes de aplicar lo que se escribió en la barra de búsqueda (FR-144).</summary>
    private readonly DispatcherTimer _demoraBusqueda = new() { Interval = TimeSpan.FromMilliseconds(150) };

    public SessionView(CompositionRoot root, ConnectionRecord registro)
    {
        _root = root;
        _registro = registro;

        InitializeComponent();

        _demoraBusqueda.Tick += (_, _) =>
        {
            _demoraBusqueda.Stop();
            EjecutarBusqueda();
        };

        _demoraDeResolucion.Tick += (_, _) =>
        {
            _demoraDeResolucion.Stop();
            AjustarResolucionRemota();
        };
    }

    public Guid ConnectionId => _registro.Connection.Id;

    /// <summary>Nombre con el que se abrió esta sesión, el mismo que muestra su pestaña.</summary>
    public string Nombre => _registro.Connection.Name;

    public SessionState State { get; private set; } = SessionState.Disconnected;

    /// <summary>Lo que tardó el saludo en abrir esta sesión. <c>null</c> mientras no haya conectado.</summary>
    public TimeSpan? TardoEnAbrir { get; private set; }

    /// <summary>Hora local en la que quedó conectada, para poder decir hace cuánto.</summary>
    public DateTimeOffset? AbiertaA { get; private set; }

    /// <summary>Bytes recibidos y enviados en esta sesión, o null si no aplica.</summary>
    public (long Recibidos, long Enviados)? Transferencia =>
        _ssh is { } ssh ? (ssh.BytesReceived, ssh.BytesSent) : null;

    /// <summary>Cuántos túneles de esta sesión están activos ahora mismo (FR-109).</summary>
    public async Task<int> ContarTunelesActivosAsync()
    {
        if (_tuneles is not { } tuneles)
        {
            return 0;
        }

        var definidos = await _root.Tunnels
            .GetForConnectionAsync(ConnectionId).ConfigureAwait(true);

        return definidos.Count(t => tuneles.IsActive(t.Id));
    }

    public event EventHandler<SessionStateChanged>? StateChanged;

    /// <summary>Se apretó F12 con el foco dentro de la sesión.</summary>
    public event EventHandler? PidioConsola;

    /// <summary>Algo que contar en la barra de estado de la ventana.</summary>
    public event EventHandler<string>? Informo;

    public async Task ConnectAsync()
    {
        _reintentar.Visibility = Visibility.Collapsed;
        _aviso.Visibility = Visibility.Visible;
        _mensaje.Text = "Conectando…";
        CambiarEstado(SessionState.Connecting, null);

        _anchosDePanel = new Dictionary<string, double>(
            await _root.AppSettings.GetPanelWidthsAsync().ConfigureAwait(true),
            StringComparer.OrdinalIgnoreCase);

        var resolver = await _root.ConnectionService.CreateResolverAsync().ConfigureAwait(true);
        var efectivo = resolver.Resolve(_registro.Connection, _registro.Rdp, _registro.Ssh);

        var conIdentidadDeWindows =
            efectivo.ResolvedUseWindowsIdentity && !_identidadDeWindowsDescartada;

        _credencial?.Dispose();
        _credencial = null;

        if (!conIdentidadDeWindows)
        {
            _credencial = _registro.Connection.Protocol == Protocol.Ssh
                          && efectivo.ResolvedAuthMethod == SshAuthMethod.Password
                ? await ObtenerCredencialGuardadaSinPedirAsync(efectivo).ConfigureAwait(true)
                : await _root.CredentialProvider
                    .GetForConnectionAsync(ConnectionId).ConfigureAwait(true);
        }

        var inicio = DateTimeOffset.UtcNow;

        _root.Logger.ConnectionOpening(
            ConnectionId,
            _registro.Connection.Protocol.ToString(),
            _registro.Connection.Host,
            efectivo.ResolvedPort);

        if (_registro.Connection.Protocol == Protocol.Rdp)
        {
            ConectarRdp(efectivo, inicio, conIdentidadDeWindows);
        }
        else
        {
            await ConectarSshAsync(efectivo, inicio).ConfigureAwait(true);
        }
    }

    /// <summary>Busca la credencial ya guardada para una conexión SSH por contraseña, sin preguntar nada si no está.</summary>
    private async Task<StoredCredential?> ObtenerCredencialGuardadaSinPedirAsync(
        EffectiveSettings efectivo)
    {
        if (efectivo.CredentialKey.Value is not { } clave)
        {
            return null;
        }

        return await _root.Credentials.ReadAsync(clave).ConfigureAwait(true);
    }

    private void AlReintentar(object sender, RoutedEventArgs e) => _ = ConnectAsync();

    /// <summary>Cierra la conexión y la vuelve a abrir, sin cerrar la pestaña.</summary>
    public async Task ReconectarAsync()
    {
        if (_dispuesto || _reconectando)
        {
            return;
        }

        _reconectando = true;

        try
        {
            Informar("Cerrando la conexión…");

            DesarmarParaReconectar();

            await ConnectAsync().ConfigureAwait(true);
        }
        finally
        {
            _reconectando = false;
        }
    }

    /// <summary>Deja la sesión como si nunca se hubiera conectado, salvo la credencial y la huella.</summary>
    private void DesarmarParaReconectar()
    {
        CerrarPanel();

        _marcoSesion.SizeChanged -= AlCambiarElTamanoDeLaSesion;
        _demoraDeResolucion.Stop();

        Aislar(() => _ventanaPropia?.SoltarYCerrar());
        _ventanaPropia = null;

        Aislar(() => _panelEstado?.Detener());
        _panelEstado = null;

        Aislar(() =>
        {
            if (_tuneles is { } tuneles)
            {
                _ = tuneles.DisposeAsync();
            }
        });

        _tuneles = null;

        Aislar(() =>
        {
            if (_comandos is { } comandos)
            {
                _ = comandos.DisposeAsync();
            }
        });

        _comandos = null;
        _inventario = null;

        _tunelesDefinidos = [];

        Aislar(() =>
        {
            if (_ssh is { } ssh)
            {
                _ = ssh.DisposeAsync();
            }
        });

        _ssh = null;

        Aislar(() => _rdp?.Dispose());
        _rdp = null;

        Aislar(() => _contenedorRdp?.Dispose());
        _contenedorRdp = null;

        Aislar(() => _terminal?.Dispose());
        _terminal = null;

        Aislar(() =>
        {
            if (_archivos is { } archivos)
            {
                _ = archivos.DisposeAsync();
            }
        });

        _archivos = null;
        _paneles.Clear();
        _accesos.Clear();
        _barra.Children.Clear();
        _marcoBarra.Visibility = Visibility.Collapsed;

        _barraSesion.Visibility = Visibility.Collapsed;
        _marcoSesion.Visibility = Visibility.Collapsed;
        _host.Child = null;

        _demoraBusqueda.Stop();
        _barraBusqueda.Visibility = Visibility.Collapsed;
    }

    private async Task ConectarSshAsync(EffectiveSettings efectivo, DateTimeOffset inicio)
    {
        _terminal = new TerminalControl { Dock = System.Windows.Forms.DockStyle.Fill };

        var negro = TerminalPalette.Dark.Background;

        _marcoSesion.Background = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(negro.R, negro.G, negro.B));

        _host.Margin = new Thickness(14, 6, 0, 10);
        _host.Child = _terminal;

        ArmarBarraDeAcciones();

        var prefs = await _root.AppSettings.GetTerminalPreferencesAsync().ConfigureAwait(true);

        _terminal.ApplyTheme(dark: true, prefs.FontFamily, prefs.FontSize, prefs.ScrollbackLines);

        var peticion = new SshSessionRequest(
            ConnectionId,
            _registro.Connection.Host,
            efectivo.ResolvedPort,
            efectivo.UserName.Value ?? string.Empty,
            efectivo.ResolvedAuthMethod,
            efectivo.PrivateKeyPath.Value,
            _registro.Ssh?.KnownHostFingerprint,
            efectivo.ResolvedKeepAliveSeconds,
            _terminal.Columns,
            _terminal.Rows,
            Domain.Settings.Defaults.ConnectionTimeoutSeconds,

            CertificatePath: efectivo.CertificatePath.Value);

        _peticionSsh = peticion;
        _ssh = new SshSession(
            peticion,
            this,
            _root.Logger,
            new PedidoDeContraseñaEnConsola(this),
            _root.Trazas,
            _registro.Connection.Name);

        _ssh.DataReceived += (_, datos) => _terminal?.Write(datos);

        _terminal.UserInput += (_, bytes) =>
        {
            if (_entradaDeContraseña is { } entrada)
            {
                AlTipearEnPedidoDeContraseña(entrada, bytes);
                return;
            }

            _ssh?.Send(bytes);
        };
        _terminal.PidioDiagnostico += (_, _) => PidioConsola?.Invoke(this, EventArgs.Empty);
        _terminal.PidioPaleta += (_, _) => Dispatcher.BeginInvoke(AbrirPaleta);
        _terminal.PidioBusqueda += (_, _) => Dispatcher.BeginInvoke(AbrirBusqueda);
        _terminal.PidioConfirmarPegado += AlPedirConfirmacionDePegado;

        _terminal.CambioElZoom += (_, puntos) => Dispatcher.BeginInvoke(() =>
        {
            Informar($"Letra en {puntos:0} pt");
            _ = GuardarTamanoDeLetraAsync(puntos);
        });

        _terminal.PidioZoomDeOrigen += (_, _) => Dispatcher.BeginInvoke(
            () => _ = VolverAlTamanoGuardadoAsync());
        _terminal.SizeChangedInCells += (_, tam) => _ssh?.Resize(tam.Columns, tam.Rows);

        var sesion = _ssh;

        sesion.StateChanged += (_, cambio) => Dispatcher.Invoke(() =>
        {
            if (!ReferenceEquals(sesion, _ssh))
            {
                return;
            }

            AlCambiarEstadoSsh(cambio, inicio);
        });

        await _ssh.ConnectAsync(_credencial).ConfigureAwait(true);
    }

    /// <summary>Adaptador entre SshSession y el terminal de esta vista.</summary>
    private sealed class PedidoDeContraseñaEnConsola(SessionView vista) : IInteractivePasswordPrompt
    {
        public Task<string?> PedirAsync(
            string userName, string host, int intento, string? errorPrevio, CancellationToken ct)
            => vista.PedirContraseñaInteractivaAsync(userName, host, errorPrevio);
    }

    /// <summary>Arranca el pedido en el hilo de interfaz y devuelve una tarea que se completa recién cuando el usuario aprieta Enter o Escape.</summary>
    private Task<string?> PedirContraseñaInteractivaAsync(
        string userName, string host, string? errorPrevio)
    {
        var tcs = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);

        Dispatcher.BeginInvoke(() => IniciarEntradaDeContraseña(userName, host, errorPrevio, tcs));

        return tcs.Task;
    }

    private void IniciarEntradaDeContraseña(
        string userName, string host, string? errorPrevio, TaskCompletionSource<string?> tcs)
    {
        if (_dispuesto || _terminal is not { } terminal)
        {
            tcs.TrySetResult(null);
            return;
        }

        _aviso.Visibility = Visibility.Collapsed;
        _marcoSesion.Visibility = Visibility.Visible;

        var texto = new StringBuilder();

        if (errorPrevio is not null)
        {
            texto.Append(errorPrevio).Append("\r\n");
        }

        texto.Append($"Contraseña de {userName}@{host}: ");

        terminal.Write(Encoding.UTF8.GetBytes(texto.ToString()));
        terminal.Focus();

        _usuarioInteractivoPendiente = userName;
        _tcsContraseña = tcs;
        _entradaDeContraseña = new EntradaDeContrasenaInteractiva();
    }

    /// <summary>Recibe lo que se tecleó mientras hay un pedido de contraseña en curso. No se hace eco de nada —ni el texto ni asteriscos—, igual que PuTTY.</summary>
    private void AlTipearEnPedidoDeContraseña(EntradaDeContrasenaInteractiva entrada, byte[] bytes)
    {
        var resultado = entrada.Alimentar(bytes);

        if (resultado == ResultadoDeEntrada.Continua)
        {
            return;
        }

        _entradaDeContraseña = null;
        var tcs = _tcsContraseña;
        _tcsContraseña = null;

        _terminal?.Write("\r\n"u8.ToArray());

        if (resultado == ResultadoDeEntrada.Cancelada)
        {
            entrada.TomarTexto();
            tcs?.TrySetResult(null);
            return;
        }

        var contraseña = entrada.TomarTexto();

        _usuarioInteractivoPendiente ??= string.Empty;
        _contraseñaInteractivaPendiente = contraseña.ToCharArray();

        tcs?.TrySetResult(contraseña);
    }

    /// <summary>Nombre pendiente de ofrecer para guardar, una vez que ConsolidarCredencialInteractiva ya armó _credencial a partir de lo que se tipeó. null cuando no hay nada que ofrecer —incluida una conexión que nunca pidió nada por consola—.</summary>
    private string? _usuarioParaOfrecerGuardado;

    /// <summary>Pasa la contraseña recién tipeada de _contraseñaInteractivaPendiente a _credencial, síncronamente y apenas la sesión queda conectada.</summary>
    private void ConsolidarCredencialInteractiva()
    {
        if (_contraseñaInteractivaPendiente is not { } contraseña)
        {
            return;
        }

        _contraseñaInteractivaPendiente = null;
        var usuario = _usuarioInteractivoPendiente ?? string.Empty;
        _usuarioInteractivoPendiente = null;

        _credencial?.Dispose();
        _credencial = new StoredCredential(usuario, domain: null, contraseña);
        Array.Clear(contraseña);

        _usuarioParaOfrecerGuardado = usuario;
    }

    /// <summary>Borra de memoria cualquier contraseña tipeada por consola que haya quedado pendiente sin consolidarse en _credencial —porque la conexión terminó en error o se canceló antes de llegar a Conectada—.</summary>
    private void LimpiarContraseñaInteractivaPendiente()
    {
        if (_contraseñaInteractivaPendiente is { } contraseña)
        {
            Array.Clear(contraseña);
            _contraseñaInteractivaPendiente = null;
        }

        _usuarioInteractivoPendiente = null;
        _usuarioParaOfrecerGuardado = null;
    }

    /// <summary>Se va a pegar más de una línea sin que el servidor haya pedido el modo 2004 (FR-030f).</summary>
    private void AlPedirConfirmacionDePegado(
        object? origen, Terminal.TerminalControl.ConfirmacionDePegado pregunta)
    {
        if (Window.GetWindow(this) is not { } ventana)
        {
            return;
        }

        pregunta.Aceptado = Services.Dialogos.Confirmar(
            ventana,
            "Pegar varias líneas",
            $"Se van a pegar {pregunta.Lineas} líneas en «{_registro.Connection.Name}». El servidor no está "
            + "esperando un pegado, así que cada salto de línea va a ejecutarse como si lo "
            + "hubieras tecleado.",
            "Pegar");
    }

    private async Task OfrecerGuardarCredencialAsync()
    {
        if (_usuarioParaOfrecerGuardado is not { } usuario)
        {
            return;
        }

        _usuarioParaOfrecerGuardado = null;

        await Task.Yield();

        if (_dispuesto || Window.GetWindow(this) is not { } ventana)
        {
            return;
        }

        var guardar = Services.Dialogos.Confirmar(
            ventana,
            "Guardar contraseña",
            $"¿Guardar la contraseña de «{usuario}» en el Administrador de credenciales de "
            + "Windows para no volver a pedirla la próxima vez que se conecte?",
            "Guardar");

        if (!guardar || _dispuesto || _credencial is not { HasSecret: true } credencial)
        {
            return;
        }

        try
        {
            var clave = CredentialKey.ForConnection(ConnectionId, Protocol.Ssh).Value;

            await _root.Credentials.WriteAsync(clave, credencial).ConfigureAwait(true);

            _registro.Connection.CredentialKey = clave;
            _registro.Connection.Touch();
            await _root.Connections.UpdateAsync(_registro).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _root.Logger.TechnicalError("guardar la contraseña interactiva", ex);
        }
    }

    private async Task GuardarTamanoDeLetraAsync(float puntos)
    {
        try
        {
            var prefs = await _root.AppSettings
                .GetTerminalPreferencesAsync().ConfigureAwait(true);

            await _root.AppSettings.SaveTerminalPreferencesAsync(
                prefs with { FontSize = (int)Math.Round(puntos) }).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _root.Logger.TechnicalError("guardar el tamaño de letra del terminal", ex);
        }
    }

    /// <summary>Ctrl+0: vuelve al tamaño que está en las preferencias.</summary>
    private async Task VolverAlTamanoGuardadoAsync()
    {
        if (_terminal is not { } terminal)
        {
            return;
        }

        var prefs = await _root.AppSettings.GetTerminalPreferencesAsync().ConfigureAwait(true);

        if (terminal.Zoom(prefs.FontSize - terminal.TamanoDeLetra))
        {
            Informar($"Letra en {prefs.FontSize} pt");
        }
    }

    private void AlCambiarEstadoSsh(SessionStateChanged cambio, DateTimeOffset inicio)
    {
        switch (cambio.State)
        {
            case SessionState.Connected when _terminal is { } terminal:
                _aviso.Visibility = Visibility.Collapsed;
                _marcoSesion.Visibility = Visibility.Visible;
                terminal.Focus();

                AnotarLaApertura(inicio);
                _root.Logger.ConnectionSucceeded(ConnectionId, TardoEnAbrir!.Value);

                ConsolidarCredencialInteractiva();

                _ = GuardarConexionExitosaAsync();
                _ = PrepararPanelesAsync();
                _ = LevantarTunelesAutomaticosAsync();
                _ = OfrecerGuardarCredencialAsync();
                break;

            case SessionState.Error:
                LimpiarContraseñaInteractivaPendiente();

                DesarmarParaReconectar();

                MostrarError(cambio.Failure);
                break;

            case SessionState.Disconnected:
                LimpiarContraseñaInteractivaPendiente();
                _marcoSesion.Visibility = Visibility.Collapsed;
                _aviso.Visibility = Visibility.Visible;
                _mensaje.Text = "Sesión cerrada.";
                _reintentar.Visibility = Visibility.Visible;
                break;
        }

        CambiarEstado(cambio.State, cambio.Failure);
    }

    /// <summary>Huella ya resuelta en esta sesión, con la decisión que se tomó.</summary>
    private (string Huella, HostKeyDecision Decision)? _huellaResuelta;

    public HostKeyDecision Verify(Guid connectionId, string host, string fingerprint, string? known)
    {
        if (_huellaResuelta is { } resuelta
            && string.Equals(resuelta.Huella, fingerprint, StringComparison.Ordinal))
        {
            return resuelta.Decision;
        }

        return Dispatcher.Invoke<HostKeyDecision>(() =>
        {
            var ventana = new HostKeyWindow(host, fingerprint, known)
            {
                Owner = Window.GetWindow(this),
            };

            if (ventana.ShowDialog() != true)
            {
                _huellaResuelta = (fingerprint, HostKeyDecision.Reject);
                return HostKeyDecision.Reject;
            }

            var decision = ventana.Recordar
                ? HostKeyDecision.AcceptAndRemember
                : HostKeyDecision.Accept;

            _huellaResuelta = (fingerprint, decision);
            return decision;
        });
    }

    private void ConectarRdp(
        EffectiveSettings efectivo, DateTimeOffset inicio, bool conIdentidadDeWindows)
    {
        var peticion = new RdpSessionRequest(
            ConnectionId,
            _registro.Connection.Host,
            efectivo.ResolvedPort,
            efectivo.UserName.Value ?? string.Empty,
            _registro.Rdp?.Domain,
            efectivo.ResolvedClipboardEnabled,
            efectivo.ResolvedFitToTab,
            efectivo.ResolvedIgnoreCertificateWarnings,
            Domain.Settings.Defaults.ConnectionTimeoutSeconds,
            conIdentidadDeWindows);

        _rdp = new RdpSession(peticion);
        _rdp.StateChanged += (_, cambio) =>
            Dispatcher.Invoke(() => AlCambiarEstadoRdp(cambio, inicio));

        if (_rdp.CrearControl(_credencial) is not { } control)
        {
            return;
        }

        var contenedor = new System.Windows.Forms.ContainerControl
        {
            Dock = System.Windows.Forms.DockStyle.Fill,
        };

        contenedor.Controls.Add(control);
        _contenedorRdp = contenedor;

        ArmarBarraDeRdp();

        if (_registro.Rdp?.StartFullScreen == true)
        {
            AbrirEnVentanaPropia(conSesionViva: false);
            ConectarRdpCuandoHayVentana();
            return;
        }

        _host.Margin = default;
        _host.Child = contenedor;
        contenedor.ActiveControl = control;
        _marcoSesion.Visibility = Visibility.Visible;
        _aviso.Visibility = Visibility.Collapsed;

        _marcoSesion.SizeChanged += AlCambiarElTamanoDeLaSesion;

        // Conectar se aplaza un turno: el ActiveX de RDP acepta Connect sin ventana real y Connected se queda en 0 hasta el tiempo límite.
        if (_host.IsLoaded)
        {
            ConectarRdpCuandoHayVentana();
        }
        else
        {
            void AlCargar(object? s, RoutedEventArgs e)
            {
                _host.Loaded -= AlCargar;
                ConectarRdpCuandoHayVentana();
            }

            _host.Loaded += AlCargar;
        }
    }

    /// <summary>Pide la conexión RDP una vez que el host tiene ventana de verdad.</summary>
    private void ConectarRdpCuandoHayVentana() =>
        Dispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.ContextIdle,
            new Action(() =>
            {
                if (!_dispuesto)
                {
                    _rdp?.PrepararYConectar(_credencial);
                }
            }));

    private void AlCambiarEstadoRdp(SessionStateChanged cambio, DateTimeOffset inicio)
    {
        var enLaPestana = _ventanaPropia is null;

        switch (cambio.State)
        {
            case SessionState.Connected:
                if (enLaPestana)
                {
                    _aviso.Visibility = Visibility.Collapsed;
                    _marcoSesion.Visibility = Visibility.Visible;
                }

                AnotarLaApertura(inicio);
                _root.Logger.ConnectionSucceeded(ConnectionId, TardoEnAbrir!.Value);
                _ = GuardarConexionExitosaAsync();
                break;

            case SessionState.Error:
                if (CaerAlPedidoDeCredenciales(cambio.Failure))
                {
                    return;
                }

                MostrarError(cambio.Failure);
                break;

            case SessionState.Disconnected:
                _marcoSesion.Visibility = Visibility.Collapsed;
                _aviso.Visibility = Visibility.Visible;
                _mensaje.Text = "Sesión cerrada.";
                _reintentar.Visibility = Visibility.Visible;
                break;
        }

        CambiarEstado(cambio.State, cambio.Failure);
    }

    /// <summary>Fuera del dominio o contra un servidor que no confía, la identidad de Windows se descarta y la conexión se reintenta pidiendo credenciales, que es lo que FR-186 exige en lugar de fallar.</summary>
    private bool CaerAlPedidoDeCredenciales(SessionFailure? fallo)
    {
        if (_dispuesto
            || _rdp is not { } rdp
            || fallo is null
            || !RdpSession.ConvieneCaerAlPedidoDeCredenciales(
                rdp.UsaIdentidadDeWindows, rdp.LlegoAConectar, fallo.Reason))
        {
            return false;
        }

        _identidadDeWindowsDescartada = true;

        _root.Logger.ConnectionFailed(ConnectionId, fallo.Reason, fallo.TechnicalDetail);

        _mensaje.Text = "El servidor no aceptó la identidad de Windows. "
                        + "Se piden las credenciales.";

        _aviso.Visibility = Visibility.Visible;
        _marcoSesion.Visibility = Visibility.Collapsed;
        _reintentar.Visibility = Visibility.Collapsed;

        // La reconexión espera un turno del despachador: el fallo llega del sondeo del propio ActiveX y desarmarlo dentro de ese callback lanza InvalidComObjectException (RdpClientHost.cs:189).
        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => _ = ReconectarAsync()));

        return true;
    }

    private System.Windows.Shapes.Path? _iconoPantallaCompleta;

    private void ArmarBarraDeRdp()
    {
        if (_accionesSesion.Children.Count == 0)
        {
            _iconoPantallaCompleta = AgregarAccionDeRdp(
                "IconoPantallaCompleta",
                "Pantalla completa dentro de la aplicación",
                AlternarPantallaCompleta);

            AgregarAccionDeRdp(
                "IconoVentanaPropia",
                "Sacar la sesión a una ventana propia",
                SacarAVentanaPropia);
        }

        _barraSesion.Visibility = Visibility.Visible;
    }

    private System.Windows.Shapes.Path AgregarAccionDeRdp(
        string icono, string ayuda, Action accion)
    {
        var dibujo = new System.Windows.Shapes.Path
        {
            Data = (System.Windows.Media.Geometry)FindResource(icono),
            Width = 15,
            Height = 15,
            Stretch = System.Windows.Media.Stretch.Uniform,
            Fill = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0xD4, 0xD4, 0xD8)),
        };

        var boton = new Button
        {
            Style = (Style)FindResource("AccionDeSesion"),
            Width = 30,
            Padding = new Thickness(0),
            ToolTip = ayuda,
            Content = dibujo,
        };

        boton.Click += (_, _) => accion();
        _accionesSesion.Children.Add(boton);

        return dibujo;
    }

    /// <summary>La sesión ocupa toda la ventana, sin el árbol al costado, y volver la deja como estaba sin reconectar: no mueve el control de lugar (FR-187).</summary>
    private void AlternarPantallaCompleta()
    {
        if (_recorte is { } recorte)
        {
            recorte.Deshacer();
            _recorte = null;
            MostrarIconoDePantallaCompleta(ampliada: false);
            Informar("Sesión restaurada");
            return;
        }

        if (_ventanaPropia is { } propia)
        {
            propia.WindowState = WindowState.Maximized;
            propia.Activate();
            return;
        }

        _recorte = RecorteDeLaVentana.Aplicar(this);

        if (_recorte is null)
        {
            Informar("No se pudo ampliar la sesión");
            return;
        }

        MostrarIconoDePantallaCompleta(ampliada: true);
        Informar("Sesión a pantalla completa; el mismo botón la devuelve");
    }

    private void MostrarIconoDePantallaCompleta(bool ampliada)
    {
        if (_iconoPantallaCompleta is { } dibujo)
        {
            dibujo.Data = (System.Windows.Media.Geometry)FindResource(
                ampliada ? "IconoRestaurarTamano" : "IconoPantallaCompleta");
        }
    }

    private void SacarAVentanaPropia()
    {
        if (_ventanaPropia is { } abierta)
        {
            abierta.Activate();
            return;
        }

        if (_recorte is { } recorte)
        {
            recorte.Deshacer();
            _recorte = null;
            MostrarIconoDePantallaCompleta(ampliada: false);
        }

        AbrirEnVentanaPropia(conSesionViva: State == SessionState.Connected);
    }

    /// <summary>Mueve el control de RDP a una ventana propia sin reconectar: los dos <c>WindowsFormsHost</c> viven en el mismo hilo de interfaz, que es lo que hace posible reparentar un ActiveX con afinidad de hilo.</summary>
    private void AbrirEnVentanaPropia(bool conSesionViva)
    {
        if (_contenedorRdp is not { } contenedor)
        {
            return;
        }

        var ventana = new VentanaDeSesion(_registro.Connection.Name)
        {
            Owner = Window.GetWindow(this),
        };

        ventana.Devolvio += (_, _) => DevolverDeLaVentanaPropia();

        _ventanaPropia = ventana;

        if (conSesionViva)
        {
            _rdp?.SuspenderVigilancia();
        }

        _marcoSesion.SizeChanged -= AlCambiarElTamanoDeLaSesion;
        _host.Child = null;

        ventana.Show();
        ventana.Alojar(contenedor);
        ventana.SizeChanged += AlCambiarElTamanoDeLaSesion;

        _marcoSesion.Visibility = Visibility.Collapsed;
        _aviso.Visibility = Visibility.Visible;
        _reintentar.Visibility = Visibility.Collapsed;
        _mensaje.Text = "Esta sesión está en su propia ventana. Cerrá esa ventana para "
                        + "devolverla a esta pestaña.";

        if (conSesionViva)
        {
            ComprobarQueElTrasladoNoCortoLaSesion();
        }
    }

    /// <summary>La pestaña queda reservada mientras la sesión está afuera; al cerrarse la ventana vuelve a alojarla.</summary>
    private void DevolverDeLaVentanaPropia()
    {
        _ventanaPropia = null;

        if (_dispuesto || _contenedorRdp is not { } contenedor)
        {
            return;
        }

        _rdp?.SuspenderVigilancia();

        _host.Margin = default;
        _host.Child = contenedor;
        _marcoSesion.Visibility = Visibility.Visible;
        _aviso.Visibility = Visibility.Collapsed;
        _marcoSesion.SizeChanged += AlCambiarElTamanoDeLaSesion;

        ComprobarQueElTrasladoNoCortoLaSesion();
    }

    /// <summary>Al segundo y medio se le pregunta al control si sigue conectado: si el traslado la cortó, se dice; no se reconecta por atrás (FR-187).</summary>
    private void ComprobarQueElTrasladoNoCortoLaSesion()
    {
        var reloj = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };

        reloj.Tick += (_, _) =>
        {
            reloj.Stop();

            if (_dispuesto || _rdp is not { } rdp)
            {
                return;
            }

            rdp.RetomarVigilancia();

            if (rdp.SigueConectado)
            {
                AjustarResolucionRemota();
                return;
            }

            AnotarQueElTrasladoNoSobrevive();
        };

        reloj.Start();
    }

    /// <summary>El intercambio en caliente no sobrevivió en este equipo: de acá en más la conexión arranca en su ventana propia y no se mueve.</summary>
    private void AnotarQueElTrasladoNoSobrevive()
    {
        Aislar(() => _ventanaPropia?.SoltarYCerrar());
        _ventanaPropia = null;

        _mensaje.Text = "Mover la sesión de ventana cortó la conexión con el servidor. "
                        + "De ahora en más esta conexión se abre directamente en su propia "
                        + "ventana, sin moverse.";

        _aviso.Visibility = Visibility.Visible;
        _marcoSesion.Visibility = Visibility.Collapsed;
        _reintentar.Visibility = Visibility.Visible;

        _root.Logger.ConnectionFailed(
            ConnectionId,
            SessionFailureReason.UnexpectedDisconnect,
            "El control de RDP no sobrevivió al cambio de ventana (FR-187, R1).");

        if (_registro.Rdp is { StartFullScreen: false } rdp)
        {
            rdp.StartFullScreen = true;
            _ = RecordarQueAbreEnVentanaPropiaAsync();
        }
    }

    private async Task RecordarQueAbreEnVentanaPropiaAsync()
    {
        try
        {
            await _root.Connections.UpdateAsync(_registro).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _root.Logger.TechnicalError("recordar que la sesión abre en ventana propia", ex);
        }
    }

    private void AlCambiarElTamanoDeLaSesion(object sender, SizeChangedEventArgs e)
    {
        _demoraDeResolucion.Stop();
        _demoraDeResolucion.Start();
    }

    /// <summary>Le pide al servidor la resolución del tamaño real del control, para que maximizar no deje una imagen escalada.</summary>
    private void AjustarResolucionRemota()
    {
        if (_rdp is not { } rdp || _contenedorRdp is not { } contenedor)
        {
            return;
        }

        if (contenedor.Width > 0 && contenedor.Height > 0)
        {
            rdp.Resize(contenedor.Width, contenedor.Height);
        }
    }

    /// <summary>Esconde todo lo que rodea a un elemento dentro de su ventana y lo devuelve como estaba. Recorre el árbol lógico y no el visual: así no entra en las plantillas de control.</summary>
    private sealed class RecorteDeLaVentana
    {
        private readonly List<UIElement> _escondidos = [];

        private readonly List<(ColumnDefinition Columna, GridLength Ancho, double Minimo)>
            _columnas = [];

        private readonly List<(RowDefinition Fila, GridLength Alto, double Minimo)> _filas = [];

        public static RecorteDeLaVentana? Aplicar(FrameworkElement foco)
        {
            if (Window.GetWindow(foco) is null)
            {
                return null;
            }

            var recorte = new RecorteDeLaVentana();
            DependencyObject actual = foco;

            while (LogicalTreeHelper.GetParent(actual) is { } padre)
            {
                recorte.Recortar(padre, actual);
                actual = padre;

                if (padre is Window)
                {
                    break;
                }
            }

            return recorte;
        }

        public void Deshacer()
        {
            foreach (var elemento in _escondidos)
            {
                elemento.Visibility = Visibility.Visible;
            }

            foreach (var (columna, ancho, minimo) in _columnas)
            {
                columna.Width = ancho;
                columna.MinWidth = minimo;
            }

            foreach (var (fila, alto, minimo) in _filas)
            {
                fila.Height = alto;
                fila.MinHeight = minimo;
            }

            _escondidos.Clear();
            _columnas.Clear();
            _filas.Clear();
        }

        private void Recortar(DependencyObject padre, DependencyObject hijo)
        {
            // Las otras pestañas no se esconden: sin sus cabeceras no habría cómo volver a ellas.
            if (padre is not TabControl)
            {
                foreach (var hermano in LogicalTreeHelper.GetChildren(padre).OfType<UIElement>())
                {
                    if (!ReferenceEquals(hermano, hijo)
                        && hermano.Visibility == Visibility.Visible)
                    {
                        _escondidos.Add(hermano);
                        hermano.Visibility = Visibility.Collapsed;
                    }
                }
            }

            if (padre is Grid grilla && hijo is FrameworkElement elemento)
            {
                Encoger(grilla, elemento);
            }
        }

        /// <summary>Esconder el elemento no alcanza: una columna con MinWidth sigue ocupando su lugar.</summary>
        private void Encoger(Grid grilla, FrameworkElement elemento)
        {
            var columna = Grid.GetColumn(elemento);
            var columnas = Grid.GetColumnSpan(elemento);
            var fila = Grid.GetRow(elemento);
            var filas = Grid.GetRowSpan(elemento);

            for (var i = 0; i < grilla.ColumnDefinitions.Count; i++)
            {
                if (i >= columna && i < columna + columnas)
                {
                    continue;
                }

                var pista = grilla.ColumnDefinitions[i];
                _columnas.Add((pista, pista.Width, pista.MinWidth));
                pista.MinWidth = 0;
                pista.Width = new GridLength(0);
            }

            for (var i = 0; i < grilla.RowDefinitions.Count; i++)
            {
                if (i >= fila && i < fila + filas)
                {
                    continue;
                }

                var pista = grilla.RowDefinitions[i];
                _filas.Add((pista, pista.Height, pista.MinHeight));
                pista.MinHeight = 0;
                pista.Height = new GridLength(0);
            }
        }
    }

    private void AnotarLaApertura(DateTimeOffset inicio)
    {
        TardoEnAbrir = DateTimeOffset.UtcNow - inicio;
        AbiertaA = DateTimeOffset.Now;
    }

    private void CambiarEstado(SessionState estado, SessionFailure? fallo)
    {
        State = estado;
        StateChanged?.Invoke(this, new SessionStateChanged(estado, fallo));
    }

    private void MostrarError(SessionFailure? fallo)
    {
        _marcoSesion.Visibility = Visibility.Collapsed;
        _aviso.Visibility = Visibility.Visible;
        _reintentar.Visibility = Visibility.Visible;

        _mensaje.Text = fallo is null
            ? "No se pudo conectar."
            : $"{fallo.UserMessage}{Environment.NewLine}{Environment.NewLine}{fallo.SuggestedAction}";

        _root.Logger.ConnectionFailed(
            ConnectionId,
            fallo?.Reason ?? SessionFailureReason.Other,
            fallo?.TechnicalDetail);
    }

    private async Task GuardarConexionExitosaAsync()
    {
        try
        {
            if (_ssh?.FingerprintToRemember is { } huella && _registro.Ssh is { } ssh)
            {
                ssh.KnownHostFingerprint = huella;
                await _root.Connections.UpdateAsync(_registro).ConfigureAwait(true);
            }

            await _root.Connections
                .SetLastConnectedAsync(ConnectionId, DateTimeOffset.UtcNow).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _root.Logger.TechnicalError("registrar la conexión exitosa", ex);
        }
    }

    public void Dispose()
    {
        if (_dispuesto)
        {
            return;
        }

        _dispuesto = true;
        _demoraBusqueda.Stop();
        _demoraDeResolucion.Stop();

        Aislar(() => _recorte?.Deshacer());
        _recorte = null;

        Aislar(() => _ventanaPropia?.SoltarYCerrar());
        _ventanaPropia = null;

        Aislar(() => _tcsContraseña?.TrySetResult(null));
        Aislar(LimpiarContraseñaInteractivaPendiente);

        Aislar(() => _panelEstado?.Detener());
        Aislar(() => _rdp?.Dispose());
        Aislar(() => _contenedorRdp?.Dispose());

        Aislar(() =>
        {
            if (_tuneles is { } tuneles)
            {
                _ = tuneles.DisposeAsync();
            }
        });

        Aislar(() =>
        {
            if (_comandos is { } comandos)
            {
                _ = comandos.DisposeAsync();
            }
        });

        Aislar(() =>
        {
            if (_ssh is { } ssh)
            {
                _ = ssh.DisposeAsync();
            }
        });

        Aislar(() =>
        {
            if (_archivos is { } archivos)
            {
                _ = archivos.DisposeAsync();
            }
        });

        Aislar(() => _terminal?.Dispose());
        Aislar(() => _credencial?.Dispose());

        GC.SuppressFinalize(this);
    }

    /// <summary>Corre un paso del cierre sin dejar que su fallo impida los siguientes.</summary>
    private void Aislar(Action paso)
    {
        try
        {
            paso();
        }
        catch (Exception ex)
        {
            _root.Logger.TechnicalError("cerrar la sesión", ex);
        }
    }

    /// <summary>Abre la barra de búsqueda sobre el terminal actual. La pide TerminalControl con Ctrl+F —el control de WinForms se queda con esa tecla antes de que llegue a la ventana—.</summary>
    private void AbrirBusqueda()
    {
        if (_terminal is null)
        {
            return;
        }

        _barraBusqueda.Visibility = Visibility.Visible;

        _textoBusqueda.SelectAll();
        _textoBusqueda.Focus();
        Keyboard.Focus(_textoBusqueda);

        if (_textoBusqueda.Text.Length > 0)
        {
            EjecutarBusqueda();
        }
    }

    private void AlCambiarTextoDeBusqueda(object sender, TextChangedEventArgs e)
    {
        _demoraBusqueda.Stop();
        _demoraBusqueda.Start();
    }

    private void AlTeclearEnBusqueda(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                CerrarBusqueda();
                e.Handled = true;
                break;

            case Key.Enter when Keyboard.Modifiers.HasFlag(ModifierKeys.Shift):
                FlushBusquedaPendiente();
                _terminal?.BusquedaAnterior();
                ActualizarContadorBusqueda();
                e.Handled = true;
                break;

            case Key.Enter:
                FlushBusquedaPendiente();
                _terminal?.BusquedaSiguiente();
                ActualizarContadorBusqueda();
                e.Handled = true;
                break;
        }
    }

    private void FlushBusquedaPendiente()
    {
        if (!_demoraBusqueda.IsEnabled)
        {
            return;
        }

        _demoraBusqueda.Stop();
        EjecutarBusqueda();
    }

    private void EjecutarBusqueda()
    {
        _terminal?.Buscar(_textoBusqueda.Text);
        ActualizarContadorBusqueda();
    }

    private void AlPedirCoincidenciaAnterior(object sender, RoutedEventArgs e)
    {
        FlushBusquedaPendiente();
        _terminal?.BusquedaAnterior();
        ActualizarContadorBusqueda();
        _textoBusqueda.Focus();
    }

    private void AlPedirCoincidenciaSiguiente(object sender, RoutedEventArgs e)
    {
        FlushBusquedaPendiente();
        _terminal?.BusquedaSiguiente();
        ActualizarContadorBusqueda();
        _textoBusqueda.Focus();
    }

    private void AlCerrarBusqueda(object sender, RoutedEventArgs e) => CerrarBusqueda();

    private void CerrarBusqueda()
    {
        _demoraBusqueda.Stop();
        _barraBusqueda.Visibility = Visibility.Collapsed;
        _terminal?.Buscar(string.Empty);
        _terminal?.Focus();
    }

    private void ActualizarContadorBusqueda()
    {
        if (_terminal is not { } terminal)
        {
            return;
        }

        _contadorBusqueda.Text = _textoBusqueda.Text.Length == 0
            ? string.Empty
            : terminal.TotalCoincidencias == 0
                ? "Sin coincidencias"
                : $"{terminal.CoincidenciaActual} de {terminal.TotalCoincidencias}";
    }
}
