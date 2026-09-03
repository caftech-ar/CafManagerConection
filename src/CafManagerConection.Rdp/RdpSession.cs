using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using CafManagerConection.Domain.Credentials;
using CafManagerConection.Domain.Sessions;

namespace CafManagerConection.Rdp;

public sealed record RdpSessionRequest(
    Guid ConnectionId,
    string Host,
    int Port,
    string UserName,
    string? Domain,
    bool ClipboardEnabled,
    bool FitToTab,
    bool IgnoreCertificateWarnings,
    int TimeoutSeconds,
    bool UseWindowsIdentity = false);

public enum AmbitoDeRdp
{
    Control,

    Avanzados,

    Asegurados,
}

public readonly record struct AjusteDeRdp(AmbitoDeRdp Ambito, string Propiedad, object? Valor);

/// <summary>Qué se le asigna al ActiveX para una petición. La contraseña no viaja acá: sólo la bandera que dice si se asigna (Principio II).</summary>
public sealed record PlanDeSesionRdp(
    IReadOnlyList<AjusteDeRdp> Ajustes,
    bool AsignaUsuario,
    bool AsignaContrasena,
    bool UsaIdentidadDeWindows)
{
    /// <summary>Con la identidad de Windows no se asigna usuario, dominio ni contraseña: CredSSP delega el token de la sesión actual (FR-186).</summary>
    public static PlanDeSesionRdp Para(
        RdpSessionRequest peticion, string? usuarioDeLaCredencial, bool hayContrasena)
    {
        ArgumentNullException.ThrowIfNull(peticion);

        var identidad = peticion.UseWindowsIdentity;

        var ajustes = new List<AjusteDeRdp>
        {
            new(AmbitoDeRdp.Control, "Server", peticion.Host),
        };

        var usuario = string.IsNullOrWhiteSpace(peticion.UserName)
            ? usuarioDeLaCredencial ?? string.Empty
            : peticion.UserName;

        if (!identidad)
        {
            ajustes.Add(new(AmbitoDeRdp.Control, "UserName", usuario));

            if (!string.IsNullOrWhiteSpace(peticion.Domain))
            {
                ajustes.Add(new(AmbitoDeRdp.Control, "Domain", peticion.Domain));
            }
        }

        ajustes.Add(new(AmbitoDeRdp.Avanzados, "RDPPort", peticion.Port));
        ajustes.Add(new(
            AmbitoDeRdp.Avanzados,
            "AuthenticationLevel",
            peticion.IgnoreCertificateWarnings ? 0 : 2));

        ajustes.Add(new(AmbitoDeRdp.Avanzados, "EnableCredSspSupport", true));

        if (identidad)
        {
            ajustes.Add(new(AmbitoDeRdp.Avanzados, "NegotiateSecurityLayer", true));
        }

        ajustes.Add(new(AmbitoDeRdp.Avanzados, "overallConnectionTimeout", peticion.TimeoutSeconds));
        ajustes.Add(new(AmbitoDeRdp.Avanzados, "SmartSizing", peticion.FitToTab));
        ajustes.Add(new(AmbitoDeRdp.Avanzados, "RedirectClipboard", peticion.ClipboardEnabled));

        ajustes.AddRange(SinRedirecciones());

        ajustes.Add(new(AmbitoDeRdp.Asegurados, "KeyboardHookMode", 1));

        return new PlanDeSesionRdp(
            ajustes,
            AsignaUsuario: !identidad,
            AsignaContrasena: !identidad && hayContrasena,
            UsaIdentidadDeWindows: identidad);
    }

    /// <summary>Discos, audio, micrófono, impresoras, puertos, cámaras y tarjetas inteligentes, apagados (FR-017).</summary>
    private static IEnumerable<AjusteDeRdp> SinRedirecciones() =>
    [
        new(AmbitoDeRdp.Avanzados, "RedirectDrives", false),
        new(AmbitoDeRdp.Avanzados, "RedirectPrinters", false),
        new(AmbitoDeRdp.Avanzados, "RedirectPorts", false),
        new(AmbitoDeRdp.Avanzados, "RedirectSmartCards", false),
        new(AmbitoDeRdp.Avanzados, "RedirectPOSDevices", false),
        new(AmbitoDeRdp.Avanzados, "RedirectDirectX", false),
        new(AmbitoDeRdp.Avanzados, "DisableRdpdr", 1),

        // AudioRedirectionMode 2 es "no reproducir".
        new(AmbitoDeRdp.Avanzados, "AudioRedirectionMode", 2),
        new(AmbitoDeRdp.Avanzados, "AudioCaptureRedirectionMode", false),

        new(AmbitoDeRdp.Avanzados, "GatewayUsageMethod", 0),
        new(AmbitoDeRdp.Avanzados, "GatewayProfileUsageMethod", 0),
    ];
}

/// <summary>Sesión RDP sobre el control ActiveX de Windows; el control tiene afinidad de hilo y vive en el de interfaz.</summary>
[SupportedOSPlatform("windows")]
public sealed class RdpSession : IDisposable
{
    private readonly RdpSessionRequest _request;

    private System.Windows.Forms.Timer? _vigilancia;
    private RdpClientHost? _host;
    private bool _disposed;

    public RdpSession(RdpSessionRequest request) => _request = request;

    public Guid ConnectionId => _request.ConnectionId;

    public SessionState State { get; private set; } = SessionState.Disconnected;

    public SessionFailure? Failure { get; private set; }

    public Control? Control => _host;

    public bool UsaIdentidadDeWindows => _request.UseWindowsIdentity;

    /// <summary>Si la sesión llegó a estar conectada alguna vez. Un fallo antes de eso es de credenciales; después, de la red.</summary>
    public bool LlegoAConectar { get; private set; }

    /// <summary>Propiedades que esta versión del ActiveX no aceptó. Nombres, nunca valores (Principio II).</summary>
    public IReadOnlyList<string> PropiedadesNoAceptadas { get; private set; } = [];

    public event EventHandler<SessionStateChanged>? StateChanged;

    public static bool IsAvailable => RdpClientHost.IsAvailable;

    /// <summary>Tras fallar la identidad de Windows, si hay que reintentar pidiendo credenciales en lugar de dar la conexión por perdida (FR-186).</summary>
    public static bool ConvieneCaerAlPedidoDeCredenciales(
        bool usoIdentidadDeWindows, bool llegoAConectar, SessionFailureReason motivo) =>
        usoIdentidadDeWindows
        && !llegoAConectar
        && motivo is SessionFailureReason.AuthenticationRejected
                  or SessionFailureReason.CredentialMissing
                  or SessionFailureReason.UnexpectedDisconnect
                  or SessionFailureReason.Other;

    /// <summary>Crea y configura el control, sin conectar todavía: hay que montarlo y mostrarlo antes de llamar a <see cref="PrepararYConectar"/>.</summary>
    public Control? CrearControl(StoredCredential? credential)
    {
        try
        {
            _host = new RdpClientHost { Dock = DockStyle.Fill };

            return _host;
        }
        catch (Exception ex)
        {
            Fail(Map(ex));
            return null;
        }
    }

    /// <summary>Crea la ventana del control, lo configura y conecta. El control ya tiene que estar montado y visible.</summary>
    public void PrepararYConectar(StoredCredential? credential)
    {
        if (_host is not { } host)
        {
            return;
        }

        try
        {
            host.CreateControl();

            Configure(credential);

            var servidor = _host.Get<string>("Server");

            if (!string.Equals(servidor, _request.Host, StringComparison.Ordinal))
            {
                Fail(new SessionFailure(
                    SessionFailureReason.Other,
                    "El control de Escritorio remoto de Windows no aceptó la configuración.",
                    "Es un problema del control ActiveX del sistema, no del servidor.",
                    $"Server quedó en «{servidor}» y debía ser «{_request.Host}». "
                    + $"CLSID={RdpClientHost.ResolvedClsid}"));

                return;
            }

            SetState(SessionState.Connecting);
            host.Invoke("Connect");

            VigilarEstado();
        }
        catch (Exception ex)
        {
            Fail(Map(ex));
        }
    }

    private void Configure(StoredCredential? credential)
    {
        if (_host is not { } host)
        {
            return;
        }

        var plan = PlanDeSesionRdp.Para(
            _request, credential?.UserName, credential?.HasSecret == true);

        var avanzados = host.GetObject("AdvancedSettings9")
                        ?? host.GetObject("AdvancedSettings8")
                        ?? host.GetObject("AdvancedSettings7")
                        ?? host.GetObject("AdvancedSettings2");

        var asegurados = host.GetObject("SecuredSettings3") ?? host.GetObject("SecuredSettings2");

        var rechazadas = new List<string>();

        foreach (var ajuste in plan.Ajustes)
        {
            if (!Aplicar(host, avanzados, asegurados, ajuste))
            {
                rechazadas.Add(ajuste.Propiedad);
            }
        }

        PropiedadesNoAceptadas = rechazadas;

        if (plan.AsignaContrasena && credential is not null && avanzados is not null)
        {
            RdpClientHost.TrySetOn(avanzados, "ClearTextPassword", credential.RevealSecret());
        }

        ConfigureDisplay(host);
    }

    private static bool Aplicar(
        RdpClientHost host, object? avanzados, object? asegurados, AjusteDeRdp ajuste) =>
        ajuste.Ambito switch
        {
            AmbitoDeRdp.Control => host.TrySet(ajuste.Propiedad, ajuste.Valor),

            AmbitoDeRdp.Avanzados => avanzados is not null
                                     && RdpClientHost.TrySetOn(
                                         avanzados, ajuste.Propiedad, ajuste.Valor),

            _ => asegurados is not null
                 && RdpClientHost.TrySetOn(asegurados, ajuste.Propiedad, ajuste.Valor),
        };

    // Antes iba por TrySetOn con el AxHost como destino: el nombre no existe en RdpClientHost y las tres propiedades no se asignaban nunca.
    private static void ConfigureDisplay(RdpClientHost host)
    {
        host.TrySet("ColorDepth", 32);

        if (host.Width > 0 && host.Height > 0)
        {
            host.TrySet("DesktopWidth", host.Width);
            host.TrySet("DesktopHeight", host.Height);
        }
    }

    /// <summary>Valor de <c>IMsTscAx::Connected</c> que significa sesión establecida; 0 es desconectado y 2, negociando.</summary>
    private const short Conectado = 1;

    private void VigilarEstado()
    {
        _vigilancia?.Dispose();

        var limite = DateTime.UtcNow.AddSeconds(Math.Max(_request.TimeoutSeconds, 5));

        // Connected sigue en 0 un rato después de pedir Connect: sin esta bandera, el primer sondeo declaraba el fallo a los 300 ms contra un servidor sano.
        var arranco = false;

        _vigilancia = new System.Windows.Forms.Timer { Interval = 250 };

        _vigilancia.Tick += (_, _) =>
        {
            if (_host is not { IsDisposed: false } host)
            {
                DetenerVigilancia();
                return;
            }

            short conectado;

            try
            {
                conectado = host.Get<short>("Connected");
            }
            catch (Exception)
            {
                DetenerVigilancia();
                return;
            }

            // Medido contra este control, apuntando a una dirección que no enruta: Connected vale 2 al segundo y medio y cae a 0 a los 17. El 2 es la negociación, no el éxito.
            if (conectado != 0)
            {
                arranco = true;
            }

            if (conectado == Conectado)
            {
                if (State != SessionState.Connected)
                {
                    SetState(SessionState.Connected);
                }

                return;
            }

            if (conectado == 0 && arranco
                && State is SessionState.Connecting or SessionState.Connected)
            {
                DetenerVigilancia();
                Fail(MotivoDeDesconexion(host));
                return;
            }

            if (State == SessionState.Connecting && DateTime.UtcNow > limite)
            {
                DetenerVigilancia();
                Fail(new SessionFailure(
                    SessionFailureReason.Timeout,
                    $"El servidor no respondió en {_request.TimeoutSeconds} segundos.",
                    "Comprobá que el equipo esté encendido y que el puerto 3389 sea alcanzable."));
            }
        };

        _vigilancia.Start();
    }

    /// <summary>Lo que dice el propio control, no lo que la aplicación cree: es la comprobación de que la sesión sobrevivió al cambio de ventana.</summary>
    public bool SigueConectado
    {
        get
        {
            try
            {
                return _host is { IsDisposed: false } host && host.Get<short>("Connected") == Conectado;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    /// <summary>Connected puede leer 0 un instante mientras el control cambia de ventana padre, y eso no es una desconexión.</summary>
    public void SuspenderVigilancia() => _vigilancia?.Stop();

    public void RetomarVigilancia() => _vigilancia?.Start();

    private void DetenerVigilancia()
    {
        _vigilancia?.Stop();
        _vigilancia?.Dispose();
        _vigilancia = null;
    }

    private SessionFailure MotivoDeDesconexion(RdpClientHost host)
    {
        int codigo;

        try
        {
            codigo = host.Get<int>("ExtendedDisconnectReason");
        }
        catch (Exception)
        {
            codigo = 0;
        }

        return ConDiagnostico(codigo switch
        {
            2 or 3 => new SessionFailure(
                SessionFailureReason.AuthenticationRejected,
                "El servidor rechazó las credenciales.",
                "Revisá el usuario, el dominio y la contraseña guardada.",
                $"ExtendedDisconnectReason={codigo}"),

            5 or 9 or 11 => new SessionFailure(
                SessionFailureReason.HostUnreachable,
                "No se pudo llegar al servidor.",
                "Comprobá que el equipo esté encendido y que acepte Escritorio remoto.",
                $"ExtendedDisconnectReason={codigo}"),

            _ => new SessionFailure(
                SessionFailureReason.UnexpectedDisconnect,
                "El servidor cerró la conexión.",
                "Reintentá; si se repite, revisá el registro de eventos del servidor.",
                $"ExtendedDisconnectReason={codigo}"),
        });
    }

    /// <summary>Agrega al detalle técnico qué propiedades del ActiveX no existían en esta versión, que es lo que hace falta para saber por qué no entró con la identidad de Windows.</summary>
    private SessionFailure ConDiagnostico(SessionFailure fallo)
    {
        if (!_request.UseWindowsIdentity)
        {
            return fallo;
        }

        var faltantes = PropiedadesNoAceptadas.Count == 0
            ? "ninguna"
            : string.Join(", ", PropiedadesNoAceptadas);

        return fallo with
        {
            TechnicalDetail = $"{fallo.TechnicalDetail}; identidadDeWindows=1; "
                              + $"propiedadesNoAceptadas={faltantes}",
        };
    }

    public void Disconnect()
    {
        try
        {
            if (_host is { } host && host.Get<int>("Connected") != 0)
            {
                host.Invoke("Disconnect");
            }
        }
        catch (Exception)
        {
        }

        SetState(SessionState.Disconnected);
    }

    public void Resize(int width, int height)
    {
        if (!_request.FitToTab || _host is not { } host || State != SessionState.Connected)
        {
            return;
        }

        try
        {
            host.Invoke("UpdateSessionDisplaySettings",
                (uint)width, (uint)height, (uint)width, (uint)height, 0u, 1u, 1u);
        }
        catch (Exception)
        {
        }
    }

    internal static SessionFailure Map(Exception ex) => ex switch
    {
        NotSupportedException e => new SessionFailure(
            SessionFailureReason.Other,
            e.Message,
            "Verificá que el cliente de Escritorio remoto esté instalado."),

        COMException { ErrorCode: unchecked((int)0x80004005) } => new SessionFailure(
            SessionFailureReason.HostUnreachable,
            "No se pudo alcanzar el servidor.",
            "Verificá el host, el puerto y la conectividad de red."),

        COMException e => new SessionFailure(
            SessionFailureReason.Other,
            "El cliente RDP rechazó la conexión.",
            "Revisá los datos de la conexión.",
            $"HRESULT 0x{e.ErrorCode:X8}"),

        _ => new SessionFailure(
            SessionFailureReason.Other,
            "No se pudo iniciar la sesión RDP.",
            "Revisá los datos de la conexión.",
            ex.GetType().Name),
    };

    /// <summary>Traduce el código de desconexión del control a una causa (FR-051).</summary>
    public static SessionFailure MapDisconnect(int reason) => reason switch
    {
        1 or 2 or 3 => new SessionFailure(
            SessionFailureReason.UnexpectedDisconnect,
            "La sesión se cerró.",
            "Podés reconectar desde la pestaña."),

        260 or 264 or 516 => new SessionFailure(
            SessionFailureReason.HostUnreachable,
            "No se pudo alcanzar el servidor.",
            "Verificá el host, el puerto y la conectividad de red."),

        2308 or 2311 => new SessionFailure(
            SessionFailureReason.UnexpectedDisconnect,
            "Se perdió la conexión con el servidor.",
            "Podés reconectar desde la pestaña."),

        2825 or 3079 or 3847 => new SessionFailure(
            SessionFailureReason.AuthenticationRejected,
            "El servidor rechazó las credenciales.",
            "Revisá el usuario, el dominio y la contraseña."),

        1288 or 1289 or 3591 => new SessionFailure(
            SessionFailureReason.CertificateUntrusted,
            "El certificado del servidor no es de confianza.",
            "Podés aceptarlo para esta conexión desde su edición."),

        _ => new SessionFailure(
            SessionFailureReason.UnexpectedDisconnect,
            "La sesión se cerró.",
            "Podés reconectar desde la pestaña.",
            $"Código {reason}"),
    };

    private void SetState(SessionState state)
    {
        if (state == SessionState.Connected)
        {
            LlegoAConectar = true;
        }

        State = state;
        StateChanged?.Invoke(this, new SessionStateChanged(state, Failure));
    }

    private void Fail(SessionFailure failure)
    {
        Failure = failure;
        State = SessionState.Error;
        StateChanged?.Invoke(this, new SessionStateChanged(SessionState.Error, failure));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        DetenerVigilancia();
        Disconnect();

        if (_host is { } host)
        {
            // Dispose tiene que ser el único que suelte el objeto COM: soltarlo desde HandleDestroyed lanzaba InvalidComObjectException en cada cierre de sesión.
            host.Dispose();
            _host = null;
        }
    }
}
