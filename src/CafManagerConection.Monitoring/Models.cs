namespace CafManagerConection.Monitoring;

public readonly record struct CpuMetrics(double UsedPercent, int CoreCount);

public readonly record struct MemoryMetrics(long TotalBytes, long UsedBytes, long AvailableBytes)
{
    public double UsedPercent => TotalBytes == 0 ? 0 : UsedBytes * 100.0 / TotalBytes;
}

public readonly record struct LoadMetrics(double OneMinute, double FiveMinutes, double FifteenMinutes);

public sealed record DiskMetrics(
    string MountPoint,
    string FileSystem,
    long TotalBytes,
    long UsedBytes,
    long AvailableBytes,
    string? Type = null)
{
    public double UsedPercent => TotalBytes == 0 ? 0 : UsedBytes * 100.0 / TotalBytes;
}

public sealed record DiskIoMetrics(
    string Device, double ReadBytesPerSecond, double WriteBytesPerSecond, double BusyPercent);

/// <summary>Lectura cruda de una línea de <c>/proc/diskstats</c>; los sectores son de 512 bytes.</summary>
public readonly record struct DiskIoSample(
    string Device, long SectorsRead, long SectorsWritten, long MillisecondsBusy);

public sealed record InterfaceInfo(
    string Name,
    string? MacAddress,
    int Mtu,
    string State,
    bool IsUp,
    IReadOnlyList<string> IPv4,
    IReadOnlyList<string> IPv6,
    string? Master = null)
{
    // Los túneles VPN (tun, tap) no entran acá: detrás de una VPN, ésa es la interfaz por la que llega la conexión.
    public bool EsDeContenedor =>
        Name.StartsWith("veth", StringComparison.Ordinal)
        || Name.StartsWith("br-", StringComparison.Ordinal)
        || Name.StartsWith("docker", StringComparison.Ordinal)
        || Name.StartsWith("virbr", StringComparison.Ordinal)
        || Master is { Length: > 0 };
}

public sealed record RouteInfo(
    string Destination,
    string? Gateway,
    string Device,
    string? Source,
    int? Metric,
    bool LinkDown,
    bool IsIPv6)
{
    public bool EsPredeterminada =>
        Destination.Equals("default", StringComparison.Ordinal)
        || Destination is "0.0.0.0/0" or "::/0";
}

public sealed record ProcessInfo(
    int Pid,
    int ParentPid,
    string User,
    double CpuPercent,
    double MemoryPercent,
    long ResidentBytes,
    TimeSpan Elapsed,
    string State,
    int Threads,
    string Command);

/// <summary>Presión de <c>/proc/pressure</c>: <c>Some</c> es el % de tiempo con alguna tarea esperando el recurso; <c>Full</c>, con todas (FR-174).</summary>
public readonly record struct PressureMetrics(double Some, double Full)
{
    public static PressureMetrics Ninguna => new(0, 0);
}

/// <summary>Presión de los tres recursos que informa el núcleo. <c>null</c> en el que no la informa.</summary>
public readonly record struct PressureSet(
    PressureMetrics? Cpu, PressureMetrics? Io, PressureMetrics? Memory)
{
    public bool Disponible => Cpu is not null || Io is not null || Memory is not null;
}

public readonly record struct SwapMetrics(long TotalBytes, long UsedBytes)
{
    public bool Existe => TotalBytes > 0;

    public double UsedPercent => TotalBytes == 0 ? 0 : UsedBytes * 100.0 / TotalBytes;
}

public sealed record TemperatureInfo(string Sensor, double Celsius);

public sealed record NetworkMetrics(
    string Interface, double BytesInPerSecond, double BytesOutPerSecond);

public sealed record SystemInfo(
    string HostName,
    string? Distribution,
    string? KernelVersion,
    DateTimeOffset? ServerTime,
    int ConnectedUsers,
    int ProcessCount,
    IReadOnlyList<string> FailedServices,
    string? CpuModel = null,
    IReadOnlyList<string>? Dns = null,
    string? DnsSearch = null);

public sealed record ServerSnapshot(
    DateTimeOffset TakenAt,
    CpuMetrics Cpu,
    MemoryMetrics Memory,
    LoadMetrics Load,
    TimeSpan Uptime,
    IReadOnlyList<DiskMetrics> Disks,
    IReadOnlyList<NetworkMetrics> Interfaces,
    SystemInfo System,
    SwapMetrics Swap = default,
    PressureSet Pressure = default,
    IReadOnlyList<DiskIoMetrics>? DiskIo = null,
    IReadOnlyList<InterfaceInfo>? NetworkInterfaces = null,
    IReadOnlyList<RouteInfo>? Routes = null,
    IReadOnlyList<TemperatureInfo>? Temperatures = null);

/// <summary>Lectura cruda de <c>/proc/stat</c>, para calcular el uso por diferencia.</summary>
public readonly record struct CpuSample(long Total, long Idle, int CoreCount);

/// <summary>Lectura cruda de <c>/proc/net/dev</c>, para calcular la velocidad por diferencia.</summary>
public sealed record NetworkSample(string Interface, long BytesIn, long BytesOut);
