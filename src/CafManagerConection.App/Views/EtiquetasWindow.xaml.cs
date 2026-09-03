using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CafManagerConection.App.Bootstrap;
using CafManagerConection.App.Themes;
using CafManagerConection.Domain.Connections;
using CafManagerConection.Domain.Settings;

namespace CafManagerConection.App.Views;

/// <summary>Administra el catálogo de etiquetas: crear, renombrar, recolorear, borrar y ordenar (FR-130).</summary>
[SupportedOSPlatform("windows")]
public partial class EtiquetasPanel : UserControl
{
    /// <summary>Una etiqueta en la lista, con el pincel ya resuelto.</summary>
    public sealed record Fila(Guid Id, string Codigo, string Nombre, string ClaveDeColor, Brush Pincel);

    private CompositionRoot? _root;

    private CatalogoDeEtiquetas _catalogo = new();

    /// <summary>La que se está editando, o null si lo que hay en pantalla es una nueva.</summary>
    private Guid? _editando;

    private string _colorElegido = PaletaIconos.Colores[0].Clave;
    private bool _cargando;

    public EtiquetasPanel()
    {
        InitializeComponent();
        ArmarPaleta();
    }

    /// <summary>Le da al panel lo que necesita para trabajar y dispara la primera carga.</summary>
    public void Inicializar(CompositionRoot root)
    {
        _root = root;
        _ = CargarAsync();
    }

    private async Task CargarAsync(Guid? aSeleccionar = null)
    {
        if (_root is not { } root)
        {
            return;
        }

        _catalogo = new CatalogoDeEtiquetas(await root.Tags.GetAllAsync().ConfigureAwait(true));

        _cargando = true;

        _lista.ItemsSource = _catalogo.Todas
            .Select(e => new Fila(e.Id, e.Codigo, e.Nombre, e.ClaveDeColor, Pinceles.DeColor(e.ClaveDeColor)))
            .ToList();

        _cargando = false;

        if (aSeleccionar is { } id)
        {
            _lista.SelectedItem = _lista.Items.OfType<Fila>().FirstOrDefault(f => f.Id == id);
        }
        else if (_lista.Items.Count > 0)
        {
            _lista.SelectedIndex = 0;
        }
        else
        {
            Nueva();
        }
    }

    private void AlElegir(object sender, SelectionChangedEventArgs e)
    {
        if (_cargando || _lista.SelectedItem is not Fila fila)
        {
            return;
        }

        _editando = fila.Id;
        _colorElegido = fila.ClaveDeColor;

        _cargando = true;
        _codigo.Text = fila.Codigo;
        _nombre.Text = fila.Nombre;
        _cargando = false;

        Marcar();
        Revisar();
    }

    private void AlEditar(object sender, TextChangedEventArgs e)
    {
        if (!_cargando)
        {
            Revisar();
        }
    }

    /// <summary>Vuelve a validar lo que hay en pantalla y ajusta los botones y la muestra.</summary>
    private void Revisar()
    {
        var motivo = _catalogo.PorQueNo(_codigo.Text, _nombre.Text, _colorElegido, _editando);

        var enBlanco = _editando is null
                       && _codigo.Text.Length == 0
                       && _nombre.Text.Length == 0;

        _error.Text = motivo ?? string.Empty;
        _error.Visibility = motivo is null || enBlanco
            ? Visibility.Collapsed
            : Visibility.Visible;

        _guardar.IsEnabled = motivo is null;
        _guardar.Content = _editando is null ? "Crear" : "Guardar cambios";
        _borrar.IsEnabled = _editando is not null;

        var posicion = Posicion();
        _subir.IsEnabled = posicion > 0;
        _bajar.IsEnabled = posicion >= 0 && posicion < _catalogo.Todas.Count - 1;

        _muestraTexto.Text = _codigo.Text.Length > 0 ? _codigo.Text : "—";
        _muestra.Background = Pinceles.DeColor(_colorElegido);
    }

    private int Posicion() =>
        _editando is { } id
            ? _catalogo.Todas.ToList().FindIndex(e => e.Id == id)
            : -1;

    private void AlNueva(object sender, RoutedEventArgs e) => Nueva();

    private void Nueva()
    {
        _editando = null;
        _colorElegido = PaletaIconos.Colores[0].Clave;

        _cargando = true;
        _lista.SelectedItem = null;
        _codigo.Text = string.Empty;
        _nombre.Text = string.Empty;
        _cargando = false;

        Marcar();
        Revisar();
        _codigo.Focus();
    }

    private async void AlGuardar(object sender, RoutedEventArgs e)
    {
        if (_root is not { } root)
        {
            return;
        }

        if (_editando is { } id)
        {
            if (_catalogo.Por(id) is not { } etiqueta
                || !_catalogo.Actualizar(id, _codigo.Text, _nombre.Text, _colorElegido))
            {
                return;
            }

            await root.Tags.UpdateAsync(etiqueta).ConfigureAwait(true);
            await CargarAsync(id).ConfigureAwait(true);
            return;
        }

        if (_catalogo.Agregar(_codigo.Text, _nombre.Text, _colorElegido) is not { } nueva)
        {
            return;
        }

        await root.Tags.AddAsync(nueva).ConfigureAwait(true);
        await CargarAsync(nueva.Id).ConfigureAwait(true);
    }

    /// <summary>Borra una etiqueta, avisando a cuántos elementos deja sin marca.</summary>
    private async void AlBorrar(object sender, RoutedEventArgs e)
    {
        if (_root is not { } root || _editando is not { } id || _catalogo.Por(id) is not { } etiqueta)
        {
            return;
        }

        var usos = await root.Tags.CountUsagesAsync(id).ConfigureAwait(true);

        var mensaje = usos == 0
            ? $"Se va a borrar la etiqueta «{etiqueta.Nombre}». No la usa nadie."
            : $"Se va a borrar la etiqueta «{etiqueta.Nombre}».\n\n"
              + (usos == 1
                  ? "1 elemento la usa y va a quedar sin etiqueta."
                  : $"{usos} elementos la usan y van a quedar sin etiqueta.")
              + " No se borra ninguna conexión ni carpeta.";

        if (Window.GetWindow(this) is not { } ventana
            || !MessageWindow.Confirmar(ventana, "Borrar la etiqueta", mensaje, "Borrar"))
        {
            return;
        }

        await root.Tags.DeleteAsync(id).ConfigureAwait(true);
        await CargarAsync().ConfigureAwait(true);
    }

    private async void AlRestablecer(object sender, RoutedEventArgs e)
    {
        if (_root is not { } root || Window.GetWindow(this) is not { } ventana)
        {
            return;
        }

        var actuales = await root.Tags.GetAllAsync().ConfigureAwait(true);
        var usos = new Dictionary<Guid, int>();

        foreach (var etiqueta in actuales)
        {
            usos[etiqueta.Id] = await root.Tags
                .CountUsagesAsync(etiqueta.Id).ConfigureAwait(true);
        }

        var cambios = RestablecerEtiquetas.Comparar(
            actuales, id => usos.GetValueOrDefault(id));

        if (!cambios.HayAlgoQueHacer)
        {
            MessageWindow.Avisar(
                ventana,
                "Restablecer etiquetas",
                "El catálogo ya es el de fábrica: no hay nada que cambiar.");

            return;
        }

        if (!MessageWindow.Confirmar(
                ventana, "Restablecer etiquetas", Resumen(cambios), "Restablecer"))
        {
            return;
        }

        await AplicarAsync(root, cambios).ConfigureAwait(true);
        await CargarAsync().ConfigureAwait(true);
    }

    private static string Resumen(CambiosAlRestablecer cambios)
    {
        var lineas = new List<string>();

        if (cambios.Faltantes.Count > 0)
        {
            lineas.Add($"Se reponen: {Nombres(cambios.Faltantes)}.");
        }

        if (cambios.Modificadas.Count > 0)
        {
            lineas.Add(
                $"Vuelven al código, nombre, color y orden de fábrica: "
                + $"{Nombres(cambios.Modificadas)}.");
        }

        if (cambios.Agregadas.Count > 0)
        {
            lineas.Add($"Se borran las que agregaste: {Nombres(cambios.Agregadas)}.");

            lineas.Add(cambios.ConexionesQuePierdenEtiqueta switch
            {
                0 => "No las usa nadie, así que ninguna conexión pierde su etiqueta.",
                1 => "1 elemento las usa y va a quedar sin etiqueta.",
                var n => $"{n} elementos las usan y van a quedar sin etiqueta.",
            });
        }

        lineas.Add("No se borra ninguna conexión ni carpeta.");

        return string.Join("\n\n", lineas);
    }

    private static string Nombres(IReadOnlyList<Etiqueta> etiquetas) =>
        string.Join(", ", etiquetas.Select(e => $"«{e.Nombre}»"));

    private static async Task AplicarAsync(CompositionRoot root, CambiosAlRestablecer cambios)
    {
        foreach (var propia in cambios.Agregadas)
        {
            await root.Tags.DeleteAsync(propia.Id).ConfigureAwait(true);
        }

        foreach (var faltante in cambios.Faltantes)
        {
            await root.Tags.AddAsync(faltante).ConfigureAwait(true);
        }

        foreach (var modificada in cambios.Modificadas)
        {
            await root.Tags.UpdateAsync(modificada).ConfigureAwait(true);
        }
    }

    private async void AlSubir(object sender, RoutedEventArgs e) => await MoverAsync(-1);

    private async void AlBajar(object sender, RoutedEventArgs e) => await MoverAsync(1);

    /// <summary>Intercambia el orden con la vecina y guarda las dos.</summary>
    private async Task MoverAsync(int paso)
    {
        if (_root is not { } root)
        {
            return;
        }

        var desde = Posicion();
        var hasta = desde + paso;

        if (desde < 0 || hasta < 0 || hasta >= _catalogo.Todas.Count)
        {
            return;
        }

        var una = _catalogo.Todas[desde];
        var otra = _catalogo.Todas[hasta];

        (una.Orden, otra.Orden) = (otra.Orden, una.Orden);

        await root.Tags.UpdateAsync(una).ConfigureAwait(true);
        await root.Tags.UpdateAsync(otra).ConfigureAwait(true);
        await CargarAsync(una.Id).ConfigureAwait(true);
    }

    /// <summary>Las mismas muestras que las conexiones y las carpetas.</summary>
    private void ArmarPaleta()
    {
        foreach (var color in PaletaIconos.Colores)
        {
            var muestra = new Border
            {
                Width = 26,
                Height = 26,
                Margin = new Thickness(0, 0, 6, 6),
                CornerRadius = new CornerRadius(6),
                BorderThickness = new Thickness(2),
                BorderBrush = Brushes.Transparent,
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = color.Nombre,
                Tag = color.Clave,
                Background = Pinceles.DeColor(color.Clave),
            };

            muestra.MouseLeftButtonDown += (_, _) =>
            {
                _colorElegido = color.Clave;
                Marcar();
                Revisar();
            };

            _colores.Children.Add(muestra);
        }
    }

    private void Marcar()
    {
        foreach (var muestra in _colores.Children.OfType<Border>())
        {
            muestra.BorderBrush = (string?)muestra.Tag == _colorElegido
                ? (Brush)FindResource("Texto")
                : Brushes.Transparent;
        }
    }
}
