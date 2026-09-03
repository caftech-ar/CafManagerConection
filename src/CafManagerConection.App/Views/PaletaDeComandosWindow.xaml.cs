using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CafManagerConection.App.Bootstrap;
using CafManagerConection.Domain.Settings;

namespace CafManagerConection.App.Views;

/// <summary>Paleta de comandos guardados: elegir, editar y enviar (FR-147).</summary>
[SupportedOSPlatform("windows")]
public partial class PaletaDeComandosWindow : Window
{
    private readonly CompositionRoot _root;
    private readonly Guid? _conexion;
    private readonly Action<string, bool>? _mandarALaSesion;

    private PaletaDeComandos _paleta = new();
    private ComandoGuardado? _elegido;
    private bool _cargando;

    public PaletaDeComandosWindow(
        CompositionRoot root,
        string? nombreDelServidor,
        Guid? conexion,
        Action<string, bool>? enviar)
    {
        _root = root;
        _conexion = conexion;
        _mandarALaSesion = enviar;

        InitializeComponent();

        _destino.Text = nombreDelServidor is { Length: > 0 }
            ? $"Sesión: {nombreDelServidor}"
            : "Sin sesión · sólo administrar la lista";

        _soloEsta.Visibility = conexion is null ? Visibility.Collapsed : Visibility.Visible;

        Loaded += async (_, _) => await CargarAsync().ConfigureAwait(true);
        PreviewKeyDown += AlPresionarTecla;
    }

    private async Task CargarAsync()
    {
        _paleta = await _root.AppSettings.GetCommandPaletteAsync().ConfigureAwait(true);
        Refrescar();
        _filtro.Focus();
    }

    private void Refrescar(Guid? seleccionar = null)
    {
        _cargando = true;

        var visibles = _paleta.Visibles(_conexion, _filtro.Text);
        _lista.ItemsSource = visibles;

        _lista.SelectedItem = seleccionar is { } id
            ? visibles.FirstOrDefault(c => c.Id == id)
            : null;

        _cargando = false;

        if (_lista.SelectedItem is null)
        {
            Limpiar();
        }
    }

    private void Limpiar()
    {
        _cargando = true;

        _elegido = null;
        _nombre.Text = string.Empty;
        _comando.Text = string.Empty;
        _soloEsta.IsChecked = false;

        _cargando = false;
        ActualizarBotones();
    }

    private void ActualizarBotones()
    {
        var hayComando = !string.IsNullOrWhiteSpace(_comando.Text);
        var haySesion = _mandarALaSesion is not null;

        _enviar.IsEnabled = hayComando && haySesion;
        _escribir.IsEnabled = hayComando && haySesion;

        _guardar.IsEnabled = hayComando && !string.IsNullOrWhiteSpace(_nombre.Text);
        _borrar.IsEnabled = _elegido is not null;
    }

    private void AlFiltrar(object sender, TextChangedEventArgs e)
    {
        if (!_cargando)
        {
            Refrescar(_elegido?.Id);
        }
    }

    private void AlElegir(object sender, SelectionChangedEventArgs e)
    {
        if (_cargando || _lista.SelectedItem is not ComandoGuardado elegido)
        {
            return;
        }

        _cargando = true;

        _elegido = elegido;
        _nombre.Text = elegido.Nombre;
        _comando.Text = elegido.Comando;
        _soloEsta.IsChecked = !elegido.EsGlobal;

        _cargando = false;
        ActualizarBotones();
    }

    private void AlDobleClic(object sender, MouseButtonEventArgs e)
    {
        if (_lista.SelectedItem is ComandoGuardado)
        {
            AlEnviar(sender, new RoutedEventArgs());
        }
    }

    private void AlEditar(object sender, RoutedEventArgs e)
    {
        if (!_cargando)
        {
            ActualizarBotones();
        }
    }

    private void AlNuevo(object sender, RoutedEventArgs e)
    {
        _lista.SelectedItem = null;
        Limpiar();
        _nombre.Focus();
    }

    private async void AlGuardar(object sender, RoutedEventArgs e)
    {
        var conexion = _soloEsta.IsChecked == true ? _conexion : null;

        if (_elegido is { } existente)
        {
            _paleta.Actualizar(existente with
            {
                Nombre = _nombre.Text,
                Comando = _comando.Text,
                Conexion = conexion,
            });
        }
        else if (_paleta.Agregar(_nombre.Text, _comando.Text, conexion) is { } nuevo)
        {
            _elegido = nuevo;
        }

        await _root.AppSettings.SaveCommandPaletteAsync(_paleta).ConfigureAwait(true);
        Refrescar(_elegido?.Id);
    }

    private async void AlBorrar(object sender, RoutedEventArgs e)
    {
        if (_elegido is not { } elegido)
        {
            return;
        }

        var confirmado = Services.Dialogos.Confirmar(
            this,
            "¿Borrar el comando?",
            $"Se borra «{elegido.Nombre}» de la lista. La sesión no se toca.",
            "Borrar");

        if (!confirmado)
        {
            return;
        }

        _paleta.Quitar(elegido.Id);
        await _root.AppSettings.SaveCommandPaletteAsync(_paleta).ConfigureAwait(true);

        Limpiar();
        Refrescar();
    }

    private void AlEscribir(object sender, RoutedEventArgs e) => Mandar(ejecutar: false);

    private void AlEnviar(object sender, RoutedEventArgs e) => Mandar(ejecutar: true);

    private void Mandar(bool ejecutar)
    {
        var texto = _comando.Text;

        if (_mandarALaSesion is null || string.IsNullOrWhiteSpace(texto))
        {
            return;
        }

        _mandarALaSesion(texto, ejecutar);
        Close();
    }

    private void AlPresionarTecla(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                Close();
                e.Handled = true;
                break;

            case Key.Enter when _filtro.IsKeyboardFocusWithin && _lista.Items.Count > 0:
                _lista.SelectedIndex = 0;
                Mandar(ejecutar: true);
                e.Handled = true;
                break;
        }
    }
}
