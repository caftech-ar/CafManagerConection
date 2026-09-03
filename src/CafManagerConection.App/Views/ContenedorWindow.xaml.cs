using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using CafManagerConection.App.Services;
using CafManagerConection.Monitoring;
using CafManagerConection.Platform;

namespace CafManagerConection.App.Views;

/// <summary>Ficha de un contenedor: qué es, cómo está, qué consume y qué dice su registro (FR-150).</summary>
[SupportedOSPlatform("windows")]
public partial class ContenedorWindow : Window
{
    private readonly ControlDeDocker _control;
    private readonly string _contenedor;
    private readonly string _servidor;

    private DetalleDeContenedor? _detalle;

    /// <summary>Canal de docker logs -f abierto, o null si no se está siguiendo el registro en vivo.</summary>
    private IAsyncDisposable? _canalDeRegistro;

    private CancellationTokenSource? _ctsRegistro;

    /// <summary>Cuántas líneas hay en el cuadro del registro, para decirlo en la cabecera.</summary>
    private int _lineas;

    private int _errores;
    private DateTimeOffset? _ultimaLinea;
    private DateTimeOffset _ultimaLectura = DateTimeOffset.Now;

    /// <summary>Refresca el «hace tanto» sin preguntarle nada al servidor.</summary>
    private readonly System.Windows.Threading.DispatcherTimer _reloj = new()
    {
        Interval = TimeSpan.FromSeconds(15),
    };

    public ContenedorWindow(ControlDeDocker control, string contenedor, string servidor)
    {
        _control = control;
        _contenedor = contenedor;
        _servidor = servidor;

        InitializeComponent();

        Title = $"{contenedor} · {servidor}";
        _nombre.Text = contenedor;

        // El pulgar del tema claro es #40000000 y desaparece sobre FondoConsola.
        _registro.Resources["Pulgar"] = FindResource("PulgarConsola");
        _registro.Resources["PulgarActivo"] = FindResource("PulgarConsolaActivo");

        _seguir.Visibility = _control.PuedeSeguirRegistro ? Visibility.Visible : Visibility.Collapsed;

        _reloj.Tick += (_, _) => Declarar();
        _reloj.Start();

        Declarar();

        Loaded += async (_, _) => await CargarAsync().ConfigureAwait(true);

        Closed += async (_, _) =>
        {
            _reloj.Stop();
            await PararRegistroAsync().ConfigureAwait(false);
        };
    }

    /// <summary>Dice qué se está monitoreando y cuándo llegó lo último: un registro congelado y un contenedor tranquilo se ven igual (FR-185a).</summary>
    private void Declarar()
    {
        var cuando = _ultimaLinea is { } linea
            ? $"última línea {SeguimientoDeArchivo.Hace(linea)}"
            : $"última lectura {SeguimientoDeArchivo.Hace(_ultimaLectura)}, sin líneas nuevas";

        _monitoreado.Text = $"docker logs {_contenedor} · stdout y stderr · {cuando}";
    }

    /// <summary>El aviso vive en la ventana y no entre las líneas del registro: tiene que verse aunque el usuario esté en otra pestaña (FR-185c).</summary>
    private void Avisar(string texto)
    {
        _aviso.Text = texto;
        _marcoAviso.Visibility = Visibility.Visible;

        Title = _errores > 0
            ? $"{_contenedor} · {_servidor} · {_errores} con error"
            : $"{_contenedor} · {_servidor} · atención";
    }

    private void AlDescartarAviso(object sender, RoutedEventArgs e)
    {
        _marcoAviso.Visibility = Visibility.Collapsed;
        Title = $"{_contenedor} · {_servidor}";
    }

    private async Task CargarAsync()
    {
        _estado.Text = "Consultando el contenedor…";
        Ocupar(true);

        var r = await _control.GetDetalleAsync(_contenedor).ConfigureAwait(true);

        Ocupar(false);

        if (!r.Success || r.Value is null)
        {
            _estado.Text = r.Error ?? "No se pudo leer el contenedor.";
            _estado.Foreground = (Brush)FindResource("Destructivo");
            return;
        }

        _estado.Text = $"Actualizado {DateTimeOffset.Now:HH:mm:ss}";
        _estado.Foreground = (Brush)FindResource("TextoTenue");

        _detalle = r.Value;
        Mostrar(r.Value);
    }

    private void Ocupar(bool si)
    {
        _reiniciar.IsEnabled = !si;
        _detener.IsEnabled = !si;
        _iniciar.IsEnabled = !si;
    }

    private void Mostrar(DetalleDeContenedor d)
    {
        var corriendo = d.Estado.Equals("running", StringComparison.OrdinalIgnoreCase);

        _punto.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, ColorDeGravedad(d.Gravedad));

        _imagen.Text = d.Imagen;

        _detener.Visibility = corriendo ? Visibility.Visible : Visibility.Collapsed;
        _iniciar.Visibility = corriendo ? Visibility.Collapsed : Visibility.Visible;

        if (d.Salud is { Length: > 0 } salud)
        {
            _salud.Text = salud;
            _marcaSalud.Visibility = Visibility.Visible;
            _marcaSalud.Background = (Brush)FindResource(ColorDeGravedad(d.Gravedad));
            _salud.Foreground = Brushes.White;
        }
        else
        {
            _marcaSalud.Visibility = Visibility.Collapsed;
        }

        Metricas(d);
        Datos(d);

        if (_canalDeRegistro is null)
        {
            PintarRegistro(d);
        }
    }

    /// <summary>Pincel del tema que le corresponde a cada gravedad.</summary>
    private static string ColorDeGravedad(GravedadDeContenedor gravedad) => gravedad switch
    {
        GravedadDeContenedor.Corriendo => "EstadoConectado",
        GravedadDeContenedor.Advertencia => "IconoAmbar",
        GravedadDeContenedor.Falla => "EstadoError",
        _ => "EstadoInactivo",
    };

    private void Metricas(DetalleDeContenedor d)
    {
        _metricas.Children.Clear();

        Metrica("CPU", d.Cpu, nivel: Tramo(d.Cpu));
        Metrica("Memoria", d.MemoriaPorcentaje, d.Memoria, Tramo(d.MemoriaPorcentaje));
        Metrica("Arriba", d.Uptime is { } arriba ? TiempoArriba(arriba) : "—");

        Metrica(
            "Reinicios",
            d.Reinicios.ToString(System.Globalization.CultureInfo.CurrentCulture),
            nivel: d.Reinicios > 0 ? NivelDeMedida.Advertencia : NivelDeMedida.Normal);
    }

    // Docker escribe el decimal con punto: con la cultura del equipo, 23.75 se leería 2375.
    /// <summary>Tramo de un porcentaje como el que escribe docker stats: «23.75%».</summary>
    private static NivelDeMedida Tramo(string? porcentaje)
    {
        var texto = porcentaje?.Trim().TrimEnd('%');

        return double.TryParse(
            texto,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out var valor)
            ? NivelDeUso.DePorcentaje(valor)
            : NivelDeMedida.Normal;
    }

    private void Metrica(
        string titulo,
        string valor,
        string? detalle = null,
        NivelDeMedida nivel = NivelDeMedida.Normal)
    {
        var marco = new Border
        {
            Margin = new Thickness(0, 0, 10, 0),
            Padding = new Thickness(10, 7, 10, 8),
            CornerRadius = new CornerRadius(4),
            BorderThickness = new Thickness(3, 0, 0, 0),
        };

        marco.SetResourceReference(Border.BackgroundProperty, "Fondo");
        marco.SetResourceReference(Border.BorderBrushProperty, PincelDe(nivel));

        var pila = new StackPanel();

        var etiqueta = new TextBlock { Text = titulo, Margin = new Thickness(0) };
        etiqueta.SetResourceReference(StyleProperty, "Etiqueta");

        var numero = new TextBlock
        {
            Text = valor is { Length: > 0 } ? valor : "—",
            FontSize = 19,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 1, 0, 0),
        };

        if (nivel != NivelDeMedida.Normal)
        {
            numero.SetResourceReference(TextBlock.ForegroundProperty, PincelDe(nivel));
        }

        pila.Children.Add(etiqueta);
        pila.Children.Add(numero);

        if (detalle is { Length: > 0 })
        {
            var chico = new TextBlock
            {
                Text = detalle,
                TextTrimming = TextTrimming.CharacterEllipsis,
                FontSize = 11,
            };

            chico.SetResourceReference(StyleProperty, "Tenue");
            pila.Children.Add(chico);
        }

        marco.Child = pila;
        _metricas.Children.Add(marco);
    }

    private static string PincelDe(NivelDeMedida nivel) => nivel switch
    {
        NivelDeMedida.Critico => "MedidaCritica",
        NivelDeMedida.Advertencia => "MedidaAdvertencia",
        _ => "Borde",
    };

    private static string TiempoArriba(TimeSpan arriba) => arriba switch
    {
        { TotalSeconds: < 60 } => $"{(int)arriba.TotalSeconds} s",
        { TotalMinutes: < 60 } => $"{(int)arriba.TotalMinutes} min",
        { TotalHours: < 24 } => $"{arriba.Hours} h {arriba.Minutes} min",
        _ => $"{arriba.Days} d {arriba.Hours} h",
    };

    private void Datos(DetalleDeContenedor d)
    {
        _datos.Children.Clear();

        Titulo("Identidad", "IconoPanelDocker", "IconoCyan", primero: true);
        Dato("Imagen", d.Imagen);
        Dato("Digest de la imagen", d.Digest ?? "—");
        Dato("Identificador", d.Id ?? "—");
        Dato("Creado", d.Creado?.ToLocalTime().ToString("dd/MM/yyyy HH:mm") ?? "—");

        if (d.Proyecto is { Length: > 0 })
        {
            Dato(
                "Compose",
                d.Servicio is { Length: > 0 } ? $"{d.Proyecto} · {d.Servicio}" : d.Proyecto,
                "IconoVioleta");
        }

        Titulo(
            "Estado",
            d.Gravedad == GravedadDeContenedor.Corriendo ? "IconoOk" : "IconoAlerta",
            ColorDeGravedad(d.Gravedad));

        Dato("Estado", d.Estado, ColorDeGravedad(d.Gravedad));

        if (d.Salud is { Length: > 0 })
        {
            Dato("Salud", d.Salud, ColorDeGravedad(d.Gravedad));
        }

        Dato("Reinicios", d.Reinicios.ToString(), d.Reinicios > 0 ? "IconoAmbar" : null);
        Dato("Política de reinicio", d.Politica ?? "ninguna");
        Dato("Desde", d.Desde?.ToLocalTime().ToString("dd/MM/yyyy HH:mm") ?? "—");

        Titulo("Arranque", "IconoTerminalExterna", "IconoLima");
        Dato("Comando", d.Comando ?? "—");
        Dato("Directorio", d.Directorio ?? "—");

        Titulo("Consumo", "IconoPanelEstado", "IconoAzul");
        Dato("Red", d.Red);
        Dato("Disco", d.Disco);
        Dato("Procesos", d.Procesos);

        Titulo("Red", "IconoPanelPuertos", "IconoNaranja");
        Lista("Redes", d.Redes, "No está conectado a ninguna.");
        Lista("Puertos publicados", d.Puertos, "No publica ninguno.");

        Titulo("Almacenamiento", "IconoPanelArchivos", "IconoRosa");
        Lista("Volúmenes", d.Volumenes, "No monta ninguno.");
    }

    /// <summary>Encabezado de un bloque de la ficha: icono con color, y el nombre en mayúsculas pequeñas.</summary>
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
            Text = valor is { Length: > 0 } ? valor : "—",
            TextWrapping = TextWrapping.Wrap,
            FontFamily = (FontFamily)FindResource("FuenteMono"),
            FontSize = (double)FindResource("CuerpoChico"),
            Margin = new Thickness(0, 2, 0, 0),
        };

        if (pincel is not null)
        {
            texto.SetResourceReference(TextBlock.ForegroundProperty, pincel);
            texto.FontWeight = FontWeights.SemiBold;
        }

        pila.Children.Add(etiqueta);
        pila.Children.Add(texto);

        _datos.Children.Add(pila);
    }

    private void Lista(string titulo, IReadOnlyList<string> valores, string vacio)
    {
        Dato(titulo, valores.Count > 0 ? string.Join(Environment.NewLine, valores) : vacio);
    }

    private async void AlSeguir(object sender, RoutedEventArgs e)
    {
        if (_canalDeRegistro is not null)
        {
            await PararRegistroAsync().ConfigureAwait(true);
            return;
        }

        await IniciarRegistroAsync().ConfigureAwait(true);
    }

    /// <summary>Abre el canal y deja el resto en manos de sus dos delegados: uno agrega cada línea al cuadro a medida que llega, el otro avisa si el canal se cerró por su cuenta —el contenedor se detuvo, o la conexión se cortó— para que el botón deje de decir «Parar» cuando ya no hay nada que parar.</summary>
    private async Task IniciarRegistroAsync()
    {
        _seguir.IsEnabled = false;

        var cts = new CancellationTokenSource();
        _ctsRegistro = cts;

        try
        {
            var canal = await _control.SeguirRegistroAsync(
                _contenedor,
                linea => Dispatcher.BeginInvoke(() => AgregarLineaEnVivo(linea)),
                motivo => Dispatcher.BeginInvoke(() => RegistroCerrado(motivo)),
                cts.Token).ConfigureAwait(true);

            _registro.Document = new FlowDocument
            {
                PageWidth = 4000,
                FontFamily = _registro.FontFamily,
                FontSize = _registro.FontSize,
                PagePadding = new Thickness(0),
            };

            _lineas = 0;
            _ultimaLinea = null;
            _cuentaLineas.Text = "en vivo";
            _registroVacio.Visibility = Visibility.Collapsed;

            Declarar();
            _canalDeRegistro = canal;
            _seguir.Content = "Parar";
            _puntoEnVivo.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            _estado.Text = $"No se pudo abrir el registro en vivo: {ex.Message}";
            _estado.Foreground = (Brush)FindResource("Destructivo");
        }
        finally
        {
            _seguir.IsEnabled = true;
        }
    }

    /// <summary>Cierra el canal de registro en vivo, si hay uno abierto.</summary>
    private async Task PararRegistroAsync()
    {
        if (_canalDeRegistro is not { } canal)
        {
            return;
        }

        _canalDeRegistro = null;
        _ctsRegistro?.Cancel();
        _ctsRegistro?.Dispose();
        _ctsRegistro = null;

        await canal.DisposeAsync().ConfigureAwait(true);

        _seguir.Content = "Seguir en vivo";
        _puntoEnVivo.Visibility = Visibility.Collapsed;
    }

    /// <summary>Dibuja el registro de la última consulta, con cada línea en el color de su nivel (FR-100f).</summary>
    private void PintarRegistro(DetalleDeContenedor d)
    {
        var documento = new FlowDocument
        {
            PageWidth = 4000,
            FontFamily = _registro.FontFamily,
            FontSize = _registro.FontSize,
            PagePadding = new Thickness(0),
        };

        var lineas = d.Registro.Length > 0
            ? d.Registro.ReplaceLineEndings("\n").Split('\n')
            : [];

        foreach (var linea in lineas)
        {
            documento.Blocks.Add(Parrafo(linea));
        }

        _registro.Document = documento;
        _lineas = lineas.Length;
        _ultimaLectura = DateTimeOffset.Now;

        Declarar();

        var conError = lineas.Count(l => NivelDeLinea.De(l) == GravedadDeLinea.Error);

        if (conError > 0)
        {
            _errores += conError;
            Avisar($"El registro que se leyó trae {conError} línea(s) de error.");
        }

        _cuentaLineas.Text = lineas.Length switch
        {
            0 => string.Empty,
            1 => "1 línea",
            _ => $"{lineas.Length} líneas",
        };

        if (lineas.Length > 0)
        {
            _registroVacio.Visibility = Visibility.Collapsed;
            return;
        }

        _registroVacio.Visibility = Visibility.Visible;
        _registroVacio.Text = d.RegistroLeido
            ? "El contenedor no escribió nada en su registro."
            : "No se pudo leer el registro. Puede faltar un permiso, o el contenedor puede usar un "
              + "driver de registro que «docker logs» no lee. Probá «Seguir en vivo».";
    }

    private Paragraph Parrafo(string linea)
    {
        var run = new Run(linea)
        {
            Foreground = NivelDeLinea.De(linea) switch
            {
                GravedadDeLinea.Error => (Brush)FindResource("ConsolaError"),
                GravedadDeLinea.Advertencia => (Brush)FindResource("ConsolaAdvertencia"),
                _ => (Brush)FindResource("TextoConsola"),
            },
        };

        return new Paragraph(run) { Margin = new Thickness(0) };
    }

    private void AgregarLinea(string linea)
    {
        _registro.Document.Blocks.Add(Parrafo(linea));
        _registroVacio.Visibility = Visibility.Collapsed;

        _lineas++;
        _cuentaLineas.Text = $"{_lineas} líneas";

        _registro.ScrollToEnd();
    }

    /// <summary>Una línea que llegó del canal en vivo: además de dibujarla, hay que avisar si trae un error (FR-185c).</summary>
    private void AgregarLineaEnVivo(string linea)
    {
        AgregarLinea(linea);

        _ultimaLinea = DateTimeOffset.Now;
        Declarar();

        if (SeguimientoDeArchivo.Diagnostico(linea) is { Clase: ClaseDeAviso.Inaccesible } caido)
        {
            Avisar(caido.Texto);
            return;
        }

        if (NivelDeLinea.De(linea) != GravedadDeLinea.Error)
        {
            return;
        }

        _errores++;
        Avisar($"Apareció una línea de error en el registro ({_errores} desde que se abrió).");
    }

    private void RegistroCerrado(string? motivo)
    {
        _canalDeRegistro = null;
        _ctsRegistro?.Dispose();
        _ctsRegistro = null;

        _seguir.Content = "Seguir en vivo";
        _puntoEnVivo.Visibility = Visibility.Collapsed;

        var texto = motivo is { Length: > 0 }
            ? motivo
            : "Se cortó el canal del registro en vivo.";

        AgregarLinea($"« {texto} »");
        Avisar(texto);
    }

    private async void AlActualizar(object sender, RoutedEventArgs e) =>
        await CargarAsync().ConfigureAwait(true);

    /// <summary>Relee el registro del servidor y lo vuelve a dibujar, aunque se lo esté siguiendo en vivo (FR-185b).</summary>
    private async void AlForzarRegistro(object sender, RoutedEventArgs e)
    {
        var seguia = _canalDeRegistro is not null;

        await PararRegistroAsync().ConfigureAwait(true);

        _forzarRegistro.IsEnabled = false;

        var r = await _control.GetDetalleAsync(_contenedor).ConfigureAwait(true);

        _forzarRegistro.IsEnabled = true;

        if (!r.Success || r.Value is null)
        {
            var motivo = r.Error ?? "No se pudo releer el registro.";

            _estado.Text = motivo;
            _estado.Foreground = (Brush)FindResource("Destructivo");
            Avisar(motivo);
            return;
        }

        _detalle = r.Value;
        _ultimaLinea = null;
        PintarRegistro(r.Value);

        _estado.Text = $"Registro releído {DateTimeOffset.Now:HH:mm:ss}";
        _estado.Foreground = (Brush)FindResource("TextoTenue");

        if (seguia)
        {
            await IniciarRegistroAsync().ConfigureAwait(true);
        }
    }

    private async void AlReiniciar(object sender, RoutedEventArgs e) =>
        await EjecutarAsync(AccionDeContenedor.Reiniciar).ConfigureAwait(true);

    private async void AlDetener(object sender, RoutedEventArgs e) =>
        await EjecutarAsync(AccionDeContenedor.Detener).ConfigureAwait(true);

    private async void AlIniciar(object sender, RoutedEventArgs e) =>
        await EjecutarAsync(AccionDeContenedor.Iniciar).ConfigureAwait(true);

    private async Task EjecutarAsync(AccionDeContenedor accion)
    {
        var verbo = accion switch
        {
            AccionDeContenedor.Iniciar => "iniciar",
            AccionDeContenedor.Detener => "detener",
            _ => "reiniciar",
        };

        var Verbo = char.ToUpperInvariant(verbo[0]) + verbo[1..];

        var confirmado = Dialogos.Confirmar(
            this,
            $"¿{Verbo} el contenedor?",
            $"Se va a {verbo} «{_contenedor}» en {_servidor}.",
            Verbo);

        if (!confirmado)
        {
            return;
        }

        Ocupar(true);
        _estado.Text = $"{Verbo}ando «{_contenedor}»…";

        var r = await _control.EjecutarAsync(accion, _contenedor).ConfigureAwait(true);

        if (!r.Success)
        {
            Ocupar(false);
            _estado.Text = r.Error ?? "No se pudo.";
            _estado.Foreground = (Brush)FindResource("Destructivo");
            return;
        }

        Cambio = true;
        await CargarAsync().ConfigureAwait(true);
    }

    /// <summary>Si se ejecutó alguna acción, para que el panel se refresque al cerrar.</summary>
    public bool Cambio { get; private set; }
}
