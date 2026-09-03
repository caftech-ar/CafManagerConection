---

description: "Lista de tareas: Procesos, registros y árbol"
---

# Tasks: Procesos, registros y árbol

**Input**: `specs/002-procesos-registros-y-arbol/spec.md`, `specs/002-procesos-registros-y-arbol/plan.md`

## Format: `[ID] [P?] [Story] Descripción con la ruta del archivo`

- **[P]**: archivo distinto de toda tarea sin terminar, se puede hacer en paralelo.
- **[Story]**: US1 a US6. Las de Fase 0, Foundational y Cierre no llevan etiqueta de historia.
- Las tareas de verificación manual contra un servidor real no nombran un archivo del repositorio:
  su entregable es la validación, no código. Se exceptúan de la ruta obligatoria.

---

## Phase 1: Fase 0 del plan — lo que hay que averiguar antes de escribir código

Tres experimentos acotados, cada uno con una respuesta binaria. Ninguno toca código de producción.

- [ ] T600 [P] **R1** — ¿el control ActiveX de RDP sobrevive a un reparent? Con una conexión RDP
  viva, mover el `WindowsFormsHost` de `src/CafManagerConection.App/Views/SessionView.xaml:105`
  (`<wf:WindowsFormsHost x:Name="_host" />`) a una `Window` nueva y devolverlo. Respuesta: sigue conectado / se
  reconecta / se cae. Si se cae, US5 (FR-187) se cumple con una ventana propia que se abre **en
  lugar de** la pestaña al iniciar la sesión, sin intercambio en caliente, y así se documenta en
  T666. Bloquea T666 y T667.
- [ ] T601 [P] **R2** — ¿el control de RDP entra con la identidad de Windows? Contra un servidor del
  dominio, en `src/CafManagerConection.Rdp/RdpSession.cs`, dejar `UserName` y `ClearTextPassword`
  sin asignar con NLA activo. Respuesta: entra / pide credenciales / falla. Si falla, FR-186 se
  cumple igual porque exige caer al pedido de credenciales; lo que no se ofrece es el tilde de
  identidad de Windows. Bloquea T668.
- [ ] T602 [P] **R3** — ¿cuánto cuesta la segunda muestra del CPU instantáneo? Medir, contra un
  servidor con 400 o más procesos: dos `ps` separados por el intervalo, contra una lectura de
  `/proc/*/stat` comparada con la muestra anterior del propio panel. Respuesta: milisegundos y
  bytes por muestra de cada camino, y el consumo de CPU en el servidor con el panel de estado y el
  de procesos abiertos a la vez, contra el techo de **1 % de un núcleo** que SC-018 de la 001 le
  pone al monitoreo (T511 de la 001 midió 1 a 3 % con unos 700 procesos sólo con el panel de
  estado). El sesgo del plan es la segunda opción; si el resultado la contradice, T609-T612 (US1) se
  escriben contra el camino que gane. Si ninguna entra en el presupuesto, se baja la frecuencia o se
  achica lo que se pide: el techo no se sube (SC-050a). Afecta a US1.

**Checkpoint**: los tres experimentos responden antes de tocar `RdpSession`, `VentanaDeSesion` o el
parser de `/proc`.

---

## Phase 2: Foundational (bloquea a más de una historia)

- [x] T603 [P] Agregar `ReorderAsync` a `IFolderRepository` en
  `src/CafManagerConection.UseCases/Abstractions/Repositories.cs` (FR-193)
- [x] T604 Escribir la prueba de integración
  `tests/CafManagerConection.Infrastructure.Tests/Database/FolderRepositoryReorderTests.cs` contra
  una SQLite temporal, antes de implementar: reordenar hermanas, mover entre carpetas, rechazar
  mover una carpeta dentro de sí misma o de un descendiente suyo (FR-193, FR-194) — depende de T603
- [x] T605 Implementar `ReorderAsync` en
  `src/CafManagerConection.Infrastructure/Database/FolderRepository.cs` (FR-193, FR-194) — depende
  de T603, T604
- [x] T606 [P] Escribir la prueba `tests/CafManagerConection.Domain.Tests/Settings/ResultadoDeSondeoTests.cs`
  de los tres estados del sondeo de `sudo` —sin contraseña, con contraseña, imposible— antes de
  crear el tipo (FR-184d)
- [x] T607 Crear el tipo de dominio `ResultadoDeSondeo` en
  `src/CafManagerConection.Domain/Settings/ResultadoDeSondeo.cs`, sin referencias a SSH.NET
  (FR-184, FR-184d) — depende de T606
- [x] T608 Migración 006: columna `icon_key` en `connection_folders` y en `connections`. La prueba
  `tests/CafManagerConection.Infrastructure.Tests/Database/Migracion006Tests.cs` va primero, contra
  una SQLite temporal y con el mismo molde que `Migracion004Tests.cs`: una base de la versión
  anterior sube sin perder filas, las columnas nuevas nacen vacías y volver a aplicarla no cambia
  nada. Después, `src/CafManagerConection.Infrastructure/Database/Migrations/Migration006_Icono.cs`
  y su registro en `src/CafManagerConection.Infrastructure/Database/DatabaseInitializer.cs`.
  Bloquea T657 y T658 (FR-195)

**Checkpoint**: `ReorderAsync`, `ResultadoDeSondeo` y la migración 006 existen y tienen prueba.

---

## Phase 3: User Story 1 - Ver qué consume el servidor, y mirar con privilegios cuando haga falta (Priority: P1) 🎯 MVP

**Goal**: panel de procesos de sólo lectura, ordenable por CPU instantáneo y por memoria, con hijos
y E/S por proceso, y el sondeo de `sudo` que habilita reintentar con privilegios donde haga falta.

**Independent Test**: contra un servidor con una fuga, el panel la señala ordenando por memoria y
por CPU; contra un servidor sin `sudo`, ningún panel ofrece escalar.

### Lógica pura, prueba antes que código

- [x] T609 [P] [US1] Prueba `tests/CafManagerConection.Monitoring.Tests/ParserDeProcesosTests.cs`:
  parseo de `/proc/*/stat` con fixtures — nombre de proceso con espacios entre paréntesis, campos
  fuera de orden por ese motivo, proceso desaparecido entre la lectura y el parseo (FR-183b)
- [x] T610 [US1] Implementar `src/CafManagerConection.Monitoring/ParserDeProcesos.cs` (FR-183b) —
  depende de T609
- [x] T611 [P] [US1] Prueba `tests/CafManagerConection.Monitoring.Tests/MuestraDeProcesosTests.cs`:
  el % de CPU se calcula por diferencia de `utime + stime` entre dos muestras, nunca da negativo, y
  un proceso que terminó entre las dos muestras no queda en la lista con el valor de la anterior
  (FR-183b, FR-173d)
- [x] T612 [US1] Implementar `src/CafManagerConection.Monitoring/MuestraDeProcesos.cs` (FR-183b,
  FR-173d) — depende de T611, T610
- [x] T613 [P] [US1] Prueba `tests/CafManagerConection.Monitoring.Tests/ArbolDeProcesosTests.cs`: el
  árbol por PPID expone los hijos directos y el consumo del padre no los cuenta dos veces (FR-183)
- [x] T614 [US1] Implementar `src/CafManagerConection.Monitoring/ArbolDeProcesos.cs` (FR-183) —
  depende de T613
- [x] T615 [P] [US1] Prueba `tests/CafManagerConection.Monitoring.Tests/ParserDeIoTests.cs`: parseo
  de `/proc/*/io`, con el caso de un proceso sin permiso para leer el propio (FR-183)
- [x] T616 [US1] Implementar `src/CafManagerConection.Monitoring/ParserDeIo.cs` (FR-183) — depende
  de T615

### Sondeo de `sudo`

- [ ] T617 [P] [US1] Prueba `tests/CafManagerConection.Ssh.Tests/SondaDeSudoTests.cs` contra el
  contenedor OpenSSH de pruebas: exactamente un `sudo -n` por sondeo, y los tres resultados de
  `ResultadoDeSondeo` distinguidos sin confundir «no está en sudoers» con «pide contraseña»
  (FR-184, FR-184c, FR-184d) — los tres resultados los distingue
  `tests/CafManagerConection.Domain.Tests/Settings/ResultadoDeSondeoTests.cs` (T606). Falta la
  prueba de la clase `SondaDeSudo`: ninguna toca `src/CafManagerConection.Ssh/SondaDeSudo.cs` ni
  cuenta sus `Sondeos`
- [x] T617b [US1] Arreglar el orden de `ConUnaContrasenaAsync` en
  `src/CafManagerConection.Ssh/SshCommandRunner.cs:280`: `Task.Run(() => cmd.Execute())` abría el
  canal en un hilo del pool y `CreateInputStream()` corría antes, así que la contraseña nunca
  llegaba a `sudo` y el usuario veía «The input stream can be used only during execution». Ahora
  `BeginExecute()` abre el canal en este hilo y `EndExecute()` espera. El IL de SSH.NET 2026.0.0
  confirma la cadena: `BeginExecute()` → `BeginExecute(cb, state)` → `ExecuteAsync`, y el cuerpo
  síncrono de `ExecuteAsync` es el único que asigna `_channel` (FR-184e)
- [ ] T617c [US1] Correr `tests/CafManagerConection.Ssh.Tests/EscaladaConContrasenaIntegracionTests.cs`
  contra el contenedor: los tres casos del camino de la contraseña por entrada estándar, que
  compilan y se omiten pero todavía no dieron verde porque Docker no estaba levantado.
  `scripts/sshd-prueba.ps1` ahora crea el segundo usuario `pruebaclave` con `PASSWD: ALL` en
  sudoers, porque con el único usuario `NOPASSWD` de antes `sudo -n` siempre prosperaba y ese
  camino no se ejercía: por eso el defecto de T617b llegó a producción (FR-184e)
- [x] T618 [US1] Implementar `src/CafManagerConection.Ssh/SondaDeSudo.cs` (FR-184, FR-184c) —
  depende de T617, T607
- [x] T619 [US1] Cablear el sondeo al abrir la sesión en `src/CafManagerConection.Ssh/SshSession.cs`,
  una sola vez por sesión y disponible para todos los paneles (FR-184c)
- [x] T620 [US1] Prueba SC-052 en
  `tests/CafManagerConection.Ssh.Tests/SC052_ContrasenaDeSudoNoQuedaEnRegistroTests.cs`: busca el
  valor conocido de la contraseña de la conexión y el de la contraseña de `sudo` que el usuario
  tipeó, en el archivo de Serilog, en la consola de traza y en el texto de un error forzado, tras
  reintentar `sudo` con `ConSudoYContrasenaAsync`
  (`src/CafManagerConection.Ssh/SshCommandRunner.cs:207`). Es la prueba de regresión de que ninguna
  de las dos queda en un registro (FR-184e, SC-052)
- [x] T621 [P] [US1] Prueba
  `tests/CafManagerConection.Ssh.Tests/ContrasenaDeSudoDeSesionTests.cs`: la contraseña se entrega
  mientras la sesión vive, el búfer queda en ceros al cerrarla, y una sesión nueva sobre la misma
  conexión nace sin contraseña y la vuelve a pedir (FR-184e, SC-052b, SC-052c)
- [x] T622 [US1] Crear `src/CafManagerConection.Ssh/ContrasenaDeSudoDeSesion.cs`, que guarda la
  contraseña en memoria mientras dura la sesión y pisa su búfer con ceros al cerrarla, con el mismo
  patrón que `TomarTexto()` de `src/CafManagerConection.Ssh/EntradaDeContrasenaInteractiva.cs`, y
  `src/CafManagerConection.Ssh/IPedidoDeContrasenaDeSudo.cs`, la abstracción con la que la capa SSH
  se la pide al usuario sin conocer la interfaz (FR-184e) — depende de T621
- [x] T623 [US1] Prueba
  `tests/CafManagerConection.Infrastructure.Tests/Credentials/ContrasenaDeSudoNoSePersisteTests.cs`:
  tras una escalada con una contraseña de valor conocido, ese valor no aparece en ninguna tabla de
  la base SQLite, ni en el archivo de configuración JSON, ni en ninguna entrada del Administrador
  de credenciales cuyo nombre empiece con `cmc:`, ni en ningún archivo bajo
  `%LocalAppData%\CafManagerConection` (FR-184e, SC-052a)
- [x] T624 [US1] En `src/CafManagerConection.Ssh/SondaDeSudo.cs`, cuando `sudo` pida contraseña
  reintentar con la contraseña de la conexión por `sudo -S -k` (`src/CafManagerConection.Ssh/SshCommandRunner.cs:213`),
  reusando `RunWithSudoFallbackAsync` (`src/CafManagerConection.Ssh/SshCommandRunner.cs:180`). Si
  esa contraseña no sirve, pedirle una al usuario por `IPedidoDeContrasenaDeSudo` **una sola vez
  por sesión** y guardarla en `ContrasenaDeSudoDeSesion` para los reintentos siguientes; si el
  usuario cancela o la que dio tampoco sirve, la escalada se declara imposible. La contraseña viaja
  por la entrada estándar del `sudo -S -k` y nunca por la línea de comandos (FR-184e) — depende de
  T620, T622, T623. El reintento con la contraseña **de la conexión** ya está en
  `RunWithSudoFallbackAsync` (`src/CafManagerConection.Ssh/SshCommandRunner.cs:180`), que llama a
  `ConSudoYContrasenaAsync` (línea 200). Falta todo el resto: `SondaDeSudo` no lo usa y no hay
  pedido al usuario
- [x] T625 [US1] Borrar el búfer de `ContrasenaDeSudoDeSesion` al cerrar la sesión, en
  `DisconnectAsync` (`src/CafManagerConection.Ssh/SshSession.cs:569`) y en `DisposeAsync`
  (`src/CafManagerConection.Ssh/SshSession.cs:763`), por los dos caminos y no sólo por el ordenado
  (FR-184e) — depende de T622
- [x] T626 [US1] Ventana del pedido de contraseña de `sudo` en
  `src/CafManagerConection.App/Views/PedidoDeContrasenaDeSudoWindow.xaml(.cs)`, que implementa
  `IPedidoDeContrasenaDeSudo`: campo sin eco, nombra el servidor, y dice que la contraseña dura lo
  que dura la sesión y no se guarda (FR-184e) — depende de T622, verificación manual

### Interfaz y cierre de la historia

- [ ] T627 [US1] `src/CafManagerConection.App/Panels/ProcesosPanel.xaml(.cs)`: ordenable por CPU y
  por memoria, hijos desplegables, columnas de E/S, sin ninguna acción de matar, señalar o cambiar
  prioridad (FR-183, FR-183a, FR-183c) — verificación manual, no hay arnés de pruebas WPF. El panel
  está: orden por CPU, memoria y disco (`ProcesosPanel.xaml:51-59`), hijos con
  `HierarchicalDataTemplate` y ninguna acción destructiva, con la prueba estática
  `tests/CafManagerConection.App.Tests/Panels/ProcesosPanelDeSoloLecturaTests.cs`. La E/S va en una
  sola columna «Disco» con la suma de `BytesLeidos + BytesEscritos`
  (`src/CafManagerConection.Monitoring/MuestraDeProcesos.cs:33`), no en dos. Falta verlo contra un
  servidor
- [x] T628 [US1] Botón «reintentar con privilegios» en los paneles que muestran menos por falta de
  permiso, oculto cuando `ResultadoDeSondeo` sea «imposible», en
  `src/CafManagerConection.App/Panels/PanelesPlataforma.cs` (FR-184a, FR-184b) — vive en
  `src/CafManagerConection.App/Panels/PanelInventario.xaml.cs:23`, base del panel de puertos, y en
  `src/CafManagerConection.App/Panels/ProcesosPanel.xaml.cs:71`; lo oculta
  `MensajeDeEscalada.MuestraElBoton` (`src/CafManagerConection.Monitoring/EscaladaDeLectura.cs:22`)
- [x] T629 [US1] El top de diez de `src/CafManagerConection.App/Panels/StatusPanel.xaml.cs` conserva
  las siete columnas que le exige FR-173 —PID, usuario, CPU, memoria residente, hilos, estado y
  tiempo corriendo— y MUST NOT agregar la jerarquía de hijos ni la entrada y salida por proceso, que
  son del `ProcesosPanel`. Sumar un acceso al `ProcesosPanel` como botón propio en el encabezado del
  top: el doble clic sobre una fila queda como está, abriendo la ficha de proceso de FR-165 que
  FR-173 ya le reserva, y no se reasigna (FR-183d)
- [x] T630 [US1] Corregir el `%CPU` del top: el `ps --sort=-pcpu` de
  `src/CafManagerConection.Monitoring/MetricsCollector.cs:23` y el formato de
  `src/CafManagerConection.Monitoring/ParsersExtendidos.cs:198` deben usar el cálculo instantáneo de
  T612 en lugar del promedio de vida de `ps`; `StatusPanel.Filas.cs` sólo trae records de fila y no
  cambia. Cierra T510 de la 001 (FR-173d)
- [ ] T631 [US1] Prueba `tests/CafManagerConection.Ssh.Tests/SC051_UnSoloSondeoPorSesionTests.cs`:
  cuenta las líneas de `sudo` en el registro del servidor de prueba antes y después de abrir la
  sesión (SC-051) — el contador que la prueba necesita ya está expuesto en
  `src/CafManagerConection.Ssh/SshSession.cs:310` (`SondeosDeSudo`); el archivo de prueba no existe
- [ ] T632 [US1] Validar manualmente contra un servidor real los escenarios de aceptación 1 a 9 de
  US1 y SC-038, SC-039, SC-050, SC-050a

**Checkpoint**: US1 funciona sola. Es el mínimo entregable.

---

## Phase 4: User Story 2 - Enterarme de lo que pasa en un registro sin quedarme mirándolo (Priority: P2)

**Goal**: los dos visores de registro que FR-185d alcanza —`ContenedorWindow` y `TextViewerWindow`—
siguen el archivo en vivo, muestran qué miran y cuándo cambió, ofrecen forzar lectura y avisan de
error, archivo caído o canal cortado.

**Independent Test**: escribir una línea en el archivo del servidor la hace aparecer sin tocar
nada; borrar el archivo produce un aviso.

- [ ] T633 [P] [US2] Prueba `tests/CafManagerConection.Ssh.Tests/ArchivoSeguidoTests.cs` contra el
  contenedor OpenSSH: detecta rotación, borrado y corte del canal por ausencia de latido (FR-185,
  FR-185c) — rotación y borrado los cubre
  `tests/CafManagerConection.Platform.Tests/SeguimientoDeArchivoTests.cs`, 34 casos sobre el texto
  que devuelve `tail`, sin servidor. Falta el corte del canal por ausencia de latido, que ninguna
  prueba toca
- [x] T634 [US2] Generalizar `SeguirAsync` para seguir cualquier archivo y no sólo `docker logs -f`:
  la firma en `src/CafManagerConection.Platform/IPlatformLogStreamer.cs:8` y su implementación en
  `src/CafManagerConection.Ssh/SshCommandRunner.cs:331` (FR-185) — depende de T633
- [ ] T635 [P] [US2] Prueba `tests/CafManagerConection.Domain.Tests/DeteccionDeLineaDeErrorTests.cs`:
  reconoce niveles de error habituales sin marcar texto normal como error (FR-185c) — lo que la
  tarea pide ya lo prueba `tests/CafManagerConection.Platform.Tests/NivelDeLineaTests.cs` sobre
  `NivelDeLinea`, de FR-100f. Falta decidir si se duplica en Domain o la tarea se cierra contra esa
- [ ] T636 [US2] Implementar
  `src/CafManagerConection.Domain/Monitoring/DeteccionDeLineaDeError.cs` (FR-185c) — depende de T635
  — la detección está en `src/CafManagerConection.Platform/NivelDeLinea.cs` y la usan los dos
  visores (`ContenedorWindow.xaml.cs:491` y `VisorDeRegistroWindow.xaml.cs:149`); el archivo de
  Domain que la tarea nombra no existe
- [x] T637 [US2] Reescribir `src/CafManagerConection.App/Views/TextViewerWindow.xaml(.cs)` para
  seguir en vivo el registro de un proceso de supervisord, dejando de ser una lectura única (FR-185,
  FR-185d) — depende de T634. El seguimiento en vivo es una ventana nueva,
  `src/CafManagerConection.App/Views/VisorDeRegistroWindow.xaml(.cs)`, con la fuente
  `RegistroDeProcesoSupervisado` (`src/CafManagerConection.App/Panels/PanelesPlataforma.cs:1313`);
  `TextViewerWindow` queda como visor estático, que es lo que pide T675
- [ ] T638 [US2] Mostrar la ruta de cada archivo seguido y la fecha de su último cambio en
  `src/CafManagerConection.App/Views/ContenedorWindow.xaml` y
  `src/CafManagerConection.App/Views/TextViewerWindow.xaml`; la consola de traza queda exceptuada
  (FR-185a, FR-185d) — el visor de registro lo hace con la lista `_archivos`
  (`VisorDeRegistroWindow.xaml:86`), que muestra ruta y fecha por archivo. `ContenedorWindow`
  declara la fuente y la hora de la última línea —`docker logs <contenedor> · stdout y stderr ·
  última línea hace X` (`ContenedorWindow.xaml.cs:75-78`)—, no la ruta del archivo: para eso hace
  falta un `docker inspect` que hoy no se pide
- [x] T639 [US2] Botón «forzar lectura» en
  `src/CafManagerConection.App/Views/ContenedorWindow.xaml.cs` y
  `src/CafManagerConection.App/Views/TextViewerWindow.xaml.cs` (FR-185b) — `_forzarRegistro` en
  `ContenedorWindow.xaml:128` y `_forzar` en `VisorDeRegistroWindow.xaml:49`, que es el visor que
  reemplaza al `TextViewerWindow` en vivo
- [x] T640 [US2] Aviso de línea de error, archivo no legible y canal cortado en los dos visores,
  generalizando el latido que ya resuelve `ContenedorWindow` para Docker, en
  `src/CafManagerConection.App/Views/ContenedorWindow.xaml.cs` y `TextViewerWindow.xaml.cs`. A
  `src/CafManagerConection.App/Views/ConsolaDeTraza.xaml.cs` sólo le alcanza el aviso de línea de
  error sobre las filas con fallo que ya marca: no sigue archivos, así que queda fuera de FR-185,
  FR-185a y FR-185b (FR-185c, FR-185d) — depende de T636
- [ ] T641 [US2] Botón de escalar con privilegios en `TextViewerWindow` cuando el registro de
  supervisord lo requiera, reusando el botón de T628 (FR-184a) — depende de T628. El seguimiento va
  sin `sudo`: `RegistroDeProcesoSupervisado.SeguirAsync` manda el `tail -F` pelado
  (`src/CafManagerConection.App/Panels/PanelesPlataforma.cs:1369`) y un archivo de root sólo produce
  el aviso «el permiso no alcanza» de `SeguimientoDeArchivo.Diagnostico`. El botón no existe en
  ningún visor de registro
- [ ] T642 [US2] Validar manualmente los escenarios de aceptación 1 a 5 de US2 y SC-040, SC-049

**Checkpoint**: US1 y US2 funcionan juntas y por separado.

---

## Phase 5: User Story 3 - Ordenar el árbol como quiero y reconocer cada servidor de un vistazo (Priority: P3)

**Goal**: orden arbitrario de carpetas y conexiones que persiste, alta alfabética, ordenar
alfabéticamente por menú, icono y color independientes, etiqueta por menú contextual, ventanas de
configuración en dos columnas.

El tramo de icono —FR-195, FR-195a y FR-195c— depende de la migración 006 (T608) y va separado del
de orden y arrastre, que no la necesita.

**Independent Test**: reordenar una carpeta y una conexión, reabrir la aplicación: las dos siguen
donde quedaron.

### Orden y arrastre — no depende de la migración 006

- [x] T643 [P] [US3] Prueba `tests/CafManagerConection.UseCases.Tests/Folders/OrdenAlfabeticoTests.cs`
  contra `FolderService.cs`: orden alfabético de los hijos directos de una carpeta, con acentos y
  mayúsculas, sin tocar el contenido de las carpetas internas, y sin efecto sobre una carpeta con un
  solo hijo (FR-193a, FR-193c)
- [x] T644 [US3] Implementar el orden alfabético en
  `src/CafManagerConection.UseCases/Folders/FolderService.cs` (FR-193a, FR-193c) — depende de T643,
  T605
- [x] T645 [US3] Asignar `SortOrder` en orden alfabético: en
  `CreateAsync` (`src/CafManagerConection.UseCases/Connections/ConnectionService.cs:171`), que hoy
  no lo asigna y nace en 0; en `ImportadorDeConexiones.CrearAsync`
  (`src/CafManagerConection.UseCases/Importacion/ImportadorDeConexiones.cs:122`), que tampoco lo
  asigna para las conexiones; y en la carpeta que crea la misma clase
  (`src/CafManagerConection.UseCases/Importacion/ImportadorDeConexiones.cs:109`), que hoy la manda
  al final con `orden = arbol.Count(...)`. `DuplicateAsync` (`ConnectionService.cs:356`, con
  `SortOrder = o.SortOrder + 1` en la 370) no se toca: ahí está bien (FR-193a)
- [x] T646 [US3] Corregir `PuedenReordenarse` en
  `src/CafManagerConection.App/Views/MainWindow.Acciones.cs:835` para admitir carpetas en el origen
  y en el destino, no sólo conexiones (FR-193, FR-193b) — quedó partido en `PuedeCaerEntreFilas`
  (`MainWindow.Acciones.cs:945`), que exige que origen y destino sean de la misma clase, y
  `PorQueNoSePuedeSoltar` (línea 1098), que dice el motivo del rechazo
- [x] T647 [US3] «Ordenar alfabéticamente» en el menú contextual de una carpeta, en
  `src/CafManagerConection.App/Views/MainWindow.Acciones.cs` (FR-193c) — depende de T644
- [x] T648 [US3] Arrastre de carpetas persistido con `ReorderAsync` (T605): al fallar, el árbol
  vuelve al estado guardado y dice por qué, en
  `src/CafManagerConection.App/Views/MainWindow.Acciones.cs` (FR-193, FR-193b, FR-194) — depende de
  T605, T646
- [x] T649 [US3] Asignar la etiqueta de una conexión o carpeta desde el menú contextual, sin abrir
  el editor, en `src/CafManagerConection.App/Views/MainWindow.Acciones.cs` (FR-190)

- [x] T680 [US3] Confirmar el movimiento sólo cuando cambia la carpeta, en
  `src/CafManagerConection.App/Views/MainWindow.Acciones.cs` (`MoverAsync`,
  `ConfirmarMoverConexion`, `ConfirmarMoverCarpeta`, `RutaDeCarpeta`): el diálogo dice la ruta
  completa del destino y, para una carpeta, cuántas conexiones y subcarpetas se van con ella. La
  advertencia de valores heredados de FR-062 va en el mismo diálogo. Acomodar dentro de la misma
  carpeta no pasa por `MoverAsync`: `ReordenarAsync` llama directo a `ReorderAsync` cuando origen y
  destino ya son hermanos (FR-194a)

### Ventanas de configuración — no depende de la migración 006

- [ ] T650 [US3] Redistribuir en dos columnas con secciones con título, separando lo heredado de
  las credenciales por protocolo, el `StackPanel` de una sola columna de la pestaña «Acceso»
  (`src/CafManagerConection.App/Views/FolderSettingsWindow.xaml:51`, dentro del `TabItem` que abre
  en la línea 41) (FR-196) — **el XAML ya está redistribuido**: tres columnas (`*` / 16 / `*`),
  secciones con el estilo `Tarjeta` y título con `Titulo` a 14, «Cuenta» y «Credenciales» separadas.
  Falta mirarlo en pantalla: la ventana pasó de 620 a 880 de ancho con `ResizeMode="NoResize"`
- [ ] T651 [US3] Redistribuir el `StackPanel` de una sola columna de la pestaña `_pestanaProtocolo`
  de `src/CafManagerConection.App/Views/ConnectionEditorWindow.xaml:89` (dentro del `TabItem` que
  abre en la línea 79), que es la contraparte de la pestaña «Acceso» de T650. Las dos ventanas
  tienen pestañas distintas, así que lo exigible es la misma **gramática de secciones** —los mismos
  títulos de sección, en el mismo orden, separando lo heredado de las credenciales por protocolo—,
  no que cada campo caiga en la misma coordenada (FR-196a) — **el XAML ya está redistribuido** con
  los mismos títulos y el mismo orden que T650. Falta mirarlo en pantalla, y lo primero es la fila
  de la clave privada —caja, «...» y «Pegar clave…»—, que ahora vive en media columna

### Icono y color — todavía sin persistencia

- [x] T652 [P] [US3] Prueba `tests/CafManagerConection.Domain.Tests/JuegoDeIconosTests.cs`: el
  juego cubre los usos de FR-195a (base de datos, web, correo, archivos, respaldo, contenedor,
  cortafuegos, monitoreo, más los genéricos) y no referencia ninguna biblioteca de terceros
  (FR-195a, FR-195c)
- [x] T653 [US3] Crear `src/CafManagerConection.Domain/Settings/JuegoDeIconos.cs`, en paralelo a
  `PaletaIconos` (FR-195a, FR-195c) — depende de T652
- [ ] T654 [US3] Geometrías del juego de iconos en el diccionario de recursos de la aplicación,
  copiadas de Fluent UI System Icons con su atribución en la documentación, no incorporadas como
  paquete (FR-195c) en `src/CafManagerConection.App/Themes/Estilos.xaml` — las tres nuevas están:
  `IconoCorreo` (`Estilos.xaml:1251`), `IconoRespaldo` (1253) y `IconoCortafuegos` (1255), y las 16
  claves del juego apuntan a una geometría declarada, con guardián en
  `tests/CafManagerConection.Domain.Tests/JuegoDeIconosTests.cs:84`. La atribución está en
  `README.md:325` y en `Estilos.xaml:1229`. Falta ver las tres geometrías dibujadas en pantalla
- [x] T655 [US3] Agregar la propiedad `Icono` a `Folder` y a `Connection` en
  `src/CafManagerConection.Domain/Connections/Folder.cs` y `Connection.cs`, sin persistencia
  todavía (FR-195)
- [ ] T656 [P] [US3] Prueba
  `tests/CafManagerConection.UseCases.Tests/Inheritance/HerenciaDeIconoYColorTests.cs` contra
  `SettingsResolver.cs`: `SettingsResolver.Resolve` no expone el color de la carpeta al hijo, y todo
  lo demás que hoy se hereda (FR-058) sigue heredándose, y el icono de T655 tampoco se hereda
  (FR-195b) — depende de T655. Que ni `FolderSettings` ni `EffectiveSettings` declaren `IconKey` ni
  `IconColor` lo guarda
  `tests/CafManagerConection.App.Tests/ViewModels/IconoDelArbolTests.cs:114-125`, por reflexión.
  Falta la prueba contra `SettingsResolver.Resolve` en el archivo que la tarea nombra: la que
  comprueba que lo demás de FR-058 sigue heredándose

### Icono y color — persistencia, depende de la migración 006 (T608)

- [x] T657 [US3] Persistir el icono elegido de una carpeta y de una conexión en
  `src/CafManagerConection.Infrastructure/Database/FolderRepository.cs` y `ConnectionRepository.cs`
  (FR-195) — depende de T608
- [x] T658 [US3] Selector de icono independiente del color, sin heredarlo de la carpeta, en
  `FolderSettingsWindow.xaml(.cs)` y `ConnectionEditorWindow.xaml(.cs)` (FR-195, FR-195b) — depende
  de T657

### Cierre de la historia

- [ ] T659 [US3] Validar manualmente los escenarios de aceptación 1 a 10 de US3 y SC-045 a SC-048,
  incluidos el 6 (icono por conexión) y el 10 (juego de iconos en el selector)

**Checkpoint**: US3 entrega orden, arrastre, alfabético, etiqueta, ventanas, e icono y color
persistidos.

---

## Phase 6: User Story 4 - Mover archivos navegando el árbol remoto (Priority: P4)

**Goal**: árbol de directorios remoto en el explorador SFTP, con confirmación de destino al bajar y
al subir, e icono y color por tipo de archivo.

**Independent Test**: un directorio de al menos 50 archivos y 3 niveles sube y baja completo.

- [x] T660 [P] [US4] Prueba `tests/CafManagerConection.Ssh.Tests/ArbolSftpTests.cs` contra el
  contenedor OpenSSH: carga por demanda del nivel desplegado, enlaces simbólicos omitidos e
  informados (FR-189, FR-189c, referencia FR-078)
- [x] T661 [US4] Implementar la exploración de directorios por demanda en
  `src/CafManagerConection.Ssh/ExploradorSftp.cs`, sobre el listado que hoy resuelve
  `RemoteFileSession.ListAsync` (`src/CafManagerConection.Ssh/RemoteFileSession.cs:83-93`), cuyo
  filtro deja pasar los enlaces simbólicos (FR-189, FR-189c) — depende de T660. No hay
  `ExploradorSftp.cs`: la carga por demanda es `NodoRemoto`
  (`src/CafManagerConection.App/Panels/FilesPanel.Arbol.cs:134`), que trae un marcador «Cargando…»
  hasta que se despliega, y el filtro de enlaces con su cuenta es `RemoteListing`
  (`src/CafManagerConection.Ssh/RemoteFileSession.cs:42-66`)
- [x] T662 [US4] Reemplazar la lista plana de
  `src/CafManagerConection.App/Panels/FilesPanel.xaml(.cs)` por el árbol de T661 (FR-189)
- [x] T663 [US4] Confirmar dónde guardar antes de bajar y confirmar el directorio remoto antes de
  subir, en `FilesPanel.xaml.cs` (FR-189a)
- [x] T664 [US4] Icono y color por tipo de archivo en el explorador, reusando `JuegoDeIconos` (T653)
  y `PaletaIconos`, en `FilesPanel.xaml` (FR-189b) — depende de T653 (US3)
- [ ] T665 [US4] Validar manualmente los escenarios de aceptación 1 a 4 de US4 y SC-042 con un
  directorio de al menos 50 archivos y 3 niveles, verificado por suma de verificación

**Checkpoint**: US1 a US4 funcionan juntas y por separado.

---

## Phase 7: User Story 5 - Trabajar cómodo en una sesión RDP (Priority: P5)

**Goal**: entrar con la identidad de Windows cuando el dominio lo permita, maximizar la sesión dentro
de la aplicación, y sacarla a una ventana propia sin reconectar. Depende de R1 (T600) y R2 (T601)
sólo para la ventana propia y la identidad de Windows; el maximizado no depende de ninguno.

**Independent Test**: una sesión sale a una ventana propia y vuelve sin reconectarse; y ocupa toda la
ventana de la aplicación y vuelve, también sin reconectarse.

- [x] T666 [US5] Según el resultado de T600: si el control sobrevive al reparent, implementar
  `src/CafManagerConection.App/Views/VentanaDeSesion.xaml(.cs)` para sacar la sesión RDP a una
  ventana propia y devolverla; si no sobrevive, la ventana propia se abre en lugar de la pestaña al
  iniciar la sesión, sin intercambio en caliente, y esta tarea deja escrito el motivo (FR-187) —
  depende de T600
- [x] T667 [US5] Cerrar la ventana propia devuelve la sesión a su pestaña en lugar de cortarla, en
  `VentanaDeSesion.xaml.cs` (FR-187) — depende de T666
- [x] T668 [US5] Según el resultado de T601: si entra con NLA, configurar
  `src/CafManagerConection.Rdp/RdpSession.cs` para usar la identidad de Windows sin usuario ni
  contraseña, cayendo al pedido de credenciales fuera del dominio o sin confianza; si falla, no se
  ofrece el tilde de identidad de Windows y esta tarea deja escrito el motivo (FR-186) — depende de
  T601
- [x] T669 [US5] Maximizar la sesión **dentro de la aplicación**: la pestaña activa ocupa toda la
  ventana ocultando la columna del árbol (`src/CafManagerConection.App/Views/MainWindow.xaml:123`,
  `_columnaLateral`) y la consola de traza (`_divisorConsola`, línea 273), y volver la deja como
  estaba sin reconectar la sesión, en `src/CafManagerConection.App/Views/MainWindow.xaml.cs`
  (FR-187). No depende de T600 ni de T601: no mueve el control de lugar — lo hace
  `RecorteDeLaVentana` (`src/CafManagerConection.App/Views/SessionView.xaml.cs:1065`, aplicada en la
  877), que recorre el árbol lógico escondiendo todo lo que rodea a la sesión y lo devuelve con
  `Deshacer`, sin nombrar `_columnaLateral` ni `_divisorConsola`
- [ ] T670 [US5] Validar manualmente los escenarios de aceptación 1 a 5 de US5 y SC-043

**Checkpoint**: US1 a US5 funcionan juntas y por separado. Si R1 o R2 dieron que no, US5 queda
reducida a lo que su propio requisito admite, y no se disimula con una reconexión.

---

## Phase 8: User Story 6 - Abrir la herramienta externa sin que me pida lo que ya sabe (Priority: P6)

**Goal**: WinSCP recibe la ruta de la clave privada; FileZilla y una conexión por contraseña avisan
en lugar de fallar en silencio.

**Independent Test**: abrir WinSCP desde una conexión con clave privada entra sin preguntar nada.

- [x] T671 [P] [US6] Ampliar `tests/CafManagerConection.Infrastructure.Tests/HerramientasExternasTests.cs`
  contra `LineaDeComando` (`src/CafManagerConection.Infrastructure/HerramientasExternas.cs`): la URL
  armada para WinSCP incluye la ruta de la clave privada, nunca una contraseña ni por línea de
  comandos ni por archivo, y FileZilla nunca la recibe (FR-188, FR-188b)
- [x] T672 [US6] Corregir
  `src/CafManagerConection.Infrastructure/HerramientasExternas.cs` (`Url()`) para pasar la clave a
  WinSCP y no a FileZilla (FR-188, FR-188b) — depende de T671
- [x] T673 [US6] Aviso «la herramienta va a pedir la contraseña» cuando la conexión se autentica por
  contraseña, al abrir cualquiera de las tres herramientas, en
  `src/CafManagerConection.App/Views/MainWindow.Acciones.cs` (FR-188a)
- [ ] T674 [US6] Validar manualmente los escenarios de aceptación 1 y 2 de US6 y SC-053

**Checkpoint**: las seis historias funcionan juntas y por separado.

---

## Phase 8a: Lo que cruza historias

Tres tareas que no caben dentro de una sola historia porque dependen de otra: cada una lleva la
etiqueta de la historia a la que le sirve y la tarea de la que depende.

- [x] T675 [US2] Separar en `src/CafManagerConection.App/Views/TextViewerWindow.xaml(.cs)` el
  seguimiento en vivo de T637 del uso estático con `esRegistro: false` que
  `src/CafManagerConection.App/Panels/PanelesPlataforma.cs:617` abre en modal para la configuración
  de nginx: ese uso MUST NOT arrancar el seguimiento. Si separarlos exige partir la ventana en dos,
  se parte (FR-185e) — depende de T637. Se partió: `TextViewerWindow.xaml.cs` quedó en 123 líneas
  sin una sola llamada a `SeguirAsync`, y su único consumidor es la configuración de nginx
  (`src/CafManagerConection.App/Panels/PanelesPlataforma.cs:617`)
- [x] T676 [US4] Informar en la interfaz, sin ofrecerlos ni transferirlos, los enlaces simbólicos
  que T661 detecta y omite del árbol, en `src/CafManagerConection.App/Panels/FilesPanel.xaml(.cs)`
  (FR-189c) — depende de T661, T662
- [ ] T677 [US1] Validar manualmente SC-038a contra un servidor real: comparar, para el mismo
  proceso y al mismo tiempo, el % de CPU que informa `ProcesosPanel` (T627) contra el de
  `top -b -n 2`, y verificar que no difieren en más de 5 puntos — depende de T627

**Checkpoint**: FR-185e, FR-189c y SC-038a quedan cubiertos.

---

## Phase 9: Cierre

- [ ] T678 Revalidar la tabla del Constitution Check de
  `specs/002-procesos-registros-y-arbol/plan.md` (Principios I a VI) contra el código final de las
  seis historias, dejando constancia en particular de FR-184e y de SC-052, SC-052a, SC-052b y
  SC-052c, que son la excepción acotada del Principio II y sus cinco reglas
- [ ] T679 Verificación manual final contra un servidor real de los criterios SC-038 a SC-053 que no
  quedaron cubiertos por una prueba automática, con capturas guardadas en
  `specs/002-procesos-registros-y-arbol/evidencia/`

---

## Dependencies & Execution Order

### Grafo de fases

```
Phase 1 (R1, R2, R3)
   │            │
   │            └──> Phase 7 (US5, depende de R1 y R2)
   └──> afecta a Phase 3 (US1, depende de R3)

Phase 2 (Foundational: ReorderAsync, ResultadoDeSondeo, migración 006)
   │
   ├──> Phase 3 (US1)
   │       │
   │       └──> Phase 4 (US2, sólo por el botón de escalar T628→T641)
   │
   ├──> Phase 5 (US3, la persistencia del icono depende de la migración 006, T608)
   │       │
   │       └──> Phase 6 (US4, sólo por T664, icono de archivo con JuegoDeIconos de T653)
   └──> Phase 8 (US6)

Phase 8a (T675 depende de T637, T676 depende de T661/T662, T677 depende de T627)

Phase 3, 4, 5, 6, 7, 8, 8a completas ──> Phase 9 (Cierre)
```

### Dependencias entre historias

- **US1** no depende de otra historia.
- **US2** depende de US1 sólo por el botón de escalar (T628 → T641); el resto es propio.
- **US3** no depende de otra historia. Su tramo de icono depende de la migración 006 (T608), que es
  de la fase Foundational y no de otra historia.
- **US4** depende de US3 sólo por T664 (icono de archivo, reusa `JuegoDeIconos` de T653); el resto
  es propio.
- **US5** depende de R1 y R2 (Fase 0), no de otra historia.
- **US6** no depende de otra historia.

### Dentro de cada historia

- La prueba de la lógica pura se escribe y falla antes de su implementación (Principio III):
  T609→T610, T611→T612, T613→T614, T615→T616, T617→T618, T620→T624, T621→T622, T623→T624,
  T633→T634, T635→T636, T604→T605, T643→T644, T652→T653, T660→T661, T671→T672. La migración 006
  sigue la misma regla dentro de T608: la prueba de la subida se escribe antes que la migración.
- La interfaz WPF (paneles, ventanas) no tiene arnés de pruebas: se verifica a mano o, cuando el
  defecto es del propio XAML, con una prueba estática como `EstilosAplicadosTests.cs` o
  `RecursosPedidosTests.cs` — este `tasks.md` no repite esa clase de prueba porque no hay un XAML
  nuevo con el mismo riesgo, salvo que al escribir T626, T627, T637, T650, T651, T658 o T666
  aparezca un
  `Style` con `TargetType` incorrecto, en cuyo caso esas dos pruebas ya existentes lo atrapan sin
  tarea nueva.

---

## Parallel Example: User Story 1

```bash
# Las cuatro pruebas de lógica pura de US1 no comparten archivo y se escriben juntas:
Task: "T609 Prueba de ParserDeProcesos en tests/CafManagerConection.Monitoring.Tests/ParserDeProcesosTests.cs"
Task: "T611 Prueba de MuestraDeProcesos en tests/CafManagerConection.Monitoring.Tests/MuestraDeProcesosTests.cs"
Task: "T613 Prueba de ArbolDeProcesos en tests/CafManagerConection.Monitoring.Tests/ArbolDeProcesosTests.cs"
Task: "T615 Prueba de ParserDeIo en tests/CafManagerConection.Monitoring.Tests/ParserDeIoTests.cs"
```

## Parallel Example: User Story 3

```bash
# El tramo de orden/arrastre y el de icono no comparten archivo hasta T655:
Task: "T643 Prueba de orden alfabético en tests/CafManagerConection.UseCases.Tests/Folders/OrdenAlfabeticoTests.cs"
Task: "T652 Prueba del juego de iconos en tests/CafManagerConection.Domain.Tests/JuegoDeIconosTests.cs"
```

---

## Implementation Strategy

### Mínimo entregable: US1 sola

Fase 1 (los tres experimentos) + Fase 2 (Foundational) + Fase 3 (US1) dejan el panel de procesos con
escalada de `sudo` funcionando. Ninguna otra historia hace falta para que esto tenga valor solo.

### Crecimiento incremental

1. Fase 1 + Fase 2 → base lista.
2. + US1 → panel de procesos y sondeo de `sudo` (MVP).
3. + US2 → los dos visores de FR-185d siguen en vivo. Reusa el botón de escalar de US1.
4. + US3 → árbol reordenable con orden, arrastre, alfabético, etiqueta, ventanas, e icono y color
   persistidos por la migración 006.
5. + US4 → árbol SFTP con confirmación de destino.
6. + US5 → RDP con identidad de Windows, maximizado dentro de la aplicación y ventana propia,
   según lo que hayan dado R1 y R2.
7. + US6 → clave privada a WinSCP y aviso de contraseña. Es la más chica y va al final.
8. Fase 9 → cierre: verificación manual completa y revalidación del Constitution Check.

Parar después de cualquier historia deja algo usable, tal como lo dice el plan.

---

## Tabla de cobertura FR → tareas

| FR | Tareas |
| --- | --- |
| FR-183 | T609, T610, T613, T614, T615, T616, T627, T632 |
| FR-183a | T627, T632 |
| FR-183b | T609, T610, T611, T612 |
| FR-183c | T627, T632 |
| FR-183d | T629, T632 |
| FR-173d | T611, T612, T630 |
| FR-184 | T606, T607, T617, T618, T619 |
| FR-184a | T628, T641 |
| FR-184b | T628 |
| FR-184c | T617, T618, T619, T631 |
| FR-184d | T606, T607, T617 |
| FR-184e | T620, T621, T622, T623, T624, T625, T626 |
| FR-185 | T633, T634, T637 |
| FR-185a | T638 |
| FR-185b | T639 |
| FR-185c | T633, T635, T636, T640 |
| FR-185d | T637, T638, T640 |
| FR-185e | T675 |
| FR-193 | T603, T605, T646, T648 |
| FR-193a | T643, T644, T645 |
| FR-193b | T646, T648 |
| FR-193c | T643, T644, T647 |
| FR-194 | T604, T605, T648 |
| FR-194a | T680 |
| FR-195 | T608, T655, T657, T658 |
| FR-195a | T652, T653 |
| FR-195b | T656, T658 |
| FR-195c | T652, T653, T654 |
| FR-190 | T649 |
| FR-196 | T650 |
| FR-196a | T651, T659 |
| FR-189 | T660, T661, T662 |
| FR-189a | T663 |
| FR-189b | T664 |
| FR-189c | T660, T661, T676 |
| FR-186 | T668 |
| FR-187 | T666, T667, T669 |
| FR-188 | T671, T672 |
| FR-188a | T673, T674 |
| FR-188b | T671, T672, T674 |

Todos los FR de la sección de Requisitos tienen al menos una tarea.

## Tabla de cobertura SC → tareas

| SC | Tareas |
| --- | --- |
| SC-038 | T632 |
| SC-038a | T677 |
| SC-039 | T632 |
| SC-040 | T642 |
| SC-042 | T665 |
| SC-043 | T670 |
| SC-045 | T659 |
| SC-046 | T659 |
| SC-047 | T648, T659 |
| SC-048 | T659 |
| SC-049 | T642 |
| SC-050 | T632 |
| SC-050a | T602, T632 |
| SC-051 | T631 |
| SC-052 | T620 |
| SC-052a | T623 |
| SC-052b | T621, T625 |
| SC-052c | T621, T624 |
| SC-053 | T674 |

---

## Lo que falta

49 tareas marcadas, 31 abiertas.

### Sin construir

- **T620 a T626 — el pedido de contraseña de `sudo` (FR-184e, SC-052 a SC-052c).** No existen
  `src/CafManagerConection.Ssh/ContrasenaDeSudoDeSesion.cs`,
  `src/CafManagerConection.Ssh/IPedidoDeContrasenaDeSudo.cs` ni la ventana del pedido. Con clave SSH
  y un `sudo` que pide contraseña, `SondaDeSudo` declara la escalada imposible. El reintento con la
  contraseña **de la conexión** sí está, en `SshCommandRunner.cs:237`.
- **T631** — la prueba de SC-051; el contador ya está en `SshSession.cs:310`.
- **T638** — la ruta del archivo en `ContenedorWindow`: hoy declara la fuente y la hora de la última
  línea, y la ruta exige un `docker inspect` que no se pide.
- **T641** — el botón de escalar en el visor de registro: el `tail -F` va sin `sudo`
  (`PanelesPlataforma.cs:1369`).
- **T656** — la prueba contra `SettingsResolver.Resolve`.
- **T678** — el Constitution Check no se puede revalidar hasta que cierre FR-184e, que es la
  excepción acotada del Principio II.

### Necesita un servidor real

- **T602** (R3), con 400 o más procesos.
- **T617**, **T617c** y **T633**, contra el contenedor OpenSSH o el servidor de
  `PruebaDeIntegracionSshAttribute`. T617c es la que habría atajado el defecto de T617b:
  `scripts/sshd-prueba.ps1` levanta el usuario `pruebaclave`, cuyo `sudo` sí pide contraseña.
- **T627** — el panel de procesos no se vio con datos.
- **T632, T642, T659, T665, T670, T674, T677, T679** — las validaciones manuales de las seis
  historias y el cierre.

### Necesita el equipo del usuario

- **T600** (R1) y **T601** (R2) — un equipo del dominio y un servidor RDP que confíe.
- **T650, T651, T654** — el XAML está escrito; falta mirar en pantalla las dos ventanas de
  configuración y las tres geometrías de icono nuevas.

### Necesita una decisión

- **T635 y T636** — la detección de línea de error ya vive en
  `src/CafManagerConection.Platform/NivelDeLinea.cs`, de FR-100f, y la usan los dos visores. Se
  duplica en `Domain/Monitoring/DeteccionDeLineaDeError.cs` o las dos tareas se cierran contra esa.

### Deuda técnica

- `TopProcessesParser` (`src/CafManagerConection.Monitoring/ParsersExtendidos.cs:195`) quedó sin
  consumidor de producción: T630 sacó los dos `ps --sort` de `MetricsCollector`. Sigue vivo sólo
  para `tests/CafManagerConection.Monitoring.Tests/DatosRealesTests.cs`.
- El tilde de identidad de Windows es por conexión, en el campo propio reservado
  `cmc:rdpIdentidadDeWindows` (`src/CafManagerConection.Domain/Connections/ProtocolSettings.cs:32`).
  Heredarlo de la carpeta exige una migración 007 que no está escrita.
- La copia de seguridad corre después de las migraciones: `App.xaml.cs:49` crea el
  `CompositionRoot`, que migra, y la copia se dispara desde el `MainWindow` de la línea 51.
- `Etiqueta.Color`, `Folder.IconColor` y `Connection.IconColor` guardan una clave de la paleta y no
  un color.
