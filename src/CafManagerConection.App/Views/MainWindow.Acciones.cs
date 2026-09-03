using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CafManagerConection.App.Panels;
using CafManagerConection.App.Services;
using CafManagerConection.Infrastructure;
using CafManagerConection.App.ViewModels;
using CafManagerConection.Domain.Connections;
using CafManagerConection.UseCases;
using CafManagerConection.UseCases.Abstractions;
using CafManagerConection.UseCases.Connections;

namespace CafManagerConection.App.Views;

/// <summary>Acciones del árbol: menú contextual, alta, edición, borrado, copiado y arrastre.</summary>
[SupportedOSPlatform("windows")]
public partial class MainWindow
{
    // No hay glifo de edición en Themes/Estilos.xaml; mismo lienzo de 20x20 que el resto.
    /// <summary>Editar (lápiz), para «Editar…» y «Editar carpeta…».</summary>
    private static readonly Geometry IconoEditar = Geometry.Parse(
        "F1 M17.414 2.586a2 2 0 00-2.828 0L7 10.172V13h2.828l7.586-7.586a2 2 0 000-2.828z "
        + "M2 6a2 2 0 012-2h4a1 1 0 010 2H4v10h10v-4a1 1 0 112 0v4a2 2 0 01-2 2H4a2 2 0 01-2-2V6z");

    /// <summary>Eliminar (tacho), para toda acción que borra algo del árbol. Va siempre con el pincel «Destructivo» y no con el color de texto normal: borrar es lo que más conviene distinguir de un vistazo entre las acciones de un menú, y el mismo icono con el mismo color que «Editar» o «Duplicar» perdería esa diferencia.</summary>
    private static readonly Geometry IconoEliminar = Geometry.Parse(
        "F1 M8.75 1A2.75 2.75 0 006 3.75v.443c-.795.077-1.584.176-2.365.298a.75.75 0 10.23 "
        + "1.482l.149-.022.841 10.518A2.75 2.75 0 007.596 19h4.807a2.75 2.75 0 002.742-2.53l."
        + "841-10.52.149.023a.75.75 0 00.23-1.482A41.03 41.03 0 0014 4.193V3.75A2.75 2.75 0 0011."
        + "25 1h-2.5zM10 4c.84 0 1.673.025 2.5.075V3.75c0-.69-.56-1.25-1.25-1.25h-2.5c-.69 0-1.25"
        + ".56-1.25 1.25v.325C8.327 4.025 9.16 4 10 4zM8.58 7.72a.75.75 0 00-1.5.06l.3 7.5a.75.75"
        + " 0 101.5-.06l-.3-7.5zm4.34.06a.75.75 0 10-1.5-.06l-.3 7.5a.75.75 0 101.5.06l.3-7.5z");

    /// <summary>Nueva conexión / nueva carpeta (más).</summary>
    private static readonly Geometry IconoNuevo = Geometry.Parse(
        "F1 M10 4C10.55 4 11 4.45 11 5V9H15C15.55 9 16 9.45 16 10C16 10.55 15.55 11 15 11H11V15"
        + "C11 15.55 10.55 16 10 16C9.45 16 9 15.55 9 15V11H5C4.45 11 4 10.55 4 10C4 9.45 4.45 9 5"
        + " 9H9V5C9 4.45 9.45 4 10 4Z");

    /// <summary>Selecciona el nodo bajo el puntero antes de abrir el menú.</summary>
    private void AlClicDerecho(object sender, MouseButtonEventArgs e)
    {
        if (BuscarNodoVisual(e.OriginalSource as DependencyObject) is { } item)
        {
            item.IsSelected = true;
        }
        else
        {
            if (_arbol.SelectedItem is NodoArbol previo)
            {
                previo.Seleccionado = false;
            }
        }

        _arbol.ContextMenu = ArmarMenu(BuscarNodoVisual(e.OriginalSource as DependencyObject) is null
            ? null
            : Elegido);
    }

    private static TreeViewItem? BuscarNodoVisual(DependencyObject? origen)
    {
        while (origen is not null and not TreeViewItem)
        {
            origen = System.Windows.Media.VisualTreeHelper.GetParent(origen);
        }

        return origen as TreeViewItem;
    }

    /// <summary>Arma el menú según qué se eligió.</summary>
    private ContextMenu ArmarMenu(NodoArbol? nodo)
    {
        var menu = new ContextMenu();

        var textoIcono = (Brush)FindResource("Texto");
        var destructivo = (Brush)FindResource("Destructivo");

        void Agregar(
            string texto, Action accion, bool destacado = false, Geometry? icono = null,
            Brush? color = null) =>
            menu.Items.Add(MenuIconos.Item(texto, accion, destacado, icono, color ?? textoIcono));

        void Separar() => menu.Items.Add(new Separator());

        switch (nodo)
        {
            case { EsCarpeta: false, Protocolo: Protocol.Web, Conexion: { } web }:
                Agregar("Abrir en el navegador", () => AbrirWeb(web), destacado: true);
                Separar();
                Agregar("Copiar dirección", () => _ = CopiarDestinoAsync(nodo));
                Agregar("Copiar usuario", () => CopiarUsuario(nodo));
                Agregar("Copiar contraseña", () => _ = CopiarSecretoAsync(nodo));
                Separar();
                menu.Items.Add(MenuDeEtiquetas(nodo));
                Agregar("Editar…", () => _ = EditarAsync(nodo), icono: IconoEditar);
                Agregar(
                    "Duplicar", () => _ = DuplicarAsync(nodo),
                    icono: (Geometry)FindResource("IconoCopiarTodo"));
                Agregar(
                    "Eliminar", () => _ = EliminarAsync(nodo), icono: IconoEliminar,
                    color: destructivo);
                break;

            case { EsCarpeta: false, Conexion: { } conexion }:
                Agregar("Conectar", () => AbrirSesion(conexion), destacado: true);
                Agregar("Abrir otra sesión", () => AbrirSesion(conexion, forzarNueva: true));

                AgregarHerramientasExternas(conexion, Agregar, Separar);

                Separar();
                Agregar("Copiar host", () => _ = CopiarDestinoAsync(nodo));
                Agregar("Copiar usuario", () => CopiarUsuario(nodo));
                Agregar("Copiar contraseña", () => _ = CopiarSecretoAsync(nodo));
                Separar();
                menu.Items.Add(MenuDeEtiquetas(nodo));
                Agregar("Editar…", () => _ = EditarAsync(nodo), icono: IconoEditar);
                Agregar(
                    "Túneles…", () => _ = EditarTunelesAsync(nodo),
                    icono: (Geometry)FindResource("IconoPanelTuneles"));
                Agregar(
                    "Duplicar", () => _ = DuplicarAsync(nodo),
                    icono: (Geometry)FindResource("IconoCopiarTodo"));
                Agregar(
                    "Eliminar", () => _ = EliminarAsync(nodo), icono: IconoEliminar,
                    color: destructivo);
                break;

            case { EsCarpeta: true }:
                Agregar("Abrir todas las conexiones", () => AbrirTodasAsync(nodo), destacado: true);
                Separar();
                Agregar(
                    "Nueva conexión aquí…", () => _ = NuevaConexionAsync(nodo.Id),
                    icono: IconoNuevo);
                Agregar(
                    "Nueva subcarpeta…", () => _ = NuevaCarpetaAsync(nodo.Id),
                    icono: (Geometry)FindResource("IconoCarpeta"));
                Agregar("Ordenar alfabéticamente", () => _ = OrdenarHijosAsync(nodo));
                Separar();
                menu.Items.Add(MenuDeEtiquetas(nodo));
                Agregar("Editar carpeta…", () => _ = EditarCarpetaAsync(nodo), icono: IconoEditar);
                Agregar("Renombrar…", () => _ = RenombrarCarpetaAsync(nodo));
                Separar();
                Agregar(
                    "Eliminar carpeta", () => _ = EliminarAsync(nodo), icono: IconoEliminar,
                    color: destructivo);
                break;

            default:
                Agregar(
                    "Nueva conexión…", () => _ = NuevaConexionAsync(), destacado: true,
                    icono: IconoNuevo);
                Agregar(
                    "Nueva carpeta…", () => _ = NuevaCarpetaAsync(),
                    icono: (Geometry)FindResource("IconoCarpeta"));
                Separar();
                Agregar(
                    "Actualizar", () => _ = RefrescarArbolAsync(_busqueda.Text),
                    icono: (Geometry)FindResource("IconoReconectar"));
                break;
        }

        return menu;
    }

    /// <summary>Submenú con el catálogo de etiquetas y la opción de quitarla, sin abrir el editor (FR-190).</summary>
    private MenuItem MenuDeEtiquetas(NodoArbol nodo)
    {
        var actual = nodo.EsCarpeta ? nodo.EtiquetaPropia : nodo.Conexion?.Etiqueta;

        // La actual va en el encabezado: así se ve sin abrir el submenú, que es de lo que sirve
        // para cambiarla rápido.
        var submenu = new MenuItem
        {
            Header = actual is null ? "Etiquetas" : $"Etiquetas · {actual.Codigo}",
        };

        foreach (var etiqueta in _etiquetas)
        {
            submenu.Items.Add(ItemDeEtiqueta(nodo, etiqueta, esActual: actual?.Id == etiqueta.Id));
        }

        if (_etiquetas.Count > 0)
        {
            submenu.Items.Add(new Separator());
        }

        submenu.Items.Add(ItemDeEtiqueta(nodo, null, esActual: actual is null));
        return submenu;
    }

    private MenuItem ItemDeEtiqueta(NodoArbol nodo, Etiqueta? etiqueta, bool esActual)
    {
        var texto = etiqueta is null ? "Sin etiqueta" : $"{etiqueta.Codigo} · {etiqueta.Nombre}";

        var item = new MenuItem
        {
            // El tilde y no IsChecked: WPF lo dibuja en la ranura del icono y taparía el color.
            Header = esActual ? $"{texto}    ✓" : texto,
            FontWeight = esActual ? FontWeights.SemiBold : FontWeights.Normal,
            IsEnabled = !esActual,
        };

        if (etiqueta is not null)
        {
            item.Icon = new System.Windows.Shapes.Ellipse
            {
                Width = 10,
                Height = 10,
                Fill = TryFindResource(etiqueta.ClaveDePincel) as Brush
                       ?? (Brush)FindResource("Texto"),
            };
        }

        var id = etiqueta?.Id;
        item.Click += (_, _) => _ = AsignarEtiquetaAsync(nodo, id);
        return item;
    }

    private async Task AsignarEtiquetaAsync(NodoArbol nodo, Guid? etiqueta)
    {
        try
        {
            var resultado = nodo.EsCarpeta
                ? await _root.FolderService.SetTagAsync(nodo.Id, etiqueta).ConfigureAwait(true)
                : await _root.ConnectionService.SetTagAsync(nodo.Id, etiqueta).ConfigureAwait(true);

            if (AvisarSiFallo(resultado, "No se pudo cambiar la etiqueta"))
            {
                return;
            }

            await RefrescarArbolAsync(_busqueda.Text).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _root.Logger.TechnicalError("cambiar la etiqueta desde el menú contextual", ex);
            Dialogos.Advertir(this, "No se pudo cambiar la etiqueta", ex.Message);
        }
    }

    /// <summary>Ordena alfabéticamente los hijos directos de la carpeta, no los de sus subcarpetas (FR-193c).</summary>
    private async Task OrdenarHijosAsync(NodoArbol carpeta)
    {
        try
        {
            var resultado = await _root.FolderService
                .OrdenarHijosAsync(carpeta.Id).ConfigureAwait(true);

            if (AvisarSiFallo(resultado, "No se pudo ordenar"))
            {
                return;
            }

            await RefrescarArbolAsync(_busqueda.Text).ConfigureAwait(true);
            _estado.Text = $"«{carpeta.Nombre}» ordenada alfabéticamente";
        }
        catch (Exception ex)
        {
            _root.Logger.TechnicalError("ordenar alfabéticamente una carpeta", ex);
            Dialogos.Advertir(this, "No se pudo ordenar", ex.Message);
        }
    }

    /// <summary>Agrega «Abrir en …» por cada herramienta externa instalada (FR-143).</summary>
    private void AgregarHerramientasExternas(
        ConnectionSummary conexion, Action<string, Action, bool, Geometry?, Brush?> agregar,
        Action separar)
    {
        if (conexion.Protocol != Protocol.Ssh)
        {
            return;
        }

        var instaladas = _root.Herramientas.Instaladas.ToList();

        if (instaladas.Count == 0)
        {
            return;
        }

        separar();

        var iconoExterna = (Geometry)FindResource("IconoTerminalExterna");

        foreach (var herramienta in instaladas)
        {
            var nombre = LanzadorExterno.Nombre(herramienta);
            var cual = herramienta;

            agregar(
                $"Abrir en {nombre}", () => _ = AbrirExternaAsync(cual, conexion), false,
                iconoExterna, null);
        }
    }

    /// <summary>Lanza la herramienta con host, usuario y puerto —y la clave si hay—, nunca la contraseña.</summary>
    private async Task AbrirExternaAsync(
        HerramientaExterna herramienta, ConnectionSummary conexion)
    {
        if (_root.Herramientas.Ruta(herramienta) is not { } ejecutable)
        {
            return;
        }

        string? clave = null;
        var porContrasena = false;

        try
        {
            var detalle = await _root.ConnectionService
                .GetDetailAsync(conexion.Id).ConfigureAwait(true);

            if (detalle is not null)
            {
                var resolver = await _root.ConnectionService
                    .CreateResolverAsync().ConfigureAwait(true);

                var efectivo = resolver.Resolve(detalle.Connection, detalle.Rdp, detalle.Ssh);

                clave = efectivo.PrivateKeyPath.Value;
                porContrasena = efectivo.ResolvedAuthMethod == SshAuthMethod.Password;
            }
        }
        catch (Exception ex)
        {
            _root.Logger.TechnicalError(
                "resolver la clave privada para abrir una herramienta externa", ex);
        }

        if (porContrasena)
        {
            AvisarQueLaHerramientaPideLaContrasena(herramienta);
        }

        var destino = new DestinoRemoto(
            conexion.Host, conexion.EffectivePort, conexion.EffectiveUserName, clave);

        var error = LanzadorExterno.Abrir(herramienta, ejecutable, destino);

        _estado.Text = error
                       ?? $"{LanzadorExterno.Nombre(herramienta)} abierto en {conexion.Host}";
    }

    /// <summary>La contraseña no se le entrega a ninguna herramienta (FR-143b): se avisa que la va a pedir ella (FR-188a).</summary>
    private void AvisarQueLaHerramientaPideLaContrasena(HerramientaExterna herramienta)
    {
        var nombre = LanzadorExterno.Nombre(herramienta);

        Dialogos.Informar(
            this,
            $"{nombre} va a pedir la contraseña",
            $"Esta conexión se autentica por contraseña, y la aplicación no se la entrega a "
            + $"ninguna herramienta externa. {nombre} la va a pedir en su propia ventana.");
    }

    private void AlPedirNuevaConexion(object sender, RoutedEventArgs e) =>
        _ = NuevaConexionAsync(Elegido is { EsCarpeta: true } c ? c.Id : null);

    private async Task NuevaConexionAsync(Guid? carpeta = null)
    {
        try
        {
            var editor = new ConnectionEditorWindow(_root, carpetaInicial: carpeta) { Owner = this };

            if (editor.ShowDialog() == true)
            {
                await RefrescarArbolAsync(_busqueda.Text).ConfigureAwait(true);
            }
        }
        catch (Exception ex)
        {
            _root.Logger.TechnicalError("abrir el editor de conexión", ex);
            Dialogos.Advertir(this, "No se pudo abrir el editor", ex.Message);
        }
    }

    /// <summary>Pide «usuario@host:puerto» y abre la sesión al instante, sin dejar una entrada en el árbol (FR-149).</summary>
    private async Task ConectarRapidoAsync()
    {
        var texto = "";

        while (true)
        {
            texto = TextPromptWindow.Pedir(
                this, "Conexión rápida", "usuario@host:puerto", texto);

            if (texto is null)
            {
                return;
            }

            if (QuickConnectionTarget.TryParse(
                    texto, out var usuario, out var host, out var puerto, out var error))
            {
                var usuarioEfectivo = usuario ?? Environment.UserName;

                await AbrirConexionRapidaAsync(usuarioEfectivo, host, puerto).ConfigureAwait(true);
                return;
            }

            Dialogos.Advertir(this, "Conexión rápida", error!);
        }
    }

    private async Task AbrirConexionRapidaAsync(string usuario, string host, int puerto)
    {
        var creada = await _root.ConnectionService
            .CreateQuickAsync(usuario, host, puerto).ConfigureAwait(true);

        if (!creada.Success)
        {
            Dialogos.Advertir(this, "Conexión rápida", creada.ErrorMessage ?? "No se pudo conectar.");
            return;
        }

        var id = creada.Value;
        _conexionesRapidas.Add(id);

        _resumenes[id] = new ConnectionSummary(
            id, FolderId: null, Name: $"{usuario}@{host}", Protocol.Ssh, host,
            EffectivePort: puerto, EffectiveUserName: usuario, LastConnectedAt: null, SortOrder: 0);

        var r = await _gestor.OpenAsync(id).ConfigureAwait(true);

        if (!r.Success)
        {
            _estado.Text = r.ErrorMessage ?? "No se pudo abrir la sesión.";

            _conexionesRapidas.Remove(id);
            await _root.ConnectionService.DeleteAsync(id).ConfigureAwait(true);
        }
    }

    /// <summary>Convierte la conexión rápida de una sesión abierta en una conexión guardada de verdad (FR-149).</summary>
    private async Task GuardarConexionRapidaAsync(TabItem pestana, Guid conexionId)
    {
        var editor = new ConnectionEditorWindow(_root, editando: conexionId) { Owner = this };

        if (editor.ShowDialog() != true)
        {
            return;
        }

        await _root.ConnectionService.MarkAsSavedAsync(conexionId).ConfigureAwait(true);
        _conexionesRapidas.Remove(conexionId);

        var detalle = await _root.ConnectionService
            .GetDetailAsync(conexionId).ConfigureAwait(true);

        if (detalle is not null && pestana.Content is SessionView vista)
        {
            pestana.Header = CabeceraDePestana(detalle.Connection.Name, vista.State, pestana);
        }

        _estado.Text = "Conexión guardada";
        await RefrescarArbolAsync(_busqueda.Text).ConfigureAwait(true);
    }

    private async Task EditarAsync(NodoArbol nodo)
    {
        if (nodo.EsCarpeta)
        {
            await EditarCarpetaAsync(nodo).ConfigureAwait(true);
            return;
        }

        var editor = new ConnectionEditorWindow(_root, editando: nodo.Id) { Owner = this };

        if (editor.ShowDialog() == true)
        {
            await RefrescarArbolAsync(_busqueda.Text).ConfigureAwait(true);
        }
    }

    /// <summary>Abre todas las conexiones que cuelgan de una carpeta, incluidas las de sus subcarpetas.</summary>
    private void AbrirTodasAsync(NodoArbol carpeta)
    {
        var conexiones = carpeta.Recorrer()
            .Where(n => !n.EsCarpeta && n.Conexion is not null)
            .Select(n => n.Conexion!)
            .Where(c => _root.Sessions.CountForConnection(c.Id) == 0)
            .ToList();

        if (conexiones.Count == 0)
        {
            _estado.Text = $"«{carpeta.Nombre}» no tiene conexiones sin abrir";
            return;
        }

        var detalle = conexiones.Count == 1
            ? $"Se va a abrir 1 sesión: «{conexiones[0].Name}»."
            : $"Se van a abrir {conexiones.Count} sesiones, una por cada conexión de "
              + $"«{carpeta.Nombre}» que no esté ya abierta.";

        if (conexiones.Count > 10)
        {
            detalle += Environment.NewLine + Environment.NewLine
                + "Son bastantes: cada una abre su propia conexión al servidor y puede tardar.";
        }

        if (!Dialogos.Confirmar(this, "Abrir todas", detalle, "Abrir"))
        {
            return;
        }

        foreach (var c in conexiones)
        {
            AbrirSesion(c);
        }
    }

    private async Task EditarCarpetaAsync(NodoArbol nodo)
    {
        var editor = new FolderSettingsWindow(_root, nodo.Id) { Owner = this };

        if (editor.ShowDialog() == true)
        {
            await RefrescarArbolAsync(_busqueda.Text).ConfigureAwait(true);
        }
    }

    private async Task EditarTunelesAsync(NodoArbol nodo)
    {
        var editor = new TunnelEditorWindow(_root, nodo.Id) { Owner = this };
        editor.ShowDialog();

        await Task.CompletedTask.ConfigureAwait(true);
    }

    private async Task NuevaCarpetaAsync(Guid? padre = null)
    {
        var nombre = TextPromptWindow.Pedir(this, "Nueva carpeta", "Nombre de la carpeta");

        if (nombre is null)
        {
            return;
        }

        await _root.FolderService.CreateAsync(nombre, padre).ConfigureAwait(true);
        await RefrescarArbolAsync(_busqueda.Text).ConfigureAwait(true);
    }

    private async Task RenombrarCarpetaAsync(NodoArbol nodo)
    {
        var nombre = TextPromptWindow.Pedir(
            this, "Renombrar carpeta", "Nuevo nombre", nodo.Nombre);

        if (nombre is null || nombre == nodo.Nombre)
        {
            return;
        }

        await _root.FolderService.RenameAsync(nodo.Id, nombre).ConfigureAwait(true);
        await RefrescarArbolAsync(_busqueda.Text).ConfigureAwait(true);
    }

    private async Task DuplicarAsync(NodoArbol nodo)
    {
        if (nodo.Conexion is null)
        {
            return;
        }

        await _root.ConnectionService.DuplicateAsync(nodo.Id).ConfigureAwait(true);
        await RefrescarArbolAsync(_busqueda.Text).ConfigureAwait(true);
    }

    private async Task EliminarAsync(NodoArbol nodo)
    {
        if (nodo.EsCarpeta)
        {
            var impacto = await _root.FolderService
                .GetDeletionImpactAsync(nodo.Id).ConfigureAwait(true);

            var contiene = (impacto.FolderCount - 1) + impacto.ConnectionCount;

            if (contiene == 0)
            {
                if (!Dialogos.Confirmar(
                        this,
                        "Eliminar carpeta",
                        $"Se va a eliminar la carpeta «{nodo.Nombre}», que está vacía.",
                        "Eliminar"))
                {
                    return;
                }
            }
            else
            {
                var detalle =
                    $"«{nodo.Nombre}» contiene {contiene} elemento(s), y se eliminan todos "
                    + "junto con sus subcarpetas, conexiones, túneles e historial."
                    + $"{Environment.NewLine}{Environment.NewLine}"
                    + "Esto no se puede deshacer.";

                if (!Dialogos.ConfirmarEnCascada(
                        this, "Eliminar carpeta", detalle, nodo.Nombre))
                {
                    return;
                }
            }

            await _root.FolderService.DeleteAsync(nodo.Id).ConfigureAwait(true);
        }
        else
        {
            var abiertas = _root.Sessions.CountForConnection(nodo.Id);

            var aviso = abiertas > 0
                ? $"«{nodo.Nombre}» tiene una sesión abierta, que se va a cerrar."
                : $"Se va a eliminar «{nodo.Nombre}» y su credencial guardada.";

            var servicios = nodo.Hijos.Count;

            if (servicios > 0)
            {
                aviso += servicios == 1
                    ? $"{Environment.NewLine}{Environment.NewLine}"
                      + $"También se elimina el servicio que cuelga de ella: "
                      + $"«{nodo.Hijos[0].Nombre}»."
                    : $"{Environment.NewLine}{Environment.NewLine}"
                      + $"También se eliminan los {servicios} servicios que cuelgan de ella: "
                      + string.Join(", ", nodo.Hijos.Select(h => $"«{h.Nombre}»")) + ".";
            }

            if (servicios > 0)
            {
                aviso += $"{Environment.NewLine}{Environment.NewLine}Esto no se puede deshacer.";

                if (!Dialogos.ConfirmarEnCascada(
                        this, "Eliminar conexión", aviso, nodo.Nombre))
                {
                    return;
                }
            }
            else if (!Dialogos.Confirmar(this, "Eliminar conexión", aviso, "Eliminar"))
            {
                return;
            }

            foreach (var pestana in _sesiones.Items.OfType<TabItem>()
                         .Where(t => t.Content is SessionView v && v.ConnectionId == nodo.Id)
                         .ToList())
            {
                CerrarPestana(pestana);
            }

            await _root.ConnectionService.DeleteAsync(nodo.Id).ConfigureAwait(true);
        }

        await RefrescarArbolAsync(_busqueda.Text).ConfigureAwait(true);
    }

    /// <summary>Menú de configuración, colgado del botón de la barra lateral.</summary>
    private void AlAbrirAjustes(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu
        {
            PlacementTarget = _botonAjustes,
            Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom,
        };

        var textoIcono = (Brush)FindResource("Texto");

        void Agregar(string texto, Action accion, Geometry? icono = null) =>
            menu.Items.Add(MenuIconos.Item(texto, accion, icono: icono, color: textoIcono));

        Agregar(
            "Conexión rápida… (Ctrl+K)", () => _ = ConectarRapidoAsync(),
            (Geometry)FindResource("IconoSsh"));
        menu.Items.Add(new Separator());
        Agregar("Preferencias…", () => _ = PreferenciasAsync(), (Geometry)FindResource("IconoAjustes"));
        menu.Items.Add(new Separator());
        Agregar("Historial de conexiones…", HistorialDeConexiones);
        Agregar("Consola de traza (F12)", () => AlternarConsola());
        Agregar("Comandos guardados…", AdministrarComandos);
        menu.Items.Add(new Separator());
        Agregar(
            "Color de los iconos…", () => _ = ColoresDeIconosAsync(),
            (Geometry)FindResource("IconoPaleta"));
        Agregar("Cambiar el tema", () => AlCambiarTema(_botonTema, new RoutedEventArgs()));
        menu.Items.Add(new Separator());
        Agregar(
            "Restablecer el ancho de los paneles", () => _ = RestablecerAnchosAsync(),
            (Geometry)FindResource("IconoRestablecer"));

        menu.IsOpen = true;
    }

    /// <summary>Olvida los anchos guardados de los paneles laterales.</summary>
    private async Task RestablecerAnchosAsync()
    {
        await _root.AppSettings.ResetPanelWidthsAsync().ConfigureAwait(true);

        _estado.Text = "Ancho de los paneles restablecido · vale para las sesiones nuevas";
    }

    /// <summary>Abre la paleta sin sesión, para administrar la lista (FR-147).</summary>
    private void AdministrarComandos()
    {
        var ventana = new PaletaDeComandosWindow(_root, null, null, null) { Owner = this };
        ventana.ShowDialog();
    }

    private async Task PreferenciasAsync()
    {
        var ventana = new PreferenciasWindow(_root) { Owner = this };
        ventana.ShowDialog();

        await RefrescarArbolAsync(_busqueda.Text).ConfigureAwait(true);
    }

    private void HistorialDeConexiones()
    {
        var ventana = new ConnectionHistoryWindow(_root) { Owner = this };
        ventana.ShowDialog();
    }

    private async Task ColoresDeIconosAsync()
    {
        var actuales = await _root.AppSettings.GetIconColorsAsync().ConfigureAwait(true);
        var ventana = new IconColorsWindow(_root, actuales) { Owner = this };

        if (ventana.ShowDialog() == true)
        {
            _estado.Text = "Color de los iconos actualizado";
        }
    }

    private async Task CopiarDestinoAsync(NodoArbol nodo)
    {
        if (nodo.Conexion is not { } c)
        {
            return;
        }

        if (c.Protocol == Protocol.Web)
        {
            var registro = await _root.ConnectionService
                .GetDetailAsync(nodo.Id).ConfigureAwait(true);

            _portapapeles.CopyText(DireccionParaCopiar(registro, c.Host));
            return;
        }

        _portapapeles.CopyText(c.Host);
    }

    /// <summary>Decide qué texto copiar como dirección: la URL completa de una entrada web si se pudo leer el detalle, o el host como respaldo si la conexión se borró entre el clic y la consulta.</summary>
    public static string DireccionParaCopiar(ConnectionRecord? registro, string host) =>
        registro?.Web?.Url is { Length: > 0 } url ? url : host;

    private void CopiarUsuario(NodoArbol nodo)
    {
        if (nodo.Conexion is { } c && !string.IsNullOrEmpty(c.EffectiveUserName))
        {
            _portapapeles.CopyText(c.EffectiveUserName);
        }
        else
        {
            _estado.Text = "Esta conexión no tiene usuario definido ni heredado";
        }
    }

    private async Task CopiarSecretoAsync(NodoArbol nodo)
    {
        if (nodo.Conexion is null)
        {
            return;
        }

        var registro = await _root.ConnectionService
            .GetDetailAsync(nodo.Id).ConfigureAwait(true);

        if (registro is null)
        {
            return;
        }

        var resolver = await _root.ConnectionService.CreateResolverAsync().ConfigureAwait(true);
        var efectivo = resolver.Resolve(registro.Connection, registro.Rdp, registro.Ssh);

        if (efectivo.CredentialKey.Value is not { } clave)
        {
            _estado.Text = "Esta conexión no tiene credencial guardada";
            return;
        }

        using var credencial = await _root.Credentials.ReadAsync(clave).ConfigureAwait(true);

        if (credencial is null)
        {
            _estado.Text = "No se encontró la credencial en el Administrador de credenciales";
            return;
        }

        _portapapeles.CopySecret(credencial.RevealSecret());
    }

    private void AbrirWeb(ConnectionSummary conexion)
    {
        _ = AbrirWebAsync(conexion);
    }

    private async Task AbrirWebAsync(ConnectionSummary conexion)
    {
        try
        {
            var registro = await _root.ConnectionService
                .GetDetailAsync(conexion.Id).ConfigureAwait(true);

            if (registro?.Web is { } web)
            {
                WebLauncher.Open(web);
                _estado.Text = $"Abierto en el navegador · {web.Url}";

                await _root.Connections
                    .SetLastConnectedAsync(conexion.Id, DateTimeOffset.UtcNow)
                    .ConfigureAwait(true);
            }
        }
        catch (Exception ex)
        {
            _root.Logger.TechnicalError("abrir la entrada web", ex);
            Dialogos.Advertir(
                this, "No se pudo abrir", "No se pudo abrir la dirección en el navegador.");
        }
    }

    private void AlPresionarBotonIzquierdo(object sender, MouseButtonEventArgs e) =>
        _origenArrastre = e.GetPosition(null);

    private void AlMoverSobreArbol(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _arrastrando is not null)
        {
            return;
        }

        var recorrido = e.GetPosition(null) - _origenArrastre;

        if (Math.Abs(recorrido.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(recorrido.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        if (Elegido is not { } nodo)
        {
            return;
        }

        _arrastrando = nodo;

        try
        {
            DragDrop.DoDragDrop(_arbol, nodo, DragDropEffects.Move);
        }
        finally
        {
            _arrastrando = null;
        }
    }

    private void AlArrastrarSobreArbol(object sender, DragEventArgs e)
    {
        var destino = DestinoDeArrastre(e);

        e.Effects = PorQueNoSePuedeSoltar(destino, ZonaDe(e)) is null
            ? DragDropEffects.Move
            : DragDropEffects.None;

        e.Handled = true;

        MarcarDondeCae(e, destino);
    }

    /// <summary>Dónde se está por soltar respecto de la fila que está debajo del puntero.</summary>
    private enum ZonaDeSoltado
    {
        /// <summary>En el medio: la acción de siempre —colgar como servicio, o mover a la carpeta—.</summary>
        Encima,

        /// <summary>En el cuarto de arriba: reordenar, quedando antes de esa fila.</summary>
        Antes,

        /// <summary>En el cuarto de abajo: reordenar, quedando después de esa fila.</summary>
        Despues,
    }

    /// <summary>En qué parte de la fila cayó el puntero.</summary>
    private static ZonaDeSoltado ZonaDe(DragEventArgs e)
    {
        if (BuscarNodoVisual(e.OriginalSource as DependencyObject) is not { } fila
            || fila.ActualHeight <= 0)
        {
            return ZonaDeSoltado.Encima;
        }

        var alto = fila.ActualHeight;

        if (VisualTreeHelper.GetChildrenCount(fila) > 0
            && VisualTreeHelper.GetChild(fila, 0) is FrameworkElement contenido
            && contenido.ActualHeight > 0)
        {
            alto = contenido.ActualHeight;
        }

        var y = e.GetPosition(fila).Y;

        return y < alto * 0.25
            ? ZonaDeSoltado.Antes
            : y > alto * 0.75
                ? ZonaDeSoltado.Despues
                : ZonaDeSoltado.Encima;
    }

    /// <summary>Línea que muestra entre qué dos filas va a caer lo que se arrastra.</summary>
    private System.Windows.Documents.AdornerLayer? _capaDeArrastre;
    private System.Windows.Documents.Adorner? _marcaDeArrastre;

    private void MarcarDondeCae(DragEventArgs e, NodoArbol? destino)
    {
        BorrarMarcaDeArrastre();

        var zona = ZonaDe(e);

        if (zona == ZonaDeSoltado.Encima
            || !PuedeCaerEntreFilas(destino)
            || PorQueNoSePuedeSoltar(destino, zona) is not null
            || BuscarNodoVisual(e.OriginalSource as DependencyObject) is not { } fila)
        {
            return;
        }

        _capaDeArrastre = System.Windows.Documents.AdornerLayer.GetAdornerLayer(fila);

        if (_capaDeArrastre is null)
        {
            return;
        }

        _marcaDeArrastre = new LineaDeInsercion(fila, zona == ZonaDeSoltado.Despues);
        _capaDeArrastre.Add(_marcaDeArrastre);
    }

    private void BorrarMarcaDeArrastre()
    {
        if (_capaDeArrastre is not null && _marcaDeArrastre is not null)
        {
            _capaDeArrastre.Remove(_marcaDeArrastre);
        }

        _capaDeArrastre = null;
        _marcaDeArrastre = null;
    }

    /// <summary>Si lo arrastrado puede caer entre dos filas del destino: los dos tienen que ser de la misma clase, porque el árbol dibuja las carpetas antes que las conexiones y cada clase lleva su propia numeración (FR-193, FR-193b).</summary>
    private bool PuedeCaerEntreFilas(NodoArbol? destino) =>
        _arrastrando is { } origen
        && destino is { } fila
        && !ReferenceEquals(origen, fila)
        && origen.EsCarpeta == fila.EsCarpeta;

    private async void AlSoltarEnArbol(object sender, DragEventArgs e)
    {
        try
        {
            await SoltarEnArbolAsync(e).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _root.Logger.TechnicalError("soltar un elemento en el árbol", ex);
            Dialogos.Advertir(this, "No se pudo mover", ex.Message);
        }
    }

    private async Task SoltarEnArbolAsync(DragEventArgs e)
    {
        var zona = ZonaDe(e);
        BorrarMarcaDeArrastre();

        if (_arrastrando is not { } origen)
        {
            return;
        }

        var destino = DestinoDeArrastre(e);

        if (PorQueNoSePuedeSoltar(destino, zona) is { } motivo)
        {
            _estado.Text = motivo;
            return;
        }

        if (zona != ZonaDeSoltado.Encima && PuedeCaerEntreFilas(destino))
        {
            await ReordenarAsync(origen, destino!, zona == ZonaDeSoltado.Despues)
                .ConfigureAwait(true);

            return;
        }

        if (!origen.EsCarpeta && destino is { EsCarpeta: false })
        {
            await ColgarAsync(origen, destino).ConfigureAwait(true);
            return;
        }

        var carpeta = destino switch
        {
            { EsCarpeta: true } c => c.Id,
            { } c => CarpetaDe(c),
            _ => null,
        };

        await MoverAsync(origen, carpeta).ConfigureAwait(true);
    }

    /// <summary>La carpeta donde vive un hermano del nodo: la propia de una conexión, o la que contiene a una carpeta.</summary>
    private static Guid? CarpetaDe(NodoArbol nodo) =>
        nodo.EsCarpeta ? nodo.Padre?.Id : nodo.Conexion?.FolderId;

    /// <summary>Deja lo arrastrado antes o después del destino. Si ya son hermanos alcanza con renumerar; si no, es un movimiento a esa posición (FR-193, FR-193b).</summary>
    private async Task ReordenarAsync(NodoArbol origen, NodoArbol destino, bool despues)
    {
        var hermanos = (destino.Padre?.Hijos ?? _raiz)
            .Where(n => n.EsCarpeta == origen.EsCarpeta)
            .Select(n => n.Id)
            .ToList();

        var indice = hermanos.IndexOf(destino.Id);

        if (indice < 0)
        {
            AvisarQueNoSePudoReordenar($"el destino {destino.Id} no está entre los hermanos");
            return;
        }

        var propio = hermanos.IndexOf(origen.Id);

        if (propio < 0)
        {
            await MoverAsync(origen, CarpetaDe(destino), despues ? indice + 1 : indice)
                .ConfigureAwait(true);

            return;
        }

        hermanos.RemoveAt(propio);

        if (propio < indice)
        {
            indice--;
        }

        hermanos.Insert(despues ? indice + 1 : indice, origen.Id);

        var carpeta = CarpetaDe(destino);

        if (origen.EsCarpeta)
        {
            await _root.FolderService.ReorderAsync(carpeta, hermanos).ConfigureAwait(true);
        }
        else
        {
            await _root.ConnectionService.ReorderAsync(carpeta, hermanos).ConfigureAwait(true);
        }

        await RefrescarArbolAsync(_busqueda.Text).ConfigureAwait(true);
    }

    private void AvisarQueNoSePudoReordenar(string motivo)
    {
        _root.Logger.TechnicalError(
            "reordenar el árbol", new InvalidOperationException(motivo));

        _estado.Text = "No se pudo reordenar: el árbol cambió mientras se arrastraba";
    }

    private bool AvisarSiFallo(OperationResult resultado, string titulo)
    {
        if (resultado.Success)
        {
            return false;
        }

        Dialogos.Advertir(
            this, titulo, resultado.ErrorMessage ?? "No se pudo completar la operación.");

        return true;
    }

    /// <summary>Cuelga una conexión de otra y avisa si no se puede.</summary>
    private async Task ColgarAsync(NodoArbol origen, NodoArbol destino)
    {
        var resultado = await _root.ConnectionService
            .SetParentAsync(origen.Id, destino.Id).ConfigureAwait(true);

        if (AvisarSiFallo(resultado, "No se puede colgar ahí"))
        {
            return;
        }

        await RefrescarArbolAsync(_busqueda.Text).ConfigureAwait(true);
    }

    private NodoArbol? DestinoDeArrastre(DragEventArgs e) =>
        BuscarNodoVisual(e.OriginalSource as DependencyObject)?.DataContext as NodoArbol;

    /// <summary>Por qué no se puede soltar ahí, o <c>null</c> si se puede. Decirlo es lo que distingue un destino rechazado de un movimiento perdido (FR-194).</summary>
    private string? PorQueNoSePuedeSoltar(NodoArbol? destino, ZonaDeSoltado zona)
    {
        if (_arrastrando is not { } origen)
        {
            return "No hay nada que soltar";
        }

        if (destino is null)
        {
            return null;
        }

        for (var actual = destino; actual is not null; actual = actual.Padre)
        {
            if (ReferenceEquals(actual, origen))
            {
                return origen.EsCarpeta
                    ? "Una carpeta no se puede mover dentro de sí misma ni de una de sus subcarpetas"
                    : "Una conexión no se puede colgar de sí misma ni de una que cuelgue de ella";
            }
        }

        if (zona != ZonaDeSoltado.Encima || origen.EsCarpeta || destino.EsCarpeta)
        {
            return null;
        }

        if (destino.Conexion?.ParentConnectionId is not null)
        {
            return "Esa conexión ya cuelga de otra: no se admite un nivel más";
        }

        return origen.Hijos.Count > 0
            ? "La conexión arrastrada tiene servicios colgando: no se admite un nivel más"
            : null;
    }

    private async Task MoverAsync(NodoArbol nodo, Guid? carpetaDestino, int? posicion = null)
    {
        // Cambiar de carpeta se confirma; acomodar dentro de la misma es sólo un orden y no pregunta.
        var cambiaDeCarpeta = CarpetaDe(nodo) != carpetaDestino;

        if (nodo.EsCarpeta)
        {
            if (cambiaDeCarpeta && !ConfirmarMoverCarpeta(nodo, carpetaDestino))
            {
                return;
            }

            var carpetaMovida = await _root.FolderService
                .MoveAsync(nodo.Id, carpetaDestino, posicion).ConfigureAwait(true);

            if (AvisarSiFallo(carpetaMovida, "No se puede mover la carpeta"))
            {
                return;
            }

            await RefrescarArbolAsync(_busqueda.Text).ConfigureAwait(true);
            return;
        }

        var registro = await _root.ConnectionService.GetDetailAsync(nodo.Id).ConfigureAwait(true);

        if (registro is null)
        {
            return;
        }

        var diferencias = cambiaDeCarpeta
            ? await _root.ConnectionService.PreviewMoveAsync(nodo.Id, carpetaDestino)
                .ConfigureAwait(true)
            : [];

        if (cambiaDeCarpeta && !ConfirmarMoverConexion(nodo, carpetaDestino, diferencias))
        {
            return;
        }

        var movida = await _root.ConnectionService
            .MoveAsync(nodo.Id, carpetaDestino, posicion).ConfigureAwait(true);

        if (AvisarSiFallo(movida, "No se puede mover la conexión"))
        {
            return;
        }

        await RefrescarArbolAsync(_busqueda.Text).ConfigureAwait(true);
    }

    private bool ConfirmarMoverConexion(
        NodoArbol nodo, Guid? carpetaDestino, IReadOnlyList<string> diferencias)
    {
        var detalle = $"«{nodo.Nombre}» pasa a {RutaDeCarpeta(carpetaDestino)}.";

        if (diferencias.Count > 0)
        {
            detalle += Environment.NewLine + Environment.NewLine
                       + "Ahí cambian estos valores heredados:" + Environment.NewLine
                       + string.Join(Environment.NewLine, diferencias.Select(d => $"· {d}"));
        }

        return Dialogos.Confirmar(this, "Mover conexión", detalle, "Mover");
    }

    private bool ConfirmarMoverCarpeta(NodoArbol nodo, Guid? carpetaDestino)
    {
        var dentro = nodo.Recorrer().Skip(1).ToList();
        var conexiones = dentro.Count(n => !n.EsCarpeta);
        var subcarpetas = dentro.Count(n => n.EsCarpeta);

        var detalle = $"«{nodo.Nombre}» pasa a {RutaDeCarpeta(carpetaDestino)}";

        detalle += dentro.Count == 0
            ? ", y está vacía."
            : $", con todo lo que contiene: {Cuenta(conexiones, "conexión", "conexiones")}"
              + $" y {Cuenta(subcarpetas, "subcarpeta", "subcarpetas")}.";

        detalle += Environment.NewLine + Environment.NewLine
                   + "Lo que herede de su carpeta actual pasa a heredarlo de la nueva.";

        return Dialogos.Confirmar(this, "Mover carpeta", detalle, "Mover");
    }

    private static string Cuenta(int cuantos, string singular, string plural) =>
        cuantos == 1 ? $"1 {singular}" : $"{cuantos} {plural}";

    /// <summary>La ruta completa, para que se vea en qué nivel del árbol cae.</summary>
    private string RutaDeCarpeta(Guid? carpeta)
    {
        if (carpeta is null)
        {
            return "la raíz del árbol";
        }

        var nodo = _raiz.SelectMany(n => n.Recorrer())
            .FirstOrDefault(n => n.EsCarpeta && n.Id == carpeta);

        if (nodo is null)
        {
            return "otra carpeta";
        }

        var partes = new List<string>();

        for (var actual = nodo; actual is not null; actual = actual.Padre)
        {
            partes.Insert(0, actual.Nombre);
        }

        return $"«{string.Join(" › ", partes)}»";
    }
}
