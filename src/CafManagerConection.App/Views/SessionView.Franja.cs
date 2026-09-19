using System.Globalization;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CafManagerConection.App.Themes;
using CafManagerConection.Domain.Connections;
using CafManagerConection.Monitoring;

namespace CafManagerConection.App.Views;

/// <summary>Franja de métricas al pie de una sesión SSH.</summary>
[SupportedOSPlatform("windows")]
public partial class SessionView
{
    /// <summary>De menor a mayor interés: lo primero es lo primero que se oculta al angostarse.</summary>
    private static readonly string[] OrdenDeCaida =
        ["actividad", "distribucion", "carga", "red"];

    private readonly Dictionary<string, FrameworkElement> _medidasPorClave = [];
    private readonly List<double> _historialCpu = [];
    private readonly List<double> _historialMemoria = [];
    private bool _muestreando;

    /// <summary>Cuántas muestras dibuja el gráfico de CPU y de memoria.</summary>
    private const int MuestrasDelGrafico = 24;

    /// <summary>Arranca la franja de esta sesión, si está activa para esta conexión.</summary>
    private async Task PrepararFranjaAsync()
    {
        if (_comandos is null)
        {
            return;
        }

        var ajustes = await _root.AppSettings.GetMetricsBarSettingsAsync().ConfigureAwait(true);

        var activa = AjustesReservados.Activo(
            _registro.Connection, AjustesReservados.BarraDeMetricas, ajustes.Activa);

        if (!activa)
        {
            return;
        }

        ArmarFranja();
        MostrarEnLaFranja(null);

        _colectorDeFranja = new ColectorDeFranja(
            new EjecutorRemoto(_comandos), logger: _root.Logger);

        _corteDeFranja = new CancellationTokenSource();

        await _colectorDeFranja
            .LeerIdentidadAsync(
                Domain.Settings.Defaults.MetricsQueryTimeoutSeconds, _corteDeFranja.Token)
            .ConfigureAwait(true);

        _relojDeFranja.Interval = TimeSpan.FromSeconds(ajustes.Segundos);
        _relojDeFranja.Tick += AlTocarElRelojDeFranja;
        _relojDeFranja.Start();

        await MuestrearLaFranjaAsync().ConfigureAwait(true);
    }

    private async Task MuestrearLaFranjaAsync()
    {
        // Sin este corte, una lectura más lenta que el intervalo deja al reloj encimando comandos
        // sobre el mismo canal.
        if (_muestreando
            || _colectorDeFranja is not { } colector
            || _corteDeFranja is not { } corte)
        {
            return;
        }

        _muestreando = true;

        try
        {
            var muestra = await colector
                .MuestrearAsync(
                    Domain.Settings.Defaults.MetricsQueryTimeoutSeconds, corte.Token)
                .ConfigureAwait(true);

            MostrarEnLaFranja(muestra ?? colector.Ultima);
        }
        catch (OperationCanceledException)
        {
            // La sesión se cerró mientras se leía: no hay nada que mostrar.
        }
        finally
        {
            _muestreando = false;
        }
    }

    /// <summary>Corta el muestreo y suelta el colector al terminar la sesión.</summary>
    private void DetenerLaFranja()
    {
        // Se desuscribe todo: DesarmarParaReconectar pasa por acá en cada reconexión, y cada tick
        // acumulado es un comando más contra el servidor.
        _relojDeFranja.Stop();
        _relojDeFranja.Tick -= AlTocarElRelojDeFranja;
        _franjaMetricas.SizeChanged -= AlCambiarElAnchoDeLaFranja;

        _corteDeFranja?.Cancel();
        _corteDeFranja?.Dispose();
        _corteDeFranja = null;
        _colectorDeFranja = null;

        _franjaMetricas.Visibility = Visibility.Collapsed;
        _medidas.Children.Clear();
        _medidasPorClave.Clear();
        _historialCpu.Clear();
        _historialMemoria.Clear();
    }

    private void ArmarFranja()
    {
        if (_medidas.Children.Count > 0)
        {
            return;
        }

        Medida("distribucion");
        Medida("cpu", conGrafico: true);
        Medida("memoria", conGrafico: true);
        Medida("disco");
        Medida("red");
        Medida("carga", tenue: true);
        Medida("actividad", tenue: true);

        _franjaMetricas.Visibility = Visibility.Visible;
        _franjaMetricas.SizeChanged += AlCambiarElAnchoDeLaFranja;
    }

    private void AlTocarElRelojDeFranja(object? remitente, EventArgs e) =>
        _ = MuestrearLaFranjaAsync();

    private void AlCambiarElAnchoDeLaFranja(object? remitente, SizeChangedEventArgs e) =>
        AjustarLoQueEntra();

    private void Medida(string clave, bool conGrafico = false, bool tenue = false)
    {
        var texto = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 11,
            Foreground = tenue
                ? Pinceles.De("TextoConsolaTenue")
                : Pinceles.De("TextoConsola"),
        };

        var contenido = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 0, 14, 0),
            Tag = clave,
        };

        contenido.Children.Add(texto);

        if (conGrafico)
        {
            contenido.Children.Add(new System.Windows.Shapes.Polyline
            {
                Stroke = Pinceles.De("TextoConsolaTenue"),
                StrokeThickness = 1,
                Width = 34,
                Height = 12,
                Margin = new Thickness(6, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
            });
        }

        _medidas.Children.Add(contenido);
        _medidasPorClave[clave] = contenido;
    }

    /// <summary>Vuelca una muestra en la franja.</summary>
    /// <param name="muestra">La lectura del servidor, o <c>null</c> si todavía no hay ninguna.</param>
    private void MostrarEnLaFranja(MuestraDeFranja? muestra)
    {
        if (_medidas.Children.Count == 0)
        {
            return;
        }

        if (muestra is null)
        {
            Escribir("cpu", "CPU —", NivelDeMedida.Normal);
            Escribir("memoria", "MEM —", NivelDeMedida.Normal);
            return;
        }

        MostrarCpu(muestra);
        MostrarMemoria(muestra);
        MostrarDisco(muestra);
        MostrarRed(muestra);

        Escribir(
            "carga",
            $"carga {muestra.Carga.OneMinute.ToString("0.0", CultureInfo.CurrentCulture)}",
            NivelDeUso.DeCarga(muestra.Carga.OneMinute, muestra.Cpu?.CoreCount ?? 0));

        Escribir("actividad", Actividad(muestra.Actividad), NivelDeMedida.Normal);

        Escribir(
            "distribucion",
            string.IsNullOrWhiteSpace(muestra.Distribucion) ? string.Empty : muestra.Distribucion,
            NivelDeMedida.Normal);

        AjustarLoQueEntra();
    }

    private void MostrarCpu(MuestraDeFranja muestra)
    {
        if (muestra.Cpu is not { } cpu)
        {
            Escribir("cpu", "CPU —", NivelDeMedida.Normal);
            return;
        }

        Escribir(
            "cpu",
            $"CPU {Porcentaje(cpu.UsedPercent)}",
            NivelDeUso.DePorcentaje(cpu.UsedPercent));

        Graficar("cpu", _historialCpu, cpu.UsedPercent);
    }

    private void MostrarMemoria(MuestraDeFranja muestra)
    {
        var usado = muestra.Memoria.UsedPercent;

        Escribir("memoria", $"MEM {Porcentaje(usado)}", NivelDeUso.DePorcentaje(usado));
        Graficar("memoria", _historialMemoria, usado);
    }

    private void MostrarDisco(MuestraDeFranja muestra)
    {
        if (muestra.PeorDisco is not { } disco)
        {
            Escribir("disco", string.Empty, NivelDeMedida.Normal);
            return;
        }

        Escribir(
            "disco",
            $"{disco.MountPoint} {Porcentaje(disco.UsedPercent)}",
            NivelDeUso.DePorcentaje(disco.UsedPercent));
    }

    private void MostrarRed(MuestraDeFranja muestra)
    {
        if (muestra.Red is not { } red)
        {
            Escribir("red", "↓ — ↑ —", NivelDeMedida.Normal);
            return;
        }

        Escribir(
            "red",
            $"↓{PorSegundo(red.BytesInPerSecond)} ↑{PorSegundo(red.BytesOutPerSecond)}",
            NivelDeMedida.Normal);
    }

    private void Escribir(string clave, string texto, NivelDeMedida nivel)
    {
        if (!_medidasPorClave.TryGetValue(clave, out var contenido)
            || contenido is not StackPanel panel
            || panel.Children[0] is not TextBlock bloque)
        {
            return;
        }

        bloque.Text = texto;
        bloque.Foreground = Pinceles.De(nivel switch
        {
            NivelDeMedida.Critico => "EstadoError",
            NivelDeMedida.Advertencia => "MedidaAdvertencia",
            _ => clave is "carga" or "actividad" ? "TextoConsolaTenue" : "TextoConsola",
        });

        panel.Visibility = texto.Length == 0 ? Visibility.Collapsed : Visibility.Visible;

        if (NivelDeUso.Etiqueta(nivel) is { } etiqueta)
        {
            panel.ToolTip = $"{texto} · {etiqueta}";
        }
        else
        {
            panel.ToolTip = null;
        }
    }

    private void Graficar(string clave, List<double> historial, double valor)
    {
        historial.Add(valor);

        if (historial.Count > MuestrasDelGrafico)
        {
            historial.RemoveRange(0, historial.Count - MuestrasDelGrafico);
        }

        if (!_medidasPorClave.TryGetValue(clave, out var contenido)
            || contenido is not StackPanel panel
            || panel.Children.Count < 2
            || panel.Children[1] is not System.Windows.Shapes.Polyline linea)
        {
            return;
        }

        var puntos = new PointCollection();
        var paso = linea.Width / Math.Max(1, MuestrasDelGrafico - 1);

        for (var i = 0; i < historial.Count; i++)
        {
            var y = linea.Height - (Math.Clamp(historial[i], 0, 100) / 100 * linea.Height);
            puntos.Add(new Point(i * paso, y));
        }

        linea.Points = puntos;
    }

    /// <summary>Oculta lo que no entra a lo ancho, empezando por lo que menos se mira.</summary>
    private void AjustarLoQueEntra()
    {
        foreach (var clave in OrdenDeCaida)
        {
            Mostrar(clave, visible: true);
        }

        _medidas.UpdateLayout();

        foreach (var clave in OrdenDeCaida)
        {
            if (AnchoNecesario() <= AnchoDisponible())
            {
                return;
            }

            Mostrar(clave, visible: false);
            _medidas.UpdateLayout();
        }
    }

    /// <summary>Lo que piden las medidas visibles. Se suma el pedido de cada una y no el ancho ya
    /// dispuesto: el del contenedor nunca supera al del padre, así que nunca acusaría el desborde.</summary>
    private double AnchoNecesario() =>
        _medidas.Children.OfType<FrameworkElement>()
            .Where(m => m.Visibility == Visibility.Visible)
            .Sum(m => m.DesiredSize.Width);

    private double AnchoDisponible() =>
        _franjaMetricas.ActualWidth
        - _franjaMetricas.Padding.Left - _franjaMetricas.Padding.Right
        - _franjaMetricas.BorderThickness.Left - _franjaMetricas.BorderThickness.Right;

    private void Mostrar(string clave, bool visible)
    {
        if (_medidasPorClave.TryGetValue(clave, out var contenido)
            && contenido is StackPanel panel
            && panel.Children[0] is TextBlock bloque
            && bloque.Text.Length > 0)
        {
            panel.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private static string Porcentaje(double valor) =>
        $"{valor.ToString("0", CultureInfo.CurrentCulture)}%";

    private static string PorSegundo(double bytes) => bytes switch
    {
        >= 1024 * 1024 => $"{(bytes / (1024.0 * 1024)).ToString("0.#", CultureInfo.CurrentCulture)} MB/s",
        >= 1024 => $"{(bytes / 1024.0).ToString("0", CultureInfo.CurrentCulture)} KB/s",
        _ => $"{bytes.ToString("0", CultureInfo.CurrentCulture)} B/s",
    };

    private static string Actividad(TimeSpan encendido) => encendido.TotalDays >= 1
        ? $"{(int)encendido.TotalDays} día(s)"
        : $"{(int)encendido.TotalHours} h";
}
