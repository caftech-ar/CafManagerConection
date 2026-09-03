using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using CafManagerConection.App.Services;
using CafManagerConection.App.Views;
using CafManagerConection.Platform;

namespace CafManagerConection.App.Panels;

/// <summary>Inventario de Docker: proyectos compose con sus contenedores, y los sueltos aparte.</summary>
[SupportedOSPlatform("windows")]
public sealed class DockerPanel : PanelInventario
{
    /// <summary>Una fila de la tabla: cabecera de proyecto o contenedor.</summary>
    public sealed record Fila(
        string Nombre,
        string Imagen,
        string Estado,
        string Cpu,
        string Memoria,
        string Puertos,
        bool EsCabecera,
        bool Corriendo,
        Geometry Icono,
        Brush Color,
        string Real = "");

    /// <summary>Un proyecto compose, con sus contenedores y los servicios que no llegaron a tener uno.</summary>
    public sealed record GrupoProyecto(
        string Nombre,
        IReadOnlyList<ContainerInfo> Contenedores,
        IReadOnlyList<string> ServiciosSinContenedor);

    /// <summary>Junta los contenedores por proyecto compose y les suma los servicios declarados que no llegaron a tener contenedor.</summary>
    public static List<GrupoProyecto> Agrupar(
        IReadOnlyList<ContainerInfo> todos, IReadOnlyList<ComposeProject> proyectos)
    {
        var porContenedor = todos
            .Where(c => !c.IsStandalone)
            .GroupBy(c => c.ComposeProject!, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                IReadOnlyList<ContainerInfo> (g) => g.ToList(),
                StringComparer.Ordinal);

        var nombres = porContenedor.Keys
            .Union(proyectos.Select(p => p.Name), StringComparer.Ordinal)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase);

        return nombres.Select(nombre =>
        {
            var proyecto = proyectos.FirstOrDefault(p => p.Name == nombre);

            var sinLevantar = proyecto?.Services
                .Where(s => s.ContainerName is null)
                .Select(s => s.Name)
                .ToList() ?? [];

            return new GrupoProyecto(
                nombre, porContenedor.GetValueOrDefault(nombre, []), sinLevantar);
        }).ToList();
    }

    private readonly PlatformInventory _inventario;
    private readonly ControlDeDocker _control;
    private readonly string _servidor;

    public DockerPanel(PlatformInventory inventario, ControlDeDocker control, string servidor)
        : base("Docker")
    {
        _inventario = inventario;
        _control = control;
        _servidor = servidor;

        Columna("Nombre", nameof(Fila.Nombre), 2);
        Columna("Imagen", nameof(Fila.Imagen), 1.4);
        ColumnaDeEstado();
        Columna("CPU", nameof(Fila.Cpu), 0.6);
        Columna("Memoria", nameof(Fila.Memoria), 1.1);
        Columna("Puertos", nameof(Fila.Puertos), 1.2);

        ItemContainerStyle();

        Tabla.MouseDoubleClick += (_, _) => AbrirFicha();

        Tabla.ContextMenuOpening += (_, e) =>
        {
            var menu = MenuDeContenedor();

            if (menu is null)
            {
                e.Handled = true;
                return;
            }

            Tabla.ContextMenu = menu;
        };
    }

    /// <summary>El contenedor elegido, o null si lo elegido es la cabecera de un proyecto.</summary>
    private Fila? Elegido =>
        Tabla.SelectedItem is Fila f && !f.EsCabecera && ControlDeDocker.EsNombreValido(f.Real)
            ? f
            : null;

    private ContextMenu? MenuDeContenedor()
    {
        if (Elegido is not { } elegido)
        {
            return null;
        }

        var menu = new ContextMenu();

        void Item(string texto, Action accion, Geometry? icono = null, Brush? color = null) =>
            menu.Items.Add(MenuIconos.Item(texto, accion, icono: icono, color: color));

        Item("Ver detalle…", AbrirFicha);
        menu.Items.Add(new Separator());
        Item(
            "Reiniciar contenedor…",
            () => _ = EjecutarAsync(AccionDeContenedor.Reiniciar, elegido),
            (Geometry)FindResource("IconoReconectar"), (Brush)FindResource("Texto"));

        if (elegido.Corriendo)
        {
            Item(
                "Detener contenedor…",
                () => _ = EjecutarAsync(AccionDeContenedor.Detener, elegido),
                MenuIconos.IconoDetener, (Brush)FindResource("Destructivo"));
        }
        else
        {
            Item(
                "Iniciar contenedor…",
                () => _ = EjecutarAsync(AccionDeContenedor.Iniciar, elegido),
                MenuIconos.IconoIniciar, (Brush)FindResource("EstadoConectado"));
        }

        return menu;
    }

    private void AbrirFicha()
    {
        if (Elegido is not { } elegido || Window.GetWindow(this) is not { } ventana)
        {
            return;
        }

        var ficha = new ContenedorWindow(_control, elegido.Real, _servidor) { Owner = ventana };

        ficha.ShowDialog();

        if (ficha.Cambio)
        {
            _ = RefrescarAsync();
        }
    }

    private async Task EjecutarAsync(AccionDeContenedor accion, Fila contenedor)
    {
        if (Window.GetWindow(this) is not { } ventana)
        {
            return;
        }

        var verbo = accion switch
        {
            AccionDeContenedor.Iniciar => "iniciar",
            AccionDeContenedor.Detener => "detener",
            _ => "reiniciar",
        };

        var Verbo = char.ToUpperInvariant(verbo[0]) + verbo[1..];

        var mostrado = contenedor.Nombre.Trim();

        var cual = mostrado.Equals(contenedor.Real, StringComparison.Ordinal)
            ? $"«{contenedor.Real}»"
            : $"«{mostrado}» (contenedor {contenedor.Real})";

        var confirmado = Dialogos.Confirmar(
            ventana,
            $"¿{Verbo} el contenedor?",
            $"Se va a {verbo} {cual} en {_servidor}.",
            Verbo);

        if (!confirmado)
        {
            return;
        }

        using var _t = Trabajando($"{Verbo}ando «{contenedor.Real}»…");

        var r = await _control.EjecutarAsync(accion, contenedor.Real).ConfigureAwait(true);

        if (!r.Success)
        {
            MostrarError(r.Error);
            return;
        }

        await RefrescarAsync().ConfigureAwait(true);
    }

    private void Columna(string cabecera, string propiedad, double peso) =>
        Tabla.Columns.Add(new DataGridTextColumn
        {
            Header = cabecera,
            Binding = new Binding(propiedad),
            Width = new DataGridLength(peso, DataGridLengthUnitType.Star),
        });

    /// <summary>Columna de estado, con el glifo de la gravedad al lado del texto que trae Docker.</summary>
    private void ColumnaDeEstado()
    {
        var plantilla = new DataTemplate();
        var fila = new FrameworkElementFactory(typeof(StackPanel));
        fila.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);

        var icono = new FrameworkElementFactory(typeof(System.Windows.Shapes.Path));
        icono.SetValue(FrameworkElement.WidthProperty, 13.0);
        icono.SetValue(FrameworkElement.HeightProperty, 13.0);
        icono.SetValue(System.Windows.Shapes.Path.StretchProperty, Stretch.Uniform);
        icono.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 6, 0));
        icono.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        icono.SetBinding(System.Windows.Shapes.Path.DataProperty, new Binding(nameof(Fila.Icono)));
        icono.SetBinding(System.Windows.Shapes.Path.FillProperty, new Binding(nameof(Fila.Color)));
        fila.AppendChild(icono);

        var texto = new FrameworkElementFactory(typeof(TextBlock));
        texto.SetBinding(TextBlock.TextProperty, new Binding(nameof(Fila.Estado)));
        texto.SetBinding(TextBlock.ForegroundProperty, new Binding(nameof(Fila.Color)));
        texto.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        texto.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
        fila.AppendChild(texto);

        plantilla.VisualTree = fila;

        Tabla.Columns.Add(new DataGridTemplateColumn
        {
            Header = "Estado",
            CellTemplate = plantilla,
            Width = new DataGridLength(1.3, DataGridLengthUnitType.Star),
        });
    }

    private void ItemContainerStyle()
    {
        var estilo = new Style(typeof(DataGridRow), (Style)FindResource(typeof(DataGridRow)));

        var cabecera = new DataTrigger
        {
            Binding = new Binding(nameof(Fila.EsCabecera)),
            Value = true,
        };

        cabecera.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.SemiBold));

        estilo.Triggers.Add(cabecera);

        Tabla.RowStyle = estilo;

        var celda = new Style(typeof(DataGridCell), (Style)FindResource(typeof(DataGridCell)));
        celda.Setters.Add(new Setter(Control.ForegroundProperty, new Binding(nameof(Fila.Color))));
        Tabla.CellStyle = celda;
    }

    /// <summary>Glifo y pincel que le corresponden a una gravedad, ya resueltos contra el tema.</summary>
    private (Geometry Icono, Brush Color) EstiloDeGravedad(GravedadDeContenedor gravedad) =>
        gravedad switch
        {
            GravedadDeContenedor.Corriendo =>
                ((Geometry)FindResource("IconoOk"), (Brush)FindResource("EstadoConectado")),

            GravedadDeContenedor.Advertencia =>
                ((Geometry)FindResource("IconoAlerta"), (Brush)FindResource("IconoAmbar")),

            GravedadDeContenedor.Falla =>
                ((Geometry)FindResource("IconoAlerta"), (Brush)FindResource("EstadoError")),

            _ => ((Geometry)FindResource("IconoAlerta"), (Brush)FindResource("TextoTenue")),
        };

    /// <summary>Gravedad de un grupo compose, a partir de la de sus contenedores.</summary>
    private static GravedadDeContenedor GravedadDeGrupo(IEnumerable<ContainerInfo> contenedores)
    {
        var gravedades = contenedores.Select(c => c.Gravedad).ToHashSet();

        if (gravedades.Contains(GravedadDeContenedor.Falla))
        {
            return GravedadDeContenedor.Falla;
        }

        if (gravedades.Contains(GravedadDeContenedor.Corriendo))
        {
            return GravedadDeContenedor.Corriendo;
        }

        return gravedades.Contains(GravedadDeContenedor.Advertencia)
            ? GravedadDeContenedor.Advertencia
            : GravedadDeContenedor.Detenido;
    }


    /// <summary>Refresco en curso, para descartar la respuesta tardía de uno anterior.</summary>
    private int _generacion;

    // docker ps contesta en unos 100 ms, compose ls suma otros 870 ms y stats dos segundos y medio más.
    public override async Task RefrescarAsync()
    {
        var mia = ++_generacion;

        using var _t = Trabajando("Consultando Docker…");

        var contenedores = await _inventario.GetContainersAsync().ConfigureAwait(true);

        if (mia != _generacion)
        {
            return;
        }

        if (!contenedores.Success)
        {
            MostrarError(contenedores.Error);
            return;
        }

        var todos = contenedores.Value!;

        var agrupadosSinCompose = Agrupar(todos, []);

        Pintar(todos, agrupadosSinCompose, rutas: new Dictionary<string, string>(), consumo: null);
        MostrarResumen($"{Detalle(todos, agrupadosSinCompose.Count)} · consultando compose…");

        var compose = await _inventario
            .GetComposeProjectsAsync(todos, incluirServicios: true).ConfigureAwait(true);

        if (mia != _generacion)
        {
            return;
        }

        var proyectos = compose.Success ? compose.Value! : [];

        var rutas = proyectos
            .ToDictionary(p => p.Name, p => p.FilePath, StringComparer.Ordinal);

        var agrupados = Agrupar(todos, proyectos);

        Pintar(todos, agrupados, rutas, consumo: null);
        MostrarResumen($"{Detalle(todos, agrupados.Count)} · midiendo consumo…");

        var uso = await _inventario.GetUsageAsync().ConfigureAwait(true);

        if (mia != _generacion)
        {
            return;
        }

        Pintar(todos, agrupados, rutas, uso.Success
            ? uso.Value!
            : new Dictionary<string, ContainerUsage>(StringComparer.Ordinal));

        MostrarResumen(uso.Success
            ? $"{Detalle(todos, agrupados.Count)} · sólo lectura"
            : $"{Detalle(todos, agrupados.Count)} · sin datos de consumo: {uso.Error}"
              + " · sólo lectura");
    }

    private void Pintar(
        IReadOnlyList<ContainerInfo> todos,
        List<GrupoProyecto> agrupados,
        IReadOnlyDictionary<string, string> rutas,
        IReadOnlyDictionary<string, ContainerUsage>? consumo)
    {
        var filas = new List<Fila>();

        foreach (var grupo in agrupados)
        {
            var enMarcha = grupo.Contenedores.Count(c => c.IsRunning);
            var total = grupo.Contenedores.Count + grupo.ServiciosSinContenedor.Count;
            var (icono, color) = EstiloDeGravedad(GravedadDeGrupo(grupo.Contenedores));

            filas.Add(new Fila(
                grupo.Nombre,
                "compose",
                $"{enMarcha}/{total}",
                string.Empty,
                string.Empty,
                rutas.GetValueOrDefault(grupo.Nombre, string.Empty),
                EsCabecera: true,
                Corriendo: enMarcha > 0,
                icono,
                color));

            foreach (var c in grupo.Contenedores
                .OrderBy(c => c.ComposeService, StringComparer.OrdinalIgnoreCase))
            {
                filas.Add(AFila(c, consumo, sangria: true));
            }

            foreach (var servicio in grupo.ServiciosSinContenedor
                .OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
            {
                filas.Add(FilaSinContenedor(servicio));
            }
        }

        var sueltos = todos
            .Where(c => c.IsStandalone)
            .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (sueltos.Count > 0)
        {
            if (agrupados.Count > 0)
            {
                var (icono, color) = EstiloDeGravedad(GravedadDeGrupo(sueltos));

                filas.Add(new Fila(
                    "Sin compose", string.Empty,
                    $"{sueltos.Count(c => c.IsRunning)}/{sueltos.Count}",
                    string.Empty, string.Empty, string.Empty,
                    EsCabecera: true, Corriendo: true, icono, color));
            }

            filas.AddRange(sueltos.Select(c => AFila(c, consumo, sangria: agrupados.Count > 0)));
        }

        Tabla.ItemsSource = filas;
    }

    private static string Detalle(IReadOnlyList<ContainerInfo> todos, int proyectos)
    {
        var corriendo = todos.Count(c => c.IsRunning);

        return proyectos > 0
            ? $"{corriendo} de {todos.Count} contenedores corriendo · "
              + $"{proyectos} proyecto(s) compose"
            : $"{corriendo} de {todos.Count} contenedores corriendo";
    }

    /// <summary>Fila de un servicio declarado en el compose que nunca llegó a tener contenedor (FR-098).</summary>
    private Fila FilaSinContenedor(string servicio)
    {
        var (icono, color) = EstiloDeGravedad(GravedadDeContenedor.Detenido);

        return new Fila(
            $"    {servicio}",
            string.Empty,
            "sin levantar",
            "—",
            "—",
            string.Empty,
            EsCabecera: false,
            Corriendo: false,
            icono,
            color);
    }

    private Fila AFila(
        ContainerInfo c,
        IReadOnlyDictionary<string, ContainerUsage>? consumo,
        bool sangria)
    {
        var etiqueta = c.ComposeService ?? c.Name;

        var esperando = consumo is null;
        var medida = consumo is not null && c.IsRunning ? Buscar(consumo, c.Id) : null;

        var cpu = esperando && c.IsRunning
            ? "…"
            : medida is null ? "—" : $"{medida.CpuPercent:0.0}%";

        var memoria = esperando && c.IsRunning
            ? "…"
            : medida is null ? "—" : Memoria(medida);

        var (icono, color) = EstiloDeGravedad(c.Gravedad);

        return new Fila(
            sangria ? $"    {etiqueta}" : etiqueta,
            c.Image,
            c.Status,
            cpu,
            memoria,
            string.Join(", ", c.PublishedPorts),
            EsCabecera: false,
            c.IsRunning,
            icono,
            color,
            Real: c.Name);
    }

    // docker ps --no-trunc da el identificador de 64 caracteres y docker stats el de 12.
    /// <summary>Busca el consumo tolerando que los identificadores tengan largo distinto.</summary>
    private static ContainerUsage? Buscar(
        IReadOnlyDictionary<string, ContainerUsage> consumo, string id)
    {
        if (consumo.TryGetValue(id, out var exacto))
        {
            return exacto;
        }

        foreach (var (clave, valor) in consumo)
        {
            if (clave.Length > 0
                && (id.StartsWith(clave, StringComparison.Ordinal)
                    || clave.StartsWith(id, StringComparison.Ordinal)))
            {
                return valor;
            }
        }

        return null;
    }

    private static string Memoria(ContainerUsage u) =>
        u.MemoryLimitBytes > 0
            ? $"{Tamano(u.MemoryBytes)} · {u.MemoryPercent:0.0}%"
            : Tamano(u.MemoryBytes);

    internal static string Tamano(long bytes)
    {
        string[] unidades = ["B", "KiB", "MiB", "GiB", "TiB"];
        double valor = bytes;
        var i = 0;

        while (valor >= 1024 && i < unidades.Length - 1)
        {
            valor /= 1024;
            i++;
        }

        return i == 0 ? $"{bytes} B" : $"{valor:0.#} {unidades[i]}";
    }
}

/// <summary>Sitios publicados por nginx (US10).</summary>
[SupportedOSPlatform("windows")]
public sealed class NginxPanel : PanelInventario
{
    public sealed record Fila(string Nombres, string Puertos, string Raiz, string Archivo);

    private readonly PlatformInventory _inventario;

    public NginxPanel(PlatformInventory inventario)
        : base("nginx")
    {
        _inventario = inventario;

        Agregar("Puertos", nameof(Fila.Puertos), 0.8);
        Agregar("Raíz", nameof(Fila.Raiz), 1.6);
        Agregar("Nombres de servidor", nameof(Fila.Nombres), 2);
        Agregar("Archivo", nameof(Fila.Archivo), 1.6);

        Tabla.MouseDoubleClick += async (_, _) => await VerConfiguracionAsync().ConfigureAwait(true);
    }

    private void Agregar(string cabecera, string propiedad, double peso) =>
        Tabla.Columns.Add(new DataGridTextColumn
        {
            Header = cabecera,
            Binding = new Binding(propiedad),
            Width = new DataGridLength(peso, DataGridLengthUnitType.Star),
        });

    public override async Task RefrescarAsync()
    {
        using var _t = Trabajando("Consultando nginx…");

        var sitios = await _inventario.GetNginxSitesAsync().ConfigureAwait(true);

        if (!sitios.Success)
        {
            MostrarError(sitios.Error);
            return;
        }

        Tabla.ItemsSource = sitios.Value!
            .Select(s => new Fila(
                string.Join(", ", s.ServerNames),
                string.Join(", ", s.ListenPorts),
                s.DocumentRoot ?? "—",
                s.ConfigFile ?? "—"))
            .ToList();

        MostrarResumen(
            $"{sitios.Value!.Count} sitio(s) · doble clic para ver la configuración efectiva "
            + "· sólo lectura");
    }

    private async Task VerConfiguracionAsync()
    {
        if (Tabla.SelectedItem is not Fila elegida || elegida.Archivo.Length == 0)
        {
            MostrarResumen("Elegí un sitio para ver su configuración.");
            return;
        }

        using var _t = Trabajando($"Leyendo {elegida.Archivo}…");

        var config = await _inventario
            .GetNginxConfigAsync(elegida.Archivo).ConfigureAwait(true);

        if (!config.Success)
        {
            MostrarError(config.Error);
            return;
        }

        if (Window.GetWindow(this) is { } ventana)
        {
            TextViewerWindow.Mostrar(ventana, "Configuración efectiva de nginx", config.Value!);
        }
    }
}

/// <summary>Procesos que gestiona supervisord (US10).</summary>
[SupportedOSPlatform("windows")]
public sealed class SupervisorPanel : PanelInventario
{
    public sealed record Fila(
        string Proceso,
        string Estado,
        string Detalle,
        bool Fallido,
        Geometry Icono,
        Brush Color);

    private readonly PlatformInventory _inventario;
    private readonly ControlDeSupervisor _control;
    private readonly IPlatformCommandRunner? _canal;

    /// <param name="canal">El mismo ejecutor de la sesión; sin él el visor no puede seguir el registro en vivo (FR-185).</param>
    public SupervisorPanel(
        PlatformInventory inventario,
        ControlDeSupervisor control,
        IPlatformCommandRunner? canal = null)
        : base("supervisord")
    {
        _inventario = inventario;
        _control = control;
        _canal = canal;

        ColumnaDeEstado();
        Agregar("Proceso", nameof(Fila.Proceso), 1.6);
        Agregar("Detalle", nameof(Fila.Detalle), 2.2);

        var estilo = new Style(typeof(DataGridRow), (Style)FindResource(typeof(DataGridRow)));

        var fallido = new DataTrigger
        {
            Binding = new Binding(nameof(Fila.Fallido)),
            Value = true,
        };

        fallido.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.SemiBold));

        estilo.Triggers.Add(fallido);
        Tabla.RowStyle = estilo;

        Tabla.CellStyle = ColorDeCelda(
            nameof(Fila.Fallido), true, (Brush)FindResource("Destructivo"));

        Tabla.MouseDoubleClick += async (_, _) => await VerRegistroAsync().ConfigureAwait(true);

        Tabla.ContextMenuOpening += (_, e) =>
        {
            var menu = MenuDeProceso();

            if (menu is null)
            {
                e.Handled = true;
                return;
            }

            Tabla.ContextMenu = menu;
        };
    }

    /// <summary>Columna de estado, con el icono al lado del texto.</summary>
    private void ColumnaDeEstado()
    {
        var plantilla = new DataTemplate();
        var fila = new FrameworkElementFactory(typeof(StackPanel));
        fila.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);

        var icono = new FrameworkElementFactory(typeof(System.Windows.Shapes.Path));
        icono.SetValue(FrameworkElement.WidthProperty, 14.0);
        icono.SetValue(FrameworkElement.HeightProperty, 14.0);
        icono.SetValue(System.Windows.Shapes.Path.StretchProperty, Stretch.Uniform);
        icono.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 7, 0));
        icono.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        icono.SetBinding(System.Windows.Shapes.Path.DataProperty, new Binding(nameof(Fila.Icono)));
        icono.SetBinding(System.Windows.Shapes.Path.FillProperty, new Binding(nameof(Fila.Color)));
        fila.AppendChild(icono);

        var texto = new FrameworkElementFactory(typeof(TextBlock));
        texto.SetBinding(TextBlock.TextProperty, new Binding(nameof(Fila.Estado)));
        texto.SetBinding(TextBlock.ForegroundProperty, new Binding(nameof(Fila.Color)));
        texto.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        fila.AppendChild(texto);

        plantilla.VisualTree = fila;

        Tabla.Columns.Add(new DataGridTemplateColumn
        {
            Header = "Estado",
            CellTemplate = plantilla,
            Width = new DataGridLength(1.0, DataGridLengthUnitType.Star),
        });
    }

    private void Agregar(string cabecera, string propiedad, double peso) =>
        Tabla.Columns.Add(new DataGridTextColumn
        {
            Header = cabecera,
            Binding = new Binding(propiedad),
            Width = new DataGridLength(peso, DataGridLengthUnitType.Star),
        });

    /// <summary>Menú del proceso elegido: ver su registro y las acciones que lo modifican.</summary>
    private ContextMenu? MenuDeProceso()
    {
        if (Tabla.SelectedItem is not Fila elegida)
        {
            return null;
        }

        var menu = new ContextMenu();

        void Item(string texto, Action accion, Geometry? icono = null, Brush? color = null) =>
            menu.Items.Add(MenuIconos.Item(texto, accion, icono: icono, color: color));

        Item("Ver registro…", () => _ = VerRegistroAsync());
        menu.Items.Add(new Separator());
        Item(
            "Reiniciar proceso…", () => _ = EjecutarAsync(AccionDeProceso.Reiniciar, elegida),
            (Geometry)FindResource("IconoReconectar"), (Brush)FindResource("Texto"));

        if (elegida.Estado.Equals("RUNNING", StringComparison.OrdinalIgnoreCase))
        {
            Item(
                "Detener proceso…", () => _ = EjecutarAsync(AccionDeProceso.Detener, elegida),
                MenuIconos.IconoDetener, (Brush)FindResource("Destructivo"));
        }
        else
        {
            Item(
                "Iniciar proceso…", () => _ = EjecutarAsync(AccionDeProceso.Iniciar, elegida),
                MenuIconos.IconoIniciar, (Brush)FindResource("EstadoConectado"));
        }

        return menu;
    }

    private async Task EjecutarAsync(AccionDeProceso accion, Fila proceso)
    {
        if (Window.GetWindow(this) is not { } ventana)
        {
            return;
        }

        var verbo = accion switch
        {
            AccionDeProceso.Iniciar => "iniciar",
            AccionDeProceso.Detener => "detener",
            _ => "reiniciar",
        };

        var Verbo = char.ToUpperInvariant(verbo[0]) + verbo[1..];

        var confirmado = Dialogos.Confirmar(
            ventana,
            $"¿{Verbo} el proceso?",
            $"Se va a {verbo} «{proceso.Proceso}» en el servidor de esta sesión.",
            Verbo);

        if (!confirmado)
        {
            return;
        }

        using var _t = Trabajando($"{Verbo}ando «{proceso.Proceso}»…");

        var r = await _control.EjecutarAsync(accion, proceso.Proceso).ConfigureAwait(true);

        if (!r.Success)
        {
            MostrarError(r.Error);
            return;
        }

        await RefrescarAsync().ConfigureAwait(true);
    }

    /// <summary>Abre el registro del proceso en un visor que lo sigue en vivo (FR-185, FR-185d).</summary>
    private Task VerRegistroAsync()
    {
        if (Tabla.SelectedItem is not Fila elegida)
        {
            MostrarResumen("Elegí un proceso para ver su registro.");
            return Task.CompletedTask;
        }

        if (Window.GetWindow(this) is { } ventana)
        {
            VisorDeRegistroWindow.Mostrar(
                ventana, new RegistroDeProcesoSupervisado(_inventario, _canal, elegida.Proceso));
        }

        return Task.CompletedTask;
    }

    public override async Task RefrescarAsync()
    {
        using var _t = Trabajando("Consultando supervisord…");

        var procesos = await _inventario.GetSupervisorAsync().ConfigureAwait(true);

        if (!procesos.Success)
        {
            MostrarError(procesos.Error);
            return;
        }

        Tabla.ItemsSource = procesos.Value!
            .OrderByDescending(p => !p.IsRunning)
            .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .Select(p => new Fila(
                p.Name,
                p.State,
                Detalle(p),
                !p.IsRunning,
                IconoDeGravedad(p.Gravedad),
                (Brush)FindResource(ColorDeGravedad(p.Gravedad))))
            .ToList();

        var caidos = procesos.Value!.Count(p => p.Gravedad == GravedadDeProceso.Falla);
        var total = procesos.Value!.Count;

        MostrarResumen(caidos == 0
            ? $"{total} proceso(s), ninguno caído · botón derecho para actuar"
            : $"{caidos} de {total} proceso(s) caído(s) · botón derecho para actuar");
    }

    private static string Detalle(SupervisorProcess proceso) =>
        proceso.Uptime is { } arriba ? TiempoArriba(arriba) : proceso.Detail ?? string.Empty;

    private static string TiempoArriba(TimeSpan arriba) => arriba switch
    {
        { TotalSeconds: < 60 } => $"{(int)arriba.TotalSeconds} s arriba",
        { TotalMinutes: < 60 } => $"{(int)arriba.TotalMinutes} min arriba",
        { TotalHours: < 24 } => $"{arriba.Hours} h {arriba.Minutes} min arriba",
        _ => $"{arriba.Days} d {arriba.Hours} h arriba",
    };

    private static string ColorDeGravedad(GravedadDeProceso gravedad) => gravedad switch
    {
        GravedadDeProceso.Corriendo => "EstadoConectado",
        GravedadDeProceso.Falla => "EstadoError",
        _ => "IconoAmbar",
    };

    // No hay glifo de falla en Themes/Estilos.xaml; mismo lienzo de 20x20 que el resto.
    /// <summary>Equis para Falla, para que no comparta glifo con Advertencia (FR-100d).</summary>
    private static readonly Geometry IconoFalla = Geometry.Parse(
        "F1 M10 2C14.42 2 18 5.58 18 10S14.42 18 10 18S2 14.42 2 10S5.58 2 10 2Z "
        + "M6.96 6.04L13.96 13.04L13.04 13.96L6.04 6.96Z "
        + "M13.04 6.04L6.04 13.04L6.96 13.96L13.96 6.96Z");

    private Geometry IconoDeGravedad(GravedadDeProceso gravedad) => gravedad switch
    {
        GravedadDeProceso.Corriendo => (Geometry)FindResource("IconoOk"),
        GravedadDeProceso.Falla => IconoFalla,
        _ => (Geometry)FindResource("IconoAlerta"),
    };
}

/// <summary>Puertos en los que el servidor está escuchando.</summary>
[SupportedOSPlatform("windows")]
public sealed class PuertosPanel : PanelInventario
{
    public sealed record Fila(
        string Puerto,
        string Protocolo,
        string Escucha,
        string Proceso,
        Geometry? Icono,
        Brush? Color,
        string? Aplicacion,
        int? Pid,
        string Tunel = "");

    /// <summary>Túnel definido para un puerto del servidor.</summary>
    public sealed record TunelDePuerto(int PuertoLocal, bool Activo);

    private readonly PlatformInventory _inventario;
    private readonly ConsultorDeProcesos? _consultor;
    private readonly string _servidor;
    private readonly string _host;

    /// <summary>Qué hacer cuando piden un túnel a un puerto: recibe el puerto del servidor y un nombre sugerido. null cuando la sesión no puede armar túneles, y entonces la opción no aparece en vez de aparecer y fallar.</summary>
    private readonly Func<int, string, Task>? _crearTunel;

    /// <summary>Qué túnel hay definido para un puerto del servidor, si hay alguno.</summary>
    private readonly Func<int, TunelDePuerto?>? _tunelDe;

    /// <summary>La línea de ssh -L equivalente, que este panel sólo copia.</summary>
    private readonly Func<int, string>? _lineaSsh;

    public PuertosPanel(
        PlatformInventory inventario,
        ConsultorDeProcesos? consultor = null,
        string servidor = "",
        string host = "",
        Func<int, string, Task>? crearTunel = null,
        Func<int, TunelDePuerto?>? tunelDe = null,
        Func<int, string>? lineaSsh = null)
        : base("Puertos a la escucha")
    {
        _inventario = inventario;
        _consultor = consultor;
        _servidor = servidor;
        _host = host;
        _crearTunel = crearTunel;
        _tunelDe = tunelDe;
        _lineaSsh = lineaSsh;

        Columna("Puerto", nameof(Fila.Puerto), 0.7);
        Columna("Proto", nameof(Fila.Protocolo), 0.6);
        Columna("Escucha en", nameof(Fila.Escucha), 1.2);
        Columna("Túnel", nameof(Fila.Tunel), 0.8);
        ColumnaDeProceso();

        Tabla.MouseDoubleClick += async (_, _) =>
        {
            try
            {
                await AbrirFichaAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                MostrarResumen($"No se pudo abrir la ficha del proceso: {ex.Message}");
            }
        };

        Tabla.PreviewMouseRightButtonDown += (_, e) =>
        {
            if (e.OriginalSource is DependencyObject origen
                && FilaQueContiene(origen) is { } fila)
            {
                fila.IsSelected = true;
            }
        };

        Tabla.ContextMenuOpening += (_, e) =>
        {
            var menu = MenuDePuerto();

            if (menu is null)
            {
                e.Handled = true;
                return;
            }

            Tabla.ContextMenu = menu;
        };
    }

    /// <summary>Menú del puerto elegido: ver el proceso, abrirlo en el navegador y copiar la dirección.</summary>
    private ContextMenu? MenuDePuerto()
    {
        if (Tabla.SelectedItem is not Fila elegida)
        {
            return null;
        }

        var menu = new ContextMenu();

        void Item(string texto, Action accion, Geometry? icono = null, Brush? color = null) =>
            menu.Items.Add(MenuIconos.Item(texto, accion, icono: icono, color: color));

        if (elegida.Pid is not null && _consultor is not null)
        {
            Item(
                "Ver el proceso…",
                () => _ = AbrirFichaAsync(),
                (Geometry)FindResource("IconoAplicacion"),
                (Brush)FindResource("IconoCyan"));

            menu.Items.Add(new Separator());
        }

        var web = elegida.Protocolo.StartsWith("tcp", StringComparison.OrdinalIgnoreCase);
        var alcanzable = elegida.Escucha is not "local" && _host.Length > 0;

        int.TryParse(elegida.Puerto, out var numero);
        var tunel = web && numero > 0 ? _tunelDe?.Invoke(numero) : null;

        if (tunel is { Activo: true } vivo)
        {
            Item(
                $"Abrir https://localhost:{vivo.PuertoLocal}  (por el túnel)",
                () => Abrir($"https://localhost:{vivo.PuertoLocal}"),
                (Geometry)FindResource("IconoWeb"),
                (Brush)FindResource("ProtocoloWeb"));

            Item(
                $"Abrir http://localhost:{vivo.PuertoLocal}  (por el túnel)",
                () => Abrir($"http://localhost:{vivo.PuertoLocal}"),
                (Geometry)FindResource("IconoWeb"),
                (Brush)FindResource("IconoGris"));
        }
        else if (web && alcanzable)
        {
            Item(
                $"Abrir {Url(elegida, seguro: true)}",
                () => Abrir(Url(elegida, seguro: true)),
                (Geometry)FindResource("IconoWeb"),
                (Brush)FindResource("ProtocoloWeb"));

            Item(
                $"Abrir {Url(elegida, seguro: false)}",
                () => Abrir(Url(elegida, seguro: false)),
                (Geometry)FindResource("IconoWeb"),
                (Brush)FindResource("IconoGris"));
        }
        else if (web)
        {
            var aviso = MenuIconos.Item(
                tunel is null
                    ? "Escucha sólo en el servidor: no hay ruta desde este equipo"
                    : "Hay un túnel definido pero está parado: levantalo en el panel de túneles",
                () => { },
                icono: (Geometry)FindResource("IconoAlerta"),
                color: (Brush)FindResource("TextoTenue"));

            aviso.IsEnabled = false;
            menu.Items.Add(aviso);
        }

        if (web && tunel is null && _crearTunel is { } crear && numero > 0)
        {
            Item(
                "Crear un túnel a este puerto…",
                () => _ = crear(numero, elegida.Aplicacion ?? elegida.Proceso),
                (Geometry)FindResource("IconoPanelTuneles"),
                (Brush)FindResource("IconoVioleta"));
        }

        menu.Items.Add(new Separator());

        if (web && numero > 0 && _lineaSsh is { } linea)
        {
            Item(
                "Copiar la línea ssh -L",
                () => Copiar(linea(numero)),
                (Geometry)FindResource("IconoTerminalExterna"),
                (Brush)FindResource("Texto"));
        }

        Item(
            $"Copiar {Destino(elegida)}",
            () => Copiar(Destino(elegida)),
            (Geometry)FindResource("IconoCopiarTodo"),
            (Brush)FindResource("Texto"));

        return menu;
    }

    /// <summary>Qué poner en la columna del túnel para este puerto.</summary>
    private string EstadoDeTunel(ListeningPort puerto)
    {
        if (_tunelDe?.Invoke(puerto.Port) is not { } tunel)
        {
            return string.Empty;
        }

        return tunel.Activo
            ? $"→ {tunel.PuertoLocal}"
            : $"→ {tunel.PuertoLocal} (parado)";
    }

    /// <summary>Qué contenedor publica cada puerto, para los que abrió el reenviador de Docker (FR-164e).</summary>
    private async Task<IReadOnlyDictionary<int, string>> ContenedoresPorPuertoAsync(
        IReadOnlyList<ListeningPort> puertos)
    {
        if (!puertos.Any(p => PuertosDeContenedores.EsReenviadorDeDocker(p.Process)))
        {
            return new Dictionary<int, string>();
        }

        try
        {
            var contenedores = await _inventario.GetContainersAsync().ConfigureAwait(true);

            return contenedores.Success
                ? PuertosDeContenedores.PorPuertoDelServidor(contenedores.Value!)
                : new Dictionary<int, string>();
        }
        catch (Exception)
        {
            return new Dictionary<int, string>();
        }
    }

    /// <summary>La fila del DataGrid que contiene este elemento, si hay alguna.</summary>
    private static DataGridRow? FilaQueContiene(DependencyObject? origen)
    {
        while (origen is not null and not DataGridRow)
        {
            origen = origen is System.Windows.Media.Visual or System.Windows.Media.Media3D.Visual3D
                ? System.Windows.Media.VisualTreeHelper.GetParent(origen)
                : System.Windows.LogicalTreeHelper.GetParent(origen);
        }

        return origen as DataGridRow;
    }

    private string Destino(Fila fila) => $"{_host}:{fila.Puerto}";

    private string Url(Fila fila, bool seguro) =>
        $"{(seguro ? "https" : "http")}://{_host}:{fila.Puerto}";

    private void Abrir(string url)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true,
            });

            MostrarResumen($"Abriendo {url} en el navegador…");
        }
        catch (Exception ex)
            when (ex is System.ComponentModel.Win32Exception or System.IO.IOException)
        {
            MostrarError($"No se pudo abrir el navegador: {ex.Message}");
        }
    }

    private void Copiar(string texto)
    {
        try
        {
            System.Windows.Clipboard.SetText(texto);
            MostrarResumen($"Copiado: {texto}");
        }
        catch (System.Runtime.InteropServices.COMException)
        {
        }
    }

    /// <summary>Abre la ficha del proceso que tiene el puerto elegido (FR-165).</summary>
    private async Task AbrirFichaAsync()
    {
        if (Tabla.SelectedItem is not Fila elegida)
        {
            return;
        }

        if (elegida.Pid is not { } pid)
        {
            MostrarResumen(
                "No se puede ver el proceso de ese puerto: hace falta más permiso en el servidor.");
            return;
        }

        if (_consultor is null || Window.GetWindow(this) is not { } ventana)
        {
            return;
        }

        using var _t = Trabajando($"Consultando el proceso {pid}…");

        var detalle = await _consultor
            .LeerAsync(pid, elegida.Proceso, CancellationToken.None)
            .ConfigureAwait(true);

        if (!detalle.Success)
        {
            MessageWindow.Avisar(
                ventana,
                "No se pudo leer el proceso",
                detalle.Error is { Length: > 0 } motivo
                    ? motivo
                    : $"El proceso {pid} ya no existe en el servidor.");

            return;
        }

        ProcesoWindow.Mostrar(ventana, detalle.Value!, _servidor, elegida.Puerto);
    }

    /// <summary>Columna del proceso, con el icono de la aplicación cuando se la reconoce.</summary>
    private void ColumnaDeProceso()
    {
        var plantilla = new DataTemplate();
        var fila = new FrameworkElementFactory(typeof(StackPanel));
        fila.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);

        var icono = new FrameworkElementFactory(typeof(System.Windows.Shapes.Path));
        icono.SetValue(FrameworkElement.WidthProperty, 14.0);
        icono.SetValue(FrameworkElement.HeightProperty, 14.0);
        icono.SetValue(System.Windows.Shapes.Path.StretchProperty, Stretch.Uniform);
        icono.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 7, 0));
        icono.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        icono.SetBinding(System.Windows.Shapes.Path.DataProperty, new Binding(nameof(Fila.Icono)));
        icono.SetBinding(System.Windows.Shapes.Path.FillProperty, new Binding(nameof(Fila.Color)));
        fila.AppendChild(icono);

        var texto = new FrameworkElementFactory(typeof(TextBlock));
        texto.SetBinding(TextBlock.TextProperty, new Binding(nameof(Fila.Proceso)));
        texto.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        texto.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
        fila.AppendChild(texto);

        var nombre = new FrameworkElementFactory(typeof(TextBlock));
        nombre.SetBinding(TextBlock.TextProperty, new Binding(nameof(Fila.Aplicacion)));
        nombre.SetValue(FrameworkElement.MarginProperty, new Thickness(8, 0, 0, 0));
        nombre.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        nombre.SetValue(TextBlock.FontSizeProperty, 12.0);
        nombre.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
        nombre.SetResourceReference(TextBlock.ForegroundProperty, "TextoTenue");
        fila.AppendChild(nombre);

        plantilla.VisualTree = fila;

        Tabla.Columns.Add(new DataGridTemplateColumn
        {
            Header = "Proceso",
            CellTemplate = plantilla,
            Width = new DataGridLength(1.5, DataGridLengthUnitType.Star),
        });
    }

    private void Columna(string cabecera, string propiedad, double peso) =>
        Tabla.Columns.Add(new DataGridTextColumn
        {
            Header = cabecera,
            Binding = new Binding(propiedad),
            Width = new DataGridLength(peso, DataGridLengthUnitType.Star),
        });

    public override async Task RefrescarAsync()
    {
        using var _t = Trabajando("Consultando los puertos…");

        var puertos = await _inventario.GetListeningPortsAsync().ConfigureAwait(true);

        if (!puertos.Success)
        {
            MostrarError(puertos.Error);
            return;
        }

        var lista = puertos.Value!;
        var contenedores = await ContenedoresPorPuertoAsync(lista).ConfigureAwait(true);

        Tabla.ItemsSource = lista
            .Select(p =>
            {
                var app = AplicacionesConocidas.Reconocer(p.Process);

                var contenedor = contenedores.GetValueOrDefault(p.Port);

                return new Fila(
                    p.Port.ToString(),
                    p.Protocol,
                    Escucha(p.Address),

                    p.Process ?? "(sin permiso para verlo)",
                    contenedor is not null
                        ? (Geometry)FindResource("IconoPanelDocker")
                        : app is null ? null : (Geometry)FindResource(IconosDeAplicacion.Glifo(app.Clase)),
                    contenedor is not null
                        ? (Brush)FindResource("IconoAzul")
                        : app is null ? null : (Brush)FindResource(IconosDeAplicacion.Color(app)),
                    contenedor ?? app?.Nombre,
                    p.Pid,
                    EstadoDeTunel(p));
            })
            .ToList();

        var expuestos = lista.Count(p => EsExpuesto(p.Address));

        MostrarResumen(expuestos > 0
            ? $"{lista.Count} puerto(s) a la escucha · {expuestos} accesible(s) desde la red"
            : $"{lista.Count} puerto(s) a la escucha, todos locales");
    }

    /// <summary>Traduce la dirección de escucha a algo que se entienda de un vistazo.</summary>
    private static string Escucha(string direccion) => direccion switch
    {
        "0.0.0.0" or "*" or "[::]" or "::" => "todo",
        "127.0.0.1" or "[::1]" or "::1" => "local",
        _ => direccion,
    };

    private static bool EsExpuesto(string direccion) =>
        direccion is "0.0.0.0" or "*" or "[::]" or "::";
}

/// <summary>El registro de un proceso de supervisord: se lee con <c>supervisorctl tail</c> y se sigue con <c>tail -F</c> sobre los archivos que el proceso tiene abiertos (FR-185, FR-185a).</summary>
[SupportedOSPlatform("windows")]
public sealed class RegistroDeProcesoSupervisado(
    PlatformInventory inventario, IPlatformCommandRunner? canal, string proceso)
    : IFuenteDeRegistro
{
    private const int TimeoutSegundos = 20;

    private IReadOnlyList<string> _rutas = [];

    public string Titulo => $"Registro de {proceso}";

    public string? PorQueNoSigue { get; private set; }

    public Task<InventoryResult<string>> LeerAsync(CancellationToken ct = default) =>
        inventario.GetSupervisorLogAsync(proceso, ct: ct);

    public async Task<IReadOnlyList<ArchivoSeguido>> ArchivosAsync(CancellationToken ct = default)
    {
        if (canal is null)
        {
            return [];
        }

        var rutas = await ResolverAsync(reusar: true, ct).ConfigureAwait(false);
        var comando = SeguimientoDeArchivo.ComandoDeFechas(rutas);

        if (comando.Length == 0)
        {
            return [];
        }

        var (_, salida, _) = await canal
            .RunWithSudoAsync(comando, TimeoutSegundos, ct).ConfigureAwait(false);

        return SeguimientoDeArchivo.LeerFechas(rutas, salida);
    }

    public async Task<IAsyncDisposable?> SeguirAsync(
        Action<string> onLinea, Action<string?> onCerrado, CancellationToken ct = default)
    {
        if (canal is not IPlatformLogStreamer seguidor)
        {
            PorQueNoSigue =
                "Esta sesión no tiene abierto el canal de registro en vivo: volvé a conectar la "
                + "pestaña.";

            return null;
        }

        var rutas = await ResolverAsync(reusar: false, ct).ConfigureAwait(false);

        if (rutas.Count == 0)
        {
            return null;
        }

        return await seguidor
            .SeguirAsync(SeguimientoDeArchivo.Comando(rutas), onLinea, onCerrado, ct)
            .ConfigureAwait(false);
    }

    /// <summary>supervisord no dice dónde escribe cada proceso: las rutas salen de las salidas que el proceso tiene abiertas.</summary>
    private async Task<IReadOnlyList<string>> ResolverAsync(bool reusar, CancellationToken ct)
    {
        if (canal is null)
        {
            PorQueNoSigue = "Esta sesión no tiene abierto el canal de comandos del servidor.";
            return [];
        }

        if (reusar && _rutas.Count > 0)
        {
            return _rutas;
        }

        var procesos = await inventario.GetSupervisorAsync(ct).ConfigureAwait(false);

        var detalle = procesos.Value?
            .FirstOrDefault(p => p.Name.Equals(proceso, StringComparison.Ordinal))?.Detail;

        if (SeguimientoDeArchivo.PidDeSupervisor(detalle) is not { } pid)
        {
            PorQueNoSigue =
                "supervisord no informa un pid para este proceso: está detenido y no hay archivo "
                + "abierto que seguir.";

            _rutas = [];
            return _rutas;
        }

        var (_, salida, _) = await canal.RunWithSudoAsync(
                SeguimientoDeArchivo.ComandoDeRegistrosAbiertos(pid), TimeoutSegundos, ct)
            .ConfigureAwait(false);

        _rutas = SeguimientoDeArchivo.LeerRegistrosAbiertos(salida);

        if (_rutas.Count == 0)
        {
            PorQueNoSigue =
                "No se pudo resolver a qué archivo escribe el proceso: puede que supervisord no le "
                + "haya configurado ninguno, o que el permiso no alcance para verlo.";
        }

        return _rutas;
    }
}
