using System.Globalization;
using System.Text.Json;

namespace CafManagerConection.Platform;

/// <summary>Interpreta <c>docker ps</c> pedido en JSON: la salida en tabla trunca los nombres largos (FR-095).</summary>
public static class DockerPsParser
{
    public const string Format = "{{json .}}";

    public static IReadOnlyList<ContainerInfo> Parse(string salida)
    {
        var resultado = new List<ContainerInfo>();

        foreach (var linea in salida.ReplaceLineEndings("\n").Split('\n'))
        {
            var texto = linea.Trim();

            if (texto.Length == 0 || !texto.StartsWith('{'))
            {
                continue;
            }

            try
            {
                using var doc = JsonDocument.Parse(texto);
                var raiz = doc.RootElement;

                var estado = Texto(raiz, "State");
                var status = Texto(raiz, "Status");

                // Con formato json, State puede faltar en versiones viejas: se deduce de Status, que empieza con Up.
                if (estado.Length == 0)
                {
                    estado = status.StartsWith("Up", StringComparison.OrdinalIgnoreCase)
                        ? "running"
                        : "exited";
                }

                var (proyecto, servicio) = ParseComposeLabels(Texto(raiz, "Labels"));

                resultado.Add(new ContainerInfo(
                    Texto(raiz, "ID"),
                    Texto(raiz, "Names"),
                    Texto(raiz, "Image"),
                    estado,
                    status,
                    ParsePorts(Texto(raiz, "Ports")),
                    proyecto,
                    servicio));
            }
            catch (JsonException)
            {
            }
        }

        return resultado;
    }

    private static string Texto(JsonElement e, string nombre) =>
        e.TryGetProperty(nombre, out var v) ? v.GetString() ?? string.Empty : string.Empty;

    /// <summary>Proyecto y servicio compose desde las etiquetas <c>com.docker.compose.*</c>, que llegan como <c>clave=valor</c> separadas por comas.</summary>
    // Deducirlo del nombre falla: el separador cambió entre versiones (_ en la v1, - en la v2).
    internal static (string? Project, string? Service) ParseComposeLabels(string etiquetas)
    {
        if (etiquetas.Length == 0)
        {
            return (null, null);
        }

        string? proyecto = null;
        string? servicio = null;

        foreach (var par in etiquetas.Split(','))
        {
            var i = par.IndexOf('=', StringComparison.Ordinal);

            if (i <= 0)
            {
                continue;
            }

            var clave = par[..i].Trim();
            var valor = par[(i + 1)..].Trim();

            if (valor.Length == 0)
            {
                continue;
            }

            if (clave.Equals("com.docker.compose.project", StringComparison.Ordinal))
            {
                proyecto = valor;
            }
            else if (clave.Equals("com.docker.compose.service", StringComparison.Ordinal))
            {
                servicio = valor;
            }
        }

        return (proyecto, servicio);
    }

    // Docker lista IPv4 e IPv6 por separado (0.0.0.0:80->80/tcp y :::80->80/tcp); sin la flecha el puerto no sale de la red de Docker.
    internal static IReadOnlyList<string> ParsePorts(string ports)
    {
        if (string.IsNullOrWhiteSpace(ports))
        {
            return [];
        }

        return ports
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(p => p.Contains("->", StringComparison.Ordinal))
            .Select(SinDireccion)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static string SinDireccion(string mapeo)
    {
        var flecha = mapeo.IndexOf("->", StringComparison.Ordinal);
        var izquierda = mapeo[..flecha];
        var ultimoDosPuntos = izquierda.LastIndexOf(':');

        return ultimoDosPuntos >= 0
            ? mapeo[(ultimoDosPuntos + 1)..]
            : mapeo;
    }
}

/// <summary>Interpreta <c>docker compose ls --format json</c> y <c>docker compose config --services</c>.</summary>
public static class ComposeParser
{
    public static IReadOnlyList<(string Name, string FilePath)> ParseProjects(string salida)
    {
        var resultado = new List<(string, string)>();
        var texto = salida.Trim();

        if (texto.Length == 0)
        {
            return resultado;
        }

        try
        {
            using var doc = JsonDocument.Parse(texto);

            // `compose ls` devuelve un arreglo; algunas versiones, una línea por objeto.
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in doc.RootElement.EnumerateArray())
                {
                    Agregar(item);
                }
            }
            else
            {
                Agregar(doc.RootElement);
            }
        }
        catch (JsonException)
        {
            foreach (var linea in texto.ReplaceLineEndings("\n").Split('\n'))
            {
                var l = linea.Trim();
                if (!l.StartsWith('{'))
                {
                    continue;
                }

                try
                {
                    using var doc = JsonDocument.Parse(l);
                    Agregar(doc.RootElement);
                }
                catch (JsonException)
                {
                }
            }
        }

        return resultado;

        void Agregar(JsonElement e)
        {
            var nombre = e.TryGetProperty("Name", out var n) ? n.GetString() : null;
            var archivos = e.TryGetProperty("ConfigFiles", out var c) ? c.GetString() : null;

            if (!string.IsNullOrEmpty(nombre))
            {
                resultado.Add((nombre, archivos ?? string.Empty));
            }
        }
    }

    /// <summary>Relaciona servicios y contenedores por el nombre <c>proyecto-servicio-N</c> de Compose v2 (FR-098).</summary>
    public static IReadOnlyList<ComposeService> Correlate(
        string proyecto,
        IReadOnlyList<string> servicios,
        IReadOnlyList<ContainerInfo> contenedores)
    {
        var resultado = new List<ComposeService>();

        foreach (var servicio in servicios)
        {
            var prefijo = $"{proyecto}-{servicio}";
            var prefijoViejo = $"{proyecto}_{servicio}";

            var contenedor = contenedores.FirstOrDefault(c =>
                c.Name.StartsWith(prefijo, StringComparison.OrdinalIgnoreCase) ||
                c.Name.StartsWith(prefijoViejo, StringComparison.OrdinalIgnoreCase));

            resultado.Add(new ComposeService(
                servicio, contenedor?.Name, contenedor?.IsRunning ?? false));
        }

        return resultado;
    }

    public static IReadOnlyList<string> ParseServices(string salida) =>
        salida.ReplaceLineEndings("\n")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(l => l.Length > 0 && !l.StartsWith("WARN", StringComparison.Ordinal))
            .ToList();
}

/// <summary>Interpreta <c>nginx -T</c>: la configuración efectiva, con los <c>include</c> ya resueltos (FR-101).</summary>
public static class NginxConfigParser
{
    public static IReadOnlyList<NginxSite> Parse(string salida)
    {
        var sitios = new List<NginxSite>();

        var archivoActual = string.Empty;
        var profundidad = 0;
        var enServer = false;
        var profundidadServer = 0;

        var nombres = new List<string>();
        var puertos = new List<int>();
        string? raiz = null;

        foreach (var lineaCruda in salida.ReplaceLineEndings("\n").Split('\n'))
        {
            var linea = lineaCruda.Trim();

            // nginx -T antecede cada archivo con un comentario de configuración.
            if (linea.StartsWith("# configuration file ", StringComparison.Ordinal))
            {
                archivoActual = linea["# configuration file ".Length..].TrimEnd(':');
                continue;
            }

            if (linea.StartsWith('#') || linea.Length == 0)
            {
                continue;
            }

            var abre = linea.Count(c => c == '{');
            var cierra = linea.Count(c => c == '}');

            if (!enServer && linea.StartsWith("server", StringComparison.Ordinal) && abre > 0)
            {
                enServer = true;
                profundidadServer = profundidad;
                nombres = [];
                puertos = [];
                raiz = null;
            }
            else if (enServer)
            {
                if (linea.StartsWith("server_name", StringComparison.Ordinal))
                {
                    nombres.AddRange(Valores(linea, "server_name"));
                }
                else if (linea.StartsWith("listen", StringComparison.Ordinal))
                {
                    foreach (var v in Valores(linea, "listen"))
                    {
                        var puerto = v.Contains(':', StringComparison.Ordinal)
                            ? v[(v.LastIndexOf(':') + 1)..]
                            : v;

                        if (int.TryParse(puerto, out var p))
                        {
                            puertos.Add(p);
                        }
                    }
                }
                else if (linea.StartsWith("root", StringComparison.Ordinal))
                {
                    raiz = Valores(linea, "root").FirstOrDefault();
                }
            }

            profundidad += abre - cierra;

            if (enServer && profundidad <= profundidadServer)
            {
                enServer = false;

                if (nombres.Count > 0 || puertos.Count > 0)
                {
                    sitios.Add(new NginxSite(
                        $"{archivoActual}#{sitios.Count}",
                        nombres.Distinct(StringComparer.Ordinal).ToList(),
                        puertos.Distinct().ToList(),
                        raiz,
                        archivoActual));
                }
            }
        }

        return sitios;
    }

    private static IEnumerable<string> Valores(string linea, string directiva) =>
        linea[directiva.Length..]
            .TrimEnd(';', ' ')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(v => v is not ("default_server" or "ssl" or "http2" or "ipv6only=on"));
}

public static class SupervisorStatusParser
{
    public static IReadOnlyList<SupervisorProcess> Parse(string salida)
    {
        var resultado = new List<SupervisorProcess>();

        foreach (var linea in salida.ReplaceLineEndings("\n").Split('\n'))
        {
            var texto = linea.Trim();

            if (texto.Length == 0)
            {
                continue;
            }

            var partes = texto.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (partes.Length < 2)
            {
                continue;
            }

            var nombre = partes[0];
            var estado = partes[1];

            if (!estado.All(c => char.IsUpper(c) || c == '_'))
            {
                continue;
            }

            var detalle = partes.Length > 2
                ? string.Join(' ', partes.Skip(2))
                : null;

            resultado.Add(new SupervisorProcess(nombre, estado, detalle));
        }

        return resultado;
    }
}

/// <summary>Lee la salida de <c>docker stats --no-stream</c>.</summary>
// Formato de plantilla y no {{json .}}: los campos vienen ya formateados en los dos casos.
public static class DockerStatsParser
{
    public const string Format = "{{.ID}}\t{{.CPUPerc}}\t{{.MemUsage}}";

    public static IReadOnlyDictionary<string, ContainerUsage> Parse(string salida)
    {
        var resultado = new Dictionary<string, ContainerUsage>(StringComparer.Ordinal);

        foreach (var linea in salida.ReplaceLineEndings("\n").Split('\n'))
        {
            var partes = linea.Trim().Split('\t');

            if (partes.Length < 3 || partes[0].Length == 0)
            {
                continue;
            }

            var id = partes[0].Trim();
            var cpu = ParsePorcentaje(partes[1]);
            var (uso, limite) = ParseMemoria(partes[2]);

            resultado[id] = new ContainerUsage(id, cpu, uso, limite);
        }

        return resultado;
    }

    private static double ParsePorcentaje(string texto)
    {
        var limpio = texto.Trim().TrimEnd('%');

        return double.TryParse(
            limpio, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0;
    }

    /// <summary>Convierte <c>"1.5GiB / 8GiB"</c> en bytes de uso y de límite.</summary>
    private static (long Uso, long Limite) ParseMemoria(string texto)
    {
        var partes = texto.Split('/');

        return partes.Length < 2
            ? (ParseTamano(partes.ElementAtOrDefault(0) ?? string.Empty), 0)
            : (ParseTamano(partes[0]), ParseTamano(partes[1]));
    }

    /// <summary>Tamaño con sufijo de Docker a bytes: <c>KiB/MiB/GiB</c> son 1024 y <c>kB/MB/GB</c> son 1000.</summary>
    // Tratar todo como 1024 da un error del 7 % en los valores en GB.
    internal static long ParseTamano(string texto)
    {
        var limpio = texto.Trim();

        if (limpio.Length == 0)
        {
            return 0;
        }

        var corte = 0;

        while (corte < limpio.Length
               && (char.IsDigit(limpio[corte]) || limpio[corte] is '.' or ','))
        {
            corte++;
        }

        if (!double.TryParse(
                limpio[..corte].Replace(',', '.'),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var numero))
        {
            return 0;
        }

        var factor = limpio[corte..].Trim().ToLowerInvariant() switch
        {
            "b" or "" => 1D,
            "kib" => 1024D,
            "mib" => 1024D * 1024,
            "gib" => 1024D * 1024 * 1024,
            "tib" => 1024D * 1024 * 1024 * 1024,
            "kb" => 1000D,
            "mb" => 1000D * 1000,
            "gb" => 1000D * 1000 * 1000,
            "tb" => 1000D * 1000 * 1000 * 1000,
            _ => 1D,
        };

        return (long)(numero * factor);
    }
}

/// <summary>Lee <c>ss -tulnpH</c> o <c>netstat -tulnp</c>: los puertos a la escucha.</summary>
// Se filtran las conexiones establecidas: un servidor con tráfico devuelve cientos de sockets en ESTAB.
public static class PuertosParser
{
    public static IReadOnlyList<ListeningPort> Parse(string salida)
    {
        var resultado = new List<ListeningPort>();
        var vistos = new HashSet<string>(StringComparer.Ordinal);

        foreach (var linea in salida.ReplaceLineEndings("\n").Split('\n'))
        {
            var texto = linea.Trim();

            if (texto.Length == 0 ||
                texto.StartsWith("Netid", StringComparison.OrdinalIgnoreCase) ||
                texto.StartsWith("Proto", StringComparison.OrdinalIgnoreCase) ||
                texto.StartsWith("Active", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var partes = texto.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (partes.Length < 4)
            {
                continue;
            }

            var protocolo = partes[0].ToLowerInvariant();

            if (!protocolo.StartsWith("tcp", StringComparison.Ordinal) &&
                !protocolo.StartsWith("udp", StringComparison.Ordinal))
            {
                continue;
            }

            // La dirección local es la primera columna con formato host:puerto; buscarla tolera las variantes de ancho.
            var local = partes.FirstOrDefault(EsDireccionConPuerto);

            if (local is null || !TryPuerto(local, out var puerto, out var direccion))
            {
                continue;
            }

            var proceso = LeerProceso(texto);

            // La misma escucha aparece dos veces cuando el servicio abre IPv4 e IPv6.
            var clave = $"{protocolo}:{puerto}:{proceso}";

            if (!vistos.Add(clave))
            {
                continue;
            }

            resultado.Add(new ListeningPort(
                protocolo, direccion, puerto, proceso, LeerPid(texto)));
        }

        return resultado
            .OrderBy(p => p.Port)
            .ThenBy(p => p.Protocol, StringComparer.Ordinal)
            .ToList();
    }

    private static bool EsDireccionConPuerto(string valor) =>
        valor.LastIndexOf(':') > 0 &&
        int.TryParse(valor[(valor.LastIndexOf(':') + 1)..], out var p) &&
        p is > 0 and <= 65535;

    private static bool TryPuerto(string valor, out int puerto, out string direccion)
    {
        var corte = valor.LastIndexOf(':');

        direccion = valor[..corte];

        // ss le pega el nombre de la interfaz a la dirección cuando el socket está atado a una sola: 127.0.0.53%lo:53.
        var interfaz = direccion.IndexOf('%');

        if (interfaz > 0)
        {
            direccion = direccion[..interfaz];
        }

        direccion = direccion.Length == 0 ? "*" : direccion;

        return int.TryParse(valor[(corte + 1)..], out puerto);
    }

    /// <summary>PID dueño del socket: <c>pid=1234</c> en <c>ss</c>, <c>1234/nginx</c> en <c>netstat</c> (FR-165).</summary>
    private static int? LeerPid(string linea)
    {
        const string marca = "pid=";
        var i = linea.IndexOf(marca, StringComparison.Ordinal);

        if (i >= 0)
        {
            var desde = i + marca.Length;
            var hasta = desde;

            while (hasta < linea.Length && char.IsAsciiDigit(linea[hasta]))
            {
                hasta++;
            }

            if (hasta > desde && int.TryParse(linea[desde..hasta], out var pid))
            {
                return pid;
            }
        }

        var ultima = linea.Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        var barra = ultima?.IndexOf('/') ?? -1;

        return barra > 0 && int.TryParse(ultima![..barra], out var dePid) ? dePid : null;
    }

    private static string? LeerProceso(string linea)
    {
        var users = linea.IndexOf("users:((\"", StringComparison.Ordinal);

        if (users >= 0)
        {
            var desde = users + "users:((\"".Length;
            var hasta = linea.IndexOf('"', desde);

            if (hasta > desde)
            {
                return linea[desde..hasta];
            }
        }

        var ultima = linea.Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();

        if (ultima is not null && ultima.Contains('/', StringComparison.Ordinal))
        {
            var nombre = ultima[(ultima.IndexOf('/') + 1)..].TrimEnd(':');
            return nombre.Length > 0 ? nombre : null;
        }

        return null;
    }
}
