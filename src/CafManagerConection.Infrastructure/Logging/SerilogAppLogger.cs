using CafManagerConection.Domain.Sessions;
using CafManagerConection.Infrastructure.Configuration;
using CafManagerConection.UseCases.Abstractions;
using Serilog;
using Serilog.Core;

namespace CafManagerConection.Infrastructure.Logging;

/// <summary><see cref="IAppLogger"/> sobre Serilog, con rotación diaria y retención de 30 días (FR-057a).</summary>
public sealed class SerilogAppLogger : IAppLogger, IDisposable
{
    private readonly Logger _logger;

    public SerilogAppLogger(AppPaths paths)
    {
        paths.EnsureCreated();

        _logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                Path.Combine(paths.LogsDirectory, "cmc-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }

    public void ApplicationStarted(string version) =>
        _logger.Information("Aplicación iniciada. Versión {Version}", version);

    public void ApplicationStopping(int activeSessions) =>
        _logger.Information("Aplicación cerrándose con {Sessions} sesiones activas", activeSessions);

    public void ConnectionOpening(Guid connectionId, string protocol, string host, int port) =>
        _logger.Information(
            "Abriendo conexión {ConnectionId} {Protocol} hacia {Host}:{Port}",
            connectionId, protocol, host, port);

    public void ConnectionSucceeded(Guid connectionId, TimeSpan elapsed) =>
        _logger.Information(
            "Conexión {ConnectionId} establecida en {Elapsed} ms",
            connectionId, (int)elapsed.TotalMilliseconds);

    public void ConnectionFailed(
        Guid connectionId, SessionFailureReason reason, string? technicalDetail) =>
        _logger.Warning(
            "Conexión {ConnectionId} falló por {Reason}. {Detail}",
            connectionId, reason, technicalDetail ?? "(sin detalle)");

    public void ConnectionClosed(Guid connectionId, TimeSpan duration) =>
        _logger.Information(
            "Conexión {ConnectionId} cerrada tras {Seconds} s",
            connectionId, (int)duration.TotalSeconds);

    public void TunnelStarted(Guid tunnelId, int localPort) =>
        _logger.Information("Túnel {TunnelId} activo en el puerto local {Port}", tunnelId, localPort);

    public void TunnelStopped(Guid tunnelId, int localPort) =>
        _logger.Information("Túnel {TunnelId} detenido, puerto {Port} liberado", tunnelId, localPort);

    public void DatabaseMigrated(int fromVersion, int toVersion) =>
        _logger.Information(
            "Base de datos migrada de la versión {From} a la {To}", fromVersion, toVersion);

    public void DatabaseCorruptionRecovered(string preservedPath) =>
        _logger.Warning(
            "La base de datos era ilegible. Se preservó en {Path} y se creó una nueva",
            preservedPath);

    public void TechnicalError(string operation, Exception exception) =>
        _logger.Error(exception, "Error técnico al {Operation}", operation);

    public void PlatformActionPerformed(Guid connectionId, string action) =>
        _logger.Information(
            "Acción de plataforma {Action} sobre la conexión {ConnectionId}", action, connectionId);

    public void WorkCompleted(Guid connectionId, RemoteWork work, TimeSpan elapsed) =>
        _logger.Information(
            "Tiempo {Work} en {ConnectionId}: {Elapsed} ms",
            work, connectionId, (int)elapsed.TotalMilliseconds);

    public void Dispose() => _logger.Dispose();
}
