using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using CafManagerConection.App.Bootstrap;
using CafManagerConection.App.Views;
using CafManagerConection.Domain.Connections;
using CafManagerConection.Ssh;

namespace CafManagerConection.App.Panels;

/// <summary>Túneles definidos para la conexión, con su estado (US8).</summary>
[SupportedOSPlatform("windows")]
public partial class TunnelsPanel : UserControl
{
    public sealed record Fila(string Nombre, string Local, string Remoto, string Estado, SshTunnel Tunel);

    private readonly TunnelHost _host;
    private readonly CompositionRoot _root;
    private readonly Guid _conexionId;

    public TunnelsPanel(TunnelHost host, CompositionRoot root, Guid conexionId)
    {
        _host = host;
        _root = root;
        _conexionId = conexionId;

        InitializeComponent();

        _host.StatusChanged += (_, _) => Dispatcher.Invoke(() => _ = RefrescarAsync());
    }

    public async Task RefrescarAsync()
    {
        var definidos = await _root.Tunnels
            .GetForConnectionAsync(_conexionId)
            .ConfigureAwait(true);

        _lista.ItemsSource = definidos
            .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .Select(t =>
            {
                var estado = _host.StatusOf(t);

                return new Fila(
                    t.Name,
                    t.LocalPort.ToString(),
                    $"{t.RemoteHost}:{t.RemotePort}",
                    estado.IsActive
                        ? "activo"
                        : estado.FailureMessage ?? "detenido",
                    t);
            })
            .ToList();

        var activos = definidos.Count(t => _host.IsActive(t.Id));

        _resumen.Text = definidos.Count == 0
            ? "No hay túneles definidos. Agregá uno con «Editar…»."
            : $"{activos} de {definidos.Count} túnel(es) activo(s)";
    }

    private void AlElegir(object sender, SelectionChangedEventArgs e) =>
        _quitar.IsEnabled = _lista.SelectedItem is Fila;

    private async void AlLevantar(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_lista.SelectedItem is not Fila fila)
            {
                _resumen.Text = "Elegí un túnel de la lista.";
                return;
            }

            var error = await _host.StartAsync(fila.Tunel).ConfigureAwait(true);

            await RefrescarAsync().ConfigureAwait(true);

            if (error is not null)
            {
                _resumen.Text = error;
            }
        }
        catch (Exception ex)
        {
            Fallar("levantar el túnel", "No se pudo levantar el túnel.", ex);
        }
    }

    private async void AlBajar(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_lista.SelectedItem is not Fila fila)
            {
                _resumen.Text = "Elegí un túnel de la lista.";
                return;
            }

            _host.Stop(fila.Tunel);
            await RefrescarAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            Fallar("bajar el túnel", "No se pudo bajar el túnel.", ex);
        }
    }

    private async void AlEditar(object sender, RoutedEventArgs e)
    {
        try
        {
            if (Window.GetWindow(this) is not { } ventana)
            {
                return;
            }

            new TunnelEditorWindow(_root, _conexionId) { Owner = ventana }.ShowDialog();

            await RefrescarAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            Fallar("abrir el editor de túneles", "No se pudo abrir el editor de túneles.", ex);
        }
    }

    private async void AlQuitar(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_lista.SelectedItem is not Fila fila
                || Window.GetWindow(this) is not { } ventana)
            {
                return;
            }

            var tunel = fila.Tunel;
            var activo = _host.IsActive(tunel.Id);

            var mensaje = activo
                ? $"Se va a borrar el túnel «{tunel.Name}».\n\n"
                  + $"Está activo: primero se lo baja y se libera el puerto local {tunel.LocalPort}."
                : $"Se va a borrar el túnel «{tunel.Name}». No está activo.";

            if (!MessageWindow.Confirmar(ventana, "Borrar el túnel", mensaje, "Borrar"))
            {
                return;
            }

            if (activo)
            {
                _host.Stop(tunel);
            }

            await _root.Tunnels.DeleteAsync(tunel.Id).ConfigureAwait(true);
            await RefrescarAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            Fallar("borrar el túnel", "No se pudo borrar el túnel.", ex);
        }
    }

    private void Fallar(string accion, string mensaje, Exception ex)
    {
        _root.Logger.TechnicalError(accion, ex);
        _resumen.Text = mensaje;
    }
}
