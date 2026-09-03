using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Media;
using CafManagerConection.Ssh;

namespace CafManagerConection.App.Panels;

public enum TipoDeArchivoRemoto
{
    Carpeta,
    Texto,
    Comprimido,
    Ejecutable,
    Imagen,
    Registro,
    Configuracion,
    Generico,
}

/// <summary>Icono y color por tipo de archivo del explorador SFTP (FR-189b).</summary>
public static class IconosDeArchivoRemoto
{
    private static readonly string[] Texto =
        [".txt", ".md", ".markdown", ".csv", ".tsv", ".rst", ".tex", ".adoc"];

    private static readonly string[] SinExtensionQueEsTexto =
        ["readme", "leeme", "license", "licencia", "changelog", "authors", "notice", "makefile"];

    private static readonly string[] Comprimido =
        [".zip", ".tar", ".gz", ".tgz", ".bz2", ".xz", ".zst", ".7z", ".rar", ".deb", ".rpm"];

    private static readonly string[] Ejecutable =
        [".exe", ".msi", ".sh", ".bash", ".zsh", ".bat", ".cmd", ".ps1", ".psm1", ".py", ".rb",
         ".pl", ".jar", ".run", ".appimage"];

    private static readonly string[] Imagen =
        [".png", ".jpg", ".jpeg", ".gif", ".bmp", ".svg", ".webp", ".ico", ".tif", ".tiff"];

    private static readonly string[] Registro = [".log", ".out", ".err", ".journal"];

    private static readonly string[] Configuracion =
        [".conf", ".cfg", ".ini", ".yaml", ".yml", ".json", ".toml", ".xml", ".properties",
         ".service", ".socket", ".repo"];

    public static TipoDeArchivoRemoto Clasificar(string nombre, bool esCarpeta)
    {
        if (esCarpeta)
        {
            return TipoDeArchivoRemoto.Carpeta;
        }

        var extension = Path.GetExtension(nombre).ToLowerInvariant();

        if (extension.Length == 0)
        {
            return SinExtensionQueEsTexto.Contains(nombre.ToLowerInvariant())
                ? TipoDeArchivoRemoto.Texto
                : TipoDeArchivoRemoto.Generico;
        }

        if (EsSufijoDeRotacion(extension))
        {
            var sinRotacion = Clasificar(Path.GetFileNameWithoutExtension(nombre), false);

            return sinRotacion == TipoDeArchivoRemoto.Generico
                ? TipoDeArchivoRemoto.Registro
                : sinRotacion;
        }

        return extension switch
        {
            _ when Texto.Contains(extension) => TipoDeArchivoRemoto.Texto,
            _ when Comprimido.Contains(extension) => TipoDeArchivoRemoto.Comprimido,
            _ when Ejecutable.Contains(extension) => TipoDeArchivoRemoto.Ejecutable,
            _ when Imagen.Contains(extension) => TipoDeArchivoRemoto.Imagen,
            _ when Registro.Contains(extension) => TipoDeArchivoRemoto.Registro,
            _ when Configuracion.Contains(extension) => TipoDeArchivoRemoto.Configuracion,
            _ => TipoDeArchivoRemoto.Generico,
        };
    }

    private static bool EsSufijoDeRotacion(string extension) =>
        extension.Length > 1 && extension[1..].All(char.IsAsciiDigit);

    public static string ClaveDeIcono(TipoDeArchivoRemoto tipo) => tipo switch
    {
        TipoDeArchivoRemoto.Carpeta => "IconoCarpeta",
        TipoDeArchivoRemoto.Texto => "IconoArchivoTexto",
        TipoDeArchivoRemoto.Comprimido => "IconoArchivoComprimido",
        TipoDeArchivoRemoto.Ejecutable => "IconoAplicacion",
        TipoDeArchivoRemoto.Imagen => "IconoArchivoImagen",
        TipoDeArchivoRemoto.Registro => "IconoArchivoRegistro",
        TipoDeArchivoRemoto.Configuracion => "IconoAjustes",
        _ => "IconoPanelArchivos",
    };

    public static string ClaveDePincel(TipoDeArchivoRemoto tipo) => tipo switch
    {
        TipoDeArchivoRemoto.Carpeta => "IconoAmbar",
        TipoDeArchivoRemoto.Texto => "IconoAzul",
        TipoDeArchivoRemoto.Comprimido => "IconoNaranja",
        TipoDeArchivoRemoto.Ejecutable => "IconoVioleta",
        TipoDeArchivoRemoto.Imagen => "IconoRosa",
        TipoDeArchivoRemoto.Registro => "IconoCyan",
        TipoDeArchivoRemoto.Configuracion => "IconoLima",
        _ => "IconoGris",
    };
}

public static class ResumenDeListado
{
    public static string Describir(
        string carpeta, int carpetas, int archivos, int enlacesOmitidos)
    {
        var texto = $"{carpeta} — {carpetas} carpeta(s), {archivos} archivo(s)";

        return enlacesOmitidos == 0
            ? texto
            : $"{texto}, {enlacesOmitidos} enlace(s) simbólico(s) omitido(s)";
    }

    public static string ConfirmacionDeSubida(string carpeta, string servidor, int elementos) =>
        $"Se van a subir {elementos} elemento(s) a «{carpeta}» en {servidor}."
        + Environment.NewLine
        + "Nada se transfiere hasta que lo confirmes.";
}

/// <summary>Un nivel del árbol remoto que sólo se lee cuando se despliega (FR-189).</summary>
[SupportedOSPlatform("windows")]
public sealed class NodoRemoto : INotifyPropertyChanged
{
    private const string TextoDelMarcador = "Cargando…";

    private bool _expandido;

    public NodoRemoto(
        string nombre,
        string ruta,
        bool esCarpeta,
        string tamano = "",
        string modificado = "")
    {
        Nombre = nombre;
        Ruta = ruta;
        EsCarpeta = esCarpeta;
        Tamano = tamano;
        Modificado = modificado;
        Tipo = IconosDeArchivoRemoto.Clasificar(nombre, esCarpeta);

        if (esCarpeta)
        {
            CargaPendiente = true;
            Hijos.Add(new NodoRemoto());
        }
    }

    private NodoRemoto()
    {
        Nombre = TextoDelMarcador;
        Ruta = string.Empty;
        Tamano = string.Empty;
        Modificado = string.Empty;
        EsMarcador = true;
        Tipo = TipoDeArchivoRemoto.Generico;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler? SolicitaCarga;

    public string Nombre { get; }

    public string Ruta { get; }

    public bool EsCarpeta { get; }

    public bool EsMarcador { get; }

    public string Tamano { get; }

    public string Modificado { get; }

    public TipoDeArchivoRemoto Tipo { get; }

    public ObservableCollection<NodoRemoto> Hijos { get; } = [];

    public NodoRemoto? Padre { get; private set; }

    public bool CargaPendiente { get; private set; }

    public string ClaveDePincel => IconosDeArchivoRemoto.ClaveDePincel(Tipo);

    public Geometry? Geometria =>
        Application.Current?.TryFindResource(IconosDeArchivoRemoto.ClaveDeIcono(Tipo)) as Geometry;

    public string Detalle => EsCarpeta ? Ruta : $"{Ruta} — {Tamano} — {Modificado}";

    public string CarpetaDeDestino =>
        EsCarpeta ? Ruta : Padre?.Ruta ?? RutaRemota.Padre(Ruta);

    public bool Expandido
    {
        get => _expandido;
        set
        {
            if (_expandido == value)
            {
                return;
            }

            _expandido = value;
            Notificar();

            if (value && CargaPendiente)
            {
                SolicitaCarga?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public void Completar(IEnumerable<NodoRemoto> hijos)
    {
        Hijos.Clear();

        foreach (var hijo in hijos)
        {
            hijo.Padre = this;
            Hijos.Add(hijo);
        }

        CargaPendiente = false;
        Notificar(nameof(CargaPendiente));
    }

    public void Recargar()
    {
        Hijos.Clear();
        Hijos.Add(new NodoRemoto());
        CargaPendiente = true;
        Notificar(nameof(CargaPendiente));

        if (_expandido)
        {
            SolicitaCarga?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Desplegar()
    {
        if (!_expandido)
        {
            Expandido = true;
        }
        else if (CargaPendiente)
        {
            SolicitaCarga?.Invoke(this, EventArgs.Empty);
        }
    }

    private void Notificar([CallerMemberName] string? propiedad = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propiedad));
}
