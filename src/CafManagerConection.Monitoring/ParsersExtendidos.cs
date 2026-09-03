using System.Globalization;

namespace CafManagerConection.Monitoring;

/// <summary>Interpreta <c>ip -o addr show</c> e <c>ip -o link show</c> (FR-171).</summary>
public static class InterfacesParser
{
    public static IReadOnlyList<InterfaceInfo> Parse(string salidaLink, string salidaAddr)
    {
        var direcciones = LeerDirecciones(salidaAddr);
        var resultado = new List<InterfaceInfo>();

        foreach (var linea in Lineas(salidaLink))
        {
            var dosPuntos = linea.IndexOf(':');

            if (dosPuntos < 0)
            {
                continue;
            }

            var resto = linea[(dosPuntos + 1)..].TrimStart();
            var finNombre = resto.IndexOf(':');

            if (finNombre < 0)
            {
                continue;
            }

            var nombre = resto[..finNombre].Trim();

            // «veth1b11b10@if2»: lo de después de la arroba es el otro extremo del par; sin recortarlo ninguna dirección se aparea con su interfaz.
            var arroba = nombre.IndexOf('@');

            if (arroba > 0)
            {
                nombre = nombre[..arroba];
            }

            if (nombre.Length == 0)
            {
                continue;
            }

            var campos = resto[(finNombre + 1)..]
                .Split([' ', '\t', '\\'], StringSplitOptions.RemoveEmptyEntries);

            var banderas = campos.FirstOrDefault(c => c.StartsWith('<'))?.Trim('<', '>') ?? string.Empty;

            var (v4, v6) = direcciones.TryGetValue(nombre, out var suyas)
                ? suyas
                : ([], []);

            resultado.Add(new InterfaceInfo(
                nombre,

                Siguiente(campos, "link/ether"),
                int.TryParse(Siguiente(campos, "mtu"), out var mtu) ? mtu : 0,
                Siguiente(campos, "state") ?? "UNKNOWN",

                banderas.Contains("LOWER_UP", StringComparison.Ordinal),
                v4,
                v6,
                Siguiente(campos, "master")));
        }

        return resultado;
    }

    private static Dictionary<string, (List<string> V4, List<string> V6)> LeerDirecciones(
        string salidaAddr)
    {
        var mapa = new Dictionary<string, (List<string> V4, List<string> V6)>(StringComparer.Ordinal);

        foreach (var linea in Lineas(salidaAddr))
        {
            var campos = linea.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);

            if (campos.Length < 4)
            {
                continue;
            }

            var nombre = campos[1].Trim();
            var arroba = nombre.IndexOf('@');

            if (arroba > 0)
            {
                nombre = nombre[..arroba];
            }

            var familia = campos[2];
            var direccion = campos[3];

            if (!mapa.TryGetValue(nombre, out var suyas))
            {
                suyas = ([], []);
                mapa[nombre] = suyas;
            }

            if (familia.Equals("inet", StringComparison.Ordinal))
            {
                suyas.V4.Add(direccion);
            }
            else if (familia.Equals("inet6", StringComparison.Ordinal))
            {
                suyas.V6.Add(direccion);
            }
        }

        return mapa;
    }

    private static string? Siguiente(string[] campos, string clave)
    {
        var i = Array.IndexOf(campos, clave);

        return i >= 0 && i + 1 < campos.Length ? campos[i + 1] : null;
    }

    internal static IEnumerable<string> Lineas(string texto) =>
        texto.ReplaceLineEndings("\n").Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0);
}

/// <summary>Interpreta <c>ip route show</c> e <c>ip -6 route show</c> (FR-172).</summary>
public static class RoutesParser
{
    public static IReadOnlyList<RouteInfo> Parse(string salidaV4, string salidaV6)
    {
        var resultado = new List<RouteInfo>();

        resultado.AddRange(Leer(salidaV4, esIPv6: false));
        resultado.AddRange(Leer(salidaV6, esIPv6: true));

        return resultado;
    }

    // Sin saltear el tipo de ruta, «unreachable 2001:db8::/62» se informa con destino «unreachable», que no es una red.
    private static readonly string[] TiposDeRuta =
        ["unreachable", "blackhole", "prohibit", "throw", "local", "broadcast", "multicast", "anycast"];

    private static IEnumerable<RouteInfo> Leer(string salida, bool esIPv6)
    {
        foreach (var linea in InterfacesParser.Lineas(salida))
        {
            var campos = linea.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);

            if (campos.Length == 0)
            {
                continue;
            }

            var i = 0;

            if (TiposDeRuta.Contains(campos[0], StringComparer.Ordinal))
            {
                i = 1;

                if (campos.Length < 2)
                {
                    continue;
                }
            }

            var destino = campos[i];
            var dispositivo = Siguiente(campos, "dev");

            if (dispositivo is null)
            {
                continue;
            }

            yield return new RouteInfo(
                destino,
                Siguiente(campos, "via"),
                dispositivo,
                Siguiente(campos, "src"),
                int.TryParse(Siguiente(campos, "metric"), out var metrica) ? metrica : null,
                campos.Contains("linkdown", StringComparer.Ordinal),
                esIPv6);
        }
    }

    private static string? Siguiente(string[] campos, string clave)
    {
        var i = Array.IndexOf(campos, clave);

        return i >= 0 && i + 1 < campos.Length ? campos[i + 1] : null;
    }
}

// No se pide la línea de comando sino <c>comm</c>: los argumentos llevan contraseñas más seguido de lo que deberían (FR-165e).
public static class TopProcessesParser
{
    // uid y no user: un nombre de usuario con espacios corre las nueve columnas siguientes y la fila sale plausible y toda mal.
    public const string Formato = "pid,ppid,uid,pcpu,pmem,rss,etimes,stat,nlwp,comm";

    private const int Columnas = 10;

    public static IReadOnlyList<ProcessInfo> Parse(string salida)
    {
        var resultado = new List<ProcessInfo>();

        foreach (var linea in InterfacesParser.Lineas(salida).Skip(1))
        {
            var campos = linea.Split([' ', '\t'], Columnas, StringSplitOptions.RemoveEmptyEntries);

            if (campos.Length < Columnas)
            {
                continue;
            }

            if (!int.TryParse(campos[0], out var pid) || pid <= 0)
            {
                continue;
            }

            resultado.Add(new ProcessInfo(
                pid,
                int.TryParse(campos[1], out var padre) ? padre : 0,

                campos[2],
                Numero(campos[3]),
                Numero(campos[4]),
                long.TryParse(campos[5], out var rss) ? rss * 1024 : 0,
                TimeSpan.FromSeconds(long.TryParse(campos[6], out var seg) ? seg : 0),
                campos[7],
                int.TryParse(campos[8], out var hilos) ? hilos : 1,

                campos[Columnas - 1].Trim()));
        }

        return resultado;
    }

    public static IReadOnlyList<ProcessInfo> ConNombres(
        IReadOnlyList<ProcessInfo> procesos, IReadOnlyDictionary<string, string> usuarios)
    {
        if (usuarios.Count == 0)
        {
            return procesos;
        }

        return procesos
            .Select(p => usuarios.TryGetValue(p.User, out var nombre)
                ? p with { User = nombre }
                : p)
            .ToList();
    }

    // InvariantCulture siempre: ps escribe el punto decimal según el locale del servidor y 3.41 se leía como 341 %.
    private static double Numero(string texto)
    {
        var normalizado = texto.Replace(',', '.');

        return double.TryParse(
            normalizado, NumberStyles.Float, CultureInfo.InvariantCulture, out var valor)
            ? valor
            : 0;
    }
}

/// <summary>Interpreta <c>/proc/pressure/*</c> (FR-174).</summary>
public static class PressureParser
{
    // /proc/pressure no existe antes del núcleo 4.20 ni con CONFIG_PSI apagado: ahí el tramo llega vacío y hay que decir que no está, no informar cero.
    public static PressureSet Parse(string cpu, string io, string memoria) =>
        new(Una(cpu), Una(io), Una(memoria));

    public static PressureMetrics? Una(string contenido)
    {
        if (!contenido.Contains("avg10", StringComparison.Ordinal))
        {
            return null;
        }

        double some = 0;
        double full = 0;

        foreach (var linea in InterfacesParser.Lineas(contenido))
        {
            var campos = linea.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);

            if (campos.Length < 2)
            {
                continue;
            }

            var avg10 = campos.FirstOrDefault(c => c.StartsWith("avg10=", StringComparison.Ordinal));

            if (avg10 is null
                || !double.TryParse(
                    avg10["avg10=".Length..],
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var valor))
            {
                continue;
            }

            if (campos[0].Equals("some", StringComparison.Ordinal))
            {
                some = valor;
            }
            else if (campos[0].Equals("full", StringComparison.Ordinal))
            {
                full = valor;
            }
        }

        return new PressureMetrics(some, full);
    }
}

public static class DatosDeSistemaParser
{
    // En aarch64 no existe la línea «model name» de /proc/cpuinfo y hay que caer a «Model name» de lscpu o al par implementador/parte.
    public static string? ModeloDeCpu(string cpuinfo, string lscpu = "")
    {
        foreach (var clave in new[] { "model name", "Model name", "cpu model" })
        {
            if (Valor(cpuinfo, clave) is { Length: > 0 } valor)
            {
                return valor;
            }
        }

        if (Valor(lscpu, "Model name") is { Length: > 0 } deLscpu)
        {
            return deLscpu;
        }

        var implementador = Valor(cpuinfo, "CPU implementer");
        var parte = Valor(cpuinfo, "CPU part");

        if (implementador is null || parte is null)
        {
            return null;
        }

        var fabricante = implementador.Equals("0x41", StringComparison.OrdinalIgnoreCase)
            ? "ARM"
            : implementador;

        return $"{fabricante} {parte}";
    }

    /// <summary>Nombre de usuario por UID, de <c>/etc/passwd</c> y no de <c>getent</c>, que consultaría LDAP en cada muestra.</summary>
    public static IReadOnlyDictionary<string, string> UsuariosPorUid(string salida)
    {
        var mapa = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var linea in InterfacesParser.Lineas(salida))
        {
            var campos = linea.Split(':', StringSplitOptions.TrimEntries);

            if (campos.Length < 2 || campos[0].Length == 0 || campos[1].Length == 0)
            {
                continue;
            }

            mapa.TryAdd(campos[1], campos[0]);
        }

        return mapa;
    }

    /// <summary>Servidores DNS y dominio de búsqueda, de <c>resolv.conf</c>.</summary>
    public static (IReadOnlyList<string> Servidores, string? Busqueda) Dns(string resolvConf)
    {
        var servidores = new List<string>();
        string? busqueda = null;

        foreach (var linea in InterfacesParser.Lineas(resolvConf))
        {
            if (linea.StartsWith('#') || linea.StartsWith(';'))
            {
                continue;
            }

            var campos = linea.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);

            if (campos.Length < 2)
            {
                continue;
            }

            if (campos[0].Equals("nameserver", StringComparison.OrdinalIgnoreCase))
            {
                servidores.Add(campos[1]);
            }
            else if (campos[0].Equals("search", StringComparison.OrdinalIgnoreCase))
            {
                busqueda = string.Join(' ', campos.Skip(1));
            }
        }

        return (servidores, busqueda);
    }

    /// <summary>Memoria de intercambio, de <c>/proc/meminfo</c>.</summary>
    public static SwapMetrics Swap(string meminfo)
    {
        var total = Kilobytes(meminfo, "SwapTotal");
        var libre = Kilobytes(meminfo, "SwapFree");

        return new SwapMetrics(total, Math.Max(0, total - libre));
    }

    /// <summary>Temperaturas de <c>sensors -u</c>: sólo las que terminan en <c>_input</c>, porque <c>_max</c> y <c>_crit</c> son configuración.</summary>
    public static IReadOnlyList<TemperatureInfo> Temperaturas(string salida)
    {
        var resultado = new List<TemperatureInfo>();
        var etiqueta = string.Empty;

        foreach (var cruda in salida.ReplaceLineEndings("\n").Split('\n'))
        {
            var linea = cruda.TrimEnd();

            if (linea.Length == 0)
            {
                continue;
            }

            if (!char.IsWhiteSpace(cruda[0]) && linea.EndsWith(':'))
            {
                etiqueta = linea[..^1].Trim();
                continue;
            }

            var partes = linea.Split(':', 2);

            if (partes.Length < 2)
            {
                continue;
            }

            var clave = partes[0].Trim();

            if (!clave.EndsWith("_input", StringComparison.Ordinal)
                || !clave.StartsWith("temp", StringComparison.Ordinal))
            {
                continue;
            }

            if (double.TryParse(
                    partes[1].Trim(),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var grados))
            {
                resultado.Add(new TemperatureInfo(
                    etiqueta.Length > 0 ? etiqueta : clave, grados));
            }
        }

        return resultado;
    }

    private static long Kilobytes(string meminfo, string clave)
    {
        if (Valor(meminfo, clave) is not { } valor)
        {
            return 0;
        }

        var numero = valor.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();

        return long.TryParse(numero, out var kb) ? kb * 1024 : 0;
    }

    private static string? Valor(string texto, string clave)
    {
        foreach (var linea in InterfacesParser.Lineas(texto))
        {
            var dosPuntos = linea.IndexOf(':');

            if (dosPuntos < 0)
            {
                continue;
            }

            if (linea[..dosPuntos].Trim().Equals(clave, StringComparison.OrdinalIgnoreCase))
            {
                return linea[(dosPuntos + 1)..].Trim();
            }
        }

        return null;
    }
}

/// <summary>Interpreta <c>/proc/diskstats</c> y calcula velocidades por diferencia.</summary>
public static class DiskIoParser
{
    private const int BytesPorSector = 512;

    public static IReadOnlyList<DiskIoSample> Parse(string diskstats)
    {
        var resultado = new List<DiskIoSample>();

        foreach (var linea in InterfacesParser.Lineas(diskstats))
        {
            var campos = linea.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);

            // major, minor, nombre y 11 contadores como mínimo.
            if (campos.Length < 14)
            {
                continue;
            }

            var nombre = campos[2];

            if (EsPrestado(nombre))
            {
                continue;
            }

            resultado.Add(new DiskIoSample(
                nombre,
                long.TryParse(campos[5], out var leidos) ? leidos : 0,
                long.TryParse(campos[9], out var escritos) ? escritos : 0,
                long.TryParse(campos[12], out var ocupado) ? ocupado : 0));
        }

        return resultado;
    }

    // Los loop son paquetes snap montados: en un Ubuntu con snaps son ocho dispositivos que entierran los dos que interesan.
    private static bool EsPrestado(string nombre) =>
        nombre.StartsWith("loop", StringComparison.Ordinal)
        || nombre.StartsWith("ram", StringComparison.Ordinal)
        || nombre.StartsWith("sr", StringComparison.Ordinal)
        || nombre.StartsWith("fd", StringComparison.Ordinal);

    // Sólo dispositivos enteros: sda y sda4 cuentan la misma entrada y salida, y mostrar las dos duplica el total.
    public static IReadOnlyList<DiskIoMetrics> Between(
        IReadOnlyList<DiskIoSample> anterior,
        IReadOnlyList<DiskIoSample> actual,
        double segundos,
        IReadOnlySet<string>? discosEnteros = null)
    {
        if (segundos <= 0)
        {
            return [];
        }

        var previos = anterior.ToDictionary(m => m.Device, StringComparer.Ordinal);
        var nombres = actual.Select(a => a.Device).ToHashSet(StringComparer.Ordinal);
        var resultado = new List<DiskIoMetrics>();

        foreach (var ahora in actual)
        {
            var esDisco = discosEnteros is { Count: > 0 }
                ? discosEnteros.Contains(ahora.Device)
                : !EsParticionDe(ahora.Device, nombres);

            if (!esDisco || !previos.TryGetValue(ahora.Device, out var antes))
            {
                continue;
            }

            var leidos = Math.Max(0, ahora.SectorsRead - antes.SectorsRead);
            var escritos = Math.Max(0, ahora.SectorsWritten - antes.SectorsWritten);
            var ocupado = Math.Max(0, ahora.MillisecondsBusy - antes.MillisecondsBusy);

            if (leidos == 0 && escritos == 0 && ocupado == 0)
            {
                continue;
            }

            resultado.Add(new DiskIoMetrics(
                ahora.Device,
                leidos * BytesPorSector / segundos,
                escritos * BytesPorSector / segundos,

                Math.Min(100, ocupado / (segundos * 10))));
        }

        return resultado
            .OrderByDescending(d => d.ReadBytesPerSecond + d.WriteBytesPerSecond)
            .ToList();
    }

    // dm-0 (LVM) y md0 (RAID) cuentan la misma entrada y salida que el disco físico de abajo: adivinando por el nombre el total salía duplicado.
    public static IReadOnlySet<string> DiscosEnteros(string salidaLsblk)
    {
        var discos = new HashSet<string>(StringComparer.Ordinal);

        foreach (var linea in InterfacesParser.Lineas(salidaLsblk))
        {
            var campos = linea.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);

            if (campos.Length >= 2 && campos[1].Equals("disk", StringComparison.OrdinalIgnoreCase))
            {
                discos.Add(campos[0]);
            }
        }

        return discos;
    }

    // Corta una sola vez por el primer dígito desde la derecha: recorriendo todos los sufijos, dm-10 daba partición de dm-1 y md127 de md1.
    public static bool EsParticionDe(string nombre, IReadOnlySet<string> todos)
    {
        var largo = nombre.Length;

        while (largo > 0 && char.IsDigit(nombre[largo - 1]))
        {
            largo--;
        }

        if (largo == 0 || largo == nombre.Length)
        {
            return false;
        }

        var raiz = nombre[..largo];

        // NVMe: «nvme0n1p3» es partición de «nvme0n1».
        return todos.Contains(raiz) || (raiz.EndsWith('p') && todos.Contains(raiz[..^1]));
    }
}
