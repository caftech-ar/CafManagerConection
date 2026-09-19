## Context

`cmc.db` está en `user_version = 3`. `Migration001` es el esquema entero y 002/003 son `ADD COLUMN`.
`DatabaseInitializer` corre las pendientes dentro de una transacción con `foreign_keys = ON` ya
activo en la conexión, y envuelve todo el arranque en un `catch (SqliteException)` que aparta la base:
hoy una violación de `CHECK` durante una migración movería la base del usuario a la ruta de
preservación y arrancaría con una vacía. SQLite (3.53 embebido) no permite `DROP COLUMN` sobre una
columna nombrada en un `CHECK` o un índice, ni cambiar un `CHECK` sin recrear la tabla. Varias
propuestas aceptadas exigen recrear tablas, así que el motor va primero.

Las decisiones del usuario están en la base del artifact
`https://claude.ai/artifact/M2zGR6juJa2GTQff8hZZ9F`, colección `decisiones`: 26 aceptadas, `P-27`
rechazada, `P-02` aceptada con la variante «conservar y arreglar».

## Goals / Non-Goals

**Goals:**
- Una sola migración 004 que deje el esquema sin columnas muertas ni duplicadas y con las
  restricciones que hoy sólo viven en código.
- Que el motor de migraciones soporte recrear tablas sin perder filas hijas y se pruebe desde bases
  v1, v2 y v3 reales.
- Que ningún camino de escritura pise datos que otro camino escribió.
- Cerrar los defectos de validación e interfaz aceptados sin ampliar funcionalidad.

**Non-Goals:**
- Fusionar `folder_settings` en `connection_folders` (rechazado).
- Cambiar el modelo de secretos: «nonce nulo = en claro» se queda; sólo se agrega una comprobación.
- Tocar `custom_fields` más allá de validarlo y sacar la marca de conexión rápida.
- Paginar el historial: sólo se corrige el resumen.

## Decisions

**Las claves foráneas se apagan fuera de la transacción.** Con `foreign_keys = ON`, `DROP TABLE`
ejecuta un `DELETE` implícito que dispara los `ON DELETE CASCADE`: recrear `connections` vaciaría
las tres tablas de protocolo, los túneles y el historial, y recrear `connection_folders` vaciaría
`folder_settings` y las conexiones. `defer_foreign_keys` difiere la comprobación, no las acciones,
así que no sirve. `Migrate` emite `PRAGMA foreign_keys = OFF` **antes** de `BeginTransaction()`
(el pragma es no-op dentro de una transacción), `PRAGMA foreign_key_check` antes del `Commit`
abortando si devuelve filas, y `PRAGMA foreign_keys = ON` en un `finally`. Apagarlo no toca la
atomicidad: `user_version` sigue escribiéndose en la misma transacción. Es el procedimiento oficial
de doce pasos de SQLite para alterar tablas.

**La recuperación de corrupción se acota por código de error.** Sólo `SqliteErrorCode` 11
(`CORRUPT`) y 26 (`NOTADB`) apartan la base; el resto se relanza. El aviso se muestra antes de crear
la base nueva y ofrece restaurar desde una copia con `ServicioDeCopias.Listar`.

**Una sola migración 004, en un orden fijo.** Primero los `DROP INDEX` de `ix_connections_search` e
`ix_connections_favorite` y los dos `DROP COLUMN` directos que quedan sobre tablas que no se recrean
(`ssh_settings.encoding`, `vault.creado_en`). Después se recrean siete tablas con el patrón
«crear `_nueva`, copiar con `INSERT … SELECT`, bajar la vieja, renombrar, recrear sus índices»:
`connection_folders`, `folder_settings`, `connections`, `rdp_settings`, `web_settings`,
`ssh_tunnels` y `connection_history`. Cada recreación copia el DDL de `Migration001` con los cambios
de este diseño, y vuelve a crear sus índices (`ix_folders_parent`, `ix_folder_settings_tag`,
`ix_connections_folder`, `ix_connections_parent`, `ix_connections_tag`, `ix_tunnels_connection`
sobre `connection_id`, `ix_history_connection`), porque `DROP TABLE` los borra. Las columnas que se
van con la recreación (`folder_settings.username`, `port`, `rdp_fit_to_tab`;
`connections.documentation_url`, `last_connected_at`; `rdp_settings.fit_to_tab`;
`ssh_tunnels.sort_order`) simplemente no figuran en la tabla nueva. Los backfills van dentro del
`INSERT … SELECT`, así la migración es autocontenida:

- `folder_settings`: `COALESCE(rdp_username, username)` y lo mismo para SSH, Web y los tres puertos:
  el valor por protocolo gana y el compartido rellena.
- `connections.folder_id`: `CASE WHEN c.parent_connection_id IS NULL THEN c.folder_id ELSE (SELECT p.folder_id FROM connections p WHERE p.id = c.parent_connection_id) END`,
  que deja a cada hija en la carpeta de su padre, nula si el padre no tiene.
- `connections.es_rapida`: `CASE WHEN json_valid(custom_fields) AND json_extract(custom_fields, '$."cmc:conexionRapida"') IS NOT NULL THEN 1 ELSE 0 END`.
  El valor guardado es el texto `True`, no un booleano, por eso se pregunta por presencia.
- `connections.custom_fields`: `CASE WHEN custom_fields IS NULL OR NOT json_valid(custom_fields) THEN NULL ELSE NULLIF(json_remove(custom_fields, '$."cmc:conexionRapida"'), '{}') END`.
  `json_remove` sobre la última clave deja `{}`, que el repositorio escribe como `NULL`; y sobre
  JSON inválido aborta, por eso se filtra antes.
- `connection_history.failure_reason`: `CASE WHEN outcome = 'Success' THEN NULL ELSE failure_reason END`.
- `tags`: `UPDATE` de `created_at` y `updated_at` al formato ISO con `Z` donde no tengan `T`.

Alternativa descartada: varias migraciones chicas, que multiplican las recreaciones de la misma
tabla y las oportunidades de olvidar un índice.

**`start_full_screen` se renombra en el `CREATE` de la recreación.** `rdp_settings` se recrea igual
para bajar `fit_to_tab` y sumar el `CHECK`; `RENAME COLUMN` sería redundante.

**`last_connected_at` se deriva, no se cachea.** `IConnectionHistoryRepository` gana
`UltimaConexionExitosaPorConexionAsync()` que devuelve un diccionario `Guid → DateTimeOffset` con
`MAX(attempted_at) … WHERE outcome = 'Success' GROUP BY connection_id`, y `ContarAsync()` para el
resumen de la ventana; `ConnectionService` pega la última conexión a los `ConnectionSummary`. La
apertura Web registra un `ConnectionHistoryEntry` con `Success` y duración nula. Alternativa
descartada: caché escrita junto al historial; el usuario aceptó la derivación y el `GROUP BY` sobre
una tabla acotada a cien filas por conexión no se mide.

**`updated_at` refleja cambios reales.** `Connection.Touch()` y `Folder.Touch()` se llaman sólo en
los mutadores que cambian estado; el repositorio mapea `UpdatedAt` de vuelta con `init` como
`CreatedAt`. `Etiqueta` gana las dos fechas y `TagRepository` las selecciona y las escribe con
`FolderRepository.Iso` (hoy usa `ToString("O")` sobre un `DateTimeOffset`, que emite `+00:00` y no
`Z`). La semilla de `tags` usa literales ISO fijos. El requisito de formato único aplica a
`created_at`, `updated_at` y `attempted_at`; `updates.lastCheckedAt` conserva su desfase a propósito
y queda fuera. El tooltip del árbol muestra «Creada» y «Modificada».

**Marca de conexión rápida en columna.** `es_rapida INTEGER NOT NULL DEFAULT 0 CHECK (es_rapida IN (0,1))`
en `connections`; `ConnectionService` filtra y barre huérfanas por la columna. Las otras nueve claves
`cmc:` se quedan en `custom_fields` con `CHECK (custom_fields IS NULL OR json_valid(custom_fields))`.
Con ese `CHECK` la base ya no puede contener JSON inválido, así que el registro de la `JsonException`
en `Serializacion.TextoACampos` se prueba por unidad, no contra la base, y la prueba existente que
inyectaba JSON corrupto pasa a verificar que la base lo rechaza.

**El historial nunca contradice su resultado.** `SessionManager.Anotar` pasa motivo nulo en
`Success` y `Cancelled`, y en `Failed` usa `UltimoFallo?.Reason ?? SessionFailureReason.Other`,
porque hoy `OpenAsync` puede anotar `Failed` sin motivo y el `CHECK` espejo lo rechazaría. El
`CHECK` tiene las dos mitades: `outcome <> 'Success' OR failure_reason IS NULL` y
`outcome <> 'Failed' OR failure_reason IS NOT NULL`.

**Escrituras angostas para la sesión.** `IConnectionRepository` gana
`SetKnownHostFingerprintAsync(Guid, string)` y `SetAbreEnVentanaPropiaAsync(Guid, bool)`, con el
molde de un método por columna. `UpdateAsync` reemplaza el `DELETE` + `INSERT` de las tablas de
protocolo por `INSERT … ON CONFLICT(connection_id) DO UPDATE`.

**Invariante padre-carpeta en el editor y en el servicio.** El editor deshabilita el combo de
carpeta cuando hay padre elegido y toma `FolderId` del padre. `MoveAsync` mueve también las hijas
del registro movido. Con el invariante sostenido en el editor, el servicio y la migración, el
impacto de borrar carpeta no necesita una rama por `parent_connection_id`. Sin `CHECK` XOR en el
esquema.

**Un solo máximo de keep-alive.** `Domain.Settings.Limites.MaxKeepAliveSeconds = 3600`, usado por
la validación de las dos ventanas, `ConnectionValidator` y el texto de error. `FolderService`
valida `FolderSettings` antes de escribir.

**Dapper con `MatchNamesWithUnderscores`, activado en Infrastructure.** Es un estático global; si
se activara en `CompositionRoot`, las pruebas de Infrastructure, que instancian los repositorios
directo, leerían todo en nulo sin error. Se activa en una clase estática `MapeoDeDapper` de
`Infrastructure/Database` con `[ModuleInitializer]`, y los DTOs pasan a PascalCase. Todas las
consultas listan columnas. Los cuatro `Enum.Parse` sin red (`Protocol` y `SshAuthMethod` en
`ConnectionRepository`, `SshAuthMethod` en `FolderRepository`) pasan a `TryParse` con reserva y
registro; el de `RepositorioDelVault` queda fuera junto con `vault.creado_en`, porque la sesión no
tiene permiso de lectura sobre `Credentials/` y bajar la columna sin ajustar su `INSERT` rompería
el alta de clave maestra. `CatalogoIdaYVueltaTests` cubre las columnas de
`connections` que el repositorio lee y escribe (todas menos `secreto` y `secreto_nonce`, que son del
vault) y suma un caso por tabla de protocolo.

**Todo lo que se toca cita símbolos, no líneas, y no escribe conteos del parque.**
`CoherenciaDelCodigoTests` prohíbe `archivo:NN` en el repositorio y `SinDatosRealesTests` recorre
también `openspec/**/*.md`: los conteos reales viven en los `SELECT` previos de la tarea de
confirmación, no en el texto. Los datos de prueba usan `192.0.2.x` y cantidades inventadas.

## Risks / Trade-offs

- [La migración 004 recrea siete tablas en una sola transacción sobre la base del usuario] → Se
  prueba sobre una base v3 sintética que cubra cada backfill y cada `CHECK`, y después sobre una
  copia de la base real; `ServicioDeCopias` hace una copia de arranque antes de migrar; el usuario
  confirma antes de correrla contra `cmc.db`.
- [Recrear con FK activas vacía las hijas en cascada] → `foreign_keys = OFF` fuera de la
  transacción y `foreign_key_check` antes del commit; la prueba «recrear una tabla con hijas
  conserva las hijas» es la que lo fija.
- [`DROP TABLE` borra los índices] → Cada recreación termina recreando sus índices; una prueba fija
  la lista exacta de índices del esquema.
- [Perder la huella SSH o la URL al guardar con un registro sin hidratar] → `ON CONFLICT DO UPDATE`
  y una prueba que guarde un registro sin ajustes y verifique que la fila de protocolo sigue.
- [Derivar la última conexión cambia el tooltip de las entradas Web hasta que registren evento] →
  La apertura Web registra historial desde la misma versión; una Web sin evento muestra «sin
  conexiones registradas», que es la verdad.
- [Quitar `CredentialMissing` cambia el reintento por identidad de Windows] → El test de
  `IdentidadDeWindowsTests` que lo pasa como caso manda: se reescribe con un motivo que sí se produce
  y se verifica que la conducta observable no cambia. `ConnectionHistoryWindow.Detalle` pierde su
  rama.
- [Cambiar el mapeo de Dapper a PascalCase toca cinco DTOs a la vez] → `CatalogoIdaYVueltaTests`
  ampliado es la compuerta; se hace en una tarea aislada.
- [Los `CHECK` nuevos rechazan datos que hoy pasan] → Antes de la migración se cuentan con `SELECT`
  las filas que violarían cada `CHECK`; el `CASE` de `custom_fields` anula el JSON inválido en vez de
  abortar.
- [El esquema y los repositorios quedan incompatibles a mitad del trabajo] → La migración y la capa
  de persistencia son un solo grupo de tareas; la suite se corre al cerrarlo, no entre medio.

## Migration Plan

1. Motor: `foreign_keys = OFF` fuera de la transacción, `foreign_key_check`, filtro de corrupción,
   aviso previo, pruebas desde v1 y v2.
2. `Migration004` junto con dominio y repositorios, con pruebas sobre una base v3 sintética que
   cubra cada `CHECK` y cada backfill.
3. Casos de uso e interfaz en el orden de `tasks.md`.
4. Antes de arrancar la aplicación contra `cmc.db`: `SELECT` de conteo por cada `CHECK` sobre una
   copia, copia manual de la base, confirmación explícita del usuario (es una escritura sobre su
   base) y `SELECT` de verificación después.
5. Rollback: la copia previa. La transacción impide una migración a medias.

## Resolved Questions

- **Las notas de plantilla del importador se limpian**, con un `UPDATE` que vacía el campo sólo en
  las filas cuyo texto coincide exactamente con lo que genera el importador; una nota editada a mano
  se conserva. Se cuenta antes con un `SELECT` de la misma forma y se confirma por operación.
- **«Creada» y «Modificada» se muestran en la pestaña Avanzado del editor**, no en el tooltip del
  árbol. El tooltip sigue mostrando la última conexión, que es otro dato y viene del historial.
