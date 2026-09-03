using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using CafManagerConection.Ssh;

namespace CafManagerConection.App.Views;

/// <summary>Pide la contraseña de <c>sudo</c> cuando la de la conexión no sirve, y dice para qué se pide y que no se guarda (FR-184e).</summary>
[SupportedOSPlatform("windows")]
public partial class PedidoDeContrasenaDeSudoWindow : Window
{
    private PedidoDeContrasenaDeSudoWindow(string servidor, string usuario)
    {
        InitializeComponent();

        _encabezado.Text =
            $"«{servidor}» pide la contraseña de sudo para leer con privilegios, y la contraseña "
            + $"de la conexión no le sirvió. Escribí la contraseña de sudo de {usuario} en ese "
            + "servidor.";

        Loaded += (_, _) => _clave.Focus();
    }

    private void AlAceptar(object sender, RoutedEventArgs e)
    {
        if (_clave.SecurePassword.Length == 0)
        {
            _error.Text = "Hace falta una contraseña. Si no la tenés, cancelá.";
            _error.Visibility = Visibility.Visible;
            _clave.Focus();
            return;
        }

        DialogResult = true;
    }

    /// <summary>Muestra el diálogo y escribe lo tecleado en <paramref name="destino"/>; devuelve false si se canceló.</summary>
    public static bool Pedir(
        Window owner, string servidor, string usuario, ContrasenaDeSudoDeSesion destino)
    {
        var ventana = new PedidoDeContrasenaDeSudoWindow(servidor, usuario)
        {
            Owner = owner,
        };

        try
        {
            return ventana.ShowDialog() == true && Tomar(ventana._clave, destino);
        }
        finally
        {
            ventana._clave.Clear();
        }
    }

    // El PasswordBox entrega un SecureString y no un string: una cadena queda en el montón sin poder pisarla con ceros.
    private static bool Tomar(PasswordBox campo, ContrasenaDeSudoDeSesion destino)
    {
        using var segura = campo.SecurePassword;

        if (segura.Length == 0)
        {
            return false;
        }

        var puntero = Marshal.SecureStringToGlobalAllocUnicode(segura);
        var letras = new char[segura.Length];

        try
        {
            Marshal.Copy(puntero, letras, 0, letras.Length);
            destino.Guardar(letras);

            return destino.Tiene;
        }
        finally
        {
            Array.Clear(letras);
            Marshal.ZeroFreeGlobalAllocUnicode(puntero);
        }
    }
}

/// <summary>Adaptador entre el puerto de la capa SSH y la ventana de WPF.</summary>
[SupportedOSPlatform("windows")]
public sealed class PedidoDeContrasenaDeSudoWpf(Func<Window?> dueño) : IPedidoDeContrasenaDeSudo
{
    public Task<bool> PedirAsync(
        string servidor,
        string usuario,
        ContrasenaDeSudoDeSesion destino,
        CancellationToken ct = default)
    {
        var app = Application.Current;

        if (app is null || ct.IsCancellationRequested)
        {
            return Task.FromResult(false);
        }

        var acepto = app.Dispatcher.Invoke(() =>
        {
            var owner = dueño() ?? app.MainWindow;

            return owner is not null
                   && PedidoDeContrasenaDeSudoWindow.Pedir(owner, servidor, usuario, destino);
        });

        return Task.FromResult(acepto);
    }
}
