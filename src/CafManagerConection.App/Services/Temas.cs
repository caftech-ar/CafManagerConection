using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Interop;
using CafManagerConection.Domain.Settings;
using Microsoft.Win32;

namespace CafManagerConection.App.Services;

/// <summary>Cambia la paleta de la aplicación en caliente.</summary>
[SupportedOSPlatform("windows")]
public static class Temas
{
    private const string PaletaClara = "Themes/Paleta.Claro.xaml";
    private const string PaletaOscura = "Themes/Paleta.Oscuro.xaml";

    public static AppTheme Actual { get; private set; } = AppTheme.Light;

    public static bool EsOscuro => Resolver(Actual);

    /// <summary>Resuelve una preferencia a claro u oscuro concreto.</summary>
    public static bool Resolver(AppTheme tema) => tema switch
    {
        AppTheme.Dark => true,
        AppTheme.Light => false,
        _ => WindowsPrefiereOscuro(),
    };

    public static void Aplicar(AppTheme tema)
    {
        Actual = tema;

        var origen = new Uri(Resolver(tema) ? PaletaOscura : PaletaClara, UriKind.Relative);
        var nueva = new ResourceDictionary { Source = origen };

        var diccionarios = Application.Current.Resources.MergedDictionaries;

        // La paleta está en el índice 0 por convención de App.xaml; se reemplaza en su lugar.
        if (diccionarios.Count > 0)
        {
            diccionarios[0] = nueva;
        }
        else
        {
            diccionarios.Add(nueva);
        }

        AplicarColoresDeIconos(_coloresIconos);

        foreach (var ventana in Application.Current.Windows.OfType<Window>())
        {
            // El tinte de Mica sigue al modo oscuro de la ventana, asi que al cambiar de tema hay
            // que volver a pedir el backdrop y no solo la barra de titulo.
            if (ReferenceEquals(ventana.Background, System.Windows.Media.Brushes.Transparent))
            {
                AplicarFondoMica(ventana);
            }
            else
            {
                AplicarBarraDeTitulo(ventana);
            }
        }
    }

    /// <summary>Barra de título y esquinas de la ventana, por DWM. Los atributos que la versión de Windows no conoce devuelven error y se ignoran.</summary>
    public static void AplicarBarraDeTitulo(Window ventana)
    {
        if (PresentationSource.FromVisual(ventana) is not HwndSource fuente)
        {
            return;
        }

        var oscuro = EsOscuro ? 1 : 0;

        if (DwmSetWindowAttribute(fuente.Handle, ModoOscuro, ref oscuro, sizeof(int)) != 0)
        {
            DwmSetWindowAttribute(fuente.Handle, ModoOscuroViejo, ref oscuro, sizeof(int));
        }

        var redondeadas = EsquinasRedondeadas;
        DwmSetWindowAttribute(fuente.Handle, PreferenciaDeEsquina, ref redondeadas, sizeof(int));
    }

    /// <summary>Pide el fondo Mica y, sólo si DWM lo concede, abre el área cliente para que se vea. Devuelve si quedó puesto.</summary>
    /// <remarks>Son cuatro pasos y ninguno alcanza solo. El backdrop sin extender el marco no se dibuja en el área cliente, y extenderlo sin vaciar el <c>BackgroundColor</c> del <c>CompositionTarget</c> deja la lateral negra: WPF pinta ahí su propio fondo opaco antes que DWM. El tinte sigue al modo oscuro de la ventana, así que <see cref="AplicarBarraDeTitulo"/> corre primero.</remarks>
    public static bool AplicarFondoMica(Window ventana)
    {
        if (PresentationSource.FromVisual(ventana) is not HwndSource fuente)
        {
            return false;
        }

        AplicarBarraDeTitulo(ventana);

        var mica = FondoMica;

        if (DwmSetWindowAttribute(fuente.Handle, TipoDeFondo, ref mica, sizeof(int)) != 0)
        {
            return false;
        }

        var todoElMarco = new Margenes { Izquierda = -1, Derecha = -1, Arriba = -1, Abajo = -1 };

        if (DwmExtendFrameIntoClientArea(fuente.Handle, ref todoElMarco) != 0)
        {
            return false;
        }

        if (fuente.CompositionTarget is { } destino)
        {
            destino.BackgroundColor = System.Windows.Media.Colors.Transparent;
        }

        ventana.Background = System.Windows.Media.Brushes.Transparent;
        return true;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Margenes
    {
        public int Izquierda;
        public int Derecha;
        public int Arriba;
        public int Abajo;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr ventana, ref Margenes margenes);

    /// <summary>DWMWA_USE_IMMERSIVE_DARK_MODE, desde Windows 10 20H1.</summary>
    private const int ModoOscuro = 20;

    /// <summary>El mismo atributo en las versiones anteriores.</summary>
    private const int ModoOscuroViejo = 19;

    /// <summary>DWMWA_WINDOW_CORNER_PREFERENCE, desde Windows 11. Sin él, una ventana con <c>WindowStyle="None"</c> no se redondea sola.</summary>
    private const int PreferenciaDeEsquina = 33;

    /// <summary>DWMWCP_ROUND.</summary>
    private const int EsquinasRedondeadas = 2;

    /// <summary>DWMWA_SYSTEMBACKDROP_TYPE, desde Windows 11 22H2.</summary>
    private const int TipoDeFondo = 38;

    /// <summary>DWMSBT_MAINWINDOW. Sólo se ve donde el área cliente es transparente: una ventana con <c>Background</c> opaco lo tapa entero.</summary>
    private const int FondoMica = 2;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr ventana, int atributo, ref int valor, int tamano);

    public static void AplicarColoresDeIconos(ColoresDeIconos colores)
    {
        _coloresIconos = colores;

        Reasignar("ProtocoloRdp", colores.Rdp, PaletaIconos.PorOmisionRdp);
        Reasignar("ProtocoloSsh", colores.Ssh, PaletaIconos.PorOmisionSsh);
        Reasignar("ProtocoloWeb", colores.Web, PaletaIconos.PorOmisionWeb);
    }

    private static ColoresDeIconos _coloresIconos = ColoresDeIconos.Default;

    private static void Reasignar(string destino, string? clave, string porOmision)
    {
        var color = PaletaIconos.Resolver(clave, porOmision);
        var recurso = "Icono" + char.ToUpperInvariant(color.Clave[0]) + color.Clave[1..];

        if (Application.Current?.TryFindResource(recurso) is System.Windows.Media.Brush pincel)
        {
            Application.Current.Resources[destino] = pincel;
        }
    }

    /// <summary>Rota entre claro, oscuro y acompañar a Windows.</summary>
    public static AppTheme Siguiente() => Actual switch
    {
        AppTheme.Light => AppTheme.Dark,
        AppTheme.Dark => AppTheme.System,
        _ => AppTheme.Light,
    };

    public static string Nombre(AppTheme tema) => tema switch
    {
        AppTheme.Light => "Tema claro",
        AppTheme.Dark => "Tema oscuro",
        _ => "El tema acompaña a Windows",
    };

    public static string Glifo(AppTheme tema) => tema switch
    {
        AppTheme.Light => "☀",
        AppTheme.Dark => "☾",
        _ => "◐",
    };

    private static bool WindowsPrefiereOscuro()
    {
        try
        {
            using var clave = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");

            return clave?.GetValue("AppsUseLightTheme") is int claro && claro == 0;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
