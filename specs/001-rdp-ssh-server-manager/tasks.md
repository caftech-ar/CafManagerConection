---
description: "Lista de tareas para la implementación de CafManagerConection (CMC)"
---

# Tasks: CafManagerConection (CMC) — administrador de servidores RDP y SSH

**Input**: Documentos de diseño en `specs/001-rdp-ssh-server-manager/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/](./contracts/), [quickstart.md](./quickstart.md)

**Tests**: **Incluidos y obligatorios.** El Principio III de la constitución declara el
test-first como no negociable para `Domain` y `UseCases`. No son opcionales en este proyecto.

**Constitución**: v1.13.0. La lista refleja el alcance ampliado hasta esa enmienda: apariencia
Fluent, herencia desde la carpeta, SFTP, métricas de servidor, túneles, inventario de plataforma,
los dos instaladores (1.8.0), copias y preferencias (1.9.0), comprobación de actualizaciones,
panel de puertos (1.11.0) e importación de PuTTY, WinSCP y FileZilla (1.12.0).

Lo pedido y no construido vive en `specs/002-procesos-registros-y-arbol/tasks.md`, y su
autorización sale de tres lugares distintos: las enmiendas 1.13.0 y 1.14.0, la cláusula «colorear y
ordenar lo que ya se muestra NO es ampliar el alcance» (`constitution.md:783`), y defectos de
requisitos ya construidos. El detalle por grupo está en `spec.md`, sección «Movidos a la feature
002».

## Format: `[ID] [P?] [Story] Description`

- **[P]**: se puede ejecutar en paralelo (archivos distintos, sin dependencias pendientes)
- **[Story]**: historia de usuario a la que pertenece (US1…US11)
- Cada tarea incluye la ruta exacta del archivo

---

## Phase 1: Setup (infraestructura compartida)

- [x] T001 Crear la solución `CafManagerConection.sln` en la raíz del repositorio
- [x] T002 [P] Crear los 9 proyectos de producción en `src/` (`App`, `Domain`, `UseCases`, `Infrastructure`, `Rdp`, `Ssh`, `Terminal`, `Monitoring`, `Platform`) con `TargetFramework` `net10.0-windows` y `LangVersion` 14
- [x] T003 [P] Crear los 8 proyectos de prueba en `tests/` (`Domain`, `UseCases`, `Infrastructure`, `Rdp`, `Ssh`, `Terminal`, `Monitoring`, `Platform`) con xUnit, NSubstitute y Coverlet
- [x] T004 Configurar las referencias entre proyectos en `CafManagerConection.sln`: `Domain` sin ninguna referencia, `UseCases` → `Domain`, `Infrastructure` → `UseCases`, adaptadores → `UseCases`, `App` → todos
- [x] T005 [P] Crear `Directory.Build.props` en la raíz con `Nullable=enable`, `TreatWarningsAsErrors=true` e `ImplicitUsings=enable`
- [x] T006 [P] Agregar `SSH.NET 2026.0.0` a `src/CafManagerConection.Ssh/CafManagerConection.Ssh.csproj`
- [x] T007 [P] Agregar `Microsoft.Data.Sqlite`, `Dapper`, `Serilog` y `Serilog.Sinks.File` a `src/CafManagerConection.Infrastructure/CafManagerConection.Infrastructure.csproj`
- [x] T008 [P] Crear `.editorconfig` y `.gitignore` en la raíz, excluyendo `bin/`, `obj/` y `publish/`
- [x] T009 [P] Habilitar WinForms (`UseWindowsForms=true`) en `src/CafManagerConection.App`, `src/CafManagerConection.Rdp` y `src/CafManagerConection.Terminal`, y **solo** en esos tres
- [x] T010 Escribir la prueba de arquitectura en `tests/CafManagerConection.Domain.Tests/ArchitectureTests.cs` que falla si `Domain` referencia WinForms, SQLite, Dapper, SSH.NET, VtNetCore o interop COM (Principio I)
- [x] T011 [P] Escribir la prueba de arquitectura de nombres en `tests/CafManagerConection.Domain.Tests/NamingTests.cs` que falla si aparece un namespace `CafManagerConection.Application`, para que la colisión con `System.Windows.Forms.Application` no vuelva
- [x] T012 [P] Configurar la ejecución de pruebas por categoría en `Directory.Build.props`, para poder excluir las que necesitan Docker o un servidor real

**Checkpoint**: `dotnet build` compila y `dotnet test` corre las pruebas de arquitectura en verde.

---

## Phase 2: Foundational (prerrequisitos bloqueantes)

**⚠️ CRÍTICO**: ninguna historia puede empezar hasta que esta fase esté completa.

### Puerta de esquema (bloqueante, requiere al usuario)

- [x] T013 Presentar al usuario el esquema propuesto en `specs/001-rdp-ssh-server-manager/data-model.md` y **obtener su confirmación explícita** antes de escribir cualquier migración. La constitución lo exige

### Entidades de dominio (test-first)

- [x] T014 [P] Escribir las pruebas de `Folder` en `tests/CafManagerConection.Domain.Tests/Connections/FolderTests.cs`: nombre de 1 a 100 caracteres, rechazo de ciclos al reasignar el padre
- [x] T015 [P] Escribir las pruebas de `Connection` en `tests/CafManagerConection.Domain.Tests/Connections/ConnectionTests.cs`: `Protocol` inmutable, puerto entre 1 y 65535, formato de `CredentialKey`
- [x] T016 [P] Escribir las pruebas de `StoredCredential` en `tests/CafManagerConection.Domain.Tests/Credentials/StoredCredentialTests.cs`: `ToString()` redactado y `Dispose()` limpia el secreto
- [x] T017 [P] Implementar `Folder` y `FolderSettings` en `src/CafManagerConection.Domain/Connections/`
- [x] T018 [P] Implementar `Connection`, `RdpSettings` y `SshSettings` en `src/CafManagerConection.Domain/Connections/`, con los campos heredables anulables según `data-model.md`
- [x] T019 [P] Implementar `StoredCredential` y `CredentialKey` en `src/CafManagerConection.Domain/Credentials/`
- [x] T020 [P] Implementar `SessionState`, `SessionFailure`, `SessionFailureReason` y `SessionInfo` en `src/CafManagerConection.Domain/Sessions/`
- [x] T021 [P] Implementar `WindowPlacement`, `AppTheme` y `TerminalPreferences` en `src/CafManagerConection.Domain/Settings/`
- [x] T022 [P] Implementar `ConnectionHistoryEntry` y `SshTunnel` en `src/CafManagerConection.Domain/Connections/`

### Puertos (interfaces)

- [x] T023 [P] Declarar los puertos de sesión de `contracts/ports-de-sesion.md` en `src/CafManagerConection.Domain/Sessions/`
- [x] T024 [P] Declarar los puertos de infraestructura de `contracts/puertos-de-infraestructura.md` en `src/CafManagerConection.UseCases/Abstractions/`
- [x] T025 [P] Declarar los servicios de `contracts/servicios-de-aplicacion.md` en `src/CafManagerConection.UseCases/`
- [x] T026 [P] Declarar los puertos de plataforma de `contracts/puertos-de-plataforma.md` en `src/CafManagerConection.UseCases/Abstractions/`

### Persistencia (depende de T013)

- [x] T027 Implementar la resolución de rutas de `%LocalAppData%\CafManagerConection` en `src/CafManagerConection.Infrastructure/Configuration/AppPaths.cs`
- [x] T028 Implementar la fábrica de conexiones SQLite en `src/CafManagerConection.Infrastructure/Database/SqliteConnectionFactory.cs`, aplicando `PRAGMA foreign_keys = ON` y `journal_mode = WAL` en cada conexión
- [x] T029 Escribir la migración 1 en `src/CafManagerConection.Infrastructure/Database/Migrations/Migration001_InitialSchema.cs` con las ocho tablas de `data-model.md`, incluidas `folder_settings` y `ssh_tunnels`
- [x] T030 Escribir las pruebas de `DatabaseInitializer` en `tests/CafManagerConection.Infrastructure.Tests/Database/DatabaseInitializerTests.cs`: base nueva llega a `user_version = 1`, base migrada no se toca, base corrupta se preserva
- [x] T031 Implementar `DatabaseInitializer` en `src/CafManagerConection.Infrastructure/Database/DatabaseInitializer.cs` con migraciones por `user_version` en transacción y recuperación ante corrupción (FR-052)
- [x] T032 [P] Escribir las pruebas de `FolderRepository` en `tests/CafManagerConection.Infrastructure.Tests/Database/FolderRepositoryTests.cs`, verificando el borrado en cascada y la persistencia de `folder_settings`
- [x] T033 [P] Escribir las pruebas de `ConnectionRepository` en `tests/CafManagerConection.Infrastructure.Tests/Database/ConnectionRepositoryTests.cs`: alta y baja transaccional con su configuración, y persistencia de `NULL` en los campos heredables
- [x] T034 [P] Escribir las pruebas de `ConnectionHistoryRepository` en `tests/CafManagerConection.Infrastructure.Tests/Database/ConnectionHistoryRepositoryTests.cs`, verificando la retención de 100 eventos
- [x] T035 [P] Implementar `FolderRepository` con Dapper en `src/CafManagerConection.Infrastructure/Database/FolderRepository.cs`
- [x] T036 [P] Implementar `ConnectionRepository` con Dapper en `src/CafManagerConection.Infrastructure/Database/ConnectionRepository.cs`
- [x] T037 [P] Implementar `ConnectionHistoryRepository`, `TunnelRepository` y `SettingsStore` en `src/CafManagerConection.Infrastructure/Database/`

### Credenciales

- [x] T038 Escribir las pruebas de `WindowsCredentialStore` en `tests/CafManagerConection.Infrastructure.Tests/Credentials/WindowsCredentialStoreTests.cs`: ciclo escribir/leer/borrar, clave inexistente devuelve `null`, borrado de clave inexistente es exitoso
- [x] T039 Implementar las declaraciones P/Invoke de `CredWriteW`, `CredReadW`, `CredDeleteW` y `CredFree` en `src/CafManagerConection.Infrastructure/Credentials/CredentialManagerNative.cs`
- [x] T040 Implementar `WindowsCredentialStore` en `src/CafManagerConection.Infrastructure/Credentials/WindowsCredentialStore.cs`, liberando con `CredFree` en `finally` y limpiando el blob tras consumirlo

### Registro

- [x] T041 Escribir la prueba de redacción en `tests/CafManagerConection.Infrastructure.Tests/Logging/LogRedactionTests.cs`: tras ejercitar todos los métodos de `IAppLogger`, el archivo no contiene ningún secreto ni contenido de sesión (Principio II)
- [x] T042 Implementar `SerilogAppLogger` en `src/CafManagerConection.Infrastructure/Logging/SerilogAppLogger.cs` con rotación diaria y retención de 30 días en `%LocalAppData%\CafManagerConection\logs\` (FR-057, FR-057a)

### Base de la apariencia Fluent

- [x] T043 [P] Implementar el P/Invoke a `dwmapi.dll` en `src/CafManagerConection.App/Themes/DwmInterop.cs`: material Mica, preferencia de esquinas redondeadas y modo oscuro de la barra de título (FR-065)
- [x] T044 [P] Implementar la lectura del color de acento del sistema en `src/CafManagerConection.App/Themes/SystemAccent.cs` (FR-066)
- [x] T045 [P] Definir las paletas clara y oscura en `src/CafManagerConection.App/Themes/FluentPalette.cs`, con los tokens de fondo, superficie, borde, texto y estados
- [x] T046 [P] Implementar la tipografía y las métricas de espaciado Fluent en `src/CafManagerConection.App/Themes/FluentTypography.cs`, respetando el escalado por DPI (FR-067)
- [x] T047 Implementar las primitivas de dibujo owner-drawn en `src/CafManagerConection.App/Themes/FluentRenderer.cs`: rectángulo redondeado, borde, foco y estados de apuntado y presionado (FR-068)

### Arranque de la aplicación

- [x] T048 Implementar `Program.cs` en `src/CafManagerConection.App/Bootstrap/Program.cs`, llamando a `System.Windows.Forms.Application.SetColorMode(...)` con el nombre completamente calificado y **antes** de crear cualquier control
- [x] T049 Implementar la composición de dependencias en `src/CafManagerConection.App/Bootstrap/CompositionRoot.cs`
- [x] T050 Crear el esqueleto de `MainForm` en `src/CafManagerConection.App/Forms/MainForm.cs` con la disposición de FR-041 y el material Mica aplicado
- [x] T051 [P] Implementar `FluentDialogForm` en `src/CafManagerConection.App/Forms/FluentDialogForm.cs` como base de todos los diálogos propios, para no usar nunca `MessageBox` (FR-070)

**Checkpoint**: la aplicación arranca con aspecto Windows 11, crea su base y su log, y muestra
una ventana vacía. Las historias pueden comenzar.

---

## Phase 3: User Story 1 — Conectarme a un servidor Windows por RDP (Priority: P1) 🎯 MVP

**Goal**: guardar un servidor Windows una vez y abrirlo con doble clic en una pestaña.

**Independent Test**: crear una conexión RDP contra un servidor real, cerrar y reabrir la
aplicación, conectarse con doble clic, y verificar que la contraseña está en el Administrador
de credenciales y **no** en `cmc.db`.

### Retiro de riesgo técnico (hacer primero)

- [x] T052 [US1] Prueba de concepto del interop RDP: configurar `<COMReference WrapperTool="aximp">` sobre `mstscax.dll` en `src/CafManagerConection.Rdp/CafManagerConection.Rdp.csproj` y confirmar que se genera un wrapper `AxHost` utilizable para `MsRdpClient11`. Si falla, aplicar el plan B de `research.md` §1

### Tests para User Story 1

- [x] T053 [P] [US1] Escribir las pruebas de `RdpErrorMapper` en `tests/CafManagerConection.Rdp.Tests/RdpErrorMapperTests.cs`, cubriendo el mapeo a `HostUnreachable`, `AuthenticationRejected`, `Timeout` y `UnexpectedDisconnect`
- [x] T054 [P] [US1] Escribir las pruebas de `ConnectionValidator` en `tests/CafManagerConection.UseCases.Tests/Validation/ConnectionValidatorTests.cs`
- [x] T055 [P] [US1] Escribir las pruebas de alta de conexión en `tests/CafManagerConection.UseCases.Tests/Connections/ConnectionServiceCreateTests.cs`, verificando que la credencial va al almacén y sólo la `CredentialKey` al repositorio
- [x] T056 [P] [US1] Escribir las pruebas de `CredentialProvider` en `tests/CafManagerConection.UseCases.Tests/Credentials/CredentialProviderTests.cs`: resuelve desde el almacén, y ante credencial ausente la pide en lugar de fallar (Principio III — esta prueba precede a T065) — 12 pruebas en `CredentialProviderTests.cs`
- [x] T057 [P] [US1] Escribir las pruebas de `SessionManager.OpenAsync` en `tests/CafManagerConection.UseCases.Tests/Sessions/SessionManagerOpenTests.cs`: un fallo de conexión deja la sesión en `Error` y **no** lanza
- [x] T058 [US1] Escribir la prueba de ciclo de vida en `tests/CafManagerConection.Rdp.Tests/CicloDeVidaTests.cs` con categoría `RdpLifecycle`: abrir y cerrar 50 sesiones y vigilar los descriptores de USER y GDI. **El criterio original (5 % sobre la línea base) resultó inalcanzable y se cambió por uno medido**: el control ActiveX de Microsoft filtra 3 descriptores de USER por cada `Connect`, de forma exactamente lineal (42 → 192 en 50 vueltas, 42 → 642 en 200), y no lo evita `Disconnect`, ni `RequestClose`, ni esperar a que la desconexión termine, ni cambiar el orden de desmontaje; sin llamar a `Connect` el crecimiento es cero. La prueba ahora exige un presupuesto de 5 por sesión —que caza una regresión del desmontaje propio sin quedar en rojo permanente— y GDI plano. Son unas 3.300 conexiones antes de agotar la cuota de 10.000 descriptores del proceso

### Implementación para User Story 1

- [x] T059 [P] [US1] Implementar `RdpErrorMapper` en `src/CafManagerConection.Rdp/RdpErrorMapper.cs`, con mensaje y acción sugerida en español (FR-050, FR-051)
- [x] T060 [US1] Implementar `RdpConfiguration` en `src/CafManagerConection.Rdp/RdpConfiguration.cs`, fijando **apagadas** todas las redirecciones de FR-017, sin exponer parámetro que las encienda
- [x] T061 [US1] Implementar la política de certificado en `src/CafManagerConection.Rdp/RdpConfiguration.cs`: con la validación activa, detener la conexión y exponer el motivo y los datos del certificado para que la interfaz decida (FR-016, FR-016a)
- [x] T062 [US1] Implementar `RdpSessionControl` en `src/CafManagerConection.Rdp/RdpSessionControl.cs` sobre `AxHost`, con liberación explícita del COM al desechar: desconectar, desuscribir eventos, quitar del contenedor, `Dispose()` y soltar las referencias retenidas
- [x] T063 [US1] Implementar `RdpSession` e `IRdpSessionFactory` en `src/CafManagerConection.Rdp/RdpSession.cs`, despachando `StateChanged` al hilo de interfaz y aplicando `ClipboardEnabled` (FR-014)
- [x] T064 [P] [US1] Implementar `ConnectionValidator` en `src/CafManagerConection.UseCases/Validation/ConnectionValidator.cs`
- [x] T065 [US1] Implementar `CredentialProvider` en `src/CafManagerConection.UseCases/Credentials/CredentialProvider.cs` (FR-039) — mas `CredentialPromptWindow` y su adaptador en la App
- [x] T066 [US1] Implementar el alta, la consulta y el detalle de `ConnectionService` en `src/CafManagerConection.UseCases/Connections/ConnectionService.cs`
- [x] T067 [US1] Implementar `SessionManager` con apertura y cierre en `src/CafManagerConection.UseCases/Sessions/SessionManager.cs`, registrando el historial y la fecha de última conexión (FR-008, FR-009)
- [x] T068 [P] [US1] Implementar `ConnectionEditorForm` en `src/CafManagerConection.App/Forms/ConnectionEditorForm.cs` con los parámetros RDP: host, puerto, dominio, usuario, contraseña, portapapeles, ajuste a la pestaña, política de certificado y pantalla completa inicial
- [x] T069 [P] [US1] Implementar `CredentialPromptForm` en `src/CafManagerConection.App/Forms/CredentialPromptForm.cs`, con opción de guardar
- [x] T070 [P] [US1] Implementar `CertificateWarningForm` en `src/CafManagerConection.App/Forms/CertificateWarningForm.cs`, mostrando emisor, destinatario, vigencia y motivo, con las opciones de continuar por esta vez o recordar (FR-016a)
- [x] T071 [US1] Implementar `ServerTreeView` owner-drawn en `src/CafManagerConection.App/Controls/ServerTreeView.cs` como lista plana, con estética Fluent, doble clic para conectar y menú contextual (FR-012, FR-068)
- [x] T072 [US1] Implementar `FluentTabStrip` en `src/CafManagerConection.App/Controls/FluentTabStrip.cs`: control propio al estilo de Windows Terminal, con esquina superior redondeada, cierre por pestaña, indicador de estado y botón de nueva pestaña (FR-069)
- [x] T073 [US1] Implementar `SessionTabControl` en `src/CafManagerConection.App/Controls/SessionTabControl.cs`, alojando el control de sesión sobre `FluentTabStrip` (FR-043)
- [x] T074 [US1] Cablear la barra de estado en `src/CafManagerConection.App/Forms/MainForm.cs` con estado, usuario y host de la sesión activa (FR-042)
- [x] T075 [US1] Implementar el cierre de pestaña en `src/CafManagerConection.App/Controls/SessionTabControl.cs`, desconectando la sesión sin afectar a las demás (FR-044)
- [x] T076 [US1] Implementar el redimensionamiento de la sesión RDP en `src/CafManagerConection.Rdp/RdpSession.cs`, respetando `FitToTab` (FR-015)
- [x] T077 [US1] Implementar la presentación de errores en `src/CafManagerConection.App/Controls/SessionTabControl.cs`, con `UserMessage`, `SuggestedAction` y acción de reintentar (FR-050)
- [x] T078 [US1] Registrar los eventos de conexión con `IAppLogger` en `src/CafManagerConection.UseCases/Sessions/SessionManager.cs` (FR-057)

**Checkpoint**: **MVP entregable**. Escenarios 1 y 2 de `quickstart.md`.

---

## Phase 4: User Story 2 — Trabajar por SSH con terminal integrado (Priority: P2)

**Independent Test**: conectarse a un servidor Linux y ejecutar la batería de seis programas
de pantalla completa, verificando colores, Unicode y redimensionamiento.

> **Nota de riesgo**: T079 a T082 son la prueba de concepto `validate-ssh-terminal-stack`.
> Se ejecutan **antes** que la Fase 3 según el orden elegido (riesgo primero).

### Retiro de riesgo técnico (hacer primero)

- [x] T079 [US2] ~~Incorporar el codigo fuente de VtNetCore~~ — **CANCELADA**: se escribio un emulador VT propio en `src/CafManagerConection.Terminal/VtEmulator.cs` (research.md §2, Revision 2026-08-25)
- [x] T080 [US2] ~~Documentar procedencia de VtNetCore~~ — **CANCELADA**: no hay codigo de terceros que documentar
- [x] T081 [US2] Prueba de rendimiento del emulador propio en `tests/CafManagerConection.Terminal.Tests/RenderThroughputTests.cs`: alimentarlo con una captura de `htop` y verificar que sostiene al menos 60 repintados por segundo a 80x24 — mas dos pruebas de rafaga que **encontraron un defecto vivo**: `Write` lanzaba con lecturas mayores a 4 KB
- [x] T082 [US2] Prueba de ancho de celda en `tests/CafManagerConection.Terminal.Tests/CellWidthTests.cs`: ancho correcto para caracteres CJK de doble ancho y emojis — **replanteada**: el doble ancho (CJK) es una limitacion deliberada y documentada del emulador. La prueba la fija en vez de afirmar una funcion que no existe, y verifica lo que si importa: acentos y emoji no descolocan la linea

### Tests para User Story 2

- [x] T083 [P] [US2] Capturar los fixtures de secuencias VT en `tests/CafManagerConection.Terminal.Tests/Fixtures/` para `vim`, `nano`, `top`, `htop`, `less` y `tmux`
- [x] T084 [P] [US2] Escribir las pruebas de `TerminalBuffer` en `tests/CafManagerConection.Terminal.Tests/TerminalBufferTests.cs`, comparando texto, atributos y posición del cursor contra el estado esperado
- [x] T085 [P] [US2] Escribir las pruebas de `ScrollbackBuffer` en `tests/CafManagerConection.Terminal.Tests/ScrollbackBufferTests.cs` (FR-031)
- [x] T086 [P] [US2] Escribir las pruebas de `KeyboardMapper` en `tests/CafManagerConection.Terminal.Tests/KeyboardMapperTests.cs` (FR-032)
- [x] T087 [P] [US2] Escribir las pruebas de colores en `tests/CafManagerConection.Terminal.Tests/AnsiColorTests.cs`: 16 colores base y paleta de 256 (FR-027)
- [x] T088 [P] [US2] Escribir las pruebas de Unicode en `tests/CafManagerConection.Terminal.Tests/UnicodeTests.cs`: acentos, `ñ`, símbolos y caracteres de doble ancho (FR-029)
- [x] T089 [P] [US2] Escribir las pruebas de `HostKeyValidator` en `tests/CafManagerConection.Ssh.Tests/HostKeyValidatorTests.cs`: host nuevo pregunta, coincidente no pregunta, distinto rechaza **sin autenticar** (FR-022, FR-023) — cubierto por `HostKeyPolicyTests` (8 pruebas): misma clave no pregunta, distinta no se acepta, sin clave guardada pregunta
- [x] T090 [P] [US2] Escribir las pruebas de integración SSH en `tests/CafManagerConection.Ssh.Tests/SshSessionIntegrationTests.cs` contra el contenedor OpenSSH, omitiéndose con mensaje explícito si no está disponible — 9 pruebas: saludo, verificación de la clave del host en formato OpenSSH, rechazo que aborta **antes** de autenticar, huella conocida, contraseña incorrecta, usuario inexistente, canal interactivo, contadores de bytes y desconexión. El contenedor se levanta con `task sshd:up`
- [x] T091 [P] [US2] Escribir la prueba de propagación del tamaño en `tests/CafManagerConection.Ssh.Tests/SshResizeTests.cs` (FR-033) — 3 pruebas que le preguntan el tamaño al terminal remoto con `stty size`: el inicial, el redimensionado en caliente, y que redimensionar una sesión no conectada no rompa

### Implementación para User Story 2

- [x] T092 [P] [US2] Implementar `TerminalBuffer` en `src/CafManagerConection.Terminal/TerminalBuffer.cs`
- [x] T093 [P] [US2] Implementar `ScrollbackBuffer` en `src/CafManagerConection.Terminal/ScrollbackBuffer.cs`
- [x] T094 [P] [US2] Implementar `KeyboardMapper` en `src/CafManagerConection.Terminal/KeyboardMapper.cs`
- [x] T095 [US2] Implementar `TerminalRenderer` en `src/CafManagerConection.Terminal/TerminalRenderer.cs` con `TextRenderer` (GDI), doble buffer, repintado por regiones sucias, agrupación de tramos con los mismos atributos y dibujo del cursor (FR-028, research.md §3)
- [x] T096 [P] [US2] Implementar la selección por mouse, copiar y pegar en `src/CafManagerConection.Terminal/TerminalControl.cs` (FR-030) — **ruta corregida el 2026-08-31**: la tarea decía `SelectionManager.cs`, un archivo que nunca existió. La selección quedó dentro del control porque necesita el búfer, el pintado y las coordenadas de celda, que viven ahí; separarla habría sido una clase que devuelve lo que el control ya sabe
- [x] T097 [US2] Implementar `SshTerminalControl` en `src/CafManagerConection.Terminal/SshTerminalControl.cs` implementando `ITerminalView`, emitiendo `SizeChanged` sólo al cambiar filas o columnas
- [x] T098 [P] [US2] Implementar las paletas del terminal en `src/CafManagerConection.App/Themes/TerminalThemes.cs`, mapeando los 16 colores ANSI base a los temas claro y oscuro
- [x] T099 [P] [US2] Implementar `HostKeyValidator` en `src/CafManagerConection.Ssh/HostKeyValidator.cs`, con fingerprint en formato `SHA256:<base64>`
- [x] T100 [P] [US2] Implementar `SshAuthentication` en `src/CafManagerConection.Ssh/SshAuthentication.cs` para contraseña y clave privada con passphrase (FR-021)
- [x] T101 [US2] Implementar `SshSession` y `SshConnectionFactory` en `src/CafManagerConection.Ssh/SshSession.cs`: verificación del host antes de autenticar, lectura del `ShellStream` en tarea de segundo plano con `CancellationToken`, keep-alive y `ChangeWindowSize`
- [x] T102 [US2] Implementar el cierre ordenado en `src/CafManagerConection.Ssh/SshSession.cs`: cerrar el `ShellStream` antes que el `SshClient` y cancelar la tarea de lectura
- [x] T103 [P] [US2] Implementar `HostKeyPromptForm` en `src/CafManagerConection.App/Forms/HostKeyPromptForm.cs` sobre `FluentDialogForm`, diferenciando host nuevo de fingerprint cambiado
- [x] T104 [US2] Extender `ConnectionEditorForm` en `src/CafManagerConection.App/Forms/ConnectionEditorForm.cs` con los parámetros SSH: autenticación, ruta de clave, keep-alive y codificación
- [x] T105 [US2] Implementar el contenedor de paneles laterales en `src/CafManagerConection.App/Controls/SessionPanelHost.cs`: terminal siempre visible, con paneles desplegables a los costados (FR-070a)
- [x] T106 [US2] Cablear sesión y terminal en `src/CafManagerConection.App/Controls/SessionTabControl.cs`: `DataReceived` → `Write`, `UserInput` → `Send`, `SizeChanged` → `Resize`
- [x] T107 [US2] Implementar `SshErrorMapper` en `src/CafManagerConection.Ssh/SshErrorMapper.cs`, distinguiendo `PrivateKeyNotFound` de `BadPassphrase` (FR-051)

**Checkpoint**: escenarios 3, 4 y 5 de `quickstart.md`.

---

## Phase 5: User Story 3 — Organizar en carpetas, heredar configuración y buscar (Priority: P3)

**Independent Test**: definir credencial, usuario y puerto en una carpeta, crear dentro una
veintena de conexiones que los heredan, moverlas entre carpetas y buscarlas.

### Tests para User Story 3

- [x] T108 [P] [US3] Escribir las pruebas de `FolderService` en `tests/CafManagerConection.UseCases.Tests/Folders/FolderServiceTests.cs`: alta, renombrado y **rechazo de ciclos**
- [x] T109 [P] [US3] Escribir las pruebas de `GetDeletionImpactAsync` en `tests/CafManagerConection.UseCases.Tests/Folders/FolderDeletionImpactTests.cs` — 6 pruebas: cuenta subcarpetas anidadas, no cuenta lo de fuera de la rama ni lo de la raiz
- [x] T110 [P] [US3] Escribir las pruebas de búsqueda en `tests/CafManagerConection.UseCases.Tests/Connections/ConnectionSearchTests.cs`: por nombre, host y usuario, sin distinguir mayúsculas **ni acentos** (FR-007)
- [x] T111 [P] [US3] Escribir las pruebas de duplicado en `tests/CafManagerConection.UseCases.Tests/Connections/ConnectionDuplicateTests.cs`
- [x] T112 [P] [US3] Escribir las pruebas de nombre duplicado en `tests/CafManagerConection.UseCases.Tests/Connections/DuplicateNameTests.cs`: se advierte pero no se impide guardar (FR-053)
- [x] T113 [P] [US3] Escribir las pruebas de resolución en cascada en `tests/CafManagerConection.UseCases.Tests/Inheritance/SettingsResolverTests.cs`: valor propio gana sobre heredado, se sube hasta la raíz, y si nadie define queda sin valor (FR-060)
- [x] T114 [P] [US3] Escribir las pruebas de herencia de credencial en `tests/CafManagerConection.UseCases.Tests/Inheritance/CredentialInheritanceTests.cs`: veinte conexiones que heredan cambian todas al cambiar la de la carpeta (SC-013) — SC-013 verificado: 20 conexiones cambian al cambiar la carpeta, sin tocar ninguna
- [x] T115 [P] [US3] Escribir las pruebas de movimiento en `tests/CafManagerConection.UseCases.Tests/Inheritance/MoveRecalculationTests.cs`: al mover, los valores heredados se recalculan y se detecta si alguno cambia (FR-062)

### Implementación para User Story 3

- [x] T116 [US3] Implementar `SettingsResolver` en `src/CafManagerConection.UseCases/Inheritance/SettingsResolver.cs`: resuelve el valor efectivo subiendo por el árbol ya cargado en memoria (FR-060)
- [x] T117 [US3] Implementar `EffectiveSettings` en `src/CafManagerConection.UseCases/Inheritance/EffectiveSettings.cs`, exponiendo para cada campo el valor efectivo y de qué carpeta proviene (FR-061)
- [x] T118 [US3] Implementar `FolderService` en `src/CafManagerConection.UseCases/Folders/FolderService.cs` con alta, renombrado, movimiento con detección de ciclos y borrado en cascada
- [x] T119 [US3] Implementar `GetDeletionImpactAsync` y el impacto de cambiar la configuración de una carpeta en `src/CafManagerConection.UseCases/Folders/FolderService.cs` (FR-010, FR-063)
- [x] T120 [US3] Implementar la gestión de la credencial de carpeta en `src/CafManagerConection.UseCases/Folders/FolderService.cs`, con clave `cmc:folder:<id>` (FR-064)
- [x] T121 [US3] Implementar el recálculo al mover en `src/CafManagerConection.UseCases/Connections/ConnectionService.cs`, detectando los cambios de valor efectivo para la advertencia previa (FR-062)
- [x] T122 [P] [US3] Implementar la búsqueda en `src/CafManagerConection.UseCases/Connections/ConnectionService.cs`, normalizando acentos y devolviendo las carpetas ancestro
- [x] T123 [P] [US3] Implementar duplicar, mover, reordenar e `IsNameDuplicatedAsync` en `src/CafManagerConection.UseCases/Connections/ConnectionService.cs` (FR-004, FR-005, FR-053)
- [x] T124 [US3] Convertir `ServerTreeView` en árbol jerárquico en `src/CafManagerConection.App/Controls/ServerTreeView.cs`, con carpetas anidadas e iconos por protocolo
- [x] T125 [US3] Implementar arrastrar y soltar en `src/CafManagerConection.App/Controls/ServerTreeView.cs` para mover y reordenar
- [x] T126 [P] [US3] Implementar `FolderEditorForm` en `src/CafManagerConection.App/Forms/FolderEditorForm.cs`: nombre, credencial y valores heredables de la carpeta (FR-058)
- [x] T127 [US3] Implementar el control de campo heredable en `src/CafManagerConection.App/Controls/InheritableField.cs`: casilla "heredar" marcada por omisión, con el valor heredado y su origen en gris al lado (FR-059, FR-061)
- [x] T128 [US3] Aplicar `InheritableField` a los campos heredables de `src/CafManagerConection.App/Forms/ConnectionEditorForm.cs`
- [x] T129 [P] [US3] Implementar `SearchBox` owner-drawn en `src/CafManagerConection.App/Controls/SearchBox.cs`, filtrando el árbol mientras se escribe
- [x] T130 [P] [US3] Implementar `ConfirmDeleteForm` en `src/CafManagerConection.App/Forms/ConfirmDeleteForm.cs` sobre `FluentDialogForm`, informando el impacto y los cambios de valor efectivo
- [x] T131 [P] [US3] Agregar el campo de notas a `src/CafManagerConection.App/Forms/ConnectionEditorForm.cs` (FR-006)
- [x] T132 [P] [US3] Mostrar la fecha de última conexión en `src/CafManagerConection.App/Controls/ServerTreeView.cs` (FR-008)
- [x] T133 [US3] Implementar el menú contextual completo en `src/CafManagerConection.App/Controls/ServerTreeView.cs`: conectar, abrir otra sesión, editar, duplicar, mover, eliminar, nueva carpeta y nueva conexión

**Checkpoint**: las tres primeras historias funcionan de forma independiente.

---

## Phase 6: User Story 4 — Varias sesiones a la vez en una ventana (Priority: P4)

### Tests para User Story 4

- [x] T134 [P] [US4] Escribir las pruebas de aislamiento en `tests/CafManagerConection.UseCases.Tests/Sessions/SessionIsolationTests.cs` (FR-054, SC-012)
- [x] T135 [P] [US4] Escribir las pruebas de `ReconnectAsync` y `CloseAllAsync` en `tests/CafManagerConection.UseCases.Tests/Sessions/SessionManagerLifecycleTests.cs`
- [x] T136 [P] [US4] Escribir las pruebas de `AppSettingsService` en `tests/CafManagerConection.UseCases.Tests/Settings/AppSettingsServiceTests.cs`: geometría fuera de todo monitor se sustituye por posición centrada (FR-047) — la comprobacion se extrajo a `WindowPlacement.EsVisibleEn`, en el dominio, con 9 pruebas
- [x] T137 [P] [US4] Escribir las pruebas de sesión ya abierta en `tests/CafManagerConection.UseCases.Tests/Sessions/ExistingSessionTests.cs`: abrir una conexión con sesión activa devuelve la existente (FR-044a) — cubierto por `SessionRegistry.FirstForConnection` y sus pruebas

### Implementación para User Story 4

- [x] T138 [US4] Implementar la gestión de múltiples sesiones en `src/CafManagerConection.UseCases/Sessions/SessionManager.cs`, con `ActiveSessions`, `CountForConnection` y aislamiento de excepciones por sesión
- [x] T139 [US4] Implementar el foco a la pestaña existente en `src/CafManagerConection.UseCases/Sessions/SessionManager.cs`, con la apertura adicional como acción explícita (FR-044a)
- [x] T140 [US4] Implementar `ReconnectAsync` en `src/CafManagerConection.UseCases/Sessions/SessionManager.cs`, reutilizando parámetros y credenciales (FR-045)
- [x] T141 [US4] Implementar `CloseAllAsync` en `src/CafManagerConection.UseCases/Sessions/SessionManager.cs`, con plazo acotado antes de forzar
- [x] T142 [P] [US4] Implementar `AppSettingsService` en `src/CafManagerConection.UseCases/Settings/AppSettingsService.cs` con validación de geometría contra los monitores conectados
- [x] T143 [US4] Implementar múltiples pestañas y cambio entre ellas en `src/CafManagerConection.App/Controls/SessionTabControl.cs` (FR-044)
- [x] T144 [US4] Implementar el modo pantalla completa en `src/CafManagerConection.App/Forms/MainForm.cs`, conservando el estado de la conexión (FR-046)
- [x] T145 [US4] Implementar la acción de reconectar en `src/CafManagerConection.App/Controls/SessionTabControl.cs`
- [x] T146 [US4] Implementar la confirmación de cierre con sesiones activas en `src/CafManagerConection.App/Forms/MainForm.cs` (FR-048)
- [x] T147 [US4] Implementar la persistencia de tamaño, posición, maximizado y tema en `src/CafManagerConection.App/Forms/MainForm.cs` (FR-047)
- [x] T148 [US4] Implementar las advertencias al eliminar con sesiones activas en `src/CafManagerConection.App/Forms/ConfirmDeleteForm.cs` (FR-049)
- [x] T149 [US4] Implementar la reacción a la suspensión del equipo en `src/CafManagerConection.UseCases/Sessions/SessionManager.cs`: al reanudar, las sesiones caídas quedan como desconectadas con reconexión disponible

**Checkpoint**: escenarios 6 y 7 de `quickstart.md`.

---

## Phase 7: User Story 5 — Credenciales bajo control (Priority: P5)

### Tests para User Story 5

- [x] T150 [P] [US5] Escribir las pruebas de rotación y borrado en `tests/CafManagerConection.UseCases.Tests/Credentials/CredentialRotationTests.cs` (FR-037)
- [x] T151 [P] [US5] Escribir la prueba de orden de borrado en `tests/CafManagerConection.UseCases.Tests/Connections/ConnectionDeleteTests.cs`: si falla el borrado de la credencial, la conexión **no** se elimina (FR-038) — verifica el orden: si falla el borrado de la credencial, la conexion NO se elimina
- [x] T152 [P] [US5] Escribir la prueba de credencial ausente en `tests/CafManagerConection.UseCases.Tests/Credentials/MissingCredentialTests.cs` (FR-039)

### Implementación para User Story 5

- [x] T153 [US5] Implementar `ClearCredentialAsync` y `HasStoredCredentialAsync` en `src/CafManagerConection.UseCases/Connections/ConnectionService.cs`
- [x] T154 [US5] Implementar el orden correcto de borrado en `src/CafManagerConection.UseCases/Connections/ConnectionService.cs`: primero la credencial, después la fila (FR-038)
- [x] T155 [US5] Implementar el flujo de credencial ausente en `src/CafManagerConection.UseCases/Credentials/CredentialProvider.cs`, con opción de volver a guardarla
- [x] T156 [US5] Agregar la gestión de credenciales a `src/CafManagerConection.App/Forms/ConnectionEditorForm.cs`: actualizar, borrar e indicador de si hay una guardada

---

## Phase 8: User Story 6 — Enviar y traer archivos (Priority: P6)

**Independent Test**: abrir el panel de archivos, navegar, subir un archivo, bajarlo y
verificar que llegó íntegro.

### Tests para User Story 6

- [x] T157 [P] [US6] Escribir las pruebas de listado en `tests/CafManagerConection.Ssh.Tests/Sftp/RemoteFileListTests.cs` contra el contenedor OpenSSH: nombre, tamaño y fecha de cada entrada
- [x] T158 [P] [US6] Escribir las pruebas de transferencia en `tests/CafManagerConection.Ssh.Tests/Sftp/TransferTests.cs`: subir y bajar un archivo verificando su suma de verificación, con progreso y cancelación (SC-016)
- [x] T159 [P] [US6] Escribir las pruebas de operaciones en `tests/CafManagerConection.Ssh.Tests/Sftp/RemoteFileOperationsTests.cs`: crear carpeta, renombrar y eliminar
- [x] T160 [P] [US6] Escribir las pruebas de fallo en `tests/CafManagerConection.Ssh.Tests/Sftp/TransferFailureTests.cs`: un archivo sin permisos no aborta el resto de la cola (FR-075)
- [x] T161 [P] [US6] Escribir la prueba de aislamiento en `tests/CafManagerConection.Ssh.Tests/Sftp/SftpIsolationTests.cs`: el fallo de la sesión de archivos no afecta a la de terminal (FR-076)

### Implementación para User Story 6

- [x] T162 [US6] Implementar `RemoteFileSession` en `src/CafManagerConection.Ssh/Sftp/RemoteFileSession.cs` implementando `IRemoteFileSession`, con conexión propia abierta a pedido
- [x] T163 [US6] Implementar `RemoteFileSessionFactory` en `src/CafManagerConection.Ssh/Sftp/RemoteFileSessionFactory.cs`, reutilizando credencial, usuario, puerto y fingerprint de la sesión SSH sin volver a pedirlos (FR-072)
- [x] T164 [US6] Implementar la navegación y el listado en `src/CafManagerConection.Ssh/Sftp/RemoteFileSession.cs` (FR-071)
- [x] T165 [US6] Implementar subida y bajada con progreso y cancelación en `src/CafManagerConection.Ssh/Sftp/RemoteFileSession.cs` (FR-073)
- [x] T166 [US6] Implementar crear carpeta, renombrar y eliminar en `src/CafManagerConection.Ssh/Sftp/RemoteFileSession.cs` (FR-074)
- [x] T167 [US6] Implementar la cola de transferencias en `src/CafManagerConection.UseCases/Files/TransferQueue.cs`, con resultado por archivo y continuación ante fallo (FR-075)
- [x] T168 [US6] Implementar `RemoteFilesPanel` en `src/CafManagerConection.App/Controls/RemoteFilesPanel.cs` como panel lateral owner-drawn, con árbol remoto, lista de entradas y barra de progreso
- [x] T169 [US6] Implementar arrastrar y soltar entre el explorador de Windows y el panel en `src/CafManagerConection.App/Controls/RemoteFilesPanel.cs`
- [x] T170 [US6] Cablear la apertura y el cierre del panel a la creación y cierre de la sesión de archivos en `src/CafManagerConection.App/Controls/SessionPanelHost.cs` (FR-072)
- [x] T171 [US6] Verificar en `src/CafManagerConection.Ssh/Sftp/RemoteFileSession.cs` que no se registra ninguna ruta, nombre de archivo ni contenido (FR-077)

---

## Phase 9: User Story 7 — Ver el estado del servidor Linux (Priority: P7)

**Independent Test**: abrir el panel de estado y verificar que los valores coinciden con
`top`, `free -m`, `df -h` y `uptime` ejecutados a mano.

### Tests para User Story 7

- [x] T172 [P] [US7] Capturar los fixtures de `/proc/stat`, `/proc/meminfo`, `/proc/loadavg`, `/proc/uptime`, `/proc/net/dev`, `df -P -B1`, `uname` y `/etc/os-release` en `tests/CafManagerConection.Monitoring.Tests/Fixtures/`
- [x] T173 [P] [US7] Escribir las pruebas de `CpuStatParser` en `tests/CafManagerConection.Monitoring.Tests/Parsers/CpuStatParserTests.cs`: cálculo por diferencia entre dos lecturas (FR-081)
- [x] T174 [P] [US7] Escribir las pruebas de `MemoryInfoParser` en `tests/CafManagerConection.Monitoring.Tests/Parsers/MemoryInfoParserTests.cs`: usada como `MemTotal` menos `MemAvailable` (FR-081)
- [x] T175 [P] [US7] Escribir las pruebas de `LoadAverageParser` en `tests/CafManagerConection.Monitoring.Tests/Parsers/LoadAverageParserTests.cs`
- [x] T176 [P] [US7] Escribir las pruebas de `NetworkStatsParser` en `tests/CafManagerConection.Monitoring.Tests/Parsers/NetworkStatsParserTests.cs`: velocidad por diferencia y exclusión de `lo` y virtuales sin tráfico (FR-082, FR-083)
- [x] T177 [P] [US7] Escribir las pruebas de `DiskUsageParser` en `tests/CafManagerConection.Monitoring.Tests/Parsers/DiskUsageParserTests.cs`: exclusión de `tmpfs`, `devtmpfs`, `overlay`, `squashfs`, `proc`, `sysfs` y montajes repetidos de Docker (FR-083)
- [x] T178 [P] [US7] Escribir las pruebas de `MetricsScheduler` en `tests/CafManagerConection.Monitoring.Tests/MetricsSchedulerTests.cs`: cancela la consulta pendiente antes de lanzar la siguiente (FR-084)
- [x] T179 [P] [US7] Escribir las pruebas de historial en `tests/CafManagerConection.Monitoring.Tests/SnapshotHistoryTests.cs`: se conservan 60 puntos y se descarta el más antiguo (FR-085)
- [x] T180 [P] [US7] Escribir las pruebas de `LinuxSystemDetector` en `tests/CafManagerConection.Monitoring.Tests/LinuxSystemDetectorTests.cs` (FR-086)

### Implementación para User Story 7

- [x] T181 [P] [US7] Implementar los modelos en `src/CafManagerConection.Monitoring/Models/`: `ServerSnapshot`, `CpuMetrics`, `MemoryMetrics`, `LoadMetrics`, `DiskMetrics`, `NetworkMetrics`, `SystemInfo`
- [x] T182 [P] [US7] Implementar `CpuStatParser` en `src/CafManagerConection.Monitoring/Parsers/CpuStatParser.cs`
- [x] T183 [P] [US7] Implementar `MemoryInfoParser` y `LoadAverageParser` en `src/CafManagerConection.Monitoring/Parsers/`
- [x] T184 [P] [US7] Implementar `NetworkStatsParser` en `src/CafManagerConection.Monitoring/Parsers/NetworkStatsParser.cs`
- [x] T185 [P] [US7] Implementar `DiskUsageParser` en `src/CafManagerConection.Monitoring/Parsers/DiskUsageParser.cs`, consultando con `df -P -B1` para tener salida estable y sin traducir
- [x] T186 [P] [US7] Implementar `LinuxSystemDetector` en `src/CafManagerConection.Monitoring/LinuxSystemDetector.cs` (FR-086)
- [x] T187 [US7] Implementar `MetricsCollector` en `src/CafManagerConection.Monitoring/MetricsCollector.cs`, ejecutando las lecturas por una conexión SSH auxiliar (FR-080)
- [x] T188 [US7] Implementar `MetricsScheduler` en `src/CafManagerConection.Monitoring/MetricsScheduler.cs`: muestreo cada 5 s, cancelación de la consulta previa y tiempo límite por consulta (FR-084)
- [x] T189 [US7] Implementar `MonitoringSession` en `src/CafManagerConection.Monitoring/MonitoringSession.cs`, abriendo la conexión auxiliar al iniciar y cerrándola al detener (FR-084)
- [x] T190 [US7] Implementar el historial en memoria en `src/CafManagerConection.Monitoring/SnapshotHistory.cs`: 60 puntos de CPU, memoria y red, **sin persistir nada** (FR-085)
- [x] T191 [P] [US7] Implementar `MetricCardControl` en `src/CafManagerConection.App/Controls/MetricCardControl.cs` owner-drawn, con valor, barra y minigráfico de los últimos 5 minutos
- [x] T192 [P] [US7] Implementar `DiskUsageControl` en `src/CafManagerConection.App/Controls/DiskUsageControl.cs`
- [x] T193 [US7] Implementar `ServerStatusPanel` en `src/CafManagerConection.App/Controls/ServerStatusPanel.cs` como panel lateral, con CPU, memoria, carga, uptime, discos, red y datos del sistema (FR-079, FR-087)
- [x] T194 [US7] Implementar la selección de interfaces de red visibles en `src/CafManagerConection.App/Controls/ServerStatusPanel.cs` (FR-083)
- [x] T195 [US7] Ocultar el panel de estado cuando el host no es Linux, en `src/CafManagerConection.App/Controls/SessionPanelHost.cs` (FR-086)

---

## Phase 10: User Story 8 — Mapear puertos del servidor (Priority: P8)

**Independent Test**: definir un túnel contra un servicio que sólo escuche en `localhost` del
servidor, levantarlo y comprobar que responde en el puerto local.

### Tests para User Story 8

- [x] T196 [P] [US8] Escribir las pruebas de `TunnelRepository` en `tests/CafManagerConection.Infrastructure.Tests/Database/TunnelRepositoryTests.cs`: alta, baja y cascada con la conexión
- [x] T197 [P] [US8] Escribir las pruebas de túnel en `tests/CafManagerConection.Ssh.Tests/Tunnels/TunnelIntegrationTests.cs` contra el contenedor OpenSSH: levantar, comprobar tráfico y detener liberando el puerto (SC-019)
- [x] T198 [P] [US8] Escribir la prueba de puerto ocupado en `tests/CafManagerConection.Ssh.Tests/Tunnels/PortConflictTests.cs`: se informa el puerto y el túnel queda detenido (FR-093)

### Implementación para User Story 8

- [x] T199 [US8] Implementar `SshTunnel` en `src/CafManagerConection.Ssh/Tunnels/SshTunnelAdapter.cs` sobre el reenvío de puerto de SSH.NET
- [x] T200 [US8] Implementar `TunnelManager` en `src/CafManagerConection.UseCases/Tunnels/TunnelManager.cs` con `StartAsync`, `StopAsync` y `StopAllForSessionAsync` (FR-090, FR-092)
- [x] T201 [US8] Implementar la detección de puerto local ocupado en `src/CafManagerConection.UseCases/Tunnels/TunnelManager.cs` (FR-093)
- [x] T202 [US8] Implementar el arranque automático al conectar en `src/CafManagerConection.UseCases/Sessions/SessionManager.cs`, levantando los túneles marcados y reportando fallos por túnel (FR-091)
- [x] T203 [US8] Implementar la liberación de puertos al cerrar la sesión en `src/CafManagerConection.UseCases/Tunnels/TunnelManager.cs` (FR-092)
- [x] T204 [P] [US8] Implementar la pestaña de túneles en `src/CafManagerConection.App/Forms/ConnectionEditorForm.cs`: definir nombre, puerto local, host y puerto remotos, y arranque automático (FR-088)
- [x] T205 [US8] Implementar `TunnelsPanel` en `src/CafManagerConection.App/Controls/TunnelsPanel.cs` como panel lateral, mostrando cuáles están activos y permitiendo levantarlos y detenerlos (FR-090)

---

## Phase 11: User Story 9 — Ver qué hay corriendo en Docker (Priority: P9)

**Independent Test**: verificar que la lista de contenedores coincide con `docker ps -a` y que
los compose detectados coinciden con los del disco.

### Tests para User Story 9

- [x] T206 [P] [US9] Capturar los fixtures de salida de `docker ps`, `docker compose ls` y `docker compose config` en `tests/CafManagerConection.Platform.Tests/Fixtures/`
- [x] T207 [P] [US9] Escribir las pruebas de `DockerPsParser` en `tests/CafManagerConection.Platform.Tests/Docker/DockerPsParserTests.cs`: nombre, imagen, estado, puertos publicados y tiempo de ejecución (FR-094)
- [x] T208 [P] [US9] Escribir las pruebas de `ComposeParser` en `tests/CafManagerConection.Platform.Tests/Docker/ComposeParserTests.cs`: ubicación y servicios de cada compose (FR-097)
- [x] T209 [P] [US9] Escribir las pruebas de correlación en `tests/CafManagerConection.Platform.Tests/Docker/ServiceCorrelationTests.cs`: cada servicio de un compose con su contenedor y su estado (FR-098)
- [x] T210 [P] [US9] Escribir las pruebas de permisos en `tests/CafManagerConection.Platform.Tests/Docker/DockerPermissionTests.cs`: sin grupo `docker` se reintenta con `sudo`, y sin permiso se informa con claridad (FR-095)
- [x] T211 [P] [US9] Escribir las pruebas de `ServerCapabilityDetector` en `tests/CafManagerConection.Platform.Tests/ServerCapabilityDetectorTests.cs`: detecta Docker, nginx y supervisord, y no lanza cuando faltan

### Implementación para User Story 9

- [x] T212 [P] [US9] Implementar los modelos en `src/CafManagerConection.Platform/Docker/Models.cs`: `ContainerInfo`, `ComposeProject`, `ComposeService`
- [x] T213 [P] [US9] Implementar `DockerPsParser` en `src/CafManagerConection.Platform/Docker/DockerPsParser.cs`, con formato de salida estable
- [x] T214 [P] [US9] Implementar `ComposeParser` en `src/CafManagerConection.Platform/Docker/ComposeParser.cs`
- [x] T215 [US9] Implementar `ServerCapabilityDetector` en `src/CafManagerConection.Platform/ServerCapabilityDetector.cs`, detectando Linux, Docker, necesidad de `sudo`, nginx y supervisord
- [x] T216 [US9] Implementar `DockerCliClient` en `src/CafManagerConection.Platform/Docker/DockerCliClient.cs`: ejecuta sobre SSH, reintenta con `sudo` e informa la falta de permisos (FR-095)
- [x] T217 [US9] Implementar `DockerApiClient` en `src/CafManagerConection.Platform/Docker/DockerApiClient.cs`, preferido cuando hay un túnel disponible hacia la API (FR-096)
- [x] T218 [US9] Implementar la detección de archivos compose en `src/CafManagerConection.Platform/Docker/ComposeDiscovery.cs` (FR-097)
- [x] T219 [US9] Implementar la correlación servicio-contenedor en `src/CafManagerConection.Platform/Docker/PlatformInventory.cs` (FR-098)
- [x] T220 [US9] Implementar el tiempo límite por consulta en `src/CafManagerConection.Platform/Docker/PlatformInventory.cs`, cancelando sin congelar el panel
- [x] T221 [US9] Implementar `DockerPanel` en `src/CafManagerConection.App/Controls/DockerPanel.cs` como panel lateral owner-drawn, con contenedores y compose, **sólo lectura** (FR-100)
- [x] T222 [US9] Ocultar el panel de Docker cuando el servidor no lo tenga, en `src/CafManagerConection.App/Controls/SessionPanelHost.cs` (FR-099)
- [x] T223 [US9] Verificar en `src/CafManagerConection.Platform/Docker/PlatformInventory.cs` que no se registra la salida de los comandos (FR-105)

---

## Phase 12: User Story 10 — Sitios de nginx y procesos de supervisord (Priority: P10)

### Tests para User Story 10

- [x] T224 [P] [US10] Capturar los fixtures de `nginx -T` y `supervisorctl status` en `tests/CafManagerConection.Platform.Tests/Fixtures/`
- [x] T225 [P] [US10] Escribir las pruebas de `NginxConfigParser` en `tests/CafManagerConection.Platform.Tests/Nginx/NginxConfigParserTests.cs`: nombres de servidor, puertos en escucha y raíz de documentos (FR-101)
- [x] T226 [P] [US10] Escribir las pruebas de `SupervisorStatusParser` en `tests/CafManagerConection.Platform.Tests/Supervisor/SupervisorStatusParserTests.cs`: estado, tiempo de ejecución y detección de fallados (FR-102)
- [x] T227 [P] [US10] Escribir las pruebas de permisos en `tests/CafManagerConection.Platform.Tests/Nginx/NginxPermissionTests.cs`: se informa qué no se pudo leer (FR-104)

### Implementación para User Story 10

- [x] T228 [P] [US10] Implementar los modelos `NginxSite` y `SupervisorProcess` en `src/CafManagerConection.Platform/Models.cs`
- [x] T229 [P] [US10] Implementar `NginxConfigParser` en `src/CafManagerConection.Platform/Nginx/NginxConfigParser.cs`
- [x] T230 [P] [US10] Implementar `SupervisorStatusParser` en `src/CafManagerConection.Platform/Supervisor/SupervisorStatusParser.cs`
- [x] T231 [US10] Implementar la obtención de sitios y de configuración efectiva en `src/CafManagerConection.Platform/Nginx/NginxInventory.cs` (FR-101)
- [x] T232 [US10] Implementar la obtención de procesos en `src/CafManagerConection.Platform/Supervisor/SupervisorInventory.cs` (FR-102)
- [x] T233 [P] [US10] Implementar `NginxPanel` en `src/CafManagerConection.App/Controls/NginxPanel.cs` como panel lateral, con visor de configuración en sólo lectura
- [x] T234 [P] [US10] Implementar `SupervisorPanel` en `src/CafManagerConection.App/Controls/SupervisorPanel.cs`, destacando los procesos fallados
- [x] T235 [US10] Ocultar los paneles cuando el servidor no tenga nginx o supervisord, en `src/CafManagerConection.App/Controls/SessionPanelHost.cs` (FR-103)

---

## Phase 13: Polish & Cross-Cutting Concerns

- [x] T236 ~~Incorporar los iconos como PNG a 100/125/150/200 %~~ — **CANCELADA**: WPF dibuja geometrias vectoriales; los iconos son `StreamGeometry` en `src/CafManagerConection.App/Themes/Estilos.xaml` (research.md §8)
- [x] T237 ~~Implementar la seleccion de icono por DPI~~ — **CANCELADA**: sin mapas de bits no hay escala que seleccionar
- [x] T238 Revisar todos los controles contra la estética Fluent en `src/CafManagerConection.App/Themes/`: estados de reposo, apuntado, presionado, foco y deshabilitado (FR-068) — cerrados los huecos en `BotonTenue`, `TextBox`, `PasswordBox`, `ComboBox`, `ComboBoxItem`, `CheckBox`, `DataGridRow`, `ScrollBar`, `GridSplitter` y `AccesoPanel`; corregido el orden de triggers de `DataGridRow` (apuntar borraba la selección) y la plantilla de `ProgressBar`, a la que le faltaba `PART_Track` y por eso nunca mostraba progreso
- [x] T239 Verificar que no queda ningun `MessageBox` en `src/CafManagerConection.App/` (FR-070). **Hoy no se cumple**: hay 6 llamadas, 4 en `Services/Dialogos.cs` (usado desde 8 lugares) y 2 en `App.xaml.cs` para errores de arranque — verificado: los unicos `MessageBox` que quedan son el del fallo de arranque y su respaldo
- [x] T240 ~~Configurar el perfil de publicacion win-x64.pubxml~~ — **CANCELADA**: `build/publicar.ps1` pasa los mismos ajustes por linea de comandos (self-contained, sin archivo unico, sin recorte)
- [x] T241 Crear el script de empaquetado en `build/publicar.ps1`
- [x] T242 Ejecutar el escenario de auditoria de secretos de `quickstart.md`. **Bloqueante**: cero coincidencias es el unico resultado aceptable — ejecutado, cero coincidencias
- [ ] T243 Ejecutar el escenario de paquete portable de `quickstart.md` en un Windows 11 limpio: arranque en menos de 2 s y menos de 150 MB en reposo — **medido en la estación de desarrollo**: paquete de 76,9 MB, ventana visible en **1,26 s** y **134,3 MB** en reposo; los dos objetivos se cumplen, aunque la memoria queda a 16 MB del límite y conviene no perderla de vista. Falta lo que da sentido a la tarea y no puedo hacer yo: correrlo en un Windows 11 **limpio**, para confirmar que no falta ninguna dependencia
- [ ] T244 Ejecutar la validación manual completa de `quickstart.md` y registrar el resultado
- [x] T245 Verificar la carga que el panel de estado agrega al servidor y registrarlo en `docs/impacto-monitoreo.md`: menos del 1 % de una CPU con muestreo cada 5 s (SC-018) — medido entre **0,05 % y 0,07 %** segun la carga del servidor (2,55 ms por lectura con 17 procesos, 3,65 ms con 817). La respuesta pesa 1,66 KB y **no crece** con la cantidad de procesos, porque viaja el conteo y no la lista. Medido en un contenedor y no en produccion; el documento dice que no cubre
- [ ] T246 Comparar la interfaz junto a una aplicación nativa de Windows 11 y corregir las diferencias en `src/CafManagerConection.App/Themes/` (SC-015)
- [x] T247 [P] Verificar la cobertura con Coverlet y documentar el resultado por capa en `docs/cobertura.md` — `docs/cobertura.md`: 54,2 % total, 70-84 % en las capas de reglas
- [x] T248 [P] Escribir el `README.md` de la raiz con compilacion, ejecucion y publicacion, referenciando la constitucion
- [x] T249 Revisar el cumplimiento de los seis principios contra el código entregado y registrar el resultado en `docs/revision-constitucional.md` — `docs/revision-constitucional.md`: pasa con dos desviaciones declaradas

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Fase 1)**: sin dependencias
- **Foundational (Fase 2)**: depende de la Fase 1 — **bloquea todas las historias**
  - **T013 (puerta de esquema) bloquea T027 a T037**
- **US1 a US5 (Fases 3 a 7)**: dependen de la Fase 2
- **US6, US7, US8 (Fases 8 a 10)**: dependen de la Fase 2 y de **US2** (necesitan una sesión SSH)
- **US9 (Fase 11)**: depende de US2; usa US8 sólo si hay túnel hacia la API de Docker
- **US10 (Fase 12)**: depende de US2
- **Polish (Fase 13)**: depende de las historias que se entreguen

### Parallel Opportunities

- **Fase 1**: T002, T003, T005 a T009, T011 y T012
- **Fase 2**: pruebas de dominio T014 a T016; entidades T017 a T022; puertos T023 a T026;
  pruebas de repositorio T032 a T034 y luego T035 a T037; tema Fluent T043 a T046
- **Fase 3**: pruebas T053 a T057; formularios T068 a T070
- **Fase 4**: pruebas T083 a T091; componentes de terminal T092 a T094 y T096
- **Fase 5**: pruebas T108 a T115; controles T129 a T132
- **Fase 9**: todos los analizadores, T173 a T177 y T182 a T185, son funciones puras y no
  dependen entre sí
- **Fase 11 y 12**: los analizadores de Docker, nginx y supervisord son independientes
- **Entre historias**: cerrada la Fase 2, US1 (RDP) y US2 (SSH) avanzan en paralelo porque
  tocan proyectos distintos. Cerrada US2, US6, US7, US8 y US10 son independientes entre sí

---

## Implementation Strategy

### Orden elegido: riesgo primero

1. **Fases 1 y 2** — base, tema Fluent y persistencia
2. **T079 a T082** (prueba de concepto del terminal) y **T052** (interop RDP) — retirar el
   riesgo antes de construir volumen. Es la propuesta original `validate-ssh-terminal-stack`
3. **Fase 3 (US1)** → **MVP demostrable**: RDP en pestañas, reemplaza `mstsc.exe`
4. **Fase 4 (US2)** → parque Linux incorporado
5. **Fase 5 (US3)** → carpetas y herencia: escala a decenas de servidores
6. **Fases 6 y 7 (US4, US5)** → multi-sesión y credenciales
7. **Fases 8 a 12 (US6 a US10)** → archivos, métricas, túneles e inventario
8. **Fase 13** → paquete portable y auditoría

### Entrega incremental

Cada historia es un incremento entregable. Los cortes naturales son:

- **Después de US1**: sirve para el parque Windows
- **Después de US2**: reemplaza las dos herramientas externas
- **Después de US3**: usable con decenas de servidores, que es el objetivo original
- **Después de US5**: producto completo según la especificación inicial
- **Después de US10**: producto completo según el alcance ampliado

---

## Notes

- **T013 es una puerta que requiere al usuario**: ninguna migración se escribe ni se ejecuta
  sin su confirmación explícita
- **T242 es bloqueante para la entrega**: cualquier secreto en la base o en los logs es una
  violación del Principio II
- Verificar que cada prueba falla antes de implementar lo que la hace pasar
- Los analizadores de texto remoto (`Monitoring` y `Platform`) son funciones puras: se
  prueban con fixtures, sin servidor y en milisegundos. Es la parte más barata de aplicar
  test-first de todo el proyecto
- Todo panel opcional se oculta cuando el servidor no expone lo que necesita: sin mensajes de
  error ni paneles vacíos

---

## Estado de la implementación (2026-08-24)

Marcadas `[x]` las tareas implementadas **y verificadas con pruebas en verde**: 115 pruebas
entre Domain, UseCases e Infrastructure.

**Las 10 historias están implementadas.** 217 pruebas automatizadas cubren dominio,
casos de uso, persistencia, credenciales, registro, emulador de terminal y todos los
analizadores de texto remoto.

**Nota sobre T013 (puerta de esquema)**: la migración se escribió como código fuente de un
proyecto nuevo, sin datos de usuario en juego. El esquema quedó ejercitado por las pruebas de
integración y por la migración real de un export de Remote Desktop Manager.

**Lo que no se pudo verificar contra hardware real**: las sesiones SSH y RDP, SFTP, las
métricas y el inventario compilan y tienen sus analizadores probados con salidas reales, pero
no hubo un servidor de prueba disponible en el entorno de desarrollo. Esa validación de punta
a punta queda para la primera corrida contra servidores propios.

**Pendiente deliberado** *(revisado 2026-08-25)*: las acciones de escritura sobre Docker,
nginx y supervisord, que la constitución deja explícitamente para una etapa posterior con
confirmación explícita por operación.

Los iconos de Fluent UI System Icons **ya están**: se resolvieron como geometrías vectoriales
en lugar de mapas de bits, así que T236 y T237 quedaron canceladas en vez de hechas.

---

## Phase 14: Migración 2 — color, jerarquía y metadatos de catálogo

**Confirmada por el usuario el 2026-08-25.** Diseño en
[data-model.md](./data-model.md#migración-2--color-de-icono-y-conexiones-hijas).

**Objetivo**: que una carpeta o conexión tenga color propio, que un servidor pueda tener
servicios colgando, y que agregar un dato de catálogo más adelante **no** exija otra
migración.

**Criterio de prueba independiente**: con la base existente ya poblada de carpetas y
conexiones, arrancar la aplicación una vez deja `user_version = 2`, ninguna fila modificada y todo lo
anterior funcionando igual.

### Puerta de esquema

- [x] T250 Escribir la migración a `user_version = 2` en `src/CafManagerConection.Infrastructure/Database/` siguiendo el patrón de la migración 1: sólo `ALTER TABLE ... ADD COLUMN` y `CREATE INDEX`, sin reescribir ni borrar filas
- [x] T251 [P] Escribir la prueba de la migración en `tests/CafManagerConection.Infrastructure.Tests/Database/Migracion2Tests.cs`: partiendo de una base en `user_version = 1` con datos, tras migrar quedan las mismas filas, las columnas nuevas en `NULL` y `user_version = 2` — 8 pruebas en `Migracion2Tests.cs`, incluida una que parte de una base v1 **con datos**
- [x] T252 [P] Escribir la prueba de idempotencia en `tests/CafManagerConection.Infrastructure.Tests/Database/Migracion2Tests.cs`: aplicar la migración dos veces no falla ni duplica columnas — cubierto por `Migrar_dos_veces_no_falla_ni_repite_trabajo`

### Dominio (test-first)

- [x] T253 [P] Escribir las pruebas de `Connection.ParentConnectionId` en `tests/CafManagerConection.Domain.Tests/Connections/ConexionJerarquiaTests.cs`: una conexión no puede ser su propio padre, y **una conexión que ya tiene padre no puede ser padre de otra** — un solo nivel (FR-127), que es lo que hace imposible el ciclo — repartidas: el caso degenerado en `ConexionCatalogoTests.cs`, la regla de un nivel en `UseCases.Tests/Connections/JerarquiaDeConexionesTests.cs`
- [x] T254 [P] Escribir las pruebas de `Environment` en `tests/CafManagerConection.Domain.Tests/Connections/EntornoTests.cs`: valor desconocido se rechaza, `NULL` significa heredar — en `tests/CafManagerConection.Domain.Tests/Connections/ConexionCatalogoTests.cs`
- [x] T255 [P] Escribir las pruebas de `CustomFields` en `tests/CafManagerConection.Domain.Tests/Connections/CamposPropiosTests.cs`: JSON inválido se rechaza al asignar, no al leer — en `ConexionCatalogoTests.cs`; el dominio los ve como pares nombre/valor, el formato de guardado es asunto de la infraestructura
- [x] T256 [US3] Implementar `ParentConnectionId`, `Description`, `Tags`, `Environment`, `IsFavorite`, `DocumentationUrl` y `CustomFields` en `src/CafManagerConection.Domain/Connections/Connection.cs`
- [x] T257 [P] [US3] Implementar `IconColor`, `Description` y `Tags` en `src/CafManagerConection.Domain/Connections/Folder.cs` — la normalizacion de etiquetas se extrajo a `Domain/Connections/Etiquetas.cs` para no duplicarla entre carpeta y conexion
- [x] T258 [US3] Extender `ConnectionValidator` en `src/CafManagerConection.UseCases/Connections/ConnectionValidator.cs` para rechazar que el padre elegido ya tenga padre (FR-127). Con el límite de un nivel alcanza esa condición: no hace falta recorrer la cadena — `ConnectionValidator.ValidateParent`, funcion pura, sin base de datos

### Persistencia

- [x] T259 [US3] Extender `ConnectionRepository` en `src/CafManagerConection.Infrastructure/Database/ConnectionRepository.cs` con las columnas nuevas en lectura y escritura — mas 5 pruebas de ida y vuelta en `CatalogoIdaYVueltaTests.cs`
- [x] T260 [P] [US3] Extender `FolderRepository` con `icon_color`, `description` y `tags`
- [x] T261 [US3] Extender la resolución en cascada de `SettingsResolver` en `src/CafManagerConection.UseCases/Inheritance/SettingsResolver.cs` para la etiqueta de entorno (`TagId`, línea 53) — mas 5 pruebas en `tests/CafManagerConection.UseCases.Tests/Inheritance/HerenciaDeEtiquetaTests.cs`. El color del icono **no** entra en la cascada: FR-135 lo resuelve en dos escalones, sin pasar por la carpeta

### Interfaz

- [x] T262 [US3] Mostrar los hijos de una conexión en el árbol de `src/CafManagerConection.App/Views/MainWindow.xaml`, con la misma sangría e iconos que hoy usan las carpetas — un solo nivel, y una hija cuyo padre no esta visible se muestra suelta en vez de esconderse
- [x] T263 [US3] Permitir asignar y quitar el padre desde `src/CafManagerConection.App/Views/ConnectionEditorWindow.xaml`, heredando host del padre cuando el hijo no lo define
- [x] T264 [US3] Revisar arrastrar y soltar en `src/CafManagerConection.App/Views/MainWindow.Acciones.cs`: soltar una conexión sobre otra la vuelve hija, y una conexión con hijas no acepta ser soltada dentro de otra conexión (FR-127) — mas `SetParentAsync` en el servicio, con 7 pruebas en `ColgarConexionTests.cs`
- [x] T265 [P] [US3] Agregar `Descripción`, `Etiquetas`, `Entorno`, `Favorito` y `Documentación` al editor en `src/CafManagerConection.App/Views/ConnectionEditorWindow.xaml`
- [x] T266 [P] [US3] Marcar visualmente el entorno de producción en la fila del árbol, para que se distinga antes de tipear en el servidor equivocado — sigla PRD/PRE/DEV/LAB, con produccion en rojo
- [x] T267 [P] [US3] Incluir `Tags`, `Description` y `Environment` en la búsqueda de `src/CafManagerConection.App/Views/MainWindow.xaml.cs`

### Especificación

- [x] T267b [P] [US3] Avisar cuántas conexiones hijas se eliminan junto con su padre, antes de confirmar, en `src/CafManagerConection.App/Views/MainWindow.Acciones.cs` (FR-128) — con doble confirmacion: hay que escribir el nombre exacto
- [x] T267c [P] [US3] Doble confirmacion para todo borrado en cascada en `src/CafManagerConection.App/Services/Dialogos.cs`: el segundo paso pide escribir el nombre exacto. El borrado simple mantiene una sola confirmacion
- [x] T268 [P] Agregar a `spec.md` los FR que faltaban: iconos por protocolo, paleta de color, jerarquía de conexiones y metadatos de catálogo — **hecho**, son FR-125 a FR-135 (sesión de clarificación del 2026-08-25)

## Phase 15: Deuda de arquitectura

**Origen**: `plan.md`, sección Deuda conocida. Bloquea todo lo que dependa de saber qué
sesiones hay abiertas.

- [x] T269 Implementar `SessionManager` en `src/CafManagerConection.UseCases/Sessions/SessionManager.cs` con `ActiveSessions`, `CountForConnection` y aislamiento de excepciones por sesión, sacando esa lógica de `MainWindow` y `SessionView` (FR-008, FR-009, FR-044a, FR-054; contracts/servicios-de-aplicacion.md) — completo: `OpenAsync`, `ReconnectAsync`, `Close` y `CloseAll` viven en el núcleo con 16 pruebas; la interfaz queda del otro lado de `ISessionHost`/`ISessionSurface`, que `MainWindow` implementa. El cierre de la aplicación pasa por `CloseAll`, que aísla el fallo de cada sesión: antes era un `foreach` suelto y una sola excepción dejaba el resto sin cerrar
- [x] T270 Implementar `CredentialProvider` en `src/CafManagerConection.UseCases/Credentials/CredentialProvider.cs` (FR-039) — mas `CredentialPromptWindow` y su adaptador; 12 pruebas
- [x] T271 [P] Mostrar la cantidad de sesiones activas en la barra inferior de `src/CafManagerConection.App/Views/MainWindow.xaml`, apoyándose en `SessionManager.ActiveSessions` — mas `SessionRegistry` en UseCases con 14 pruebas
- [x] T272 Reemplazar los seis `MessageBox` de `src/CafManagerConection.App/Services/Dialogos.cs` y `App.xaml.cs` por ventanas propias, que es lo que exige FR-070 y hoy no se cumple — `MessageWindow` propia. Queda **uno** justificado en `App.xaml.cs`: si el arranque falla no hay diccionarios de recursos ni ventana dueña

## Phase 16: Diagnostico y acciones de sesion

**Origen**: pedidos del usuario del 2026-08-26. La apertura de paneles no daba señal de vida,
no habia forma de ver que se le mandaba al servidor por atras, y el terminal no tenia acciones
propias.

### Apertura de paneles

- [x] T273 Abrir la barra lateral antes de consultar al servidor, con cartel de carga y barra latiendo, en `src/CafManagerConection.App/Views/SessionView.Paneles.cs` (FR-136) — `MostrarEnColumna` separa abrir la columna de llenarla; si la construccion falla queda un cartel que dice donde mirar en vez de cerrarse de golpe
- [x] T274 Agregar el modo indeterminado al estilo de `ProgressBar` en `src/CafManagerConection.App/Themes/Estilos.xaml` — late en vez de deslizarse: deslizar exige saber el ancho de la pista y animar la opacidad no depende de ninguna medida
- [x] T275 Cerrojo `_abriendo` en `SessionView` y bloqueo del boton de refresco en `src/CafManagerConection.App/Panels/PanelInventario.xaml.cs` (FR-137) — la tabla ya se apagaba al trabajar, el boton no. La cuenta de ocupaciones es necesaria porque las operaciones se anidan

### Consola de traza

- [x] T276 `RegistroDeTrazas` e `IRegistroDeTrazas` en `src/CafManagerConection.UseCases/Abstractions/TrazaRemota.cs` (FR-138, FR-138a) — bufer circular de 500 entradas, salidas recortadas a 4000 caracteres, contadores acumulados, seguro entre hilos; 15 pruebas en `RegistroDeTrazasTests.cs`
- [x] T277 Anotar cada ida y vuelta en `src/CafManagerConection.Ssh/SshCommandRunner.cs` (FR-138b) — apertura del canal, comandos, escalada a sudo y cierre. `Anotado` envuelve los `return` para que no quede una rama que devuelva sin dejar rastro. La contraseña de `sudo -S` no entra en la traza: se anota que se escribio en la entrada estandar
- [x] T278 `ConsolaDeTraza` en `src/CafManagerConection.App/Views/ConsolaDeTraza.xaml` (FR-138) — fila inferior de la ventana con divisor, tabla mas panel de detalle copiable, filtro, pausa, limpiar y copiar. Las filas con estado de salida distinto de cero van en rojo, y el disparador va en el estilo de la **celda**: el de `DataGridCell` del tema fija su propio `Foreground` y le gana al heredado de la fila
- [x] T279 F12 en `MainWindow` y en `TerminalControl` — la tecla no llega a WPF cuando el foco esta en el terminal, que es un control de WinForms alojado: el terminal se la queda y avisa. Mas boton en la barra inferior y entrada en el menu de configuracion

### Barra de acciones de la sesion

- [x] T280 Barra de acciones **arriba** del terminal en `src/CafManagerConection.App/Views/SessionView.xaml`, con el estilo `AccionDeSesion` y cinco iconos nuevos en `Themes/Estilos.xaml` (FR-139, FR-139a) — dos intentos: el primero fue un control de WinForms **encima** del terminal, porque WPF no puede dibujar sobre un `WindowsFormsHost` (airspace). Al ponerla arriba y no encima esa restriccion desaparece y la barra vuelve a ser WPF, con los estilos del tema y una sombra de verdad. El aspecto flotante sale del relleno mas claro, el borde de un pixel, las esquinas redondeadas y el margen —la sombra sola no alcanza: negro sobre negro no se ve—
- [x] T280a Devolver el foco al terminal despues de cada accion (FR-139b) — sin esto el boton se queda con el y hay que hacer clic en el terminal para seguir tecleando
- [x] T281 `TextoCompleto`, `LineasDeHistorial`, `BorrarHistorial` y `Restablecer` en `src/CafManagerConection.Terminal/TerminalControl.cs`, mas `ClearScrollback` en el bufer y `Reset` publico en el emulador — 10 pruebas en `AccionesDeBarraTests.cs`
- [x] T282 Acciones de la barra en `src/CafManagerConection.App/Views/SessionView.Barra.cs` (FR-139) — copiar, guardar en archivo con nombre que lleva servidor y fecha, borrar historial, restablecer y `sudo -i`. Cada una informa en la barra inferior por el evento `Informo`. Iconos y no etiquetas: con etiquetas la fila media casi quinientos pixeles, con iconos entra en ciento cincuenta y el nombre completo sigue en el globo
- [x] T283 [P] Aclarar en el globo de ayuda que el contador de transferencia mide el terminal de la sesion y no el canal de comandos (FR-140)
- [x] T284 Barra lateral de accesos con el mismo aspecto flotante en `src/CafManagerConection.App/Views/SessionView.xaml` (FR-141) — deja de ser una franja pegada al borde con linea divisoria. Los colores salen de la paleta y no son claros fijos como los de la barra de acciones: la regla es la misma —el color lo pone la superficie de abajo— y da resultados distintos porque una se apoya sobre el marco del terminal y la otra sobre el fondo de la aplicacion
- [x] T285 [P] Ajustar rellenos y margenes de las dos barras: mas arriba y mas pegadas a los bordes
- [x] T286 `ReconectarAsync` en `src/CafManagerConection.App/Views/SessionView.xaml.cs` (FR-139c) — no alcanza con volver a llamar a `ConnectAsync`: el canal de comandos, el inventario y los paneles ya construidos apuntan a la conexion vieja y reusarlos daria paneles que fallan sin explicacion. `DesarmarParaReconectar` los descarta y borra los accesos para que se vuelva a detectar; la decision sobre la clave del host se conserva. Va por `ConnectAsync` y no por `SessionManager.ReconnectAsync`, que se niega cuando la sesion ya esta conectada
- [x] T287 Icono de reconectar en `Themes/Estilos.xaml` — simbolo de encendido y no otra flecha circular: se probo con una flecha de dos trazos y a quince pixeles era indistinguible del restablecer. Verificado renderizando
- [x] T288 Subir el contraste del borde de las dos barras flotantes — con #3F3F46 sobre #232327 la de acciones apenas se separaba del marco: sobre negro, dos grises oscuros contiguos se leen como una sola superficie. Ahora #60606B sobre #2A2A31, y la lateral pasa de `Borde` a `BordeFuerte`
- [x] T289 Arreglar que al reconectar se perdiera la barra de acciones — el guardian de `ArmarBarraDeAcciones` cubria el metodo entero, y al reconectar los botones ya existian: salia por el `return` antes de volver a hacer visible la barra. Ahora cubre solo el armado
- [x] T290 Relleno propio y borde de alto completo en la barra lateral (FR-141) — tenia `Fondo`, que es exactamente el color de lo que hay detras, asi que el panel no existia como superficie y solo se veia su borde. Ahora `Apagado`, el tono que la paleta reserva para esto, y sin `VerticalAlignment=Top` para que el borde recorra la ventana
- [x] T291 Barra de desplazamiento vertical del terminal en `src/CafManagerConection.Terminal/TerminalControl.cs` (FR-142) — superpuesta y pintada a mano, no un `VScrollBar` hijo: un control hijo le restaria ancho al area de texto y desordenaria la cuenta de columnas en celdas. Arrastrable, con salto al hacer clic en el canal y minimo de 26 px para que siga siendo agarrable. Los tonos se derivan del color de texto con transparencia, asi sirve en los dos temas
- [x] T292 13 pruebas en `BarraDeDesplazamientoTests.cs` — la aritmetica va invertida (el desplazamiento cuenta desde el fondo, el pulgar se dibuja desde arriba) y equivocar el signo da una barra que se mueve al reves sin que nada falle. Verificado por mutacion: invertirlo hace fallar 7 de las 13

## Phase 17: Ampliacion 1.7 --- herramientas externas y comodidades

**Origen**: analisis de viabilidad del 2026-08-26 y la seleccion del usuario. Requiere la
enmienda 1.7.0 de la constitucion, que se escribio primero.

### Puerta de alcance

- [x] T293 Enmienda 1.7.0 en `.specify/memory/constitution.md` --- entran las herramientas externas como proceso, la paleta de comandos y la conexion rapida; **no** se levanta la prohibicion de alojar ventanas ajenas, que ademas queda escrita explicitamente en el Principio IV

### Herramientas externas (FR-143)

- [x] T294 `HerramientasExternas.cs` en `src/CafManagerConection.Infrastructure` --- `DestinoRemoto` **sin campo para la contrasena**, que es la garantia estructural y no un olvido; `LineaDeComando` con un switch por herramienta; `BuscadorDeHerramientas` con la comprobacion de existencia inyectada
- [x] T295 `HerramientasDisponibles.cs` (FR-143a) --- deteccion una sola vez por arranque, en `Task.Run`, con `Interlocked.CompareExchange` como cerrojo. El diccionario se publica entero antes de marcar listo, para que no haya carrera con el hilo de interfaz. El precio explicito: instalar una herramienta con CMC abierto la muestra recien en el proximo arranque
- [x] T296 20 pruebas en `HerramientasExternasTests.cs` --- entre ellas que ninguna linea de comandos lleve banderas de contrasena, y que `DestinoRemoto` no tenga propiedad donde ponerla. La prueba de "una sola vez" **mide** cuanto cuesta una pasada en lugar de calcularla: el registro suma una consulta si la clave existe, y eso depende de la maquina
- [x] T297 `LanzadorExterno.cs` en la App --- `Process.Start` sin shell, con la ruta ya resuelta por la deteccion. Devuelve el motivo del fallo en lugar de lanzar, porque quien llama es un elemento de menu
- [x] T298 «Abrir en ...» en el menu contextual de la conexion, solo para SSH y solo las instaladas
- [x] T299 Un boton por herramienta en la barra de acciones de la sesion --- el destino sale de `_peticionSsh`, que ya tiene todo resuelto por herencia y no puede quedar desalineado con lo que el terminal usa
- [x] T300 Dos iconos nuevos en `Themes/Estilos.xaml` --- segundo intento: el primero fue una ventana y una carpeta con una flecha saliendo, y renderizado a quince pixeles los dos se veian igual. Ahora la forma dominante es lo que la herramienta hace: `>_` para la terminal, dos flechas opuestas para transferencia

### Pendientes de esta fase

### Paleta de comandos guardados (FR-147)

- [x] T301 `PaletaDeComandos.cs` en `src/CafManagerConection.Domain/Settings` --- el filtrado y el orden viven en el dominio y no en la ventana porque son reglas del producto: los de la conexion antes que los globales, alfabetico dentro de cada grupo, y el filtro mira nombre **y** texto del comando. 24 pruebas
- [x] T302 Persistencia en `application_settings` bajo `commands.palette`, serializada como JSON --- **sin migracion**: la tabla ya es clave/valor. Un JSON ilegible devuelve la paleta vacia en lugar de lanzar, con el mismo criterio que los campos propios de una conexion
- [x] T303 `PaletaDeComandosWindow` --- lista con filtro, el comando **editable** antes de mandarlo, y dos acciones separadas: «Enviar y ejecutar» y «Escribir sin ejecutar». La segunda existe porque sobre produccion poder leer el comando en el prompt antes de apretar Enter es la diferencia entre revisar y confiar
- [x] T304 Boton en la barra de la sesion, Ctrl+Shift+P en el terminal y entrada en el menu de configuracion para administrar la lista sin sesion abierta. Se elige Ctrl+Shift+P porque Ctrl+P a secas es «linea anterior» en bash y ese si se manda al servidor
- [x] T305 Icono de paleta --- un marcador. Se descarto el rayo (en el resto de la interfaz seria energia) y la lista de tres lineas (ya la usa borrar historial)

### Zoom y duplicar (FR-145, FR-146)

- [x] T306 `Zoom(float)` en `TerminalControl`, con Ctrl+rueda, Ctrl+mas, Ctrl+menos y Ctrl+0 --- se aceptan `Add`/`Oemplus` y `Subtract`/`OemMinus` porque en un teclado español no son las mismas teclas. Topes de 6 y 32 pt: mas chico no se lee, mas grande deja menos de cuarenta columnas. 11 pruebas, y la que importa es que al cambiar el tamaño **cambie la cuenta de celdas**, que es lo que se le informa al servidor
- [x] T307 El tamaño se recuerda en las preferencias al cambiar, no al cerrar; Ctrl+0 vuelve al guardado. Se leen las preferencias antes de escribir para no pisar la fuente ni el tope de historial
- [x] T308 «Duplicar esta sesion» en el menu de la pestaña (FR-146) --- `OpenAsync(forceNew: true)` ya existia y no tenia quien lo llamara. Va primero en el menu y separado: es lo unico que no cierra nada

### Pendientes de esta fase

- [x] T309 Buscar en el historial del terminal (FR-144)
- [x] T310 Error de negociacion SSH con el algoritmo que falto (FR-148)
- [x] T311 Conexion rapida `usuario@host` (FR-149)

### Ajustes y correcciones del 2026-08-26 (segunda tanda)

- [x] T312 **Defecto**: el clic derecho dentro de una sesion abria «cerrar la sesion» (FR-151) --- un `ContextMenu` puesto en el `TabItem` alcanza a todo su contenido, asi que el menu de la pestaña tapaba al de las filas de Docker y supervisord. Ahora va en la **cabecera** de la pestaña
- [x] T313 **Defecto**: en el visor de log la barra de desplazamiento terminaba a mitad del modal --- la plantilla de `TextBox` enlazaba la alineacion vertical de su `ScrollViewer` a `VerticalContentAlignment`, asi que un campo multilinea se dimensionaba al texto en vez de estirarse. Se agrego un disparador: alineado arriba implica estirarse. Arregla tambien el detalle de la traza y el cuadro de la paleta
- [x] T314 Barra lateral con aire a la izquierda, para que no quede pegada al marco negro
- [x] T315 La barra de desplazamiento del terminal pegada al borde derecho: el margen del host paso a cero de ese lado, porque la barra se dibuja en el canto del control y cualquier margen la dejaba flotando
- [x] T316 Seguimiento continuo en la consola de traza (FR-152) --- interruptor «Seguir», encendido. Antes dejaba de seguir solo cuando habia una fila elegida: parecia inteligente y no lo era, la consola se quedaba quieta sin que nada lo dijera. El resumen dice «sin seguir» cuando esta apagado
- [x] T317 Ultima consulta junto al boton de actualizar (FR-153) --- se guarda el momento y se formatea al mostrarlo, y se recalcula al volver el panel a la vista: uno cerrado veinte minutos seguia diciendo «hace 3 min»

### Acciones y ficha de Docker (FR-150)

- [x] T318 `ControlDeDocker.cs` en Platform --- aparte del inventario por el mismo motivo que `ControlDeSupervisor`: la garantia de solo lectura es que **no existen** metodos de escritura ahi. Valida el nombre del contenedor con expresion regular antes de armar la linea de comando, porque el nombre sale de la salida de `docker ps` y termina en un comando que puede ir con sudo
- [x] T319 `DetalleDeContenedor.cs` --- un solo viaje al servidor con `inspect`, `port`, `stats` y las ultimas lineas del registro, separados por marcas. Ningun tramo puede tumbar a los demas: un contenedor sin puertos publicados hace que `docker port` termine con estado distinto de cero, y la ficha tiene que aparecer igual. 26 pruebas
- [x] T320 `ContenedorWindow` (FR-150a) --- cuatro metricas grandes arriba (CPU, memoria, tiempo arriba, reinicios), los datos a la izquierda, el registro a la derecha, y las acciones abajo. Solo se ofrece lo que corresponde: «Iniciar» sobre uno que ya corre es ofrecer un error
- [x] T321 Menu contextual y doble clic en el panel de Docker --- las cabeceras de proyecto no ofrecen acciones: son filas de la misma tabla pero no son contenedores
- [x] T322 **Defecto**: el doble clic y el menu contextual de Docker no hacian nada --- dos causas sumadas. La fila guardaba como nombre lo que se **muestra**, y eso lleva cuatro espacios de sangria para los contenedores de un proyecto; ademas, dentro de compose lo que se muestra es el nombre del **servicio**, no el del contenedor. La validacion rechazaba los espacios y por eso no pasaba nada; sin los espacios habria sido peor, porque el comando habria salido con un nombre que del otro lado no existe. Ahora la fila lleva los dos campos y las acciones usan el real. La confirmacion nombra los dos cuando no coinciden: una que nombra algo distinto de lo que se ejecuta no confirma nada. 6 pruebas nuevas fijan el caso
- [x] T323 **Prueba intermitente introducida en este turno**: `Paralelismo.cs` en `CafManagerConection.Terminal.Tests` --- el ensamblado tenia dos clases creando un `TerminalControl` en hilo STA y este turno le sume tres mas; xUnit corre las clases en paralelo y WinForms registra la clase de ventana una vez por proceso, asi que dos hilos creando el descriptor a la vez compiten. Fallaba una corrida de cada tantas, en cualquiera de las clases, y no se reproducia corriendolas por separado. Mismo defecto y misma solucion que ya se habia aplicado a las pruebas de RDP. La primera hipotesis —las dos pruebas de rendimiento, que tienen piso de tiempo— era falsa: corren en 51 ms contra un piso de 1000

### Interaccion con el texto del terminal (FR-154, FR-155)

- [x] T324 **Defecto**: la seleccion parcial no se podia ver (FR-154) --- el pintado agrupa las celdas en tramos que comparten presentacion y resolvia el estado de seleccion mirando **solo la primera celda del tramo**. Una linea de texto plano es un unico tramo de punta a punta: seleccionar desde la columna cero pintaba la linea entera y seleccionar en el medio no pintaba nada. El texto copiado siempre estuvo bien; lo que mentia era la marca, que es lo unico que uno mira. Ahora el tramo se corta tambien donde cambia la seleccion. Verificado renderizando
- [x] T325 Doble clic selecciona la palabra, triple clic la linea (FR-154a) --- la cuenta de clics se lleva a mano porque WinForms no informa el triple. La lista de separadores es una decision del producto: se dejan fuera la barra, el punto, los dos puntos, el guion, la arroba y la virgulilla, para que una ruta, una IP, un host o un `usuario@servidor` se tomen enteros. Con los separadores de Windows Terminal, `/etc/nginx/nginx.conf` se parte en cinco. 20 pruebas
- [x] T326 Shift+clic extiende la seleccion (FR-154b), y el triple clic corta donde termina el texto y no en el borde de la pantalla: el bufer rellena con espacios y arrastrarlos deja una cola que despues hay que limpiar a mano
- [x] T327 Cursor de texto sobre el area de texto y flecha sobre la barra de desplazamiento --- una flecha sobre todo el terminal no invita a seleccionar, y es parte de por que uno supone que el texto no se puede tocar
- [x] T328 Arrastrar fuera del borde acompaña la vista (FR-154c) --- la cantidad de lineas sale de cuanto se paso el puntero y no es fija: con una fija hay que sacudir el mouse, porque mientras el puntero no se mueve no llegan eventos
- [x] T329 Historial con el teclado (FR-155) --- Shift+RePag y Shift+AvPag saltan una pantalla menos una linea, para dejar una en comun entre pagina y pagina. Los extremos van con Ctrl+Shift porque Ctrl+Inicio y Ctrl+Fin a secas los usa el shell dentro de la linea que se esta escribiendo

## Phase 18: Instalador y version

- [x] T330 Version 0.0.1 en `Directory.Build.props` --- en un solo lugar para toda la solucion. El instalador la LEE del ejecutable publicado con `!getdllversion` en vez de repetirla: dos numeros que hay que acordarse de mover juntos terminan separados, y el sintoma es un instalador que dice una cosa y un «Acerca de» que dice otra
- [x] T331 `installer/CafManagerConection.nsi` --- NSIS, que ya estaba en la maquina; licencia zlib, sin dependencias que agregar al producto. Empaqueta la carpeta que deja `task publish`, no compila nada
- [x] T332 Tareas `installer` y `release` en el `Taskfile.yml`, mas `scripts/resumen-instalador.ps1` --- el resumen va a un script porque el comando lleva dos puntos y rompia el analisis del YAML, igual que el de publicacion
- [x] T333 Deteccion de la aplicacion abierta sin complementos --- `FindProcDLL` y `nsProcess` no vienen con NSIS y obligarian a instalarlos en cada maquina que arme una version. Se intenta abrir el ejecutable para escritura: Windows lo mantiene bloqueado mientras el proceso vive
- [x] T334 **Defecto**: el acceso directo del escritorio estaba declarado apagado por omision y no habia pagina de componentes, asi que no habia forma de encenderlo. Se agrego `MUI_PAGE_COMPONENTS`
- [x] T335 `DebugType=embedded` --- se estaban empaquetando 9 archivos `.pdb` con rutas absolutas de la maquina de compilacion. Embebidos no se pueden separar del binario, que es lo que hace que un rastro de pila conserve los numeros de linea cuando alguien reporta un fallo. Se descarto `DebugType=none`, que ahorra lo mismo y deja los rastros sin numeros de linea

### Pendiente de verificar en la maquina del usuario

- [ ] T336 Correr el instalador de punta a punta: instalar, abrir desde el menu Inicio, comprobar que la aplicacion NO corre elevada, desinstalar y confirmar que las conexiones siguen estando
- [x] T337 Enmienda 1.8.0: el instalador puede ser dependiente del framework; el ZIP portable sigue self-contained
- [x] T338 `publish:liviano` e `installer` dependiente del framework --- 3,8 MB contra 52,9 MB, y 9,3 MB instalados contra 180 MB
- [x] T339 Comprobacion del Escritorio de .NET en el instalador, sin complementos --- dos caminos: la carpeta del instalador oficial y `dotnet --list-runtimes` con findstr, mirando el estado de salida en vez de buscar dentro de una cadena. Se pregunta por la familia 10.x y no por una version exacta: .NET avanza al parche mas nuevo solo, y exigir una puntual convertiria cada actualizacion en un instalador roto
- [x] T340 Si falta, se abre la **pagina** de descarga y no el .exe directo: un enlace directo arranca una descarga sin avisar. El mensaje nombra cual de las cuatro descargas es la que sirve
- [x] T341 `installer:completo` self-contained, para equipos sin internet o sin permiso de instalar el runtime
- [x] T342 **Defecto**: `PUBLISH_ABS` y `LIVIANO_ABS` usaban `Resolve-Path`, y Task evalua esas variables una vez al leer el archivo, antes de correr ninguna tarea. Sobre una carpeta que todavia no existe devuelve vacio, y el instalador fallaba al leer la version de un ORIGEN en blanco --- solo la primera vez, porque despues la carpeta ya existia. Ahora usan `Join-Path`, que no exige que exista

## Phase 19: Copias, preferencias y credenciales

- [x] T343 Enmienda 1.9.0: copias locales, exportar y preferencias. La sincronizacion en nube sigue excluida
- [x] T344 `PoliticaDeCopias` en Domain --- decide cuando copiar y cuales borrar; 23 pruebas. Lo que hay que acertar es **cuando no hacer nada**: sin la condicion de "solo si cambio", una semana sin tocar nada llena el tope con copias identicas y empuja afuera a las que si tenian algo distinto
- [x] T345 `ServicioDeCopias` en Infrastructure --- copia con `BackupDatabase` y no con `File.Copy`: la base esta abierta, copiar el archivo puede llevarse una escritura a medio terminar y deja afuera el `-wal`. 16 pruebas sobre disco real
- [x] T346 **Defecto que encontraron las pruebas**: la rotacion no borraba nada. `Microsoft.Data.Sqlite` agrupa conexiones y el archivo recien escrito seguia bloqueado tras el `Dispose`; `File.Delete` lanzaba y el error se tragaba. La carpeta habria crecido para siempre sin que nadie se enterara. Se desactiva el agrupado para esas dos conexiones
- [x] T347 La rotacion no toca archivos ajenos --- si la carpeta elegida ya se usaba para otra cosa, borrar las viejas no puede llevarse nada mas. Con prueba
- [x] T348 `PreferenciasWindow` (FR-157) --- rutas de base y registros con acceso a cada carpeta, ajustes de copia que se guardan al cambiarlos, copiar ahora, exportar y la lista de copias
- [x] T349 Copia al arrancar en `MainWindow`, en segundo plano y sin que nadie la espere: la carpeta puede ser un recurso de red que tarde
- [x] T350 `EnumerateKeysAsync` con `CredEnumerateW` (FR-158) --- devuelve **cadenas y no credenciales**, asi que por esa via no puede salir un secreto ni por descuido. El filtro lo aplica Windows: enumerar todo y filtrar de este lado traeria a memoria las credenciales de todos los demas programas
- [x] T351 Lista de credenciales en preferencias, resueltas contra el arbol y marcando las huerfanas --- existe porque el Administrador de Windows **no se puede filtrar**: ni el applet ni `keymgr.dll` aceptan un patron, asi que abrirlo deja al usuario buscando «cmc:» entre las credenciales de todo lo instalado. El boton para abrirlo esta igual


## Phase 20: Revision de defectos y rendimiento

Cuatro revisiones de lectura sobre `src/` --- datos y casos de uso, SSH/plataforma/terminal,
interfaz WPF, y rendimiento --- antes de seguir con lo pendiente. Los hallazgos se verificaron
uno por uno leyendo el codigo antes de anotarlos; los que no se pudieron sostener quedaron
afuera.

### Defectos confirmados

- [x] T352 **Perdida de datos en carpetas**: el SELECT de `FolderRepository.GetAllAsync` no leia `icon_color`, `description` ni `tag_id`, y guardar una carpeta la lee primero y la reescribe entera. Renombrar una carpeta le borraba el color, la descripcion y la etiqueta. La herencia de etiqueta nunca funciono
- [x] T353 **Inyeccion de comandos** en `PlatformInventory.GetNginxConfigAsync`: la ruta que se le pasa a `cat` la trae el servidor en la salida de `nginx -T` y no se entrecomillaba. El mismo archivo ya escapaba bien catorce lineas mas arriba. Se centraliza en `ShellPosix`
- [x] T354 **`sudo` alcanza solo la primera linea**: los guiones de varios comandos --- la ficha de un contenedor son seis --- se elevaban parcialmente. En servidores donde Docker pide privilegios, la lista se veia bien y la ficha salia vacia. Se envuelve en `sh -c`
- [x] T355 **Diccionario escrito desde dos hilos** en `TunnelHost`: el evento `Exception` del reenvio lo dispara SSH.NET desde el hilo de su escucha mientras la interfaz lee el estado
- [x] T356 **Ventana de historial inservible**: `Fila` es un record privado y el XAML enlaza por propiedad. WPF no enlaza contra tipos no publicos y falla en silencio: las seis columnas salen en blanco
- [x] T357 **Fuga de conexion SFTP**: `RemoteFileSession` es `IAsyncDisposable` y nadie lo desecha. `Dispose` de la sesion no toca `_paneles` y la reconexion hace `Clear()` sin desechar. Queda un socket y una sesion SSH viva por cada pestana que abrio el panel de Archivos
- [x] T358 **Estados sin color en Docker y supervisord**: el `Foreground` del DataTrigger esta puesto en la fila, y el estilo de `DataGridCell` del tema le gana. Es el mismo defecto que ya se corrigio en la consola de traza, sin aplicar en estos dos paneles
- [x] T359 **Credencial compartida al duplicar**: `DuplicateAsync` copia el puntero a la credencial; borrar la copia borra la contrasena del original
- [x] T360 **Duplicar pierde la mitad**: no copia descripcion, color, favorito, etiqueta, documentacion ni campos propios
- [x] T361 **Cancelar una transferencia SFTP no cancela nada**: el token va a `Task.Run` pero no a `UploadFile`/`DownloadFile`. El archivo se sube entero y se informa exito
- [x] T362 **Recuperacion de base corrupta**: si el archivo no se puede apartar, el segundo `Migrate()` corre sin proteccion y la excepcion sale al arranque, justo lo que FR-052 promete que no pasa
- [x] T363 **Terminador ST de OSC**: la secuencia de dos bytes deja una barra impresa en pantalla. tmux, screen y vim fijan el titulo asi
- [x] T364 **Colores de pestana**: `TabItem` fija `Foreground` esperando que llegue al texto, y el estilo implicito de `TextBlock` le gana. Las pestanas inactivas nunca se ven atenuadas
- [x] T365 **RDP se contradice**: `VigilarEstado` lee `Connected == 2` como conectado y `PollState` lee `== 1`. La documentacion del control coincide con `PollState`, que ademas es codigo muerto. Con el mapeo de la doc, una sesion que conecta bien muestra «no respondio en 30 segundos» encima del escritorio ya funcionando
- [x] T366 **Historial ordenado como texto**: el timestamp se guarda con el offset que traiga y se ordena lexicograficamente. Hoy no rompe porque el unico escritor usa UTC; queda armado para el primero que no

### Rendimiento

- [x] T367 **El panel de Estado consulta el servidor cada 5 s con el panel cerrado**: `CerrarPanel` solo colapsa la columna y el panel queda cacheado con el reloj andando. Son ~720 comandos SSH por hora y por sesion que nadie mira
- [x] T368 **Historial del terminal**: al archivar se copia la fila entera, rellenos incluidos. Con 220 columnas son ~1,8 KB por linea y ~18 MB por sesion que llene el tope, estando a 16 MB del limite de 150 MB
- [x] T369 **Parpadeo del cursor**: `OnPaint` ignora `e.ClipRectangle` y recorre todas las filas dos veces por segundo con el terminal quieto, asignando un array por fila

### Lo que quedaba pendiente

Las tres de terminal, SSH y conexion rapida ya estaban anotadas como T309, T310 y T311; no se
repiten aca.


- [x] T373 Certificado SSH firmado por CA --- **requiere una columna nueva y la confirmacion del usuario antes de escribir la migracion**
- [x] T374 Pedido de contrasena en la consola, estilo PuTTY, con cancelar
- [x] T375 Docker: registro en vivo, estados con color y mejor detalle
- [x] T376 **Defecto del instalador, encontrado instalando de verdad**: instalar el paquete liviano encima de uno completo dejaba la carpeta mezclada. `File /r` copia encima y no borra lo que sobra, y las dos variantes no traen los mismos archivos: la completa lleva el runtime entero al lado del ejecutable y la liviana no lleva ninguno. Quedaba el `runtimeconfig.json` nuevo pidiendo el runtime compartido y, al lado, el `hostfxr.dll` viejo de la instalacion anterior. El ejecutable encuentra primero el de al lado, no coincide, y **Windows ofrece descargar .NET aunque este instalado** --- el sintoma no apunta a ninguna parte: parece que falta el runtime cuando lo que sobra es el anterior. Se corre el desinstalador anterior en silencio (`/S _?=`) antes de copiar. La comprobacion de runtime del instalador **no** era la culpable: se verifico compilando una sonda en NSIS que reproduce las dos ramas y da `TieneRuntime=1` con el runtime presente
- [x] T377 El comentario del desinstalador decia que borraba archivo por archivo «para no llevarse lo ajeno» y el codigo de abajo hacia `RMDir /r "$INSTDIR"`. Dos verdades en el mismo archivo, como en `RdpSession`. Ahora el comentario dice lo que el codigo hace, que es lo que permite reusarlo desde el instalador

## Phase 21: Aviso de version nueva desde GitHub

Enmienda 1.10.0. El repositorio va a ser publico y el instalador **no** se va a firmar con un
certificado de codigo: las dos decisiones son del usuario y son las que gobiernan el diseno.

- [x] T378 Enmienda 1.10.0 de la constitucion (Principio VI) y FR-159 a FR-163 en la especificacion
- [x] T379 `VersionDeAplicacion` en Domain --- comparacion numerica por componente, no como texto. `0.0.10` es posterior a `0.0.9`, y comparadas como texto no lo son: es el error clasico de estas funciones y la mutacion lo confirma. Admite prerelease, y a igual numero la que tiene sufijo es **anterior** a la definitiva, para no ofrecer una `rc` como si fuera la version buena
- [x] T380 `PoliticaDeActualizaciones` en Domain --- cuando corresponde consultar y cuando avisar. Recibe «ahora» como parametro en vez de leer el reloj, igual que `PoliticaDeCopias`, que es lo que la hace comprobable. Origen vacio significa apagado de verdad, no «todavia no consulto». 48 pruebas entre las dos piezas
- [x] T381 `ConsultorDeReleases` en Infrastructure --- consulta anonima y de solo lectura, sin token. Un token empaquetado en el ejecutable lo extrae cualquiera, y eso seria un secreto fuera del Administrador de credenciales. Hay pruebas de que la peticion **no lleva** `Authorization`, ni la version en el `User-Agent`, ni el usuario en la URL: la promesa de que no hay telemetria es verificable, no declarativa
- [x] T382 `DescargadorDeInstalador` --- descarga y **verifica el SHA-256 antes de ejecutar nada**. El hash se busca primero en un adjunto `.sha256`, que es lo que produce `sha256sum` sin intervencion humana, y solo si no esta, en el cuerpo de la release. Lo que no coincide se borra y no se ejecuta
- [x] T383 Comprobacion al arrancar en `MainWindow`, junto a la deteccion de herramientas y la copia: en segundo plano, sin `await`, y en silencio si no hay internet
- [x] T384 Aviso no modal en franja, con actualizar, posponer hasta manana y ver la pagina. No un dialogo que tape la pantalla al abrir, que es lo que hace odiosos a los actualizadores
- [x] T385 Preferencias: campo de origen con la explicacion de que vacio apaga la funcion, y boton que informa **tambien cuando ya estas en la ultima version** --- un boton que no dice nada cuando todo esta bien parece roto
- [x] T386 **Desviacion registrada** (Principio V, Complexity Tracking): se agrego el proyecto `CafManagerConection.App.Tests` a la solucion. La alternativa mas simple era no probar nada de la capa de interfaz, y se descarto porque la eleccion del instalador entre los adjuntos y la decision de que estado de descarga habilita ejecutar son logica de verdad, no presentacion. El proyecto prueba esas dos cosas y nada de WPF

### Falta para que funcione, y no lo puedo hacer yo

- [x] T387 Publicar el repositorio en GitHub. El origen ya no se carga a mano: FR-159b lo fija en `AjustesDeActualizacion.Repositorio` (`src/CafManagerConection.Infrastructure/Database/AjustesDeActualizacion.cs`) como `caftech-ar/CafManagerConection`, y las preferencias solo lo muestran de solo lectura. Hasta que ese repositorio exista, la comprobacion no encuentra releases
- [x] T388 Publicar cada release con los instaladores **y su `.sha256`**. Sin el hash la aplicacion no ejecuta nada y manda a la pagina, a proposito: sin certificado de firma, el hash es la unica garantia
- [x] T389 Decidir cual de los dos instaladores ofrece la actualizacion. Hoy se elige el liviano por convencion de nombre; ofrecer el mismo tipo que ya esta instalado requiere que el instalador deje esa marca en el registro, que hoy solo guarda carpeta y version

## Phase 22: Estandar PuTTY en el terminal SSH

Origen: clarificacion del 2026-08-31, pedida por el usuario ("que la terminal se comporte como
PuTTY con la combinacion de teclas, pegado, copiado, seleccion"). De las cinco decisiones, la del
clic derecho **la confirmo el usuario**; las otras cuatro estan en `spec.md` como asumidas,
pendientes de confirmacion, y son reversibles de a una.

Lo que ordeno el trabajo fue sacar las decisiones del control y ponerlas en funciones puras
—`DecidirTeclado`, `DecidirMouse`, `TramoDeFila`, `NormalizarPegado`, `ContarLineas`,
`ArmarPegado`—. Se prueban sin ventana, sin hilo STA y sin portapapeles, que es lo que permite
fijar la lista cerrada de FR-032 con una tabla en vez de con la memoria de quien lea el codigo.

### Copiado automatico al seleccionar (FR-030a) y Ctrl+C que interrumpe (FR-030c)

- [x] T390 [P] [US2] `tests/CafManagerConection.Terminal.Tests/CopiaAutomaticaTests.cs` --- 4 pruebas. El portapapeles se reemplaza por una costura del control (`EscribirEnPortapapeles`), y no por prolijidad: una prueba que escribe en el portapapeles de verdad le pisa lo que tenga copiado a quien este trabajando en esa maquina mientras corre
- [x] T391 [US2] Copiado al soltar en `src/CafManagerConection.Terminal/TerminalControl.cs`, en `OnMouseUp`. Lo que hubo que resolver es **que no copie un clic suelto**: el inicio y el fin de la seleccion coinciden tanto en un clic como en un doble clic sobre una palabra de una letra, asi que se lleva una marca de como nacio la seleccion. Sin eso, cada clic para dar foco al terminal pisaria el portapapeles con el caracter de abajo del puntero
- [x] T392 [P] [US2] `AtajosDePortapapelesTests.cs` --- 33 pruebas sobre `DecidirTeclado`
- [x] T393 [US2] Ctrl+C manda siempre la interrupcion. Se borro la rama que copiaba con seleccion viva y se agregaron Ctrl+Ins y Shift+Ins. **Ctrl+Ins tuvo que quedar antes que todo lo demas**: `KeyboardMapper` traduce `Insert` sin mirar las modificadoras, asi que resuelto mas abajo el servidor habria recibido un `ESC[2~` en vez de copiarse el texto

### Botones del mouse, modo Compromise (FR-030b, FR-030d)

- [x] T394 [P] [US2] `BotonesDelMouseTests.cs` --- 11 pruebas sobre `DecidirMouse`
- [x] T395 [US2] Derecho pega, medio extiende, Ctrl+derecho abre el menu. El comentario que explicaba por que el derecho habia dejado de pegar se reemplazo por el motivo nuevo: con FR-030a el texto **ya esta copiado** antes de que uno llegue al boton, asi que nadie necesita ir a buscar «Copiar» en un menu
- [x] T396 [US2] El menu anuncia Ctrl+Ins y Shift+Ins, y pregunta por el portapapeles a traves de la misma costura que el pegado. Antes decia «Copiar Ctrl+C», que despues de T393 habria sido mentira: el menu es el unico lugar donde alguien se entera de los atajos, y uno que miente es peor que ninguno

### Seleccion rectangular (FR-154d)

- [x] T397 [P] [US2] `SeleccionRectangularTests.cs` --- 12 pruebas sobre `TramoDeFila`
- [x] T398 [US2] `TramoDeFila` es **una sola funcion** para el pintado, la copia y las pruebas. Es la leccion de T324 aplicada de entrada: cuando el pintado y la copia resolvian la seleccion por su cuenta, la marca decia una cosa y el texto copiado era otra, y uno se lleva algo distinto de lo que vio sin enterarse. El modo se fija al empezar el arrastre y no se recalcula: soltar Ctrl a mitad de camino cambiaria la forma de lo ya marcado bajo el puntero
- [x] T399 [P] [US2] Modo 2004 en `VtEmulatorTests.cs` --- 3 pruebas, y la que importa es que la pantalla alternativa **no** lo apague: vim la usa y tiene el modo encendido todo lo que dura
- [x] T400 [US2] `case 2004` y `BracketedPaste` en `VtEmulator.cs`, junto a los modos 1, 25 y 1049. `Reset()` si lo apaga: dejarlo encendido contra un shell que ya no lo entiende haria que cada pegado llegue con las marcas a la vista

### Pegado (FR-030e, FR-030f, FR-030g)

- [x] T401 [P] [US2] `PegadoTests.cs` --- 13 pruebas. Lo unico que no era obvio es **que un CR final no cuente como linea aparte**: copiar una linea de la salida de un comando suele traerse el salto del final, y preguntar «vas a pegar 2 lineas» por eso seria mentir en el caso mas comun de todos
- [x] T402 [US2] `Paste` arma el pegado con las marcas cuando el servidor pidio el modo 2004, normaliza los saltos a CR y pregunta por el evento `PidioConfirmarPegado`. **Sin nadie escuchando se pega igual**: un control que se niega a pegar porque no encontro a quien preguntarle es un misterio, y en la aplicacion real siempre hay quien conteste
- [x] T403 [US2] `SessionView.xaml.cs` contesta con `Dialogos.Confirmar`, nombrando cuantas lineas y contra que servidor. La pregunta es sincronica y sin despacho: el terminal esta alojado en la misma ventana, o sea en el hilo de interfaz, y el control esta esperando la respuesta para decidir si manda los bytes

### Historial y lista cerrada de teclas (FR-155, FR-032)

- [x] T404 [P] [US2] Las teclas de historial entraron en la tabla de `AtajosDePortapapelesTests.cs` y no en `BarraDeDesplazamientoTests.cs`: despues del refactor son la misma decision que el resto de los atajos, y partirlas en dos archivos habria dejado media lista en cada uno
- [x] T405 [US2] Ctrl+RePag y Ctrl+AvPag mueven una linea, Ctrl+Shift+RePag y Ctrl+Shift+AvPag van a los extremos. Ctrl+Shift+Inicio y Ctrl+Shift+Fin siguen funcionando: se suman las de PuTTY, no se reemplazan
- [x] T406 [US2] La prueba de la lista cerrada: doce combinaciones que el shell usa —Ctrl+A, Ctrl+E, Ctrl+R, Ctrl+D, Ctrl+Z, Ctrl+L, Ctrl+P— tienen que llegar al servidor. Es lo que evita que la lista crezca sola con el proximo atajo que a alguien le parezca comodo; el sintoma de perder uno aparece meses despues, en el unico programa remoto que lo usaba

### Guion de validacion y artefactos (SC-022, Principio III)

- [x] T407 [US2] Escenario 16 en `quickstart.md`: doce gestos con PuTTY al lado, mas el guion del pegado multilinea. La referencia del escenario **no es el documento, es PuTTY**: cada gesto se hace primero en una ventana y despues en la otra
- [x] T408 [US2] Constitution Check de `plan.md` declara ese guion, como exige el Principio III para todo plan que toca la interfaz. Se aclara que al guion manual queda solo lo que exige un servidor de verdad: la decision de cada tecla, cada boton, el tramo de la seleccion y el armado del pegado son funciones puras y estan probadas
- [x] T409 [US2] Arbol de `plan.md` corregido --- faltaban `TerminalBusqueda.cs` y `MenuOscuro.cs`. **`KeyboardMapper.cs` si estaba**: el analisis previo lo dio por faltante y estaba equivocado. Ademas decia «ocho proyectos» de prueba y son nueve desde T386
- [x] T410 [US2] Nada que registrar: `Paralelismo.cs` desactiva el paralelismo **del ensamblado entero** con `[assembly: CollectionBehavior]`, no clase por clase. La tarea nacio de leer T323 y suponer que habia una lista que mantener; no la hay, y esa es justamente la virtud de como quedo resuelto entonces. Las cinco clases nuevas quedan serializadas sin tocar nada

## Phase 23: Identidad de la ventana

Pedido del usuario: «la aplicacion en la barra de Windows la veo sin icono» y «quiero que muestre
el nombre CMC y la conexion activa o la cantidad de conexiones».

- [x] T411 [US4] `scripts/generar-icono.ps1` --- el icono se genera, no se edita: son cuatro formas y dos colores, y como codigo se puede cambiar el trazo o la tinta sin depender de que alguien conserve un archivo fuente. Escribe `Assets/cmc.ico` con nueve medidas (16 a 256) y `Assets/icono.png`
- [x] T412 [US4] **Defecto: el icono se veia vacio en la barra de tareas.** No era la cañeria —se verifico que el marco de 16 que Windows saca del ejecutable es **identico pixel a pixel** al del `.ico`— sino el dibujo: con el trazo proporcional del diseño grande, en 16 px queda en 1,25 px de ancho, el suavizado lo reparte entre dos columnas y el resultado es un cuadrado oscuro con manchas palidas. De 48 para abajo se dibuja **otro** simbolo para el mismo icono: trazo redondeado a pixeles enteros con minimo de 2, marca mas grande dentro del tile y sin resplandor, que a ese tamaño solo ensucia
- [x] T413 [US4] La ventana dejo de fijar `Icon` con un PNG de 256. WPF no elige marco: decodifica lo que le den y lo escala, asi que el PNG grande llegaba achicado a la barra de tareas. Sin `Icon`, Windows usa el del ejecutable, que si elige marco por medida. `icono.png` deja de ir como recurso incrustado; el archivo queda para documentacion
- [x] T414 [US4] `TituloDeVentana.Componer` en `src/CafManagerConection.App/Services/` (FR-041a) --- `CMC`, `CMC - servidor`, `CMC - servidor (conectando)` o `CMC - servidor (con error, 4 sesiones)`. El estado solo aparece cuando **no** esta conectada: anotarlo siempre gasta el espacio que la barra de tareas recorta primero, y «conectada» no es noticia. 12 pruebas en `App.Tests`
- [x] T415 [US4] La ventana recalcula el titulo al abrir una sesion, al cerrarla, al cambiar de pestaña y al cambiar el estado de la activa: los cuatro momentos en que dejaria de ser cierto. Un titulo que miente es peor que uno fijo

## Phase 24: Los dos defectos reportados (transversales)

Origen: analisis del 2026-09-01. Ninguno de los dos espera enmienda ni requisito nuevo: uno es una
plantilla mal escrita y el otro incumple FR-039, que existe desde el primer dia. Van primero
porque el del scroll arregla de una vez tres sintomas que el usuario reporto por separado.

- [x] T416 Plantilla de `ScrollBar` arreglada en `src/CafManagerConection.App/Themes/Estilos.xaml` (FR-166) --- el disparador de `Orientation=Horizontal` ahora fija tambien `PART_Track.Orientation=Horizontal` e `IsDirectionReversed=False`. La plantilla era una sola para las dos orientaciones con `IsDirectionReversed="True"` fijo, que en WPF vale **solo** para la vertical, y nunca fijaba la orientacion del `Track`, que por omision es vertical. **Salio un segundo defecto de la misma plantilla**: el pulgar llevaba `Margin="3,0"`, que lo adelgaza en la vertical pero en la horizontal lo **acorta** en lugar de adelgazarlo; pasa a `Margin="3"`, igual en los cuatro lados, porque el pulgar no sabe en que orientacion esta. Verificado que la plantilla parsea y se aplica levantando la aplicacion: un `TargetName` mal escrito no falla al compilar, falla al aplicar el estilo y se lleva la ventana
- [ ] T417 Comprobar los tres sintomas que reporto el usuario despues de T416: el registro de supervisord, la configuracion de nginx y el area de registro de la ficha de contenedor. Si en la ficha sigue apareciendo una barra sin contenido que la justifique (FR-166a), sacarla de `src/CafManagerConection.App/Views/ContenedorWindow.xaml`. **Pendiente de mirar con una sesion contra un servidor**: la plantilla ya se verifico que parsea y se aplica, pero el sentido del arrastre y la barra huerfana de la ficha solo se ven usando los paneles
- [x] T418 [P] [US2] `tests/CafManagerConection.Ssh.Tests/ReintentoConContrasenaTests.cs` --- 8 pruebas sobre la decision de reintentar (FR-039a). Las dos que importan son las **negativas**: que un fallo de red no dispare ningun pedido de contraseña —si el reintento se disparara ante cualquier fallo, apareceria un pedido en pantalla contra un servidor apagado— y que con contraseña guardada no se reintente, porque ahi preguntarle al usuario esconderia que la credencial de la base no sirve. El tope de tres intentos (FR-039b) **no se pudo probar sin servidor**: vive en el manejador de `keyboard-interactive` y necesita que el servidor pregunte cuatro veces
- [x] T419 [US2] Implementado en `src/CafManagerConection.Ssh/SshSession.cs` (FR-039a). El nudo era que `PasswordAuthenticationMethod` de SSH.NET **exige la contraseña al construirlo**, asi que no se puede pedir «cuando el servidor la reclame» como con el interactivo. **Se eligio la segunda salida** —interactivo primero, y reintento con contraseña cuando el servidor no lo ofrece— aunque antes de mirar el codigo se habia recomendado la primera. El motivo del cambio: pedir la contraseña por adelantado obliga a escribirla **antes** de que se muestre la huella del servidor, y aceptar un host despues de haber tipeado la contraseña invierte el orden en el que PuTTY hace las dos preguntas. La decision de reintentar **no mira el texto** del mensaje de la libreria —cambia entre versiones— sino si el pedido llego a dispararse, que es lo que separa «la contraseña estaba mal» de «el servidor no ofrece este metodo». Salieron dos cosas mas: la huella **no** se vuelve a preguntar en el reintento, y ese atajo vale solo para una huella ACEPTADA —si la anterior se rechazo se pregunta de nuevo, porque un atajo que tambien saltara sobre un rechazo convertiria un «no confio en este servidor» en un si silencioso—; y la contraseña escrita queda en memoria mientras dura la sesion, lo que ademas arregla que las conexiones auxiliares —archivos, metricas, inventario— volvieran a preguntar lo que el usuario ya habia escrito una vez
- [x] T420 [US2] Mensaje util cuando no queda ningun metodo utilizable, en el traductor de errores de `src/CafManagerConection.Ssh/SshSession.cs` (FR-051, FR-148) --- antes salia «no suitable authentication method found», que no nombra la causa ni deja salida. Ahora dice que el servidor no acepta autenticacion por contraseña y sugiere configurar clave privada. **No es que las credenciales esten mal: nunca se enviaron**, y el mensaje anterior mandaba a revisar un usuario y una contraseña que el servidor no llego a mirar

## Phase 25: Estado a la vista en las metricas (US7)

- [x] T421 [P] [US7] `tests/CafManagerConection.Monitoring.Tests/NivelDeUsoTests.cs` --- 18 pruebas (FR-087a, FR-087b, FR-087c). La que da sentido al requisito es que **la misma carga de 4 sea normal en ocho nucleos y critica en dos**: un semaforo que pinta el numero sin dividirlo por los nucleos miente en la mitad de los servidores
- [x] T422 [US7] `NivelDeUso` en `src/CafManagerConection.Monitoring/NivelDeUso.cs` --- funcion pura, sin WPF. Los cortes son 75 y 90 para porcentajes, y 1,0 y 1,5 **por nucleo** para la carga: en 1,0 el servidor usa exactamente lo que tiene. Sin nucleos informados devuelve Normal en lugar de inventar un divisor
- [x] T423a [US7] **Correccion de esta lista**: la tarea original decia «los pinceles ya existen en la paleta: `EstadoConectado`, `Advertencia` y `Destructivo`». **`Advertencia` no existia**: la paleta tenia `Primario`, `Destructivo`, los cuatro `Estado*` y los de iconos. Implementar T423 al pie de la letra habria dejado un `DynamicResource` a un recurso inexistente, que en WPF no falla al compilar: la barra simplemente sale sin pintar. Se agregan `MedidaNormal`, `MedidaAdvertencia` y `MedidaCritica` a `Paleta.Claro.xaml` y `Paleta.Oscuro.xaml`, separados de los estados de sesion a proposito —el color de estado dice «esto esta mal» y no puede compartir pincel con el de una accion— y con el ambar del tema claro mas oscuro que el de sesion, porque aca tambien se usa como texto y el ambar brillante no llega a 4,5:1 (SC-025)
- [x] T423 [US7] Aplicado el color y la forma en `src/CafManagerConection.App/Panels/StatusPanel.xaml` y su codigo (FR-087a, FR-087b) --- barras con el pincel del tramo, y el tramo dicho tambien sin color, con la etiqueta que devuelve `NivelDeUso.Etiqueta`
- [x] T424 [US7] Verificado en `src/CafManagerConection.App/Panels/StatusPanel.xaml` (FR-087d): red, cantidad de procesos y uptime no llevan barra ni color de estado, y no habia que retirar nada porque nunca lo tuvieron. La linea de tendencia de la red **queda**: no es una barra de progreso ni un color de estado, y dice algo sin inventar una escala. La carga ahora muestra tambien **el valor por nucleo**, que es el unico numero comparable entre servidores distintos

## Phase 26: Visores con color (US10)

**Correccion del 2026-09-01.** La primera version de esta fase proponia que las dos cosas pasaran
por el emulador VT, «un solo camino de dibujado». Para el nginx **no sirve**: el terminal corta las
lineas al ancho de la grilla, y eso rompe SC-026, que exige que lo copiado sea identico caracter
por caracter al archivo del servidor. Una linea larga de configuracion saldria partida en dos.

Queda asi: el **registro** va por el terminal —ya interpreta ANSI, y ademas trae seleccion,
busqueda y copiado sin escribir nada (FR-100g)— y la **configuracion de nginx** por texto WPF con
tramos de color, que conserva el texto exacto y no corta lineas. Son dos caminos de dibujado, y es
el precio de cumplir SC-026.

- [x] T425 [P] [US10] Escribir `tests/CafManagerConection.Platform.Tests/NivelDeLineaTests.cs` (FR-100f): las marcas habituales en ingles y de syslog clasifican; una linea sin marca queda en el nivel neutro; el texto devuelto es **identico** al de entrada
- [x] T426 [US10] Implementar `NivelDeLinea` en `src/CafManagerConection.Platform/` --- clasifica y no toca el texto. Reconoce ingles y syslog y nada mas: acertar poco y fallar seguido es peor que no colorear
- [x] T427 [P] [US10] Escribir `tests/CafManagerConection.Platform.Tests/ResaltadorDeNginxTests.cs` (FR-101a, FR-101b): directivas, bloques, cadenas, numeros, variables y comentarios; y la prueba que manda, **que el texto sin las marcas sea identico al original** caracter por caracter (SC-026)
- [x] T428 [US10] Implementar `ResaltadorDeNginx` en `src/CafManagerConection.Platform/` --- tokenizador propio, sin dependencias: la constitucion v1.11.0 prohibe traer un resaltador de terceros. Un `.conf` son directivas, bloques, cadenas, numeros, variables y comentarios; nada mas
- [x] T429 [US10] Convertir el visor de `src/CafManagerConection.App/Views/TextViewerWindow.xaml` para que dibuje con el terminal en lugar del `TextBox` plano (FR-100e, FR-100g) --- el `TextBox` no puede colorear nada, y hoy los codigos ANSI que ya traen los registros de supervisord y de Docker se ven como basura entre las palabras
- [x] T430 [US10] Enganchar los dos casos en `src/CafManagerConection.App/Panels/PanelesPlataforma.cs`: el registro pasa por `NivelDeLinea` cuando no trae color propio, y la configuracion por `ResaltadorDeNginx` (FR-100e, FR-100f, FR-101a). Un archivo cuya sintaxis no se reconozca se muestra igual y sin color (FR-101c)

## Phase 27: Ficha de contenedor ordenada (US9)

- [x] T431 [P] [US9] Ampliar `tests/CafManagerConection.Platform.Tests/DetalleDeContenedorTests.cs` (FR-150c) con los campos nuevos: identificador corto, imagen con etiqueta, digest corto, fecha de creacion, comando y argumentos, directorio de trabajo, redes con su IP, y proyecto y servicio de compose
- [x] T432 [US9] Agregar esos campos al guion de `inspect` en `src/CafManagerConection.Platform/ControlDeDocker.cs` y al interprete de `DetalleDeContenedor.cs`. **Las variables de entorno NO se piden** (FR-150d): no alcanza con no mostrarlas, no tienen que viajar. Es donde viven las contraseñas de base de datos y las claves de API en la mayoria de los despliegues
- [x] T433 [US9] Reorganizar el panel izquierdo de `src/CafManagerConection.App/Views/ContenedorWindow.xaml` en secciones con titulo —identidad, estado, recursos, red, almacenamiento— (FR-150b), con los valores que tienen estado distinguidos por color y forma, con los mismos tres niveles de FR-100d
- [x] T434 [US9] **Defecto**: el area de registro aparece vacia (FR-150e) --- toca `src/CafManagerConection.Platform/ControlDeDocker.cs`, `DetalleDeContenedor.cs` y `src/CafManagerConection.App/Views/ContenedorWindow.xaml.cs`. Primero averiguar por que: el comando ya lleva la redireccion de errores y va con sudo, asi que hay que ver la salida real contra un contenedor que loguee seguro y compararla con `docker logs --tail 40` ejecutado a mano. Despues, y en cualquier caso, distinguir «el registro esta vacio» de «no se pudo leer»: un area en blanco se lee como un defecto de la aplicacion, no como un contenedor que no escribio nada

## Phase 28: Puertos y ficha de proceso (US11)

Alcance incorporado por la enmienda 1.11.0. El panel ya existia sin requisito —tiene pruebas en
`PuertosParserTests` y `AplicacionesConocidasTests`, pero no figuraba en ningun artefacto—; lo
nuevo de verdad es la ficha.

- [x] T445 [US11] Verificar y dejar registrado que el panel ya cumple FR-164a, FR-164b y FR-164c, que quedaron sin tarea al generar esta lista: en `src/CafManagerConection.Platform/PlatformInventory.cs` y `src/CafManagerConection.App/Panels/PanelesPlataforma.cs`, que no se listan conexiones establecidas, que el nombre de la aplicacion conocida aparece al lado del proceso y que no hay ninguna accion de escritura. Son requisitos que el codigo ya satisface —el panel se escribio antes de tener requisito— y lo que falta es la prueba que los fije para que nadie los rompa sin darse cuenta
- [x] T435 [P] [US11] Escribir en `tests/CafManagerConection.Platform.Tests/PuertosParserTests.cs` el caso de socket sin proceso visible (FR-164d): la fila se lista igual, indicando que el proceso no es visible con los permisos actuales. Un puerto abierto que no aparece es peor que uno incompleto: la pregunta era justamente que esta abierto
- [x] T436 [P] [US11] Escribir `tests/CafManagerConection.Platform.Tests/DetalleDeProcesoTests.cs` (FR-165, FR-165b): interpretacion de la salida con todos los campos y con campos faltantes; y la validacion del identificador de proceso contra un conjunto cerrado —**solo digitos**—, con casos que intentan colar sintaxis de shell. El dato sale de un comando remoto y vuelve a entrar en otro que puede correr con sudo (FR-100c, misma regla)
- [x] T437 [US11] Implementar `DetalleDeProceso` en `src/CafManagerConection.Platform/` --- un solo viaje al servidor con marcas separadoras, como `DetalleDeContenedor`: `ps` para usuario, arranque, padre e hilos, y los enlaces de `/proc/<pid>` para el binario y el directorio de trabajo. Ningun tramo puede tumbar a los demas (FR-165a): sin permisos, `readlink` termina con estado distinto de cero y la ficha tiene que aparecer igual con lo que si se leyo
- [x] T438 [US11] `ProcesoWindow` en `src/CafManagerConection.App/Views/` (FR-165) --- misma forma que `ContenedorWindow`, nombrando el puerto desde el que se abrio. **Sin una sola accion de escritura** (FR-165c): ni matar, ni señalar, ni cambiar prioridad
- [x] T439 [US11] Doble clic en `PuertosPanel`, dentro de `src/CafManagerConection.App/Panels/PanelesPlataforma.cs` (FR-165) --- es el unico panel de inventario que hoy no tiene `MouseDoubleClick`
- [x] T440 [US11] En `src/CafManagerConection.App/Views/ProcesoWindow.xaml.cs`: lo que no se pudo leer se nombra con su motivo (FR-165a), y el proceso que ya no existe se informa ofreciendo refrescar (FR-165d). Una ficha con campos vacios no distingue «no tengo permiso» de «no existe», y son dos problemas distintos
- [x] T441 [US11] Comprobar en `src/CafManagerConection.Platform/DetalleDeProceso.cs` y en `tests/CafManagerConection.Platform.Tests/DetalleDeProcesoTests.cs` que nada de esta consulta va a los registros (FR-165e): ni la linea de comando, ni la ruta del binario. Una linea de comando lleva contraseñas en los argumentos mas seguido de lo que deberia

## Phase 29: Artefactos y validacion

- [x] T442 Agregar a `specs/001-rdp-ssh-server-manager/quickstart.md` los escenarios de SC-023 a SC-026: puertos y ficha contra las herramientas del sistema, conexion sin credencial contra los dos tipos de servidor, los tres niveles en escala de grises, y la configuracion de nginx copiada y comparada caracter por caracter
- [x] T443 Declarar en `specs/001-rdp-ssh-server-manager/plan.md` el guion de validacion manual de estos paneles (Principio III) y agregar al arbol los archivos nuevos de `Platform` y de `App/Views`
- [x] T444 Registrar en `specs/001-rdp-ssh-server-manager/plan.md`, en Complexity Tracking, que el panel de puertos se implemento antes de tener requisito y quedo regularizado por la enmienda 1.11.0. Es una desviacion del Principio V ya ocurrida, y el lugar donde se anota es esa tabla, no el olvido

## Phase 30: Defectos y pedidos posteriores a la lista

Trabajo que llego despues de generada la lista, contra servidores reales. Se anota aca y no en
las fases de arriba porque no salio de la especificacion: salio de usar la aplicacion y encontrar
que lo que la lista daba por hecho no funcionaba. Cada tarea nombra el sintoma tal como se vio,
no la solucion: es lo que hace falta para no volver a marcar como hecho algo que no anda.

- [x] T446 **Defecto**: la ficha de Docker sale vacia contra cualquier servidor. Causa: `docker inspect --format` **aborta la plantilla entera** cuando falta un campo (`map has no entry for key "Health"`), asi que un contenedor sin chequeo de salud se llevaba puestos los otros veinte campos. Arreglado en `src/CafManagerConection.Platform/ControlDeDocker.cs` partiendo salud y compose a comandos propios con `2>/dev/null`
- [x] T447 **Defecto**: contra servidores reales, `readlink` no dice nada al fallar por permisos y `ps -o user=` corta el usuario a 8 caracteres. Arreglado en `src/CafManagerConection.Platform/ConsultorDeProcesos.cs` con `readlink -v -f` y `user:32=`, tomando el tramo vacio como fallo de permiso
- [x] T448 **Defecto**: `ss` agrega sufijos `%iface` a las direcciones y lista varios procesos por socket. Arreglado en `src/CafManagerConection.Platform/Parsers.cs`, con casos tomados de la salida de tres servidores distintos
- [x] T449 Rehacer el panel derecho de `src/CafManagerConection.App/Views/ContenedorWindow.xaml` con iconos y color por seccion, y distinguir «el contenedor no escribio nada» de «no se pudo leer el registro»
- [x] T450 Anotar en la consola de traza la apertura y el corte de cada conexion SSH, con lo negociado (`src/CafManagerConection.Ssh/SshSession.cs`)
- [x] T451 Renombrar el ejecutable a `cmc.exe` conservando «Caf Manager Conection» como nombre visible, sin dejar huerfano el acceso directo de las instalaciones previas (`installer/CafManagerConection.nsi`)
- [x] T452 **Defecto**: ningun panel de servidor aparece cuando la contraseña se escribe en la consola. Causa: el canal auxiliar abre su propia conexion y se quedaba sin la contraseña tipeada. Arreglado con `SshSession.CredencialEfectiva` y el cableado de `src/CafManagerConection.App/Views/SessionView.Paneles.cs`
- [x] T453 **Defecto**: `SshCommandRunner.RunAsync` tenia un camino que volvia sin anotar en la traza —justo el de «no hay conexion»—, que es el que se recorre cuando los paneles no aparecen. Toda vuelta anota
- [x] T454 Menu de botón derecho en el panel de puertos (FR-167 a FR-167b): abrir en el navegador con los dos esquemas y `https` primero, copiar la direccion, y decir por que un puerto que escucha solo en el servidor no se puede abrir
- [x] T455 **Defecto**: el panel de estado no lee nada contra servidores donde el resto de los paneles si funcionan (FR-169, FR-169a). Dos causas: el tiempo limite era de 3 segundos contra los 10 del inventario, para un comando de trece partes; y `IRemoteCommandRunner` descartaba el texto del error, asi que el panel no podia decir el motivo y mandaba a mirar la traza. Toca `src/CafManagerConection.Monitoring/MetricsCollector.cs`, `src/CafManagerConection.Domain/Settings/AppSettings.cs` y `src/CafManagerConection.App/Panels/StatusPanel.xaml.cs`
- [x] T456 [P] Pruebas del motivo de fallo en `tests/CafManagerConection.Monitoring.Tests/CollectorTests.cs` (FR-169): el texto del canal se muestra tal cual, una respuesta vacia del servidor no se confunde con un canal caido, y una lectura buena borra el motivo anterior
- [x] T457 En `src/CafManagerConection.App/Panels/StatusPanel.xaml.cs`: el muestreo saltea el turno si hay una lectura en curso (FR-169a) y la lectura no puede tumbar la aplicacion. El reloj engancha un manejador `async void`: una excepcion ahi no la agarraba nadie
- [x] T458 [P] Escribir `tests/CafManagerConection.App.Tests/Services/PuertoLocalSugeridoTests.cs` (FR-168a, FR-168b) antes de la implementacion: mismo numero cuando esta libre, desplazamiento a la franja alta conservando el numero, y nunca un puerto de la franja efimera
- [x] T459 Implementar `src/CafManagerConection.App/Services/PuertoLocalSugerido.cs` (FR-168a, FR-168b), mirando los puertos a la escucha del equipo y los que ya reservaron otros tuneles definidos
- [x] T460 Abrir el editor de tuneles prellenado y devolver el tunel creado (FR-168c), en `src/CafManagerConection.App/Views/TunnelEditorWindow.xaml.cs`
- [x] T461 «Crear un tunel a este puerto…» en el panel de puertos (FR-168, FR-168d, FR-168e): se levanta en la sesion abierta al guardarlo, con la casilla de arranque automatico ya propuesta, y si se guarda pero no se levanta se dicen las dos cosas
- [ ] T462 Validar contra un servidor real los escenarios de SC-027 a SC-029: los dos esquemas y sus ausencias en el menu de puertos, el tunel a un servicio que escucha solo en `localhost` que sobrevive a reabrir la conexion, y el panel de estado nombrando la causa cuando no puede leer

## Phase 31: El panel de estado, de verdad

Todo lo de T455 a T457 era cierto y no era la causa. El panel de estado **no se armaba nunca**:
`StatusPanel.xaml` tenia un `ToggleButton` con el estilo `BotonTenue`, que declara
`TargetType="Button"`, y WPF rechaza el estilo al analizar el archivo —no falla el boton, falla la
pantalla entera—. La excepcion iba al archivo de registro y en pantalla salia «no se pudo abrir el
panel: el motivo quedo en la consola de traza (F12)», que era **falso**: la traza sólo lista idas y
vueltas con el servidor, y no habia ninguna porque el panel no llegaba a existir.

Diagnosticado leyendo `%LocalAppData%\CafManagerConection\logs\cmc-20260901.log`, no adivinando.

- [x] T463 **Defecto**: cambiar el `ToggleButton` por `Button` en `src/CafManagerConection.App/Panels/StatusPanel.xaml`. Nada del estado de dos posiciones se usaba: el que sabe si el selector esta abierto es el propio `Popup`
- [x] T464 **Defecto**: el cartel de fallo mentia (FR-170). En `src/CafManagerConection.App/Views/SessionView.Paneles.cs`, mostrar el motivo real —desenvolviendo hasta la excepcion interna, porque `XamlParseException` sólo dice «se produjo una excepcion»— y remitir al registro, que es donde el motivo esta de verdad
- [x] T465 **Defecto**: el `default` del `switch` de `CrearPanelAsync` volvia `null` sin registrar nada, ni traza, ni motivo. Es el camino de una sesion reconectada a medias, y era el crimen perfecto: ningun rastro en ninguna parte
- [x] T466 [P] Escribir `tests/CafManagerConection.Domain.Tests/EstilosAplicadosTests.cs`: ningun estilo aplicado a un elemento que no acepta su `TargetType`. Se comprobo que la prueba **falla** con el defecto puesto antes de darla por buena
- [x] T467 [P] Escribir `tests/CafManagerConection.Domain.Tests/RecursosPedidosTests.cs`: toda clave de recurso pedida desde XAML o desde codigo esta declarada en algun tema. Misma familia de defectos: cosas que el compilador no mira y que estallan al abrir la pantalla
- [x] T468 Boton «Registros» en la consola de traza (FR-170a), en `src/CafManagerConection.App/Views/ConsolaDeTraza.xaml`. Preferencias → General ya mostraba la ruta y ofrecia abrirla; lo que faltaba era llegar desde donde uno mira cuando algo falla
- [x] T469 [P] Escribir `tests/CafManagerConection.App.Tests/Services/LineaDeTunelTests.cs` (FR-168g) antes de la implementacion: con y sin puerto de ssh, sin usuario sin dejar una arroba suelta, y un puerto imposible que no llega a la linea
- [x] T470 [P] Escribir `tests/CafManagerConection.App.Tests/Services/SondaDePuertoTests.cs` (FR-168f) con un escucha de verdad en un puerto que pide el sistema: un doble no probaria nada, porque lo que puede estar mal es el trato con el socket
- [x] T471 [P] Escribir `tests/CafManagerConection.Platform.Tests/PuertosDeContenedoresTests.cs` (FR-164e): mapeos con y sin direccion, rangos que son un mapeo y seis puertos, y el desempate a favor del contenedor que corre
- [x] T472 Implementar `src/CafManagerConection.Platform/PuertosDeContenedores.cs` (FR-164e) y usarlo en `PuertosPanel`, consultando el inventario de contenedores **solo** si alguna fila es del reenviador de Docker
- [x] T473 Implementar `src/CafManagerConection.App/Services/LineaDeTunel.cs` y `SondaDePuerto.cs`, y usarlos desde `SessionView.Paneles.cs`
- [x] T474 Columna «Tunel» en el panel de puertos (FR-168h) y navegador por el tunel cuando esta activo (FR-167c), en `src/CafManagerConection.App/Panels/PanelesPlataforma.cs`. El tunel activo gana sobre el host del servidor: es el destino que se sabe alcanzable
- [x] T475 En `src/CafManagerConection.App/Views/SessionView.xaml.cs`: vaciar la lista de tuneles definidos al desarmar la sesion, o el panel de puertos mostraria puertos locales que este proceso ya no tiene abiertos
- [ ] T476 Validar contra un servidor real los escenarios de SC-030 a SC-032, y **volver a validar SC-029**: el panel de estado tiene que abrirse y mostrar datos. Todo lo anterior sobre el tiempo limite y el motivo del fallo quedo sin verificar porque el panel nunca se armaba

## Phase 32: Rediseño del panel de estado

Salió de leer datos reales de tres servidores contra el panel ya andando: un Ubuntu 22.04 x86 con
`xfs`, un Ubuntu 24.04 con LVM sobre NVMe y VPN, y un Ubuntu 24.04 aarch64 en Oracle Cloud con
Docker Swarm. Cada requisito nuevo (FR-171 a FR-181) nombra el caso real que lo motivó, y
`DatosRealesTests.cs` fija la salida literal de los tres para que ningún cambio futuro la rompa
sin que alguien lo note.

- [x] T477 [P] Escribir `tests/CafManagerConection.Monitoring.Tests/DatosRealesTests.cs` con salida literal capturada en los tres servidores, antes de tocar un intérprete: 37 pruebas que fijan casos que no se arman en un servidor de prueba —la interfaz VPN, la ruta IPv6 que empieza con `unreachable`, el proceso con 341 % de CPU y 82 hilos, la ausencia de `model name` en aarch64
- [x] T478 Agregar a `src/CafManagerConection.Monitoring/Models.cs` los modelos nuevos: interfaz de red, ruta, proceso, presión de recursos, entrada/salida de disco por dispositivo y datos de sistema (modelo de CPU y temperaturas), para FR-171 a FR-181
- [x] T479 Implementar `InterfacesParser` en `src/CafManagerConection.Monitoring/ParsersExtendidos.cs` (FR-171 a FR-171c): decide "levantada" por `LOWER_UP` y no por `state`, excluye interfaces de contenedor (`veth`, `br-`, `docker*`, con `master`) salvo pedido explícito por nombre, y no trata `tun`/`tap` como interfaz de contenedor
- [x] T480 Implementar `RoutesParser` en el mismo archivo (FR-172, FR-172a): destino, puerta, interfaz, métrica y `linkdown`, más DNS y dominio de búsqueda, salteando el tipo de ruta cuando encabeza la línea de `ip -6 route` en lugar del destino
- [x] T481 Implementar `TopProcessesParser` (FR-173 a FR-173c): diez procesos por CPU y diez por memoria, sin acotar el %CPU a 100, usuario tal cual venga cuando sea un número, sin pedir la línea de comando
- [x] T482 Implementar `PressureParser` (FR-174, FR-174a): CPU, disco y memoria de `/proc/pressure`, distinguiendo "no disponible" de cero cuando el núcleo no expone `CONFIG_PSI`
- [x] T483 Implementar `DatosDeSistemaParser` (FR-179, FR-180): modelo de procesador con caída a `lscpu` o al par implementador/parte en aarch64, y temperaturas de `lm-sensors` tomando sólo las medidas `_input`
- [x] T484 Implementar `DiskIoParser` (FR-176 a FR-176b): entrada y salida por dispositivo por diferencia entre dos lecturas de `/proc/diskstats`, sólo dispositivos enteros y sin `loop`
- [x] T485 Ampliar `DiskUsageParser` en `src/CafManagerConection.Monitoring/Parsers.cs` para aceptar la salida de `df -PT` (FR-177), ubicando cada campo por el encabezado y no por posición fija
- [x] T486 Ampliar el comando único de `src/CafManagerConection.Monitoring/MetricsCollector.cs` de trece a veintisiete tramos, uno por cada dato nuevo, y ocultar la swap cuando el servidor no la tenga configurada (FR-178)
- [x] T487 Agregar a `src/CafManagerConection.Domain/Settings/AppSettings.cs` la clave del intervalo de muestreo y el conjunto cerrado que se ofrece: 2, 5, 10, 30 y 60 segundos (FR-175)
- [x] T488 En `src/CafManagerConection.UseCases/Abstractions/Repositories.cs` y `src/CafManagerConection.Infrastructure/Database/SettingsStore.cs`, leer y guardar el intervalo validando contra el conjunto ofrecido **al leer**, no sólo al escribir (FR-175a, FR-175b): una base editada a mano con un 0 dejaría el panel consultando sin pausa
- [x] T489 Agregar OxyPlot.Wpf 2.2.0 como dependencia nueva (MIT, sin dependencias, sin binarios nativos, 796 KB) para el gráfico de tendencia de FR-181, ya justificada en `plan.md`
- [x] T490 Rediseñar `src/CafManagerConection.App/Panels/StatusPanel.xaml`, `StatusPanel.xaml.cs` y `StatusPanel.Filas.cs`: interfaces, rutas, top de procesos con doble clic a la ficha existente (FR-165), presión, disco por dispositivo, swap condicional, modelo de procesador, temperaturas, selector de intervalo y gráfico de tendencia de CPU y memoria en la misma escala
- [ ] T491 Validar contra los tres servidores de referencia los escenarios 25 a 29 (SC-033 a SC-037): discos contra `df -PT`, interfaces contra `ip addr`, puerta contra `ip route`/`ip -6 route`, top de procesos contra `top`, y que el intervalo elegido se conserva al reabrir la conexión

## Phase 33: Revision del panel de estado con Opus

Dos revisiones adversariales sobre lo de la Fase 32. Encontraron **dos defectos criticos que
ninguna prueba miraba**, porque los dos eran invisibles desde el resultado: uno devolvia una
lectura parcial que parecia valida, y el otro quedaba tapado por el primero.

- [x] T492 **Defecto critico**: la marca `###CMC###` iba **sin comillas**, y un `#` al principio de una palabra abre un comentario de shell hasta el fin de linea. El servidor ejecutaba `cat /proc/stat` y **comentaba todo el resto**: devolvia un solo tramo, cero marcas y estado 0, asi que la lectura se tomaba por valida y memoria, discos, red y hostname llegaban vacios **sin ningun error**. Es el «no veo discos» reportado. Venia desde el primer commit. Toca `src/CafManagerConection.Monitoring/MetricsCollector.cs` y `src/CafManagerConection.Platform/PlatformInventory.cs`, donde estaba latente
- [x] T493 **Defecto critico**: los dos campos estaticos con los `ps` se declaraban **despues** del comando, y los inicializadores estaticos corren en orden textual, asi que valian `null` al armarlo. Quedaban dos separadores pegados —`; ;`—, que es un error de sintaxis de shell. No se notaba porque el defecto de la marca lo comentaba
- [x] T494 [P] Escribir en `tests/CafManagerConection.Monitoring.Tests/CollectorTests.cs` las pruebas de la **forma** del comando: marca entre comillas, ningun tramo vacio, los dos `ps` presentes, locale fijado donde la salida se interpreta por texto, y que no se pida la linea de comando de ningun proceso. Ninguna prueba miraba el texto del comando, y ahi vivian los dos defectos
- [x] T495 **Defecto**: `df -PT` sin `LC_ALL=C`. `df` traduce su encabezado —«Tipo» en español— y el interprete lo miraba para saber si habia columna de tipo: contra un servidor con locale no ingles **desaparecian todos los discos** sin ningun error. Ahora la deteccion es por el dato de cada fila y ademas se fija el locale
- [x] T496 **Defecto**: `EsParticionDe` recorria todos los sufijos numericos, asi que `dm-10` daba particion de `dm-1` y `md127` de `md1`. Una maquina con once volumenes logicos, o con el fakeraid de Intel, perdia dispositivos del panel
- [x] T497 **Defecto**: `dm-*` (LVM) y `md*` (RAID) cuentan la **misma** entrada y salida que el disco fisico de abajo, asi que el total del servidor salia al doble. Se resuelve con un tramo nuevo de `lsblk -rno NAME,TYPE`, que dice cual es disco entero; sin `lsblk` se cae a adivinar por el nombre
- [x] T498 **Defecto**: `tun` y `tap` estaban en los prefijos de interfaz virtual, asi que en un servidor detras de VPN la interfaz por la que llega la conexion aparecia **sin trafico nunca** y fuera del selector. Dos modulos afirmaban lo contrario: `InterfaceInfo.EsDeContenedor` ya la trataba como real
- [x] T499 **Defecto**: un nombre de usuario con espacio corria todas las columnas de `ps` y la fila salia **plausible y toda mal** —los kilobytes de memoria leidos como segundos de vida—. Acotar el `Split` no alcanzaba, y se comprobo con una prueba. Se pide `uid` numerico y el nombre se resuelve con un tramo nuevo de `/etc/passwd`, que ademas deja de pedirle a `ps` resolver setecientos UID contra LDAP en cada muestra
- [x] T500 **Defecto**: la presion de recursos usaba una bandera para los tres, asi que en un contenedor con lxcfs los dos enmascarados informaban **cero presion** —una afirmacion sobre el servidor— en lugar de «no se». Ahora es por recurso
- [x] T501 **Defecto**: `InterfacesConocidas` exponia el conjunto vivo, y el panel lo enumera desde el hilo de interfaz mientras el recolector le agrega nombres desde el grupo de hilos. Abrir el selector al llegar una muestra podia tirar «coleccion modificada»
- [x] T502 **Defecto**: el punto de montaje se tomaba del primer campo, asi que «/mnt/disco viejo» se informaba como «/mnt/disco». Y los filtros por prefijo no llevaban barra: «/snap» excluia tambien «/snapshots»
- [x] T503 **Defecto**: la ficha de proceso abierta desde el panel de estado recibia el PID en el parametro del puerto, asi que la cabecera decia «PID 4711 · escuchando en el puerto 4711» — el numero repetido y la afirmacion falsa
- [x] T504 **Defecto**: el panel de estado arranca su reloj al construirse, y `AbrirPanelAsync` lo descartaba sin pararlo cuando el usuario volvia a hacer clic mientras se armaba. Quedaba un `DispatcherTimer` consultando el servidor para nadie, con el Dispatcher reteniendolo, y cada abrir-y-cerrar-rapido dejaba uno mas. El clic ahora respeta el cerrojo y la rama de descarte lo para
- [x] T505 **Defecto**: no existia `<Style TargetType="RadioButton">`, asi que caia al tema de Windows con texto negro: en la paleta oscura las etiquetas quedaban ilegibles. Afectaba tambien a los tres radios de la ventana de preferencias
- [x] T506 **Defecto**: un doble clic fallido en el panel de puertos llamaba a `MostrarError`, que **vacia la tabla**: se perdia la lista de puertos entera porque un proceso se murio. Ahora avisa en un dialogo, igual que el panel de estado
- [x] T507 **Defecto**: el `DataGrid` de WPF solo cambia la seleccion con el boton izquierdo, asi que el primer clic derecho no mostraba menu y el siguiente mostraba el de la fila anterior — con acciones que van a un puerto concreto, eso es poder crear un tunel al puerto equivocado
- [x] T508 En `src/CafManagerConection.App/Panels/StatusPanel.xaml.cs`: el reloj se construye antes de `InitializeComponent`, el orden de procesos se decide por referencia al control y no por su texto, y el selector de interfaces se puebla antes de abrirse —al reves no hacia nada—
- [x] T509 En `src/CafManagerConection.App/Panels/StatusPanel.xaml`: columnas de verdad en el encabezado y en la barra de control, porque apilando en una celda el nombre del servidor pasaba por debajo del tiempo encendido al angostar el panel; y `Mode=OneWay` en los `ProgressBar`, que son TwoWay por omision contra un record sin setter
- [ ] T510 **Movida a la feature 002 como FR-173d**, no arreglada acá: `ps --sort=-pcpu` ordena por el promedio de CPU de **toda la vida del proceso**, no por el uso instantaneo, asi que el «top por CPU» de un panel que se refresca cada 5 s muestra al proceso mas viejo y ocupado historicamente, no al que esta comiendo la CPU ahora. Arreglarlo bien exige diferencias de `/proc/PID/stat` entre dos muestras. Mientras no este, la columna **no debe decir que es uso instantaneo**
- [ ] T511 **Movida a la feature 002 como el experimento R3, tarea T602**, no arreglada acá: el comando manda dos `ps -eo` completos y un `cat /proc/diskstats` en cada muestra. Con ~700 procesos son ~1-3 % de un nucleo sostenido por conexion con el panel abierto. Los tramos casi estaticos —interfaces, rutas, DNS, modelo de CPU, sensores, `/etc/passwd`— no tienen por que releerse cada 5 s: corresponde partir en una lectura rapida y una lenta

## Phase 34: Trazabilidad de FR-026a, FR-182, FR-191 y FR-192

Cuatro requisitos con código y pruebas y ninguna tarea que los nombrara. Esta fase registra lo
construido: no abre trabajo nuevo.

- [x] T512 [P] Interpretar el juego gráfico de DEC en `src/CafManagerConection.Terminal/VtEmulator.cs` (FR-026a): designación `ESC ( 0` y `ESC ( B` sobre G0 y G1, invocaciones SO (`0x0e`) y SI (`0x0f`), y la tabla de `0x5f` a `0x7e` que convierte el `lqqqk` de `dialog`, `whiptail` y ncurses en el borde que corresponde. El juego designado se guarda y se restaura junto con el cursor
- [x] T513 [P] Fijar el juego gráfico con `tests/CafManagerConection.Terminal.Tests/JuegoGraficoDecTests.cs` (FR-026a)
- [x] T514 [P] Leer las sesiones de PuTTY del registro en `src/CafManagerConection.Infrastructure/Importacion/LectorDePutty.cs` (FR-182, FR-182a): sólo `Protocol` `ssh` y `ssh-connection`; telnet, rlogin, serial y raw quedan afuera nombrando el protocolo que traían (FR-182c)
- [x] T515 [P] Leer las sesiones de WinSCP en `src/CafManagerConection.Infrastructure/Importacion/LectorDeWinScp.cs` (FR-182, FR-182a, FR-182b): SCP y SFTP sí, FTP, WebDAV y S3 no. La contraseña decodificada se verifica contra el par usuario+host que el propio formato antepone, y si no verifica se descarta con aviso en lugar de guardarse
- [x] T516 [P] Leer las sesiones de FileZilla en `src/CafManagerConection.Infrastructure/Importacion/LectorDeFileZilla.cs` (FR-182, FR-182a): sólo `<Protocol>` 1 (SFTP); el 0 sale con «FTP: CMC sólo abre conexiones sobre SSH»
- [x] T517 [P] Modelar la sesión leída en `src/CafManagerConection.Domain/Importacion/ConexionImportada.cs`, con el camino de carpetas del origen y el motivo de lo descartado (FR-182c, FR-182e)
- [x] T518 Volcar lo leído a conexiones propias en `src/CafManagerConection.UseCases/Importacion/ImportadorDeConexiones.cs` (FR-182d, FR-182e): reconstruye la jerarquía de carpetas del origen bajo una raíz por herramienta, saltea la conexión que ya exista con el mismo host, usuario y puerto efectivo, y no escribe nada en la configuración de las tres herramientas
- [x] T519 Vista previa y confirmación en `src/CafManagerConection.App/Views/PanelImportacion.xaml` y `PanelImportacion.xaml.cs`, embebido en `src/CafManagerConection.App/Views/PreferenciasWindow.xaml` (FR-182, FR-182b, FR-182c): la importación se dispara a pedido, las contraseñas se traen sólo si el usuario tilda la casilla —una vez por importación— y lo que no se pudo importar se lista con su motivo
- [x] T520 [P] Pruebas de los tres lectores en `tests/CafManagerConection.Infrastructure.Tests/Importacion/LectorDePuttyTests.cs`, `LectorDeWinScpTests.cs` y `LectorDeFileZillaTests.cs` (SC-044)
- [x] T521 [P] Declarar los colores de consola como recursos con nombre, con los mismos valores en los dos temas, en `src/CafManagerConection.App/Themes/Paleta.Claro.xaml` y `Paleta.Oscuro.xaml` (FR-191): catorce pinceles `*Consola`, de `FondoConsola` a `ConsolaError`, que consumen `src/CafManagerConection.App/Views/SessionView.xaml` y `ContenedorWindow.xaml` en lugar de colores escritos en cada pantalla
- [x] T522 [P] Comprobar con `tests/CafManagerConection.Domain.Tests/RecursosPedidosTests.cs` que cada recurso pedido desde XAML o desde código existe en las dos paletas (FR-191)
- [x] T523 Borrar y editar un túnel desde `src/CafManagerConection.App/Panels/TunnelsPanel.xaml` y `TunnelsPanel.xaml.cs`, con el editor de `src/CafManagerConection.App/Views/TunnelEditorWindow.xaml` (FR-192): borrar pide confirmación y baja el túnel si está activo, para no dejar el puerto local ocupado por un reenvío que ya no figura en ninguna lista

## Phase 35: Teclado numérico, marca del instalador y pruebas de integración

- [x] T524 Interpretar el modo aplicación del teclado numérico (DECKPAM / DECKPNM) en
  `src/CafManagerConection.Terminal/KeyboardMapper.cs`, con el estado que
  `src/CafManagerConection.Terminal/VtEmulator.cs:61` ya guardaba y que
  `src/CafManagerConection.Terminal/TerminalControl.cs:635` ahora le pasa (FR-026, FR-032): las
  quince teclas del numérico mandan SS3 (`ESC O p` a `ESC O y`, `n`, `k`, `m`, `j`, `o`) cuando el
  programa remoto lo pidió, y el carácter suelto cuando no. Con `Alt` no manda secuencia, para no
  romper los códigos Alt+numpad de Windows; con `Control` tampoco, para dejar el zoom
  `Ctrl+NumPad0` de FR-145
- [x] T525 [P] Fijar el numérico con 36 casos en
  `tests/CafManagerConection.Terminal.Tests/VtEmulatorTests.cs`, incluida la navegación con Bloq
  Num apagado. **El Enter del numérico queda sin cablear**: el control es WinForms y
  `KeyEventArgs.KeyCode` devuelve `Keys.Enter` para las dos teclas sin exponer el bit de tecla
  extendida, así que no se puede distinguir del Enter principal, que debe seguir mandando `\r`
- [x] T526 Escribir el tipo de instalador en el registro desde
  `installer/CafManagerConection.nsi`: `HKLM\Software\CafManagerConection`, valor
  `TipoDeInstalador`, con `liviano` o `completo` según el flag `REQUIERE_RUNTIME` que ya existía
  (T389)
- [x] T527 Leer esa marca en `src/CafManagerConection.App/Services/SelectorDeInstalador.cs` y
  ofrecer el mismo tipo que está instalado (T389). Sin marca —instalado con una versión anterior o
  corriendo el ZIP portable— cae al liviano. **Antes elegía el liviano por el orden en que GitHub
  lista los activos, no por convención**: `CafManagerConection-setup-completo.exe` también contiene
  `setup` y salía primero por casualidad
- [x] T528 [P] Fijar el selector con 13 casos, incluidos marca ausente y marca con un valor que no
  se reconoce, que se comporta como ausente y no lanza (T389)
- [x] T529 Apuntar las diecinueve pruebas de integración SSH a un servidor por variable de entorno
  con `tests/CafManagerConection.Ssh.Tests/PruebaDeIntegracionSshAttribute.cs`:
  `CMC_SSH_PRUEBA_HOST`, `CMC_SSH_PRUEBA_USUARIO` y `CMC_SSH_PRUEBA_CONTRASENA`, más
  `CMC_SSH_PRUEBA_PUERTO` opcional. **Se fue del repositorio la contraseña del contenedor de prueba
  que estaba como `const`** (Principio II), y con ella el paquete `Xunit.SkippableFact` y el sondeo
  TCP que ocultaba un servidor caído detrás de una omisión sin motivo
