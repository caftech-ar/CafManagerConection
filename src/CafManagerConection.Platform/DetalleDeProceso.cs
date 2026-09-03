namespace CafManagerConection.Platform;

// Sólo lectura: no hay matar, ni señalar, ni cambiar prioridad (FR-165c); un kill equivocado no tiene deshacer.
// Cada dato se lee por separado: sin permisos los enlaces de /proc fallan y el resto de la ficha sale igual (FR-165a).
public sealed record DetalleDeProceso
{
    public int Pid { get; init; }

    public string Nombre { get; init; } = string.Empty;

    public string? Usuario { get; init; }

    public string? Binario { get; init; }

    public string? Directorio { get; init; }

    public string? Comando { get; init; }

    public TimeSpan? Corriendo { get; init; }

    public int? Padre { get; init; }

    public int? Hilos { get; init; }

    public IReadOnlyList<string> NoSePudo { get; init; } = [];

    public bool Existe { get; init; }

    public bool TieneAlgo =>
        Usuario is not null || Binario is not null || Comando is not null || Hilos is not null;

    public static DetalleDeProceso Interpretar(int pid, string nombre, string salida)
    {
        var tramos = Cortar(salida ?? string.Empty);
        var noSePudo = new List<string>();

        // ps es lo único que necesita existir: sin su línea, o el proceso ya no está o no hay permiso ni para verlo (FR-165d).
        var ps = Primera(tramos, MarcaDeProceso.Ps);
        var campos = ps.Split('|', StringSplitOptions.TrimEntries);

        string? Campo(int i) =>
            i < campos.Length && campos[i] is { Length: > 0 } v && v != "-" ? v : null;

        var existe = ps.Length > 0;
        var binario = Leer(MarcaDeProceso.Binario, "La ruta del binario");
        var directorio = Leer(MarcaDeProceso.Directorio, "El directorio de trabajo");

        // Un tramo vacío también es un fallo de lectura: readlink sobre el /proc de un proceso ajeno no imprime nada (FR-165a).
        string Leer(string marca, string queCosa)
        {
            var texto = Primera(tramos, marca);

            if (EsFalloDePermiso(texto) || (existe && texto.Length == 0))
            {
                noSePudo.Add($"{queCosa} necesita permisos que este usuario no tiene.");
                return string.Empty;
            }

            return texto;
        }

        return new DetalleDeProceso
        {
            Pid = pid,
            Nombre = Campo(0) ?? nombre,
            Existe = existe,
            Usuario = Campo(1),
            Corriendo = LeerDuracion(Campo(2)),
            Padre = int.TryParse(Campo(3), out var padre) ? padre : null,
            Hilos = int.TryParse(Campo(4), out var hilos) ? hilos : null,
            Comando = Campo(5),
            Binario = binario is { Length: > 0 } ? binario : null,
            Directorio = directorio is { Length: > 0 } ? directorio : null,
            NoSePudo = noSePudo,
        };
    }

    private static bool EsFalloDePermiso(string texto) =>
        texto.Contains("Permission denied", StringComparison.OrdinalIgnoreCase)
        || texto.Contains("Operation not permitted", StringComparison.OrdinalIgnoreCase)
        || texto.Contains("No such file", StringComparison.OrdinalIgnoreCase)
        || texto.Contains("cannot access", StringComparison.OrdinalIgnoreCase);

    /// <summary>Formato de <c>ps -o etime</c>: <c>[[dd-]hh:]mm:ss</c>, o sea <c>01:23</c>, <c>10:05:33</c> o <c>12-04:11:09</c>.</summary>
    internal static TimeSpan? LeerDuracion(string? etime)
    {
        if (string.IsNullOrWhiteSpace(etime))
        {
            return null;
        }

        var texto = etime.Trim();
        var dias = 0;

        var guion = texto.IndexOf('-');

        if (guion > 0)
        {
            if (!int.TryParse(texto[..guion], out dias))
            {
                return null;
            }

            texto = texto[(guion + 1)..];
        }

        var partes = texto.Split(':');

        if (partes.Length is < 2 or > 3)
        {
            return null;
        }

        var numeros = new int[partes.Length];

        for (var i = 0; i < partes.Length; i++)
        {
            if (!int.TryParse(partes[i], out numeros[i]))
            {
                return null;
            }
        }

        return partes.Length == 3
            ? new TimeSpan(dias, numeros[0], numeros[1], numeros[2])
            : new TimeSpan(dias, 0, numeros[0], numeros[1]);
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

    private static string Primera(Dictionary<string, string> tramos, string marca) =>
        tramos.TryGetValue(marca, out var texto)
            ? texto.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(l => l.Trim().Length > 0)
                ?.Trim() ?? string.Empty
            : string.Empty;
}

internal static class MarcaDeProceso
{
    public const string Ps = "cmc:ps";
    public const string Binario = "cmc:binario";
    public const string Directorio = "cmc:directorio";
}
