using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using CafManagerConection.App.Bootstrap;
using CafManagerConection.Domain.Connections;

namespace CafManagerConection.App.Views;

/// <summary>Define los túneles de una conexión: reenvíos de puerto local (FR-088 a FR-093).</summary>
[SupportedOSPlatform("windows")]
public partial class TunnelEditorWindow : Window
{
    public sealed record Fila(string Nombre, string Local, string Remoto, string Auto, SshTunnel Tunel);

    /// <summary>Valores con los que abrir el formulario ya cargado.</summary>
    public sealed record Sugerencia(
        string Nombre, int PuertoLocal, string HostRemoto, int PuertoRemoto, bool AutoIniciar);

    private readonly CompositionRoot _root;
    private readonly Guid _conexionId;
    private readonly Sugerencia? _sugerencia;

    private SshTunnel? _elegido;

    /// <summary>El túnel que se creó en esta ventana, o null si no se creó ninguno.</summary>
    public SshTunnel? Creado { get; private set; }

    public TunnelEditorWindow(
        CompositionRoot root, Guid conexionId, Sugerencia? sugerencia = null)
    {
        _root = root;
        _conexionId = conexionId;
        _sugerencia = sugerencia;

        InitializeComponent();

        Loaded += async (_, _) =>
        {
            await CargarAsync().ConfigureAwait(true);
            Prellenar();
        };
    }

    /// <summary>Carga el formulario con la sugerencia, si hay una.</summary>
    private void Prellenar()
    {
        if (_sugerencia is not { } s)
        {
            return;
        }

        _elegido = null;
        _lista.SelectedItem = null;

        _nombre.Text = s.Nombre;
        _puertoLocal.Text = s.PuertoLocal.ToString();
        _hostRemoto.Text = s.HostRemoto;
        _puertoRemoto.Text = s.PuertoRemoto.ToString();
        _autoIniciar.IsChecked = s.AutoIniciar;

        _nombre.Focus();
        _nombre.SelectAll();
    }

    private async Task CargarAsync()
    {
        var tuneles = await _root.Tunnels.GetForConnectionAsync(_conexionId).ConfigureAwait(true);

        _lista.ItemsSource = tuneles
            .OrderBy(t => t.SortOrder)
            .Select(t => new Fila(
                t.Name,
                t.LocalPort.ToString(),
                $"{t.RemoteHost}:{t.RemotePort}",
                t.AutoStart ? "sí" : "no",
                t))
            .ToList();
    }

    private void AlElegir(object sender, SelectionChangedEventArgs e)
    {
        if (_lista.SelectedItem is not Fila fila)
        {
            return;
        }

        _elegido = fila.Tunel;

        _nombre.Text = fila.Tunel.Name;
        _puertoLocal.Text = fila.Tunel.LocalPort.ToString();
        _hostRemoto.Text = fila.Tunel.RemoteHost;
        _puertoRemoto.Text = fila.Tunel.RemotePort.ToString();
        _autoIniciar.IsChecked = fila.Tunel.AutoStart;
    }

    private async void AlGuardar(object sender, RoutedEventArgs e)
    {
        _error.Visibility = Visibility.Collapsed;

        var nombre = _nombre.Text.Trim();
        var hostRemoto = _hostRemoto.Text.Trim();

        if (nombre.Length == 0 || hostRemoto.Length == 0)
        {
            Fallar("El nombre y el host remoto son obligatorios.");
            return;
        }

        if (!Puerto(_puertoLocal.Text, out var local))
        {
            Fallar("El puerto local debe ser un número entre 1 y 65535.");
            return;
        }

        if (!Puerto(_puertoRemoto.Text, out var remoto))
        {
            Fallar("El puerto remoto debe ser un número entre 1 y 65535.");
            return;
        }

        try
        {
            if (_elegido is { } existente)
            {
                existente.Name = nombre;
                existente.LocalPort = local;
                existente.RemoteHost = hostRemoto;
                existente.RemotePort = remoto;
                existente.AutoStart = _autoIniciar.IsChecked == true;

                await _root.Tunnels.UpdateAsync(existente).ConfigureAwait(true);
            }
            else
            {
                var nuevo = new SshTunnel(
                    Guid.NewGuid(), _conexionId, nombre, local, hostRemoto, remoto)
                {
                    AutoStart = _autoIniciar.IsChecked == true,
                };

                await _root.Tunnels.AddAsync(nuevo).ConfigureAwait(true);
                Creado = nuevo;
            }

            Limpiar();
            await CargarAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _root.Logger.TechnicalError("guardar el túnel", ex);
            Fallar("No se pudo guardar el túnel.");
        }
    }

    private async void AlQuitar(object sender, RoutedEventArgs e)
    {
        if (_elegido is not { } tunel)
        {
            Fallar("Elegí un túnel de la lista.");
            return;
        }

        await _root.Tunnels.DeleteAsync(tunel.Id).ConfigureAwait(true);

        Limpiar();
        await CargarAsync().ConfigureAwait(true);
    }

    private void Limpiar()
    {
        _elegido = null;
        _nombre.Clear();
        _puertoLocal.Clear();
        _hostRemoto.Clear();
        _puertoRemoto.Clear();
        _autoIniciar.IsChecked = false;
        _lista.SelectedItem = null;
    }

    private void Fallar(string mensaje)
    {
        _error.Text = mensaje;
        _error.Visibility = Visibility.Visible;
    }

    private static bool Puerto(string texto, out int valor) =>
        int.TryParse(texto.Trim(), out valor) && valor is >= 1 and <= 65535;
}
