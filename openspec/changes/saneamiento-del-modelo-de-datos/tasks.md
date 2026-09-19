# Tareas

La suite del proyecto tocado se corre al cerrar cada grupo, no entre tareas: el grupo 2 deja el
esquema y los repositorios incompatibles hasta su última tarea. Toda operación sobre `cmc.db`
(correr la migración contra la base real, cualquier `UPDATE`) se confirma con el usuario antes, por
operación.

## 1. Motor de migraciones (P-09)

- [x] 1.1 `DatabaseInitializer.Migrate`: `PRAGMA foreign_keys = OFF` antes de `BeginTransaction()`;
  `PRAGMA foreign_key_check` antes del `Commit`, abortando si devuelve filas;
  `PRAGMA foreign_keys = ON` en un `finally`.
- [x] 1.2 `DatabaseInitializer.InitializeAsync`: apartar la base sólo con `SqliteErrorCode` 11 o 26;
  relanzar el resto.
- [x] 1.3 `MainWindow`: mostrar el aviso de base apartada antes de cargar el árbol, con la opción de
  restaurar desde `ServicioDeCopias.Listar`.
- [x] 1.4 `DatabaseInitializerTests`: migrar desde una base creada sólo con `Migration001` y desde
  una con 001+002, con una fila de datos que sobrevive; un error que no es corrupción no aparta; NOTADB
  sí aparta; una migración de prueba que recrea una tabla con hijas conserva las hijas; una que deja
  huérfanas se revierte y `user_version` no cambia.
- [x] 1.5 Suite de `Infrastructure.Tests` en verde.

## 2. Migración 004, dominio y persistencia (P-01, P-02, P-03, P-05, P-06, P-07, P-08, P-10, P-11, P-13, P-14, P-16, P-21, P-22, P-23, P-26)

Esquema:

- [x] 2.1 `Migration004_Saneamiento`: `DROP INDEX ix_connections_search`, `ix_connections_favorite`;
  `DROP COLUMN ssh_settings.encoding`; `UPDATE tags` para llevar `created_at` y `updated_at` al
  formato ISO con `Z` donde no tengan `T`. `vault.creado_en` y el `Enum.Parse` de
  `RepositorioDelVault` quedan fuera: la sesión no tiene permiso de lectura sobre `Credentials/`,
  y bajar la columna sin poder ajustar su `INSERT` rompería el alta de clave maestra.
- [x] 2.2 Recrear `connection_folders` (`CHECK TRIM(name) <> ''`) y `folder_settings` sin `username`,
  `port` ni `rdp_fit_to_tab`, con `CHECK` 1..65535 en `rdp_port`/`ssh_port`/`web_port` y
  `CHECK json_valid` en `custom_fields`; el `INSERT … SELECT` usa `COALESCE(rdp_username, username)`
  y equivalentes para los seis campos; recrear `ix_folders_parent` e `ix_folder_settings_tag`.
- [x] 2.3 Recrear `connections` sin `documentation_url` ni `last_connected_at`, con `es_rapida`
  (`INTEGER NOT NULL DEFAULT 0 CHECK IN (0,1)`), `CHECK json_valid` en `custom_fields`,
  `CHECK TRIM(name) <> ''`, `CHECK TRIM(host) <> ''`, `CHECK is_favorite IN (0,1)`; el
  `INSERT … SELECT` aplica los `CASE` del design para `es_rapida`, `custom_fields` y `folder_id` de
  las hijas; recrear `ix_connections_folder`, `ix_connections_parent`, `ix_connections_tag`.
- [x] 2.4 Recrear `rdp_settings` sin `fit_to_tab` y con `abre_en_ventana_propia INTEGER NOT NULL
  DEFAULT 0 CHECK IN (0,1)` copiada desde `start_full_screen`; `web_settings` con
  `CHECK TRIM(url) <> ''` y `private_window IN (0,1)`; `ssh_tunnels` sin `sort_order`, con
  `auto_start IN (0,1)` e `ix_tunnels_connection(connection_id)`; `connection_history` con
  `failure_reason` anulado en `Success` y los dos `CHECK` outcome ↔ motivo, más
  `ix_history_connection`.
- [x] 2.5 `DatabaseInitializer.LatestVersion` y el arreglo `Migraciones` incluyen la 004.

Dominio:

- [x] 2.6 `FolderSettings`: quitar `UserName`, `Port`, `RdpFitToTab`, `IsEmpty` y los fallbacks de
  `UsuarioDe`/`PuertoDe`; `CustomFields` con comparador `OrdinalIgnoreCase`.
- [x] 2.7 `RdpSettings`: quitar `FitToTab`; `StartFullScreen` pasa a `AbreEnVentanaPropia`.
  `SshSettings`: quitar `Encoding`. `Connection`: quitar `DocumentationUrl`,
  `ValidateDocumentationUrl` y `LastConnectedAt`; agregar `EsRapida`; `UpdatedAt` con `init` y
  `Touch()` sólo en mutadores que cambian estado. `Folder`: idem `UpdatedAt`. `SshTunnel`: quitar
  `SortOrder`. `Etiqueta`: agregar `CreatedAt` y `UpdatedAt`. `Domain.Settings.Limites` con
  `MaxKeepAliveSeconds = 3600`.
- [x] 2.8 `EffectiveSettings` y `SettingsResolver`: quitar `FitToTab`/`ResolvedFitToTab`;
  `ResolvedModoDeTamano` con reserva `EscalarPixeles`.

Persistencia:

- [x] 2.9 `Infrastructure/Database/MapeoDeDapper` con `[ModuleInitializer]` que activa
  `DefaultTypeMap.MatchNamesWithUnderscores`; DTOs de los cinco repositorios a `PascalCase`; todas
  las consultas con columnas listadas; los cuatro `Enum.Parse` (`ConnectionRepository` ×2,
  `FolderRepository`, `RepositorioDelVault`) a `TryParse` con reserva y registro.
- [x] 2.10 `ConnectionRepository`: mapear `UpdatedAt` de vuelta; `UpdateAsync` con
  `ON CONFLICT(connection_id) DO UPDATE` en las tres tablas de protocolo; nuevos
  `SetKnownHostFingerprintAsync` y `SetAbreEnVentanaPropiaAsync`; quitar `SetLastConnectedAsync`;
  leer y escribir `es_rapida`; `IConnectionRepository` actualizado.
- [x] 2.11 `FolderRepository`: mapear `UpdatedAt` de vuelta; `FolderRow.ToDomain` deja de delegar en
  `Crear` (una sola función); quitar `username`, `port` y `rdp_fit_to_tab`.
- [x] 2.12 `TagRepository`: seleccionar y mapear las fechas, escribirlas con `FolderRepository.Iso`;
  semilla de `Migration001` con literales ISO fijos. `TunnelRepository`: sin `sort_order`, orden por
  `name`, quitar `GetAllAsync` (también de `ITunnelRepository`). `ConnectionHistoryRepository`: alta
  y poda en transacción; quitar `GetForConnectionAsync`; agregar
  `UltimaConexionExitosaPorConexionAsync` y `ContarAsync` (también en `IConnectionHistoryRepository`).
  `RepositorioDelVault`: quitar `creado_en`.
- [x] 2.13 `Serializacion.TextoACampos`: registrar la `JsonException` por `IAppLogger` con el
  identificador de la fila; prueba unitaria del registro.
- [x] 2.14 Comprobación de arranque del invariante secretos ↔ vault, con registro y aviso.

Pruebas del grupo:

- [x] 2.15 `DatabaseInitializerTests`: base v3 sintética con una carpeta con usuario compartido y
  otra con compartido y SSH propio, una conexión con la clave reservada de conexión rápida junto a
  otra clave, una hija en otra carpeta, una hija cuyo padre no tiene carpeta, una fila `Success` con motivo, una `rdp_settings` con la
  bandera vieja encendida, una etiqueta con fecha en formato viejo y un `custom_fields` inválido;
  verificar cada backfill, que cada `CHECK` nuevo rechaza, y la lista exacta de tablas, columnas e
  índices.
- [x] 2.16 `CatalogoIdaYVueltaTests`: todas las columnas de `connections` que el repositorio lee y
  escribe, más una fila completa por tabla de protocolo; la prueba que inyectaba JSON corrupto pasa a
  verificar que la base lo rechaza. `ConnectionRepositoryTests`: guardar un registro sin ajustes
  conserva la fila de protocolo. `FolderRepositoryTests`, `TagRepositoryTests`,
  `HistorialDeConexionesTests` (usos de `GetForConnectionAsync` reescritos contra `GetRecentAsync`),
  `VaultTests` y `FlujoCompletoTests` (`SshUserName`/`SshPort`) ajustados.
- [x] 2.17 Suite de `Domain.Tests` e `Infrastructure.Tests` en verde.

## 3. Casos de uso (P-03, P-05, P-08, P-12, P-13, P-15, P-17, P-18, P-26)

- [x] 3.1 `ConnectionService`: `MoveAsync` arrastra las hijas; filtrado y barrido de conexiones
  rápidas por `EsRapida`; `ConnectionSummary` toma la última conexión del diccionario del historial;
  quitar `Clone` de `Encoding`, `FitToTab` y `DocumentationUrl`.
- [x] 3.2 `SessionManager.Anotar`: motivo nulo en `Success` y `Cancelled`; en `Failed`,
  `UltimoFallo?.Reason ?? SessionFailureReason.Other`. `SessionManagerTests`: casos para los tres
  resultados; `HistorialFalso` implementa los dos métodos nuevos.
- [x] 3.3 `ConnectionValidator` usa `Limites.MaxKeepAliveSeconds`; `FolderService.UpdateSettingsAsync`
  valida `FolderSettings` (puertos y keep-alive) antes de escribir.
- [x] 3.4 `ImportadorDeConexiones`: sin nota de plantilla (origen y tipo al `ResultadoDeImportacion`);
  `YaEsta` compara por el protocolo de la importada. `LectorDeRdmTests`: caso de reimportación de RDP
  y Web sin duplicados, y regresión de que una entrada SSH no trae dominio.
- [x] 3.5 `SessionState`: quitar `CredentialMissing`; ajustar `RdpSession` y reescribir el caso de
  `IdentidadDeWindowsTests` con un motivo que sí se produce. `OpcionesDePantallaRdp`: quitar
  `SinSombraDelCursor`. `AppSettings`: quitar `SettingKeys.ConnectionTimeoutSeconds`, sumar las tres
  claves `updates.*`; `AjustesDeActualizacion` las usa y pierde el parámetro `Origen`;
  `ActualizacionesService` lee `AjustesDeActualizacion.Repositorio` sin guardas;
  `AjustesDeActualizacionTests` ajustado.
- [x] 3.6 `AppSettingsService`: `GetTabsModeAsync`/`SetTabsModeAsync` con `TryParse` y reserva;
  `IAppSettingsService` gana las dos firmas.
- [x] 3.7 `UseCases.Tests`: `SettingsResolverTests`, `FolderUpdateImpactTests`,
  `UsuarioYPuertoPorProtocoloTests`, `CredentialProviderTests` y `OpcionesDePantallaHerenciaTests`
  sin el compartido ni `FitToTab`; `MoverCarpetaTests` con hijas; invariante «hija en la carpeta del
  padre».
- [x] 3.8 Suite de `UseCases.Tests` en verde.

## 4. Interfaz (P-02, P-03, P-05, P-08, P-11, P-13, P-15, P-16, P-17, P-19, P-20, P-24, P-25)

- [x] 4.1 `ConnectionEditorWindow`: combo de carpeta deshabilitado y derivado del padre; casilla
  «Abrir en ventana propia» en RDP; sin campo de documentación; colapsar el puerto por omisión a nulo;
  validar largo de notas antes de asignar y mostrar en la pestaña Avanzado; validación de keep-alive
  con `Limites.MaxKeepAliveSeconds`; ventana privada deshabilitada sin navegador y aviso cuando
  `WebLauncher.ConoceModoPrivado(ruta)` es falso; «Creada» y «Modificada» en la pestaña Avanzado,
  sólo al editar una conexión existente.
- [x] 4.2 `WebLauncher`: exponer `ConoceModoPrivado(string ruta)` sobre el catálogo que ya usa.
- [x] 4.3 `FolderSettingsWindow`: sin `UserName`/`Port` compartidos ni `RdpFitToTab`; keep-alive con
  la constante; mostrar el motivo del error de guardado; «Creada» y «Modificada» de la carpeta.
- [x] 4.4 `SessionView`: usar `SetKnownHostFingerprintAsync` y `SetAbreEnVentanaPropiaAsync`; apagar
  la bandera tras un traslado exitoso; ya no escribe `last_connected_at`.
- [x] 4.5 `MainWindow.Acciones`: la apertura Web registra historial `Success`; «Túneles…» sólo para
  SSH; ya no escribe `last_connected_at`. `MainWindow.AplicarModoDePestanaAsync` lee el modo vía
  `IAppSettingsService.GetTabsModeAsync`.
- [x] 4.6 `NodoArbol`: tooltip con «Última conexión» derivada del historial; sin puerto para Web.
  `NodoArbolTests` ajustado. Las fechas de registro van en los editores (4.1 y 4.3), no en el tooltip.
- [x] 4.7 `ConnectionHistoryWindow`: sin detalle de motivo en filas `Success`; sin la rama de
  `CredentialMissing`; resumen «últimos N de M» con `ContarAsync`.
- [x] 4.8 `PreferenciasWindow`: modo de pestaña por valor en `Tag` y vía `AppSettingsService`.
- [x] 4.9 `EtiquetasWindow`: aviso de fábrica en el borrado; `try/catch` en el guardado. `Etiqueta`:
  quitar `EsValida` y `Quitar`; `CatalogoDeEtiquetasTests` y `EtiquetasDeFabricaTests` ajustados.
- [x] 4.10 `TunnelEditorWindow` y `TunnelsPanel`: orden por nombre en los dos.
- [x] 4.11 `MainWindow.CopiaDeArranqueAsync`: mostrar el motivo cuando la copia falla;
  `ResultadoDeCopia` distingue «no hizo falta» de «falló».
- [x] 4.12 Suite de `App.Tests` en verde.

## 5. Cierre

- [x] 5.1 `openspec/changes/usuario-y-puerto-por-protocolo/design.md`: quitar los riesgos obsoletos y
  anotar que el requisito de reserva compartida queda superado por este cambio.
- [x] 5.2 README: sección de datos y notas técnicas al día (migración 004, última conexión derivada,
  `es_rapida`), sin versiones numéricas nuevas y con rutas `src/…` que existan.
- [x] 5.3 `openspec validate saneamiento-del-modelo-de-datos --strict`.
- [x] 5.4 Los mismos comandos que CI (`.github/workflows/ci.yml`) en verde; `dotnet build-server
  shutdown` al terminar.
- [x] 5.5a Sobre una copia de `cmc.db`, en solo lectura: conteo de filas que violarían cada `CHECK`
  nuevo (ninguna), de las que tocan cada backfill y de las que tienen la nota de plantilla del
  importador.
- [x] 5.5b Con confirmación del usuario: copia manual de la base, arranque de la aplicación para
  migrar, y `SELECT` de verificación de que los backfills tocaron exactamente las filas contadas,
  que no queda ninguna clave `cmc:conexionRapida` y que las fechas de `tags` están en ISO.
- [x] 5.6 Con confirmación aparte del usuario: `UPDATE connections SET notes = NULL` sólo en las filas
  cuyo texto coincide exactamente con la plantilla del importador, previo `SELECT` del mismo predicado
  con el conteo a la vista.
