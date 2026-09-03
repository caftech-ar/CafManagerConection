using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using CafManagerConection.Platform;
using CafManagerConection.Terminal;

namespace CafManagerConection.App.Views;

/// <summary>De dónde saca un visor el registro que muestra: leerlo entero, decir qué archivos mira y seguirlos (FR-185, FR-185a, FR-185b).</summary>
public interface IFuenteDeRegistro
{
    string Titulo { get; }

    /// <summary>Por qué no se puede seguir en vivo, cuando <see cref="SeguirAsync"/> devolvió null.</summary>
    string? PorQueNoSigue { get; }

    Task<InventoryResult<string>> LeerAsync(CancellationToken ct = default);

    Task<IReadOnlyList<ArchivoSeguido>> ArchivosAsync(CancellationToken ct = default);

    /// <returns>El canal abierto, o null si esta fuente no se puede seguir.</returns>
    Task<IAsyncDisposable?> SeguirAsync(
        Action<string> onLinea, Action<string?> onCerrado, CancellationToken ct = default);
}

/// <summary>Visor de un registro remoto que se sigue en vivo, declara qué archivos mira y avisa cuando algo se rompe (FR-185 a FR-185c).</summary>
[SupportedOSPlatform("windows")]
public partial class VisorDeRegistroWindow : Window
{
    private const int FrecuenciaDeFechasSegundos = 30;

    private readonly IFuenteDeRegistro _fuente;
    private readonly DispatcherTimer _fechas;
    private readonly StringBuilder _copia = new();
    private readonly StringBuilder _pendiente = new();

    private TerminalControl? _terminal;
    private IAsyncDisposable? _canal;
    private CancellationTokenSource? _ctsCanal;
    private bool _listo;
    private bool _quiereSeguir = true;
    private int _lineas;
    private int _errores;

    private VisorDeRegistroWindow(IFuenteDeRegistro fuente)
    {
        _fuente = fuente;

        InitializeComponent();

        Title = fuente.Titulo;
        _titulo.Text = fuente.Titulo;

        _fechas = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(FrecuenciaDeFechasSegundos),
        };

        _fechas.Tick += async (_, _) => await DeclararArchivosAsync().ConfigureAwait(true);

        CrearTerminal();

        _host.Loaded += (_, _) => Volcar();

        Loaded += async (_, _) => await ArrancarAsync().ConfigureAwait(true);
        Closed += async (_, _) => await CerrarCanalAsync().ConfigureAwait(false);
    }

    private void CrearTerminal()
    {
        _terminal?.Dispose();

        _terminal = new TerminalControl { Dock = System.Windows.Forms.DockStyle.Fill };
        _terminal.ApplyTheme(dark: true, "Cascadia Mono", 10, scrollback: 5000);

        _host.Child = _terminal;
    }

    private async Task ArrancarAsync()
    {
        await LeerAsync().ConfigureAwait(true);
        await DeclararArchivosAsync().ConfigureAwait(true);
        await SeguirAsync().ConfigureAwait(true);

        _fechas.Start();
    }

    private async Task LeerAsync()
    {
        _estado.Text = "Leyendo el registro…";
        _forzar.IsEnabled = false;

        var r = await _fuente.LeerAsync().ConfigureAwait(true);

        _forzar.IsEnabled = true;

        if (!r.Success)
        {
            _estado.Text = r.Error ?? "No se pudo leer el registro.";
            Avisar(r.Error ?? "No se pudo leer el registro.");
            return;
        }

        Escribir(r.Value ?? string.Empty);
        _estado.Text = $"Leído {DateTimeOffset.Now:HH:mm:ss}";
    }

    /// <summary>Escribe cada línea con el color de su nivel, para no perder el error entre miles de líneas (FR-100f).</summary>
    private void Escribir(string registro)
    {
        var conError = registro
            .ReplaceLineEndings("\n")
            .Split('\n')
            .Count(linea => Agregar(linea, avisar: false));

        if (conError == 0)
        {
            return;
        }

        _errores += conError;
        Avisar($"El registro que se leyó trae {conError} línea(s) de error.");
    }

    /// <returns>Si la línea es de error, para poder contarlas (FR-185c).</returns>
    private bool Agregar(string linea, bool avisar)
    {
        if (SeguimientoDeArchivo.Diagnostico(linea) is { } diagnostico)
        {
            AgregarConColor(diagnostico.Texto, GravedadDeLinea.Error);

            if (diagnostico.Clase == ClaseDeAviso.Inaccesible)
            {
                Avisar(diagnostico.Texto);
                return false;
            }

            _estado.Text = diagnostico.Texto;
            return false;
        }

        var gravedad = NivelDeLinea.De(linea);

        AgregarConColor(linea, gravedad);

        if (gravedad != GravedadDeLinea.Error)
        {
            return false;
        }

        if (!avisar)
        {
            return true;
        }

        _errores++;
        Avisar($"Apareció una línea de error en el registro ({_errores} desde que se abrió).");

        return true;
    }

    private void AgregarConColor(string linea, GravedadDeLinea gravedad)
    {
        var codigo = gravedad switch
        {
            GravedadDeLinea.Error => "\x1b[91m",
            GravedadDeLinea.Advertencia => "\x1b[93m",
            _ => null,
        };

        var texto = linea.Contains('\x1b') || codigo is null
            ? linea + "\r\n"
            : codigo + linea + "\x1b[0m\r\n";

        _copia.AppendLine(linea);

        if (_copia.Length > 8_000_000)
        {
            _copia.Remove(0, _copia.Length / 2);
        }

        _lineas++;
        _cuentaLineas.Text = _lineas == 1 ? "1 línea" : $"{_lineas} líneas";

        if (!_listo)
        {
            _pendiente.Append(texto);
            return;
        }

        _terminal?.Write(Encoding.UTF8.GetBytes(texto));
    }

    private void Volcar()
    {
        _listo = true;

        if (_pendiente.Length == 0)
        {
            return;
        }

        _terminal?.Write(Encoding.UTF8.GetBytes(_pendiente.ToString()));
        _pendiente.Clear();
    }

    /// <summary>Dice qué archivos se están mirando y cuándo cambió cada uno (FR-185a).</summary>
    private async Task DeclararArchivosAsync()
    {
        var archivos = await _fuente.ArchivosAsync().ConfigureAwait(true);

        _archivos.Children.Clear();

        if (archivos.Count == 0)
        {
            _archivos.Children.Add(Fila(
                "No se pudo resolver ningún archivo de registro en el servidor.", tenue: true));

            return;
        }

        foreach (var archivo in archivos)
        {
            _archivos.Children.Add(Fila($"{archivo.Ruta}  ·  {archivo.Cambio()}", tenue: false));
        }
    }

    private TextBlock Fila(string texto, bool tenue)
    {
        var t = new TextBlock
        {
            Text = texto,
            TextTrimming = TextTrimming.CharacterEllipsis,
            FontFamily = (System.Windows.Media.FontFamily)FindResource("FuenteMono"),
            FontSize = (double)FindResource("CuerpoChico"),
            Margin = new Thickness(0, 1, 0, 1),
        };

        t.SetResourceReference(
            TextBlock.ForegroundProperty, tenue ? "TextoTenue" : "Texto");

        return t;
    }

    private async Task SeguirAsync()
    {
        if (_canal is not null)
        {
            return;
        }

        _seguir.IsEnabled = false;

        var cts = new CancellationTokenSource();
        _ctsCanal = cts;

        try
        {
            var canal = await _fuente.SeguirAsync(
                linea => Dispatcher.BeginInvoke(() => Agregar(linea, avisar: true)),
                motivo => Dispatcher.BeginInvoke(() => Corto(motivo)),
                cts.Token).ConfigureAwait(true);

            if (canal is null)
            {
                _quiereSeguir = false;
                _seguir.IsChecked = false;
                _seguir.IsEnabled = true;
                _seguir.ToolTip = _fuente.PorQueNoSigue;
                _estado.Text = _fuente.PorQueNoSigue ?? "Este registro no se puede seguir en vivo.";
                return;
            }

            _canal = canal;
            _quiereSeguir = true;
            _seguir.IsChecked = true;
            _seguir.IsEnabled = true;
            _puntoEnVivo.SetResourceReference(
                System.Windows.Shapes.Shape.FillProperty, "EstadoConectado");

            _estado.Text = "En vivo.";
        }
        catch (Exception ex)
        {
            _seguir.IsChecked = false;
            _seguir.IsEnabled = true;
            _estado.Text = ex.Message;
            Avisar($"No se pudo abrir el seguimiento en vivo: {ex.Message}");
        }
    }

    private async Task CerrarCanalAsync()
    {
        if (_canal is not { } canal)
        {
            return;
        }

        _canal = null;
        _ctsCanal?.Cancel();
        _ctsCanal?.Dispose();
        _ctsCanal = null;

        await canal.DisposeAsync().ConfigureAwait(true);
    }

    private async Task PararAsync()
    {
        await CerrarCanalAsync().ConfigureAwait(true);

        _quiereSeguir = false;
        _seguir.IsChecked = false;
        _puntoEnVivo.SetResourceReference(
            System.Windows.Shapes.Shape.FillProperty, "EstadoInactivo");

        _estado.Text = "Sin seguir. Lo que se ve es de la última lectura.";
    }

    /// <summary>El canal se cerró por su cuenta: hay que decirlo o el registro queda congelado sin avisar (FR-185c).</summary>
    private void Corto(string? motivo)
    {
        _canal = null;
        _ctsCanal?.Dispose();
        _ctsCanal = null;

        _seguir.IsChecked = false;
        _puntoEnVivo.SetResourceReference(
            System.Windows.Shapes.Shape.FillProperty, "EstadoError");

        var texto = motivo is { Length: > 0 }
            ? motivo
            : "Se cortó el canal del registro en vivo.";

        AgregarConColor($"« {texto} »", GravedadDeLinea.Error);
        Avisar(texto);
    }

    /// <summary>El aviso vive en la ventana y no en el registro: tiene que verse aunque el usuario esté en otra pestaña (FR-185c).</summary>
    private void Avisar(string texto)
    {
        _aviso.Text = texto;
        _marcoAviso.Visibility = Visibility.Visible;

        Title = _errores > 0
            ? $"{_fuente.Titulo} · {_errores} con error"
            : $"{_fuente.Titulo} · atención";
    }

    private void AlDescartarAviso(object sender, RoutedEventArgs e)
    {
        _marcoAviso.Visibility = Visibility.Collapsed;
        Title = _fuente.Titulo;
    }

    private async void AlAlternarSeguir(object sender, RoutedEventArgs e)
    {
        if (_canal is null)
        {
            await SeguirAsync().ConfigureAwait(true);
            return;
        }

        await PararAsync().ConfigureAwait(true);
    }

    /// <summary>Relee el archivo de verdad y vuelve a dibujar: no alcanza con repintar lo que ya estaba (FR-185b).</summary>
    private async void AlForzarLectura(object sender, RoutedEventArgs e)
    {
        await CerrarCanalAsync().ConfigureAwait(true);

        CrearTerminal();

        _listo = true;
        _pendiente.Clear();
        _copia.Clear();
        _lineas = 0;

        await LeerAsync().ConfigureAwait(true);
        await DeclararArchivosAsync().ConfigureAwait(true);

        if (_quiereSeguir)
        {
            await SeguirAsync().ConfigureAwait(true);
        }
    }

    private void AlBuscar(object sender, TextChangedEventArgs e)
    {
        if (_terminal is not { } terminal)
        {
            return;
        }

        var texto = _buscar.Text;

        terminal.Buscar(texto);

        _resultado.Text = texto.Length == 0
            ? string.Empty
            : terminal.TotalCoincidencias == 0
                ? "sin coincidencias"
                : $"{terminal.CoincidenciaActual} de {terminal.TotalCoincidencias}";
    }

    private void AlCopiar(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(_copia.ToString());
        }
        catch (COMException)
        {
        }
    }

    private void AlCerrar(object sender, RoutedEventArgs e) => Close();

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);

        _fechas.Stop();
        _host.Child = null;
        _terminal?.Dispose();
        _terminal = null;
    }

    /// <summary>Abre el visor sin bloquear la sesión: seguir en vivo dentro de una ventana modal no sirve de nada (FR-185e).</summary>
    public static VisorDeRegistroWindow Mostrar(Window owner, IFuenteDeRegistro fuente)
    {
        var visor = new VisorDeRegistroWindow(fuente) { Owner = owner };

        visor.Show();

        return visor;
    }
}
