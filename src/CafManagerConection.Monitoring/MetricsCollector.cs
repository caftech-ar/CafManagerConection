using System.Diagnostics;
using CafManagerConection.UseCases.Abstractions;

namespace CafManagerConection.Monitoring;

public interface IRemoteCommandRunner
{
    Task<(bool Success, string Output, string Error)> RunAsync(
        string command, int timeoutSeconds, CancellationToken ct = default);
}

public sealed class MetricsCollector
{
    private const string Marca = "###CMC###";

    // La marca va entre comillas simples: un # al principio de una palabra abre un comentario de shell y el servidor devolvía un solo tramo con estado 0.
    private const string MarcaCitada = "'" + Marca + "'";

    private static readonly string Comando = string.Join(
        $"; echo {MarcaCitada}; ",
        [
            "cat /proc/stat",
            "cat /proc/meminfo",
            "cat /proc/loadavg",
            "cat /proc/uptime",
            "cat /proc/net/dev",

            // LC_ALL=C es obligatorio: df traduce su encabezado y contra un servidor con locale no inglés desaparecían todos los discos del panel, sin error.
            "LC_ALL=C df -PT -B1 2>/dev/null",
            "hostname",
            "cat /etc/os-release 2>/dev/null",
            "uname -r",
            "date -Iseconds",
            "who 2>/dev/null",
            "ls -d /proc/[0-9]* 2>/dev/null | wc -l",
            "systemctl --failed --no-legend --plain 2>/dev/null",

            "cat /proc/diskstats 2>/dev/null",
            "ip -o link show 2>/dev/null",
            "ip -o addr show 2>/dev/null",
            "ip route show 2>/dev/null",
            "ip -6 route show 2>/dev/null",
            "cat /etc/resolv.conf 2>/dev/null",

            "cat /proc/pressure/cpu 2>/dev/null",
            "cat /proc/pressure/io 2>/dev/null",
            "cat /proc/pressure/memory 2>/dev/null",

            "grep -E '^(model name|Model name|CPU implementer|CPU part)' /proc/cpuinfo 2>/dev/null "
                + "| head -4",
            "LC_ALL=C lscpu 2>/dev/null | grep -E '^Model name' | head -1",
            "(command -v sensors >/dev/null 2>&1 && LC_ALL=C sensors -u 2>/dev/null | head -60) "
                + "|| true",

            // lsblk va último para no correr los índices de los tramos anteriores; sin él, dm-0 y md0 contaban la E/S del disco físico de abajo y el total salía al doble.
            "lsblk -rno NAME,TYPE 2>/dev/null",
        ]) + "; exit 0";

    public static string ComandoDeLectura => Comando;

    /// <summary>Cuántos tramos separados por marca tiene <see cref="Comando"/>.</summary>
    public const int Tramos = 26;

    private readonly IRemoteCommandRunner _runner;
    private readonly TimeProvider _time;
    private readonly IAppLogger? _logger;
    private readonly Guid _conexion;

    public string? UltimoError { get; private set; }

    private CpuSample? _cpuAnterior;
    private IReadOnlyList<NetworkSample> _redAnterior = [];
    private IReadOnlyList<DiskIoSample> _discoAnterior = [];
    private DateTimeOffset? _instanteAnterior;

    public MetricsCollector(
        IRemoteCommandRunner runner,
        TimeProvider? time = null,
        IAppLogger? logger = null,
        Guid connectionId = default)
    {
        _runner = runner;
        _time = time ?? TimeProvider.System;
        _logger = logger;
        _conexion = connectionId;
    }

    /// <summary>Interfaces que el usuario eligió ver. Vacío significa "las que tengan tráfico".</summary>
    public IReadOnlyCollection<string> InterfacesVisibles { get; set; } = [];

    private readonly HashSet<string> _interfacesConocidas = new(StringComparer.Ordinal);

    public IReadOnlyCollection<string> InterfacesConocidas
    {
        get
        {
            lock (_interfacesConocidas)
            {
                return [.. _interfacesConocidas];
            }
        }
    }

    public async Task<ServerSnapshot?> CollectAsync(int timeoutSeconds, CancellationToken ct = default)
    {
        var reloj = _logger is null ? null : Stopwatch.StartNew();

        var (ok, salida, error) = await _runner
            .RunAsync(Comando, timeoutSeconds, ct).ConfigureAwait(false);

        if (reloj is not null)
        {
            _logger!.WorkCompleted(_conexion, RemoteWork.Metrics, reloj.Elapsed);
        }

        if (!ok || string.IsNullOrWhiteSpace(salida))
        {
            UltimoError = ok
                ? "El servidor no devolvió ninguna lectura. Puede no ser Linux, o no exponer /proc."
                : error?.Trim() is { Length: > 0 } motivo
                    ? motivo
                    : "La consulta de estado falló y el canal no dijo por qué.";

            return null;
        }

        UltimoError = null;

        var partes = salida.Split(Marca);

        string P(int i) => i < partes.Length ? partes[i].Trim() : string.Empty;

        var ahora = _time.GetUtcNow();

        var cpuActual = CpuStatParser.Parse(P(0));
        var redActual = NetworkStatsParser.Parse(P(4));

        lock (_interfacesConocidas)
        {
            foreach (var i in redActual.Where(x => !NetworkStatsParser.EsVirtual(x.Interface)))
            {
                _interfacesConocidas.Add(i.Interface);
            }
        }

        var segundos = _instanteAnterior is { } antes
            ? (ahora - antes).TotalSeconds
            : 0;

        var cpu = _cpuAnterior is { } previa
            ? CpuStatParser.Between(previa, cpuActual)
            : new CpuMetrics(0, cpuActual.CoreCount);

        var red = segundos > 0
            ? NetworkStatsParser.Between(_redAnterior, redActual, segundos)
            : [];

        if (InterfacesVisibles.Count > 0)
        {
            red = red.Where(r => InterfacesVisibles.Contains(r.Interface)).ToList();
        }

        var discoActual = DiskIoParser.Parse(P(13));

        var discoIo = segundos > 0
            ? DiskIoParser.Between(
                _discoAnterior, discoActual, segundos, DiskIoParser.DiscosEnteros(P(25)))
            : [];

        _cpuAnterior = cpuActual;
        _redAnterior = redActual;
        _discoAnterior = discoActual;
        _instanteAnterior = ahora;

        var (dns, busqueda) = DatosDeSistemaParser.Dns(P(18));

        return new ServerSnapshot(
            ahora,
            cpu,
            MemoryInfoParser.Parse(P(1)),
            LoadAverageParser.ParseLoad(P(2)),
            LoadAverageParser.ParseUptime(P(3)),
            DiskUsageParser.Parse(P(5)),
            red,
            new SystemInfo(
                P(6),
                SystemInfoParser.ParseDistribution(P(7)),
                P(8),
                DateTimeOffset.TryParse(P(9), out var fecha) ? fecha : null,
                SystemInfoParser.ParseConnectedUsers(P(10)),
                LoadAverageParser.ParseProcessCount(P(11)),
                SystemInfoParser.ParseFailedServices(P(12)),
                DatosDeSistemaParser.ModeloDeCpu(P(22), P(23)),
                dns,
                busqueda),
            DatosDeSistemaParser.Swap(P(1)),
            PressureParser.Parse(P(19), P(20), P(21)),
            discoIo,
            InterfacesParser.Parse(P(14), P(15)),
            RoutesParser.Parse(P(16), P(17)),
            DatosDeSistemaParser.Temperaturas(P(24)));
    }

    public static async Task<bool> IsLinuxAsync(
        IRemoteCommandRunner runner, CancellationToken ct = default)
    {
        var (ok, salida, _) = await runner
            .RunAsync("test -r /proc/stat && echo si", 5, ct).ConfigureAwait(false);

        return ok && salida.Contains("si", StringComparison.Ordinal);
    }
}

/// <summary>Historial corto en memoria para los minigráficos: 60 puntos a 5 segundos son 5 minutos, y nunca se persiste (FR-085).</summary>
public sealed class SnapshotHistory
{
    public const int MaxPoints = 60;

    private readonly Queue<ServerSnapshot> _puntos = new();

    public IReadOnlyCollection<ServerSnapshot> Points => _puntos;

    public ServerSnapshot? Latest => _puntos.Count > 0 ? _puntos.Last() : null;

    public void Add(ServerSnapshot snapshot)
    {
        _puntos.Enqueue(snapshot);

        while (_puntos.Count > MaxPoints)
        {
            _puntos.Dequeue();
        }
    }

    public IReadOnlyList<double> CpuSeries() =>
        _puntos.Select(p => p.Cpu.UsedPercent).ToList();

    public IReadOnlyList<double> MemorySeries() =>
        _puntos.Select(p => p.Memory.UsedPercent).ToList();

    public IReadOnlyList<double> NetworkSeries() =>
        _puntos.Select(p => p.Interfaces.Sum(i => i.BytesInPerSecond + i.BytesOutPerSecond))
            .ToList();

    public void Clear() => _puntos.Clear();
}
