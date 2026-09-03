using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using CafManagerConection.Platform;

namespace CafManagerConection.App.Views;

/// <summary>Visor de sólo lectura para texto estático de un servidor, como la configuración efectiva de nginx (FR-101a). Un registro no se muestra acá: se sigue en vivo en <see cref="VisorDeRegistroWindow"/> (FR-185e).</summary>
[SupportedOSPlatform("windows")]
public partial class TextViewerWindow : Window
{
    /// <summary>Texto original, tal como lo entregó el servidor. Es lo que se copia.</summary>
    private readonly string _original;

    private TextViewerWindow(string titulo, string contenido)
    {
        InitializeComponent();

        Title = titulo;
        _original = contenido ?? string.Empty;

        MostrarComoCodigo();
    }

    private void MostrarComoCodigo()
    {
        var parrafo = new Paragraph { Margin = new Thickness(0) };
        var tramos = ResaltadorDeNginx.Analizar(_original);

        if (tramos.Count == 0)
        {
            parrafo.Inlines.Add(new Run(_original));
        }
        else
        {
            foreach (var tramo in tramos)
            {
                var texto = _original.Substring(tramo.Desde, tramo.Largo);
                var run = new Run(texto);

                if (PincelDe(tramo.Tipo) is { } pincel)
                {
                    run.Foreground = pincel;
                }

                if (tramo.Tipo == TipoDeTramo.Comentario)
                {
                    run.FontStyle = FontStyles.Italic;
                }

                parrafo.Inlines.Add(run);
            }
        }

        _codigo.Document = new FlowDocument(parrafo)
        {
            PageWidth = 4000,
            FontFamily = _codigo.FontFamily,
            FontSize = 13,
        };
    }

    private Brush? PincelDe(TipoDeTramo tipo) => tipo switch
    {
        TipoDeTramo.Comentario => (Brush)FindResource("TextoTenue"),
        TipoDeTramo.Bloque => (Brush)FindResource("IconoVioleta"),
        TipoDeTramo.Directiva => (Brush)FindResource("IconoAzul"),
        TipoDeTramo.Cadena => (Brush)FindResource("IconoLima"),
        TipoDeTramo.Numero => (Brush)FindResource("IconoAmbar"),
        TipoDeTramo.Variable => (Brush)FindResource("IconoCyan"),
        _ => null,
    };

    private void AlBuscar(object sender, RoutedEventArgs e)
    {
        var texto = _buscar.Text;

        if (texto.Length == 0)
        {
            _codigo.Selection.Select(_codigo.Document.ContentStart, _codigo.Document.ContentStart);
            _resultado.Text = string.Empty;
            return;
        }

        var i = _original.IndexOf(texto, StringComparison.OrdinalIgnoreCase);

        if (i < 0)
        {
            _resultado.Text = "sin coincidencias";
            return;
        }

        var inicio = _codigo.Document.ContentStart.GetPositionAtOffset(i + 1, LogicalDirection.Forward);
        var fin = inicio?.GetPositionAtOffset(texto.Length, LogicalDirection.Forward);

        if (inicio is null || fin is null)
        {
            _resultado.Text = string.Empty;
            return;
        }

        _codigo.Selection.Select(inicio, fin);
        _codigo.Focus();
        _resultado.Text = "1 coincidencia";
    }

    private void AlCopiar(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(_original);
        }
        catch (COMException)
        {
        }
    }

    /// <summary>Muestra un archivo de configuración con resaltado de sintaxis, en modal y sin seguir nada (FR-101a, FR-185e).</summary>
    public static void Mostrar(Window owner, string titulo, string contenido) =>
        new TextViewerWindow(titulo, contenido) { Owner = owner }.ShowDialog();
}
