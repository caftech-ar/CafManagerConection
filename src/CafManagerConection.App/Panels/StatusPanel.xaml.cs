using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using CafManagerConection.Monitoring;
using CafManagerConection.UseCases.Abstractions;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;

namespace CafManagerConection.App.Panels;

/// <summary>Estado del servidor Linux: CPU, memoria, carga, discos, red, rutas y procesos (US7).</summary>
[SupportedOSPlatform("windows")]
public partial class StatusPanel : UserControl
{
    /// <summary>Las diez filas que FR-173 le pide al resumen del panel de estado.</summary>
    private const int CuantosEnElTop = 10;

    /// <summary>Dónde se recuerdan las preferencias de este panel (FR-083, FR-175).</summary>
    private readonly IAppSettingsService _ajustes;

    private readonly MetricsCollector _recolector;

    /// <summary>La muestra de procesos de la sesión, compartida con el panel de procesos (SC-050a).</summary>
    private readonly MonitorDeProcesos _procesosDelServidor;

    private readonly DispatcherTimer _reloj;
    private readonly SnapshotHistory _historial = new();

    /// <summary>Con qué abrir la ficha de un proceso, o null si esta sesión no puede.</summary>
    private readonly Func<int, string, Task>? _verProceso;

    /// <summary>Con qué abrir el panel de procesos, o null si esta sesión no lo tiene (FR-183d).</summary>
    private readonly Action? _verTodosLosProcesos;

    private LineSeries? _serieCpu;
    private LineSeries? _serieMemoria;
    private bool _ordenarPorCpu = true;
    private ServerSnapshot? _ultima;

    public StatusPanel(
        MetricsCollector recolector,
        MonitorDeProcesos procesos,
        IAppSettingsService ajustes,
        Func<int, string, Task>? verProceso = null,
        Action? verTodosLosProcesos = null)
    {
        _recolector = recolector;
        _procesosDelServidor = procesos;
        _ajustes = ajustes;
        _verProceso = verProceso;
        _verTodosLosProcesos = verTodosLosProcesos;

        // El reloj antes de InitializeComponent: AlCambiarIntervalo desreferencia _reloj y el XAML puede dispararlo al construir.
        _reloj = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(Domain.Settings.Defaults.MetricsSampleIntervalSeconds),
        };

        _reloj.Tick += async (_, _) => await LeerAsync().ConfigureAwait(true);

        InitializeComponent();

        // Marcado por código y no en el XAML: ahí el Checked corre durante InitializeComponent(),
        // cuando _procesosPanel todavía es null, y el panel entero no abre.
        _porCpu.IsChecked = true;

        ArmarGrafico();

        _intervalo.ItemsSource = Domain.Settings.Defaults.IntervalosDeMuestreo
            .Select(s => $"{s} s")
            .ToList();

        _ordenarPorCpu = _porCpu.IsChecked == true;

        _verTodos.Visibility = _verTodosLosProcesos is null
            ? Visibility.Collapsed
            : Visibility.Visible;

        Loaded += async (_, _) =>
        {
            _recolector.InterfacesVisibles =
                await _ajustes.GetVisibleInterfacesAsync().ConfigureAwait(true);

            AplicarIntervalo(await _ajustes.GetStatusIntervalAsync().ConfigureAwait(true));
        };
    }

    public void Iniciar()
    {
        _reloj.Start();
        _ = LeerAsync();
    }

    public void Detener() => _reloj.Stop();

    /// <summary>Arma el gráfico una sola vez; después sólo se le agregan puntos.</summary>
    private void ArmarGrafico()
    {
        var texto = ColorDeRecurso("TextoTenue");
        var borde = ColorDeRecurso("Borde");

        var modelo = new PlotModel
        {
            PlotAreaBorderColor = borde,
            TextColor = texto,
            PlotMargins = new OxyThickness(38, 4, 6, 6),
            Padding = new OxyThickness(0),
            IsLegendVisible = false,
        };

        modelo.Axes.Add(new LinearAxis
        {
            Position = AxisPosition.Left,
            Minimum = 0,
            Maximum = 100,
            MajorStep = 50,
            MinorTickSize = 0,
            MajorGridlineStyle = LineStyle.Dot,
            MajorGridlineColor = borde,
            TicklineColor = borde,
            TextColor = texto,
            StringFormat = "0'%'",
        });

        modelo.Axes.Add(new LinearAxis
        {
            Position = AxisPosition.Bottom,
            Minimum = 0,
            Maximum = SnapshotHistory.MaxPoints - 1,
            IsAxisVisible = false,
        });

        _serieCpu = new LineSeries
        {
            Color = ColorDeRecurso("Primario"),
            StrokeThickness = 1.6,
            Title = "CPU",
            TrackerFormatString = "CPU: {4:0.0}%",
        };

        _serieMemoria = new LineSeries
        {
            Color = ColorDeRecurso("IconoVioleta"),
            StrokeThickness = 1.6,
            Title = "Memoria",
            TrackerFormatString = "Memoria: {4:0.0}%",
        };

        modelo.Series.Add(_serieCpu);
        modelo.Series.Add(_serieMemoria);

        _grafico.Model = modelo;
    }

    private OxyColor ColorDeRecurso(string clave) =>
        TryFindResource(clave) is SolidColorBrush pincel
            ? OxyColor.FromArgb(pincel.Color.A, pincel.Color.R, pincel.Color.G, pincel.Color.B)
            : OxyColors.Gray;

    private void RedibujarGrafico()
    {
        if (_serieCpu is null || _serieMemoria is null || _grafico.Model is null)
        {
            return;
        }

        Cargar(_serieCpu, _historial.CpuSeries());
        Cargar(_serieMemoria, _historial.MemorySeries());

        _grafico.Model.InvalidatePlot(updateData: true);

        static void Cargar(LineSeries serie, IReadOnlyList<double> valores)
        {
            serie.Points.Clear();

            var desplazamiento = SnapshotHistory.MaxPoints - valores.Count;

            for (var i = 0; i < valores.Count; i++)
            {
                serie.Points.Add(new DataPoint(desplazamiento + i, valores[i]));
            }
        }
    }

    private void AplicarIntervalo(int segundos)
    {
        _reloj.Interval = TimeSpan.FromSeconds(segundos);

        var i = Array.IndexOf(Domain.Settings.Defaults.IntervalosDeMuestreo, segundos);

        if (i >= 0 && _intervalo.SelectedIndex != i)
        {
            _intervalo.SelectedIndex = i;
        }
    }

    private void AlCambiarIntervalo(object sender, SelectionChangedEventArgs e)
    {
        if (_intervalo.SelectedIndex < 0
            || _intervalo.SelectedIndex >= Domain.Settings.Defaults.IntervalosDeMuestreo.Length)
        {
            return;
        }

        var segundos = Domain.Settings.Defaults.IntervalosDeMuestreo[_intervalo.SelectedIndex];

        if (_reloj.Interval == TimeSpan.FromSeconds(segundos))
        {
            return;
        }

        _reloj.Interval = TimeSpan.FromSeconds(segundos);
        _ = _ajustes.SaveStatusIntervalAsync(segundos);

        _ = LeerAsync();
    }

    // El tiempo límite de una lectura (10 s) supera el intervalo más corto del reloj (2 s).
    /// <summary>Hay una lectura en curso: el reloj se saltea el turno en vez de encimar otra.</summary>
    private bool _leyendo;

    private async Task LeerAsync()
    {
        if (_leyendo)
        {
            return;
        }

        _leyendo = true;

        try
        {
            await UnaLecturaAsync().ConfigureAwait(true);
        }
        finally
        {
            _leyendo = false;
        }
    }

    private async Task UnaLecturaAsync()
    {
        ServerSnapshot? lectura;

        try
        {
            lectura = await _recolector
                .CollectAsync(Domain.Settings.Defaults.MetricsQueryTimeoutSeconds)
                .ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            SinLectura(ex.Message);
            return;
        }

        if (lectura is null)
        {
            SinLectura(_recolector.UltimoError ?? "No se pudo leer el estado del servidor.");
            return;
        }

        _ultima = lectura;
        _distro.ClearValue(ForegroundProperty);
        _historial.Add(lectura);

        Cabecera(lectura);
        Cpu(lectura);
        Memoria(lectura);
        Carga(lectura);
        Discos(lectura);
        Red(lectura);
        Rutas(lectura);
        Temperaturas(lectura);
        Fallidos(lectura);

        RedibujarGrafico();
        PoblarSelectorInterfaces();

        _ultimaLectura.Text = $"leído {lectura.TakenAt.ToLocalTime():HH:mm:ss}";

        await LeerProcesosAsync().ConfigureAwait(true);
    }

    /// <summary>El top sale de la misma muestra que mira el panel de procesos: dos paneles, una lectura.</summary>
    private async Task LeerProcesosAsync()
    {
        try
        {
            await _procesosDelServidor
                .MuestraAsync(
                    _reloj.Interval * 0.6,
                    Domain.Settings.Defaults.MetricsQueryTimeoutSeconds)
                .ConfigureAwait(true);
        }
        catch (Exception)
        {
        }

        Procesos();
    }

    private void SinLectura(string motivo)
    {
        _host.Text = "Sin lectura del servidor";
        _distro.Text = motivo;
        _distro.Foreground = (Brush)FindResource("MedidaAdvertencia");
    }

    private void Cabecera(ServerSnapshot l)
    {
        _host.Text = l.System.HostName;

        _distro.Text = string.Join(" · ", new[]
        {
            l.System.Distribution,
            l.System.KernelVersion,
        }.Where(s => !string.IsNullOrEmpty(s)));

        // En aarch64 /proc/cpuinfo no trae el modelo de procesador: la fila desaparece en vez de quedar vacía.
        _cpuModelo.Text = l.System.CpuModel ?? string.Empty;
        _cpuModelo.Visibility = l.System.CpuModel is { Length: > 0 }
            ? Visibility.Visible
            : Visibility.Collapsed;

        _uptime.Text = $"Encendido hace {Magnitudes.Duracion(l.Uptime)}";
    }

    private void Cpu(ServerSnapshot l)
    {
        _cpu.Value = l.Cpu.UsedPercent;

        _cpuDetalle.Text =
            $"{l.Cpu.UsedPercent:0.0}% · {l.Cpu.CoreCount} núcleo(s) · "
            + $"{l.System.ProcessCount} procesos";

        Semaforo(_cpu, _cpuNivel, NivelDeUso.DePorcentaje(l.Cpu.UsedPercent));
        Presion(_cpuPresion, "CPU", l.Pressure.Cpu);
    }

    private void Memoria(ServerSnapshot l)
    {
        _memoria.Value = l.Memory.UsedPercent;

        _memoriaDetalle.Text =
            $"{Magnitudes.Tamano(l.Memory.UsedBytes)} de {Magnitudes.Tamano(l.Memory.TotalBytes)} "
            + $"({l.Memory.UsedPercent:0.0}%)";

        Semaforo(_memoria, _memoriaNivel, NivelDeUso.DePorcentaje(l.Memory.UsedPercent));

        if (l.Swap.Existe)
        {
            _swapPanel.Visibility = Visibility.Visible;
            _swap.Value = l.Swap.UsedPercent;

            _swapDetalle.Text =
                $"{Magnitudes.Tamano(l.Swap.UsedBytes)} de {Magnitudes.Tamano(l.Swap.TotalBytes)} "
                + $"({l.Swap.UsedPercent:0.0}%)";

            Semaforo(_swap, _swapNivel, NivelDeUso.DePorcentaje(l.Swap.UsedPercent));
        }
        else
        {
            _swapPanel.Visibility = Visibility.Collapsed;
        }

        Presion(_memoriaPresion, "memoria", l.Pressure.Memory);
    }

    /// <summary>Escribe la presión de un recurso, y sólo cuando dice algo (FR-174).</summary>
    private void Presion(TextBlock donde, string recurso, PressureMetrics? presion)
    {
        if (presion is not { } p || p.Some < 1)
        {
            donde.Visibility = Visibility.Collapsed;
            return;
        }

        donde.Text = p.Full >= 1
            ? $"Presión de {recurso}: {p.Some:0.#}% del tiempo hay algo esperando, "
              + $"{p.Full:0.#}% está todo detenido"
            : $"Presión de {recurso}: {p.Some:0.#}% del tiempo hay algo esperando";

        donde.Foreground = PincelDe(NivelDeUso.DePorcentaje(p.Some));
        donde.Visibility = Visibility.Visible;
    }

    private void Carga(ServerSnapshot l)
    {
        var porNucleo = l.Cpu.CoreCount > 0 ? l.Load.OneMinute / l.Cpu.CoreCount : 0;

        _carga.Text =
            $"{l.Load.OneMinute:0.00} · {l.Load.FiveMinutes:0.00} · "
            + $"{l.Load.FifteenMinutes:0.00}   (1, 5 y 15 minutos)"
            + (l.Cpu.CoreCount > 0 ? $"   ·   {porNucleo:0.00} por núcleo" : string.Empty);

        Semaforo(null, _cargaNivel, NivelDeUso.DeCarga(l.Load.OneMinute, l.Cpu.CoreCount));
    }

    private void Discos(ServerSnapshot l)
    {
        _discos.ItemsSource = l.Disks
            .Select(d =>
            {
                var nivel = NivelDeUso.DePorcentaje(d.UsedPercent);

                return new FilaDisco(
                    d.MountPoint,
                    d.Type ?? string.Empty,

                    $"{Magnitudes.Tamano(d.AvailableBytes)} libres de {Magnitudes.Tamano(d.TotalBytes)} · {d.UsedPercent:0}%",
                    d.UsedPercent,
                    PincelDe(nivel),
                    NivelDeUso.Etiqueta(nivel) ?? string.Empty);
            })
            .ToList();

        var io = l.DiskIo ?? [];

        _ioPanel.Visibility = io.Count > 0 || l.Pressure.Io?.Some >= 1
            ? Visibility.Visible
            : Visibility.Collapsed;

        _discoIo.ItemsSource = io
            .Select(d => new FilaIo(
                d.Device,
                $"↓ {Velocidad(d.ReadBytesPerSecond)}   ↑ {Velocidad(d.WriteBytesPerSecond)}"
                + $"   ·   {d.BusyPercent:0}% ocupado"))
            .ToList();

        Presion(_ioPresion, "disco", l.Pressure.Io);
    }

    private void Red(ServerSnapshot l)
    {
        var trafico = l.Interfaces.ToDictionary(i => i.Interface, StringComparer.Ordinal);
        var configuradas = l.NetworkInterfaces ?? [];
        var elegidas = _recolector.InterfacesVisibles;

        var visibles = configuradas
            .Where(i => i.Name != "lo")
            .Where(i => !i.EsDeContenedor || elegidas.Contains(i.Name))
            .OrderByDescending(i => i.IsUp)
            .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (visibles.Count == 0)
        {
            _red.ItemsSource = l.Interfaces
                .Select(i => new FilaRed(
                    i.Interface,
                    $"↓ {Velocidad(i.BytesInPerSecond)}   ↑ {Velocidad(i.BytesOutPerSecond)}",
                    string.Empty,
                    string.Empty,
                    (Brush)FindResource("MedidaNormal")))
                .ToList();

            return;
        }

        _red.ItemsSource = visibles
            .Select(i =>
            {
                var suyo = trafico.GetValueOrDefault(i.Name);

                var detalle = suyo is null
                    ? "sin tráfico medido"
                    : $"↓ {Velocidad(suyo.BytesInPerSecond)}   ↑ {Velocidad(suyo.BytesOutPerSecond)}";

                var direcciones = string.Join("   ", i.IPv4.Concat(i.IPv6.Take(1)));

                var enlace = string.Join(" · ", new[]
                {
                    i.MacAddress,
                    i.Mtu > 0 ? $"MTU {i.Mtu}" : null,
                    i.IsUp ? "enlace activo" : $"enlace {i.State.ToLowerInvariant()}",
                }.Where(s => s is { Length: > 0 }));

                return new FilaRed(
                    i.Name,
                    detalle,
                    direcciones,
                    enlace,

                    (Brush)FindResource(i.IsUp
                        ? "MedidaNormal"
                        : i.IPv4.Count > 0 ? "MedidaAdvertencia" : "TextoTenue"));
            })
            .ToList();
    }

    private void Rutas(ServerSnapshot l)
    {
        // Un servidor con IPv6 tiene una ruta fe80::/64 por interfaz: veintiocho en uno de los servidores probados.
        var rutas = (l.Routes ?? [])
            .Where(r => !r.IsIPv6 || r.EsPredeterminada)
            .OrderByDescending(r => r.EsPredeterminada)
            .ThenBy(r => r.Destination, StringComparer.Ordinal)
            .ToList();

        var dns = l.System.Dns ?? [];

        _rutasPanel.Visibility = rutas.Count > 0 || dns.Count > 0
            ? Visibility.Visible
            : Visibility.Collapsed;

        _dns.Text = dns.Count == 0
            ? string.Empty
            : "DNS: " + string.Join(", ", dns)
              + (l.System.DnsSearch is { Length: > 0 } b ? $"   ·   búsqueda: {b}" : string.Empty);

        _dns.Visibility = dns.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

        _rutas.ItemsSource = rutas
            .Select(r => new FilaRuta(
                r.EsPredeterminada ? "predeterminada" : r.Destination,

                string.Join(" · ", new[]
                {
                    r.Gateway is { Length: > 0 } ? $"vía {r.Gateway}" : null,
                    r.Device,
                    r.LinkDown ? "interfaz caída" : null,
                }.Where(s => s is { Length: > 0 })),

                (Brush)FindResource(r.LinkDown
                    ? "MedidaAdvertencia"
                    : r.EsPredeterminada ? "Texto" : "TextoTenue")))
            .ToList();
    }

    private void Procesos()
    {
        var medidos = _procesosDelServidor.Ultima;

        _procesosPanel.Visibility = medidos is { Count: > 0 }
            ? Visibility.Visible
            : Visibility.Collapsed;

        if (medidos is null)
        {
            return;
        }

        var nucleos = _ultima?.Cpu.CoreCount ?? 0;

        var criterio = _ordenarPorCpu ? CriterioDeProcesos.Cpu : CriterioDeProcesos.Memoria;

        _procesos.ItemsSource = OrdenDeProcesos
            .Primeros(medidos, criterio, CuantosEnElTop)
            .Select(p => new FilaProceso(
                p.Pid,
                p.Nombre,

                // Sin acotar a 100: un proceso con ochenta hilos en ocho núcleos marca 341 %.
                p.PorcentajeDeCpu is { } cpu ? $"{cpu:0.#}%" : "\u2014",
                Magnitudes.Tamano(p.BytesResidentes),

                PincelDe(NivelDeUso.DePorcentaje(
                    nucleos > 0 ? (p.PorcentajeDeCpu ?? 0) / nucleos : p.PorcentajeDeCpu ?? 0)),
                Ficha(p),
                p.Nombre))
            .ToList();
    }

    /// <summary>Las siete columnas que FR-173 le exige al top; las que no entran a lo ancho van acá.</summary>
    private string Ficha(ProcesoMedido p) =>
        string.Join(" · ", new[]
        {
            $"PID {p.Pid}",
            p.Usuario is { Length: > 0 } ? p.Usuario : null,
            $"{p.Hilos} hilo(s)",
            $"estado {p.Estado}",
            p.TiempoCorriendo is { } corriendo
                ? $"corriendo hace {Magnitudes.Duracion(corriendo)}"
                : null,
        }.Where(t => t is { Length: > 0 }))
        + (_verProceso is null ? string.Empty : "\nDoble clic para la ficha completa");

    private void AlCambiarOrdenDeProcesos(object sender, RoutedEventArgs e)
    {
        _ordenarPorCpu = ReferenceEquals(sender, _porCpu);

        Procesos();
    }

    private void AlProcesoDobleClic(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount < 2
            || _verProceso is not { } abrir
            || sender is not FrameworkElement { DataContext: FilaProceso fila })
        {
            return;
        }

        e.Handled = true;
        _ = abrir(fila.Pid, fila.Comando);
    }

    private void AlVerTodosLosProcesos(object sender, RoutedEventArgs e) =>
        _verTodosLosProcesos?.Invoke();

    private void Temperaturas(ServerSnapshot l)
    {
        var temperaturas = l.Temperatures ?? [];

        _temperaturasPanel.Visibility = temperaturas.Count > 0
            ? Visibility.Visible
            : Visibility.Collapsed;

        _temperaturas.ItemsSource = temperaturas
            .Select(t => new FilaTemperatura(
                t.Sensor,
                $"{t.Celsius:0.#} °C",

                (Brush)FindResource(t.Celsius switch
                {
                    >= 85 => "MedidaCritica",
                    >= 70 => "MedidaAdvertencia",
                    _ => "MedidaNormal",
                })))
            .ToList();
    }

    private void Fallidos(ServerSnapshot l)
    {
        var fallidos = l.System.FailedServices;

        _fallidosTitulo.Visibility = fallidos.Count > 0
            ? Visibility.Visible
            : Visibility.Collapsed;

        _fallidos.ItemsSource = fallidos;
    }

    /// <summary>Pinta la barra y escribe el tramo al lado del título (FR-087a, FR-087b).</summary>
    private void Semaforo(ProgressBar? barra, TextBlock etiqueta, NivelDeMedida nivel)
    {
        if (barra is not null)
        {
            barra.Foreground = nivel == NivelDeMedida.Normal
                ? (Brush)FindResource("Primario")
                : PincelDe(nivel);
        }

        if (NivelDeUso.Etiqueta(nivel) is { } texto)
        {
            etiqueta.Text = texto;
            etiqueta.Foreground = PincelDe(nivel);
            etiqueta.Visibility = Visibility.Visible;
        }
        else
        {
            etiqueta.Visibility = Visibility.Collapsed;
        }
    }

    private Brush PincelDe(NivelDeMedida nivel) => (Brush)FindResource(nivel switch
    {
        NivelDeMedida.Critico => "MedidaCritica",
        NivelDeMedida.Advertencia => "MedidaAdvertencia",
        _ => "MedidaNormal",
    });

    private static string Velocidad(double bytesPorSegundo) =>
        bytesPorSegundo < 1024
            ? $"{bytesPorSegundo:0} B/s"
            : bytesPorSegundo < 1024 * 1024
                ? $"{bytesPorSegundo / 1024:0.#} KiB/s"
                : $"{bytesPorSegundo / (1024 * 1024):0.#} MiB/s";

    /// <summary>Rearma la lista de interfaces del selector con lo que se conoce hasta ahora.</summary>
    private void PoblarSelectorInterfaces()
    {
        if (_popupInterfaces.IsOpen)
        {
            return;
        }

        _listaInterfaces.Children.Clear();

        var conocidas = _recolector.InterfacesConocidas
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (conocidas.Count == 0)
        {
            _listaInterfaces.Children.Add(new TextBlock
            {
                Text = "Todavía no se detectó ninguna interfaz.",
                Style = (Style)FindResource("Tenue"),
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 180,
            });

            return;
        }

        var elegidas = _recolector.InterfacesVisibles;

        foreach (var nombre in conocidas)
        {
            var casilla = new CheckBox
            {
                Content = nombre,
                IsChecked = elegidas.Count == 0 || elegidas.Contains(nombre),
                Margin = new Thickness(0, 2, 0, 2),
                Foreground = (Brush)FindResource("Texto"),
            };

            casilla.Checked += (_, _) => AlElegirInterfaz();
            casilla.Unchecked += (_, _) => AlElegirInterfaz();

            _listaInterfaces.Children.Add(casilla);
        }
    }

    private void AlElegirInterfacesClick(object sender, RoutedEventArgs e)
    {
        if (!_popupInterfaces.IsOpen)
        {
            PoblarSelectorInterfaces();
            _popupInterfaces.IsOpen = true;
        }
        else
        {
            _popupInterfaces.IsOpen = false;
        }
    }

    private void AlElegirInterfaz()
    {
        var casillas = _listaInterfaces.Children.OfType<CheckBox>().ToList();

        var elegidas = casillas
            .Where(c => c.IsChecked == true)
            .Select(c => (string)c.Content)
            .ToList();

        _recolector.InterfacesVisibles = elegidas.Count == casillas.Count ? [] : elegidas;

        _ = _ajustes.SaveVisibleInterfacesAsync([.. _recolector.InterfacesVisibles]);

        _ = LeerAsync();
    }
}
