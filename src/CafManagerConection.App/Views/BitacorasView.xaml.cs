using System.Globalization;
using System.IO;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CafManagerConection.App.Bootstrap;
using CafManagerConection.Infrastructure.Bitacora;

namespace CafManagerConection.App.Views;

/// <summary>Las bitácoras de sesión escritas: listado por fecha y lectura del contenido.</summary>
[SupportedOSPlatform("windows")]
public partial class BitacorasView : UserControl, IDisposable
{
    private readonly CompositionRoot _root;
    private readonly string _carpeta;
    private IReadOnlyList<BitacoraEnDisco> _todas = [];
    private BitacoraEnDisco? _abierta;
    private long _leidoDesde;
    private CancellationTokenSource? _corte;
    private bool _dispuesto;

    /// <summary>Una fila del listado, ya lista para mostrar.</summary>
    public sealed record Fila(string Cuando, string Conexion, string Tamano, BitacoraEnDisco Origen);

    public BitacorasView(CompositionRoot root, string carpeta, string? conexion = null)
    {
        _root = root;
        _carpeta = carpeta;

        InitializeComponent();

        Loaded += (_, _) => Cargar(conexion);
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

    private void Cargar(string? conexion)
    {
        _todas = CatalogoDeBitacoras.Listar(_carpeta, _root.Logger);

        var conexiones = _todas
            .Select(b => b.Conexion)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var opciones = new List<string> { "(todas)" };
        opciones.AddRange(conexiones);

        _filtro.ItemsSource = opciones;

        _filtro.SelectedItem = conexion is { Length: > 0 }
                               && opciones.Any(o => o.Equals(conexion, StringComparison.OrdinalIgnoreCase))
            ? opciones.First(o => o.Equals(conexion, StringComparison.OrdinalIgnoreCase))
            : opciones[0];

        Filtrar();
    }

    private void Filtrar()
    {
        var elegida = _filtro.SelectedItem as string;

        var visibles = elegida is null or "(todas)"
            ? _todas
            : [.. _todas.Where(b => b.Conexion.Equals(elegida, StringComparison.OrdinalIgnoreCase))];

        _listado.ItemsSource = visibles
            .Select(b => new Fila(
                b.Inicio.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture),
                string.IsNullOrEmpty(b.Host) ? b.Conexion : $"{b.Conexion} · {b.Host}",
                Legible(b.Bytes),
                b))
            .ToList();

        _vacio.Text = _todas.Count == 0
            ? "Todavía no hay bitácoras escritas. Se activan en Preferencias → Sesiones."
            : $"«{elegida}» no tiene bitácoras. Elegí «(todas)» para ver las demás.";

        _vacio.Visibility = visibles.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        _resumen.Text = $"{visibles.Count} de {_todas.Count} registro(s)";
    }

    private void AlCambiarFiltro(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded)
        {
            Filtrar();
        }
    }

    private void AlElegirBitacora(object sender, SelectionChangedEventArgs e)
    {
        if (_listado.SelectedItem is Fila fila)
        {
            _ = AbrirAsync(fila.Origen);
        }
    }

    private async Task AbrirAsync(BitacoraEnDisco bitacora)
    {
        Cancelar();

        var corte = new CancellationTokenSource();
        _corte = corte;

        _abierta = bitacora;
        _contenido.Clear();
        _estadoBusqueda.Text = string.Empty;

        var largo = LectorDeBitacora.Largo(bitacora.Ruta);

        if (largo == 0 && !File.Exists(bitacora.Ruta))
        {
            _estadoContenido.Text = "El archivo ya no está: pudo haberlo borrado la purga.";
            _masAtras.IsEnabled = false;
            Cargar(_filtro.SelectedItem as string);
            return;
        }

        await MostrarTramoAsync(largo, reemplazar: true, corte.Token).ConfigureAwait(true);
    }

    private async Task MostrarTramoAsync(long hasta, bool reemplazar, CancellationToken ct)
    {
        if (_abierta is not { } bitacora)
        {
            return;
        }

        try
        {
            var (texto, desde) = await LectorDeBitacora
                .LeerHaciaAtrasAsync(bitacora.Ruta, hasta, ct: ct).ConfigureAwait(true);

            if (reemplazar)
            {
                _contenido.Text = texto;
                _contenido.CaretIndex = _contenido.Text.Length;
                _contenido.ScrollToEnd();
            }
            else
            {
                _contenido.Text = texto + _contenido.Text;
                _contenido.CaretIndex = 0;
                _contenido.ScrollToHome();
            }

            _leidoDesde = desde;
            _masAtras.IsEnabled = desde > 0;

            _estadoContenido.Text = desde > 0
                ? $"Mostrando el final · quedan {Legible(desde)} hacia atrás"
                : $"Bitácora completa · {Legible(bitacora.Bytes)}";
        }
        catch (OperationCanceledException)
        {
            // Se cerró la pestaña o se eligió otra bitácora.
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _root.Logger.TechnicalError("leer una bitácora de sesión", ex);
            _estadoContenido.Text = "No se pudo leer el archivo. El motivo quedó en el registro.";
        }
    }

    private void AlCargarLoAnterior(object sender, RoutedEventArgs e)
    {
        if (_corte is { } corte)
        {
            _ = MostrarTramoAsync(_leidoDesde, reemplazar: false, corte.Token);
        }
    }

    private void AlTeclearEnBusqueda(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Buscar();
            e.Handled = true;
        }
    }

    private void AlBuscar(object sender, RoutedEventArgs e) => Buscar();

    /// <summary>Lleva a la coincidencia siguiente desde donde está el cursor, y vuelve al principio al llegar al final.</summary>
    private void Buscar()
    {
        var texto = _busqueda.Text;

        if (texto.Length == 0 || _contenido.Text.Length == 0)
        {
            _estadoBusqueda.Text = string.Empty;
            return;
        }

        var desde = Math.Min(_contenido.CaretIndex, _contenido.Text.Length);

        var donde = _contenido.Text.IndexOf(texto, desde, StringComparison.OrdinalIgnoreCase);

        if (donde < 0)
        {
            donde = _contenido.Text.IndexOf(texto, StringComparison.OrdinalIgnoreCase);
        }

        if (donde < 0)
        {
            _estadoBusqueda.Text = "Sin coincidencias en lo cargado";
            return;
        }

        _contenido.Focus();
        _contenido.Select(donde, texto.Length);
        _contenido.ScrollToLine(_contenido.GetLineIndexFromCharacterIndex(donde));

        _estadoBusqueda.Text = _masAtras.IsEnabled
            ? "Se busca en lo cargado; cargá lo anterior para seguir"
            : string.Empty;
    }

    private void Cancelar()
    {
        _corte?.Cancel();
        _corte?.Dispose();
        _corte = null;
    }

    private static string Legible(long bytes) => bytes switch
    {
        >= 1024L * 1024 * 1024 => $"{bytes / (1024.0 * 1024 * 1024):0.#} GB",
        >= 1024 * 1024 => $"{bytes / (1024.0 * 1024):0.#} MB",
        >= 1024 => $"{bytes / 1024.0:0} KB",
        _ => $"{bytes} B",
    };
}
