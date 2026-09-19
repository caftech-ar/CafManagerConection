using System.Runtime.Versioning;
using System.Windows;

namespace CafManagerConection.App.Views;

/// <summary>Aviso o confirmación con la estética de la aplicación.</summary>
[SupportedOSPlatform("windows")]
public partial class MessageWindow : Window
{
    private MessageWindow(
        string titulo, string mensaje, string aceptar, string? cancelar, bool destructivo)
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
            return;
        }

        _secundario.Content = cancelar;

        // Enter ejecuta lo que la ventana propone, salvo cuando eso destruye algo: ahí propone no
        // hacerlo, y confirmar pide ir al botón.
        var predeterminado = destructivo ? _secundario : _primario;

        predeterminado.IsDefault = true;
        Loaded += (_, _) => predeterminado.Focus();
    }

    private void AlAceptar(object sender, RoutedEventArgs e) => DialogResult = true;

    private void AlCancelar(object sender, RoutedEventArgs e) => DialogResult = false;

    /// <summary>Pregunta y devuelve si se confirmó.</summary>
    /// <param name="owner">Ventana sobre la que se abre.</param>
    /// <param name="titulo">Título de la ventana.</param>
    /// <param name="mensaje">Lo que se pregunta.</param>
    /// <param name="verbo">Texto del botón que confirma.</param>
    /// <param name="cancelar">Texto del botón que cancela.</param>
    /// <param name="destructivo">Si confirmar borra o descarta algo; entonces Enter cancela.</param>
    public static bool Confirmar(
        Window owner,
        string titulo,
        string mensaje,
        string verbo,
        string cancelar = "Cancelar",
        bool destructivo = false)
    {
        var ventana = new MessageWindow(titulo, mensaje, verbo, cancelar, destructivo)
        {
            Owner = owner,
        };

        return ventana.ShowDialog() == true;
    }

    public static void Avisar(Window owner, string titulo, string mensaje)
    {
        var ventana = new MessageWindow(titulo, mensaje, "Entendido", null, destructivo: false)
        {
            Owner = owner,
        };

        ventana.ShowDialog();
    }
}
