namespace CafManagerConection.Platform;

public sealed record DetalleDeContenedor
{
    public string Nombre { get; init; } = string.Empty;

    public string Imagen { get; init; } = string.Empty;

    public string Estado { get; init; } = string.Empty;

    public string? Salud { get; init; }

    public int Reinicios { get; init; }

    public DateTimeOffset? Desde { get; init; }

    public string? Politica { get; init; }

    public string? Directorio { get; init; }

    public string? Comando { get; init; }

    public string Cpu { get; init; } = string.Empty;

    public string Memoria { get; init; } = string.Empty;

    public string MemoriaPorcentaje { get; init; } = string.Empty;

    public string Red { get; init; } = string.Empty;

    public string Disco { get; init; } = string.Empty;

    public string Procesos { get; init; } = string.Empty;

    public IReadOnlyList<string> Puertos { get; init; } = [];

    public IReadOnlyList<string> Volumenes { get; init; } = [];

    public string? Id { get; init; }

    /// <summary>Digest corto: dos contenedores con <c>nginx:latest</c> pueden correr imágenes distintas.</summary>
    public string? Digest { get; init; }

    public DateTimeOffset? Creado { get; init; }

    public IReadOnlyList<string> Redes { get; init; } = [];

    public string? Proyecto { get; init; }

    public string? Servicio { get; init; }

    /// <summary>Distingue «el registro vino vacío» de «no se pudo leer» (FR-150e).</summary>
    public bool RegistroLeido { get; init; }

    public string Registro { get; init; } = string.Empty;

    public bool TieneAlgo => Imagen.Length > 0 || Estado.Length > 0 || Registro.Length > 0;

    public TimeSpan? Uptime => Desde is { } d && d > DateTimeOffset.UnixEpoch
        ? DateTimeOffset.UtcNow - d
        : null;

    /// <summary>Mismo criterio que <see cref="ContainerInfo.Gravedad"/>, pero acá la salud viene en su propio campo (FR-150a).</summary>
    public GravedadDeContenedor Gravedad
    {
        get
        {
            var estado = Estado.Trim().ToLowerInvariant();

            if (estado == "running")
            {
                return Salud is { } salud && salud.Equals("unhealthy", StringComparison.OrdinalIgnoreCase)
                    ? GravedadDeContenedor.Falla
                    : GravedadDeContenedor.Corriendo;
            }

            return estado switch
            {
                "restarting" or "paused" or "created" => GravedadDeContenedor.Advertencia,
                "dead" => GravedadDeContenedor.Falla,
                _ => GravedadDeContenedor.Detenido,
            };
        }
    }

    public static DetalleDeContenedor Interpretar(string nombre, string salida)
    {
        var tramos = Cortar(salida ?? string.Empty);

        var detalle = new DetalleDeContenedor { Nombre = nombre };

        detalle = ConResumen(detalle, Primera(tramos, ControlDeDocker.Marca.Resumen));
        detalle = ConConsumo(detalle, Primera(tramos, ControlDeDocker.Marca.Consumo));
        detalle = ConSalud(detalle, Primera(tramos, ControlDeDocker.Marca.Salud));
        detalle = ConCompose(detalle, Primera(tramos, ControlDeDocker.Marca.Compose));

        return detalle with
        {
            Puertos = Lineas(tramos, ControlDeDocker.Marca.Puertos),
            Volumenes = Lineas(tramos, ControlDeDocker.Marca.Volumenes),
            Redes = Lineas(tramos, ControlDeDocker.Marca.Redes),
            Registro = Tramo(tramos, ControlDeDocker.Marca.Registro).Trim(),

            // La marca del registro en la salida dice que el comando corrió: sin ella, no llegó a ejecutarse (FR-150e).
            RegistroLeido = tramos.ContainsKey(ControlDeDocker.Marca.Registro),
        };
    }

    private static Dictionary<string, string> Cortar(string salida)
    {
        var tramos = new Dictionary<string, string>(StringComparer.Ordinal);
        var actual = string.Empty;
        var texto = new System.Text.StringBuilder();

        foreach (var linea in salida.ReplaceLineEndings("\n").Split('\n'))
        {
            var limpia = linea.Trim();

            if (limpia.StartsWith("cmc:", StringComparison.Ordinal))
            {
                if (actual.Length > 0)
                {
                    tramos[actual] = texto.ToString();
                }

                actual = limpia;
                texto.Clear();
                continue;
            }

            if (actual.Length > 0)
            {
                texto.Append(linea).Append('\n');
            }
        }

        if (actual.Length > 0)
        {
            tramos[actual] = texto.ToString();
        }

        return tramos;
    }

    private static string Tramo(Dictionary<string, string> tramos, string marca) =>
        tramos.TryGetValue(marca, out var texto) ? texto : string.Empty;

    private static string Primera(Dictionary<string, string> tramos, string marca) =>
        Tramo(tramos, marca)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(l => l.Trim().Length > 0)
            ?.Trim()
        ?? string.Empty;

    private static IReadOnlyList<string> Lineas(
        Dictionary<string, string> tramos, string marca) =>
        [
            .. Tramo(tramos, marca)
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => l.Length > 0),
        ];

    // <c>docker inspect</c> devuelve la cadena literal <c>&lt;no value&gt;</c> cuando el campo no existe.
    private static DetalleDeContenedor ConResumen(DetalleDeContenedor d, string linea)
    {
        var p = linea.Split('|');

        string? Campo(int i) =>
            i < p.Length && p[i].Trim() is { Length: > 0 } v && v != "<no value>" ? v : null;

        return d with
        {
            Nombre = Campo(0)?.TrimStart('/') ?? d.Nombre,
            Imagen = Campo(1) ?? string.Empty,
            Estado = Campo(2) ?? string.Empty,
            Reinicios = int.TryParse(Campo(3), out var r) ? r : 0,
            Desde = DateTimeOffset.TryParse(
                Campo(4),
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AdjustToUniversal
                | System.Globalization.DateTimeStyles.AssumeUniversal,
                out var desde)
                ? desde
                : null,
            Politica = Campo(5) is { } pol && pol != "no" ? pol : null,
            Directorio = Campo(6),
            Comando = Campo(7)?.Trim(),

            Id = Corto(Campo(8), 12),
            Digest = Corto(Campo(9)?.Replace("sha256:", string.Empty, StringComparison.Ordinal), 12),
            Creado = DateTimeOffset.TryParse(
                Campo(10),
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AdjustToUniversal
                | System.Globalization.DateTimeStyles.AssumeUniversal,
                out var creado)
                ? creado
                : null,
        };
    }

    /// <summary>La salud llega en su propio tramo: sin healthcheck no existe el campo y la plantilla de Go abortaría entera.</summary>
    private static DetalleDeContenedor ConSalud(DetalleDeContenedor d, string linea)
    {
        var texto = linea.Trim();

        return texto is { Length: > 0 } && texto != "<no value>"
            ? d with { Salud = texto }
            : d;
    }

    private static DetalleDeContenedor ConCompose(DetalleDeContenedor d, string linea)
    {
        var p = linea.Split('|');

        string? Campo(int i) =>
            i < p.Length && p[i].Trim() is { Length: > 0 } v && v != "<no value>" ? v : null;

        return d with { Proyecto = Campo(0), Servicio = Campo(1) };
    }

    private static string? Corto(string? valor, int largo) => valor is null
        ? null
        : valor.Length <= largo ? valor : valor[..largo];

    private static DetalleDeContenedor ConConsumo(DetalleDeContenedor d, string linea)
    {
        var p = linea.Split('|');

        string Campo(int i) => i < p.Length ? p[i].Trim() : string.Empty;

        return d with
        {
            Cpu = Campo(0),
            Memoria = Campo(1),
            MemoriaPorcentaje = Campo(2),
            Red = Campo(3),
            Disco = Campo(4),
            Procesos = Campo(5),
        };
    }
}
