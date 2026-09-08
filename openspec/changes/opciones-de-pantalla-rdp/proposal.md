## Why

Una conexión RDP a un servidor sin SSH, con un usuario sin privilegios y con sólo el 3389 abierto no
admite ningún panel del estilo SSH: no hay canal para pedirle procesos, puertos ni servicios al
servidor. Lo único que se puede mejorar es lo que vive dentro de la propia conexión y del lado del
cliente. Hoy `PlanDeSesionRdp` configura 20 propiedades del control y deja fuera cuatro ajustes que
no piden puerto ni privilegio y que cambian de forma directa cómo se ve y cómo entra el usuario:
rendimiento sobre enlaces lentos, escala/DPI, resolución que sigue al tamaño de la pestaña, y abrir
un programa en vez del escritorio entero.

## What Changes

- Se agrega un **ámbito nuevo** al plan de sesión —`AmbitoDeRdp.Extendida`— para alcanzar
  `IMsRdpExtendedSettings`, que no es el dispatch por defecto del control y hoy `TrySetOn` no llega.
  Conserva el diagnóstico existente: un ajuste que el control no soporte cae en
  `PropiedadesNoAceptadas`.
- **Rendimiento**: `PerformanceFlags` (fondo, temas, animaciones, suavizado, arrastre de ventana) y
  `NetworkConnectionType`, elegibles por conexión y heredables por carpeta.
- **Escala/DPI**: `DesktopScaleFactor` y `DeviceScaleFactor` vía el ámbito extendido, para que el
  texto del servidor no quede diminuto en pantallas de alta densidad.
- **Resolución dinámica**: se desdobla la única casilla `FitToTab`, que hoy mezcla dos conductas
  opuestas —escalar los píxeles (`SmartSizing`) y renegociar la resolución real
  (`UpdateSessionDisplaySettings`)—, en dos intenciones separadas y excluyentes.
- **Programa inicial**: `StartProgram` + `WorkDir` (ámbito Asegurados) para abrir una aplicación al
  conectar. Opcionalmente `RemoteProgramMode` para RemoteApp, que **sólo funciona si el servidor lo
  publicó** y falla con mensaje claro si no.

Fuera de alcance en este cambio: multimonitor (necesita `IMsRdpClientNonScriptable5` y elección de
monitores), reconexión automática del control (reescribiría el watchdog de `VigilarEstado`) y captura
de pantalla del control.

## Capabilities

### New Capabilities

- `rendimiento-de-rdp`: qué banderas de rendimiento y qué tipo de red se aplican a la sesión, cómo
  se eligen y se heredan, y qué pasa si el control no acepta una.
- `escala-de-rdp`: cómo se fija la escala del escritorio y del dispositivo, por qué interfaz del
  control viaja, y qué pasa cuando esa interfaz no existe en la versión instalada.
- `resolucion-dinamica-de-rdp`: las dos conductas del ajuste de tamaño —escalar píxeles vs.
  renegociar resolución—, cuál excluye a cuál y cuándo el servidor no soporta la renegociación.
- `programa-inicial-de-rdp`: qué se abre al conectar en lugar del escritorio, la diferencia entre
  programa inicial y RemoteApp, y el fallo cuando el servidor no publicó el RemoteApp.

### Modified Capabilities

Ninguna: `openspec/specs/` está vacío. El desdoble de `FitToTab` cambia comportamiento pero no hay
spec previa que modificar; queda descrito en `resolucion-dinamica-de-rdp`.

## Persistencia (decidido en la conversación)

Los ajustes nuevos NO abren una columna por ajuste. Viven como campos reservados `cmc:` dentro de
`custom_fields`, la columna de texto que la conexión ya serializaba. Para que se hereden por carpeta,
`folder_settings` gana **una** columna `custom_fields` (ADD COLUMN, no destructivo), y el motor de
migraciones pasa a ser incremental (`user_version` 1 → 2). Es la única modificación de esquema, y fue
autorizada explícitamente.

## Impact

- `CafManagerConection.Rdp`: `AmbitoDeRdp` suma `Extendida`; `PlanDeSesionRdp.Para` suma los ajustes
  nuevos; `RdpClientHost` gana `IMsRdpExtendedSettings` y `TrySetExtended`; `RdpSessionRequest`
  reemplaza `FitToTab` por `ModoDeTamano` y suma los campos nuevos. `Resize` renegocia sólo en el modo
  correspondiente; `MotivoDeDesconexion` reinterpreta un corte con RemoteApp como «no publicado».
- `CafManagerConection.Domain`: nuevo `OpcionesDePantallaRdp` (enums, struct, validación de escalas) y
  los helpers de `AjustesReservados`; `FolderSettings` suma `CustomFields`. `RdpSettings` queda igual.
- `CafManagerConection.UseCases`: `EffectiveSettings` y `SettingsResolver` resuelven las opciones por
  la cascada de campos reservados; el modo cae en el viejo `FitToTab` si nadie lo define.
- `CafManagerConection.Infrastructure`: `Migration002` suma `custom_fields` a `folder_settings`;
  `DatabaseInitializer` aplica migraciones en orden; `FolderRepository` lee/escribe la columna.
  **Cambio de esquema** (una columna nueva, sin migración destructiva).
- `CafManagerConection.App`: `ConnectionEditorWindow` y `FolderSettingsWindow` suman los controles y su
  visualización de herencia; `SessionView` arma el pedido con las opciones resueltas.
- Sin puertos nuevos, sin privilegios nuevos, sin dependencias externas.
