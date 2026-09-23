using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CafManagerConection.App.Services;
using CafManagerConection.App.Themes;
using CafManagerConection.Domain.Settings;

namespace CafManagerConection.App.Views;

/// <summary>El catálogo entero dibujado, para revisar a ojo cómo queda cada icono.</summary>
[SupportedOSPlatform("windows")]
public partial class MuestraDeIconosWindow : Window
{
    private static readonly double[] Tamanos = [16, 20, 24, 32, 48, 64];

    private const string ColorDelTema = "El del tema";

    public MuestraDeIconosWindow()
    {
        InitializeComponent();

        Temas.AplicarBarraDeTitulo(this);

        foreach (var tamano in Tamanos)
        {
            _tamano.Items.Add(tamano.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        _color.Items.Add(ColorDelTema);

        foreach (var color in PaletaIconos.Colores)
        {
            _color.Items.Add(color.Nombre);
        }

        _tamano.SelectedIndex = 3;
        _color.SelectedIndex = 0;

        Dibujar();
    }

    /// <summary>El tamaño marcado en el selector, o 32 si todavía no hay ninguno.</summary>
    private double TamanoElegido =>
        Tamanos[_tamano.SelectedIndex < 0 ? 3 : _tamano.SelectedIndex];

    /// <summary>El pincel del color marcado, o null para usar el del tema.</summary>
    private Brush? PincelElegido =>
        _color.SelectedIndex <= 0
            ? null
            : (Brush?)TryFindResource(
                PaletaIconos.ClaveDeRecurso(PaletaIconos.Colores[_color.SelectedIndex - 1].Clave));

    /// <summary>Vuelve a dibujar cuando cambia el tamaño o el color.</summary>
    /// <param name="sender">El combo que cambió.</param>
    /// <param name="e">Datos del cambio, que acá no se usan.</param>
    private void AlCambiarLaMuestra(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded || _grupos.Items.Count > 0)
        {
            Dibujar();
        }
    }

    /// <summary>Rehace la grilla entera con el tamaño y el color elegidos.</summary>
    private void Dibujar()
    {
        _grupos.Items.Clear();

        var tamano = TamanoElegido;
        var pincel = PincelElegido;

        foreach (var grupo in CatalogoDeIconos.Grupos)
        {
            var iconos = CatalogoDeIconos.Iconos.Where(i => i.Grupo == grupo).ToList();

            if (iconos.Count == 0)
            {
                continue;
            }

            _grupos.Items.Add(Encabezado(grupo, iconos.Count));
            _grupos.Items.Add(Rejilla(iconos, tamano, pincel));
        }

        _resumen.Text =
            $"{CatalogoDeIconos.Iconos.Count} iconos · trazo {IconoVectorial.GrosorDelTrazo(tamano)} "
            + $"· tema {Temas.Actual}";
    }

    /// <summary>El título de un grupo, con cuántos iconos trae.</summary>
    /// <param name="grupo">Grupo a titular.</param>
    /// <param name="cuantos">Cuántos iconos tiene.</param>
    private static UIElement Encabezado(GrupoDeIconos grupo, int cuantos)
    {
        var texto = new TextBlock
        {
            Text = $"{grupo.Nombre}  ·  {cuantos}",
            Margin = new Thickness(0, 18, 0, 8),
            FontWeight = FontWeights.SemiBold,
        };

        texto.SetResourceReference(TextBlock.ForegroundProperty, "TextoTenue");

        return texto;
    }

    /// <summary>La grilla de un grupo.</summary>
    /// <param name="iconos">Iconos del grupo.</param>
    /// <param name="tamano">Con qué tamaño se dibujan.</param>
    /// <param name="pincel">Con qué se pintan, o null para el color del tema.</param>
    private static UIElement Rejilla(IReadOnlyList<IconoDelCatalogo> iconos, double tamano, Brush? pincel)
    {
        var panel = new WrapPanel();

        foreach (var icono in iconos)
        {
            panel.Children.Add(Muestra(icono, tamano, pincel));
        }

        return panel;
    }

    /// <summary>Una celda: el icono dibujado y su clave debajo.</summary>
    /// <param name="icono">Entrada del catálogo a mostrar.</param>
    /// <param name="tamano">Con qué tamaño se dibuja.</param>
    /// <param name="pincel">Con qué se pinta, o null para el color del tema.</param>
    private static UIElement Muestra(IconoDelCatalogo icono, double tamano, Brush? pincel)
    {
        var dibujo = new IconoVectorial
        {
            Clave = icono.Clave,
            Tamano = tamano,
            Pincel = pincel,
            HorizontalAlignment = HorizontalAlignment.Center,
        };

        var etiqueta = new TextBlock
        {
            Text = icono.Clave,
            FontSize = 10,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 6, 0, 0),
            Width = 92,
        };

        etiqueta.SetResourceReference(TextBlock.ForegroundProperty, "TextoTenue");

        var celda = new StackPanel
        {
            Width = 100,
            Margin = new Thickness(0, 0, 4, 12),
            ToolTip = $"{icono.Etiqueta} · {icono.Modo} · lienzo {icono.Lienzo}",
        };

        celda.Children.Add(new Border { Height = 68, Child = dibujo });
        celda.Children.Add(etiqueta);

        return celda;
    }
}
