using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;

namespace CafManagerConection.App.Views;

/// <summary>Pestañas que no son sesiones: el ping y el visor de bitácoras.</summary>
[SupportedOSPlatform("windows")]
public partial class MainWindow
{
    /// <summary>Abre una herramienta en su pestaña y la trae al frente.</summary>
    /// <param name="titulo">Lo que se lee en la solapa.</param>
    /// <param name="contenido">La vista de la herramienta.</param>
    private TabItem AbrirHerramienta(string titulo, FrameworkElement contenido)
    {
        var pestana = new TabItem { Content = contenido };

        _titulosDePestana[pestana] = titulo;
        pestana.Header = CabeceraDeHerramienta(titulo, pestana);

        _sesiones.Items.Add(pestana);
        _sesiones.SelectedItem = pestana;

        _vacio.Visibility = Visibility.Collapsed;
        _sesiones.Visibility = Visibility.Visible;

        ActualizarTitulo();

        return pestana;
    }

    /// <summary>Solapa de una herramienta: sin punto de estado, que es de las sesiones.</summary>
    /// <param name="titulo">Lo que se lee en la solapa.</param>
    /// <param name="pestana">La pestaña que la cabecera encabeza.</param>
    private object CabeceraDeHerramienta(string titulo, TabItem pestana)
    {
        var fila = new StackPanel { Orientation = Orientation.Horizontal };

        fila.Children.Add(new TextBlock
        {
            Text = titulo,
            VerticalAlignment = VerticalAlignment.Center,
        });

        var cerrar = new Button
        {
            Content = "✕",
            Style = (Style)FindResource("BotonTenue"),
            Width = 18,
            Height = 18,
            Padding = new Thickness(0),
            FontSize = 10,
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = "Cerrar (Ctrl+W)",
        };

        cerrar.Click += (_, ev) =>
        {
            ev.Handled = true;
            CerrarHerramienta(pestana);
        };

        fila.Children.Add(cerrar);

        return fila;
    }

    /// <summary>Cierra una pestaña de herramienta, dando de baja lo que tuviera en curso.</summary>
    /// <param name="pestana">La pestaña a cerrar.</param>
    private void CerrarHerramienta(TabItem pestana)
    {
        if (EsSesion(pestana))
        {
            return;
        }

        if (pestana.Content is IDisposable descartable)
        {
            try
            {
                descartable.Dispose();
            }
            catch (Exception ex)
            {
                _root.Logger.TechnicalError("cerrar una pestaña de herramienta", ex);
            }
        }

        _sesiones.Items.Remove(pestana);
        _titulosDePestana.Remove(pestana);

        AjustarVacio();
        ActualizarTitulo();
    }

    /// <summary>Si la pestaña es una sesión; las de herramienta no llevan identificador.</summary>
    /// <param name="pestana">La pestaña a clasificar.</param>
    private static bool EsSesion(TabItem pestana) => pestana.Tag is Guid;

    /// <summary>Cuántas pestañas son sesiones. El resto no cuenta para avisar al cerrar.</summary>
    private int SesionesAbiertas() => _sesiones.Items.OfType<TabItem>().Count(EsSesion);
}
