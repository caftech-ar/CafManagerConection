using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CafManagerConection.Platform;

namespace CafManagerConection.App.Views;

/// <summary>Ficha del proceso que tiene un puerto abierto (FR-165).</summary>
[SupportedOSPlatform("windows")]
public partial class ProcesoWindow : Window
{
    private readonly DetalleDeProceso _proceso;

    private ProcesoWindow(DetalleDeProceso proceso, string servidor, string puerto)
    {
        InitializeComponent();

        _proceso = proceso;

        Title = $"Proceso {proceso.Pid} · {proceso.Nombre}";
        _nombre.Text = proceso.Nombre;

        _contexto.Text = string.Join(" · ", new[]
        {
            $"PID {proceso.Pid}",
            puerto.Length > 0 ? $"escuchando en el puerto {puerto}" : null,
            servidor.Length > 0 ? servidor : null,
        }.Where(s => s is { Length: > 0 }));

        Faltantes(proceso);
        Datos(proceso);
    }

    private void Faltantes(DetalleDeProceso p)
    {
        if (p.NoSePudo.Count == 0)
        {
            return;
        }

        _avisoPermisos.Visibility = Visibility.Visible;

        var cabecera = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 0, 0, 6),
        };

        var glifo = new System.Windows.Shapes.Path
        {
            Width = 14,
            Height = 14,
            Stretch = Stretch.Uniform,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 7, 0),
        };

        glifo.SetResourceReference(System.Windows.Shapes.Path.DataProperty, "IconoAlerta");
        glifo.SetResourceReference(System.Windows.Shapes.Path.FillProperty, "MedidaAdvertencia");

        var titulo = new TextBlock
        {
            Text = "Hay datos que este usuario no puede leer",
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
        };

        cabecera.Children.Add(glifo);
        cabecera.Children.Add(titulo);

        _faltantes.Children.Add(cabecera);

        foreach (var motivo in p.NoSePudo)
        {
            var linea = new TextBlock
            {
                Text = "· " + motivo,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 2),
            };

            linea.SetResourceReference(TextBlock.ForegroundProperty, "TextoTenue");
            _faltantes.Children.Add(linea);
        }

        var salida = new TextBlock
        {
            Text = "Con permiso de sudo en el servidor, la ficha muestra todo.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 6, 0, 0),
        };

        salida.SetResourceReference(TextBlock.ForegroundProperty, "TextoTenue");
        _faltantes.Children.Add(salida);
    }

    private void Datos(DetalleDeProceso p)
    {
        Titulo("Identidad", "IconoAplicacion", "IconoCyan", primero: true);
        Dato("Binario", p.Binario ?? "—", p.Binario is null ? "TextoTenue" : null);

        Dato(
            "Usuario",
            p.Usuario ?? "—",
            p.Usuario is "root" ? "IconoAmbar" : null);

        Titulo("Ejecución", "IconoPanelEstado", "IconoAzul");
        Dato("Corriendo hace", p.Corriendo is { } t ? Duracion(t) : "—");
        Dato("Directorio de trabajo", p.Directorio ?? "—", p.Directorio is null ? "TextoTenue" : null);
        Dato("Proceso padre", p.Padre?.ToString() ?? "—");
        Dato("Hilos", p.Hilos?.ToString() ?? "—");

        Titulo("Línea de comando", "IconoTerminalExterna", "IconoLima");
        Dato("Comando", p.Comando ?? "—");
    }

    private void Titulo(string texto, string icono, string color, bool primero = false)
    {
        var fila = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, primero ? 0 : 16, 0, 7),
        };

        var glifo = new System.Windows.Shapes.Path
        {
            Width = 13,
            Height = 13,
            Stretch = Stretch.Uniform,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 7, 0),
        };

        glifo.SetResourceReference(System.Windows.Shapes.Path.DataProperty, icono);
        glifo.SetResourceReference(System.Windows.Shapes.Path.FillProperty, color);

        var t = new TextBlock
        {
            Text = texto.ToUpperInvariant(),
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0),
        };

        t.SetResourceReference(TextBlock.ForegroundProperty, "TextoTenue");

        fila.Children.Add(glifo);
        fila.Children.Add(t);

        _datos.Children.Add(fila);
    }

    private void Dato(string titulo, string valor, string? pincel = null)
    {
        var pila = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };

        var etiqueta = new TextBlock { Text = titulo, Margin = new Thickness(0) };
        etiqueta.SetResourceReference(StyleProperty, "Etiqueta");

        var texto = new TextBlock
        {
            Text = valor,
            TextWrapping = TextWrapping.Wrap,
            FontFamily = (FontFamily)FindResource("FuenteMono"),
            FontSize = (double)FindResource("CuerpoChico"),
            Margin = new Thickness(0, 2, 0, 0),
        };

        if (pincel is not null)
        {
            texto.SetResourceReference(TextBlock.ForegroundProperty, pincel);
        }

        pila.Children.Add(etiqueta);
        pila.Children.Add(texto);

        _datos.Children.Add(pila);
    }

    private static string Duracion(TimeSpan t) => t.TotalDays >= 1
        ? $"{(int)t.TotalDays} día(s) y {t.Hours} h"
        : t.TotalHours >= 1
            ? $"{(int)t.TotalHours} h {t.Minutes} min"
            : $"{t.Minutes} min {t.Seconds} s";

    private void AlCopiarComando(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(_proceso.Comando ?? _proceso.Nombre);
        }
        catch (COMException)
        {
        }
    }

    public static void Mostrar(
        Window owner, DetalleDeProceso proceso, string servidor, string puerto) =>
        new ProcesoWindow(proceso, servidor, puerto) { Owner = owner }.ShowDialog();
}
