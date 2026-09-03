using System.Collections.ObjectModel;
using System.Runtime.Versioning;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using CafManagerConection.UseCases.Abstractions;

namespace CafManagerConection.App.Views;

/// <summary>Una fila de la tabla, con los textos ya armados.</summary>
[SupportedOSPlatform("windows")]
public sealed class FilaDeTraza(EntradaDeTraza entrada)
{
    public EntradaDeTraza Entrada { get; } = entrada;

    public string Hora => Entrada.Momento.ToString("HH:mm:ss.fff");

    public string Servidor => Entrada.Servidor;

    public string Tipo => Entrada.Tipo switch
    {
        TipoDeTraza.Conexion => "conexión",
        TipoDeTraza.Escalada => "sudo",
        TipoDeTraza.Cierre => "cierre",
        _ => "comando",
    };

    public string Tardo => Entrada.Duracion switch
    {
        { TotalMilliseconds: < 1 } => "—",
        { TotalSeconds: < 1 } => $"{Entrada.Duracion.TotalMilliseconds:0} ms",
        _ => $"{Entrada.Duracion.TotalSeconds:0.0} s",
    };

    public string Estado => Entrada.Codigo?.ToString() ?? "—";

    public bool Fallo => Entrada.Fallo;

    /// <summary>Primera línea de lo enviado, para que la fila no crezca.</summary>
    public string Resumen
    {
        get
        {
            var corte = Entrada.Enviado.IndexOf('\n');
            var primera = corte < 0 ? Entrada.Enviado : Entrada.Enviado[..corte];
            var lineas = Entrada.Enviado.Count(c => c == '\n');

            return lineas > 0 ? $"{primera.TrimEnd()} … (+{lineas} líneas)" : primera;
        }
    }

    /// <summary>Texto completo, para el panel de detalle y para copiar.</summary>
    public string Completo
    {
        get
        {
            var texto = new StringBuilder();

            texto.Append(Hora).Append("  ").Append(Servidor)
                 .Append("  [").Append(Tipo).Append("]  ")
                 .Append(Tardo).Append("  salida ").AppendLine(Estado);

            texto.AppendLine().AppendLine("── enviado ──").AppendLine(Entrada.Enviado);

            if (Entrada.Salida.Length > 0)
            {
                texto.AppendLine().AppendLine("── recibido ──").AppendLine(Entrada.Salida);
            }

            if (Entrada.Error.Length > 0)
            {
                texto.AppendLine().AppendLine("── error ──").AppendLine(Entrada.Error);
            }

            return texto.ToString();
        }
    }

    /// <summary>Si esta fila coincide con lo que se escribió en el filtro.</summary>
    public bool Coincide(string aguja) =>
        aguja.Length == 0
        || Entrada.Enviado.Contains(aguja, StringComparison.OrdinalIgnoreCase)
        || Entrada.Servidor.Contains(aguja, StringComparison.OrdinalIgnoreCase)
        || Entrada.Salida.Contains(aguja, StringComparison.OrdinalIgnoreCase)
        || Entrada.Error.Contains(aguja, StringComparison.OrdinalIgnoreCase);
}

/// <summary>Consola de diagnóstico: qué manda y qué recibe la aplicación por atrás.</summary>
[SupportedOSPlatform("windows")]
public partial class ConsolaDeTraza : UserControl
{
    private readonly ObservableCollection<FilaDeTraza> _filas = [];
    private readonly List<FilaDeTraza> _todas = [];
    private RegistroDeTrazas? _registro;
    private string _aguja = string.Empty;

    public ConsolaDeTraza()
    {
        InitializeComponent();
        _tabla.ItemsSource = _filas;
    }

    /// <summary>Se pidió cerrar la consola desde su propio botón.</summary>
    public event EventHandler? PidioCerrar;

    public event EventHandler? PidioAbrirRegistros;

    /// <summary>Engancha la consola al registro y carga lo que ya haya pasado.</summary>
    public void Enganchar(RegistroDeTrazas registro)
    {
        if (_registro is not null)
        {
            return;
        }

        _registro = registro;

        _todas.Clear();
        _filas.Clear();

        foreach (var entrada in registro.Entradas())
        {
            Agregar(new FilaDeTraza(entrada), false);
        }

        registro.Anotada += AlAnotar;
        Refrescar();
        Resumir();
    }

    public void Desenganchar()
    {
        if (_registro is not null)
        {
            _registro.Anotada -= AlAnotar;
            _registro = null;
        }
    }

    private void AlAnotar(object? origen, EntradaDeTraza entrada) =>
        Dispatcher.BeginInvoke(() => Agregar(new FilaDeTraza(entrada), true));

    private void Agregar(FilaDeTraza fila, bool enVivo)
    {
        _todas.Add(fila);

        if (_todas.Count > RegistroDeTrazas.Capacidad)
        {
            var vieja = _todas[0];
            _todas.RemoveAt(0);
            _filas.Remove(vieja);
        }

        if (!fila.Coincide(_aguja))
        {
            return;
        }

        _filas.Add(fila);

        if (!enVivo)
        {
            return;
        }

        Resumir();
        Seguir();
    }

    /// <summary>Lleva la vista a la última entrada, si está activado seguir.</summary>
    private void Seguir()
    {
        if (_seguir.IsChecked == true && _filas.Count > 0)
        {
            _tabla.ScrollIntoView(_filas[^1]);
        }
    }

    private void AlAlternarSeguir(object sender, RoutedEventArgs e) => Seguir();

    private void Refrescar()
    {
        _filas.Clear();

        foreach (var fila in _todas.Where(f => f.Coincide(_aguja)))
        {
            _filas.Add(fila);
        }

        Seguir();
    }

    private void Resumir()
    {
        if (_registro is null)
        {
            return;
        }

        var mostradas = _filas.Count == _todas.Count
            ? $"{_todas.Count} a la vista"
            : $"{_filas.Count} de {_todas.Count} a la vista";

        _resumen.Text = $"{_registro.Anotadas} intercambios · "
                        + $"↑ {Tamano(_registro.BytesEnviados)} "
                        + $"↓ {Tamano(_registro.BytesRecibidos)} · {mostradas}"
                        + (_seguir.IsChecked == true ? string.Empty : " · sin seguir")
                        + (_registro.Activo ? string.Empty : " · EN PAUSA");

        Avisar();
    }

    /// <summary>El fallo se cuenta al pie y no sólo se colorea en su fila: si quedó fuera de la vista, nadie lo ve (FR-185c, FR-185d).</summary>
    private void Avisar()
    {
        var fallos = _todas.Count(f => f.Fallo);

        _fallos.Text = fallos == 1 ? "1 intercambio con fallo" : $"{fallos} intercambios con fallo";

        _fallos.Visibility = fallos > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private static string Tamano(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:0} KB",
        _ => $"{bytes / (1024.0 * 1024):0.0} MB",
    };

    private void AlFiltrar(object sender, TextChangedEventArgs e)
    {
        _aguja = _filtro.Text.Trim();
        Refrescar();
        Resumir();
    }

    private void AlElegirFila(object sender, SelectionChangedEventArgs e) =>
        _detalle.Text = _tabla.SelectedItem is FilaDeTraza fila
            ? fila.Completo
            : "Elegí una fila para ver lo que se mandó y lo que contestó.";

    private void AlPausar(object sender, RoutedEventArgs e)
    {
        if (_registro is null)
        {
            return;
        }

        _registro.Activo = !_registro.Activo;
        _pausa.Content = _registro.Activo ? "Pausar" : "Grabar";
        Resumir();
    }

    private void AlLimpiar(object sender, RoutedEventArgs e)
    {
        _registro?.Limpiar();
        _todas.Clear();
        _filas.Clear();
        _detalle.Text = "Elegí una fila para ver lo que se mandó y lo que contestó.";
        Resumir();
    }

    private void AlCopiar(object sender, RoutedEventArgs e)
    {
        if (_filas.Count == 0)
        {
            return;
        }

        var texto = string.Join(
            Environment.NewLine + new string('─', 60) + Environment.NewLine,
            _filas.Select(f => f.Completo));

        try
        {
            Clipboard.SetText(texto);
        }
        catch (Exception)
        {
        }
    }

    private void AlCerrar(object sender, RoutedEventArgs e) =>
        PidioCerrar?.Invoke(this, EventArgs.Empty);

    private void AlAbrirRegistros(object sender, RoutedEventArgs e) =>
        PidioAbrirRegistros?.Invoke(this, EventArgs.Empty);
}
