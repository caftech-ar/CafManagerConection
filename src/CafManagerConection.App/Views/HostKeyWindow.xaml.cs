using System.Runtime.Versioning;
using System.Windows;

namespace CafManagerConection.App.Views;

/// <summary>Muestra el fingerprint del host y pide una decisión antes de autenticar (FR-022, FR-023).</summary>
[SupportedOSPlatform("windows")]
public partial class HostKeyWindow : Window
{
    public HostKeyWindow(string host, string fingerprint, string? conocido)
    {
        InitializeComponent();

        var cambio = !string.IsNullOrEmpty(conocido)
                     && !string.Equals(conocido, fingerprint, StringComparison.Ordinal);

        Title = cambio ? "La identidad del servidor cambió" : "Servidor desconocido";
        _titulo.Text = Title;
        _presentado.Text = fingerprint;

        if (cambio)
        {
            _titulo.Foreground = (System.Windows.Media.Brush)FindResource("Destructivo");

            _explicacion.Text =
                $"El servidor {host} presenta una clave distinta de la que se aceptó antes."
                + Environment.NewLine + Environment.NewLine
                + "Puede deberse a una reinstalación del servidor, o a que otro equipo esté "
                + "haciéndose pasar por él. No se envió ninguna credencial.";

            _bloqueConocido.Visibility = Visibility.Visible;
            _conocido.Text = conocido!;

            _aceptar.Content = "Aceptar igual";
            _aceptar.Style = (Style)FindResource("BotonSecundario");
            _recordar.IsChecked = false;
        }
        else
        {
            _explicacion.Text =
                $"Es la primera vez que se conecta a {host}."
                + Environment.NewLine + Environment.NewLine
                + "Verificá que el fingerprint coincida con el del servidor antes de aceptar.";

            _aceptar.IsDefault = true;
            _recordar.IsChecked = true;
        }
    }

    public bool Recordar => _recordar.IsChecked == true;

    private void AlAceptar(object sender, RoutedEventArgs e) => DialogResult = true;
}
