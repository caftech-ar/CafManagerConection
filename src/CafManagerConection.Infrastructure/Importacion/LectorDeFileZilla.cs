using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using CafManagerConection.Domain.Credentials;
using CafManagerConection.Domain.Importacion;

namespace CafManagerConection.Infrastructure.Importacion;

/// <summary>Lee los sitios guardados de FileZilla desde su <c>sitemanager.xml</c>.</summary>
public static class LectorDeFileZilla
{
    // FileZilla: <Protocol> 0 es FTP y 1 es SFTP; el resto se corrió entre versiones del enum.
    private const int ProtocoloFtp = 0;
    private const int ProtocoloSftp = 1;

    public const string AvisoContrasenaConMaestra =
        "Su contraseña guardada está cifrada con la contraseña maestra de FileZilla y no se "
        + "puede descifrar: hay que escribirla a mano la primera vez.";

    public const string AvisoContrasenaEnBase64Invalido =
        "Su contraseña guardada no está en base64 válido y no se pudo decodificar: hay que "
        + "escribirla a mano la primera vez.";

    /// <summary>Ruta del <c>sitemanager.xml</c> en una instalación normal.</summary>
    public static string RutaHabitual() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "FileZilla",
        "sitemanager.xml");

    public static LecturaDeImportacion Leer(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            return LecturaDeImportacion.Vacia;
        }

        XDocument documento;

        try
        {
            documento = XDocument.Parse(xml);
        }
        catch (XmlException error)
        {
            return SoloUnMotivo(
                "sitemanager.xml",
                "El archivo no es XML válido y no se pudo leer ninguna conexión: "
                    + error.Message);
        }

        var servidores = documento.Root?.Element("Servers");

        if (servidores is null)
        {
            return LecturaDeImportacion.Vacia;
        }

        var compatibles = new List<ConexionImportada>();
        var omitidas = new List<ImportacionOmitida>();

        ConvertirContenedor(servidores, [], compatibles, omitidas);

        return new LecturaDeImportacion(compatibles, omitidas);
    }

    private static LecturaDeImportacion SoloUnMotivo(string nombre, string motivo) =>
        new([], [new ImportacionOmitida(OrigenDeImportacion.FileZilla, nombre, motivo)]);

    private static void ConvertirContenedor(
        XElement contenedor,
        string[] carpetas,
        List<ConexionImportada> compatibles,
        List<ImportacionOmitida> omitidas)
    {
        foreach (var hijo in contenedor.Elements())
        {
            switch (hijo.Name.LocalName)
            {
                case "Server":
                    ConvertirServidor(hijo, carpetas, compatibles, omitidas);
                    break;

                case "Folder":
                    var nombre = NombreDeCarpeta(hijo);

                    ConvertirContenedor(
                        hijo,
                        nombre.Length == 0 ? carpetas : [.. carpetas, nombre],
                        compatibles,
                        omitidas);
                    break;
            }
        }
    }

    // En %APPDATA%\FileZilla\sitemanager.xml el nombre de una <Folder> es un nodo de texto suelto.
    private static string NombreDeCarpeta(XElement carpeta) => string.Join(
        ' ',
        carpeta.Nodes()
            .OfType<XText>()
            .Select(texto => texto.Value.Trim())
            .Where(texto => texto.Length > 0));

    private static void ConvertirServidor(
        XElement servidor,
        string[] carpetas,
        List<ConexionImportada> compatibles,
        List<ImportacionOmitida> omitidas)
    {
        var host = Campo(servidor, "Host");
        var nombre = Campo(servidor, "Name") ?? host ?? "(sin nombre)";

        if (host is null)
        {
            omitidas.Add(new ImportacionOmitida(
                OrigenDeImportacion.FileZilla,
                nombre,
                "No tiene servidor (<Host>), así que no hay a dónde conectarse."));

            return;
        }

        var protocolo = CampoEntero(servidor, "Protocol");

        if (protocolo != ProtocoloSftp)
        {
            omitidas.Add(new ImportacionOmitida(
                OrigenDeImportacion.FileZilla, nombre, MotivoDelProtocolo(protocolo)));

            return;
        }

        var usuario = Campo(servidor, "User");
        var advertencias = new List<string>();
        var credencial = LeerContrasena(servidor, usuario, advertencias);

        compatibles.Add(new ConexionImportada(
            OrigenDeImportacion.FileZilla,
            nombre,
            carpetas,
            host,
            PuertoDeclarado(servidor),
            usuario,
            Campo(servidor, "Keyfile"),
            "SFTP",
            credencial,
            advertencias.Count == 0 ? null : advertencias));
    }

    private static string MotivoDelProtocolo(int? protocolo) => protocolo switch
    {
        ProtocoloFtp => "FTP: CMC sólo abre conexiones sobre SSH.",
        null => "No declara protocolo, así que no se puede saber si va sobre SSH.",
        _ => string.Format(
            CultureInfo.CurrentCulture,
            "Protocolo {0} de FileZilla, que CMC no maneja: sólo abre conexiones sobre SSH.",
            protocolo.Value),
    };

    private static StoredCredential? LeerContrasena(
        XElement servidor, string? usuario, List<string> advertencias)
    {
        var pass = servidor.Element("Pass");

        if (pass is null || SinContrasenaGuardada(CampoEntero(servidor, "Logontype")))
        {
            return null;
        }

        var codificacion = pass.Attribute("encoding")?.Value;

        if (string.Equals(codificacion, "crypt", StringComparison.OrdinalIgnoreCase))
        {
            advertencias.Add(AvisoContrasenaConMaestra);

            return null;
        }

        if (string.Equals(codificacion, "base64", StringComparison.OrdinalIgnoreCase))
        {
            return ContrasenaEnBase64(pass.Value, usuario, advertencias);
        }

        return pass.Value.Length == 0
            ? null
            : new StoredCredential(usuario ?? string.Empty, null, pass.Value);
    }

    // <Logontype>: 0 anónimo, 1 normal, 2 preguntar, 3 interactivo, 4 cuenta, 5 archivo de clave.
    private static bool SinContrasenaGuardada(int? logontype) => logontype is 0 or 2 or 3;

    private static StoredCredential? ContrasenaEnBase64(
        string valor, string? usuario, List<string> advertencias)
    {
        byte[] bytes;

        try
        {
            bytes = Convert.FromBase64String(valor);
        }
        catch (FormatException)
        {
            advertencias.Add(AvisoContrasenaEnBase64Invalido);

            return null;
        }

        var caracteres = new char[Encoding.UTF8.GetMaxCharCount(bytes.Length)];

        try
        {
            var largo = Encoding.UTF8.GetChars(bytes, 0, bytes.Length, caracteres, 0);

            return largo == 0
                ? null
                : new StoredCredential(usuario ?? string.Empty, null, caracteres.AsSpan(0, largo));
        }
        finally
        {
            Array.Clear(bytes);
            Array.Clear(caracteres);
        }
    }

    private static string? Campo(XElement servidor, string nombre)
    {
        var valor = servidor.Element(nombre)?.Value.Trim();

        return string.IsNullOrEmpty(valor) ? null : valor;
    }

    private static int? CampoEntero(XElement servidor, string nombre) =>
        int.TryParse(
            Campo(servidor, nombre), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)
            ? n
            : null;

    // Sin puerto usable queda en null y no en 22: en CMC null hereda del árbol.
    private static int? PuertoDeclarado(XElement servidor) =>
        CampoEntero(servidor, "Port") is int puerto && puerto is > 0 and <= 65535 ? puerto : null;
}
