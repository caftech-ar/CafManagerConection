using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace CafManagerConection.Platform;

/// <summary>Canal SSH propio para comandos que no terminan y van entregando líneas, como <c>docker logs -f</c> o <c>tail -F</c> (FR-150, FR-185).</summary>
public interface IPlatformLogStreamer
{
    /// <summary>Entrega cada línea a <paramref name="onLinea"/> sin esperar a que el comando termine.</summary>
    /// <returns>Lo que hay que desechar para cerrar el canal y la conexión SSH que lo sostiene.</returns>
    Task<IAsyncDisposable> SeguirAsync(
        string command,
        Action<string> onLinea,
        Action<string?> onCerrado,
        CancellationToken ct = default);
}

/// <summary>Un archivo que un visor declara estar siguiendo, con la fecha de su último cambio (FR-185a).</summary>
public sealed record ArchivoSeguido(string Ruta, DateTimeOffset? Cambiado, string? Falla = null)
{
    public string Cambio() => Cambio(DateTimeOffset.Now);

    public string Cambio(DateTimeOffset ahora)
    {
        if (Falla is { Length: > 0 } falla)
        {
            return falla;
        }

        if (Cambiado is not { } cambiado)
        {
            return "sin fecha";
        }

        return SeguimientoDeArchivo.Hace(cambiado, ahora);
    }
}

public enum ClaseDeAviso
{
    Rotacion,
    Inaccesible,
}

public sealed record AvisoDeSeguimiento(ClaseDeAviso Clase, string Texto);

/// <summary>Arma los comandos que siguen un archivo de registro y lee lo que el servidor contesta (FR-185, FR-185a, FR-185c).</summary>
public static class SeguimientoDeArchivo
{
    public const int LineasIniciales = 200;

    /// <summary>El máximo de PID de Linux, <c>/proc/sys/kernel/pid_max</c>, es 4194304.</summary>
    private const int PidMaximo = 4194304;

    private static readonly Regex Fecha = new(@"^(\d{1,19})\|(.+)$", RegexOptions.Compiled);

    private static readonly Regex RutaCitada = new(@"'([^']*)'", RegexOptions.Compiled);

    private static readonly Regex PidEnDetalle = new(@"\bpid\s+(\d{1,7})\b", RegexOptions.Compiled);

    /// <summary>Cuánto pasó desde un cambio, dicho como lo diría una persona; con más de un día, la fecha.</summary>
    public static string Hace(DateTimeOffset cuando) => Hace(cuando, DateTimeOffset.Now);

    public static string Hace(DateTimeOffset cuando, DateTimeOffset ahora)
    {
        var paso = ahora - cuando;

        return paso switch
        {
            { Ticks: < 0 } => "hace 0 s",
            { TotalSeconds: < 60 } => $"hace {(int)paso.TotalSeconds} s",
            { TotalMinutes: < 60 } => $"hace {(int)paso.TotalMinutes} min",
            { TotalHours: < 24 } => $"hace {(int)paso.TotalHours} h",
            _ => cuando.ToString("dd/MM HH:mm", CultureInfo.InvariantCulture),
        };
    }

    public static bool RutaAceptable(string? ruta)
    {
        if (ruta is null || ruta.Length is 0 or > 4096 || ruta[0] != '/')
        {
            return false;
        }

        foreach (var c in ruta)
        {
            if (char.IsControl(c))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary><c>-F</c> y no <c>-f</c>: sigue el nombre y no el descriptor, así la rotación no deja el visor congelado.</summary>
    public static string Comando(string ruta, int lineas = LineasIniciales) =>
        $"LC_ALL=C tail -n {Positivo(lineas)} -F -- {Citar(ruta)} 2>&1";

    /// <summary>Un solo canal para varios archivos: <c>tail</c> encabeza cada uno con su nombre.</summary>
    public static string Comando(IReadOnlyList<string> rutas, int lineas = LineasIniciales)
    {
        if (rutas.Count == 0)
        {
            throw new ArgumentException("No hay ningún archivo que seguir.", nameof(rutas));
        }

        var citadas = string.Join(' ', rutas.Select(Citar));

        return $"LC_ALL=C tail -n {Positivo(lineas)} -F -- {citadas} 2>&1";
    }

    public static string ComandoDeFechas(IReadOnlyList<string> rutas)
    {
        var aceptables = rutas.Where(RutaAceptable).Select(Citar).ToList();

        return aceptables.Count == 0
            ? string.Empty
            : $"LC_ALL=C stat -c '%Y|%n' -- {string.Join(' ', aceptables)} 2>&1";
    }

    public static IReadOnlyList<ArchivoSeguido> LeerFechas(
        IReadOnlyList<string> rutas, string? salida)
    {
        var fechas = new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal);
        var fallas = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var linea in Lineas(salida))
        {
            var fecha = Fecha.Match(linea);

            if (fecha.Success
                && long.TryParse(
                    fecha.Groups[1].Value,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var segundos))
            {
                fechas[fecha.Groups[2].Value] =
                    DateTimeOffset.FromUnixTimeSeconds(segundos).ToLocalTime();

                continue;
            }

            if (!linea.StartsWith("stat:", StringComparison.Ordinal))
            {
                continue;
            }

            if (RutaCitada.Match(linea) is { Success: true } citada)
            {
                fallas[citada.Groups[1].Value] = MotivoDelServidor(linea);
            }
        }

        return rutas.Select(ruta => fechas.TryGetValue(ruta, out var cambiado)
                ? new ArchivoSeguido(ruta, cambiado)
                : new ArchivoSeguido(
                    ruta,
                    null,
                    fallas.GetValueOrDefault(ruta, "el servidor no contestó por este archivo")))
            .ToList();
    }

    public static string ComandoDeRegistrosAbiertos(int pid)
    {
        if (pid is < 1 or > PidMaximo)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pid), pid, "El pid no es uno que pueda tener un proceso.");
        }

        return $"LC_ALL=C readlink -- /proc/{pid}/fd/1 /proc/{pid}/fd/2 2>/dev/null";
    }

    /// <summary>Las salidas del proceso que son archivos de verdad: descarta la consola, los conductos y lo ya borrado.</summary>
    public static IReadOnlyList<string> LeerRegistrosAbiertos(string? salida)
    {
        var rutas = new List<string>();

        foreach (var linea in Lineas(salida))
        {
            if (linea.EndsWith("(deleted)", StringComparison.Ordinal)
                || !RutaAceptable(linea)
                || linea.StartsWith("/dev/", StringComparison.Ordinal)
                || linea.StartsWith("/proc/", StringComparison.Ordinal)
                || linea.StartsWith("/sys/", StringComparison.Ordinal)
                || rutas.Contains(linea, StringComparer.Ordinal))
            {
                continue;
            }

            rutas.Add(linea);
        }

        return rutas;
    }

    public static int? PidDeSupervisor(string? detalle)
    {
        if (detalle is null)
        {
            return null;
        }

        var m = PidEnDetalle.Match(detalle);

        return m.Success
               && int.TryParse(
                   m.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var pid)
               && pid is >= 1 and <= PidMaximo
            ? pid
            : null;
    }

    /// <summary>Lo que dice <c>tail</c> cuando el archivo rota o deja de poder leerse; null si la línea es del registro (FR-185c).</summary>
    public static AvisoDeSeguimiento? Diagnostico(string? linea)
    {
        if (linea is null || !linea.StartsWith("tail:", StringComparison.Ordinal))
        {
            return null;
        }

        if (Dice(linea, "has appeared") || Dice(linea, "has been replaced"))
        {
            return new AvisoDeSeguimiento(
                ClaseDeAviso.Rotacion, "El archivo rotó; se sigue el nuevo.");
        }

        if (Dice(linea, "has become inaccessible"))
        {
            return new AvisoDeSeguimiento(
                ClaseDeAviso.Inaccesible,
                "El archivo dejó de poder leerse: se borró o se movió.");
        }

        if (Dice(linea, "Permission denied"))
        {
            return new AvisoDeSeguimiento(
                ClaseDeAviso.Inaccesible,
                "El archivo dejó de poder leerse: el permiso no alcanza.");
        }

        if (Dice(linea, "No such file"))
        {
            return new AvisoDeSeguimiento(
                ClaseDeAviso.Inaccesible, "El archivo del registro no existe en el servidor.");
        }

        if (Dice(linea, "cannot open"))
        {
            return new AvisoDeSeguimiento(
                ClaseDeAviso.Inaccesible, "No se pudo abrir el archivo del registro.");
        }

        return Dice(linea, "error reading")
            ? new AvisoDeSeguimiento(
                ClaseDeAviso.Inaccesible, "El servidor no pudo leer el archivo del registro.")
            : null;
    }

    private static string MotivoDelServidor(string linea) =>
        Dice(linea, "No such file") ? "no existe en el servidor"
        : Dice(linea, "Permission denied") ? "el permiso no alcanza"
        : "el servidor no pudo leerlo";

    private static bool Dice(string linea, string marca) =>
        linea.Contains(marca, StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<string> Lineas(string? salida) =>
        (salida ?? string.Empty)
        .ReplaceLineEndings("\n")
        .Split('\n')
        .Select(l => l.Trim())
        .Where(l => l.Length > 0);

    private static int Positivo(int lineas) => lineas is > 0 and <= 100000 ? lineas : LineasIniciales;

    /// <summary>Cita para el shell del servidor: la comilla simple se cierra, se escapa y se vuelve a abrir.</summary>
    public static string Citar(string ruta)
    {
        if (!RutaAceptable(ruta))
        {
            throw new ArgumentException(
                "La ruta del registro no es absoluta o trae caracteres que no se pueden enviar.",
                nameof(ruta));
        }

        var citada = new StringBuilder(ruta.Length + 8).Append('\'');

        foreach (var c in ruta)
        {
            if (c == '\'')
            {
                citada.Append("'\\''");
                continue;
            }

            citada.Append(c);
        }

        return citada.Append('\'').ToString();
    }
}
