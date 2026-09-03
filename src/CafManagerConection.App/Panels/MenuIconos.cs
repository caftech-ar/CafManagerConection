using System.Runtime.Versioning;
using System.Windows.Controls;
using System.Windows.Media;

namespace CafManagerConection.App.Panels;

/// <summary>Arma una entrada de menú con icono, para no repetir a mano el mismo Path de 14x14 en cada menú contextual de la aplicación —el árbol, el de ajustes, y el de cada panel de inventario armaban su propio MenuItem suelto, y el icono es lo único que agregan todos por igual—.</summary>
[SupportedOSPlatform("windows")]
internal static class MenuIconos
{
    /// <summary>Iniciar (triángulo de reproducción), para el menú de Docker y de supervisord.</summary>
    public static readonly Geometry IconoIniciar = Geometry.Parse(
        "F1 M6.5 5.5C6.5 4.68 7.4 4.18 8.1 4.6L15.6 9.1C16.27 9.5 16.27 10.5 15.6 10.9L8.1 15.4"
        + "C7.4 15.82 6.5 15.32 6.5 14.5V5.5Z");

    /// <summary>Detener (cuadrado), para el menú de Docker y de supervisord. Quien lo usa lo pinta con el pincel «Destructivo»: es la misma clase de acción que Eliminar en el árbol —corta algo que está en marcha— y merece la misma distinción de color.</summary>
    public static readonly Geometry IconoDetener = Geometry.Parse(
        "F1 M7 6C6.45 6 6 6.45 6 7V13C6 13.55 6.45 14 7 14H13C13.55 14 14 13.55 14 13V7C14 6.45"
        + " 13.55 6 13 6H7Z");

    public static MenuItem Item(
        string texto,
        Action accion,
        bool destacado = false,
        Geometry? icono = null,
        Brush? color = null)
    {
        var item = new MenuItem { Header = texto };

        if (destacado)
        {
            item.FontWeight = System.Windows.FontWeights.SemiBold;
        }

        if (icono is not null)
        {
            item.Icon = new System.Windows.Shapes.Path
            {
                Data = icono,
                Fill = color,
                Width = 14,
                Height = 14,
                Stretch = Stretch.Uniform,
            };
        }

        item.Click += (_, _) => accion();
        return item;
    }
}
