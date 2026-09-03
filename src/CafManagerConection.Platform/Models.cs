namespace CafManagerConection.Platform;

public enum ServiceState
{
    NotInstalled,

    NotRunning,

    NoPermission,

    Available,
}

public sealed record ServerCapabilities(
    bool IsLinux,
    ServiceState Docker,
    ServiceState Nginx,
    ServiceState Supervisord)
{
    public static ServerCapabilities None { get; } =
        new(false, ServiceState.NotInstalled, ServiceState.NotInstalled, ServiceState.NotInstalled);

    public bool HasDocker => Docker != ServiceState.NotInstalled;

    public bool HasNginx => Nginx != ServiceState.NotInstalled;

    public bool HasSupervisord => Supervisord != ServiceState.NotInstalled;

    public bool DockerNeedsSudo => Docker == ServiceState.NoPermission;

    public static string Explicar(string servicio, ServiceState estado) => estado switch
    {
        ServiceState.NotInstalled => $"{servicio} no está instalado en este servidor.",
        ServiceState.NotRunning => $"{servicio} está instalado pero no está corriendo.",
        ServiceState.NoPermission =>
            $"{servicio} está corriendo, pero este usuario no puede consultarlo. "
            + "Hace falta permiso de sudo para ese comando, o pertenecer al grupo correspondiente.",
        _ => string.Empty,
    };
}

public sealed record ContainerInfo(
    string Id,
    string Name,
    string Image,
    string State,
    string Status,
    IReadOnlyList<string> PublishedPorts,
    string? ComposeProject = null,
    string? ComposeService = null)
{
    public bool IsRunning =>
        State.Equals("running", StringComparison.OrdinalIgnoreCase);

    public bool IsStandalone => string.IsNullOrEmpty(ComposeProject);

    /// <summary>Con qué gravedad pintar el contenedor (FR-150a).</summary>
    // docker ps mete el chequeo de salud en el texto libre de Status: «Up 3 minutes (unhealthy)».
    public GravedadDeContenedor Gravedad
    {
        get
        {
            var estado = State.Trim().ToLowerInvariant();

            if (estado == "running")
            {
                return Status.Contains("(unhealthy)", StringComparison.OrdinalIgnoreCase)
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
}

public enum GravedadDeContenedor
{
    Corriendo,

    Detenido,

    Advertencia,

    Falla,
}

// Una sola lectura por consulta (FR-107); el CPU% que informa Docker pasa de 100 % con varios núcleos.
public sealed record ContainerUsage(
    string Id,
    double CpuPercent,
    long MemoryBytes,
    long MemoryLimitBytes)
{
    public double MemoryPercent =>
        MemoryLimitBytes > 0 ? MemoryBytes * 100.0 / MemoryLimitBytes : 0;
}

public sealed record ComposeService(string Name, string? ContainerName, bool IsRunning);

public sealed record ComposeProject(
    string Name, string FilePath, IReadOnlyList<ComposeService> Services);

public sealed record NginxSite(
    string Id,
    IReadOnlyList<string> ServerNames,
    IReadOnlyList<int> ListenPorts,
    string? DocumentRoot,
    string? ConfigFile);

public enum GravedadDeProceso
{
    Corriendo,

    Advertencia,

    Falla,
}

public sealed record SupervisorProcess(string Name, string State, string? Detail)
{
    public bool HasFailed =>
        State.Equals("FATAL", StringComparison.OrdinalIgnoreCase) ||
        State.Equals("BACKOFF", StringComparison.OrdinalIgnoreCase) ||
        State.Equals("EXITED", StringComparison.OrdinalIgnoreCase) ||
        State.Equals("UNKNOWN", StringComparison.OrdinalIgnoreCase);

    public bool IsRunning => State.Equals("RUNNING", StringComparison.OrdinalIgnoreCase);

    public GravedadDeProceso Gravedad => State.ToUpperInvariant() switch
    {
        "RUNNING" => GravedadDeProceso.Corriendo,
        "FATAL" or "BACKOFF" or "UNKNOWN" => GravedadDeProceso.Falla,
        _ => GravedadDeProceso.Advertencia,
    };

    /// <summary>Cuánto lleva arriba, leído de <c>pid 80621, uptime 77 days, 22:02:44</c>.</summary>
    public TimeSpan? Uptime
    {
        get
        {
            if (Detail is null)
            {
                return null;
            }

            var m = TiempoArriba.Match(Detail);

            if (!m.Success)
            {
                return null;
            }

            var dias = m.Groups[1].Success ? int.Parse(m.Groups[1].Value) : 0;

            return new TimeSpan(
                dias,
                int.Parse(m.Groups[2].Value),
                int.Parse(m.Groups[3].Value),
                int.Parse(m.Groups[4].Value));
        }
    }

    private static readonly System.Text.RegularExpressions.Regex TiempoArriba = new(
        @"uptime\s+(?:(\d+)\s+days?,\s*)?(\d+):(\d{2}):(\d{2})",
        System.Text.RegularExpressions.RegexOptions.Compiled
        | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
}

/// <summary>Un puerto a la escucha; <c>Process</c> y <c>Pid</c> quedan nulos sin permiso para ver el socket (FR-164d).</summary>
public sealed record ListeningPort(
    string Protocol, string Address, int Port, string? Process, int? Pid = null);
