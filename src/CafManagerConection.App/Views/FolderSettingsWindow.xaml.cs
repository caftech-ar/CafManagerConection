using System.IO;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using CafManagerConection.App.Bootstrap;
using CafManagerConection.App.Services;
using CafManagerConection.Domain.Connections;
using CafManagerConection.Domain.Credentials;
using CafManagerConection.Domain.Settings;
using CafManagerConection.Domain.Ssh;
using CafManagerConection.UseCases.Inheritance;
using Microsoft.Win32;

namespace CafManagerConection.App.Views;

/// <summary>Valores que una carpeta presta a lo que contiene, y sus credenciales por protocolo (FR-060 a FR-064a).</summary>
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

    /// <summary>El árbol completo, para poder recalcular de qué carpeta viene cada valor heredado (FR-061) cada vez que cambia un campo, sin volver a pedirlo al servicio.</summary>
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

        _usuario.Text = s.UserName ?? string.Empty;
        _dominio.Text = s.Domain ?? string.Empty;
        _puerto.Text = s.Port?.ToString() ?? string.Empty;
        _clavePrivada.Text = s.SshPrivateKeyPath ?? string.Empty;
        _certificadoSsh.Text = s.SshCertificatePath ?? string.Empty;
        ActualizarHuellaClave();

        _sshMetodoAuth.SelectedIndex = ConnectionEditorWindow.IndiceDeMetodoAuth(s.SshAuthMethod);
        _sshKeepAlive.Text = s.SshKeepAliveSeconds?.ToString() ?? string.Empty;

        _rdpPortapapeles.IsChecked = s.RdpClipboardEnabled;
        _rdpAjustarResolucion.IsChecked = s.RdpFitToTab;
        _rdpIgnorarCertificado.IsChecked = s.RdpIgnoreCertificateWarnings;

        ActualizarHeredados();
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

        MostrarHeredadoTexto(_usuarioHeredado, _usuario.Text, ancestry, f => f.Settings.UserName);
        MostrarHeredadoTexto(_dominioHeredado, _dominio.Text, ancestry, f => f.Settings.Domain);
        MostrarHeredadoValor(
            _puertoHeredado, _puerto.Text.Trim().Length > 0, ancestry,
            f => f.Settings.Port, v => v.ToString());
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

    /// <summary>Handler único para todos los campos heredables: sólo hace falta recalcular qué mostrar, da igual cuál haya cambiado. RoutedEventArgs alcanza porque TextChangedEventArgs y SelectionChangedEventArgs derivan de él.</summary>
    private void AlCambiarCampoHeredable(object sender, RoutedEventArgs e) => ActualizarHeredados();

    private void AlCambiarRutaDeClave(object sender, TextChangedEventArgs e) =>
        ActualizarHuellaClave();

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

    /// <summary>Igual que en el editor de conexiones: la huella de la clave ya configurada, calculada al vuelo desde el archivo y nunca guardada en ningún lado. Ver el detalle de por qué esto no infringe el Principio II en ConnectionEditorWindow.ActualizarHuellaClaveSsh.</summary>
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

        int? puerto = null;

        if (_puerto.Text.Trim().Length > 0)
        {
            if (!int.TryParse(_puerto.Text.Trim(), out var valor) || valor < 1 || valor > 65535)
            {
                MostrarErrorEnPestana(
                    "El puerto debe ser un número entre 1 y 65535.", _pestanaAcceso,
                    _avisoErrorAcceso);
                return;
            }

            puerto = valor;
        }

        // Mismo rango que ConnectionEditorWindow.ValidarKeepAliveSegundos, donde está el motivo de que 0 valga.
        if (!ConnectionEditorWindow.ValidarKeepAliveSegundos(
                _sshKeepAlive.Text, out var keepAlive, out var errorKeepAlive))
        {
            MostrarErrorEnPestana(errorKeepAlive!, _pestanaSsh, _avisoErrorSsh);
            return;
        }

        var propuesta = new FolderSettings
        {
            UserName = Texto(_usuario.Text),
            Domain = Texto(_dominio.Text),
            Port = puerto,
            SshPrivateKeyPath = Texto(_clavePrivada.Text),
            SshCertificatePath = Texto(_certificadoSsh.Text),
            SshAuthMethod = ConnectionEditorWindow.MetodoAuthDeIndice(_sshMetodoAuth.SelectedIndex),
            SshKeepAliveSeconds = keepAlive,
            RdpClipboardEnabled = _rdpPortapapeles.IsChecked,
            RdpFitToTab = _rdpAjustarResolucion.IsChecked,
            RdpIgnoreCertificateWarnings = _rdpIgnorarCertificado.IsChecked,
            TagId = (_etiqueta.SelectedItem as OpcionEtiqueta)?.Id,
            RdpCredentialKey = _carpeta.Settings.RdpCredentialKey,
            SshCredentialKey = _carpeta.Settings.SshCredentialKey,
            WebCredentialKey = _carpeta.Settings.WebCredentialKey,
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

        var clavesNuevas = new List<string>();

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
            foreach (var clave in clavesNuevas)
            {
                try
                {
                    await _root.Credentials.DeleteAsync(clave).ConfigureAwait(true);
                }
                catch (Exception exBorrado)
                {
                    _root.Logger.TechnicalError(
                        $"limpiar la credencial huérfana de la carpeta ({clave})", exBorrado);
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
        s.SshPrivateKeyPath = p.SshPrivateKeyPath;
        s.SshCertificatePath = p.SshCertificatePath;
        s.SshAuthMethod = p.SshAuthMethod;
        s.SshKeepAliveSeconds = p.SshKeepAliveSeconds;
        s.RdpClipboardEnabled = p.RdpClipboardEnabled;
        s.RdpFitToTab = p.RdpFitToTab;
        s.RdpIgnoreCertificateWarnings = p.RdpIgnoreCertificateWarnings;
        s.RdpCredentialKey = p.RdpCredentialKey;
        s.SshCredentialKey = p.SshCredentialKey;
        s.WebCredentialKey = p.WebCredentialKey;
    }

    /// <summary>Guarda la credencial de un protocolo si se escribió una nueva, sobre el borrador todavía sin persistir.</summary>
    private async Task GuardarCredencialAsync(
        Folder borrador, List<string> clavesNuevas, Protocol protocolo, string clave)
    {
        if (clave.Length == 0)
        {
            return;
        }

        var llave = CredentialKey.ForFolder(borrador.Id, protocolo);
        var existiaAntes = await _root.Credentials.ExistsAsync(llave.Value).ConfigureAwait(true);
        var usuario = Texto(_usuario.Text) ?? string.Empty;

        using var credencial = new StoredCredential(usuario, Texto(_dominio.Text), clave);
        await _root.Credentials.WriteAsync(llave.Value, credencial).ConfigureAwait(true);

        if (!existiaAntes)
        {
            clavesNuevas.Add(llave.Value);
        }

        switch (protocolo)
        {
            case Protocol.Rdp:
                borrador.Settings.RdpCredentialKey = llave.Value;
                break;

            case Protocol.Ssh:
                borrador.Settings.SshCredentialKey = llave.Value;
                break;

            case Protocol.Web:
                borrador.Settings.WebCredentialKey = llave.Value;
                break;
        }
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
