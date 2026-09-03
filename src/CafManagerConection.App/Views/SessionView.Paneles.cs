using System.Diagnostics;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using CafManagerConection.App.Panels;
using CafManagerConection.App.Services;
using CafManagerConection.Monitoring;
using CafManagerConection.Platform;
using CafManagerConection.Ssh;
using CafManagerConection.UseCases.Abstractions;

namespace CafManagerConection.App.Views;

/// <summary>Paneles que pueden acompañar a una sesión SSH.</summary>
public enum TipoPanel
{
    Archivos,
    Estado,
    Tuneles,
    Docker,
    Nginx,
    Supervisord,
    Puertos,
    Procesos,
}

/// <summary>Adaptador entre el ejecutor de comandos SSH y lo que esperan métricas e inventario.</summary>
[SupportedOSPlatform("windows")]
internal sealed class EjecutorRemoto(SshCommandRunner runner)
    : IRemoteCommandRunner, IPlatformCommandRunner, IPlatformLogStreamer
{
    Task<IAsyncDisposable> IPlatformLogStreamer.SeguirAsync(
        string command, Action<string> onLinea, Action<string?> onCerrado, CancellationToken ct) =>
        ((IPlatformLogStreamer)runner).SeguirAsync(command, onLinea, onCerrado, ct);

    async Task<(bool Success, string Output, string Error)> IRemoteCommandRunner.RunAsync(
        string command, int timeoutSeconds, CancellationToken ct)
    {
        var r = await runner.RunAsync(command, timeoutSeconds, ct).ConfigureAwait(false);
        return (r.Success, r.Output, r.Error);
    }

    async Task<(bool Success, string Output, string Error)> IPlatformCommandRunner.RunAsync(
        string command, int timeoutSeconds, CancellationToken ct)
    {
        var r = await runner.RunAsync(command, timeoutSeconds, ct).ConfigureAwait(false);
        return (r.Success, r.Output, r.Error);
    }

    async Task<(bool Success, string Output, string Error)> IPlatformCommandRunner.RunWithSudoAsync(
        string command, int timeoutSeconds, CancellationToken ct)
    {
        var r = await runner
            .RunWithSudoFallbackAsync(command, timeoutSeconds, ct).ConfigureAwait(false);

        return (r.Success, r.Output, r.Error);
    }
}

/// <summary>Lee con privilegios: el canario de <see cref="EscaladaDeLectura"/> es lo que hace que el reintento con <c>sudo</c> llegue a ejecutarse (FR-184a). Sólo lecturas (FR-184b).</summary>
[SupportedOSPlatform("windows")]
internal sealed class EjecutorConSudo(SshCommandRunner runner)
    : IRemoteCommandRunner, IPlatformCommandRunner
{
    Task<(bool Success, string Output, string Error)> IRemoteCommandRunner.RunAsync(
        string command, int timeoutSeconds, CancellationToken ct) =>
        LeerAsync(command, timeoutSeconds, ct);

    Task<(bool Success, string Output, string Error)> IPlatformCommandRunner.RunAsync(
        string command, int timeoutSeconds, CancellationToken ct) =>
        LeerAsync(command, timeoutSeconds, ct);

    Task<(bool Success, string Output, string Error)> IPlatformCommandRunner.RunWithSudoAsync(
        string command, int timeoutSeconds, CancellationToken ct) =>
        LeerAsync(command, timeoutSeconds, ct);

    private async Task<(bool Success, string Output, string Error)> LeerAsync(
        string command, int timeoutSeconds, CancellationToken ct)
    {
        var r = await runner
            .RunWithSudoFallbackAsync(
                EscaladaDeLectura.Guardado(command), timeoutSeconds, ct)
            .ConfigureAwait(false);

        return (r.Success, r.Output, r.Error);
    }
}

/// <summary>Armado y apertura de los paneles laterales de una sesión.</summary>
[SupportedOSPlatform("windows")]
public partial class SessionView
{
    /// <summary>Registra los paneles que este servidor admite.</summary>
    private async Task PrepararPanelesAsync()
    {
        if (_peticionSsh is null || _accesos.Count > 0)
        {
            return;
        }

        Registrar(TipoPanel.Archivos, "Archivos", "Archivos (SFTP)");
        Registrar(TipoPanel.Tuneles, "Tuneles", "Túneles");

        await LeerTunelesDefinidosAsync().ConfigureAwait(true);

        try
        {
            _comandos = new SshCommandRunner(
                _peticionSsh,
                this,
                _ssh?.CredencialEfectiva ?? _credencial,
                _root.Logger,
                _root.Trazas,
                _registro.Connection.Name,
                ContrasenaDeSudoDeLaSesion(),
                new PedidoDeContrasenaDeSudoWpf(() => Window.GetWindow(this)));

            if (!await _comandos.ConnectAsync().ConfigureAwait(true))
            {
                _root.Logger.TechnicalError(
                    "abrir la conexión de comandos para los paneles del servidor",
                    _comandos.LastError ?? new InvalidOperationException(
                        "La conexión de comandos no quedó establecida y no informó ningún error."));

                return;
            }

            var inventario = new PlatformInventory(
                new EjecutorRemoto(_comandos),
                Domain.Settings.Defaults.InventoryQueryTimeoutSeconds,
                _root.Logger,
                ConnectionId);

            var capacidades = await inventario.DetectAsync().ConfigureAwait(true);

            if (_dispuesto)
            {
                return;
            }

            if (capacidades.IsLinux)
            {
                Registrar(TipoPanel.Estado, "Estado", "Estado del servidor");
                Registrar(TipoPanel.Procesos, "Procesos", "Procesos del servidor", "IconoAplicacion");
                Registrar(TipoPanel.Puertos, "Puertos", "Puertos a la escucha");

                _ = SondeoDeSudoAsync();
            }
            else
            {
                _root.Logger.TechnicalError(
                    "detectar el servidor: no parece Linux. Respuesta cruda: "
                    + $"[{inventario.LastDetectionOutput?.ReplaceLineEndings(" | ")}]",
                    new InvalidOperationException("La detección no devolvió la marca cmc:linux."));
            }

            if (capacidades.HasDocker)
            {
                Registrar(TipoPanel.Docker, "Docker", "Docker");
            }

            if (capacidades.HasNginx)
            {
                Registrar(TipoPanel.Nginx, "Nginx", "nginx");
            }

            if (capacidades.HasSupervisord)
            {
                Registrar(TipoPanel.Supervisord, "Supervisor", "supervisord");
            }

            _inventario = inventario;
        }
        catch (Exception ex)
        {
            _root.Logger.TechnicalError("detectar las capacidades del servidor", ex);
        }
    }

    /// <summary>Agrega el acceso de un panel a la barra.</summary>
    private void Registrar(TipoPanel tipo, string icono, string nombre, string? clave = null)
    {
        if (_accesos.ContainsKey(tipo))
        {
            return;
        }

        var boton = new ToggleButton
        {
            Content = new System.Windows.Shapes.Path
            {
                Data = (System.Windows.Media.Geometry)FindResource(clave ?? $"IconoPanel{icono}"),
                Width = 16,
                Height = 16,
                Stretch = System.Windows.Media.Stretch.Uniform,
                Fill = (System.Windows.Media.Brush)FindResource("TextoTenue"),
            },
            ToolTip = nombre,
            Width = 26,
            Height = 24,

            Margin = new Thickness(0, 0, 0, 2),
            Style = (Style)FindResource("AccesoPanel"),
        };

        boton.Click += (_, _) =>
        {
            if (_abriendo)
            {
                return;
            }

            if (_abierto == tipo)
            {
                CerrarPanel();
            }
            else
            {
                _ = AbrirPanelAsync(tipo);
            }
        };

        _accesos[tipo] = boton;
        boton.Tag = OrdenEnLaBarra(tipo);

        // Se inserta en su lugar y no al final: Docker, nginx y supervisord se registran despues
        // de la deteccion, asi que agregar al final los dejaria en el orden en que respondio el
        // servidor y no en el que el usuario quiere verlos.
        var donde = 0;

        while (donde < _barra.Children.Count
               && _barra.Children[donde] is ToggleButton { Tag: int previo }
               && previo <= (int)boton.Tag)
        {
            donde++;
        }

        _barra.Children.Insert(donde, boton);
        _marcoBarra.Visibility = Visibility.Visible;
    }

    /// <summary>Orden de los accesos en la barra lateral de la sesión, pedido por el usuario.</summary>
    internal static int OrdenEnLaBarra(TipoPanel tipo) => tipo switch
    {
        TipoPanel.Estado => 1,
        TipoPanel.Docker => 2,
        TipoPanel.Supervisord => 3,
        TipoPanel.Nginx => 4,
        TipoPanel.Procesos => 5,
        TipoPanel.Tuneles => 6,
        TipoPanel.Puertos => 7,
        TipoPanel.Archivos => 8,
        _ => 99,
    };

    /// <summary>Abre un panel: la barra aparece primero y los datos llegan cuando llegan.</summary>
    private async Task AbrirPanelAsync(TipoPanel tipo)
    {
        if (_abriendo)
        {
            return;
        }

        _abriendo = true;

        try
        {
            if (_paneles.TryGetValue(tipo, out var listo))
            {
                MostrarEnColumna(tipo, listo);
                return;
            }

            MostrarEnColumna(tipo, CartelDeCarga(NombreParaCargar(tipo)));

            var reloj = Stopwatch.StartNew();
            _motivoDeFallo = null;
            var contenido = await CrearPanelAsync(tipo).ConfigureAwait(true);
            _root.Logger.WorkCompleted(ConnectionId, RemoteWork.PanelBuild, reloj.Elapsed);

            if (_dispuesto || _abierto != tipo)
            {
                Aislar(() => (contenido as StatusPanel)?.Detener());
                Aislar(() => (contenido as ProcesosPanel)?.Detener());

                if (ReferenceEquals(_panelEstado, contenido))
                {
                    _panelEstado = null;
                }

                if (ReferenceEquals(_panelProcesos, contenido))
                {
                    _panelProcesos = null;
                }

                return;
            }

            if (contenido is null)
            {
                _panel.Content = CartelDeFallo(_motivoDeFallo);
                return;
            }

            _paneles[tipo] = contenido;
            _panel.Content = contenido;
        }
        finally
        {
            _abriendo = false;
        }
    }

    private void MostrarEnColumna(TipoPanel tipo, FrameworkElement contenido)
    {
        _panel.Content = contenido;

        _columnaPanel.Width = new GridLength(AnchoRecordado(tipo));
        _divisor.Visibility = Visibility.Visible;

        _abierto = tipo;
        ActualizarAccesos();
        AjustarRelojDeEstado(tipo);
    }

    /// <summary>Arranca o para el muestreo de los paneles que consultan al servidor por reloj.</summary>
    private void AjustarRelojDeEstado(TipoPanel? visible)
    {
        if (_panelEstado is { } estado)
        {
            if (visible == TipoPanel.Estado)
            {
                estado.Iniciar();
            }
            else
            {
                estado.Detener();
            }
        }

        if (_panelProcesos is not { } procesos)
        {
            return;
        }

        if (visible == TipoPanel.Procesos)
        {
            procesos.Iniciar();
        }
        else
        {
            procesos.Detener();
        }
    }

    /// <summary>Cómo se nombra cada panel dentro de la frase «Consultando …».</summary>
    private static string NombreParaCargar(TipoPanel tipo) => tipo switch
    {
        TipoPanel.Archivos => "los archivos del servidor",
        TipoPanel.Tuneles => "los túneles",
        TipoPanel.Estado => "el estado del servidor",
        TipoPanel.Procesos => "los procesos del servidor",
        TipoPanel.Puertos => "los puertos a la escucha",
        TipoPanel.Docker => "Docker",
        TipoPanel.Nginx => "nginx",
        TipoPanel.Supervisord => "supervisord",
        _ => "el servidor",
    };

    /// <summary>Lo que se ve mientras el servidor contesta.</summary>
    private static FrameworkElement CartelDeCarga(string nombre)
    {
        var texto = new TextBlock
        {
            Text = $"Consultando {nombre}…",
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 12),
        };

        texto.SetResourceReference(TextBlock.StyleProperty, "Tenue");

        var barra = new ProgressBar
        {
            IsIndeterminate = true,
            Width = 160,
            Height = 4,
            HorizontalAlignment = HorizontalAlignment.Center,
        };

        var pila = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(20),
        };

        pila.Children.Add(texto);
        pila.Children.Add(barra);

        return pila;
    }

    /// <summary>Lo que se ve cuando un panel no se pudo armar, con el motivo a la vista.</summary>
    private static FrameworkElement CartelDeFallo(string? motivo)
    {
        var texto = new TextBlock
        {
            Text = "No se pudo abrir el panel." + Environment.NewLine + Environment.NewLine
                   + (motivo is { Length: > 0 } m ? m : "No se informó ningún motivo.")
                   + Environment.NewLine + Environment.NewLine
                   + "El detalle completo está en el registro: Preferencias → Registros.",
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 320,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(20),
        };

        texto.SetResourceReference(TextBlock.StyleProperty, "Tenue");

        return texto;
    }

    /// <summary>Ancho guardado de un panel, o el de origen si nunca se tocó.</summary>
    private double AnchoRecordado(TipoPanel tipo) =>
        _anchosDePanel.TryGetValue(tipo.ToString(), out var ancho) ? ancho : AnchoPanel;

    private void CerrarPanel()
    {
        if (_abierto is { } abierto)
        {
            var ancho = _columnaPanel.ActualWidth;

            if (ancho > 0)
            {
                _anchosDePanel[abierto.ToString()] = ancho;
                _ = _root.AppSettings.SavePanelWidthAsync(abierto.ToString(), ancho);
            }

        }

        AjustarRelojDeEstado(null);

        _columnaPanel.Width = new GridLength(0);
        _divisor.Visibility = Visibility.Collapsed;
        _abierto = null;
        ActualizarAccesos();
    }

    private void ActualizarAccesos()
    {
        foreach (var (tipo, boton) in _accesos)
        {
            boton.IsChecked = _abierto == tipo;
        }
    }

    /// <summary>Crea el contenido de un panel la primera vez que se lo abre.</summary>
    private async Task<FrameworkElement?> CrearPanelAsync(TipoPanel tipo)
    {
        if (_peticionSsh is null)
        {
            return null;
        }

        try
        {
            switch (tipo)
            {
                case TipoPanel.Archivos:
                    {
                        _archivos = new RemoteFileSession(_peticionSsh, this, _credencial);
                        var panel = new FilesPanel(_archivos, () => _registro.Connection.Name);
                        await panel.IniciarAsync().ConfigureAwait(true);
                        return panel;
                    }

                case TipoPanel.Tuneles:
                    {
                        var panel = new TunnelsPanel(
                            AnfitrionDeTuneles(), _root, ConnectionId);

                        await panel.RefrescarAsync().ConfigureAwait(true);
                        return panel;
                    }

                case TipoPanel.Estado when _comandos is not null:
                    {
                        var recolector = new MetricsCollector(
                            new EjecutorRemoto(_comandos), null, _root.Logger, ConnectionId);

                        var consultor = new ConsultorDeProcesos(
                            new EjecutorRemoto(_comandos),
                            Domain.Settings.Defaults.InventoryQueryTimeoutSeconds,
                            _root.Logger,
                            ConnectionId);

                        _panelEstado = new StatusPanel(
                            recolector,
                            MonitorDeLaSesion(),
                            _root.AppSettings,
                            (pid, nombre) => AbrirFichaDeProcesoAsync(consultor, pid, nombre),
                            () => _ = AbrirPanelAsync(TipoPanel.Procesos));

                        _panelEstado.Iniciar();
                        return _panelEstado;
                    }

                case TipoPanel.Procesos when _comandos is not null:
                    {
                        var consultor = new ConsultorDeProcesos(
                            new EjecutorRemoto(_comandos),
                            Domain.Settings.Defaults.InventoryQueryTimeoutSeconds,
                            _root.Logger,
                            ConnectionId);

                        _panelProcesos = new ProcesosPanel(
                            MonitorDeLaSesion(),
                            _root.AppSettings,
                            (pid, nombre) => AbrirFichaDeProcesoAsync(consultor, pid, nombre));

                        _panelProcesos.AplicarSondeo(
                            await SondeoDeSudoAsync().ConfigureAwait(true));

                        _panelProcesos.Iniciar();
                        return _panelProcesos;
                    }

                case TipoPanel.Puertos when _inventario is not null:
                    {
                        var consultor = _comandos is null
                            ? null
                            : new ConsultorDeProcesos(
                                new EjecutorRemoto(_comandos),
                                Domain.Settings.Defaults.InventoryQueryTimeoutSeconds,
                                _root.Logger,
                                ConnectionId);

                        await LeerTunelesDefinidosAsync().ConfigureAwait(true);

                        var panel = new PuertosPanel(
                            _puertosConPrivilegios ? InventarioConPrivilegios() : _inventario,
                            consultor,
                            _registro.Connection.Name,
                            _registro.Connection.Host,
                            CrearTunelAPuertoAsync,
                            TunelParaPuerto,
                            LineaSshParaPuerto);

                        await panel.RefrescarAsync().ConfigureAwait(true);
                        await OfrecerEscalarLosPuertosAsync(panel).ConfigureAwait(true);

                        return panel;
                    }

                case TipoPanel.Docker when _inventario is not null && _comandos is not null:
                    {
                        var control = new ControlDeDocker(
                            new EjecutorRemoto(_comandos),
                            Domain.Settings.Defaults.InventoryQueryTimeoutSeconds,
                            _root.Logger,
                            ConnectionId);

                        var panel = new DockerPanel(
                            _inventario, control, _registro.Connection.Name);
                        await panel.RefrescarAsync().ConfigureAwait(true);
                        return panel;
                    }

                case TipoPanel.Nginx when _inventario is not null:
                    {
                        var panel = new NginxPanel(_inventario);
                        await panel.RefrescarAsync().ConfigureAwait(true);
                        return panel;
                    }

                case TipoPanel.Supervisord when _inventario is not null && _comandos is not null:
                    {
                        var control = new ControlDeSupervisor(
                            new EjecutorRemoto(_comandos),
                            _inventario.SupervisorctlResuelto,
                            Domain.Settings.Defaults.InventoryQueryTimeoutSeconds,
                            _root.Logger,
                            ConnectionId);

                        var panel = new SupervisorPanel(_inventario, control, new EjecutorRemoto(_comandos));
                        await panel.RefrescarAsync().ConfigureAwait(true);
                        return panel;
                    }

                default:

                    _motivoDeFallo =
                        $"El panel {tipo} necesita el canal de comandos del servidor y esta "
                        + "sesión no lo tiene. Volvé a conectar la pestaña.";

                    _root.Logger.TechnicalError(
                        $"abrir el panel {tipo}", new InvalidOperationException(_motivoDeFallo));

                    return null;
            }
        }
        catch (Exception ex)
        {
            var causa = ex;

            while (causa.InnerException is { } interna)
            {
                causa = interna;
            }

            _motivoDeFallo = causa.Message;
            _root.Logger.TechnicalError($"abrir el panel {tipo}", ex);

            return null;
        }
    }

    /// <summary>Por qué falló el último intento de armar un panel, para poder mostrarlo en su lugar.</summary>
    private string? _motivoDeFallo;

    private ProcesosPanel? _panelProcesos;

    /// <summary>Una sola muestra de procesos para los dos paneles que la miran (SC-050a).</summary>
    private MonitorDeProcesos? _monitorDeProcesos;

    /// <summary>El sondeo de sudo cuando esta sesión no tiene canal interactivo propio.</summary>
    private SondaDeSudo? _sondaPropia;

    private ContrasenaDeSudoDeSesion? _contrasenaDeSudoPropia;

    /// <summary>La contraseña de <c>sudo</c> vive en la sesión SSH cuando hay una, y sólo así se borra al cerrarla; un canal de comandos suelto lleva la suya (FR-184e, regla 5).</summary>
    private ContrasenaDeSudoDeSesion ContrasenaDeSudoDeLaSesion() =>
        _ssh?.ContrasenaDeSudo
        ?? (_contrasenaDeSudoPropia ??= new ContrasenaDeSudoDeSesion());

    private Domain.Settings.ResultadoDeSondeo? _escaladaDeSudo;

    private PlatformInventory? _inventarioPrivilegiado;

    private bool _puertosConPrivilegios;

    private MonitorDeProcesos MonitorDeLaSesion() =>
        _monitorDeProcesos ??= new MonitorDeProcesos(
            new ColectorDeProcesos(
                new EjecutorRemoto(_comandos!),
                null,
                new EjecutorConSudo(_comandos!)));

    /// <summary>Un solo sondeo de <c>sudo</c> por sesión, compartido por todos los paneles (FR-184c, SC-051).</summary>
    private async Task<Domain.Settings.ResultadoDeSondeo?> SondeoDeSudoAsync()
    {
        if (_escaladaDeSudo is { } sabido)
        {
            return sabido;
        }

        try
        {
            if (_ssh is { } sesion)
            {
                _escaladaDeSudo = await sesion.SondearSudoAsync().ConfigureAwait(true);
            }
            else if (_comandos is { } comandos)
            {
                _sondaPropia ??= new SondaDeSudo(
                    (comando, espera, ct) => comandos.RunAsync(comando, espera, ct));

                _escaladaDeSudo = await _sondaPropia.SondearAsync().ConfigureAwait(true);
            }
        }
        catch (Exception ex)
        {
            _root.Logger.TechnicalError("sondear si el usuario puede escalar con sudo", ex);
        }

        return _escaladaDeSudo;
    }

    /// <summary>El proceso de un puerto ajeno no se ve sin privilegios: ahí va el botón (FR-184a).</summary>
    private async Task OfrecerEscalarLosPuertosAsync(PuertosPanel panel)
    {
        if (_puertosConPrivilegios)
        {
            panel.AvisoDeEscalada(
                "Los puertos se están leyendo con privilegios: el proceso de los ajenos también "
                + "se ve.");

            return;
        }

        panel.MostrarEscalada(
            await SondeoDeSudoAsync().ConfigureAwait(true),
            "el proceso que abrió cada puerto ajeno",
            _comandos is null ? null : ReabrirLosPuertosConPrivilegiosAsync);
    }

    // Se comprueba antes de rearmar el panel: sin esto, un sudo que pide contraseña que no tenemos dejaba el aviso diciendo que se leía con privilegios y las mismas filas de antes.
    private async Task ReabrirLosPuertosConPrivilegiosAsync()
    {
        if (_comandos is not { } comandos)
        {
            return;
        }

        var lector = (IRemoteCommandRunner)new EjecutorConSudo(comandos);

        var (concedida, _, error) = await lector
            .RunAsync("true", Domain.Settings.Defaults.InventoryQueryTimeoutSeconds)
            .ConfigureAwait(true);

        if (!concedida)
        {
            if (_paneles.TryGetValue(TipoPanel.Puertos, out var actual)
                && actual is PuertosPanel puertos)
            {
                puertos.AvisoDeEscalada(
                    "El servidor no concedió la escalada: "
                    + (error.Trim() is { Length: > 0 } motivo
                        ? motivo
                        : "sudo la rechazó sin decir por qué."));
            }

            return;
        }

        _puertosConPrivilegios = true;
        _paneles.Remove(TipoPanel.Puertos);

        await AbrirPanelAsync(TipoPanel.Puertos).ConfigureAwait(true);
    }

    private PlatformInventory InventarioConPrivilegios() =>
        _inventarioPrivilegiado ??= new PlatformInventory(
            new EjecutorConSudo(_comandos!),
            Domain.Settings.Defaults.InventoryQueryTimeoutSeconds,
            _root.Logger,
            ConnectionId);

    /// <summary>Los túneles definidos para esta conexión, como estaban la última vez que se leyeron.</summary>
    private IReadOnlyList<Domain.Connections.SshTunnel> _tunelesDefinidos = [];

    /// <summary>El anfitrión de túneles de esta sesión, creándolo la primera vez.</summary>
    private TunnelHost AnfitrionDeTuneles()
    {
        if (_tuneles is { } existente)
        {
            return existente;
        }

        var anfitrion = new TunnelHost(_peticionSsh!, this, _credencial);

        anfitrion.StatusChanged += (_, _) => Dispatcher.BeginInvoke(() =>
            _ = RefrescarPanelDePuertosAsync());

        _tuneles = anfitrion;

        return anfitrion;
    }

    // Sólo el puerto remoto: localhost:5432 y 127.0.0.1:5432 son el mismo servicio.
    /// <summary>Qué túnel hay para un puerto del servidor, para el panel de puertos.</summary>
    private PuertosPanel.TunelDePuerto? TunelParaPuerto(int puertoRemoto)
    {
        var tunel = _tunelesDefinidos.FirstOrDefault(t => t.RemotePort == puertoRemoto);

        return tunel is null
            ? null
            : new PuertosPanel.TunelDePuerto(
                tunel.LocalPort, _tuneles?.IsActive(tunel.Id) == true);
    }

    /// <summary>La línea de ssh -L equivalente para un puerto del servidor.</summary>
    private string LineaSshParaPuerto(int puertoRemoto)
    {
        var local = TunelParaPuerto(puertoRemoto)?.PuertoLocal
                    ?? PuertoLocalSugerido.Elegir(
                        puertoRemoto,
                        PuertoLocalSugerido.Tomados(_tunelesDefinidos.Select(t => t.LocalPort)));

        return LineaDeTunel.Armar(
            local,
            "localhost",
            puertoRemoto,
            _peticionSsh?.UserName ?? string.Empty,
            _registro.Connection.Host,
            _peticionSsh?.Port ?? 22);
    }

    /// <summary>Vuelve a leer los túneles definidos de esta conexión.</summary>
    private async Task LeerTunelesDefinidosAsync()
    {
        try
        {
            _tunelesDefinidos = await _root.Tunnels
                .GetForConnectionAsync(ConnectionId).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _root.Logger.TechnicalError("leer los túneles definidos de la conexión", ex);
        }
    }

    /// <summary>Arma un túnel al puerto que se eligió en el panel de puertos (FR-168 a FR-168e).</summary>
    private async Task CrearTunelAPuertoAsync(int puertoRemoto, string sugerencia)
    {
        if (_peticionSsh is null || Window.GetWindow(this) is not { } ventana)
        {
            return;
        }

        try
        {
            await LeerTunelesDefinidosAsync().ConfigureAwait(true);

            var local = PuertoLocalSugerido.Elegir(
                puertoRemoto,
                PuertoLocalSugerido.Tomados(_tunelesDefinidos.Select(t => t.LocalPort)));

            var nombre = NombreDeTunel(sugerencia, puertoRemoto);

            var editor = new TunnelEditorWindow(
                _root,
                ConnectionId,

                new TunnelEditorWindow.Sugerencia(
                    nombre, local, "localhost", puertoRemoto, AutoIniciar: true))
            {
                Owner = ventana,
            };

            editor.ShowDialog();

            await LeerTunelesDefinidosAsync().ConfigureAwait(true);

            if (editor.Creado is not { } nuevo)
            {
                await RefrescarPanelDePuertosAsync().ConfigureAwait(true);
                return;
            }

            var error = await AnfitrionDeTuneles().StartAsync(nuevo).ConfigureAwait(true);

            if (error is { Length: > 0 })
            {
                MessageWindow.Avisar(
                    ventana,
                    "Túnel guardado, pero no levantado",
                    "El túnel quedó guardado y se va a intentar levantar en las próximas "
                    + "conexiones. Ahora no se pudo: "
                    + error);

                return;
            }

            await RefrescarPanelDeTunelesAsync().ConfigureAwait(true);
            await RefrescarPanelDePuertosAsync().ConfigureAwait(true);

            var responde = await SondaDePuerto
                .RespondeAsync(nuevo.LocalPort).ConfigureAwait(true);

            Informar(responde
                ? $"Túnel activo: localhost:{nuevo.LocalPort} → localhost:{nuevo.RemotePort}"
                : $"Túnel activo en localhost:{nuevo.LocalPort}, pero el servicio del servidor "
                  + "no contestó.");
        }
        catch (Exception ex)
        {
            _root.Logger.TechnicalError("crear un túnel desde el panel de puertos", ex);
        }
    }

    /// <summary>Nombre con el que se propone el túnel: el del proceso cuando se lo pudo averiguar.</summary>
    private static string NombreDeTunel(string sugerencia, int puertoRemoto)
    {
        var limpio = sugerencia.Trim();

        return limpio.Length == 0 || limpio.StartsWith('(')
            ? $"Puerto {puertoRemoto}"
            : $"{limpio} ({puertoRemoto})";
    }

    /// <summary>Refresca el panel de túneles si está armado, para que el nuevo aparezca activo.</summary>
    private async Task RefrescarPanelDeTunelesAsync()
    {
        if (_paneles.TryGetValue(TipoPanel.Tuneles, out var panel)
            && panel is TunnelsPanel tuneles)
        {
            await tuneles.RefrescarAsync().ConfigureAwait(true);
        }
    }

    /// <summary>Abre la ficha de un proceso elegido en el panel de estado (FR-173).</summary>
    private async Task AbrirFichaDeProcesoAsync(
        ConsultorDeProcesos consultor, int pid, string nombre)
    {
        if (Window.GetWindow(this) is not { } ventana)
        {
            return;
        }

        try
        {
            var detalle = await consultor
                .LeerAsync(pid, nombre, CancellationToken.None).ConfigureAwait(true);

            if (!detalle.Success)
            {
                MessageWindow.Avisar(
                    ventana,
                    "No se pudo leer el proceso",
                    detalle.Error is { Length: > 0 } motivo
                        ? motivo
                        : $"El proceso {pid} ya no existe en el servidor.");
                return;
            }

            // Sin puerto: pasarle el PID hacía que la cabecera dijera «PID 4711 · escuchando en el puerto 4711».
            ProcesoWindow.Mostrar(
                ventana, detalle.Value!, _registro.Connection.Name, string.Empty);
        }
        catch (Exception ex)
        {
            _root.Logger.TechnicalError("abrir la ficha de un proceso del panel de estado", ex);
        }
    }

    /// <summary>Refresca el panel de puertos si está armado, para que la columna del túnel se vea.</summary>
    private async Task RefrescarPanelDePuertosAsync()
    {
        if (_paneles.TryGetValue(TipoPanel.Puertos, out var panel) && panel is PuertosPanel puertos)
        {
            await LeerTunelesDefinidosAsync().ConfigureAwait(true);
            await puertos.RefrescarAsync().ConfigureAwait(true);
        }
    }

    /// <summary>Levanta los túneles marcados para iniciarse con la sesión (FR-092).</summary>
    private async Task LevantarTunelesAutomaticosAsync()
    {
        if (_peticionSsh is null)
        {
            return;
        }

        try
        {
            await LeerTunelesDefinidosAsync().ConfigureAwait(true);

            var automaticos = _tunelesDefinidos.Where(t => t.AutoStart).ToList();

            if (automaticos.Count == 0)
            {
                return;
            }

            var anfitrion = AnfitrionDeTuneles();

            foreach (var tunel in automaticos)
            {
                await anfitrion.StartAsync(tunel).ConfigureAwait(true);
            }
        }
        catch (Exception ex)
        {
            _root.Logger.TechnicalError("levantar los túneles automáticos", ex);
        }
    }
}
