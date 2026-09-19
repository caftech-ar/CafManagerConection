using CafManagerConection.UseCases.Abstractions;

namespace CafManagerConection.Monitoring;

/// <summary>Lo que muestra la franja de métricas de una sesión.</summary>
public sealed record MuestraDeFranja(
    CpuMetrics? Cpu,
    MemoryMetrics Memoria,
    LoadMetrics Carga,
    TimeSpan Actividad,
    DiskMetrics? PeorDisco,
    IReadOnlyList<DiskMetrics> Discos,
    NetworkMetrics? Red,
    string? Distribucion);

/// <summary>Lectura acotada a lo que entra en la franja: cinco archivos de <c>/proc</c> por vuelta.</summary>
public sealed class ColectorDeFranja
{
    private const string Marca = "###CMC###";

    private const string MarcaCitada = "'" + Marca + "'";

    /// <summary>Cuántas vueltas pasan entre lecturas de disco: el espacio libre no cambia en cinco segundos y <c>df</c> es el único caro.</summary>
    public const int VueltasPorDisco = 12;

    private static readonly string ComandoDeVuelta = string.Join(
        $"; echo {MarcaCitada}; ",
        [
            "cat /proc/stat",
            "cat /proc/meminfo",
            "cat /proc/loadavg",
            "cat /proc/uptime",
            "cat /proc/net/dev",
        ]) + "; exit 0";

    // LC_ALL=C es obligatorio: df traduce su encabezado y contra un servidor con locale no inglés desaparecen todos los discos.
    private static readonly string ComandoConDisco =
        ComandoDeVuelta[..^"; exit 0".Length]
        + $"; echo {MarcaCitada}; LC_ALL=C df -PT -B1 2>/dev/null; exit 0";

    /// <summary>Lo que no cambia mientras dura la sesión, así que se lee una sola vez.</summary>
    public const string ComandoDeIdentidad = "cat /etc/os-release 2>/dev/null";

    private readonly IEjecutorRemoto _runner;
    private readonly TimeProvider _time;
    private readonly IAppLogger? _logger;

    private CpuSample? _cpuAnterior;
    private IReadOnlyList<NetworkSample> _redAnterior = [];
    private DateTimeOffset? _instanteAnterior;
    private IReadOnlyList<DiskMetrics> _discos = [];
    private int _vuelta;

    public ColectorDeFranja(
        IEjecutorRemoto runner, TimeProvider? time = null, IAppLogger? logger = null)
    {
        _runner = runner;
        _time = time ?? TimeProvider.System;
        _logger = logger;
    }

    /// <summary>Distribución del servidor, abreviada; <c>null</c> mientras no se haya leído.</summary>
    public string? Distribucion { get; private set; }

    /// <summary>La última muestra buena, que se conserva cuando una lectura falla.</summary>
    public MuestraDeFranja? Ultima { get; private set; }

    /// <summary>Lee la distribución del servidor. Se llama una sola vez, al conectar.</summary>
    /// <param name="timeoutSeconds">Cuánto esperar la respuesta.</param>
    /// <param name="ct">Para cortar la lectura desde afuera.</param>
    public async Task LeerIdentidadAsync(int timeoutSeconds, CancellationToken ct = default)
    {
        try
        {
            var (ok, salida, _) = await _runner
                .RunAsync(ComandoDeIdentidad, timeoutSeconds, ct).ConfigureAwait(false);

            if (ok && SystemInfoParser.ParseDistribution(salida) is { Length: > 0 } completa)
            {
                Distribucion = NombreDeDistribucion.Abreviar(completa);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger?.TechnicalError("leer la distribución del servidor", ex);
        }
    }

    /// <summary>Toma una muestra. Devuelve <c>null</c> si la lectura falló, conservando la anterior.</summary>
    /// <param name="timeoutSeconds">Cuánto esperar la respuesta.</param>
    /// <param name="ct">Para cortar la lectura desde afuera.</param>
    public async Task<MuestraDeFranja?> MuestrearAsync(
        int timeoutSeconds, CancellationToken ct = default)
    {
        var conDisco = _vuelta % VueltasPorDisco == 0;
        _vuelta++;

        var (ok, salida, _) = await _runner
            .RunAsync(conDisco ? ComandoConDisco : ComandoDeVuelta, timeoutSeconds, ct)
            .ConfigureAwait(false);

        if (!ok || string.IsNullOrWhiteSpace(salida))
        {
            return null;
        }

        var partes = salida.Split(Marca);

        string P(int i) => i < partes.Length ? partes[i].Trim() : string.Empty;

        var ahora = _time.GetUtcNow();
        var cpuActual = CpuStatParser.Parse(P(0));
        var redActual = NetworkStatsParser.Parse(P(4));

        var transcurrido = _instanteAnterior is { } antes
            ? (ahora - antes).TotalSeconds
            : 0;

        var cpu = _cpuAnterior is { } previa
            ? CpuStatParser.Between(previa, cpuActual)
            : (CpuMetrics?)null;

        var red = _redAnterior.Count > 0 && transcurrido > 0
            ? NetworkStatsParser.Between(_redAnterior, redActual, transcurrido)
                .OrderByDescending(r => r.BytesInPerSecond + r.BytesOutPerSecond)
                .FirstOrDefault()
            : null;

        if (conDisco)
        {
            _discos = DiskUsageParser.Parse(P(5));
        }

        _cpuAnterior = cpuActual;
        _redAnterior = redActual;
        _instanteAnterior = ahora;

        Ultima = new MuestraDeFranja(
            cpu,
            MemoryInfoParser.Parse(P(1)),
            LoadAverageParser.ParseLoad(P(2)),
            LoadAverageParser.ParseUptime(P(3)),
            PeorDe(_discos),
            _discos,
            red,
            Distribucion);

        return Ultima;
    }

    /// <summary>El punto de montaje más comprometido, que es el que muestra la franja.</summary>
    /// <param name="discos">Los discos leídos.</param>
    public static DiskMetrics? PeorDe(IReadOnlyList<DiskMetrics> discos)
    {
        ArgumentNullException.ThrowIfNull(discos);

        return discos.Count == 0
            ? null
            : discos.Aggregate((peor, d) => d.UsedPercent > peor.UsedPercent ? d : peor);
    }
}
