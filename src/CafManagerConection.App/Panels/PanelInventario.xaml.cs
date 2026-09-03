using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;

namespace CafManagerConection.App.Panels;

/// <summary>Base de los paneles de inventario: encabezado con botón de refresco, tabla y resumen.</summary>
[SupportedOSPlatform("windows")]
public partial class PanelInventario : UserControl
{
    protected PanelInventario(string titulo)
    {
        InitializeComponent();
        _titulo.Text = titulo;
    }

    public virtual Task RefrescarAsync() => Task.CompletedTask;

    /// <summary>Qué hacer si el usuario pide reintentar con privilegios; null si no hay por dónde.</summary>
    private Func<Task>? _reintentarConPrivilegios;

    /// <summary>Ofrece reintentar con privilegios lo que este panel no alcanza a ver (FR-184a, FR-184d).</summary>
    public void MostrarEscalada(
        Domain.Settings.ResultadoDeSondeo? sondeo, string queNoSeVe, Func<Task>? reintentar)
    {
        _reintentarConPrivilegios = reintentar;

        _escaladaTexto.Text = Monitoring.MensajeDeEscalada.Texto(sondeo, queNoSeVe);
        _escalada.Visibility = Visibility.Visible;

        _botonEscalar.Visibility =
            reintentar is not null && Monitoring.MensajeDeEscalada.MuestraElBoton(sondeo)
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    /// <summary>Lo que pasó con la escalada: queda el aviso y no el botón.</summary>
    public void AvisoDeEscalada(string texto)
    {
        _reintentarConPrivilegios = null;

        _escaladaTexto.Text = texto;
        _escalada.Visibility = Visibility.Visible;
        _botonEscalar.Visibility = Visibility.Collapsed;
    }

    private async void AlEscalarPrivilegios(object sender, RoutedEventArgs e)
    {
        if (_reintentarConPrivilegios is not { } reintentar)
        {
            return;
        }

        _botonEscalar.IsEnabled = false;

        try
        {
            await reintentar().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            MostrarError($"La escalada de privilegios falló: {ex.Message}");
        }
        finally
        {
            _botonEscalar.IsEnabled = true;
        }
    }

    protected void MostrarResumen(string texto)
    {
        _resumen.Text = texto;
        _resumen.Foreground = (System.Windows.Media.Brush)FindResource("TextoTenue");
    }

    /// <summary>Enciende la franja de «trabajando» mientras dure lo que se devuelve.</summary>
    protected IDisposable Trabajando(string texto)
    {
        _ocupaciones++;

        _trabajandoTexto.Text = texto;
        _trabajando.Visibility = Visibility.Visible;
        Tabla.Opacity = 0.45;

        Tabla.IsHitTestVisible = false;
        _actualizar.IsEnabled = false;

        return new Fin(this);
    }

    private int _ocupaciones;

    private DateTimeOffset? _ultimaConsulta;

    private void MarcarConsulta()
    {
        _ultimaConsulta = DateTimeOffset.Now;
        MostrarUltima();
    }

    private void MostrarUltima()
    {
        if (_ultimaConsulta is not { } cuando)
        {
            _ultima.Text = string.Empty;
            return;
        }

        var hace = DateTimeOffset.Now - cuando;

        _ultima.Text = hace switch
        {
            { TotalSeconds: < 10 } => $"Actualizado {cuando:HH:mm:ss}",
            { TotalMinutes: < 1 } => $"hace {(int)hace.TotalSeconds} s · {cuando:HH:mm:ss}",
            { TotalMinutes: < 60 } => $"hace {(int)hace.TotalMinutes} min · {cuando:HH:mm}",
            _ => $"hace {(int)hace.TotalHours} h · {cuando:HH:mm}",
        };
    }

    private sealed class Fin(PanelInventario panel) : IDisposable
    {
        public void Dispose()
        {
            if (--panel._ocupaciones > 0)
            {
                return;
            }

            panel.MarcarConsulta();

            panel._trabajando.Visibility = Visibility.Collapsed;
            panel.Tabla.Opacity = 1;
            panel.Tabla.IsHitTestVisible = true;
            panel._actualizar.IsEnabled = true;
        }
    }

    /// <summary>Estilo de celda que cambia el color del texto cuando se cumple una condición de la fila.</summary>
    protected Style ColorDeCelda(string propiedad, object valor, System.Windows.Media.Brush pincel)
    {
        var estilo = new Style(typeof(DataGridCell), (Style)FindResource(typeof(DataGridCell)));

        var disparador = new DataTrigger
        {
            Binding = new System.Windows.Data.Binding(propiedad),
            Value = valor,
        };

        disparador.Setters.Add(new Setter(Control.ForegroundProperty, pincel));
        estilo.Triggers.Add(disparador);

        return estilo;
    }

    protected void MostrarError(string? error)
    {
        Tabla.ItemsSource = null;
        _resumen.Text = error ?? "No se pudo consultar el servidor.";
        _resumen.Foreground = (System.Windows.Media.Brush)FindResource("Destructivo");
    }

    protected override void OnVisualParentChanged(System.Windows.DependencyObject oldParent)
    {
        base.OnVisualParentChanged(oldParent);
        MostrarUltima();
    }

    private async void AlActualizar(object sender, RoutedEventArgs e) =>
        await RefrescarAsync().ConfigureAwait(true);
}
