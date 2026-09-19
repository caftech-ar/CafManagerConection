## Why

La auditoría del modelo de datos del 17/09/2026 (seis revisiones independientes cruzadas contra el
perfil real de `cmc.db`) dejó 27 problemas consolidados, de los que el usuario aceptó 26 y rechazó
uno. Hay columnas que se escriben y nadie lee, dos fuentes de verdad para el mismo dato, un guardado
que pisa ediciones ajenas, un motor de migraciones que no puede recrear tablas y aparta la base ante
cualquier error, y una decena de defectos menores de validación e interfaz. Conviene resolverlo ahora, con una sola
instalación y una base chica, antes de que el esquema se congele con usuarios afuera.

## What Changes

- **Motor de migraciones**: puede recrear tablas sin perder filas hijas (claves foráneas apagadas
  fuera de la transacción y comprobadas antes de confirmar), sólo aparta la base ante corrupción
  real (códigos `SQLITE_CORRUPT` y `SQLITE_NOTDB`) y avisa antes de crear una nueva; se prueban las
  migraciones desde bases v1, v2 y v3.
- **Migración 004** (**BREAKING** para bases en `user_version = 3`, irreversible como todas):
  - `folder_settings`: backfill del usuario compartido a las tres columnas por protocolo donde
    falten, baja de `username` y `port`, `CHECK` de rango en `rdp_port`/`ssh_port`/`web_port`, baja
    de `rdp_fit_to_tab`, `CHECK json_valid` en `custom_fields`.
  - `connection_folders`: `CHECK` de no vacío en `name`.
  - `connections`: baja de `documentation_url` y `last_connected_at`; nueva columna `es_rapida`
    poblada desde la clave reservada de `custom_fields`; `folder_id` de cada hija igualado al de su
    padre; `CHECK json_valid` sobre `custom_fields` (el inválido previo se anula); `CHECK` de no
    vacío en `name` y `host`; `CHECK IN (0,1)` en `is_favorite`.
  - `rdp_settings`: baja de `fit_to_tab`; `start_full_screen` pasa a `abre_en_ventana_propia` con
    `CHECK IN (0,1)`.
  - `ssh_settings`: baja de `encoding`.
  - `web_settings`: `CHECK` de no vacío en `url`, `CHECK IN (0,1)` en `private_window`.
  - `ssh_tunnels`: baja de `sort_order`, índice reducido a `connection_id`, `CHECK IN (0,1)` en
    `auto_start`.
  - `connection_history`: `CHECK` que ata `outcome` con `failure_reason`, anulando antes el motivo
    de las filas `Success`.
  - `tags`: fechas existentes llevadas al formato ISO.
  - `vault`: baja de `creado_en`.
  - Índices `ix_connections_search` e `ix_connections_favorite` eliminados.
- **Fechas de registro**: `created_at`/`updated_at` vuelven a la entidad, `updated_at` se toca sólo
  ante un cambio real, formato ISO unificado (semilla de `tags` incluida) y se muestran en la
  interfaz. `Etiqueta` gana `CreatedAt`/`UpdatedAt`.
- **Última conexión** derivada del historial; la apertura de una entrada Web registra evento.
- **Conexiones hijas**: la carpeta de una hija se deriva siempre de su padre; mover un padre arrastra
  a las hijas.
- **Guardado de conexión**: la sesión persiste la huella SSH y la bandera de ventana propia con
  métodos de una columna; las tablas de protocolo se actualizan con `ON CONFLICT DO UPDATE`.
- **Historial**: `Success` nunca lleva motivo de falla y `Failed` siempre lleva uno; alta y poda en
  una transacción; la ventana muestra «últimos N de M»; se retira el camino por conexión sin uso.
- **Campos propios**: la marca de conexión rápida pasa a columna; un texto ilegible se registra al
  interpretarlo; las carpetas comparan claves sin distinguir mayúsculas.
- **Importación**: sin nota de plantilla; deduplicación por el protocolo real; regresión de que el
  dominio sólo se puebla para RDP.
- **Validación**: un solo máximo de keep-alive (3600) en dominio, UI y validador, también para
  carpetas; error de largo de notas mostrado en la pestaña Avanzado; puerto por omisión
  colapsado a nulo; tooltip sin puerto para Web; ventana privada deshabilitada sin navegador.
- **Enums y ajustes**: baja de `CredentialMissing`, `SinSombraDelCursor` y
  `SettingKeys.ConnectionTimeoutSeconds`; modo de pestaña mapeado por valor y no por posición;
  claves `updates.*` en `SettingKeys`.
- **Etiquetas**: aviso al borrar una de fábrica; guardado con manejo de error; baja de `EsValida` y
  `Quitar`.
- **Persistencia explícita**: columnas listadas en todas las consultas, mapeo de Dapper con
  `MatchNamesWithUnderscores` activado en Infrastructure, `TryParse` con reserva en los cuatro
  `Enum.Parse`, ida y vuelta ampliada a todas las columnas que pasan por el repositorio.
- **Copias**: el fallo de la copia de arranque se muestra en la barra de estado.
- **Código sin efecto**: se van `FolderSettings.IsEmpty`, `ITunnelRepository.GetAllAsync` e
  `IConnectionHistoryRepository.GetForConnectionAsync`; `FolderRow.ToDomain` deja de delegar en una
  segunda función; `AjustesDeActualizacion` pierde el parámetro `Origen` y sus guardas inalcanzables.

Rechazado y fuera de alcance: fusionar `folder_settings` dentro de `connection_folders`.

## Capabilities

### New Capabilities

- `motor-de-migraciones`: recreación de tablas, recuperación de corrupción acotada y pruebas desde
  versiones anteriores.
- `cierre-de-usuario-y-puerto-compartidos`: una sola fuente de verdad por protocolo en la carpeta.
- `fechas-de-registro`: `created_at`/`updated_at` fieles, en un formato y visibles.
- `ultima-conexion-derivada`: la última conexión sale del historial, incluidas las entradas Web.
- `herencia-de-conexiones-hijas`: una hija hereda siempre de la carpeta de su padre.
- `guardado-parcial-de-conexion`: la sesión no pisa ediciones ajenas; los ajustes de protocolo no se
  borran al guardar.
- `historial-coherente`: resultado y motivo consistentes, transacción, resumen honesto.
- `campos-propios-validados`: JSON válido por esquema, fallo registrado, conexión rápida en columna.
- `ventana-propia-rdp`: la bandera dice lo que guarda y se puede apagar.
- `importacion-fiel`: sin nota de plantilla, deduplicación por protocolo, dominio sólo para RDP.
- `validacion-en-el-editor`: un solo límite de keep-alive, errores en su pestaña, puerto y ventana
  privada coherentes.
- `enums-y-ajustes-saneados`: miembros sin productor fuera, mapeo por valor, claves centralizadas.
- `etiquetas-de-fabrica`: aviso al borrar y guardado con manejo de error.
- `restricciones-del-esquema`: `CHECK` de booleanos, textos no vacíos, puertos por protocolo e
  invariante del vault.
- `persistencia-explicita`: columnas nombradas, mapeo declarado, lectura tolerante, ida y vuelta
  completa.
- `copias-visibles`: una copia que falla se ve.
- `poda-de-columnas-muertas`: columnas, índices y miembros sin lector eliminados.

### Modified Capabilities

Ninguna en `openspec/specs/`, que sigue vacío. El requisito «Las carpetas existentes conservan su
comportamiento» del cambio `usuario-y-puerto-por-protocolo` queda superado por
`cierre-de-usuario-y-puerto-compartidos`; se anota en su `design.md` al implementar.

## Impact

- `CafManagerConection.Domain`: `Connection`, `Folder`, `FolderSettings`, `ProtocolSettings`,
  `SshTunnel`, `Etiqueta`, `SessionState`, `OpcionesDePantallaRdp`, `AppSettings`.
- `CafManagerConection.UseCases`: `ConnectionService`, `FolderService`, `SettingsResolver`,
  `EffectiveSettings`, `SessionManager`, `ConnectionValidator`, `ImportadorDeConexiones`,
  `Repositories` (puertos), `AjustesDeActualizacion`.
- `CafManagerConection.Infrastructure`: `DatabaseInitializer`, `Migration004`, los siete
  repositorios, `Serializacion`, `SqliteConnectionFactory`, `RepositorioDelVault`.
- `CafManagerConection.App`: `ConnectionEditorWindow`, `FolderSettingsWindow`, `EtiquetasWindow`,
  `PreferenciasWindow`, `ConnectionHistoryWindow`, `TunnelEditorWindow`, `SessionView`,
  `MainWindow`, `MainWindow.Acciones`, `NodoArbol`, `WebLauncher`, `CompositionRoot`.
- Pruebas: `DatabaseInitializerTests`, `CatalogoIdaYVueltaTests`, `ConnectionRepositoryTests`,
  `FolderRepositoryTests`, `FlujoCompletoTests`, `SettingsResolverTests`,
  `OpcionesDePantallaHerenciaTests`, `HistorialDeConexionesTests`, `SessionManagerTests`,
  `IdentidadDeWindowsTests`, `NodoArbolTests`, `LectorDeRdmTests`, `CatalogoDeEtiquetasTests`,
  `EtiquetasDeFabricaTests`, `AjustesDeActualizacionTests`, `PaletaIconosTests`.
- Datos existentes: la migración 004 hace dentro de su transacción el backfill de usuario de
  carpeta, la carpeta de las hijas, la marca de conexión rápida, el motivo de las filas `Success` y
  el formato de las fechas de `tags`; correrla contra `cmc.db` pide confirmación explícita, con los
  conteos de las filas que toca medidos antes con `SELECT`. La limpieza de las notas de importación
  es opcional y aparte.
- Compuertas: `CoherenciaDelCodigoTests` prohíbe citar `archivo:línea` en el código;
  `SinDatosRealesTests` prohíbe conteos del parque en pruebas y en `openspec/`. Se citan símbolos y
  se usan datos ficticios.
