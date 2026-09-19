using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Media;
using CafManagerConection.App.Bootstrap;
using CafManagerConection.App.Services;
using CafManagerConection.App.Themes;
using CafManagerConection.UseCases.Connections;

namespace CafManagerConection.App.Views;

/// <summary>Un puerto configurado de la conexión, con su resultado.</summary>
[SupportedOSPlatform("windows")]
public sealed class FilaDePuerto(string puerto, string descripcion, string estado, Brush color)
{
    public string Puerto { get; } = puerto;

    public string Descripcion { get; } = descripcion;

    public string Estado { get; } = estado;

    public Brush Color { get; } = color;
}

/// <summary>Una conexión sondeada, que se despliega para ver el resto de sus puertos.</summary>
[SupportedOSPlatform("windows")]
public sealed class FilaDePing(ConnectionSummary conexion) : INotifyPropertyChanged
{
    private string _estado = SondeoDeHosts.Titulo(EstadoDeHost.Sondeando);
    private string _detalle = string.Empty;
    private Brush _color = Pinceles.De("EstadoInactivo");
    private bool _desplegada;

    public ConnectionSummary Conexion { get; } = conexion;

    public string Nombre => Conexion.Name;

    public string Host => string.IsNullOrWhiteSpace(Conexion.Host) ? "—" : Conexion.Host;

    public string Puerto { get; private set; } =
        conexion.EffectivePort.ToString(CultureInfo.InvariantCulture);

    public ObservableCollection<FilaDePuerto> Puertos { get; } = [];

    /// <summary>Qué hacer cuando el usuario despliega la fila; el sondeo de los puertos va acá.</summary>
    public Func<FilaDePing, Task>? AlDesplegar { get; set; }

    public EstadoDeHost Valor { get; private set; } = EstadoDeHost.Sondeando;

    public string Estado
    {
        get => _estado;
        private set => Asignar(ref _estado, value);
    }

    public string Detalle
    {
        get => _detalle;
        private set => Asignar(ref _detalle, value);
    }

    public Brush Color
    {
        get => _color;
        private set => Asignar(ref _color, value);
    }

    public bool Desplegada
    {
        get => _desplegada;
        set
        {
            if (!Asignar(ref _desplegada, value) || !value)
            {
                return;
            }

            _ = AlDesplegar?.Invoke(this);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Vuelca el resultado del sondeo en la fila.</summary>
    /// <param name="sondeo">Lo que devolvió el sondeo de esta conexión.</param>
    public void Mostrar(FilaDeSondeo sondeo)
    {
        ArgumentNullException.ThrowIfNull(sondeo);

        Valor = sondeo.Estado;
        Estado = sondeo.Titulo;
        Detalle = sondeo.Detalle;
        Color = ColorDe(sondeo.Estado);

        if (sondeo.Estado == EstadoDeHost.NoSeSondea)
        {
            Puerto = "—";
            OnPropertyChanged(nameof(Puerto));
        }
    }

    /// <summary>Deja la fila como recién abierta, para volver a sondearla.</summary>
    public void Reiniciar()
    {
        Valor = EstadoDeHost.Sondeando;
        Estado = SondeoDeHosts.Titulo(EstadoDeHost.Sondeando);
        Detalle = string.Empty;
        Color = Pinceles.De("EstadoInactivo");
        Puertos.Clear();
        _desplegada = false;
        OnPropertyChanged(nameof(Desplegada));
    }

    private static Brush ColorDe(EstadoDeHost estado) => Pinceles.De(estado switch
    {
        EstadoDeHost.Vivo => "EstadoConectado",
        EstadoDeHost.SinServicio => "MedidaAdvertencia",
        EstadoDeHost.SinRespuesta => "EstadoError",
        _ => "EstadoInactivo",
    });

    private bool Asignar<T>(ref T campo, T valor, [CallerMemberName] string? propiedad = null)
    {
        if (EqualityComparer<T>.Default.Equals(campo, valor))
        {
            return false;
        }

        campo = valor;
        OnPropertyChanged(propiedad);

        return true;
    }

    private void OnPropertyChanged(string? propiedad) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propiedad));
}

/// <summary>Estado de vida de un conjunto de conexiones, en una sola pasada.</summary>
[SupportedOSPlatform("windows")]
public partial class PingView : System.Windows.Controls.UserControl, IDisposable
{
    private readonly CompositionRoot _root;
    private readonly IReadOnlyList<ConnectionSummary> _conexiones;
    private readonly ObservableCollection<FilaDePing> _filas = [];
    private CancellationTokenSource? _corte;
    private bool _dispuesto;

    public PingView(CompositionRoot root, IReadOnlyList<ConnectionSummary> conexiones)
    {
        ArgumentNullException.ThrowIfNull(conexiones);

        _root = root;
        _conexiones = conexiones;

        InitializeComponent();

        _arbol.ItemsSource = _filas;

        Loaded += (_, _) => _ = SondearAsync();
    }

    public void Dispose()
    {
        if (_dispuesto)
        {
            return;
        }

        _dispuesto = true;
        Cancelar();
    }

    private async Task SondearAsync()
    {
        Cancelar();

        var corte = new CancellationTokenSource();
        _corte = corte;

        _repetir.IsEnabled = false;

        if (_filas.Count == 0)
        {
            foreach (var conexion in _conexiones)
            {
                _filas.Add(new FilaDePing(conexion) { AlDesplegar = SondearLosPuertosAsync });
            }
        }
        else
        {
            foreach (var fila in _filas)
            {
                fila.Reiniciar();
            }
        }

        _resumen.Text = $"Sondeando {_filas.Count} conexión(es)…";

        var porId = _filas.ToDictionary(f => f.Conexion.Id);
        var reloj = Stopwatch.StartNew();

        try
        {
            await SondeoDeHosts.SondearLoteAsync(
                _conexiones,
                sondeo => Dispatcher.BeginInvoke(() =>
                {
                    if (porId.TryGetValue(sondeo.Conexion.Id, out var fila))
                    {
                        fila.Mostrar(sondeo);
                    }
                }),
                ct: corte.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        reloj.Stop();
        Resumir(reloj.Elapsed);
        _repetir.IsEnabled = true;
    }

    /// <summary>Sondea los puertos configurados de una conexión, recién cuando se despliega su fila.</summary>
    private async Task SondearLosPuertosAsync(FilaDePing fila)
    {
        if (fila.Puertos.Count > 0 || _corte is not { } corte)
        {
            return;
        }

        try
        {
            var detalle = await _root.ConnectionService
                .GetDetailAsync(fila.Conexion.Id, corte.Token).ConfigureAwait(true);

            var tuneles = await _root.Tunnels
                .GetForConnectionAsync(fila.Conexion.Id, corte.Token).ConfigureAwait(true);

            var puertos = PuertosDeLaConexion.De(fila.Conexion, detalle, tuneles);

            if (puertos.Count == 0)
            {
                fila.Puertos.Add(new FilaDePuerto(
                    "—", "sin puertos configurados", string.Empty, Pinceles.De("EstadoInactivo")));

                return;
            }

            var resultados = await SondeoDeHosts
                .SondearPuertosAsync(puertos, ct: corte.Token).ConfigureAwait(true);

            foreach (var (puerto, abre) in resultados)
            {
                fila.Puertos.Add(new FilaDePuerto(
                    puerto.Puerto.ToString(CultureInfo.InvariantCulture),
                    $"{puerto.Descripcion} · {puerto.Host}",
                    abre ? "acepta la conexión" : "no acepta",
                    Pinceles.De(abre ? "EstadoConectado" : "EstadoError")));
            }
        }
        catch (OperationCanceledException)
        {
            // Se cerró la pestaña mientras se sondeaba.
        }
        catch (Exception ex)
        {
            _root.Logger.TechnicalError("sondear los puertos de una conexión", ex);
        }
    }

    private void Resumir(TimeSpan tardo)
    {
        var partes = new List<string> { $"{_filas.Count} conexión(es)" };

        void Contar(EstadoDeHost estado, string texto)
        {
            var cuantas = _filas.Count(f => f.Valor == estado);

            if (cuantas > 0)
            {
                partes.Add($"{cuantas} {texto}");
            }
        }

        Contar(EstadoDeHost.Vivo, "viva(s)");
        Contar(EstadoDeHost.SinServicio, "sin servicio");
        Contar(EstadoDeHost.SinRespuesta, "sin respuesta");
        Contar(EstadoDeHost.SinResolver, "sin resolución de nombre");
        Contar(EstadoDeHost.NoSeSondea, "no sondeada(s)");

        _resumen.Text = string.Join(" · ", partes)
            + $"   ({tardo.TotalSeconds.ToString("0.0", CultureInfo.CurrentCulture)} s)";
    }

    private void Cancelar()
    {
        _corte?.Cancel();
        _corte?.Dispose();
        _corte = null;
    }

    private void AlRepetir(object sender, RoutedEventArgs e) => _ = SondearAsync();
}
