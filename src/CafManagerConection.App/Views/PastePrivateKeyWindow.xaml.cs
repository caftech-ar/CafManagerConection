using System.IO;
using System.Runtime.Versioning;
using System.Windows;
using CafManagerConection.App.Bootstrap;
using CafManagerConection.Domain.Ssh;
using CafManagerConection.Infrastructure.Credentials;

namespace CafManagerConection.App.Views;

/// <summary>Pegar una clave privada en vez de escribir su ruta a mano, y ver qué es antes de guardarla.</summary>
[SupportedOSPlatform("windows")]
public partial class PastePrivateKeyWindow : Window
{
    private readonly CompositionRoot _root;
    private readonly SshKeyFileWriter _escritor;

    private ReconocimientoClavePegada _reconocimiento =
        ReconocedorDeClavePegada.Reconocer(null);

    private PastePrivateKeyWindow(CompositionRoot root)
    {
        _root = root;
        _escritor = new SshKeyFileWriter();

        InitializeComponent();

        MostrarReconocimiento();
        ActualizarDestino();
    }

    /// <summary>Ruta del archivo recién escrito, o null si se canceló.</summary>
    public string? RutaGuardada { get; private set; }

    /// <summary>Abre el diálogo y, si se guardó, devuelve la ruta para dejarla en el campo de la ventana que llamó.</summary>
    public static string? Mostrar(Window owner, CompositionRoot root)
    {
        var ventana = new PastePrivateKeyWindow(root) { Owner = owner };

        return ventana.ShowDialog() == true ? ventana.RutaGuardada : null;
    }

    private void AlCambiarElPegado(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        _reconocimiento = ReconocedorDeClavePegada.Reconocer(_pegado.Text);
        MostrarReconocimiento();
        OcultarError();
    }

    private void AlCambiarElNombre(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        ActualizarDestino();
        OcultarError();
    }

    /// <summary>Vuelca _reconocimiento a los rótulos. Es el único lugar de la ventana que toca esos controles, para que no haya dos caminos que puedan quedar en desacuerdo sobre qué se está mostrando.</summary>
    private void MostrarReconocimiento()
    {
        var r = _reconocimiento;

        _tipo.Text = "Tipo: " + DescripcionDeFormato(r.Formato);

        _cifrada.Text = r.Cifrada switch
        {
            true => "Está protegida por frase de contraseña.",
            false => "No está protegida por frase de contraseña.",
            null => string.Empty,
        };
        _cifrada.Visibility = r.Cifrada is null
            ? Visibility.Collapsed
            : Visibility.Visible;

        _comentario.Text = r.Comentario is { Length: > 0 } c
            ? $"Comentario: {c}"
            : string.Empty;
        _comentario.Visibility = _comentario.Text.Length > 0
            ? Visibility.Visible
            : Visibility.Collapsed;

        if (r.Huella is { } huella)
        {
            _huella.Text = huella.Sha256;
            _huella.Visibility = Visibility.Visible;
        }
        else
        {
            _huella.Text = string.Empty;
            _huella.Visibility = Visibility.Collapsed;
        }

        _notaHuella.Text = r.Formato == FormatoClavePegada.Desconocido ? r.Motivo : r.NotaHuella;
        _notaHuella.Visibility = string.IsNullOrEmpty(_notaHuella.Text)
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private static string DescripcionDeFormato(FormatoClavePegada formato) => formato switch
    {
        FormatoClavePegada.PpkPutty => "archivo .ppk de PuTTY",
        FormatoClavePegada.OpenSshPrivada => "clave privada OpenSSH",
        FormatoClavePegada.PemClasica => "PEM clásico",
        FormatoClavePegada.ClavePublica =>
            "esto es una clave PÚBLICA, no privada — pegá el archivo privado",
        _ => "todavía no se pegó nada reconocible",
    };

    private void ActualizarDestino()
    {
        var nombre = _nombreArchivo.Text.Trim();

        _destino.Text = nombre.Length > 0
            ? $"Se va a guardar en: {System.IO.Path.Combine(_escritor.CarpetaSsh, nombre)}"
            : $"Se va a guardar dentro de: {_escritor.CarpetaSsh}";
    }

    private void AlGuardar(object sender, RoutedEventArgs e)
    {
        OcultarError();

        var nombre = _nombreArchivo.Text.Trim();

        if (nombre.Length == 0)
        {
            MostrarError("Elegí un nombre para el archivo.");
            return;
        }

        if (_pegado.Text.Trim().Length == 0)
        {
            MostrarError("Pegá el contenido de la clave privada.");
            return;
        }

        if (_reconocimiento.Formato == FormatoClavePegada.ClavePublica)
        {
            MostrarError(
                "Esto es una clave pública, no privada. Pegá el archivo que NO termina en " +
                ".pub y que tu servidor no publica.");
            return;
        }

        if (_reconocimiento.Formato == FormatoClavePegada.Desconocido)
        {
            MostrarError(
                "El texto pegado no se reconoce como ninguno de los formatos admitidos. " +
                "Revisá que sea el archivo completo, sin cortar.");
            return;
        }

        try
        {
            RutaGuardada = _escritor.Guardar(nombre, _pegado.Text);
        }
        catch (IOException ex)
        {
            MostrarError(ex.Message);
            return;
        }
        catch (Exception ex)
        {
            _root.Logger.TechnicalError("guardar la clave privada pegada", ex);
            MostrarError("No se pudo guardar el archivo.");
            return;
        }
        finally
        {
            _pegado.Clear();
        }

        DialogResult = true;
    }

    private void MostrarError(string mensaje)
    {
        _error.Text = mensaje;
        _error.Visibility = Visibility.Visible;
    }

    private void OcultarError() => _error.Visibility = Visibility.Collapsed;
}
