using System.Globalization;

namespace CafManagerConection.Monitoring;

/// <summary>Interpreta <c>/proc/stat</c>. El uso de CPU se calcula por diferencia entre dos lecturas (FR-081).</summary>
public static class CpuStatParser
{
    public static CpuSample Parse(string procStat)
    {
        long total = 0;
        long idle = 0;
        var cores = 0;

        foreach (var linea in procStat.ReplaceLineEndings("\n").Split('\n'))
        {
            if (!linea.StartsWith("cpu", StringComparison.Ordinal))
            {
                continue;
            }

            var partes = linea.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (partes[0] == "cpu")
            {
                var valores = partes.Skip(1)
                    .Select(p => long.TryParse(p, out var v) ? v : 0)
                    .ToArray();

                total = valores.Sum();

                idle = valores.Length > 4 ? valores[3] + valores[4] : 0;
            }
            else if (partes[0].Length > 3 && char.IsDigit(partes[0][3]))
            {
                cores++;
            }
        }

        return new CpuSample(total, idle, Math.Max(1, cores));
    }

    public static CpuMetrics Between(CpuSample anterior, CpuSample actual)
    {
        var totalDiff = actual.Total - anterior.Total;
        var idleDiff = actual.Idle - anterior.Idle;

        if (totalDiff <= 0)
        {
            return new CpuMetrics(0, actual.CoreCount);
        }

        var usado = (totalDiff - idleDiff) * 100.0 / totalDiff;

        return new CpuMetrics(Math.Clamp(usado, 0, 100), actual.CoreCount);
    }
}

/// <summary>Interpreta <c>/proc/meminfo</c>. La usada es <c>MemTotal - MemAvailable</c>, no <c>MemTotal - MemFree</c> (FR-081).</summary>
public static class MemoryInfoParser
{
    public static MemoryMetrics Parse(string procMeminfo)
    {
        var valores = new Dictionary<string, long>(StringComparer.Ordinal);

        foreach (var linea in procMeminfo.ReplaceLineEndings("\n").Split('\n'))
        {
            var corte = linea.IndexOf(':', StringComparison.Ordinal);
            if (corte <= 0)
            {
                continue;
            }

            var clave = linea[..corte].Trim();
            var resto = linea[(corte + 1)..].Trim();
            var numero = resto.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();

            if (long.TryParse(numero, out var kb))
            {
                valores[clave] = kb * 1024;
            }
        }

        var total = valores.GetValueOrDefault("MemTotal");

        // MemAvailable existe desde Linux 3.14; en kernels anteriores se aproxima.
        var disponible = valores.TryGetValue("MemAvailable", out var ma)
            ? ma
            : valores.GetValueOrDefault("MemFree") +
              valores.GetValueOrDefault("Buffers") +
              valores.GetValueOrDefault("Cached");

        return new MemoryMetrics(total, Math.Max(0, total - disponible), disponible);
    }
}

/// <summary>Interpreta <c>/proc/loadavg</c> y <c>/proc/uptime</c>.</summary>
public static class LoadAverageParser
{
    public static LoadMetrics ParseLoad(string procLoadavg)
    {
        var partes = procLoadavg.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        double N(int i) =>
            partes.Length > i && double.TryParse(
                partes[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
                ? v
                : 0;

        return new LoadMetrics(N(0), N(1), N(2));
    }

    public static TimeSpan ParseUptime(string procUptime)
    {
        var primero = procUptime.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();

        return double.TryParse(
            primero, NumberStyles.Float, CultureInfo.InvariantCulture, out var segundos)
            ? TimeSpan.FromSeconds(segundos)
            : TimeSpan.Zero;
    }

    /// <summary>Cantidad de procesos, a partir del conteo de directorios numéricos de <c>/proc</c>.</summary>
    public static int ParseProcessCount(string salida) =>
        int.TryParse(salida.Trim(), out var n) ? n : 0;
}

/// <summary>Interpreta <c>/proc/net/dev</c>. La velocidad se calcula por diferencia entre dos lecturas (FR-082).</summary>
public static class NetworkStatsParser
{
    // tun y tap no están acá: detrás de una VPN, tun0 es la interfaz por la que pasa todo el tráfico, e InterfaceInfo.EsDeContenedor ya la trata como real.
    private static readonly string[] PrefijosVirtuales =
        ["lo", "veth", "docker", "br-", "virbr"];

    public static IReadOnlyList<NetworkSample> Parse(string procNetDev)
    {
        var resultado = new List<NetworkSample>();

        foreach (var linea in procNetDev.ReplaceLineEndings("\n").Split('\n').Skip(2))
        {
            var corte = linea.IndexOf(':', StringComparison.Ordinal);
            if (corte <= 0)
            {
                continue;
            }

            var nombre = linea[..corte].Trim();
            var campos = linea[(corte + 1)..]
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (campos.Length < 9)
            {
                continue;
            }

            var entrada = long.TryParse(campos[0], out var e) ? e : 0;
            var salida = long.TryParse(campos[8], out var s) ? s : 0;

            resultado.Add(new NetworkSample(nombre, entrada, salida));
        }

        return resultado;
    }

    public static bool EsVirtual(string nombre) =>
        PrefijosVirtuales.Any(p => nombre.StartsWith(p, StringComparison.OrdinalIgnoreCase));

    public static IReadOnlyList<NetworkMetrics> Between(
        IReadOnlyList<NetworkSample> anterior,
        IReadOnlyList<NetworkSample> actual,
        double segundos,
        bool incluirVirtuales = false)
    {
        if (segundos <= 0)
        {
            return [];
        }

        var previos = anterior.ToDictionary(s => s.Interface, StringComparer.Ordinal);
        var resultado = new List<NetworkMetrics>();

        foreach (var s in actual)
        {
            if (!incluirVirtuales && EsVirtual(s.Interface))
            {
                continue;
            }

            if (!previos.TryGetValue(s.Interface, out var antes))
            {
                continue;
            }

            var entrada = Math.Max(0, s.BytesIn - antes.BytesIn) / segundos;
            var salida = Math.Max(0, s.BytesOut - antes.BytesOut) / segundos;

            if (!incluirVirtuales && entrada == 0 && salida == 0 && s.BytesIn == 0)
            {
                continue;
            }

            resultado.Add(new NetworkMetrics(s.Interface, entrada, salida));
        }

        return resultado;
    }
}

/// <summary>Interpreta la salida de <c>df -P -B1</c>.</summary>
public static class DiskUsageParser
{
    private static readonly string[] Virtuales =
    [
        "tmpfs", "devtmpfs", "overlay", "squashfs", "proc", "sysfs", "cgroup", "cgroup2",
        "devpts", "mqueue", "hugetlbfs", "debugfs", "tracefs", "fusectl", "configfs",
        "ramfs", "efivarfs", "binfmt_misc", "nsfs", "none", "udev", "shm",
    ];

    // La columna de tipo se detecta fila por fila y por el dato: con locale en español el encabezado dice «Tipo» y desaparecían todos los discos sin error.
    public static IReadOnlyList<DiskMetrics> Parse(string salidaDf)
    {
        var resultado = new List<DiskMetrics>();
        var vistos = new HashSet<string>(StringComparer.Ordinal);

        foreach (var linea in salidaDf.ReplaceLineEndings("\n").Split('\n'))
        {
            var campos = linea.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (campos.Length < 6)
            {
                continue;
            }

            var conTipo = !long.TryParse(campos[1], out _) && long.TryParse(campos[2], out _);

            if (!conTipo && !long.TryParse(campos[1], out _))
            {
                continue;
            }

            var columnaTotal = conTipo ? 2 : 1;
            var columnaPunto = conTipo ? 6 : 5;

            if (campos.Length < columnaPunto + 1)
            {
                continue;
            }

            var dispositivo = campos[0];

            if (EsVirtual(dispositivo))
            {
                continue;
            }

            // df des-escapa los espacios de /proc/mounts: tomar sólo el primer campo informaba «/mnt/disco» donde el montaje es «/mnt/disco viejo».
            var punto = string.Join(' ', campos.Skip(columnaPunto));

            // La barra al final es necesaria: sin ella «/snap» excluía también «/snapshots» y «/run» excluía «/runtime».
            if (EsInterno(punto))
            {
                continue;
            }

            if (!vistos.Add(dispositivo))
            {
                continue;
            }

            var total = long.TryParse(campos[columnaTotal], out var t) ? t : 0;
            var usado = long.TryParse(campos[columnaTotal + 1], out var u) ? u : 0;
            var libre = long.TryParse(campos[columnaTotal + 2], out var l) ? l : 0;

            if (total <= 0)
            {
                continue;
            }

            resultado.Add(new DiskMetrics(
                punto, dispositivo, total, usado, libre, conTipo ? campos[1] : null));
        }

        return resultado;
    }

    private static bool EsInterno(string punto)
    {
        string[] internos = ["/var/lib/docker", "/snap", "/run"];

        return internos.Any(i =>
            punto.Equals(i, StringComparison.Ordinal)
            || punto.StartsWith(i + "/", StringComparison.Ordinal));
    }

    public static bool EsVirtual(string dispositivo) =>
        Virtuales.Any(v => dispositivo.Equals(v, StringComparison.OrdinalIgnoreCase)) ||
        dispositivo.StartsWith("tmpfs", StringComparison.OrdinalIgnoreCase);
}

public static class SystemInfoParser
{
    public static string? ParseDistribution(string osRelease)
    {
        foreach (var linea in osRelease.ReplaceLineEndings("\n").Split('\n'))
        {
            if (linea.StartsWith("PRETTY_NAME=", StringComparison.Ordinal))
            {
                return linea["PRETTY_NAME=".Length..].Trim().Trim('"');
            }
        }

        return null;
    }

    public static int ParseConnectedUsers(string salidaWho) =>
        salidaWho.ReplaceLineEndings("\n").Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Count(l => !string.IsNullOrWhiteSpace(l));

    public static IReadOnlyList<string> ParseFailedServices(string salida)
    {
        var resultado = new List<string>();

        foreach (var linea in salida.ReplaceLineEndings("\n").Split('\n'))
        {
            var limpia = linea.Replace("●", string.Empty).Trim();

            if (limpia.Length == 0 ||
                limpia.StartsWith("UNIT", StringComparison.Ordinal) ||
                limpia.StartsWith("LOAD", StringComparison.Ordinal) ||
                limpia.StartsWith("ACTIVE", StringComparison.Ordinal) ||
                limpia.StartsWith("SUB", StringComparison.Ordinal) ||
                limpia.Contains("loaded units listed", StringComparison.Ordinal) ||
                limpia.StartsWith("To show", StringComparison.Ordinal))
            {
                continue;
            }

            var nombre = limpia.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();

            if (!string.IsNullOrEmpty(nombre) && nombre.Contains('.', StringComparison.Ordinal))
            {
                resultado.Add(nombre);
            }
        }

        return resultado;
    }
}
