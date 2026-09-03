# Contrato: servicios de aplicación

**Feature**: `001-rdp-ssh-server-manager` · **Fase**: 1

Superficie que `CafManagerConection.UseCases` expone a la capa de interfaz. Es el único
punto por el que los formularios acceden a la lógica: ningún formulario habla directamente
con un repositorio, con el almacén de credenciales ni con un adaptador de protocolo.

---

## Resultado de operación

```csharp
public readonly record struct OperationResult(bool Success, string? ErrorMessage)
{
    public static OperationResult Ok();
    public static OperationResult Fail(string message);
}

public readonly record struct OperationResult<T>(bool Success, T? Value, string? ErrorMessage);
```

Las operaciones que pueden fallar por causas previstas —validación, credencial ausente,
conflicto— devuelven `OperationResult` en lugar de lanzar. Las excepciones quedan para
defectos de programación. Esto mantiene los formularios libres de bloques `try/catch`
alrededor de cada llamada.

---

## `IFolderService`

```csharp
public interface IFolderService
{
    Task<OperationResult<Folder>> CreateAsync(string name, Guid? parentId, CancellationToken ct);
    Task<OperationResult> RenameAsync(Guid id, string newName, CancellationToken ct);
    Task<OperationResult> MoveAsync(Guid id, Guid? newParentId, CancellationToken ct);

    /// <summary>Cuántas conexiones y subcarpetas se eliminarían. Para la confirmación previa.</summary>
    Task<FolderDeletionImpact> GetDeletionImpactAsync(Guid id, CancellationToken ct);

    /// <summary>Elimina la carpeta, su contenido y las credenciales de sus conexiones.</summary>
    Task<OperationResult> DeleteAsync(Guid id, CancellationToken ct);
}

public sealed record FolderDeletionImpact(
    int FolderCount,
    int ConnectionCount,
    int ActiveSessionCount);
```

`GetDeletionImpactAsync` existe para que la interfaz pueda cumplir FR-010 y FR-049 sin
calcular nada por su cuenta: pregunta el impacto, lo muestra y solo entonces confirma.

`MoveAsync` rechaza los movimientos que crearían un ciclo (mover una carpeta dentro de sí
misma o de un descendiente).

---

## `IConnectionService`

```csharp
public interface IConnectionService
{
    Task<IReadOnlyList<ConnectionSummary>> GetTreeAsync(CancellationToken ct);
    Task<IReadOnlyList<ConnectionSummary>> SearchAsync(string query, CancellationToken ct);
    Task<OperationResult<ConnectionDetail>> GetDetailAsync(Guid id, CancellationToken ct);

    Task<OperationResult<Guid>> CreateAsync(
        ConnectionDraft draft, CredentialInput? credential, CancellationToken ct);

    Task<OperationResult> UpdateAsync(
        Guid id, ConnectionDraft draft, CredentialInput? credential, CancellationToken ct);

    Task<OperationResult<Guid>> DuplicateAsync(Guid id, CancellationToken ct);
    Task<OperationResult> MoveAsync(Guid id, Guid? folderId, CancellationToken ct);
    Task<OperationResult> ReorderAsync(Guid folderId, IReadOnlyList<Guid> orderedIds, CancellationToken ct);

    /// <summary>Elimina la conexión y su credencial asociada.</summary>
    Task<OperationResult> DeleteAsync(Guid id, CancellationToken ct);

    Task<OperationResult> ClearCredentialAsync(Guid id, CancellationToken ct);
    Task<bool> HasStoredCredentialAsync(Guid id, CancellationToken ct);

    /// <summary>Nombres iguales en la misma carpeta: se advierte, no se impide (FR-053).</summary>
    Task<bool> IsNameDuplicatedAsync(Guid? folderId, string name, Guid? excludingId, CancellationToken ct);
}
```

**Orden obligatorio en `DeleteAsync`** (FR-038): primero se borra la credencial del almacén
del sistema, después la fila de la base. Si el borrado de la credencial falla, la conexión
**no** se elimina y la operación devuelve un fallo. Es preferible una conexión visible a una
credencial huérfana que el usuario ya no puede encontrar ni borrar.

**`SearchAsync`** compara el texto contra nombre, host y usuario, sin distinguir mayúsculas
ni acentos (FR-007), y devuelve también las carpetas ancestro de cada coincidencia para que
el árbol se pueda dibujar con su jerarquía.

**`DuplicateAsync`** copia todos los parámetros, agrega un sufijo distintivo al nombre y
reutiliza la credencial del original de forma predeterminada (supuesto documentado).

---

## `ISessionManager`

```csharp
public interface ISessionManager
{
    IReadOnlyList<SessionInfo> ActiveSessions { get; }

    event EventHandler<SessionInfo>? SessionOpened;
    event EventHandler<SessionInfo>? SessionStateChanged;
    event EventHandler<Guid>? SessionClosed;

    Task<OperationResult<Guid>> OpenAsync(Guid connectionId, CancellationToken ct);
    Task<OperationResult> ReconnectAsync(Guid sessionId, CancellationToken ct);
    Task CloseAsync(Guid sessionId);
    Task CloseAllAsync();

    int CountForConnection(Guid connectionId);
}

public sealed record SessionInfo(
    Guid SessionId,
    Guid ConnectionId,
    string DisplayName,
    string Protocol,
    string Host,
    string? Username,
    SessionState State,
    SessionFailure? Failure,
    DateTimeOffset StartedAt);
```

**Responsabilidades**:

- Resolver la credencial antes de abrir, pidiéndola al usuario cuando falta (FR-039).
- Registrar el evento de historial y actualizar la fecha de última conexión ante un intento
  exitoso (FR-008, FR-009).
- Aislar cada sesión: una excepción o un fallo en una no afecta a las demás ni cierra la
  aplicación (FR-054, SC-012).
- `CloseAllAsync` cancela todas las sesiones y espera un plazo acotado antes de forzar; es lo
  que usa el cierre de la aplicación tras la confirmación de FR-048.
- `CountForConnection` alimenta las advertencias de FR-049 antes de eliminar una conexión con
  sesiones abiertas.

**`OpenAsync` nunca lanza por un fallo de conexión**: la sesión se crea igual y queda en
estado `Error` con su motivo, de modo que la pestaña exista y ofrezca reconectar. Devuelve
fallo solo cuando la sesión no se pudo ni siquiera crear (por ejemplo, la conexión no
existe).

---

## `IAppSettingsService`

```csharp
public interface IAppSettingsService
{
    Task<WindowPlacement> GetWindowPlacementAsync(CancellationToken ct);
    Task SaveWindowPlacementAsync(WindowPlacement placement, CancellationToken ct);

    Task<AppTheme> GetThemeAsync(CancellationToken ct);
    Task SetThemeAsync(AppTheme theme, CancellationToken ct);

    Task<TerminalPreferences> GetTerminalPreferencesAsync(CancellationToken ct);
    Task SaveTerminalPreferencesAsync(TerminalPreferences preferences, CancellationToken ct);
}

public sealed record WindowPlacement(int X, int Y, int Width, int Height, bool Maximized);
public enum AppTheme { Light, Dark, System }
public sealed record TerminalPreferences(string FontFamily, int FontSize, int ScrollbackLines);
```

`GetWindowPlacementAsync` valida que la geometría guardada caiga dentro de algún monitor
conectado; si no, devuelve una posición centrada en el principal. Sin esa validación, una
aplicación cerrada en un monitor que ya no está reaparece fuera de la pantalla (FR-047).

---

## Validación

```csharp
public interface IConnectionValidator
{
    ValidationResult Validate(ConnectionDraft draft);
}

public sealed record ValidationResult(bool IsValid, IReadOnlyList<ValidationError> Errors);
public sealed record ValidationError(string Field, string Message);
```

Reglas que aplica, alineadas con `data-model.md`:

| Campo | Regla |
| --- | --- |
| `Name` | obligatorio, 1 a 100 caracteres |
| `Host` | obligatorio; nombre de host o dirección IP válida |
| `Port` | entre 1 y 65535 |
| `Notes` | hasta 4000 caracteres |
| `PrivateKeyPath` | obligatorio cuando la autenticación es por clave privada |
| `KeepAliveSeconds` | entre 0 y 3600 |
| `Protocol` | inmutable al editar una conexión existente |

La existencia del archivo de clave privada **no** se valida al guardar: se comprueba al
conectar, porque la clave puede estar en una unidad que en ese momento no está disponible.
