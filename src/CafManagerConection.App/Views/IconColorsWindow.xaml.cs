using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CafManagerConection.App.Bootstrap;
using CafManagerConection.App.Services;
using CafManagerConection.Domain.Settings;

namespace CafManagerConection.App.Views;

/// <summary>Elige el color del icono de cada protocolo, de una paleta cerrada de diez.</summary>
[SupportedOSPlatform("windows")]
public partial class IconColorsWindow : Window
{
    private readonly CompositionRoot _root;
    private readonly ColoresDeIconos _original;

    private string _rdpElegido;
    private string _sshElegido;
    private string _webElegido;

    public IconColorsWindow(CompositionRoot root, ColoresDeIconos actuales)
    {
        _root = root;
        _original = actuales;

        _rdpElegido = actuales.Rdp;
        _sshElegido = actuales.Ssh;
        _webElegido = actuales.Web;

        InitializeComponent();

        Armar(_rdp, () => _rdpElegido, valor => _rdpElegido = valor);
        Armar(_ssh, () => _sshElegido, valor => _sshElegido = valor);
        Armar(_web, () => _webElegido, valor => _webElegido = valor);

        Closed += (_, _) =>
        {
            if (DialogResult == true)
            {
                return;
            }

            Temas.AplicarColoresDeIconos(_original);

            if (Owner is MainWindow principal)
            {
                principal.RepintarIconos();
            }
        };
    }

    /// <summary>Arma la fila de muestras de color de un protocolo.</summary>
    private void Armar(ItemsControl destino, Func<string> leer, Action<string> escribir)
    {
        var fila = new WrapPanel();

        foreach (var color in PaletaIconos.Colores)
        {
            var muestra = new Border
            {
                Width = 32,
                Height = 32,
                Margin = new Thickness(0, 0, 6, 0),
                CornerRadius = new CornerRadius(6),
                BorderThickness = new Thickness(2),
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = color.Nombre,
                Background = Pincel(color.Clave),
                BorderBrush = Brushes.Transparent,
                Tag = color.Clave,
            };

            muestra.MouseLeftButtonDown += (s, _) =>
            {
                escribir(color.Clave);
                Marcar(fila, color.Clave);
                Previsualizar();
            };

            fila.Children.Add(muestra);
        }

        Marcar(fila, leer());
        destino.Items.Add(fila);
    }

    private void Marcar(WrapPanel fila, string clave)
    {
        foreach (var hijo in fila.Children.OfType<Border>())
        {
            var elegido = (string?)hijo.Tag == clave;

            hijo.BorderBrush = elegido
                ? (Brush)FindResource("Texto")
                : Brushes.Transparent;
        }
    }

    private static Brush Pincel(string clave)
    {
        var recurso = "Icono" + char.ToUpperInvariant(clave[0]) + clave[1..];

        return Application.Current?.TryFindResource(recurso) as Brush ?? Brushes.Gray;
    }

    private void Previsualizar()
    {
        Temas.AplicarColoresDeIconos(new ColoresDeIconos(_rdpElegido, _sshElegido, _webElegido));

        if (Owner is MainWindow principal)
        {
            principal.RepintarIconos();
        }
    }

    private void AlRestablecer(object sender, RoutedEventArgs e)
    {
        var d = ColoresDeIconos.Default;

        _rdpElegido = d.Rdp;
        _sshElegido = d.Ssh;
        _webElegido = d.Web;

        _rdp.Items.Clear();
        _ssh.Items.Clear();
        _web.Items.Clear();

        Armar(_rdp, () => _rdpElegido, valor => _rdpElegido = valor);
        Armar(_ssh, () => _sshElegido, valor => _sshElegido = valor);
        Armar(_web, () => _webElegido, valor => _webElegido = valor);

        Previsualizar();
    }

    private async void AlGuardar(object sender, RoutedEventArgs e)
    {
        var elegidos = new ColoresDeIconos(_rdpElegido, _sshElegido, _webElegido);

        await _root.AppSettings.SetIconColorsAsync(elegidos).ConfigureAwait(true);

        Temas.AplicarColoresDeIconos(elegidos);
        DialogResult = true;
    }
}
