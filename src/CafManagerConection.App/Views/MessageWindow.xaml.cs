using System.Runtime.Versioning;
using System.Windows;

namespace CafManagerConection.App.Views;

/// <summary>Aviso o confirmación con la estética de la aplicación.</summary>
[SupportedOSPlatform("windows")]
public partial class MessageWindow : Window
{
    private MessageWindow(string titulo, string mensaje, string aceptar, string? cancelar)
    {
        InitializeComponent();

        Title = titulo;
        _mensaje.Text = mensaje;
        _primario.Content = aceptar;

        if (cancelar is null)
        {
            _secundario.Visibility = Visibility.Collapsed;
            _primario.IsDefault = true;
            _primario.IsCancel = true;
        }
        else
        {
            _secundario.Content = cancelar;
            _secundario.IsDefault = true;
        }
    }

    private void AlAceptar(object sender, RoutedEventArgs e) => DialogResult = true;

    private void AlCancelar(object sender, RoutedEventArgs e) => DialogResult = false;

    public static bool Confirmar(
        Window owner, string titulo, string mensaje, string verbo, string cancelar = "Cancelar")
    {
        var ventana = new MessageWindow(titulo, mensaje, verbo, cancelar) { Owner = owner };

        return ventana.ShowDialog() == true;
    }

    public static void Avisar(Window owner, string titulo, string mensaje)
    {
        var ventana = new MessageWindow(titulo, mensaje, "Entendido", null) { Owner = owner };

        ventana.ShowDialog();
    }
}
