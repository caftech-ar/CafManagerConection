using System.Runtime.Versioning;
using System.Windows;
using CafManagerConection.UseCases.Abstractions;

namespace CafManagerConection.App.Views;

/// <summary>Pide usuario y contraseña cuando la conexión no tiene credencial guardada (FR-039).</summary>
[SupportedOSPlatform("windows")]
public partial class CredentialPromptWindow : Window
{
    private CredentialPromptWindow(string conexion, string? usuario, bool pideDominio)
    {
        InitializeComponent();

        _encabezado.Text =
            $"«{conexion}» no tiene una credencial guardada. Escribí con qué usuario conectar.";

        _usuario.Text = usuario ?? string.Empty;
        _bloqueDominio.Visibility = pideDominio ? Visibility.Visible : Visibility.Collapsed;

        Loaded += (_, _) =>
        {
            if (_usuario.Text.Length > 0)
            {
                _clave.Focus();
            }
            else
            {
                _usuario.Focus();
            }
        };
    }

    private void AlAceptar(object sender, RoutedEventArgs e)
    {
        if (_usuario.Text.Trim().Length == 0)
        {
            _error.Text = "Hace falta un usuario.";
            _error.Visibility = Visibility.Visible;
            _usuario.Focus();
            return;
        }

        DialogResult = true;
    }

    /// <summary>Muestra el diálogo y devuelve lo tecleado, o null si se canceló.</summary>
    public static CredentialPromptResult? Pedir(
        Window owner, string conexion, string? usuario, bool pideDominio)
    {
        var ventana = new CredentialPromptWindow(conexion, usuario, pideDominio)
        {
            Owner = owner,
        };

        if (ventana.ShowDialog() != true)
        {
            return null;
        }

        var dominio = ventana._dominio.Text.Trim();

        return new CredentialPromptResult(
            ventana._usuario.Text.Trim(),
            dominio.Length > 0 ? dominio : null,
            ventana._clave.Password,
            ventana._recordar.IsChecked == true);
    }
}

/// <summary>Adaptador entre el puerto del núcleo y la ventana de WPF.</summary>
[SupportedOSPlatform("windows")]
public sealed class CredentialPromptWpf : ICredentialPrompt
{
    private readonly Func<Window?> _dueño;

    public CredentialPromptWpf(Func<Window?> dueño) => _dueño = dueño;

    public Task<CredentialPromptResult?> RequestAsync(
        string connectionName,
        string? suggestedUserName,
        bool needsDomain,
        CancellationToken ct = default)
    {
        var app = Application.Current;

        if (app is null)
        {
            return Task.FromResult<CredentialPromptResult?>(null);
        }

        var resultado = app.Dispatcher.Invoke(() =>
        {
            var owner = _dueño() ?? app.MainWindow;

            return owner is null
                ? null
                : CredentialPromptWindow.Pedir(
                    owner, connectionName, suggestedUserName, needsDomain);
        });

        return Task.FromResult(resultado);
    }
}
