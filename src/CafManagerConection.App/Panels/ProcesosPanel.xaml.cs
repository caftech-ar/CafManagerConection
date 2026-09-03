using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using CafManagerConection.Domain.Settings;
using CafManagerConection.Monitoring;
using CafManagerConection.UseCases.Abstractions;

namespace CafManagerConection.App.Panels;

/// <summary>Procesos del servidor, ordenables por CPU, memoria o disco, con los hijos del elegido (FR-183).</summary>
[SupportedOSPlatform("windows")]
public partial class ProcesosPanel : UserControl
{
    /// <summary>Cuántos procesos se muestran como raíz. Los hijos salen debajo del suyo, sin contar acá.</summary>
    private const int CuantasFilas = 40;

    private readonly MonitorDeProcesos _monitor;
    private readonly IAppSettingsService _ajustes;
    private readonly DispatcherTimer _reloj;

    /// <summary>Con qué abrir la ficha de un proceso, o null si esta sesión no puede.</summary>
    private readonly Func<int, string, Task>? _verProceso;

    /// <summary>Los PID desplegados, para que un refresco no cierre lo que el usuario abrió.</summary>
    private readonly HashSet<int> _desplegados = [];

    private CriterioDeProcesos _criterio = CriterioDeProcesos.Cpu;
    private ResultadoDeSondeo? _sondeo;
    private bool _leyendo;

    public ProcesosPanel(
        MonitorDeProcesos monitor,
        IAppSettingsService ajustes,
        Func<int, string, Task>? verProceso = null)
    {
        _monitor = monitor;
        _ajustes = ajustes;
        _verProceso = verProceso;

        // El reloj antes de InitializeComponent: AlCambiarIntervalo lo desreferencia y el XAML puede dispararlo al construir.
        _reloj = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(Defaults.MetricsSampleIntervalSeconds),
        };

        _reloj.Tick += async (_, _) => await LeerAsync().ConfigureAwait(true);

        InitializeComponent();

        // Marcado por código y no en el XAML: ahí el Checked corre durante InitializeComponent(),
        // cuando _arbol todavía es null, y el panel entero no abre.
        _porCpu.IsChecked = true;

        _intervalo.ItemsSource = Defaults.IntervalosDeMuestreo.Select(s => $"{s} s").ToList();

        Loaded += async (_, _) =>
            AplicarIntervalo(await _ajustes.GetStatusIntervalAsync().ConfigureAwait(true));

        // Al cambiar de panel el control sale del árbol visual: sin esto el reloj queda huérfano muestreando un panel que nadie mira.
        Unloaded += (_, _) => Detener();
    }

    public void Iniciar()
    {
        _reloj.Start();
        _ = LeerAsync();
    }

    public void Detener() => _reloj.Stop();

    /// <summary>Qué se sabe de la escalada de esta sesión: sin esto el botón no aparece (FR-184a).</summary>
    public void AplicarSondeo(ResultadoDeSondeo? sondeo)
    {
        _sondeo = sondeo;
        MostrarEscalada();
    }

    private void MostrarEscalada()
    {
        if (!_monitor.PuedeEscalar || _monitor.ConPrivilegios)
        {
            _escalada.Visibility = _monitor.ConPrivilegios ? Visibility.Visible : Visibility.Collapsed;
            _botonEscalar.Visibility = Visibility.Collapsed;

            if (_monitor.ConPrivilegios)
            {
                _escaladaTexto.Text =
                    "Leyendo con privilegios: la entrada y salida de disco de los procesos ajenos "
                    + "también se ve.";
            }

            return;
        }

        _escalada.Visibility = Visibility.Visible;
        _escaladaTexto.Text = MensajeDeEscalada.Texto(_sondeo, "la E/S de los procesos ajenos");

        _botonEscalar.Visibility = MensajeDeEscalada.MuestraElBoton(_sondeo)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private async void AlEscalar(object sender, RoutedEventArgs e)
    {
        _monitor.ConPrivilegios = true;
        MostrarEscalada();

        await LeerAsync().ConfigureAwait(true);

        if (_monitor.UltimoError is { Length: > 0 })
        {
            _monitor.ConPrivilegios = false;
            MostrarEscalada();

            _escaladaTexto.Text =
                "La escalada no sirvió y se sigue leyendo sin privilegios: "
                + _monitor.UltimoError;
        }
    }

    private void AplicarIntervalo(int segundos)
    {
        _reloj.Interval = TimeSpan.FromSeconds(segundos);

        var i = Array.IndexOf(Defaults.IntervalosDeMuestreo, segundos);

        if (i >= 0 && _intervalo.SelectedIndex != i)
        {
            _intervalo.SelectedIndex = i;
        }
    }

    private void AlCambiarIntervalo(object sender, SelectionChangedEventArgs e)
    {
        if (_intervalo.SelectedIndex < 0
            || _intervalo.SelectedIndex >= Defaults.IntervalosDeMuestreo.Length)
        {
            return;
        }

        var segundos = Defaults.IntervalosDeMuestreo[_intervalo.SelectedIndex];

        if (_reloj.Interval == TimeSpan.FromSeconds(segundos))
        {
            return;
        }

        _reloj.Interval = TimeSpan.FromSeconds(segundos);
        _ = _ajustes.SaveStatusIntervalAsync(segundos);

        _ = LeerAsync();
    }

    private void AlCambiarOrden(object sender, RoutedEventArgs e)
    {
        _criterio = ReferenceEquals(sender, _porMemoria)
            ? CriterioDeProcesos.Memoria
            : ReferenceEquals(sender, _porDisco)
                ? CriterioDeProcesos.Disco
                : CriterioDeProcesos.Cpu;

        if (_monitor.Ultima is { } filas)
        {
            Pintar(filas);
        }
    }

    /// <summary>Cuánto vale la muestra que ya trajo el otro panel antes de pedir otra (SC-050a).</summary>
    private TimeSpan Frescura() => _reloj.Interval * 0.6;

    private async Task LeerAsync()
    {
        if (_leyendo)
        {
            return;
        }

        _leyendo = true;

        IReadOnlyList<ProcesoMedido>? filas;

        try
        {
            filas = await _monitor
                .MuestraAsync(Frescura(), Defaults.MetricsQueryTimeoutSeconds)
                .ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            SinLectura(ex.Message);
            return;
        }
        finally
        {
            _leyendo = false;
        }

        if (filas is null)
        {
            SinLectura(_monitor.UltimoError ?? "No se pudieron leer los procesos del servidor.");
            return;
        }

        Pintar(filas);

        _ultimaLectura.Text = _monitor.Instante is { } cuando
            ? $"leído {cuando.ToLocalTime():HH:mm:ss}"
            : string.Empty;
    }

    private void SinLectura(string motivo)
    {
        _arbol.ItemsSource = null;
        _resumen.Text = motivo;
        _resumen.Foreground = (Brush)FindResource("Destructivo");
    }

    private void Pintar(IReadOnlyList<ProcesoMedido> filas)
    {
        var indice = new IndiceDeProcesos(filas);
        var raices = OrdenDeProcesos.Primeros(filas, _criterio, CuantasFilas);

        _arbol.ItemsSource = raices.Select(p => Nodo(indice.Subarbol(p))).ToList();

        _resumen.Foreground = (Brush)FindResource("TextoTenue");

        var conDisco = filas.Count(p => p.TieneDisco);

        _resumen.Text = _monitor.TienePorcentajes
            ? $"{filas.Count} procesos · {conDisco} con E/S visible · "
              + $"orden por {Criterio()}"
            : $"{filas.Count} procesos · el % de CPU sale en la próxima lectura, "
              + "que es la diferencia contra ésta";
    }

    private string Criterio() => _criterio switch
    {
        CriterioDeProcesos.Memoria => "memoria residente",
        CriterioDeProcesos.Disco => "bytes leídos y escritos",
        _ => "CPU del instante",
    };

    private FilaDeProceso Nodo(NodoDeProceso nodo)
    {
        var p = nodo.Proceso;

        var detalle = string.Join(" · ", new[]
        {
            $"PID {p.Pid}",
            p.Usuario is { Length: > 0 } ? p.Usuario : null,
            $"{p.Hilos} hilo(s)",
            $"estado {p.Estado}",
            p.TiempoCorriendo is { } corriendo
                ? $"corriendo hace {Magnitudes.Duracion(corriendo)}"
                : null,
            nodo.Hijos.Count > 0
                ? $"{nodo.Hijos.Count} hijo(s) · el subárbol suma "
                  + $"{nodo.CpuDelSubarbol:0.#}% y {Magnitudes.Tamano(nodo.BytesResidentesDelSubarbol)}"
                : null,
            p.TieneDisco ? null : "sin permiso para ver su E/S",
        }.Where(t => t is { Length: > 0 }));

        return new FilaDeProceso(
            p.Pid,
            p.Nombre,
            p.PorcentajeDeCpu is { } cpu ? $"{cpu:0.#}%" : "—",
            Magnitudes.Tamano(p.BytesResidentes),
            p.TieneDisco ? Magnitudes.Tamano(p.BytesDeDisco) : "—",
            PincelDeCpu(p.PorcentajeDeCpu),
            _verProceso is null ? detalle : detalle + "\nDoble clic para la ficha completa",
            p.Nombre,
            [.. nodo.Hijos.Select(Nodo)],
            _desplegados.Contains(p.Pid),
            Desplegar);
    }

    private void Desplegar(int pid, bool abierto)
    {
        if (abierto)
        {
            _desplegados.Add(pid);
        }
        else
        {
            _desplegados.Remove(pid);
        }
    }

    // El porcentaje es de un núcleo, como en htop: un proceso con ochenta hilos en ocho núcleos marca 341 % y el color se mira contra 100, no contra el total de la máquina.
    private Brush PincelDeCpu(double? porcentaje)
    {
        if (porcentaje is not { } valor)
        {
            return (Brush)FindResource("TextoTenue");
        }

        return (Brush)FindResource(NivelDeUso.DePorcentaje(valor) switch
        {
            NivelDeMedida.Critico => "MedidaCritica",
            NivelDeMedida.Advertencia => "MedidaAdvertencia",
            _ => "MedidaNormal",
        });
    }

    private void AlProcesoDobleClic(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount < 2
            || _verProceso is not { } abrir
            || sender is not FrameworkElement { DataContext: FilaDeProceso fila })
        {
            return;
        }

        e.Handled = true;
        _ = abrir(fila.Pid, fila.Comando);
    }

    /// <summary>Una fila del árbol de procesos, con sus hijos y si el usuario la dejó abierta.</summary>
    public sealed class FilaDeProceso(
        int pid,
        string nombre,
        string cpu,
        string memoria,
        string disco,
        Brush colorCpu,
        string ayuda,
        string comando,
        IReadOnlyList<FilaDeProceso> hijos,
        bool expandido,
        Action<int, bool> recordar)
    {
        private bool _expandido = expandido;

        public int Pid => pid;

        public string PidTexto => pid.ToString(System.Globalization.CultureInfo.InvariantCulture);

        public string Nombre => nombre;

        /// <summary>Clave del icono cuando el proceso se reconoce, y <c>null</c> cuando no.</summary>
        public string? ClaveDeIcono => Domain.Monitoring.IconoDeProceso.ClaveDeIcono(nombre);

        public Visibility VisibilidadDelIcono =>
            ClaveDeIcono is null ? Visibility.Collapsed : Visibility.Visible;

        public string Cpu => cpu;

        public string Memoria => memoria;

        public string Disco => disco;

        public Brush ColorCpu => colorCpu;

        public string Ayuda => ayuda;

        public string Comando => comando;

        public IReadOnlyList<FilaDeProceso> Hijos => hijos;

        /// <summary>El estilo implícito de TreeViewItem la ata a IsExpanded en las dos direcciones.</summary>
        public bool Expandido
        {
            get => _expandido;

            set
            {
                _expandido = value;
                recordar(pid, value);
            }
        }
    }
}
