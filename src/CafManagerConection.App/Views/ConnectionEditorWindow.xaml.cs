using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using CafManagerConection.App.Bootstrap;
using CafManagerConection.Domain.Connections;
using CafManagerConection.Domain.Settings;
using CafManagerConection.Domain.Ssh;
using CafManagerConection.UseCases.Abstractions;
using CafManagerConection.UseCases.Inheritance;
using Microsoft.Win32;

namespace CafManagerConection.App.Views;

/// <summary>Alta y edición de una conexión, con los campos propios de cada protocolo.</summary>
[SupportedOSPlatform("windows")]
public partial class ConnectionEditorWindow : Window
{
    public sealed record OpcionCarpeta(Guid? Id, string Nombre)
    {
        public override string ToString() => Nombre;
    }

    /// <summary>Opción del desplegable de etiquetas. null significa heredar.</summary>
    public sealed record OpcionEtiqueta(Guid? Id, string Nombre)
    {
        public override string ToString() => Nombre;
    }

    /// <summary>Opción del desplegable de conexión padre. null significa ninguna.</summary>
    public sealed record OpcionPadre(Guid? Id, string Nombre)
    {
        public override string ToString() => Nombre;
    }

    /// <summary>Fila de la grilla de campos propios (Connection.CustomFields).</summary>
    public sealed class CampoPropio
    {
        public string Nombre { get; set; } = string.Empty;

        public string Valor { get; set; } = string.Empty;
    }

    private readonly CompositionRoot _root;
    private readonly Guid? _editando;
    private readonly Guid? _carpetaInicial;

    private ConnectionRecord? _registro;

    /// <summary>Clave del color elegido, o null para usar el del protocolo.</summary>
    private string? _colorElegido;

    /// <summary>Clave del icono elegido, o null para usar el del protocolo.</summary>
    private string? _iconoElegido;

    /// <summary>El árbol completo, para resolver de qué carpeta viene cada heredado (FR-061).</summary>
    private IReadOnlyList<Folder> _todasLasCarpetas = [];

    /// <summary>Descarta la respuesta del chequeo de nombre duplicado si ya salió otra pregunta más nueva mientras se esperaba: sin esto, dos tecleos seguidos pueden contestar en el orden contrario al que se escribieron y dejar el aviso mostrando el estado de antes.</summary>
    private int _tokenDuplicado;

    /// <summary>Los campos propios de la conexión, en edición. Es una ObservableCollection y no una lista a secas porque la grilla necesita enterarse de los altas y bajas (agregar fila, quitar fila) sin que haya que reasignar ItemsSource cada vez.</summary>
    private readonly ObservableCollection<CampoPropio> _camposPropios = new();

    public ConnectionEditorWindow(
        CompositionRoot root, Guid? editando = null, Guid? carpetaInicial = null)
    {
        _root = root;
        _editando = editando;
        _carpetaInicial = carpetaInicial;

        InitializeComponent();

        _camposPropiosGrid.ItemsSource = _camposPropios;

        Title = editando is null ? "Nueva conexión" : "Editar conexión";
        _protocolo.SelectedIndex = 1;

        _sshMetodoAuth.SelectedIndex = 0;

        Loaded += async (_, _) => await CargarAsync().ConfigureAwait(true);
    }

    private Protocol Elegido => _protocolo.SelectedIndex switch
    {
        0 => Protocol.Rdp,
        2 => Protocol.Web,
        _ => Protocol.Ssh,
    };

    private async Task CargarAsync()
    {
        var carpetas = await _root.FolderService.GetAllAsync().ConfigureAwait(true);
        _todasLasCarpetas = carpetas;

        var opciones = new List<OpcionCarpeta> { new(null, "(raíz)") };

        opciones.AddRange(carpetas
            .Select(f => new OpcionCarpeta(f.Id, RutaDe(f, carpetas)))
            .OrderBy(o => o.Nombre, StringComparer.OrdinalIgnoreCase));

        _carpeta.ItemsSource = opciones;

        var etiquetas = await _root.Tags.GetAllAsync().ConfigureAwait(true);

        _etiqueta.ItemsSource = new List<OpcionEtiqueta>
        {
            new(null, "Heredar de la carpeta"),
        }.Concat(etiquetas.Select(e => new OpcionEtiqueta(e.Id, $"{e.Codigo} · {e.Nombre}")))
         .ToList();

        _etiqueta.SelectedIndex = 0;

        await CargarPadresPosiblesAsync().ConfigureAwait(true);
        ArmarPaleta();
        ArmarIconos();

        if (_editando is { } id)
        {
            _registro = await _root.ConnectionService.GetDetailAsync(id).ConfigureAwait(true);

            if (_registro is null)
            {
                Close();
                return;
            }

            Volcar(_registro);
        }
        else
        {
            _carpeta.SelectedItem = opciones.FirstOrDefault(o => o.Id == _carpetaInicial)
                                    ?? opciones[0];
        }

        ActualizarVisibilidad();
        ActualizarHeredados();
        await ActualizarNombreDuplicadoAsync().ConfigureAwait(true);
    }

    /// <summary>Arma la lista de conexiones que pueden ser padre de ésta.</summary>
    private async Task CargarPadresPosiblesAsync()
    {
        var todas = await _root.ConnectionService.GetTreeAsync().ConfigureAwait(true);

        var opciones = new List<OpcionPadre> { new(null, "(ninguna)") };

        opciones.AddRange(todas
            .Where(c => c.Id != _editando && c.ParentConnectionId is null)
            .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .Select(c => new OpcionPadre(c.Id, $"{c.Name} — {c.Host}")));

        _padre.ItemsSource = opciones;
        _padre.SelectedIndex = 0;
    }

    /// <summary>Arma las muestras de color, con una primera opción para "el del protocolo".</summary>
    private void ArmarPaleta()
    {
        _colores.Children.Clear();

        Agregar(null, "El del protocolo");

        foreach (var color in PaletaIconos.Colores)
        {
            Agregar(color.Clave, color.Nombre);
        }

        Marcar();

        void Agregar(string? clave, string nombre)
        {
            var muestra = new Border
            {
                Width = 26,
                Height = 26,
                Margin = new Thickness(0, 0, 6, 6),
                CornerRadius = new CornerRadius(6),
                BorderThickness = new Thickness(2),
                BorderBrush = System.Windows.Media.Brushes.Transparent,
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = nombre,
                Tag = clave,
                Background = clave is null
                    ? (System.Windows.Media.Brush)FindResource("Apagado")
                    : Themes.Pinceles.DeColor(clave),
            };

            if (clave is null)
            {
                muestra.Child = new TextBlock
                {
                    Text = "—",
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = (System.Windows.Media.Brush)FindResource("TextoTenue"),
                };
            }

            muestra.MouseLeftButtonDown += (_, _) =>
            {
                _colorElegido = clave;
                Marcar();
            };

            _colores.Children.Add(muestra);
        }
    }

    private void Marcar()
    {
        foreach (var muestra in _colores.Children.OfType<Border>())
        {
            var elegido = (string?)muestra.Tag == _colorElegido;

            muestra.BorderBrush = elegido
                ? (System.Windows.Media.Brush)FindResource("Texto")
                : System.Windows.Media.Brushes.Transparent;
        }
    }

    private void ArmarIconos()
    {
        ArmarSelectorDeIconos(this, _iconos, "El del protocolo", clave =>
        {
            _iconoElegido = clave;
            MarcarIconoElegido(this, _iconos, _iconoElegido);
        });

        MarcarIconoElegido(this, _iconos, _iconoElegido);
    }

    /// <summary>Muestras del juego de iconos, independientes de las de color (FR-195). La comparte la ventana de la carpeta, igual que IndiceDeMetodoAuth.</summary>
    internal static void ArmarSelectorDeIconos(
        FrameworkElement dueno,
        WrapPanel destino,
        string textoDeOmision,
        Action<string?> alElegir)
    {
        destino.Children.Clear();

        Agregar(null, textoDeOmision);

        foreach (var icono in JuegoDeIconos.Iconos)
        {
            Agregar(icono.Clave, icono.Nombre);
        }

        void Agregar(string? clave, string nombre)
        {
            var muestra = new Border
            {
                Width = 30,
                Height = 30,
                Margin = new Thickness(0, 0, 6, 6),
                CornerRadius = new CornerRadius(6),
                BorderThickness = new Thickness(2),
                BorderBrush = System.Windows.Media.Brushes.Transparent,
                Background = (System.Windows.Media.Brush)dueno.FindResource("Apagado"),
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = nombre,
                Tag = clave,
                Child = MuestraDe(dueno, JuegoDeIconos.ClaveDeRecurso(clave)),
            };

            muestra.MouseLeftButtonDown += (_, _) => alElegir(clave);

            destino.Children.Add(muestra);
        }
    }

    internal static void MarcarIconoElegido(
        FrameworkElement dueno, WrapPanel destino, string? elegido)
    {
        foreach (var muestra in destino.Children.OfType<Border>())
        {
            muestra.BorderBrush = (string?)muestra.Tag == elegido
                ? (System.Windows.Media.Brush)dueno.FindResource("Texto")
                : System.Windows.Media.Brushes.Transparent;
        }
    }

    private static UIElement MuestraDe(FrameworkElement dueno, string? recurso) =>
        recurso is null
            ? new TextBlock
            {
                Text = "—",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (System.Windows.Media.Brush)dueno.FindResource("TextoTenue"),
            }
            : new System.Windows.Shapes.Path
            {
                Width = 16,
                Height = 16,
                Stretch = System.Windows.Media.Stretch.Uniform,
                Data = (System.Windows.Media.Geometry)dueno.FindResource(recurso),
                Fill = (System.Windows.Media.Brush)dueno.FindResource("Texto"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };

    private static string RutaDe(Folder carpeta, IReadOnlyList<Folder> todas)
    {
        var partes = new List<string> { carpeta.Name };

        for (var padre = carpeta.ParentId; padre is { } id;)
        {
            var actual = todas.FirstOrDefault(f => f.Id == id);

            if (actual is null)
            {
                break;
            }

            partes.Insert(0, actual.Name);
            padre = actual.ParentId;
        }

        return string.Join(" / ", partes);
    }

    private void Volcar(ConnectionRecord registro)
    {
        var c = registro.Connection;

        _protocolo.SelectedIndex = c.Protocol switch
        {
            Protocol.Rdp => 0,
            Protocol.Web => 2,
            _ => 1,
        };

        _protocolo.IsEnabled = false;

        _nombre.Text = c.Name;
        _host.Text = c.Host;
        _puerto.Text = c.Port?.ToString() ?? string.Empty;
        _usuario.Text = c.UserName ?? string.Empty;
        _notas.Text = c.Notes ?? string.Empty;

        _dominio.Text = registro.Rdp?.Domain ?? string.Empty;
        _url.Text = registro.Web?.Url ?? string.Empty;
        _webNavegador.Text = registro.Web?.Browser ?? string.Empty;
        _ventanaPrivada.IsChecked = registro.Web?.PrivateWindow ?? false;
        _certificadoSsh.Text = registro.Ssh?.CertificatePath ?? string.Empty;
        _claveSshRuta.Text = registro.Ssh?.PrivateKeyPath ?? string.Empty;
        ActualizarHuellaClaveSsh();

        _rdpPortapapeles.IsChecked = registro.Rdp?.ClipboardEnabled;
        _rdpAjustarResolucion.IsChecked = registro.Rdp?.FitToTab;
        _rdpIgnorarCertificado.IsChecked = registro.Rdp?.IgnoreCertificateWarnings;
        _rdpIdentidadDeWindows.IsChecked = AjustesReservados.UsaIdentidadDeWindows(c);
        AplicarLaIdentidadDeWindowsAlFormulario();

        _sshMetodoAuth.SelectedIndex = IndiceDeMetodoAuth(registro.Ssh?.AuthMethod);
        _sshKeepAlive.Text = registro.Ssh?.KeepAliveSeconds?.ToString() ?? string.Empty;

        _colorElegido = c.ClaveDeColor;
        ArmarPaleta();

        _iconoElegido = c.ClaveDeIcono;
        ArmarIconos();

        _descripcion.Text = c.Description ?? string.Empty;
        _documentacion.Text = c.DocumentationUrl ?? string.Empty;
        _favorita.IsChecked = c.IsFavorite;

        var deEtiqueta = (List<OpcionEtiqueta>)_etiqueta.ItemsSource;
        _etiqueta.SelectedItem = deEtiqueta.FirstOrDefault(o => o.Id == c.TagId) ?? deEtiqueta[0];

        var padres = (List<OpcionPadre>)_padre.ItemsSource;
        _padre.SelectedItem = padres.FirstOrDefault(o => o.Id == c.ParentConnectionId)
                              ?? padres[0];

        var opciones = (List<OpcionCarpeta>)_carpeta.ItemsSource;
        _carpeta.SelectedItem = opciones.FirstOrDefault(o => o.Id == c.FolderId) ?? opciones[0];

        _camposPropios.Clear();

        foreach (var (nombre, valor) in c.CustomFields)
        {
            if (!AjustesReservados.EsReservado(nombre))
            {
                _camposPropios.Add(new CampoPropio { Nombre = nombre, Valor = valor });
            }
        }
    }

    private void AlCambiarProtocolo(object sender, SelectionChangedEventArgs e) =>
        ActualizarVisibilidad();

    private void ActualizarVisibilidad()
    {
        if (!IsLoaded && _protocolo.SelectedIndex < 0)
        {
            return;
        }

        var esWeb = Elegido == Protocol.Web;
        var esRdp = Elegido == Protocol.Rdp;
        var esSsh = Elegido == Protocol.Ssh;

        _bloqueHost.Visibility = esWeb ? Visibility.Collapsed : Visibility.Visible;
        _bloqueUrl.Visibility = esWeb ? Visibility.Visible : Visibility.Collapsed;
        _bloqueNavegadorWeb.Visibility = esWeb ? Visibility.Visible : Visibility.Collapsed;
        _bloquePuerto.Visibility = esWeb ? Visibility.Collapsed : Visibility.Visible;
        _seccionCuenta.Visibility = esWeb ? Visibility.Collapsed : Visibility.Visible;
        _bloqueCredencial.Visibility = esWeb ? Visibility.Collapsed : Visibility.Visible;
        _bloqueDominio.Visibility = esRdp ? Visibility.Visible : Visibility.Collapsed;
        _bloqueRdpAvanzado.Visibility = esRdp ? Visibility.Visible : Visibility.Collapsed;
        _seccionAutenticacion.Visibility = esSsh ? Visibility.Visible : Visibility.Collapsed;
        _bloqueKeepAliveSsh.Visibility = esSsh ? Visibility.Visible : Visibility.Collapsed;
        _ventanaPrivada.Visibility = esWeb ? Visibility.Visible : Visibility.Collapsed;

        _etiquetaPuerto.Text = esRdp
            ? "Puerto (vacío = heredar; por omisión 3389)"
            : "Puerto (vacío = heredar; por omisión 22)";

        AplicarLaIdentidadDeWindowsAlFormulario();
    }

    /// <summary>Recalcula, para cada campo heredable, el valor efectivo con el que se va a conectar mientras quede en blanco (o indeterminado, para los de tres estados) y de qué carpeta sale. El dominio ya lo resolvía SettingsResolver para conectar; lo único que faltaba era mostrárselo a quien edita, que hasta ahora sólo veía "vacío = heredar" sin forma de saber qué iba a terminar usando.</summary>
    private void ActualizarHeredados()
    {
        var carpetaId = (_carpeta.SelectedItem as OpcionCarpeta)?.Id;
        var resolver = new SettingsResolver(_todasLasCarpetas);
        var ancestry = resolver.AncestryOf(carpetaId);

        MostrarHeredadoTexto(_usuarioHeredado, _usuario.Text, ancestry, f => f.Settings.UserName);
        MostrarHeredadoTexto(_dominioHeredado, _dominio.Text, ancestry, f => f.Settings.Domain);
        MostrarHeredadoValor(
            _puertoHeredado, _puerto.Text.Trim().Length > 0, ancestry,
            f => f.Settings.Port, v => v.ToString());
        MostrarHeredadoTexto(
            _claveSshRutaHeredada, _claveSshRuta.Text, ancestry, f => f.Settings.SshPrivateKeyPath);
        MostrarHeredadoTexto(
            _certificadoSshHeredado, _certificadoSsh.Text, ancestry,
            f => f.Settings.SshCertificatePath);
        MostrarHeredadoValor(
            _sshMetodoAuthHeredado, _sshMetodoAuth.SelectedIndex != 0, ancestry,
            f => f.Settings.SshAuthMethod,
            v => v == SshAuthMethod.Password ? "Contraseña" : "Clave privada");
        MostrarHeredadoTexto(
            _sshKeepAliveHeredado, _sshKeepAlive.Text, ancestry,
            f => f.Settings.SshKeepAliveSeconds?.ToString());
        MostrarHeredadoValor(
            _rdpPortapapelesHeredado, _rdpPortapapeles.IsChecked.HasValue, ancestry,
            f => f.Settings.RdpClipboardEnabled, v => v ? "activado" : "desactivado");
        MostrarHeredadoValor(
            _rdpAjustarResolucionHeredado, _rdpAjustarResolucion.IsChecked.HasValue, ancestry,
            f => f.Settings.RdpFitToTab, v => v ? "activado" : "desactivado");
        MostrarHeredadoValor(
            _rdpIgnorarCertificadoHeredado, _rdpIgnorarCertificado.IsChecked.HasValue, ancestry,
            f => f.Settings.RdpIgnoreCertificateWarnings, v => v ? "activado" : "desactivado");

        ActualizarEtiquetaHeredada(ancestry);
    }

    private void ActualizarEtiquetaHeredada(IReadOnlyList<Folder> ancestry)
    {
        if ((_etiqueta.SelectedItem as OpcionEtiqueta)?.Id is not null)
        {
            _etiquetaHeredada.Visibility = Visibility.Collapsed;
            return;
        }

        foreach (var folder in ancestry)
        {
            if (folder.Settings.TagId is not { } id)
            {
                continue;
            }

            var opciones = (List<OpcionEtiqueta>)_etiqueta.ItemsSource;
            var nombre = opciones.FirstOrDefault(o => o.Id == id)?.Nombre ?? "(etiqueta eliminada)";

            _etiquetaHeredada.Text = $"Heredado de {folder.Name}: {nombre}";
            _etiquetaHeredada.Visibility = Visibility.Visible;
            return;
        }

        _etiquetaHeredada.Visibility = Visibility.Collapsed;
    }

    /// <summary>Campo heredable de texto: se muestra sólo mientras el propio esté en blanco.</summary>
    private static void MostrarHeredadoTexto(
        TextBlock destino, string propio, IReadOnlyList<Folder> ancestry,
        Func<Folder, string?> selector)
    {
        if (propio.Trim().Length > 0)
        {
            destino.Visibility = Visibility.Collapsed;
            return;
        }

        foreach (var folder in ancestry)
        {
            var valor = selector(folder);
            if (!string.IsNullOrEmpty(valor))
            {
                destino.Text = $"Heredado de {folder.Name}: {valor}";
                destino.Visibility = Visibility.Visible;
                return;
            }
        }

        destino.Visibility = Visibility.Collapsed;
    }

    /// <summary>Campo heredable de valor (puerto, booleano de tres estados, método de autenticación): se muestra sólo mientras el propio no esté definido.</summary>
    private static void MostrarHeredadoValor<T>(
        TextBlock destino, bool propioDefinido, IReadOnlyList<Folder> ancestry,
        Func<Folder, T?> selector, Func<T, string> formatear)
        where T : struct
    {
        if (propioDefinido)
        {
            destino.Visibility = Visibility.Collapsed;
            return;
        }

        foreach (var folder in ancestry)
        {
            if (selector(folder) is { } valor)
            {
                destino.Text = $"Heredado de {folder.Name}: {formatear(valor)}";
                destino.Visibility = Visibility.Visible;
                return;
            }
        }

        destino.Visibility = Visibility.Collapsed;
    }

    /// <summary>Handler único para todos los campos heredables: da igual cuál cambió, hay que recalcular todos porque cambiar uno propio puede dejar de tapar el heredado de otro que ni se tocó (por ejemplo, cambiar la carpeta).</summary>
    private void AlCambiarCampoHeredable(object sender, RoutedEventArgs e) => ActualizarHeredados();

    private void AlCambiarCarpeta(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        ActualizarHeredados();
        _ = ActualizarNombreDuplicadoAsync();
    }

    private void AlCambiarNombre(object sender, TextChangedEventArgs e) =>
        _ = ActualizarNombreDuplicadoAsync();

    /// <summary>Avisa si ya hay otra conexión con el mismo nombre en la misma carpeta. Es un aviso y no un bloqueo (FR-053 lo dice expresamente): nunca deshabilita Guardar, sólo informa para que quien edita decida si el nombre repetido es un error o no.</summary>
    private async Task ActualizarNombreDuplicadoAsync()
    {
        if (!IsLoaded)
        {
            return;
        }

        var nombre = _nombre.Text.Trim();

        if (nombre.Length == 0)
        {
            _nombreDuplicado.Visibility = Visibility.Collapsed;
            return;
        }

        var carpeta = (_carpeta.SelectedItem as OpcionCarpeta)?.Id;
        var token = ++_tokenDuplicado;

        var duplicado = await _root.ConnectionService
            .IsNameDuplicatedAsync(carpeta, nombre, _editando)
            .ConfigureAwait(true);

        if (token != _tokenDuplicado)
        {
            return;
        }

        _nombreDuplicado.Text = "Ya hay otra conexión con este nombre en la misma carpeta.";
        _nombreDuplicado.Visibility = duplicado ? Visibility.Visible : Visibility.Collapsed;
    }

    private void AlCambiarRutaDeClaveSsh(object sender, TextChangedEventArgs e)
    {
        ActualizarHuellaClaveSsh();
        ActualizarHeredados();
    }

    private void AlExaminarClaveSsh(object sender, RoutedEventArgs e)
    {
        var dialogo = new OpenFileDialog
        {
            Title = "Elegir clave privada",
            Filter = "Clave privada (*.ppk;*.pem;*.key)|*.ppk;*.pem;*.key|"
                     + "Todos los archivos (*.*)|*.*",
            CheckFileExists = true,
        };

        if (dialogo.ShowDialog(this) == true)
        {
            _claveSshRuta.Text = dialogo.FileName;
        }
    }

    private void AlPegarClaveSsh(object sender, RoutedEventArgs e)
    {
        if (PastePrivateKeyWindow.Mostrar(this, _root) is { } ruta)
        {
            _claveSshRuta.Text = ruta;
        }
    }

    private void AlExaminarNavegador(object sender, RoutedEventArgs e)
    {
        var dialogo = new OpenFileDialog
        {
            Title = "Elegir el ejecutable del navegador",
            Filter = "Ejecutables (*.exe)|*.exe|Todos los archivos (*.*)|*.*",
            CheckFileExists = true,
        };

        if (dialogo.ShowDialog(this) == true)
        {
            _webNavegador.Text = dialogo.FileName;
        }
    }

    private void AlAgregarCampoPropio(object sender, RoutedEventArgs e) =>
        _camposPropios.Add(new CampoPropio());

    /// <summary>Quita la fila de la grilla a la que pertenece el botón que se apretó.</summary>
    private void AlQuitarCampoPropio(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: CampoPropio campo })
        {
            _camposPropios.Remove(campo);
        }
    }

    /// <summary>Muestra la huella de la clave ya configurada, si el archivo existe y se puede leer.</summary>
    private void ActualizarHuellaClaveSsh()
    {
        var ruta = _claveSshRuta.Text.Trim();

        if (ruta.Length == 0 || !File.Exists(ruta))
        {
            _huellaClaveSsh.Visibility = Visibility.Collapsed;
            return;
        }

        try
        {
            if (new FileInfo(ruta).Length > 1_000_000)
            {
                _huellaClaveSsh.Visibility = Visibility.Collapsed;
                return;
            }

            var contenido = File.ReadAllText(ruta);
            var huella = ReconocedorDeClavePegada.Reconocer(contenido).Huella;

            if (huella is null)
            {
                _huellaClaveSsh.Visibility = Visibility.Collapsed;
                return;
            }

            _huellaClaveSsh.Text = huella.Sha256;
            _huellaClaveSsh.Visibility = Visibility.Visible;
        }
        catch (IOException)
        {
            _huellaClaveSsh.Visibility = Visibility.Collapsed;
        }
        catch (UnauthorizedAccessException)
        {
            _huellaClaveSsh.Visibility = Visibility.Collapsed;
        }
    }

    private async void AlGuardar(object sender, RoutedEventArgs e)
    {
        _error.Visibility = Visibility.Collapsed;
        LimpiarAvisosDeError();

        var nombre = _nombre.Text.Trim();
        var esWeb = Elegido == Protocol.Web;
        var destino = esWeb ? _url.Text.Trim() : _host.Text.Trim();

        if (nombre.Length == 0)
        {
            MostrarErrorEnPestana("El nombre es obligatorio.", _pestanaGeneral, _avisoErrorGeneral);
            return;
        }

        if (destino.Length == 0)
        {
            var mensaje = esWeb
                ? "La dirección URL es obligatoria."
                : "El host o dirección IP es obligatorio.";

            MostrarErrorEnPestana(
                mensaje,
                esWeb ? _pestanaProtocolo : _pestanaGeneral,
                esWeb ? _avisoErrorProtocolo : _avisoErrorGeneral);
            return;
        }

        int? puerto = null;

        if (!esWeb && _puerto.Text.Trim().Length > 0)
        {
            if (!int.TryParse(_puerto.Text.Trim(), out var valor) || valor < 1 || valor > 65535)
            {
                MostrarErrorEnPestana(
                    "El puerto debe ser un número entre 1 y 65535.", _pestanaGeneral,
                    _avisoErrorGeneral);
                return;
            }

            puerto = valor;
        }

        int? keepAlive = null;

        if (Elegido == Protocol.Ssh
            && !ValidarKeepAliveSegundos(_sshKeepAlive.Text, out keepAlive, out var errorKeepAlive))
        {
            MostrarErrorEnPestana(errorKeepAlive!, _pestanaAvanzado, _avisoErrorAvanzado);
            return;
        }

        var carpeta = (_carpeta.SelectedItem as OpcionCarpeta)?.Id;
        var usuario = _usuario.Text.Trim();
        var clave = UsaLaIdentidadDeWindows ? string.Empty : _clave.Password;

        try
        {
            if (_registro is { } existente)
            {
                await ActualizarAsync(
                        existente, nombre, destino, puerto, carpeta, usuario, clave, keepAlive)
                    .ConfigureAwait(true);
            }
            else
            {
                await CrearAsync(nombre, destino, puerto, carpeta, usuario, clave, keepAlive)
                    .ConfigureAwait(true);
            }
        }
        catch (Exception ex)
        {
            _root.Logger.TechnicalError("guardar la conexión", ex);
            MostrarError("No se pudo guardar la conexión.");
        }
        finally
        {
            _clave.Clear();
        }
    }

    /// <summary>Vuelca los campos de catálogo del formulario a la conexión.</summary>
    private void AplicarCatalogo(Connection c)
    {
        c.Description = _descripcion.Text;
        c.DocumentationUrl = _documentacion.Text;
        c.IsFavorite = _favorita.IsChecked == true;
        c.TagId = (_etiqueta.SelectedItem as OpcionEtiqueta)?.Id;
        c.ParentConnectionId = (_padre.SelectedItem as OpcionPadre)?.Id;
        c.ClaveDeColor = _colorElegido;
        c.ClaveDeIcono = _iconoElegido;

        var reservados = c.CustomFields
            .Where(campo => AjustesReservados.EsReservado(campo.Key))
            .ToList();

        c.ClearCustomFields();

        foreach (var (clave, valor) in reservados)
        {
            c.SetCustomField(clave, valor);
        }

        foreach (var (clave, valor) in CamposPropiosValidos(_camposPropios))
        {
            c.SetCustomField(clave, valor);
        }

        AjustesReservados.FijarIdentidadDeWindows(c, UsaLaIdentidadDeWindows);
    }

    /// <summary>Con la identidad de Windows no se pide ni se guarda ninguna contraseña: la que no se guarda no se puede filtrar (FR-186).</summary>
    private bool UsaLaIdentidadDeWindows =>
        Elegido == Protocol.Rdp && _rdpIdentidadDeWindows.IsChecked == true;

    private void AlCambiarLaIdentidadDeWindows(object sender, RoutedEventArgs e) =>
        AplicarLaIdentidadDeWindowsAlFormulario();

    private void AplicarLaIdentidadDeWindowsAlFormulario()
    {
        if (UsaLaIdentidadDeWindows)
        {
            _clave.Clear();
        }

        _clave.IsEnabled = !UsaLaIdentidadDeWindows;
    }

    /// <summary>Filtra y recorta las filas de la grilla de campos propios, para quedarse con las que realmente hay que guardar.</summary>
    public static IEnumerable<(string Nombre, string Valor)> CamposPropiosValidos(
        IEnumerable<CampoPropio> filas)
    {
        foreach (var fila in filas)
        {
            var nombre = fila.Nombre.Trim();

            if (nombre.Length > 0)
            {
                yield return (nombre, fila.Valor.Trim());
            }
        }
    }

    private async Task CrearAsync(
        string nombre,
        string destino,
        int? puerto,
        Guid? carpeta,
        string usuario,
        string clave,
        int? keepAlive)
    {
        var esWeb = Elegido == Protocol.Web;
        var host = esWeb ? Anfitrion(destino) : destino;

        var conexion = new Connection(Guid.NewGuid(), nombre, Elegido, host)
        {
            FolderId = carpeta,
            UserName = usuario.Length > 0 ? usuario : null,
            Notes = _notas.Text.Trim().Length > 0 ? _notas.Text.Trim() : null,
        };

        conexion.SetPort(puerto);
        AplicarCatalogo(conexion);

        var registro = new ConnectionRecord(
            conexion,
            Elegido == Protocol.Rdp ? Rdp(conexion.Id) : null,
            Elegido == Protocol.Ssh
                ? new SshSettings
                {
                    ConnectionId = conexion.Id,
                    AuthMethod = MetodoAuthElegido,
                    PrivateKeyPath = Texto(_claveSshRuta.Text),
                    CertificatePath = Texto(_certificadoSsh.Text),
                    KeepAliveSeconds = keepAlive,
                }
                : null,
            esWeb ? Web(conexion.Id, destino) : null);

        var credencial = clave.Length > 0
            ? new CredentialPromptResult(usuario, _dominio.Text.Trim(), clave, Remember: true)
            : null;

        var resultado = await _root.ConnectionService
            .CreateAsync(registro, credencial).ConfigureAwait(true);

        if (!resultado.Success)
        {
            MostrarError(resultado.ErrorMessage);
            return;
        }

        DialogResult = true;
    }

    private async Task ActualizarAsync(
        ConnectionRecord existente,
        string nombre,
        string destino,
        int? puerto,
        Guid? carpeta,
        string usuario,
        string clave,
        int? keepAlive)
    {
        var c = existente.Connection;
        var esWeb = c.Protocol == Protocol.Web;

        c.Rename(nombre);
        c.ChangeHost(esWeb ? Anfitrion(destino) : destino);
        c.SetPort(puerto);
        c.FolderId = carpeta;
        c.UserName = usuario.Length > 0 ? usuario : null;
        c.Notes = _notas.Text.Trim().Length > 0 ? _notas.Text.Trim() : null;
        AplicarCatalogo(c);

        if (existente.Rdp is { } rdp)
        {
            rdp.Domain = _dominio.Text.Trim().Length > 0 ? _dominio.Text.Trim() : null;
            rdp.ClipboardEnabled = _rdpPortapapeles.IsChecked;
            rdp.FitToTab = _rdpAjustarResolucion.IsChecked;
            rdp.IgnoreCertificateWarnings = _rdpIgnorarCertificado.IsChecked;
        }

        if (existente.Web is { } web)
        {
            web.Url = destino;
            web.Browser = Texto(_webNavegador.Text);
            web.PrivateWindow = _ventanaPrivada.IsChecked == true;
        }

        if (existente.Ssh is { } ssh)
        {
            ssh.AuthMethod = MetodoAuthElegido;
            ssh.PrivateKeyPath = Texto(_claveSshRuta.Text);
            ssh.CertificatePath = Texto(_certificadoSsh.Text);
            ssh.KeepAliveSeconds = keepAlive;
        }

        var credencial = clave.Length > 0
            ? new CredentialPromptResult(usuario, _dominio.Text.Trim(), clave, Remember: true)
            : null;

        var resultado = await _root.ConnectionService
            .UpdateAsync(existente, credencial).ConfigureAwait(true);

        if (!resultado.Success)
        {
            MostrarError(resultado.ErrorMessage);
            return;
        }

        DialogResult = true;
    }

    private RdpSettings Rdp(Guid id) => new()
    {
        ConnectionId = id,
        Domain = _dominio.Text.Trim().Length > 0 ? _dominio.Text.Trim() : null,
        ClipboardEnabled = _rdpPortapapeles.IsChecked,
        FitToTab = _rdpAjustarResolucion.IsChecked,
        IgnoreCertificateWarnings = _rdpIgnorarCertificado.IsChecked,
    };

    /// <summary>Método de autenticación SSH elegido, o null para "automático" (índice 0): la primera opción del desplegable no es un método más, es la ausencia de valor propio.</summary>
    private SshAuthMethod? MetodoAuthElegido => MetodoAuthDeIndice(_sshMetodoAuth.SelectedIndex);

    /// <summary>Traduce el índice del desplegable de método de autenticación SSH al valor de dominio.</summary>
    public static SshAuthMethod? MetodoAuthDeIndice(int indice) => indice switch
    {
        1 => SshAuthMethod.Password,
        2 => SshAuthMethod.PrivateKey,
        _ => null,
    };

    /// <summary>Camino inverso de MetodoAuthDeIndice, para volcar un valor guardado.</summary>
    public static int IndiceDeMetodoAuth(SshAuthMethod? metodo) => metodo switch
    {
        SshAuthMethod.Password => 1,
        SshAuthMethod.PrivateKey => 2,
        _ => 0,
    };

    // 0 es válido y desactiva el keep-alive; vacío es heredar (SshSettings.KeepAliveSeconds).
    /// <summary>Valida el texto del campo de keep-alive SSH, compartido con FolderSettingsWindow.</summary>
    public static bool ValidarKeepAliveSegundos(string texto, out int? valor, out string? error)
    {
        var recortado = texto.Trim();

        if (recortado.Length == 0)
        {
            valor = null;
            error = null;
            return true;
        }

        if (!int.TryParse(recortado, out var numero) || numero < 0 || numero > 86_400)
        {
            valor = null;
            error = "El keep-alive SSH debe ser un número de segundos entre 0 y 86400 (24 horas). "
                + "0 desactiva las señales de keep-alive.";
            return false;
        }

        valor = numero;
        error = null;
        return true;
    }

    private WebSettings Web(Guid id, string url) => new()
    {
        ConnectionId = id,
        Url = url,
        Browser = Texto(_webNavegador.Text),
        PrivateWindow = _ventanaPrivada.IsChecked == true,
    };

    /// <summary>Saca el anfitrión de una URL, para guardarlo en el campo Host.</summary>
    private static string Anfitrion(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : url;

    private static string? Texto(string valor) =>
        valor.Trim().Length > 0 ? valor.Trim() : null;

    private void MostrarError(string? mensaje)
    {
        _error.Text = mensaje ?? "No se pudo guardar.";
        _error.Visibility = Visibility.Visible;
    }

    /// <summary>Muestra un error de validación propio de esta ventana y lleva al usuario a la pestaña donde está el campo que lo causó, marcándola además con un aviso que sobrevive a que se vuelva a cambiar de pestaña.</summary>
    private void MostrarErrorEnPestana(string mensaje, TabItem pestana, TextBlock aviso)
    {
        LimpiarAvisosDeError();
        aviso.Visibility = Visibility.Visible;
        _pestanas.SelectedItem = pestana;
        MostrarError(mensaje);
    }

    private void LimpiarAvisosDeError()
    {
        _avisoErrorGeneral.Visibility = Visibility.Collapsed;
        _avisoErrorProtocolo.Visibility = Visibility.Collapsed;
        _avisoErrorAvanzado.Visibility = Visibility.Collapsed;
    }
}
