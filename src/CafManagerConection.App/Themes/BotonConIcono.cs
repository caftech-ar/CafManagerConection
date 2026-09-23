using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;

namespace CafManagerConection.App.Themes;

/// <summary>Le pone icono a un botón sin tocar su contenido en el XAML.</summary>
/// <remarks>
/// Un botón con texto declara <c>Content</c> como atributo; meterle un icono obligaría a abrirlo en
/// sintaxis de elemento y a repetir el mismo andamiaje en cada uno. Con esto alcanza un atributo, y
/// el icono toma el color del botón: en uno destructivo sale rojo sin decírselo.
/// </remarks>
[SupportedOSPlatform("windows")]
public static class BotonConIcono
{
    private const double TamanoDelIcono = 13;

    public static readonly DependencyProperty ClaveProperty = DependencyProperty.RegisterAttached(
        "Clave",
        typeof(string),
        typeof(BotonConIcono),
        new PropertyMetadata(null, AlCambiarLaClave));

    /// <summary>Lee la clave del catálogo con la que se dibuja el icono del botón.</summary>
    /// <param name="boton">El botón.</param>
    public static string? GetClave(ButtonBase boton) => (string?)boton.GetValue(ClaveProperty);

    /// <summary>Fija la clave del catálogo con la que se dibuja el icono del botón.</summary>
    /// <param name="boton">El botón.</param>
    /// <param name="clave">Clave del catálogo, o null para dejarlo sin icono.</param>
    public static void SetClave(ButtonBase boton, string? clave) =>
        boton.SetValue(ClaveProperty, clave);

    private static void AlCambiarLaClave(DependencyObject destino, DependencyPropertyChangedEventArgs e)
    {
        if (destino is not ButtonBase boton || e.NewValue is not string clave)
        {
            return;
        }

        boton.Content = ConIcono(boton, clave, boton.Content as string ?? string.Empty);
    }

    private static UIElement ConIcono(ButtonBase boton, string clave, string texto)
    {
        var icono = new IconoVectorial
        {
            Clave = clave,
            Tamano = TamanoDelIcono,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, texto.Length == 0 ? 0 : 6, 0),
        };

        icono.SetBinding(
            IconoVectorial.PincelProperty,
            new Binding(nameof(Control.Foreground)) { Source = boton });

        var fila = new StackPanel { Orientation = Orientation.Horizontal };
        fila.Children.Add(icono);

        if (texto.Length > 0)
        {
            fila.Children.Add(new TextBlock { Text = texto, VerticalAlignment = VerticalAlignment.Center });
        }

        return fila;
    }
}
