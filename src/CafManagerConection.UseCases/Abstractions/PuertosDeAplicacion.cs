using CafManagerConection.Domain.Credentials;
using CafManagerConection.Domain.Sessions;

namespace CafManagerConection.UseCases.Abstractions;

/// <summary>Lee y escribe el secreto de una fila. Cifra o no según haya clave maestra, y eso lo decide el vault, no quien llama.</summary>
public interface ICredentialStore
{
    /// <summary><c>null</c> cuando esa fila no tiene contraseña guardada; no es un error, dispara el pedido al usuario. El llamador pisa el arreglo cuando termina.</summary>
    Task<char[]?> ReadAsync(ReferenciaDeSecreto referencia, CancellationToken ct = default);

    Task WriteAsync(
        ReferenciaDeSecreto referencia,
        ReadOnlyMemory<char> secreto,
        CancellationToken ct = default);

    Task DeleteAsync(ReferenciaDeSecreto referencia, CancellationToken ct = default);

    /// <summary>Se contesta con el vault cerrado: saber que hay una contraseña no es leerla.</summary>
    Task<bool> ExistsAsync(ReferenciaDeSecreto referencia, CancellationToken ct = default);

    /// <summary>Dónde hay contraseña guardada; nunca el secreto. También se contesta con el vault cerrado.</summary>
    Task<IReadOnlyList<ReferenciaDeSecreto>> EnumerateAsync(CancellationToken ct = default);
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

/// <summary>Copia datos al portapapeles con borrado diferido.</summary>
public interface IClipboardService
{
    void CopyText(string text);

    /// <summary>Copia un secreto y vacía el portapapeles a los 30 segundos si sigue siendo el copiado.</summary>
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

// No hay método para registrar teclado, terminal, pantalla, portapapeles ni salida de comandos.
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

    /// <summary>Recibe el verbo y no el objeto: el nombre del proceso es contenido de sesión.</summary>
    void PlatformActionPerformed(Guid connectionId, string action);

    void WorkCompleted(Guid connectionId, RemoteWork work, TimeSpan elapsed);
}
