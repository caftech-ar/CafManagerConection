using System.Runtime.Versioning;
using System.Windows.Controls;
using System.Windows.Media;
using CafManagerConection.App.Themes;

namespace CafManagerConection.App.Panels;

/// <summary>Entradas de menú con icono, con el tamaño y el alto armados en un solo lugar.</summary>
[SupportedOSPlatform("windows")]
internal static class MenuIconos
{
    private const double TamanoDelIcono = 14;

    /// <summary>Una entrada de menú que corre una acción, con su icono del catálogo.</summary>
    /// <param name="texto">Lo que se lee en el menú.</param>
    /// <param name="accion">Qué corre al elegirla.</param>
    /// <param name="destacado">Si va en negrita, para la acción principal del menú.</param>
    /// <param name="icono">Clave del catálogo, o null para no dibujar ninguno.</param>
    /// <param name="color">Con qué se pinta el icono.</param>
    public static MenuItem Item(
        string texto,
        Action accion,
        bool destacado = false,
        string? icono = null,
        Brush? color = null)
    {
        var item = new MenuItem { Header = texto };

        if (destacado)
        {
            item.FontWeight = System.Windows.FontWeights.SemiBold;
        }

        if (icono is not null)
        {
            item.Icon = new IconoVectorial
            {
                Clave = icono,
                Tamano = TamanoDelIcono,
                Pincel = color,
            };
        }

        item.Click += (_, _) => accion();
        return item;
    }
}
