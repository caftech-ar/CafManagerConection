using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using CafManagerConection.App.Bootstrap;
using CafManagerConection.App.Services;
using CafManagerConection.Domain.Settings;
using CafManagerConection.Infrastructure.Database;

namespace CafManagerConection.App.Views;

/// <summary>Preferencias, en pestañas: dónde vive todo, apariencia, etiquetas, importación, copias y ajustes avanzados (FR-156, FR-157).</summary>
[SupportedOSPlatform("windows")]
public partial class PreferenciasWindow : Window
{
    /// <summary>Una copia en la lista, con los textos ya armados.</summary>
    public sealed record Fila(string Cuando, string Tamano, string Archivo);

    /// <summary>Una credencial guardada. Nunca lleva el secreto: no hay campo donde ponerlo.</summary>
    public sealed record FilaCredencial(string Tipo, string Dueno, string Clave);

    private readonly CompositionRoot _root;
    private readonly ServicioDeCopias _servicio;
    private readonly Services.ActualizacionesService _actualizaciones;

    private AjustesDeCopia _ajustes = AjustesDeCopia.Default;
    private Infrastructure.Database.AjustesDeActualizacion _ajustesDeActualizacion = new();
    private AjustesDelArbol _ajustesDelArbol = new();
    private TerminalPreferences _terminalPrefs = TerminalPreferences.Default;
    private bool _cargando = true;

    public PreferenciasWindow(CompositionRoot root)
    {
        _root = root;
        _servicio = new ServicioDeCopias(root.Paths, root.Logger);
        _actualizaciones = new Services.ActualizacionesService(root.Settings, root.Logger);

        InitializeComponent();

        _rutaBase.Text = root.Paths.DatabasePath;
        _rutaLogs.Text = root.Paths.LogsDirectory;

        _panelEtiquetas.Inicializar(root);
        _panelImportacion.Inicializar(root);

        Loaded += async (_, _) => await CargarAsync().ConfigureAwait(true);
    }

    private async Task CargarAsync()
    {
        _cargando = true;

        _ajustes = await _root.AppSettings.GetBackupSettingsAsync().ConfigureAwait(true);

        _copiasActivas.IsChecked = _ajustes.Activas;
        _cuantas.Text = _ajustes.CuantasGuardar.ToString(
            System.Globalization.CultureInfo.CurrentCulture);

        _carpeta.Text = _servicio.CarpetaDe(_ajustes);

        _ajustesDeActualizacion = await _actualizaciones.ObtenerAjustesAsync().ConfigureAwait(true);
        _origenActualizaciones.Text = AjustesDeActualizacion.Repositorio;

        var tema = await _root.AppSettings.GetThemeAsync().ConfigureAwait(true);
        MarcarTema(tema);

        _ajustesDelArbol = await _root.AppSettings.GetTreeAppearanceAsync().ConfigureAwait(true);
        _mostrarHost.IsChecked = _ajustesDelArbol.MuestraHost;
        MostrarTamanoDelArbol();

        _terminalPrefs = await _root.AppSettings.GetTerminalPreferencesAsync().ConfigureAwait(true);
        CargarTipografias();
        _terminalTamano.Text = _terminalPrefs.FontSize.ToString(
            System.Globalization.CultureInfo.CurrentCulture);
        _terminalHistorial.Text = _terminalPrefs.ScrollbackLines.ToString(
            System.Globalization.CultureInfo.CurrentCulture);

        CargarAcercaDe();

        _cargando = false;
        Refrescar();

        await RefrescarCredencialesAsync().ConfigureAwait(true);
    }

    /// <summary>Lista las credenciales cmc:*, resueltas contra el árbol (FR-158).</summary>
    private async Task RefrescarCredencialesAsync()
    {
        try
        {
            var claves = await _root.Credentials
                .EnumerateKeysAsync("cmc:").ConfigureAwait(true);

            var conexiones = (await _root.ConnectionService.GetTreeAsync().ConfigureAwait(true))
                .ToDictionary(c => c.Id, c => c.Name);

            var carpetas = (await _root.FolderService.GetAllAsync().ConfigureAwait(true))
                .ToDictionary(f => f.Id, f => f.Name);

            var huerfanas = 0;
            var filas = new List<FilaCredencial>();

            foreach (var clave in claves)
            {
                var (tipo, dueno, resuelta) = Describir(clave, conexiones, carpetas);

                if (!resuelta)
                {
                    huerfanas++;
                }

                filas.Add(new FilaCredencial(tipo, dueno, clave));
            }

            _credenciales.ItemsSource = filas;

            _resumenCreds.Text = filas.Count switch
            {
                0 => "Todavía no guardaste ninguna.",
                _ => $"{filas.Count} guardada(s)."
                     + (huerfanas > 0
                        ? $" {huerfanas} quedó(aron) de conexiones que ya no existen: podés "
                          + "borrarlas desde el Administrador de Windows."
                        : string.Empty),
            };
        }
        catch (Exception ex)
        {
            _root.Logger.TechnicalError("listar las credenciales guardadas", ex);
            _resumenCreds.Text = "No se pudieron listar las credenciales.";
        }
    }

    private static (string Tipo, string Dueno, bool Resuelta) Describir(
        string clave,
        IReadOnlyDictionary<Guid, string> conexiones,
        IReadOnlyDictionary<Guid, string> carpetas)
    {
        var partes = clave.Split(':');

        if (partes.Length >= 4 && partes[1] == "folder")
        {
            var tipo = $"carpeta {partes[3]}";

            return Guid.TryParse(partes[2], out var idCarpeta)
                   && carpetas.TryGetValue(idCarpeta, out var nombreCarpeta)
                ? (tipo, nombreCarpeta, true)
                : (tipo, "— huérfana —", false);
        }

        if (partes.Length >= 3)
        {
            var tipo = partes[1].ToUpperInvariant();

            return Guid.TryParse(partes[2], out var id)
                   && conexiones.TryGetValue(id, out var nombre)
                ? (tipo, nombre, true)
                : (tipo, "— huérfana —", false);
        }

        return ("?", "—", false);
    }

    private void AlAbrirCredenciales(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("control.exe", "/name Microsoft.CredentialManager")
            {
                UseShellExecute = true,
            });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException)
        {
            _estado.Text = "No se pudo abrir el Administrador de credenciales.";
        }
    }

    private void Refrescar()
    {
        var copias = _servicio.Listar(_ajustes);

        MostrarCopias(copias);

        _estado.Text = copias.Count switch
        {
            0 => "Todavía no hay ninguna copia.",
            1 => "1 copia guardada.",
            _ => $"{copias.Count} copias guardadas.",
        };
    }

    private void MostrarCopias(IReadOnlyList<CopiaDeSeguridad> copias) =>
        _copias.ItemsSource = copias
            .Select(c => new Fila(
                c.Momento.ToLocalTime().ToString("dd/MM/yyyy HH:mm"),
                Tamano(c.Bytes),
                Path.GetFileName(c.Ruta)))
            .ToList();

    private static string Tamano(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:0} KB",
        _ => $"{bytes / (1024.0 * 1024):0.0} MB",
    };

    private async void AlCambiarAjuste(object sender, RoutedEventArgs e)
    {
        if (_cargando)
        {
            return;
        }

        await GuardarAsync().ConfigureAwait(true);
    }

    private async Task GuardarAsync()
    {
        var cuantas = int.TryParse(_cuantas.Text, out var n)
            ? n
            : AjustesDeCopia.Default.CuantasGuardar;

        _ajustes = new AjustesDeCopia(
            _copiasActivas.IsChecked == true,
            _ajustes.Carpeta,
            cuantas).Normalizados();

        await _root.AppSettings.SaveBackupSettingsAsync(_ajustes).ConfigureAwait(true);
        Refrescar();
    }

    private void AlEscribirNumero(object sender, System.Windows.Input.TextCompositionEventArgs e) =>
        e.Handled = !e.Text.All(char.IsAsciiDigit);

    private async void AlElegirCarpeta(object sender, RoutedEventArgs e)
    {
        var dialogo = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Dónde guardar las copias",
            InitialDirectory = Directory.Exists(_carpeta.Text) ? _carpeta.Text : null,
        };

        if (dialogo.ShowDialog(this) != true)
        {
            return;
        }

        _ajustes = _ajustes with { Carpeta = dialogo.FolderName };
        _carpeta.Text = _servicio.CarpetaDe(_ajustes);

        await GuardarAsync().ConfigureAwait(true);
    }

    private async void AlCopiarAhora(object sender, RoutedEventArgs e)
    {
        _estado.Text = "Copiando…";

        var r = await Task.Run(
            () => _servicio.CopiarAhora(_ajustes, DateTimeOffset.Now)).ConfigureAwait(true);

        Refrescar();

        _estado.Text = r.Hecha
            ? $"Copia hecha: {Path.GetFileName(r.Ruta)}"
            : r.Motivo ?? "No se pudo copiar.";
    }

    private async void AlExportar(object sender, RoutedEventArgs e)
    {
        var dialogo = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Exportar la base",
            Filter = "Base de CMC (*.db)|*.db|Todos los archivos (*.*)|*.*",
            DefaultExt = ".db",
            AddExtension = true,
            FileName = $"cmc-{DateTimeOffset.Now:yyyyMMdd-HHmm}.db",
        };

        if (dialogo.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            var destino = dialogo.FileName;

            await Task.Run(() => _servicio.Exportar(destino)).ConfigureAwait(true);

            _estado.Text = $"Exportada a {Path.GetFileName(destino)}";
        }
        catch (Exception ex)
        {
            _root.Logger.TechnicalError("exportar la base", ex);
            _estado.Text = "No se pudo exportar. El motivo quedó en el registro.";
        }
    }

    private void AlAbrirCarpetaBase(object sender, RoutedEventArgs e) =>
        Mostrar(_root.Paths.DatabasePath);

    private void AlAbrirCarpetaLogs(object sender, RoutedEventArgs e) =>
        Mostrar(_root.Paths.LogsDirectory);

    private void AlAbrirCarpetaCopias(object sender, RoutedEventArgs e) =>
        Mostrar(_carpeta.Text);

    private void Mostrar(string ruta)
    {
        try
        {
            if (File.Exists(ruta))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{ruta}\""));
                return;
            }

            if (!Directory.Exists(ruta))
            {
                _estado.Text = "Esa carpeta todavía no existe.";
                return;
            }

            Process.Start(new ProcessStartInfo(ruta) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException)
        {
            _estado.Text = "No se pudo abrir el Explorador.";
        }
    }

    private void AlCerrar(object sender, RoutedEventArgs e) => Close();

    private async void AlBuscarActualizaciones(object sender, RoutedEventArgs e)
    {
        _estadoActualizaciones.Text = "Buscando…";

        var resultado = await _actualizaciones.ComprobarAsync().ConfigureAwait(true);

        _ajustesDeActualizacion = await _actualizaciones.ObtenerAjustesAsync().ConfigureAwait(true);

        _estadoActualizaciones.Text = resultado switch
        {
            { Consultada: false } => resultado.Motivo ?? "No se pudo consultar.",
            { Release: null } => resultado.Motivo ?? "No se pudo consultar GitHub.",
            { HayVersionNueva: true, VersionDisponible: { } version } =>
                $"Hay una versión nueva: {version}. El aviso va a aparecer al reabrir CMC.",
            _ => $"Ya estás en la última versión ({Services.ActualizacionesService.VersionActual()}).",
        };
    }

    private void MarcarTema(AppTheme tema)
    {
        _temaClaro.IsChecked = tema == AppTheme.Light;
        _temaOscuro.IsChecked = tema == AppTheme.Dark;
        _temaSistema.IsChecked = tema == AppTheme.System;
    }

    private async void AlCambiarTema(object sender, RoutedEventArgs e)
    {
        if (_cargando)
        {
            return;
        }

        var tema = _temaOscuro.IsChecked == true ? AppTheme.Dark
            : _temaSistema.IsChecked == true ? AppTheme.System
            : AppTheme.Light;

        Temas.Aplicar(tema);
        await _root.AppSettings.SetThemeAsync(tema).ConfigureAwait(true);
    }

    private async void AlPersonalizarColores(object sender, RoutedEventArgs e)
    {
        var actuales = await _root.AppSettings.GetIconColorsAsync().ConfigureAwait(true);
        var ventana = new IconColorsWindow(_root, actuales) { Owner = Owner ?? this };

        if (ventana.ShowDialog() == true)
        {
            _estado.Text = "Color de los iconos actualizado";
        }
    }

    private void MostrarTamanoDelArbol()
    {
        if (_tamanoArbol.Items.Count == 0)
        {
            foreach (var escalon in AjustesDelArbol.Escalones)
            {
                _tamanoArbol.Items.Add(escalon.Nombre);
            }
        }

        _tamanoArbol.SelectedIndex = _ajustesDelArbol.IndiceDeEscalon();
    }

    private async void AlElegirTamanoDelArbol(object sender, SelectionChangedEventArgs e)
    {
        if (_cargando || _tamanoArbol.SelectedIndex < 0)
        {
            return;
        }

        _ajustesDelArbol = _ajustesDelArbol.ConEscalon(_tamanoArbol.SelectedIndex);

        await GuardarAparienciaDelArbolAsync().ConfigureAwait(true);
    }

    private async void AlCambiarAparienciaDelArbol(object sender, RoutedEventArgs e)
    {
        if (_cargando)
        {
            return;
        }

        _ajustesDelArbol = _ajustesDelArbol with { MuestraHost = _mostrarHost.IsChecked == true };
        await GuardarAparienciaDelArbolAsync().ConfigureAwait(true);
    }

    /// <summary>Guarda la apariencia del árbol y la aplica en el acto en la ventana de atrás.</summary>
    private async Task GuardarAparienciaDelArbolAsync()
    {
        await _root.AppSettings.SaveTreeAppearanceAsync(_ajustesDelArbol).ConfigureAwait(true);

        if (Owner is MainWindow principal)
        {
            principal.AplicarAjustesDelArbol(_ajustesDelArbol);
        }
    }

    private async void AlCambiarTerminal(object sender, TextChangedEventArgs e)
    {
        if (_cargando)
        {
            return;
        }

        var tamano = int.TryParse(_terminalTamano.Text, out var t) && t > 0
            ? t
            : _terminalPrefs.FontSize;

        var historial = int.TryParse(_terminalHistorial.Text, out var h) && h > 0
            ? h
            : _terminalPrefs.ScrollbackLines;

        _terminalPrefs = _terminalPrefs with { FontSize = tamano, ScrollbackLines = historial };
        await _root.AppSettings.SaveTerminalPreferencesAsync(_terminalPrefs).ConfigureAwait(true);
    }

    /// <summary>Las tipografías instaladas, con las que sirven para un terminal arriba.</summary>
    private IReadOnlyList<string> _tipografias = [];

    private void CargarTipografias()
    {
        _tipografias = TipografiasDisponibles.Ordenar(
            System.Windows.Media.Fonts.SystemFontFamilies.Select(f => f.Source),
            TipografiasDisponibles.PreferidasParaTerminal);

        MostrarTipografias();
    }

    private void AlBuscarFuente(object sender, TextChangedEventArgs e)
    {
        if (!_cargando)
        {
            MostrarTipografias();
        }
    }

    private void MostrarTipografias()
    {
        var visibles = TipografiasDisponibles.Buscar(_tipografias, _buscarFuente.Text);

        _fuentes.ItemsSource = visibles;
        _fuentes.SelectedItem = visibles.FirstOrDefault(
            f => string.Equals(f, _terminalPrefs.FontFamily, StringComparison.OrdinalIgnoreCase));

        MostrarLaElegida();
    }

    private void MostrarLaElegida()
    {
        var instalada = _tipografias.Any(
            f => string.Equals(f, _terminalPrefs.FontFamily, StringComparison.OrdinalIgnoreCase));

        // Decir que la guardada no esta instalada importa: si no, se ve una lista sin nada
        // marcado y parece que la preferencia se perdio.
        _fuenteElegida.Text = instalada
            ? $"Elegida: {_terminalPrefs.FontFamily}"
            : $"Elegida: {_terminalPrefs.FontFamily} — no está instalada en este equipo, "
              + "así que Windows va a usar un reemplazo.";
    }

    private async void AlElegirFuente(object sender, SelectionChangedEventArgs e)
    {
        if (_cargando || _fuentes.SelectedItem is not string elegida)
        {
            return;
        }

        _terminalPrefs = _terminalPrefs with { FontFamily = elegida };
        MostrarLaElegida();

        await _root.AppSettings.SaveTerminalPreferencesAsync(_terminalPrefs).ConfigureAwait(true);
    }

    private void CargarAcercaDe()
    {
        _acercaNombre.Text = "CafManagerConection";
        _acercaVersion.Text = $"Versión {VersionDeLaAplicacion.Corta}";

        // El logo azul marino sobre fondo oscuro da 1,09 a 1 de contraste; el mínimo legible es 3 a 1.
        var oscuro = Temas.EsOscuro;

        _logoAcercaColor.Visibility = oscuro ? Visibility.Collapsed : Visibility.Visible;
        _logoAcercaClaro.Visibility = oscuro ? Visibility.Visible : Visibility.Collapsed;
    }

    private void AlAbrirRepositorio(object sender, RoutedEventArgs e) =>
        Abrir("https://github.com/caftech-ar/CafManagerConection");

    private void AlAbrirWeb(object sender, RoutedEventArgs e) => Abrir("https://caftech.com.ar");

    private void AlCopiarVersion(object sender, RoutedEventArgs e)
    {
        var texto = $"CafManagerConection {VersionDeLaAplicacion.Corta}";

        try
        {
            System.Windows.Clipboard.SetText(texto);
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            // El portapapeles lo puede tener tomado otro proceso; no vale tirar la ventana.
        }
    }

    private void Abrir(string url)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
            when (ex is System.ComponentModel.Win32Exception or System.IO.IOException)
        {
            _root.Logger.TechnicalError($"abrir {url}", ex);
        }
    }
}
