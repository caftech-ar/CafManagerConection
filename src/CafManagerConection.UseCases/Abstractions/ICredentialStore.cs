using CafManagerConection.Domain.Credentials;
using CafManagerConection.Domain.Sessions;

namespace CafManagerConection.UseCases.Abstractions;

/// <summary>Único lugar donde puede vivir un secreto (Principio II).</summary>
public interface ICredentialStore
{
    /// <summary><c>null</c> cuando no existe; no es un error, dispara el pedido al usuario (FR-039).</summary>
    Task<StoredCredential?> ReadAsync(string credentialKey, CancellationToken ct = default);

    Task WriteAsync(string credentialKey, StoredCredential credential, CancellationToken ct = default);

    Task DeleteAsync(string credentialKey, CancellationToken ct = default);

    Task<bool> ExistsAsync(string credentialKey, CancellationToken ct = default);

    /// <summary>Sólo los nombres de las claves con ese prefijo; nunca el secreto (FR-158).</summary>
    Task<IReadOnlyList<string>> EnumerateKeysAsync(
        string prefix, CancellationToken ct = default);
}

public interface ICredentialProvider
{
    Task<StoredCredential?> GetForConnectionAsync(Guid connectionId, CancellationToken ct = default);
}

public interface ICredentialPrompt
{
    Task<CredentialPromptResult?> RequestAsync(
        string connectionName, string? suggestedUserName, bool needsDomain, CancellationToken ct = default);
}

public sealed record CredentialPromptResult(
    string UserName, string? Domain, string Secret, bool Remember);

/// <summary>Copia datos al portapapeles con borrado diferido (FR-121 a FR-124).</summary>
public interface IClipboardService
{
    void CopyText(string text);

    /// <summary>Copia un secreto y vacía el portapapeles a los 30 segundos si sigue siendo el copiado (FR-123).</summary>
    void CopySecret(string secret);
}

public enum RemoteWork
{
    Handshake,

    ShellChannel,

    AuxiliaryHandshake,

    SftpHandshake,

    TunnelHandshake,

    PlatformDetection,

    Metrics,

    Docker,

    Nginx,

    Supervisor,

    Puertos,

    PanelBuild,
}

// No hay método para registrar teclado, terminal, pantalla, portapapeles ni salida de comandos (Principio II).
public interface IAppLogger
{
    void ApplicationStarted(string version);

    void ApplicationStopping(int activeSessions);

    void ConnectionOpening(Guid connectionId, string protocol, string host, int port);

    void ConnectionSucceeded(Guid connectionId, TimeSpan elapsed);

    void ConnectionFailed(Guid connectionId, SessionFailureReason reason, string? technicalDetail);

    void ConnectionClosed(Guid connectionId, TimeSpan duration);

    void TunnelStarted(Guid tunnelId, int localPort);

    void TunnelStopped(Guid tunnelId, int localPort);

    void DatabaseMigrated(int fromVersion, int toVersion);

    void DatabaseCorruptionRecovered(string preservedPath);

    void TechnicalError(string operation, Exception exception);

    /// <summary>Recibe el verbo y no el objeto: el nombre del proceso es contenido de sesión (FR-100).</summary>
    void PlatformActionPerformed(Guid connectionId, string action);

    void WorkCompleted(Guid connectionId, RemoteWork work, TimeSpan elapsed);
}
