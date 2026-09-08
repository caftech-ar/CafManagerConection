using System.IO;
using System.Runtime.Versioning;
using System.Windows.Controls;
using System.Windows;
using CafManagerConection.App.Bootstrap;
using CafManagerConection.App.Services;
using CafManagerConection.Domain.Connections;
using CafManagerConection.Domain.Credentials;
using CafManagerConection.Domain.Settings;
using CafManagerConection.Domain.Ssh;
using CafManagerConection.UseCases.Connections;
using CafManagerConection.UseCases.Inheritance;
using Microsoft.Win32;

namespace CafManagerConection.App.Views;

/// <summary>Valores que una carpeta presta a lo que contiene, y sus credenciales por protocolo.</summary>
[SupportedOSPlatform("windows")]
public partial class FolderSettingsWindow : Window
{
    private readonly CompositionRoot _root;
    private readonly Guid _carpetaId;

    private Folder? _carpeta;

    /// <summary>Clave del color elegido, o null para el de las carpetas.</summary>
    private string? _colorElegido;

    /// <summary>Clave del icono elegido, o null para el de las carpetas.</summary>
    private string? _iconoElegido;

    /// <summary>El árbol completo, para poder recalcular de qué carpeta viene cada valor heredado cada vez que cambia un campo, sin volver a pedirlo al servicio.</summary>
    private IReadOnlyList<Folder> _todasLasCarpetas = [];

    public FolderSettingsWindow(CompositionRoot root, Guid carpetaId)
    {
        _root = root;
        _carpetaId = carpetaId;

        InitializeComponent();

        Loaded += async (_, _) => await CargarAsync().ConfigureAwait(true);
    }

    private async Task CargarAsync()
    {
        var carpetas = await _root.FolderService.GetAllAsync().ConfigureAwait(true);
        _todasLasCarpetas = carpetas;
        _carpeta = carpetas.FirstOrDefault(f => f.Id == _carpetaId);

        if (_carpeta is null)
        {
            Close();
            return;
        }

        Title = $"Carpeta · {_carpeta.Name}";

        _descripcion.Text = _carpeta.Description ?? string.Empty;
        _colorElegido = _carpeta.ClaveDeColor;
        _iconoElegido = _carpeta.ClaveDeIcono;

        ArmarPaleta();
        ArmarIconos();
        await CargarEtiquetasAsync().ConfigureAwait(true);

        var contenidas = await _root.ConnectionService.GetTreeAsync().ConfigureAwait(true);
        var cuantas = contenidas.Count(c => c.FolderId == _carpetaId);

        _encabezado.Text = cuantas == 0
            ? "Todavía no hay conexiones en esta carpeta. Lo que se defina acá lo van a heredar "
              + "las que se creen dentro."
            : $"{cuantas} conexión(es) en esta carpeta heredan lo que no definan por su cuenta.";

        var s = _carpeta.Settings;

        // Prefilado con el propio por protocolo o, si no hay, el compartido de reserva, para que el valor viejo quede a la vista y editable.
        _usuarioRdp.Text = s.UsuarioDe(Protocol.Rdp) ?? string.Empty;
        _usuarioSsh.Text = s.UsuarioDe(Protocol.Ssh) ?? string.Empty;
        _usuarioWeb.Text = s.UsuarioDe(Protocol.Web) ?? string.Empty;
        _puertoRdp.Text = s.PuertoDe(Protocol.Rdp)?.ToString() ?? string.Empty;
        _puertoSsh.Text = s.PuertoDe(Protocol.Ssh)?.ToString() ?? string.Empty;
        _puertoWeb.Text = s.PuertoDe(Protocol.Web)?.ToString() ?? string.Empty;

        MostrarCuentasPorProtocolo(ProtocolosEnUso(contenidas));

        _dominio.Text = s.Domain ?? string.Empty;
        _clavePrivada.Text = s.SshPrivateKeyPath ?? string.Empty;
        _certificadoSsh.Text = s.SshCertificatePath ?? string.Empty;
        ActualizarHuellaClave();

        _sshMetodoAuth.SelectedIndex = ConnectionEditorWindow.IndiceDeMetodoAuth(s.SshAuthMethod);
        _sshKeepAlive.Text = s.SshKeepAliveSeconds?.ToString() ?? string.Empty;

        _claveRdpGuardada.Visibility = s.RdpTieneSecreto ? Visibility.Visible : Visibility.Collapsed;
        _claveSshGuardada.Visibility = s.SshTieneSecreto ? Visibility.Visible : Visibility.Collapsed;
        _claveWebGuardada.Visibility = s.WebTieneSecreto ? Visibility.Visible : Visibility.Collapsed;

        _rdpPortapapeles.IsChecked = s.RdpClipboardEnabled;
        _rdpIgnorarCertificado.IsChecked = s.RdpIgnoreCertificateWarnings;
        VolcarOpcionesDePantalla(s);

        ActualizarHeredados();
    }

    /// <summary>Los protocolos de las conexiones que cuelgan de esta carpeta o de sus descendientes.</summary>
    private ISet<Protocol> ProtocolosEnUso(IReadOnlyList<ConnectionSummary> contenidas)
    {
        var descendientes = new HashSet<Guid> { _carpetaId };

        bool cambio;
        do
        {
            cambio = false;
            foreach (var f in _todasLasCarpetas)
            {
                if (f.ParentId is { } p && descendientes.Contains(p) && descendientes.Add(f.Id))
                {
                    cambio = true;
                }
            }
        }
        while (cambio);

        return contenidas
            .Where(c => c.FolderId is { } fid && descendientes.Contains(fid))
            .Select(c => c.Protocol)
            .ToHashSet();
    }

    /// <summary>Un puerto vacío es válido (sin definir); si tiene texto, debe estar entre 1 y 65535.</summary>
    private static bool ParsearPuerto(string texto, out int? puerto)
    {
        puerto = null;

        if (texto.Trim().Length == 0)
        {
            return true;
        }

        if (int.TryParse(texto.Trim(), out var valor) && valor is >= 1 and <= 65535)
        {
            puerto = valor;
            return true;
        }

        return false;
    }

    /// <summary>Muestra el usuario y el puerto sólo de los protocolos en uso; una carpeta vacía muestra los tres.</summary>
    private void MostrarCuentasPorProtocolo(ISet<Protocol> enUso)
    {
        var vacio = enUso.Count == 0;

        _cuentaRdp.Visibility = vacio || enUso.Contains(Protocol.Rdp)
            ? Visibility.Visible : Visibility.Collapsed;
        _cuentaSsh.Visibility = vacio || enUso.Contains(Protocol.Ssh)
            ? Visibility.Visible : Visibility.Collapsed;
        _cuentaWeb.Visibility = vacio || enUso.Contains(Protocol.Web)
            ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Recalcula, para cada campo heredable, de qué carpeta ascendente viene el valor que se va a usar mientras este campo quede en blanco (o indeterminado, para los de tres estados).</summary>
    private void ActualizarHeredados()
    {
        if (_carpeta is null)
        {
            return;
        }

        var resolver = new SettingsResolver(_todasLasCarpetas);
        var ancestry = resolver.AncestryOf(_carpeta.ParentId);

        MostrarHeredadoTexto(_dominioHeredado, _dominio.Text, ancestry, f => f.Settings.Domain);
        MostrarHeredadoTexto(
            _clavePrivadaHeredada, _clavePrivada.Text, ancestry, f => f.Settings.SshPrivateKeyPath);
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
            _rdpIgnorarCertificadoHeredado, _rdpIgnorarCertificado.IsChecked.HasValue, ancestry,
            f => f.Settings.RdpIgnoreCertificateWarnings, v => v ? "activado" : "desactivado");
        ConnectionEditorWindow.MostrarHeredadoReservado(
            _rdpModoTamanoHeredado, _rdpModoTamano.SelectedIndex > 0, ancestry,
            AjustesReservados.ModoDeTamano, ConnectionEditorWindow.NombreDeModo);

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

    /// <summary>Un solo handler para todos los campos heredables; <c>RoutedEventArgs</c> alcanza porque <c>TextChangedEventArgs</c> y <c>SelectionChangedEventArgs</c> derivan de él.</summary>
    private void AlCambiarCampoHeredable(object sender, RoutedEventArgs e) => ActualizarHeredados();

    private void AlCambiarRutaDeClave(object sender, TextChangedEventArgs e)
    {
        // Definir una clave privada implica querer autenticar con ella: sólo pisa «Automático» o «Contraseña».
        if (_clavePrivada.Text.Trim().Length > 0 && _sshMetodoAuth.SelectedIndex is 0 or 1)
        {
            _sshMetodoAuth.SelectedIndex = 2;
        }

        ActualizarHuellaClave();
    }

    /// <summary>Muestra u oculta la contraseña de un protocolo. Revelar es sólo lectura: la fuente sigue siendo el <see cref="PasswordBox"/>.</summary>
    private async void AlAlternarVerClave(object sender, RoutedEventArgs e)
    {
        if (_carpeta is null || (sender as System.Windows.Controls.Button)?.Tag is not string tag)
        {
            return;
        }

        var protocolo = Enum.Parse<Protocol>(tag);

        var (clave, visible, tiene) = protocolo switch
        {
            Protocol.Rdp => (_claveRdp, _claveRdpVisible, _carpeta.Settings.RdpTieneSecreto),
            Protocol.Ssh => (_claveSsh, _claveSshVisible, _carpeta.Settings.SshTieneSecreto),
            _ => (_claveWeb, _claveWebVisible, _carpeta.Settings.WebTieneSecreto),
        };

        if (visible.Visibility == Visibility.Visible)
        {
            visible.Clear();
            visible.Visibility = Visibility.Collapsed;
            clave.Visibility = Visibility.Visible;
            return;
        }

        if (clave.Password.Length > 0)
        {
            Mostrar(clave, visible, clave.Password);
            return;
        }

        if (tiene)
        {
            var secreto = await LectorDeSecreto
                .LeerAsync(_root, this, ReferenciaDeSecreto.DeCarpeta(_carpeta.Id, protocolo))
                .ConfigureAwait(true);

            if (secreto is not null)
            {
                Mostrar(clave, visible, secreto);
            }
        }
    }

    private static void Mostrar(
        System.Windows.Controls.PasswordBox clave,
        System.Windows.Controls.TextBox visible,
        string texto)
    {
        visible.Text = texto;
        visible.Visibility = Visibility.Visible;
        clave.Visibility = Visibility.Collapsed;
    }

    private void AlExaminarClave(object sender, RoutedEventArgs e)
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
            _clavePrivada.Text = dialogo.FileName;
        }
    }

    private void AlPegarClave(object sender, RoutedEventArgs e)
    {
        if (PastePrivateKeyWindow.Mostrar(this, _root) is { } ruta)
        {
            _clavePrivada.Text = ruta;
        }
    }

    /// <summary>La huella de la clave ya configurada, calculada al vuelo desde el archivo y nunca guardada.</summary>
    private void ActualizarHuellaClave()
    {
        var ruta = _clavePrivada.Text.Trim();

        if (ruta.Length == 0 || !File.Exists(ruta))
        {
            _huellaClave.Visibility = Visibility.Collapsed;
            return;
        }

        try
        {
            if (new FileInfo(ruta).Length > 1_000_000)
            {
                _huellaClave.Visibility = Visibility.Collapsed;
                return;
            }

            var huella = ReconocedorDeClavePegada.Reconocer(File.ReadAllText(ruta)).Huella;

            if (huella is null)
            {
                _huellaClave.Visibility = Visibility.Collapsed;
                return;
            }

            _huellaClave.Text = huella.Sha256;
            _huellaClave.Visibility = Visibility.Visible;
        }
        catch (IOException)
        {
            _huellaClave.Visibility = Visibility.Collapsed;
        }
        catch (UnauthorizedAccessException)
        {
            _huellaClave.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>Opcion del desplegable de etiquetas. null significa ninguna.</summary>
    public sealed record OpcionEtiqueta(Guid? Id, string Nombre)
    {
        public override string ToString() => Nombre;
    }

    private async Task CargarEtiquetasAsync()
    {
        var etiquetas = await _root.Tags.GetAllAsync().ConfigureAwait(true);

        var opciones = new List<OpcionEtiqueta> { new(null, "(ninguna)") };

        opciones.AddRange(etiquetas.Select(
            e => new OpcionEtiqueta(e.Id, $"{e.Codigo} · {e.Nombre}")));

        _etiqueta.ItemsSource = opciones;

        _etiqueta.SelectedItem =
            opciones.FirstOrDefault(o => o.Id == _carpeta?.Settings.TagId) ?? opciones[0];
    }

    /// <summary>Muestras de color, iguales a las del editor de conexiones.</summary>
    private void ArmarPaleta()
    {
        _colores.Children.Clear();

        Agregar(null, "El de las carpetas");

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
                    Text = "\u2014",
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

    private void ArmarIconos()
    {
        ConnectionEditorWindow.ArmarSelectorDeIconos(this, _iconos, "El de las carpetas", clave =>
        {
            _iconoElegido = clave;
            ConnectionEditorWindow.MarcarIconoElegido(this, _iconos, _iconoElegido);
        });

        ConnectionEditorWindow.MarcarIconoElegido(this, _iconos, _iconoElegido);
    }

    private void Marcar()
    {
        foreach (var muestra in _colores.Children.OfType<Border>())
        {
            muestra.BorderBrush = (string?)muestra.Tag == _colorElegido
                ? (System.Windows.Media.Brush)FindResource("Texto")
                : System.Windows.Media.Brushes.Transparent;
        }
    }

    private async void AlGuardar(object sender, RoutedEventArgs e)
    {
        if (_carpeta is null)
        {
            return;
        }

        _error.Visibility = Visibility.Collapsed;
        LimpiarAvisosDeError();

        if (!ParsearPuerto(_puertoRdp.Text, out var rdpPort)
            || !ParsearPuerto(_puertoSsh.Text, out var sshPort)
            || !ParsearPuerto(_puertoWeb.Text, out var webPort))
        {
            MostrarErrorEnPestana(
                "El puerto debe ser un número entre 1 y 65535.", _pestanaAcceso,
                _avisoErrorAcceso);
            return;
        }

        if (!ConnectionEditorWindow.ValidarKeepAliveSegundos(
                _sshKeepAlive.Text, out var keepAlive, out var errorKeepAlive))
        {
            MostrarErrorEnPestana(errorKeepAlive!, _pestanaSsh, _avisoErrorSsh);
            return;
        }

        var propuesta = new FolderSettings
        {
            // El usuario y el puerto compartidos quedan como estaban: son sólo reserva.
            UserName = _carpeta.Settings.UserName,
            Port = _carpeta.Settings.Port,
            RdpUserName = Texto(_usuarioRdp.Text),
            SshUserName = Texto(_usuarioSsh.Text),
            WebUserName = Texto(_usuarioWeb.Text),
            RdpPort = rdpPort,
            SshPort = sshPort,
            WebPort = webPort,
            Domain = Texto(_dominio.Text),
            SshPrivateKeyPath = Texto(_clavePrivada.Text),
            SshCertificatePath = Texto(_certificadoSsh.Text),
            SshAuthMethod = ConnectionEditorWindow.MetodoAuthDeIndice(_sshMetodoAuth.SelectedIndex),
            SshKeepAliveSeconds = keepAlive,
            RdpClipboardEnabled = _rdpPortapapeles.IsChecked,
            RdpIgnoreCertificateWarnings = _rdpIgnorarCertificado.IsChecked,
            TagId = (_etiqueta.SelectedItem as OpcionEtiqueta)?.Id,
            RdpTieneSecreto = _carpeta.Settings.RdpTieneSecreto,
            SshTieneSecreto = _carpeta.Settings.SshTieneSecreto,
            WebTieneSecreto = _carpeta.Settings.WebTieneSecreto,
            CustomFields = LeerOpcionesComoCampos(),
        };

        var credencialesCambiadas = new HashSet<Protocol>();

        if (_claveRdp.Password.Length > 0)
        {
            credencialesCambiadas.Add(Protocol.Rdp);
        }

        if (_claveSsh.Password.Length > 0)
        {
            credencialesCambiadas.Add(Protocol.Ssh);
        }

        if (_claveWeb.Password.Length > 0)
        {
            credencialesCambiadas.Add(Protocol.Web);
        }

        var impacto = await _root.FolderService
            .GetUpdateImpactAsync(_carpeta.Id, propuesta, credencialesCambiadas).ConfigureAwait(true);

        if (impacto > 0 && !Dialogos.Confirmar(
                this,
                "Confirmar cambio heredado",
                $"Este cambio va a modificar el usuario, dominio, puerto o credencial con el que "
                + $"va a conectar {impacto} conexión(es) que heredan esta configuración.",
                "Guardar igual"))
        {
            return;
        }

        var descripcionNueva = string.IsNullOrWhiteSpace(_descripcion.Text)
            ? null
            : _descripcion.Text.Trim();

        var borrador = new Folder(_carpeta.Id, _carpeta.Name, _carpeta.ParentId, _carpeta.SortOrder)
        {
            ClaveDeColor = _colorElegido,
            ClaveDeIcono = _iconoElegido,
            Description = descripcionNueva,
            Settings = propuesta,
        };

        var clavesNuevas = new List<ReferenciaDeSecreto>();

        try
        {
            await GuardarCredencialAsync(borrador, clavesNuevas, Protocol.Rdp, _claveRdp.Password)
                .ConfigureAwait(true);
            await GuardarCredencialAsync(borrador, clavesNuevas, Protocol.Ssh, _claveSsh.Password)
                .ConfigureAwait(true);
            await GuardarCredencialAsync(borrador, clavesNuevas, Protocol.Web, _claveWeb.Password)
                .ConfigureAwait(true);

            await _root.FolderService.UpdateSettingsAsync(borrador).ConfigureAwait(true);

            AplicarGuardado(borrador);
            DialogResult = true;
        }
        catch (Exception ex)
        {
            foreach (var referencia in clavesNuevas)
            {
                try
                {
                    await _root.Credentials.DeleteAsync(referencia).ConfigureAwait(true);
                }
                catch (Exception exBorrado)
                {
                    _root.Logger.TechnicalError(
                        $"limpiar la contraseña huérfana de {referencia}", exBorrado);
                }
            }

            _root.Logger.TechnicalError("guardar la configuración de la carpeta", ex);
            _error.Text = "No se pudo guardar la configuración.";
            _error.Visibility = Visibility.Visible;
        }
        finally
        {
            _claveRdp.Clear();
            _claveSsh.Clear();
            _claveWeb.Clear();
        }
    }

    /// <summary>Refleja en la carpeta cargada lo que ya quedó persistido, no antes.</summary>
    private void AplicarGuardado(Folder borrador)
    {
        if (_carpeta is null)
        {
            return;
        }

        _carpeta.Description = borrador.Description;
        _carpeta.ClaveDeColor = borrador.ClaveDeColor;
        _carpeta.ClaveDeIcono = borrador.ClaveDeIcono;

        var s = _carpeta.Settings;
        var p = borrador.Settings;

        s.TagId = p.TagId;
        s.UserName = p.UserName;
        s.Domain = p.Domain;
        s.Port = p.Port;
        s.RdpUserName = p.RdpUserName;
        s.SshUserName = p.SshUserName;
        s.WebUserName = p.WebUserName;
        s.RdpPort = p.RdpPort;
        s.SshPort = p.SshPort;
        s.WebPort = p.WebPort;
        s.SshPrivateKeyPath = p.SshPrivateKeyPath;
        s.SshCertificatePath = p.SshCertificatePath;
        s.SshAuthMethod = p.SshAuthMethod;
        s.SshKeepAliveSeconds = p.SshKeepAliveSeconds;
        s.RdpClipboardEnabled = p.RdpClipboardEnabled;
        s.RdpIgnoreCertificateWarnings = p.RdpIgnoreCertificateWarnings;
        s.RdpTieneSecreto = p.RdpTieneSecreto;
        s.SshTieneSecreto = p.SshTieneSecreto;
        s.WebTieneSecreto = p.WebTieneSecreto;

        s.CustomFields.Clear();

        foreach (var (clave, valor) in p.CustomFields)
        {
            s.CustomFields[clave] = valor;
        }
    }

    /// <summary>Reúne las opciones de pantalla del formulario como campos reservados <c>cmc:</c> para la carpeta.</summary>
    private Dictionary<string, string> LeerOpcionesComoCampos()
    {
        var rendimiento = BanderasDeRendimientoRdp.Ninguna;

        if (_rdpSinFondo.IsChecked == true) rendimiento |= BanderasDeRendimientoRdp.SinFondoDeEscritorio;
        if (_rdpSinTemas.IsChecked == true) rendimiento |= BanderasDeRendimientoRdp.SinTemas;
        if (_rdpSinAnimaciones.IsChecked == true) rendimiento |= BanderasDeRendimientoRdp.SinAnimacionesDeMenu;
        if (_rdpSinArrastre.IsChecked == true) rendimiento |= BanderasDeRendimientoRdp.SinArrastreDeContenido;

        var opciones = new OpcionesDePantallaRdp(
            rendimiento,
            ConnectionEditorWindow.TiposDeRed[Math.Max(_rdpTipoRed.SelectedIndex, 0)],
            ConnectionEditorWindow.ModosDeTamano[Math.Max(_rdpModoTamano.SelectedIndex, 0)],
            ConnectionEditorWindow.EscalasEscritorio[Math.Max(_rdpEscalaEscritorio.SelectedIndex, 0)],
            ConnectionEditorWindow.EscalasDispositivo[Math.Max(_rdpEscalaDispositivo.SelectedIndex, 0)],
            Texto(_rdpProgramaInicial.Text),
            Texto(_rdpDirectorioTrabajo.Text),
            _rdpRemoteApp.IsChecked == true);

        var campos = new Dictionary<string, string>();
        AjustesReservados.EscribirOpcionesDePantalla(opciones, (clave, valor) =>
        {
            if (valor is not null)
            {
                campos[clave] = valor;
            }
        });

        return campos;
    }

    private void VolcarOpcionesDePantalla(FolderSettings s)
    {
        var o = AjustesReservados.LeerOpcionesDePantalla(s.CustomFields);

        _rdpSinFondo.IsChecked = o.Rendimiento.HasFlag(BanderasDeRendimientoRdp.SinFondoDeEscritorio);
        _rdpSinTemas.IsChecked = o.Rendimiento.HasFlag(BanderasDeRendimientoRdp.SinTemas);
        _rdpSinAnimaciones.IsChecked = o.Rendimiento.HasFlag(BanderasDeRendimientoRdp.SinAnimacionesDeMenu);
        _rdpSinArrastre.IsChecked = o.Rendimiento.HasFlag(BanderasDeRendimientoRdp.SinArrastreDeContenido);

        _rdpModoTamano.SelectedIndex =
            Math.Max(Array.IndexOf(ConnectionEditorWindow.ModosDeTamano, o.ModoDeTamano), 0);
        _rdpTipoRed.SelectedIndex =
            Math.Max(Array.IndexOf(ConnectionEditorWindow.TiposDeRed, o.TipoDeRed), 0);
        _rdpEscalaEscritorio.SelectedIndex =
            Math.Max(Array.IndexOf(ConnectionEditorWindow.EscalasEscritorio, o.EscalaDeEscritorio), 0);
        _rdpEscalaDispositivo.SelectedIndex =
            Math.Max(Array.IndexOf(ConnectionEditorWindow.EscalasDispositivo, o.EscalaDeDispositivo), 0);
        _rdpProgramaInicial.Text = o.ProgramaInicial ?? string.Empty;
        _rdpDirectorioTrabajo.Text = o.DirectorioDeTrabajo ?? string.Empty;
        _rdpRemoteApp.IsChecked = o.ComoRemoteApp;
    }

    /// <summary>Guarda la contraseña de un protocolo si se escribió una nueva, sobre el borrador todavía sin persistir.</summary>
    private async Task GuardarCredencialAsync(
        Folder borrador,
        List<ReferenciaDeSecreto> nuevas,
        Protocol protocolo,
        string clave)
    {
        if (clave.Length == 0)
        {
            return;
        }

        var referencia = ReferenciaDeSecreto.DeCarpeta(borrador.Id, protocolo);
        var existiaAntes = await _root.Credentials.ExistsAsync(referencia).ConfigureAwait(true);

        await _root.Credentials.WriteAsync(referencia, clave.AsMemory()).ConfigureAwait(true);

        if (!existiaAntes)
        {
            nuevas.Add(referencia);
        }

        borrador.Settings.DefinirSecretoPara(protocolo, true);
    }

    private void MostrarErrorEnPestana(string mensaje, TabItem pestana, TextBlock aviso)
    {
        LimpiarAvisosDeError();
        aviso.Visibility = Visibility.Visible;
        _pestanas.SelectedItem = pestana;
        _error.Text = mensaje;
        _error.Visibility = Visibility.Visible;
    }

    private void LimpiarAvisosDeError()
    {
        _avisoErrorAcceso.Visibility = Visibility.Collapsed;
        _avisoErrorSsh.Visibility = Visibility.Collapsed;
    }

    private static string? Texto(string valor) =>
        valor.Trim().Length > 0 ? valor.Trim() : null;
}
