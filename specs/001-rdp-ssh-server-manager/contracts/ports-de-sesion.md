# Contrato: puertos de sesión (RDP, SSH y terminal)

**Feature**: `001-rdp-ssh-server-manager` · **Fase**: 1

Estas interfaces son la frontera entre la aplicación y los adaptadores de protocolo. Las
declara `CafManagerConection.Domain` (o `UseCases`); las implementan
`CafManagerConection.Rdp`, `CafManagerConection.Ssh` y `CafManagerConection.Terminal`. Ningún
tipo de estas firmas menciona WPF, WinForms, COM ni SSH.NET: eso es lo que hace
cumplible el Principio I.

## Tipos compartidos

```csharp
public enum SessionState { Connecting, Connected, Disconnected, Error }

public enum SessionFailureReason
{
    HostUnreachable,
    AuthenticationRejected,
    Timeout,
    HostKeyMismatch,
    PrivateKeyNotFound,
    BadPassphrase,
    CredentialMissing,
    UnexpectedDisconnect,
    Other
}

/// <summary>Motivo de un fallo, ya traducido a lenguaje del usuario.</summary>
/// <remarks>
/// <see cref="TechnicalDetail"/> NO se muestra en la interfaz: se registra en el log y
/// nunca contiene credenciales ni contenido de sesión.
/// </remarks>
public sealed record SessionFailure(
    SessionFailureReason Reason,
    string UserMessage,
    string SuggestedAction,
    string? TechnicalDetail);

public sealed record SessionStateChanged(SessionState State, SessionFailure? Failure);
```

**Regla sobre `SessionFailure`**: todo fallo debe llegar a la interfaz con `UserMessage` y
`SuggestedAction` ya resueltos (FR-050). Un adaptador que no sepa clasificar un error usa
`Other`, pero igual debe producir un mensaje entendible; devolver el texto crudo de una
excepción incumple el contrato.

---

## `ISessionHandle` — base común

```csharp
public interface ISessionHandle : IAsyncDisposable
{
    Guid ConnectionId { get; }
    SessionState State { get; }
    SessionFailure? Failure { get; }

    event EventHandler<SessionStateChanged>? StateChanged;

    Task ConnectAsync(CancellationToken cancellationToken);
    Task DisconnectAsync();
}
```

**Semántica**:

- `ConnectAsync` **no** lanza excepción ante un fallo de conexión previsto: transiciona a
  `Error` con su `SessionFailure` y retorna. Las excepciones quedan reservadas para defectos
  de programación. Esto es lo que sostiene FR-054 y SC-012: un fallo de sesión no puede
  propagarse como una excepción que tumbe la aplicación.
- `StateChanged` se dispara siempre en el hilo de interfaz. El adaptador es responsable del
  despacho, no quien lo consume.
- `DisposeAsync` es idempotente y siempre libera los recursos, incluso si la conexión nunca
  se estableció.
- Reconectar es crear un handle nuevo, no reutilizar uno desechado.

---

## `IRdpSession`

```csharp
public interface IRdpSession : ISessionHandle
{
    bool IsFullScreen { get; }

    void Resize(int width, int height);
    void SetFullScreen(bool fullScreen);
}

public sealed record RdpSessionRequest(
    Guid ConnectionId,
    string Host,
    int Port,
    string Username,
    string? Domain,
    bool ClipboardEnabled,
    bool FitToTab,
    bool IgnoreCertificateWarnings,
    int TimeoutSeconds);

public interface IRdpSessionFactory
{
    IRdpSession Create(RdpSessionRequest request, ICredentialProvider credentials);
}
```

**Obligaciones del adaptador RDP**:

1. Desactivar sin excepción, en cada sesión, las redirecciones de discos, audio, micrófono,
   impresoras, puertos, cámaras y tarjetas inteligentes, y no habilitar RemoteApp ni Gateway
   (FR-017). No son configurables: no hay parámetro que las encienda.
2. Respetar `ClipboardEnabled` durante toda la vida de la sesión (FR-014).
3. Traducir los códigos de error del control a `SessionFailureReason` (FR-051). Ese mapeo es
   `RdpErrorMapper` y es una unidad con pruebas propias.
4. `Resize` solo tiene efecto cuando la conexión se creó con `FitToTab`; en caso contrario es
   una operación nula, no un error.
5. Liberar el control COM de forma explícita al desechar, sin depender del recolector de
   basura ni del comportamiento de `AxHost.Dispose()` (ver `research.md`, sección 1).

**Nota**: `IRdpSession` no expone el control visual. La obtención del control que se inserta
en la pestaña es responsabilidad de `CafManagerConection.Rdp` y ocurre por un canal
específico de la capa de interfaz, para que `UseCases` nunca manipule un `Control`.

---

## `ISshSession`

```csharp
public interface ISshSession : ISessionHandle
{
    /// <summary>Bytes recibidos del servidor, ya listos para el emulador VT.</summary>
    event EventHandler<ReadOnlyMemory<byte>>? DataReceived;

    void Send(ReadOnlySpan<byte> data);
    void Resize(int columns, int rows);
}

public sealed record SshSessionRequest(
    Guid ConnectionId,
    string Host,
    int Port,
    string Username,
    SshAuthMethod AuthMethod,
    string? PrivateKeyPath,
    string? KnownHostFingerprint,
    int KeepAliveSeconds,
    int InitialColumns,
    int InitialRows,
    int TimeoutSeconds);

public enum SshAuthMethod { Password, PrivateKey }

public interface ISshSessionFactory
{
    ISshSession Create(
        SshSessionRequest request,
        ICredentialProvider credentials,
        IHostKeyVerifier hostKeyVerifier);
}
```

**Obligaciones del adaptador SSH**:

1. Verificar la clave del host **antes** de enviar cualquier credencial (FR-022, FR-023).
2. Ante un fingerprint distinto del conocido, abortar con `HostKeyMismatch` sin autenticar.
3. `Resize` propaga el nuevo tamaño al servidor remoto (FR-033).
4. `DataReceived` se dispara desde el hilo de lectura, **no** desde el de interfaz: el
   consumidor es el emulador VT, que no toca la interfaz. El repintado se despacha después.
   Es la única excepción a la regla de despacho y está aquí para no serializar el flujo de
   datos por la cola de mensajes de la ventana.
5. Nunca registrar en el log el contenido de `DataReceived` ni de `Send` (FR-040,
   Principio II).

### `IHostKeyVerifier`

```csharp
public interface IHostKeyVerifier
{
    /// <param name="fingerprint">Formato "SHA256:&lt;base64&gt;", igual al de OpenSSH.</param>
    Task<HostKeyDecision> VerifyAsync(
        Guid connectionId,
        string host,
        string fingerprint,
        string? knownFingerprint,
        CancellationToken cancellationToken);
}

public enum HostKeyDecision { Accept, AcceptAndRemember, Reject }
```

**Semántica**: cuando `knownFingerprint` es nulo se trata de un host nuevo y se le pregunta
al usuario. Cuando difiere del presentado, la implementación **debe** devolver `Reject`
salvo que el usuario confirme el cambio de forma deliberada y explícita; nunca de forma
predeterminada.

---

## `ITerminalView` — puerto del control de terminal

```csharp
public interface ITerminalView
{
    int Columns { get; }
    int Rows { get; }

    /// <summary>Tamaño de la grilla cambiado por el usuario al redimensionar.</summary>
    event EventHandler<TerminalSize>? SizeChanged;

    /// <summary>Bytes que el usuario generó y hay que enviar al servidor.</summary>
    event EventHandler<ReadOnlyMemory<byte>>? UserInput;

    void Write(ReadOnlyMemory<byte> data);
    void ApplyTheme(TerminalTheme theme);
    void Clear();
}

public readonly record struct TerminalSize(int Columns, int Rows);
```

**Obligaciones del control de terminal**:

1. Renderizar correctamente aplicaciones de pantalla completa (FR-026), colores ANSI y de
   256 colores (FR-027), cursor (FR-028) y Unicode (FR-029).
2. Traducir teclado a las secuencias que espera el servidor, incluidas modificadoras y
   teclas de función (FR-032).
3. Selección con el mouse, copiar y pegar (FR-030), sin registrar nunca el contenido del
   portapapeles.
4. Mantener el scrollback dentro del límite configurado (FR-031) descartando lo más antiguo.
5. Emitir `SizeChanged` **solo** cuando cambia la cantidad de filas o columnas, no en cada
   píxel de redimensionamiento: cada evento provoca un mensaje de red.

El emparejamiento entre `ISshSession` y `ITerminalView` es directo y simétrico:
`DataReceived` → `Write`, y `UserInput` → `Send`, `SizeChanged` → `Resize`. Esa simetría es
deliberada: hace que el control de terminal sea reutilizable y probable sin una sesión SSH
real.
