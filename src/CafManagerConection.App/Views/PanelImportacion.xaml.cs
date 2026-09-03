using System.Globalization;
using System.IO;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using CafManagerConection.App.Bootstrap;
using CafManagerConection.Domain.Importacion;
using CafManagerConection.Infrastructure.Importacion;
using CafManagerConection.UseCases.Importacion;

namespace CafManagerConection.App.Views;

/// <summary>Trae conexiones guardadas de PuTTY, WinSCP y FileZilla (FR-182).</summary>
[SupportedOSPlatform("windows")]
public partial class PanelImportacion : UserControl
{
    /// <summary>Una conexión leída, como se ve en la vista previa. Nunca lleva la contraseña.</summary>
    public sealed class Fila(ConexionImportada conexion)
    {
        public bool Elegida { get; set; } = true;

        public string Ruta => conexion.Ruta;

        public string Host => conexion.Host;

        public string Usuario => conexion.Usuario ?? "—";

        public string Puerto => conexion.Puerto?.ToString(CultureInfo.CurrentCulture) ?? "—";

        public string Protocolo => conexion.ProtocoloOriginal;

        internal ConexionImportada Conexion => conexion;
    }

    private CompositionRoot? _root;

    /// <summary>Viva mientras se mira la vista previa: sus conexiones llevan la contraseña en memoria.</summary>
    private LecturaDeImportacion? _lectura;

    private string? _rutaDelIni;
    private string? _rutaDeFileZilla;
    private bool _cargando;

    public PanelImportacion() => InitializeComponent();

    /// <summary>Le da al panel lo que necesita; lo llama la ventana que lo aloja.</summary>
    public void Inicializar(CompositionRoot root)
    {
        _root = root;
        Loaded += AlCargarPorPrimeraVez;
    }

    private void AlCargarPorPrimeraVez(object sender, RoutedEventArgs e)
    {
        Loaded -= AlCargarPorPrimeraVez;

        if (Window.GetWindow(this) is { } ventana)
        {
            ventana.Closed += (_, _) => Desechar();
        }

        RevisarOrigenes();
        Mostrar();
    }

    /// <summary>Deja elegibles sólo los orígenes que hoy tienen algo que leer.</summary>
    private void RevisarOrigenes()
    {
        _cargando = true;

        var putty = ProbarRegistro(LectorDePutty.LeerRegistro, "PuTTY");
        var winScp = ProbarRegistro(LectorDeWinScp.LeerRegistro, "WinSCP");

        _rutaDelIni = RutaQueExiste(LectorDeWinScp.RutasHabitualesDelIni());
        _rutaDeFileZilla = RutaQueExiste([LectorDeFileZilla.RutaHabitual()]);

        Aplicar(_origenPutty, _detallePutty, putty.Disponible, putty.Detalle);
        Aplicar(_origenWinScpRegistro, _detalleWinScpRegistro, winScp.Disponible, winScp.Detalle);

        Aplicar(
            _origenWinScpIni,
            _detalleWinScpIni,
            _rutaDelIni is not null,
            _rutaDelIni ?? "No hay ningún WinSCP.ini en las rutas habituales.");

        Aplicar(
            _origenFileZilla,
            _detalleFileZilla,
            _rutaDeFileZilla is not null,
            _rutaDeFileZilla ?? $"No está: {LectorDeFileZilla.RutaHabitual()}");

        var primero = Radios().FirstOrDefault(r => r.IsEnabled);

        if (primero is not null)
        {
            primero.IsChecked = true;
        }

        _buscar.IsEnabled = primero is not null;

        _estado.Text = primero is null
            ? "No hay ningún origen para leer en esta máquina."
            : "Elegí un origen y tocá «Buscar».";

        _cargando = false;
    }

    private static void Aplicar(
        RadioButton opcion, TextBlock detalle, bool disponible, string texto)
    {
        opcion.IsEnabled = disponible;
        detalle.Text = texto;
        detalle.ToolTip = texto;
    }

    private RadioButton[] Radios() =>
        [_origenPutty, _origenWinScpRegistro, _origenWinScpIni, _origenFileZilla];

    private (bool Disponible, string Detalle) ProbarRegistro(
        Func<LecturaDeImportacion> leer, string programa)
    {
        try
        {
            using var lectura = leer();
            var cuantas = lectura.Compatibles.Count + lectura.Omitidas.Count;

            return cuantas == 0
                ? (false, $"No hay sesiones de {programa} guardadas en el registro.")
                : (true, $"El registro, bajo tu usuario: {Plural(cuantas, "sesión", "sesiones")}.");
        }
        catch (Exception ex)
        {
            _root?.Logger.TechnicalError($"revisar las sesiones de {programa}", ex);

            return (false, $"No se pudo leer el registro: {ex.Message}");
        }
    }

    private static string? RutaQueExiste(IEnumerable<string> candidatas)
    {
        foreach (var candidata in candidatas)
        {
            if (File.Exists(candidata))
            {
                return candidata;
            }
        }

        return null;
    }

    private OrigenDeImportacion? OrigenElegido() =>
        _origenPutty.IsChecked == true ? OrigenDeImportacion.Putty
        : _origenWinScpRegistro.IsChecked == true ? OrigenDeImportacion.WinScpRegistro
        : _origenWinScpIni.IsChecked == true ? OrigenDeImportacion.WinScpIni
        : _origenFileZilla.IsChecked == true ? OrigenDeImportacion.FileZilla
        : null;

    private void AlElegirOrigen(object sender, RoutedEventArgs e)
    {
        if (_cargando)
        {
            return;
        }

        Reemplazar(null);
        Mostrar();

        _estado.Text = "Tocá «Buscar» para leer este origen.";
    }

    private void AlBuscar(object sender, RoutedEventArgs e)
    {
        if (OrigenElegido() is not { } origen)
        {
            return;
        }

        Ocultar(_error);

        try
        {
            Reemplazar(Leer(origen));

            _estado.Text = $"Leído de {Nombre(origen)}.";
        }
        catch (Exception ex)
        {
            Reemplazar(null);
            Fallar($"leer las conexiones de {Nombre(origen)}", ex);
        }

        Mostrar();
    }

    private LecturaDeImportacion Leer(OrigenDeImportacion origen) => origen switch
    {
        OrigenDeImportacion.Putty => LectorDePutty.LeerRegistro(),
        OrigenDeImportacion.WinScpRegistro => LectorDeWinScp.LeerRegistro(),
        OrigenDeImportacion.WinScpIni when _rutaDelIni is { } ini =>
            LectorDeWinScp.LeerIni(File.ReadAllText(ini)),
        OrigenDeImportacion.FileZilla when _rutaDeFileZilla is { } xml =>
            LectorDeFileZilla.Leer(File.ReadAllText(xml)),
        _ => LecturaDeImportacion.Vacia,
    };

    private static string Nombre(OrigenDeImportacion origen) => origen switch
    {
        OrigenDeImportacion.Putty => "PuTTY",
        OrigenDeImportacion.WinScpRegistro => "WinSCP (registro)",
        OrigenDeImportacion.WinScpIni => "WinSCP.ini",
        _ => "FileZilla",
    };

    private void Reemplazar(LecturaDeImportacion? lectura)
    {
        _lectura?.Dispose();
        _lectura = lectura;
    }

    private void Desechar() => Reemplazar(null);

    private void Mostrar()
    {
        IReadOnlyList<ConexionImportada> compatibles = _lectura?.Compatibles ?? [];
        IReadOnlyList<ImportacionOmitida> omitidas = _lectura?.Omitidas ?? [];

        var filas = compatibles.Select(c => new Fila(c)).ToList();
        var conContrasena = _lectura?.ConContrasena ?? 0;

        _previa.ItemsSource = filas;

        _resumen.Text = filas.Count == 0
            ? "Todavía no hay nada que importar."
            : $"{Plural(filas.Count, "conexión compatible", "conexiones compatibles")}, "
              + $"{Plural(conContrasena, "trae contraseña guardada", "traen contraseña guardada")}.";

        _omitidas.ItemsSource = omitidas;
        _tituloOmitidas.Text = $"No entran ({omitidas.Count})";
        Ver(_bloqueOmitidas, omitidas.Count > 0);

        _traerContrasenas.IsEnabled = conContrasena > 0;

        _cuantasContrasenas.Text = (conContrasena, OrigenElegido()) switch
        {
            (0, OrigenDeImportacion.Putty) => "PuTTY no guarda contraseñas: no hay ninguna.",
            (0, _) => "Ninguna de las leídas trae contraseña guardada.",
            var (cuantas, _) => Plural(cuantas, "disponible", "disponibles") + ".",
        };

        Repasar();
    }

    private IReadOnlyList<ConexionImportada> Elegidas()
    {
        IEnumerable<Fila> filas = _previa.ItemsSource as IEnumerable<Fila> ?? [];

        return filas.Where(f => f.Elegida).Select(f => f.Conexion).ToList();
    }

    private void AlTildar(object sender, RoutedEventArgs e) => Repasar();

    private void Repasar()
    {
        var elegidas = Elegidas();

        _importar.IsEnabled = elegidas.Count > 0;

        var avisos = elegidas
            .SelectMany(c => c.AdvertenciasOVacio.Select(aviso => (Aviso: aviso, c.Ruta)))
            .GroupBy(par => par.Aviso, StringComparer.Ordinal)
            .Select(grupo => LineaDeAviso(grupo.Key, [.. grupo.Select(par => par.Ruta)]))
            .ToList();

        _advertencias.Text = string.Join(Environment.NewLine, avisos);
        Ver(_bloqueAdvertencias, avisos.Count > 0);
    }

    private static string LineaDeAviso(string aviso, IReadOnlyList<string> rutas) =>
        $"{aviso} ({Recortar(rutas, 3)})";

    private async void AlImportar(object sender, RoutedEventArgs e)
    {
        try
        {
            var elegidas = Elegidas();

            if (_root is not { } root
                || Window.GetWindow(this) is not { } ventana
                || elegidas.Count == 0)
            {
                return;
            }

            Ocultar(_error);

            _importar.IsEnabled = false;
            _buscar.IsEnabled = false;
            _estado.Text = "Importando…";

            var importador = new ImportadorDeConexiones(
                root.Folders, root.Connections, root.ConnectionService);

            var resultado = await importador
                .ImportarAsync(elegidas, _traerContrasenas.IsChecked == true)
                .ConfigureAwait(true);

            _estado.Text = $"Importadas: {resultado.Creadas} de {elegidas.Count}.";

            MessageWindow.Avisar(ventana, "Importar conexiones", Resumen(resultado));
        }
        catch (Exception ex)
        {
            Fallar("importar las conexiones", ex);
        }
        finally
        {
            _buscar.IsEnabled = OrigenElegido() is not null;
            Repasar();
        }
    }

    private static string Resumen(ResultadoDeImportacion resultado)
    {
        var partes = new List<string>
        {
            $"Conexiones creadas: {resultado.Creadas}.\n"
            + $"Carpetas creadas: {resultado.CarpetasCreadas}.\n"
            + $"Contraseñas guardadas: {resultado.ContrasenasGuardadas}.",
        };

        if (resultado.YaExistian.Count > 0)
        {
            partes.Add(
                $"Ya existían, así que no se tocaron ({resultado.YaExistian.Count}):\n"
                + Recortar(resultado.YaExistian, 8));
        }

        if (resultado.Fallidas.Count > 0)
        {
            partes.Add(
                $"No se pudieron crear ({resultado.Fallidas.Count}):\n"
                + Recortar(resultado.Fallidas, 8));
        }

        partes.Add("El árbol de conexiones muestra lo nuevo al reabrir la aplicación.");

        return string.Join("\n\n", partes);
    }

    private static string Recortar(IReadOnlyList<string> textos, int cuantas) =>
        textos.Count <= cuantas
            ? string.Join(", ", textos)
            : string.Join(", ", textos.Take(cuantas)) + $" y {textos.Count - cuantas} más";

    private static string Plural(int cuantas, string singular, string plural) =>
        cuantas == 1 ? $"1 {singular}" : $"{cuantas} {plural}";

    private static void Ver(UIElement elemento, bool visible) =>
        elemento.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;

    private static void Ocultar(UIElement elemento) => Ver(elemento, false);

    private void Fallar(string queSeIntentaba, Exception ex)
    {
        _root?.Logger.TechnicalError(queSeIntentaba, ex);

        _estado.Text = string.Empty;
        _error.Text = $"No se pudo {queSeIntentaba}: {ex.Message}";
        Ver(_error, true);
    }
}
