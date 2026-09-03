using System.Collections.ObjectModel;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using CafManagerConection.App.Bootstrap;
using CafManagerConection.App.Services;
using CafManagerConection.App.ViewModels;
using CafManagerConection.Domain.Connections;
using CafManagerConection.Domain.Sessions;
using CafManagerConection.Domain.Settings;
using CafManagerConection.UseCases.Abstractions;
using CafManagerConection.UseCases.Connections;
using CafManagerConection.UseCases.Sessions;

namespace CafManagerConection.App.Views;

/// <summary>Ventana principal: árbol de servidores a la izquierda, sesiones en pestañas a la derecha (FR-041).</summary>
[SupportedOSPlatform("windows")]
public partial class MainWindow : Window, ISessionHost
{
    private readonly CompositionRoot _root;
    private readonly ObservableCollection<NodoArbol> _raiz = [];
    private readonly ClipboardService _portapapeles;
    private readonly SessionManager _gestor;

    /// <summary>Resumen de cada conexion del arbol, por identificador.</summary>
    private readonly Dictionary<Guid, ConnectionSummary> _resumenes = [];

    /// <summary>Catálogo de etiquetas en orden, para el submenú del árbol (FR-190).</summary>
    private readonly List<Etiqueta> _etiquetas = [];

    /// <summary>Cómo estaba el árbol al cerrar la vez pasada. Se aplica una sola vez, en la primera carga; después manda lo que el usuario vaya abriendo y cerrando en esta sesión.</summary>
    private EstadoDelArbol? _estadoGuardado;

    private DispatcherTimer? _esperaBusqueda;
    private Point _origenArrastre;
    private NodoArbol? _arrastrando;

    /// <summary>Identificadores de conexión de las conexiones rápidas abiertas en esta ventana (FR-149).</summary>
    private readonly HashSet<Guid> _conexionesRapidas = [];

    /// <summary>Aviso de versión nueva (FR-159 a FR-162): qué se está ofreciendo y con qué ajustes.</summary>
    private readonly Services.ActualizacionesService _actualizaciones;
    private Infrastructure.Actualizaciones.InformacionDeRelease? _releaseDisponible;
    private Domain.Settings.VersionDeAplicacion? _versionDisponible;
    private Infrastructure.Database.AjustesDeActualizacion _ajustesDeActualizacion = new();

    public MainWindow(CompositionRoot root)
    {
        _root = root;
        _actualizaciones = new Services.ActualizacionesService(_root.Settings, _root.Logger);

        InitializeComponent();

        _arbol.ItemsSource = _raiz;

        _gestor = new SessionManager(
            _root.Sessions,
            (id, ct) => _root.ConnectionService.GetDetailAsync(id, ct),
            this,
            _root.Logger,
            _root.History);

        _portapapeles = new ClipboardService();
        _portapapeles.Copied += (_, mensaje) => _estado.Text = mensaje;

        _relojTransferencia = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1),
        };

        _relojTransferencia.Tick += (_, _) => MostrarTransferencia();
        _relojTransferencia.Start();

        _root.Sessions.Changed += (_, _) => Dispatcher.Invoke(() =>
        {
            _sesionesAbiertas.Text = _root.Sessions.Resumen;
            MarcarSesionesVivas();
        });

        Loaded += async (_, _) => await AlCargarAsync().ConfigureAwait(true);
        Closing += AlCerrar;
        PreviewKeyDown += AlPresionarTecla;
    }

    private async Task AlCargarAsync()
    {
        await AplicarTemaGuardadoAsync().ConfigureAwait(true);
        await RestaurarGeometriaAsync().ConfigureAwait(true);

        // Antes del arbol: si el vault se abre, las credenciales estan listas cuando la primera
        // conexion las pida, y la migracion corre una sola vez y con la ventana ya visible.
        await AbrirElVaultAsync().ConfigureAwait(true);

        _estadoGuardado = await _root.AppSettings.GetTreeStateAsync().ConfigureAwait(true);

        AplicarAjustesDelArbol(
            await _root.AppSettings.GetTreeAppearanceAsync().ConfigureAwait(true));

        await RefrescarArbolAsync().ConfigureAwait(true);

        if (_root.Startup.RecoveredFromCorruptionPath is { } preservada)
        {
            Dialogos.Informar(
                this,
                "Base de datos recuperada",
                "La base de datos anterior no se pudo leer. Se creó una nueva y la anterior "
                + $"quedó preservada en:{Environment.NewLine}{preservada}");
        }

        _ = _root.Herramientas.DetectarUnaVezAsync();
        _ = CopiaDeArranqueAsync();
        _ = LimpiarConexionesRapidasAsync();
        _ = ComprobarActualizacionAsync();

        _nombreYVersion.Text = $"CafManagerConection {VersionDeLaAplicacion.Corta}";

        _root.Logger.ApplicationStarted(VersionDeLaAplicacion.Corta);
    }

    private async Task AbrirElVaultAsync()
    {
        var aviso = await new AperturaDelVault(
            _root.Vault,
            _root.AdministradorDeWindows,
            _root.Logger,
            () => this).AbrirAsync().ConfigureAwait(true);

        if (aviso is { Length: > 0 })
        {
            _estado.Text = aviso;
        }
    }

    private async Task AplicarTemaGuardadoAsync()
    {
        var preferencia = await _root.AppSettings.GetThemeAsync().ConfigureAwait(true);
        var colores = await _root.AppSettings.GetIconColorsAsync().ConfigureAwait(true);

        Temas.Aplicar(preferencia);

        Temas.AplicarColoresDeIconos(colores);

        _botonTema.Content = Temas.Glifo(preferencia);
        AjustarLogoAlTema();
    }

    // El logo azul marino sobre el fondo oscuro da 1,09 a 1 de contraste; el mínimo legible es 3 a 1.
    /// <summary>Elige cuál de las dos versiones del logo se muestra, según el tema vigente.</summary>
    private void AjustarLogoAlTema()
    {
        var oscuro = Temas.EsOscuro;

        _logoColor.Visibility = oscuro ? Visibility.Collapsed : Visibility.Visible;
        _logoClaro.Visibility = oscuro ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Abre el sitio de CAFTech en el navegador del sistema.</summary>
    private void AlAbrirSitioDeCafTech(object sender, RoutedEventArgs e)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://caftech.com.ar",
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
            when (ex is System.ComponentModel.Win32Exception or System.IO.IOException)
        {
            _root.Logger.TechnicalError("abrir el sitio de CAFTech", ex);
        }
    }

    /// <summary>Repinta los iconos del árbol después de cambiar la paleta.</summary>
    public void RepintarIconos() => _arbol.Items.Refresh();

    /// <summary>Aplica el tamaño de letra y si se muestra el servidor, y repinta para que se note sin reabrir la aplicación.</summary>
    public void AplicarAjustesDelArbol(AjustesDelArbol ajustes)
    {
        var acotado = ajustes.Acotado();

        NodoArbol.AjusteDeTamano = acotado.AjusteDeTamano;
        NodoArbol.MuestraServidor = acotado.MuestraHost;

        RepintarIconos();
    }

    /// <summary>Rota entre claro, oscuro y acompañar a Windows, y lo recuerda.</summary>
    private async void AlCambiarTema(object sender, RoutedEventArgs e)
    {
        var siguiente = Temas.Siguiente();

        Temas.Aplicar(siguiente);
        RepintarIconos();
        AjustarLogoAlTema();

        _botonTema.Content = Temas.Glifo(siguiente);
        _estado.Text = Temas.Nombre(siguiente);

        await _root.AppSettings.SetThemeAsync(siguiente).ConfigureAwait(true);
    }

    /// <summary>Filtro rápido de arriba del árbol. Uno a la vez.</summary>
    private FiltroDelArbol _filtroRapido = FiltroDelArbol.Ninguno;

    private void AlAlternarFiltro(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Primitives.ToggleButton { Tag: string nombre }
            || !Enum.TryParse<FiltroDelArbol>(nombre, out var apretado))
        {
            return;
        }

        _filtroRapido = _filtroRapido.Alternar(apretado);

        // Uno a la vez: se marca el que quedo y se desmarcan los otros dos, incluido el que se
        // acaba de apretar cuando lo que hizo fue apagarse.
        _filtroFavoritas.IsChecked = _filtroRapido == FiltroDelArbol.Favoritas;
        _filtroSsh.IsChecked = _filtroRapido == FiltroDelArbol.Ssh;
        _filtroRdp.IsChecked = _filtroRapido == FiltroDelArbol.Rdp;

        _ = RefrescarArbolAsync(_busqueda.Text);
    }

    private async Task RefrescarArbolAsync(string? filtro = null)
    {
        var carpetas = await _root.FolderService.GetAllAsync().ConfigureAwait(true);

        _etiquetas.Clear();
        _etiquetas.AddRange(
            new CatalogoDeEtiquetas(await _root.Tags.GetAllAsync().ConfigureAwait(true)).Todas);

        var todas = string.IsNullOrWhiteSpace(filtro)
            ? await _root.ConnectionService.GetTreeAsync().ConfigureAwait(true)
            : await _root.ConnectionService.SearchAsync(filtro).ConfigureAwait(true);

        IReadOnlyList<ConnectionSummary> conexiones = _filtroRapido.Activo()
            ? [.. todas.Where(c => _filtroRapido.Admite(c.Protocol, c.IsFavorite))]
            : todas;

        var buscando = !string.IsNullOrWhiteSpace(filtro);

        var abiertas = _raiz
            .SelectMany(n => n.Recorrer())
            .Where(n => n.Expandido)
            .Select(n => n.Id)
            .ToHashSet();

        var seleccionado = (_arbol.SelectedItem as NodoArbol)?.Id;
        var primeraCarga = _raiz.Count == 0;

        _raiz.Clear();

        var porPadre = carpetas
            .GroupBy(f => f.ParentId)
            .ToDictionary(g => g.Key ?? Guid.Empty, g => g.OrderBy(f => f.SortOrder).ToList());

        var etiquetasDeCarpetas = EtiquetasDeCarpetas(carpetas);

        var presentes = conexiones.Select(c => c.Id).ToHashSet();

        foreach (var c in conexiones)
        {
            _resumenes[c.Id] = c;
        }

        // Con un filtro puesto, una carpeta que quedo sin nada adentro es ruido: el usuario pidio
        // ver sus favoritas, no las carpetas donde no tiene ninguna.
        var escondeVacias = _filtroRapido.Activo() || buscando;

        foreach (var nodo in Construir(porPadre, conexiones, presentes, null, escondeVacias))
        {
            if (!escondeVacias || TieneAlgunaConexion(nodo))
            {
                _raiz.Add(nodo);
            }
        }

        foreach (var c in conexiones
                     .Where(c => c.FolderId is null && EsRaizDeSuCarpeta(c, presentes))
                     .OrderBy(c => c.SortOrder))
        {
            _raiz.Add(NodoDeConexion(c, conexiones));
        }


        foreach (var nodo in _raiz.SelectMany(n => n.Recorrer()))
        {
            if (nodo.EsCarpeta)
            {
                nodo.EtiquetaPropia = etiquetasDeCarpetas.GetValueOrDefault(nodo.Id);
            }

            nodo.Expandido = buscando
                             || abiertas.Contains(nodo.Id)
                             || (primeraCarga && _estadoGuardado is { } guardado
                                 && guardado.CarpetasAbiertas.Contains(nodo.Id))
                             || (primeraCarga && _estadoGuardado is null && nodo.Padre is null);

            if (nodo.Id == seleccionado
                || (primeraCarga && seleccionado is null
                    && _estadoGuardado is { Seleccionado: { } elegida } && nodo.Id == elegida))
            {
                nodo.Seleccionado = true;
            }
        }

        MarcarSesionesVivas();

        // El motivo importa: un arbol vacio por el filtro se lee igual que uno vacio por haber
        // perdido las conexiones, y son dos sustos muy distintos.
        var porElFiltro = _filtroRapido.Descripcion();

        _contador.Text = conexiones.Count switch
        {
            0 when buscando => "Sin resultados",
            0 when porElFiltro.Length > 0 => $"Ninguna con el filtro: {porElFiltro}",
            0 => "Sin conexiones guardadas",
            1 when porElFiltro.Length > 0 => $"1 conexión · {porElFiltro}",
            1 => "1 conexión",
            var n when porElFiltro.Length > 0 => $"{n} conexiones · {porElFiltro}",
            var n => $"{n} conexiones",
        };
    }

    /// <summary>La etiqueta propia de cada carpeta que tenga una: la carpeta guarda el identificador y el árbol muestra el código (FR-190).</summary>
    private Dictionary<Guid, Etiqueta> EtiquetasDeCarpetas(IReadOnlyList<Folder> carpetas)
    {
        var porId = _etiquetas.ToDictionary(e => e.Id);
        var resultado = new Dictionary<Guid, Etiqueta>();

        foreach (var carpeta in carpetas)
        {
            if (carpeta.Settings.TagId is { } id && porId.TryGetValue(id, out var etiqueta))
            {
                resultado[carpeta.Id] = etiqueta;
            }
        }

        return resultado;
    }

    /// <summary>Pone en cada fila del árbol el estado de la sesión que tenga abierta.</summary>
    private void MarcarSesionesVivas()
    {
        var porConexion = _root.Sessions.ActiveSessions
            .GroupBy(s => s.ConnectionId)
            .ToDictionary(g => g.Key, g => Predominante([.. g.Select(s => s.State)]));

        foreach (var nodo in _raiz.SelectMany(n => n.Recorrer()))
        {
            nodo.EstadoSesion = nodo.EsCarpeta
                ? null
                : porConexion.TryGetValue(nodo.Id, out var estado) ? estado : null;
        }
    }

    /// <summary>Qué carpetas están desplegadas y qué fila elegida, ahora mismo.</summary>
    private EstadoDelArbol EstadoActualDelArbol()
    {
        var todos = _raiz.SelectMany(n => n.Recorrer()).ToList();

        return new EstadoDelArbol(
            [.. todos.Where(n => n.EsCarpeta && n.Expandido).Select(n => n.Id)],
            (_arbol.SelectedItem as NodoArbol)?.Id);
    }

    private static SessionState Predominante(IReadOnlyCollection<SessionState> estados)
    {
        if (estados.Contains(SessionState.Connecting))
        {
            return SessionState.Connecting;
        }

        if (estados.Contains(SessionState.Error))
        {
            return SessionState.Error;
        }

        if (estados.Contains(SessionState.Connected))
        {
            return SessionState.Connected;
        }

        return SessionState.Disconnected;
    }

    /// <summary>Si en toda la rama hay al menos una conexión. Una carpeta que sólo contiene carpetas vacías también está vacía.</summary>
    private static bool TieneAlgunaConexion(NodoArbol nodo) =>
        nodo.Recorrer().Any(n => !n.EsCarpeta);

    private static IEnumerable<NodoArbol> Construir(
        Dictionary<Guid, List<Folder>> porPadre,
        IReadOnlyList<ConnectionSummary> conexiones,
        HashSet<Guid> presentes,
        Guid? padre,
        bool escondeVacias = false)
    {
        if (!porPadre.TryGetValue(padre ?? Guid.Empty, out var hijas))
        {
            yield break;
        }

        foreach (var carpeta in hijas)
        {
            var nodo = NodoArbol.Carpeta(carpeta);

            foreach (var sub in Construir(porPadre, conexiones, presentes, carpeta.Id, escondeVacias))
            {
                if (!escondeVacias || TieneAlgunaConexion(sub))
                {
                    nodo.Agregar(sub);
                }
            }

            foreach (var c in conexiones
                         .Where(c => c.FolderId == carpeta.Id && EsRaizDeSuCarpeta(c, presentes))
                         .OrderBy(c => c.SortOrder))
            {
                nodo.Agregar(NodoDeConexion(c, conexiones));
            }

            yield return nodo;
        }
    }

    /// <summary>Indica si la conexión se muestra al nivel de su carpeta, en vez de colgando de otra.</summary>
    private static bool EsRaizDeSuCarpeta(ConnectionSummary c, HashSet<Guid> presentes) =>
        c.ParentConnectionId is not { } padre || !presentes.Contains(padre);

    /// <summary>Arma el nodo de una conexión con los servicios que cuelgan de ella.</summary>
    private static NodoArbol NodoDeConexion(
        ConnectionSummary c, IReadOnlyList<ConnectionSummary> conexiones)
    {
        var nodo = NodoArbol.Conectable(c);

        foreach (var hija in conexiones
                     .Where(h => h.ParentConnectionId == c.Id)
                     .OrderBy(h => h.SortOrder))
        {
            nodo.Agregar(NodoArbol.Conectable(hija));
        }

        return nodo;
    }

    private NodoArbol? Elegido => _arbol.SelectedItem as NodoArbol;

    private void AlCambiarSeleccion(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (Elegido is { Conexion: { } c })
        {
            _estado.Text = string.IsNullOrEmpty(c.EffectiveUserName)
                ? c.Host
                : $"{c.EffectiveUserName}@{c.Host}";
        }
    }

    private void AlDobleClicEnArbol(object sender, MouseButtonEventArgs e)
    {
        if (Elegido is { EsCarpeta: false, Conexion: { } conexion })
        {
            AbrirSesion(conexion);
            e.Handled = true;
        }
    }

    private void AlPresionarTeclaEnArbol(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter when Elegido is { EsCarpeta: false, Conexion: { } c }:
                AbrirSesion(c);
                e.Handled = true;
                break;

            case Key.F2 when Elegido is { } nodo:
                _ = EditarAsync(nodo);
                e.Handled = true;
                break;

            case Key.Delete when Elegido is { } nodo:
                _ = EliminarAsync(nodo);
                e.Handled = true;
                break;
        }
    }

    private void AlEscribirBusqueda(object sender, TextChangedEventArgs e)
    {
        _esperaBusqueda?.Stop();

        _esperaBusqueda ??= new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250),
        };

        _esperaBusqueda.Tick -= AlVencerEspera;
        _esperaBusqueda.Tick += AlVencerEspera;
        _esperaBusqueda.Start();
    }

    private async void AlVencerEspera(object? sender, EventArgs e)
    {
        _esperaBusqueda?.Stop();
        await RefrescarArbolAsync(_busqueda.Text).ConfigureAwait(true);
    }

    private void AbrirSesion(ConnectionSummary conexion, bool forzarNueva = false)
    {
        if (conexion.Protocol == Protocol.Web)
        {
            AbrirWeb(conexion);
            return;
        }

        _resumenes[conexion.Id] = conexion;
        _ = AbrirSesionAsync(conexion, forzarNueva);
    }

    private async Task AbrirSesionAsync(ConnectionSummary conexion, bool forzarNueva)
    {
        var r = await _gestor.OpenAsync(conexion.Id, forzarNueva).ConfigureAwait(true);

        if (!r.Success)
        {
            _estado.Text = r.ErrorMessage ?? "No se pudo abrir la sesión.";
            await RefrescarArbolAsync().ConfigureAwait(true);
        }
    }

    /// <summary>Construye la pestaña de una sesión y la muestra, sin conectarla.</summary>
    ISessionSurface ISessionHost.Create(Guid sessionId, ConnectionRecord connection)
    {
        var nombre = connection.Connection.Name;
        var vista = new SessionView(_root, connection);
        var pestana = new TabItem { Content = vista, Tag = sessionId };

        pestana.Header = CabeceraDePestana(nombre, SessionState.Connecting, pestana);

        vista.PidioConsola += (_, _) => AlternarConsola();
        vista.Informo += (_, mensaje) => _estado.Text = mensaje;

        vista.StateChanged += (_, cambio) =>
        {
            pestana.Header = CabeceraDePestana(nombre, cambio.State, pestana);

            if (ReferenceEquals(_sesiones.SelectedItem, pestana))
            {
                ActualizarTitulo();

                if (_resumenes.TryGetValue(connection.Connection.Id, out var resumen))
                {
                    MostrarEstado(resumen, cambio);
                }
            }
        };

        _sesiones.Items.Add(pestana);
        _sesiones.SelectedItem = pestana;
        ActualizarTitulo();

        _vacio.Visibility = Visibility.Collapsed;
        _sesiones.Visibility = Visibility.Visible;

        return new PestanaDeSesion(this, pestana, vista);
    }

    /// <summary>Asa que le da al núcleo control sobre una pestaña, y nada más.</summary>
    private sealed class PestanaDeSesion(MainWindow ventana, TabItem pestana, SessionView vista)
        : ISessionSurface
    {
        public SessionState State => vista.State;

        public event EventHandler<SessionStateChanged>? StateChanged
        {
            add => vista.StateChanged += value;
            remove => vista.StateChanged -= value;
        }

        public Task ConnectAsync(CancellationToken ct = default) => vista.ConnectAsync();

        public void Activate() => ventana._sesiones.SelectedItem = pestana;

        public void Dispose()
        {
            try
            {
                vista.Dispose();
            }
            finally
            {
                ventana._sesiones.Items.Remove(pestana);
                ventana.AjustarVacio();
                ventana.ActualizarTitulo();
            }
        }
    }

    /// <summary>Muestra el cartel de «sin sesiones» cuando no queda ninguna pestaña.</summary>
    private void AjustarVacio()
    {
        if (_sesiones.Items.Count > 0)
        {
            return;
        }

        _sesiones.Visibility = Visibility.Collapsed;
        _vacio.Visibility = Visibility.Visible;
        _estado.Text = "Listo";
        _puntoEstado.Visibility = Visibility.Collapsed;
    }

    /// <summary>Cabecera de la pestaña: punto de estado, título y botón de cierre.</summary>
    private object CabeceraDePestana(string titulo, SessionState estado, TabItem pestana)
    {
        var fila = new StackPanel { Orientation = Orientation.Horizontal };

        var esRapida = ConexionDeLaPestana(pestana) is { } conexionId
            && _conexionesRapidas.Contains(conexionId);

        fila.ContextMenu = MenuDePestana(pestana);

        fila.Children.Add(new System.Windows.Shapes.Ellipse
        {
            Width = 7,
            Height = 7,
            Margin = new Thickness(0, 0, 7, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Fill = (System.Windows.Media.Brush)new Themes.ColorDeEstado()
                .Convert(estado, typeof(object), null, System.Globalization.CultureInfo.CurrentCulture),
        });

        fila.Children.Add(new TextBlock
        {
            Text = esRapida ? $"⚡ {titulo}" : titulo,
            ToolTip = esRapida ? "Conexión rápida: no está guardada en el árbol" : null,
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
            ToolTip = "Cerrar sesión (Ctrl+W)",
        };

        cerrar.Click += (_, ev) =>
        {
            CerrarPestana(pestana);
            ev.Handled = true;
        };

        fila.Children.Add(cerrar);
        return fila;
    }

    private void AlCambiarDeSesion(object sender, SelectionChangedEventArgs e)
    {
        if (!ReferenceEquals(e.OriginalSource, _sesiones))
        {
            return;
        }

        _estado.Text = _sesiones.Items.Count == 0 ? "Listo" : _estado.Text;
        ActualizarTitulo();
    }

    /// <summary>Ancho de la lateral cuando está a la vista. Se guarda para poder volver.</summary>
    private double _anchoDeLaLateral = 270;

    /// <summary>Ctrl+B esconde o trae el árbol. Es manual y vale igual para RDP y para SSH: ninguna sesión lo mueve sola.</summary>
    private void AlternarLateral() => Lateral(_lateral.Visibility != Visibility.Visible);

    private void Lateral(bool visible)
    {
        if (visible == (_lateral.Visibility == Visibility.Visible))
        {
            return;
        }

        if (!visible)
        {
            _anchoDeLaLateral = _columnaLateral.ActualWidth > 0
                ? _columnaLateral.ActualWidth
                : _anchoDeLaLateral;
        }

        _lateral.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        _divisorLateral.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;

        // El MinWidth hay que bajarlo primero: con 200 puesto, un ancho de 0 no se aplica.
        _columnaLateral.MinWidth = visible ? 200 : 0;
        _columnaLateral.Width = visible
            ? new GridLength(_anchoDeLaLateral)
            : new GridLength(0);
    }

    /// <summary>El título dice qué sesión está activa y cuántas hay abiertas (FR-041a).</summary>
    private void ActualizarTitulo()
    {
        var activa = _sesiones.SelectedItem as TabItem;
        var vista = activa?.Content as SessionView;

        Title = Services.TituloDeVentana.Componer(
            vista?.Nombre,
            vista?.State ?? SessionState.Disconnected,
            _sesiones.Items.Count,
            Services.VersionDeLaAplicacion.Corta);
    }

    /// <summary>Menú de una pestaña: cerrar esta, las demás, o todas.</summary>
    private ContextMenu MenuDePestana(TabItem pestana)
    {
        var menu = new ContextMenu();

        void Agregar(string texto, Action accion)
        {
            var item = new MenuItem { Header = texto };
            item.Click += (_, _) => accion();
            menu.Items.Add(item);
        }

        if (ConexionDeLaPestana(pestana) is { } conexionId && _conexionesRapidas.Contains(conexionId))
        {
            Agregar("Guardar esta conexión…", () => _ = GuardarConexionRapidaAsync(pestana, conexionId));
            menu.Items.Add(new Separator());
        }

        Agregar("Duplicar esta sesión", () => DuplicarSesion(pestana));
        menu.Items.Add(new Separator());
        Agregar("Cerrar", () => CerrarPestana(pestana));
        Agregar("Cerrar las demás", () => CerrarOtrasPestanas(pestana));
        menu.Items.Add(new Separator());
        Agregar("Cerrar todas", () => CerrarTodasLasPestanas());

        return menu;
    }

    /// <summary>Identificador de conexión de la sesión que muestra esta pestaña, si la hay.</summary>
    private Guid? ConexionDeLaPestana(TabItem pestana) =>
        pestana.Tag is Guid idSesion
            ? _root.Sessions.ActiveSessions.FirstOrDefault(s => s.SessionId == idSesion)?.ConnectionId
            : null;

    /// <summary>Abre una segunda sesión sobre la misma conexión (FR-146).</summary>
    private void DuplicarSesion(TabItem pestana)
    {
        if (pestana.Tag is not Guid idSesion)
        {
            return;
        }

        var abierta = _root.Sessions.ActiveSessions
            .FirstOrDefault(s => s.SessionId == idSesion);

        if (abierta is null)
        {
            return;
        }

        _ = _gestor.OpenAsync(abierta.ConnectionId, forceNew: true);
    }

    private void CerrarOtrasPestanas(TabItem conservar)
    {
        var otras = _sesiones.Items.OfType<TabItem>()
            .Where(t => !ReferenceEquals(t, conservar))
            .ToList();

        if (otras.Count == 0)
        {
            return;
        }

        if (!ConfirmarCierreMasivo(otras.Count))
        {
            return;
        }

        foreach (var t in otras)
        {
            CerrarPestana(t);
        }
    }

    private void CerrarTodasLasPestanas()
    {
        var todas = _sesiones.Items.OfType<TabItem>().ToList();

        if (todas.Count == 0 || !ConfirmarCierreMasivo(todas.Count))
        {
            return;
        }

        foreach (var t in todas)
        {
            CerrarPestana(t);
        }
    }

    /// <summary>Confirma antes de cerrar varias sesiones de una vez (FR-048).</summary>
    private bool ConfirmarCierreMasivo(int cuantas) =>
        cuantas <= 1 ||
        Dialogos.Confirmar(
            this,
            "Cerrar sesiones",
            cuantas == 2
                ? "Se van a cerrar 2 sesiones abiertas."
                : $"Se van a cerrar {cuantas} sesiones abiertas.",
            "Cerrar");

    /// <summary>La rueda del mouse desplaza el carril de pestañas.</summary>
    private void AlRodarSobrePestanas(object sender, MouseWheelEventArgs e)
    {
        if (BuscarCarril(e.OriginalSource as DependencyObject) is not { } carril)
        {
            return;
        }

        carril.ScrollToHorizontalOffset(carril.HorizontalOffset - e.Delta);
        e.Handled = true;
    }

    private static ScrollViewer? BuscarCarril(DependencyObject? origen)
    {
        while (origen is not null)
        {
            if (origen is ScrollViewer sv && sv.Name == "_carril")
            {
                return sv;
            }

            origen = System.Windows.Media.VisualTreeHelper.GetParent(origen);
        }

        return null;
    }

    private System.Windows.Threading.DispatcherTimer? _relojTransferencia;

    /// <summary>Muestra lo transferido en la sesión que se está mirando.</summary>
    private void MostrarTransferencia()
    {
        MostrarTiempoDeSesion();

        if (_sesiones.SelectedItem is not TabItem { Content: SessionView vista } ||
            vista.Transferencia is not { } t)
        {
            _transferencia.Visibility = Visibility.Collapsed;
            return;
        }

        _transferencia.Text = $"↓ {Tamano(t.Recibidos)}   ↑ {Tamano(t.Enviados)}";
        _transferencia.Visibility = Visibility.Visible;
    }

    /// <summary>Cuánto tardó en abrir la sesión activa, a qué hora abrió y hace cuánto. Se cuelga del mismo reloj que la transferencia, así el «hace N minutos» avanza solo.</summary>
    private void MostrarTiempoDeSesion()
    {
        var texto = _sesiones.SelectedItem is TabItem { Content: SessionView vista }
            ? Services.TiempoDeSesion.Componer(
                vista.TardoEnAbrir, vista.AbiertaA, DateTimeOffset.Now)
            : string.Empty;

        _tiempoDeSesion.Text = texto;

        _tiempoDeSesion.Visibility = texto.Length == 0
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    /// <summary>Tamaño legible: sin decimales en bytes y KB, con uno de MB para arriba.</summary>
    private static string Tamano(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:0} KB",
        < 1024L * 1024 * 1024 => $"{bytes / (1024.0 * 1024):0.0} MB",
        _ => $"{bytes / (1024.0 * 1024 * 1024):0.00} GB",
    };

    private void CerrarPestana(TabItem pestana)
    {
        if (pestana.Tag is not Guid idSesion)
        {
            return;
        }

        var conexionId = ConexionDeLaPestana(pestana);

        _gestor.Close(idSesion);

        if (conexionId is { } id && _conexionesRapidas.Remove(id))
        {
            _ = BorrarConexionRapidaAsync(id);
        }
    }

    /// <summary>Borra del repositorio la conexión que sostenía una sesión rápida ya cerrada (FR-149).</summary>
    private async Task BorrarConexionRapidaAsync(Guid id)
    {
        var r = await _root.ConnectionService.DeleteAsync(id).ConfigureAwait(true);

        if (!r.Success)
        {
            _root.Logger.TechnicalError(
                "borrar la conexión rápida al cerrar su sesión",
                new InvalidOperationException(r.ErrorMessage));
        }
    }

    private void CerrarPestanaActual()
    {
        if (_sesiones.SelectedItem is TabItem pestana)
        {
            CerrarPestana(pestana);
        }
    }

    private void MostrarEstado(ConnectionSummary conexion, SessionStateChanged cambio)
    {
        var destino = string.IsNullOrEmpty(conexion.EffectiveUserName)
            ? conexion.Host
            : $"{conexion.EffectiveUserName}@{conexion.Host}";

        _estado.Text = cambio.State switch
        {
            SessionState.Connected => $"Conectado · {destino}",
            SessionState.Connecting => $"Conectando a {destino}…",
            SessionState.Error => $"Error · {cambio.Failure?.UserMessage ?? "sin detalle"}",
            _ => $"Desconectado · {destino}",
        };

        var recurso = cambio.State switch
        {
            SessionState.Connected => "EstadoConectado",
            SessionState.Connecting => "EstadoConectando",
            SessionState.Error => "EstadoError",
            _ => "EstadoInactivo",
        };

        _puntoEstado.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, recurso);
        _puntoEstado.Visibility = Visibility.Visible;
    }

    /// <summary>Copia de seguridad al arrancar, si corresponde (FR-156).</summary>
    private async Task CopiaDeArranqueAsync()
    {
        try
        {
            var ajustes = await _root.AppSettings.GetBackupSettingsAsync().ConfigureAwait(true);

            var r = await Task.Run(() => new Infrastructure.Database.ServicioDeCopias(
                _root.Paths, _root.Logger).CopiarSiCorresponde(ajustes, DateTimeOffset.Now))
                .ConfigureAwait(true);

            if (r.Hecha)
            {
                _estado.Text = $"Copia de seguridad hecha: {System.IO.Path.GetFileName(r.Ruta)}";
            }
        }
        catch (Exception ex)
        {
            _root.Logger.TechnicalError("hacer la copia de seguridad al arrancar", ex);
        }
    }

    /// <summary>Barre las conexiones rápidas que sobrevivieron a una corrida anterior.</summary>
    private async Task LimpiarConexionesRapidasAsync()
    {
        try
        {
            await _root.ConnectionService.LimpiarConexionesRapidasAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _root.Logger.TechnicalError("limpiar las conexiones rápidas huérfanas", ex);
        }
    }

    private ConsolaDeTraza? _consolaDeTraza;
    private double _altoConsola = 240;

    /// <summary>Abre en el Explorador la carpeta donde la aplicación escribe sus registros.</summary>
    private void AbrirCarpetaDeRegistros()
    {
        try
        {
            System.IO.Directory.CreateDirectory(_root.Paths.LogsDirectory);

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                _root.Paths.LogsDirectory) { UseShellExecute = true });
        }
        catch (Exception ex)
            when (ex is System.ComponentModel.Win32Exception or System.IO.IOException
                     or UnauthorizedAccessException)
        {
            _root.Logger.TechnicalError("abrir la carpeta de registros", ex);
        }
    }

    private void AlternarConsola()
    {
        if (_consola.Content is null)
        {
            if (_consolaDeTraza is null)
            {
                _consolaDeTraza = new ConsolaDeTraza();

                _consolaDeTraza.PidioCerrar += (_, _) => AlternarConsola();
                _consolaDeTraza.PidioAbrirRegistros += (_, _) => AbrirCarpetaDeRegistros();
            }

            _consolaDeTraza.Enganchar(_root.Trazas);

            _consola.Content = _consolaDeTraza;
            _filaConsola.Height = new GridLength(_altoConsola);
            _divisorConsola.Visibility = Visibility.Visible;
            _botonConsola.IsChecked = true;

            return;
        }

        if (_filaConsola.ActualHeight > 0)
        {
            _altoConsola = _filaConsola.ActualHeight;
        }

        _consolaDeTraza?.Desenganchar();
        _consola.Content = null;
        _filaConsola.Height = new GridLength(0);
        _divisorConsola.Visibility = Visibility.Collapsed;
        _botonConsola.IsChecked = false;
    }

    private void AlAlternarConsola(object sender, RoutedEventArgs e) => AlternarConsola();

    private void AlPresionarTecla(object sender, KeyEventArgs e)
    {
        var control = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);

        switch (e.Key)
        {
            case Key.F when control:
                _busqueda.Focus();
                _busqueda.SelectAll();
                e.Handled = true;
                break;

            case Key.N when control:
                _ = NuevaConexionAsync();
                e.Handled = true;
                break;

            case Key.K when control:
                _ = ConectarRapidoAsync();
                e.Handled = true;
                break;

            case Key.W when control:
                CerrarPestanaActual();
                e.Handled = true;
                break;

            case Key.B when control:
                AlternarLateral();
                e.Handled = true;
                break;

            case Key.Tab when control && _sesiones.Items.Count > 1:
                _sesiones.SelectedIndex = (_sesiones.SelectedIndex + 1) % _sesiones.Items.Count;
                e.Handled = true;
                break;

            case Key.F12:
                AlternarConsola();
                e.Handled = true;
                break;

            case Key.F11:
                WindowState = WindowState == WindowState.Maximized
                    ? WindowState.Normal
                    : WindowState.Maximized;
                e.Handled = true;
                break;
        }
    }

    /// <summary>Nombre con el que se recuerda el ancho del árbol.</summary>
    private const string AnchoDelArbol = "arbol";

    private async Task RestaurarGeometriaAsync()
    {
        var g = await _root.AppSettings.GetWindowPlacementAsync().ConfigureAwait(true);

        Width = g.Width;
        Height = g.Height;

        var anchos = await _root.AppSettings.GetPanelWidthsAsync().ConfigureAwait(true);

        if (anchos.TryGetValue(AnchoDelArbol, out var ancho)
            && ancho >= _columnaLateral.MinWidth)
        {
            _columnaLateral.Width = new GridLength(ancho);
        }

        var pantallas = System.Windows.Forms.Screen.AllScreens
            .Select(s => new Domain.Settings.AreaDePantalla(
                s.WorkingArea.X, s.WorkingArea.Y, s.WorkingArea.Width, s.WorkingArea.Height))
            .ToList();

        var visible = g.EsVisibleEn(pantallas);

        if (visible && g.X != 0 && g.Y != 0)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = g.X;
            Top = g.Y;
        }
        else
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        if (g.Maximized)
        {
            WindowState = WindowState.Maximized;
        }
    }

    /// <summary>Si el cierre ya guardó lo suyo y esta vez va en serio.</summary>
    private bool _cerrandoDeVerdad;

    private async void AlCerrar(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_cerrandoDeVerdad)
        {
            return;
        }

        var tunelesActivos = 0;

        foreach (var vista in _sesiones.Items.OfType<TabItem>()
                     .Select(t => t.Content).OfType<SessionView>())
        {
            tunelesActivos += await vista.ContarTunelesActivosAsync().ConfigureAwait(true);
        }

        if (_sesiones.Items.Count > 0 || tunelesActivos > 0)
        {
            var confirmado = Dialogos.Confirmar(
                this,
                "Cerrar la aplicación",
                TextoDeAvisoDeCierre(_sesiones.Items.Count, tunelesActivos),
                "Cerrar igual");

            if (!confirmado)
            {
                e.Cancel = true;
                return;
            }
        }

        e.Cancel = true;

        try
        {
            _root.Logger.ApplicationStopping(_sesiones.Items.Count);

            var maximizada = WindowState == WindowState.Maximized;
            var caja = maximizada ? RestoreBounds : new Rect(Left, Top, Width, Height);

            await _root.AppSettings.SaveWindowPlacementAsync(new Domain.Settings.WindowPlacement(
                (int)caja.X, (int)caja.Y, (int)caja.Width, (int)caja.Height, maximizada))
                .ConfigureAwait(true);

            await _root.AppSettings.SaveTreeStateAsync(EstadoActualDelArbol()).ConfigureAwait(true);

            // El divisor volvía a los 270 del XAML en cada arranque.
            if (_columnaLateral.ActualWidth > 0)
            {
                await _root.AppSettings
                    .SavePanelWidthAsync(AnchoDelArbol, _columnaLateral.ActualWidth)
                    .ConfigureAwait(true);
            }
        }
        catch (Exception ex)
        {
            _root.Logger.TechnicalError("guardar el estado de la ventana", ex);
        }

        _gestor.CloseAll();

        foreach (var id in _conexionesRapidas)
        {
            var r = await _root.ConnectionService.DeleteAsync(id).ConfigureAwait(true);

            if (!r.Success)
            {
                _root.Logger.TechnicalError(
                    "borrar una conexión rápida al cerrar la aplicación",
                    new InvalidOperationException(r.ErrorMessage));
            }
        }

        _conexionesRapidas.Clear();

        _portapapeles.Dispose();

        _cerrandoDeVerdad = true;

        _ = Dispatcher.BeginInvoke(Close);
    }

    /// <summary>Arma la frase de la advertencia previa al cierre a partir de cuántas sesiones y túneles van a cerrarse (FR-048, FR-109).</summary>
    public static string TextoDeAvisoDeCierre(int sesiones, int tunelesActivos)
    {
        var partes = new List<string>(2);

        if (sesiones > 0)
        {
            partes.Add($"{sesiones} sesión(es) abierta(s)");
        }

        if (tunelesActivos > 0)
        {
            partes.Add($"{tunelesActivos} túnel(es) activo(s)");
        }

        return $"Hay {string.Join(" y ", partes)}. Se van a cerrar.";
    }

    /// <summary>Comprueba si hay una versión nueva, al arrancar (FR-159, FR-160).</summary>
    private async Task ComprobarActualizacionAsync()
    {
        try
        {
            var resultado = await _actualizaciones.ComprobarAsync().ConfigureAwait(true);

            if (!resultado.Consultada || !resultado.HayVersionNueva
                || resultado.Release is null || resultado.VersionDisponible is null)
            {
                return;
            }

            var ajustes = await _actualizaciones.ObtenerAjustesAsync().ConfigureAwait(true);

            if (!_actualizaciones.CorrespondeAvisar(ajustes, resultado.VersionDisponible))
            {
                return;
            }

            MostrarAvisoDeActualizacion(resultado.Release, resultado.VersionDisponible, ajustes);
        }
        catch (Exception ex)
        {
            _root.Logger.TechnicalError("comprobar si hay una versión nueva", ex);
        }
    }

    private void MostrarAvisoDeActualizacion(
        Infrastructure.Actualizaciones.InformacionDeRelease release,
        Domain.Settings.VersionDeAplicacion version,
        Infrastructure.Database.AjustesDeActualizacion ajustes)
    {
        _releaseDisponible = release;
        _versionDisponible = version;
        _ajustesDeActualizacion = ajustes;

        var resumen = PrimeraLinea(release.Novedades);

        _textoAviso.Text = string.IsNullOrEmpty(resumen)
            ? $"Hay una versión nueva de CMC: {version}"
            : $"Hay una versión nueva de CMC: {version} — {resumen}";
        _textoAviso.ToolTip = release.Novedades;

        ConfigurarBotonesDeAviso(descargando: false);
        _avisoActualizacion.Visibility = Visibility.Visible;
    }

    /// <summary>Primer renglón no vacío de las novedades, recortado: el resto se lee en el ToolTip.</summary>
    private static string PrimeraLinea(string? novedades)
    {
        if (string.IsNullOrWhiteSpace(novedades))
        {
            return string.Empty;
        }

        var linea = novedades
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(l => l.Length > 0 && !l.StartsWith('#')) ?? string.Empty;

        return linea.Length > 140 ? string.Concat(linea.AsSpan(0, 140), "…") : linea;
    }

    private void OcultarAvisoDeActualizacion()
    {
        _avisoActualizacion.Visibility = Visibility.Collapsed;
        _releaseDisponible = null;
        _versionDisponible = null;
    }

    private void ConfigurarBotonesDeAviso(bool descargando)
    {
        _botonActualizar.IsEnabled = !descargando;
        _botonDespues.IsEnabled = !descargando;
        _botonVerPagina.IsEnabled = !descargando;
    }

    private void AlVerPaginaDeRelease(object sender, RoutedEventArgs e)
    {
        if (_releaseDisponible is null)
        {
            return;
        }

        var url = Services.ActualizacionesService.UrlDePagina(
            _ajustesDeActualizacion.Origen, _releaseDisponible.Version);

        try
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or System.IO.IOException)
        {
            _estado.Text = "No se pudo abrir el navegador.";
        }
    }

    /// <summary>«Después»: pospone esta versión hasta mañana (FR-160a).</summary>
    private async void AlPosponerActualizacion(object sender, RoutedEventArgs e)
    {
        if (_versionDisponible is null)
        {
            return;
        }

        try
        {
            await _actualizaciones
                .PosponerAsync(_ajustesDeActualizacion, _versionDisponible)
                .ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _root.Logger.TechnicalError("posponer el aviso de actualización", ex);
        }

        OcultarAvisoDeActualizacion();
    }

    /// <summary>«Actualizar»: descarga el instalador, lo verifica contra el hash publicado, y sólo lo ejecuta si la verificación dio bien (FR-161, FR-161a).</summary>
    private async void AlActualizarAhora(object sender, RoutedEventArgs e)
    {
        if (_releaseDisponible is null)
        {
            return;
        }

        var instalador = Services.SelectorDeInstalador.Elegir(_releaseDisponible.Activos);

        if (instalador is null)
        {
            _textoAviso.Text = "La release no publica un instalador para Windows.";
            return;
        }

        ConfigurarBotonesDeAviso(descargando: true);

        var progreso = new Progress<Infrastructure.Actualizaciones.ProgresoDeDescarga>(p =>
        {
            _textoAviso.Text = p.BytesTotales is { } total && total > 0
                ? $"Descargando la actualización… {p.BytesDescargados * 100 / total}%"
                : $"Descargando la actualización… {p.BytesDescargados / (1024.0 * 1024):0.0} MB";
        });

        Infrastructure.Actualizaciones.ResultadoDeDescarga resultado;

        using (var descargador = new Infrastructure.Actualizaciones.DescargadorDeInstalador(
            _root.Paths, logger: _root.Logger))
        {
            resultado = await descargador
                .DescargarYVerificarAsync(_releaseDisponible, instalador, progreso)
                .ConfigureAwait(true);
        }

        var (mensaje, ejecutar) = Services.MensajesDeDescarga.Interpretar(resultado);

        if (ejecutar && resultado.RutaArchivo is not null)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                    resultado.RutaArchivo)
                { UseShellExecute = true });

                Application.Current.Shutdown();
                return;
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or System.IO.IOException)
            {
                _root.Logger.TechnicalError("iniciar el instalador descargado", ex);
                _textoAviso.Text = "Se descargó pero no se pudo iniciar el instalador.";
                ConfigurarBotonesDeAviso(descargando: false);
                return;
            }
        }

        _textoAviso.Text = mensaje;
        ConfigurarBotonesDeAviso(descargando: false);
    }
}
