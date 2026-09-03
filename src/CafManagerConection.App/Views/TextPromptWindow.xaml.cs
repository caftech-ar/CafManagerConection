using System.Runtime.Versioning;
using System.Windows;

namespace CafManagerConection.App.Views;

/// <summary>Pide un texto: un nombre de carpeta, un renombre.</summary>
[SupportedOSPlatform("windows")]
public partial class TextPromptWindow : Window
{
    private TextPromptWindow(string titulo, string etiqueta, string valorInicial)
    {
        InitializeComponent();

        Title = titulo;
        _etiqueta.Text = etiqueta;
        _valor.Text = valorInicial;

        Loaded += (_, _) =>
        {
            _valor.Focus();
            _valor.SelectAll();
        };
    }

    private void AlAceptar(object sender, RoutedEventArgs e)
    {
        if (_valor.Text.Trim().Length == 0)
        {
            return;
        }

        DialogResult = true;
    }

    /// <summary>Devuelve el texto, o null si se canceló o quedó vacío.</summary>
    public static string? Pedir(
        Window owner, string titulo, string etiqueta, string valorInicial = "")
    {
        var ventana = new TextPromptWindow(titulo, etiqueta, valorInicial) { Owner = owner };

        return ventana.ShowDialog() == true && ventana._valor.Text.Trim().Length > 0
            ? ventana._valor.Text.Trim()
            : null;
    }
}
