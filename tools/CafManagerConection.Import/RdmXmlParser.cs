using System.Xml.Linq;
using CafManagerConection.Domain.Connections;

namespace CafManagerConection.Import;

/// <summary>Una entrada del export, ya traducida al modelo de CMC.</summary>
public sealed record ImportedConnection(
    string Name,
    Protocol Protocol,
    string Host,
    int? Port,
    string? UserName,
    string FolderPath,
    string? Url,
    string? Browser,
    IReadOnlyList<ImportedTunnel> Tunnels);

public sealed record ImportedTunnel(
    string Name, int LocalPort, string RemoteHost, int RemotePort);

/// <summary>Una entrada que no se pudo traducir, con el motivo.</summary>
public sealed record SkippedEntry(string Name, string Type, string Reason);

public sealed record ImportPlan(
    IReadOnlyList<string> FolderPaths,
    IReadOnlyList<ImportedConnection> Connections,
    IReadOnlyList<SkippedEntry> Skipped,
    int EncryptedPasswordCount,
    int NonLocalTunnelCount);

/// <summary>
/// Lee un export XML de Remote Desktop Manager (Devolutions) y lo traduce al modelo de CMC.
/// </summary>
/// <remarks>
/// <b>Las contraseñas no se migran.</b> RDM las exporta en el campo <c>SafePassword</c>
/// cifradas con la clave del data source: sin RDM no se pueden descifrar, y este importador
/// no lo intenta. Se cuentan para informarlo, y nada más.
/// <para>
/// Es una herramienta de migración puntual, deliberadamente fuera del producto: el
/// Principio V mantiene los importadores fuera de alcance, y esto se corre una vez.
/// </para>
/// </remarks>
public static class RdmXmlParser
{
    public static ImportPlan Parse(string xml)
    {
        var doc = XDocument.Parse(xml);
        var entries = doc.Root?.Elements("Connection").ToList() ?? [];

        var connections = new List<ImportedConnection>();
        var skipped = new List<SkippedEntry>();
        var folderPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var encrypted = doc.Descendants("SafePassword")
            .Count(e => !string.IsNullOrWhiteSpace(e.Value));

        // Reenvios que CMC no soporta: solo hace reenvio de puerto local.
        var noLocales = doc.Descendants("PortForward")
            .Count(e => !string.Equals(
                e.Element("Mode")?.Value, "Local", StringComparison.OrdinalIgnoreCase));

        foreach (var entry in entries)
        {
            var type = Value(entry, "ConnectionType") ?? "(sin tipo)";
            var name = Value(entry, "Name") ?? "(sin nombre)";
            var group = Value(entry, "Group") ?? string.Empty;

            // Los grupos de RDM son las carpetas. Se registran incluso los vacios de
            // contenido, porque el usuario los creo a proposito.
            //
            // Ojo: en un nodo Group, el campo Group YA es la ruta completa e incluye su
            // propio nombre ("Padre\Hija" para el grupo llamado "Hija"). Concatenarle el
            // nombre otra vez crea una carpeta fantasma duplicada dentro de si misma.
            if (type == "Group")
            {
                RegisterPath(folderPaths, string.IsNullOrWhiteSpace(group) ? name : group);
                continue;
            }

            if (!string.IsNullOrWhiteSpace(group))
            {
                RegisterPath(folderPaths, group);
            }

            var imported = type switch
            {
                "SSHShell" => FromTerminal(entry, group, Protocol.Ssh),
                "PortForward" => FromTerminal(entry, group, Protocol.Ssh),
                "Putty" => FromPutty(entry, group),
                "RDPConfigured" => FromRdp(entry, group),
                "WebBrowser" => FromWeb(entry, group),
                _ => null,
            };

            if (imported is null)
            {
                skipped.Add(new SkippedEntry(name, type, ReasonFor(type)));
                continue;
            }

            if (string.IsNullOrWhiteSpace(imported.Host))
            {
                skipped.Add(new SkippedEntry(name, type, "no tiene host ni dirección"));
                continue;
            }

            connections.Add(imported);
        }

        return new ImportPlan(
            folderPaths.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList(),
            connections,
            skipped,
            encrypted,
            noLocales);
    }

    private static string ReasonFor(string type) => type switch
    {
        "Ftp" => "FTP/SFTP todavía no está implementado como tipo de conexión",
        "AddOn" => "los complementos de RDM no tienen equivalente",
        "SessionTool" => "las herramientas de sesión no tienen equivalente",
        "Credential" => "las credenciales sueltas no se migran (van al Credential Manager)",
        "Root" => "nodo raíz del export, no es una conexión",
        _ => $"el tipo '{type}' no tiene equivalente en CMC",
    };

    private static ImportedConnection? FromTerminal(XElement entry, string group, Protocol protocol)
    {
        var terminal = entry.Element("Terminal");
        if (terminal is null)
        {
            return null;
        }

        return new ImportedConnection(
            Value(entry, "Name") ?? "(sin nombre)",
            protocol,
            Value(terminal, "Host") ?? string.Empty,
            ParsePort(Value(terminal, "HostPort")),
            Value(terminal, "Username"),
            group,
            Url: null,
            Browser: null,
            ParseTunnels(terminal));
    }

    /// <summary>
    /// PuTTY guarda el host como <c>usuario@host</c> cuando el usuario está embebido.
    /// Se separan para que el usuario quede en su campo y pueda heredarse.
    /// </summary>
    private static ImportedConnection? FromPutty(XElement entry, string group)
    {
        var putty = entry.Element("Putty");
        if (putty is null)
        {
            return null;
        }

        var host = Value(putty, "Host") ?? string.Empty;
        var user = Value(putty, "LoginName");

        var at = host.IndexOf('@', StringComparison.Ordinal);
        if (at > 0)
        {
            user ??= host[..at];
            host = host[(at + 1)..];
        }

        return new ImportedConnection(
            Value(entry, "Name") ?? "(sin nombre)",
            Protocol.Ssh,
            host,
            ParsePort(Value(putty, "Port")),
            user,
            group,
            Url: null,
            Browser: null,
            []);
    }

    private static ImportedConnection FromRdp(XElement entry, string group) => new(
        Value(entry, "Name") ?? "(sin nombre)",
        Protocol.Rdp,
        // En RDM, el host de una conexión RDP vive en Url.
        Value(entry, "Url") ?? string.Empty,
        null,
        Value(entry.Element("RDP"), "UserName"),
        group,
        Url: null,
        Browser: null,
        []);

    private static ImportedConnection FromWeb(XElement entry, string group)
    {
        var url = Value(entry, "WebBrowserUrl") ?? string.Empty;
        var host = Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : url;

        return new ImportedConnection(
            Value(entry, "Name") ?? "(sin nombre)",
            Protocol.Web,
            host,
            null,
            null,
            group,
            url,
            MapBrowser(Value(entry, "WebBrowserApplication")),
            []);
    }

    /// <summary>
    /// RDM nombra el navegador por su identificador interno. Se traduce a una ruta sólo si
    /// se puede resolver; si no, se deja nulo, que en CMC significa "el predeterminado".
    /// </summary>
    private static string? MapBrowser(string? application)
    {
        if (string.IsNullOrWhiteSpace(application))
        {
            return null;
        }

        var candidatos = application switch
        {
            "GoogleChrome" =>
            [
                @"C:\Program Files\Google\Chrome\Application\chrome.exe",
                @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
            ],
            "Firefox" => new[] { @"C:\Program Files\Mozilla Firefox\firefox.exe" },
            "Edge" => [@"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"],
            _ => [],
        };

        return candidatos.FirstOrDefault(File.Exists);
    }

    /// <summary>
    /// Sólo se migran los reenvíos locales: CMC no soporta reenvío remoto ni SOCKS, y la
    /// constitución los deja explícitamente fuera de alcance.
    /// </summary>
    private static List<ImportedTunnel> ParseTunnels(XElement terminal)
    {
        var result = new List<ImportedTunnel>();

        foreach (var pf in terminal.Element("PortForwards")?.Elements("PortForward") ?? [])
        {
            var mode = Value(pf, "Mode");
            if (!string.Equals(mode, "Local", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var localPort = ParsePort(Value(pf, "SourcePort"));
            var remotePort = ParsePort(Value(pf, "DestinationPort"));
            var destino = Value(pf, "Destination");

            if (localPort is null || remotePort is null || string.IsNullOrWhiteSpace(destino))
            {
                continue;
            }

            result.Add(new ImportedTunnel(
                Value(pf, "Description") is { Length: > 0 } d ? d : destino,
                localPort.Value,
                destino,
                remotePort.Value));
        }

        return result;
    }

    /// <summary>Registra la ruta y todas sus rutas ancestro, para no perder niveles.</summary>
    private static void RegisterPath(HashSet<string> paths, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var parts = path.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 1; i <= parts.Length; i++)
        {
            paths.Add(string.Join('\\', parts[..i]));
        }
    }

    private static int? ParsePort(string? value) =>
        int.TryParse(value, out var port) && port is >= 1 and <= 65535 ? port : null;

    private static string? Value(XElement? parent, string name)
    {
        var v = parent?.Element(name)?.Value;
        return string.IsNullOrWhiteSpace(v) ? null : v.Trim();
    }
}
