# Contrato: puertos de infraestructura (persistencia, credenciales, registro)

**Feature**: `001-rdp-ssh-server-manager` · **Fase**: 1

Interfaces declaradas por `UseCases` e implementadas por
`CafManagerConection.Infrastructure`. Ninguna menciona SQLite, Dapper, P/Invoke ni Serilog.

---

## Persistencia

```csharp
public interface IFolderRepository
{
    Task<IReadOnlyList<Folder>> GetAllAsync(CancellationToken ct);
    Task<Folder?> GetByIdAsync(Guid id, CancellationToken ct);
    Task AddAsync(Folder folder, CancellationToken ct);
    Task UpdateAsync(Folder folder, CancellationToken ct);

    /// <summary>Borra la carpeta, sus descendientes y las conexiones que contienen.</summary>
    /// <returns>Identificadores de todas las conexiones eliminadas, para que quien llama
    /// pueda borrar sus credenciales.</returns>
    Task<IReadOnlyList<Guid>> DeleteAsync(Guid id, CancellationToken ct);
}

public interface IConnectionRepository
{
    Task<IReadOnlyList<Connection>> GetAllAsync(CancellationToken ct);
    Task<Connection?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<RdpSettings?> GetRdpSettingsAsync(Guid connectionId, CancellationToken ct);
    Task<SshSettings?> GetSshSettingsAsync(Guid connectionId, CancellationToken ct);

    Task AddAsync(Connection connection, ConnectionSettings settings, CancellationToken ct);
    Task UpdateAsync(Connection connection, ConnectionSettings settings, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);

    Task SetLastConnectedAsync(Guid id, DateTimeOffset when, CancellationToken ct);
    Task ReorderAsync(Guid folderId, IReadOnlyList<Guid> orderedIds, CancellationToken ct);
}

public interface IConnectionHistoryRepository
{
    Task AddAsync(ConnectionHistoryEntry entry, CancellationToken ct);
    Task<IReadOnlyList<ConnectionHistoryEntry>> GetForConnectionAsync(
        Guid connectionId, int limit, CancellationToken ct);
}

public interface ISettingsStore
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct);
    Task SetAsync<T>(string key, T value, CancellationToken ct);
}
```

**Contrato de comportamiento**:

- `AddAsync` y `UpdateAsync` de `IConnectionRepository` escriben la conexión y su
  configuración específica **en una sola transacción**. Una conexión sin su fila de
  configuración es un estado inválido que nunca debe existir en disco.
- `DeleteAsync` **no** borra la credencial: eso es responsabilidad de la capa de aplicación,
  que la borra primero (ver `IConnectionService.DeleteAsync`).
- `IConnectionHistoryRepository.AddAsync` aplica la retención de 100 eventos por conexión.
- Las carpetas y conexiones se cargan enteras al arrancar: son decenas o cientos de filas y
  el filtrado de FR-007 se resuelve en memoria, sin ida y vuelta a la base por pulsación.

### Migraciones

```csharp
public interface IDatabaseInitializer
{
    /// <summary>Abre o crea la base y aplica las migraciones pendientes.</summary>
    Task<DatabaseStartupResult> InitializeAsync(CancellationToken ct);
}

public sealed record DatabaseStartupResult(
    bool Migrated,
    int FromVersion,
    int ToVersion,
    string? RecoveredFromCorruptionPath);
```

Ante una base ilegible o corrupta, la implementación la renombra, crea una nueva y devuelve
la ruta del archivo preservado en `RecoveredFromCorruptionPath` para que la interfaz informe
al usuario (FR-052). No lanza excepción: no poder abrir la base no debe impedir arrancar.

---

## Credenciales

```csharp
public interface ICredentialStore
{
    Task<StoredCredential?> ReadAsync(string credentialKey, CancellationToken ct);
    Task WriteAsync(string credentialKey, StoredCredential credential, CancellationToken ct);
    Task DeleteAsync(string credentialKey, CancellationToken ct);
    Task<bool> ExistsAsync(string credentialKey, CancellationToken ct);
}

/// <summary>
/// Credencial recuperada del almacén del sistema operativo.
/// Implementa <see cref="IDisposable"/>: al desecharse limpia el secreto de la memoria.
/// </summary>
public sealed class StoredCredential : IDisposable
{
    public string UserName { get; }
    public string? Domain { get; }
    public ReadOnlySpan<char> Secret { get; }

    /// <summary>Devuelve siempre "StoredCredential(redactada)".</summary>
    public override string ToString();
}

/// <summary>
/// Provee la credencial de una conexión, ya sea desde el almacén o pidiéndosela al usuario.
/// </summary>
public interface ICredentialProvider
{
    Task<StoredCredential?> GetAsync(Guid connectionId, CancellationToken ct);
}
```

**Reglas no negociables** (Principio II):

1. `StoredCredential.ToString()` devuelve un marcador redactado. Nunca el secreto. Esto es
   lo que hace que un registro accidental no filtre nada.
2. `StoredCredential` no se serializa: ni a JSON, ni a la base, ni a un archivo temporal.
3. `Dispose()` sobrescribe el buffer del secreto en memoria.
4. `ReadAsync` devuelve `null` cuando la credencial no existe. Eso **no** es un error: es la
   condición prevista por FR-039 que dispara el pedido al usuario.
5. `DeleteAsync` sobre una clave inexistente es una operación exitosa, no un fallo.
6. `credentialKey` siempre tiene el formato `cmc:<rdp|ssh>:<GUID de la conexión>`.

**Restricción heredada de la plataforma**: el secreto no puede superar los 2560 bytes. Esto
impide por diseño guardar allí el contenido de una clave privada, y confirma que solo se
guarde su ruta.

---

## Registro de eventos

```csharp
public interface IAppLogger
{
    void ApplicationStarted(string version);
    void ApplicationStopping(int activeSessions);

    void ConnectionOpening(Guid connectionId, string protocol, string host, int port);
    void ConnectionSucceeded(Guid connectionId, TimeSpan elapsed);
    void ConnectionFailed(Guid connectionId, SessionFailureReason reason, string? technicalDetail);
    void ConnectionClosed(Guid connectionId, TimeSpan duration);

    void DatabaseMigrated(int fromVersion, int toVersion);
    void DatabaseCorruptionRecovered(string preservedPath);

    void TechnicalError(string operation, Exception exception);
}
```

**Por qué una interfaz cerrada y no un registrador genérico**: el Principio II prohíbe que
lleguen secretos y contenido de sesión al archivo de log. Un `ILogger` genérico permite
escribir cualquier cosa y traslada la garantía a la disciplina de quien escribe cada línea.
Una interfaz con métodos de parámetros explícitos convierte esa garantía en algo auditable:
alcanza con revisar estos diez métodos para saber todo lo que puede terminar en disco.

**Reglas**:

- No existe ningún método que acepte un objeto arbitrario ni una plantilla libre.
- `technicalDetail` y `TechnicalError` nunca reciben credenciales ni contenido de sesión;
  quien llama es responsable de ello y hay pruebas que lo verifican.
- No hay ningún método para registrar entrada de teclado, salida de terminal, contenido de
  pantalla RDP ni portapapeles. La ausencia es el mecanismo de cumplimiento.
