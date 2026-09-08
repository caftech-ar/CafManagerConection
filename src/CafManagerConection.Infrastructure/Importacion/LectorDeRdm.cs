using System.Xml.Linq;
using CafManagerConection.Domain.Importacion;

namespace CafManagerConection.Infrastructure.Importacion;

/// <summary>Lee un archivo exportado desde Remote Desktop Manager. A diferencia de los otros tres orígenes, que son clientes SSH, éste trae además RDP y direcciones web.</summary>
public static class LectorDeRdm
{
    public static LecturaDeImportacion Leer(string xml)
    {
        XDocument doc;

        try
        {
            doc = XDocument.Parse(xml);
        }
        catch (System.Xml.XmlException ex)
        {
            return SoloUnMotivo("el archivo", $"no es un XML válido: {ex.Message}");
        }

        var compatibles = new List<ConexionImportada>();
        var omitidas = new List<ImportacionOmitida>();

        foreach (var entrada in doc.Root?.Elements("Connection") ?? [])
        {
            var tipo = Valor(entrada, "ConnectionType") ?? "(sin tipo)";
            var nombre = Valor(entrada, "Name") ?? "(sin nombre)";
            var grupo = Valor(entrada, "Group") ?? string.Empty;

            if (tipo == "Group")
            {
                continue;
            }

            var importada = tipo switch
            {
                "SSHShell" or "PortForward" => DeTerminal(entrada, grupo),
                "Putty" => DePutty(entrada, grupo),
                "RDPConfigured" => DeRdp(entrada, grupo),
                "WebBrowser" => DeWeb(entrada, grupo),
                _ => null,
            };

            if (importada is null)
            {
                omitidas.Add(new ImportacionOmitida(OrigenDeImportacion.Rdm, nombre, Motivo(tipo)));
                continue;
            }

            if (string.IsNullOrWhiteSpace(importada.Host))
            {
                omitidas.Add(new ImportacionOmitida(
                    OrigenDeImportacion.Rdm, nombre, "no tiene host ni dirección"));
                continue;
            }

            compatibles.Add(importada);
        }

        return new LecturaDeImportacion(compatibles, omitidas);
    }

    private static string Motivo(string tipo) => tipo switch
    {
        "Ftp" => "FTP y FTPS no están en alcance; SFTP viaja sobre una conexión SSH",
        "AddOn" => "los complementos no tienen equivalente",
        "SessionTool" => "las herramientas de sesión no tienen equivalente",
        "Credential" => "las credenciales sueltas no se migran: se cargan al usarlas",
        "Root" => "nodo raíz del archivo, no es una conexión",
        _ => $"el tipo «{tipo}» no tiene equivalente en CMC",
    };

    private static ConexionImportada? DeTerminal(XElement entrada, string grupo)
    {
        var terminal = entrada.Element("Terminal");

        return terminal is null
            ? null
            : Armar(
                entrada,
                grupo,
                ProtocoloImportado.Ssh,
                Valor(terminal, "Host") ?? string.Empty,
                Puerto(Valor(terminal, "HostPort")),
                Valor(terminal, "Username"));
    }

    /// <summary>PuTTY guarda el host como <c>usuario@host</c> cuando el usuario está embebido, y así queda en el archivo de RDM. Se separan para que el usuario pueda heredarse.</summary>
    private static ConexionImportada? DePutty(XElement entrada, string grupo)
    {
        var putty = entrada.Element("Putty");

        if (putty is null)
        {
            return null;
        }

        var host = Valor(putty, "Host") ?? string.Empty;
        var usuario = Valor(putty, "LoginName");
        var arroba = host.IndexOf('@', StringComparison.Ordinal);

        if (arroba > 0)
        {
            usuario ??= host[..arroba];
            host = host[(arroba + 1)..];
        }

        return Armar(
            entrada, grupo, ProtocoloImportado.Ssh, host, Puerto(Valor(putty, "Port")), usuario);
    }

    // En Remote Desktop Manager el host de una conexion RDP vive en Url, no en un campo Host.
    private static ConexionImportada DeRdp(XElement entrada, string grupo) =>
        Armar(
            entrada,
            grupo,
            ProtocoloImportado.Rdp,
            Valor(entrada, "Url") ?? string.Empty,
            null,
            Valor(entrada.Element("RDP"), "UserName"),
            dominio: Valor(entrada.Element("RDP"), "Domain"));

    private static ConexionImportada DeWeb(XElement entrada, string grupo)
    {
        var url = Valor(entrada, "WebBrowserUrl") ?? string.Empty;
        var host = Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : url;

        return Armar(entrada, grupo, ProtocoloImportado.Web, host, null, null, url: url);
    }

    private static ConexionImportada Armar(
        XElement entrada,
        string grupo,
        ProtocoloImportado protocolo,
        string host,
        int? puerto,
        string? usuario,
        string? url = null,
        string? dominio = null) =>
        new(
            OrigenDeImportacion.Rdm,
            Valor(entrada, "Name") ?? "(sin nombre)",
            Carpetas(grupo),
            host,
            puerto,
            string.IsNullOrWhiteSpace(usuario) ? null : usuario,
            RutaDeClavePrivada: null,
            ProtocoloOriginal: Valor(entrada, "ConnectionType") ?? "(sin tipo)",
            Credencial: null,
            Advertencias: null,
            Protocolo: protocolo,
            Url: url,
            Dominio: string.IsNullOrWhiteSpace(dominio) ? null : dominio);

    private static IReadOnlyList<string> Carpetas(string grupo) =>
        string.IsNullOrWhiteSpace(grupo)
            ? []
            : [.. grupo.Split('\\', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

    private static int? Puerto(string? texto) =>
        int.TryParse(texto, out var puerto) && puerto is > 0 and <= 65535 ? puerto : null;

    private static string? Valor(XElement? padre, string nombre) => padre?.Element(nombre)?.Value;

    private static LecturaDeImportacion SoloUnMotivo(string nombre, string motivo) =>
        new([], [new ImportacionOmitida(OrigenDeImportacion.Rdm, nombre, motivo)]);
}
