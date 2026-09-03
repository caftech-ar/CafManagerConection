# Contrato: puertos de archivos, túneles, métricas e inventario

**Feature**: `001-rdp-ssh-server-manager` · **Fase**: 1

Puertos que sostienen las historias US6 a US10. Los declara el núcleo; los implementan
`CafManagerConection.Ssh` (archivos y túneles), `CafManagerConection.Monitoring` (métricas) y
`CafManagerConection.Platform` (inventario). Ninguna firma menciona SSH.NET.

## Capacidades del servidor

Todo panel opcional se ofrece o se oculta según lo que el host exponga (FR-086, FR-099,
FR-103). La detección ocurre una vez por sesión.

```csharp
public interface IServerCapabilityDetector
{
    Task<ServerCapabilities> DetectAsync(ISshSession session, CancellationToken ct);
}

public sealed record ServerCapabilities(
    bool IsLinux,
    bool HasDocker,
    bool DockerNeedsSudo,
    bool HasNginx,
    bool HasSupervisord,
    string? Distribution,
    string? KernelVersion);
```

**Regla**: una capacidad ausente **no** es un error ni un panel vacío: el panel no se ofrece.
La detección nunca lanza; si algo falla, la capacidad se reporta como ausente.

---

## Archivos remotos (US6)

```csharp
public interface IRemoteFileSession : IAsyncDisposable
{
    Task<OperationResult> ConnectAsync(CancellationToken ct);

    Task<IReadOnlyList<RemoteEntry>> ListAsync(string path, CancellationToken ct);
    Task<OperationResult> CreateDirectoryAsync(string path, CancellationToken ct);
    Task<OperationResult> RenameAsync(string from, string to, CancellationToken ct);
    Task<OperationResult> DeleteAsync(string path, CancellationToken ct);

    Task<OperationResult> UploadAsync(
        string localPath, string remotePath,
        IProgress<TransferProgress> progress, CancellationToken ct);

    Task<OperationResult> DownloadAsync(
        string remotePath, string localPath,
        IProgress<TransferProgress> progress, CancellationToken ct);
}

public sealed record RemoteEntry(
    string Name, string FullPath, bool IsDirectory, long SizeBytes, DateTimeOffset ModifiedAt);

public readonly record struct TransferProgress(long BytesTransferred, long TotalBytes);

public interface IRemoteFileSessionFactory
{
    /// <summary>Crea la sesión de archivos reutilizando los datos de una sesión SSH viva.</summary>
    IRemoteFileSession CreateFor(SshSessionRequest request, ICredentialProvider credentials);
}
```

**Obligaciones**:

1. La conexión se abre al abrir el panel y se cierra al cerrarlo (FR-072). Reutiliza
   credencial, usuario, puerto y fingerprint aceptado, sin volver a pedirlos.
2. Un fallo acá **no** puede afectar a la sesión de terminal (FR-076): las dos sesiones son
   independientes y no comparten estado.
3. Cada transferencia informa progreso y respeta la cancelación (FR-073).
4. El fallo de un archivo no aborta la cola (FR-075): el resultado es por archivo.
5. **Prohibido registrar rutas, nombres de archivo o contenido** (FR-077, Principio II).
6. No se exponen permisos, dueño, enlaces simbólicos ni edición remota (FR-078).

---

## Túneles (US8)

```csharp
public interface ITunnelManager
{
    IReadOnlyList<TunnelStatus> GetStatus(Guid connectionId);

    Task<OperationResult> StartAsync(Guid tunnelId, CancellationToken ct);
    Task<OperationResult> StopAsync(Guid tunnelId);
    Task StopAllForSessionAsync(Guid sessionId);

    event EventHandler<TunnelStatus>? StatusChanged;
}

public sealed record TunnelStatus(
    Guid TunnelId, string Name, int LocalPort, string RemoteHost, int RemotePort,
    bool IsActive, string? FailureMessage);
```

**Obligaciones**:

1. `StartAsync` sobre un puerto local ocupado devuelve un fallo que **nombra el puerto**, y
   el túnel queda detenido, nunca a medias (FR-093).
2. Cerrar la sesión SSH que sostiene un túnel libera su puerto local (FR-092).
3. Los túneles marcados con `AutoStart` se levantan al conectar la sesión (FR-091); si uno
   falla, los demás se levantan igual y el fallo se reporta por túnel.

---

## Métricas del servidor (US7)

```csharp
public interface IMonitoringSession : IAsyncDisposable
{
    bool IsRunning { get; }

    event EventHandler<ServerSnapshot>? SnapshotReceived;

    Task StartAsync(TimeSpan interval, CancellationToken ct);
    Task StopAsync();
}

public sealed record ServerSnapshot(
    DateTimeOffset TakenAt,
    CpuMetrics Cpu,
    MemoryMetrics Memory,
    LoadMetrics Load,
    TimeSpan Uptime,
    IReadOnlyList<DiskMetrics> Disks,
    IReadOnlyList<NetworkMetrics> Interfaces,
    SystemInfo System);

public readonly record struct CpuMetrics(double UsedPercent, int CoreCount);
public readonly record struct MemoryMetrics(long TotalBytes, long UsedBytes, long AvailableBytes);
public readonly record struct LoadMetrics(double OneMinute, double FiveMinutes, double FifteenMinutes);
public sealed record DiskMetrics(string MountPoint, string FileSystem, long TotalBytes, long UsedBytes, long AvailableBytes);
public sealed record NetworkMetrics(string Interface, double BytesInPerSecond, double BytesOutPerSecond);
public sealed record SystemInfo(
    string HostName, string? Distribution, string? KernelVersion,
    DateTimeOffset ServerTime, int ConnectedUsers, int ProcessCount,
    IReadOnlyList<string> FailedServices);
```

**Obligaciones**:

1. Conexión SSH **auxiliar**, independiente de la del terminal y de la de archivos (FR-080).
   Se abre al iniciar el muestreo y se cierra al detenerlo (FR-084).
2. CPU por diferencia entre dos lecturas de `/proc/stat`; memoria como `MemTotal` menos
   `MemAvailable`; red por diferencia de `/proc/net/dev` (FR-081, FR-082). **Prohibido
   interpretar la salida de `top` o `free`**, que cambia según distribución, versión e idioma.
3. Si una consulta sigue en curso cuando toca la siguiente, **se cancela la anterior** en
   lugar de encolarlas (FR-084). Un servidor lento no debe acumular trabajo.
4. Se filtran los sistemas de archivos virtuales y las interfaces sin tráfico (FR-083).
5. `SnapshotReceived` se despacha al hilo de interfaz.
6. **Nada se persiste** (FR-085) y **nada de la salida remota se registra** (Principio II).

### Analizadores

Cada formato remoto se interpreta en una unidad propia, con pruebas sobre capturas reales:
`CpuStatParser`, `MemoryInfoParser`, `LoadAverageParser`, `NetworkStatsParser`,
`DiskUsageParser`. Son funciones puras de texto a modelo: no tocan la red y se prueban sin
servidor.

---

## Inventario de plataforma (US9 y US10)

```csharp
public interface IPlatformInventory
{
    Task<OperationResult<IReadOnlyList<ContainerInfo>>> GetContainersAsync(CancellationToken ct);
    Task<OperationResult<IReadOnlyList<ComposeProject>>> GetComposeProjectsAsync(CancellationToken ct);
    Task<OperationResult<IReadOnlyList<NginxSite>>> GetNginxSitesAsync(CancellationToken ct);
    Task<OperationResult<string>> GetNginxSiteConfigAsync(string siteId, CancellationToken ct);
    Task<OperationResult<IReadOnlyList<SupervisorProcess>>> GetSupervisorProcessesAsync(CancellationToken ct);
}

public sealed record ContainerInfo(
    string Id, string Name, string Image, string State, string Status,
    IReadOnlyList<string> PublishedPorts, TimeSpan? Uptime);

public sealed record ComposeProject(
    string Name, string FilePath, IReadOnlyList<ComposeService> Services);

public sealed record ComposeService(string Name, string? ContainerId, bool IsRunning);

public sealed record NginxSite(
    string Id, IReadOnlyList<string> ServerNames, IReadOnlyList<int> ListenPorts, string? DocumentRoot);

public sealed record SupervisorProcess(string Name, string State, TimeSpan? Uptime, bool HasFailed);
```

**Obligaciones**:

1. **Sólo lectura en esta versión** (FR-100). No hay ningún método que inicie, detenga,
   recree o recargue nada. La ausencia es el mecanismo de cumplimiento, igual que en
   `IAppLogger`.
2. Consulta por línea de comandos sobre SSH con formato de salida estable; se reintenta con
   `sudo` cuando corresponde y se informa la falta de permisos con claridad (FR-095, FR-104).
3. Cuando hay un túnel disponible hacia la API de Docker, se prefiere la API sobre el texto
   (FR-096).
4. Cada consulta tiene tiempo límite; al vencer se cancela y se informa, sin congelar el
   panel ni encolar consultas.
5. **Prohibido registrar la salida de los comandos y el contenido de las configuraciones**
   (FR-105, Principio II).

**Por qué `OperationResult` y no excepciones**: un servidor sin Docker, un usuario sin
permisos o un comando que no responde son resultados previstos, no fallos del programa. El
panel necesita distinguirlos para decidir si se oculta, si pide permisos o si reintenta.
