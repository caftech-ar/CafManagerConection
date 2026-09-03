# Implementation Plan: Procesos, registros y árbol

**Feature**: `002-procesos-registros-y-arbol`

**Spec**: [spec.md](./spec.md)

**Created**: 2026-09-02

**Status**: en ejecución. `tasks.md` tiene 80 tareas (T600–T679): 49 verificadas contra el código y 31 abiertas, agrupadas por motivo al final de ese archivo.

## Summary

Lo que se pidió y no se construyó, en seis historias por prioridad: panel de procesos con escalada
por `sudo`, registros en vivo, orden e iconos del árbol, explorador SFTP en árbol, comodidad de la
sesión RDP, y la clave privada a las herramientas externas.

Ninguna necesita una dependencia nueva. Cuatro reusan piezas que ya existen y hoy sirven a un solo
consumidor.

La enmienda 1.14.0 autoriza las dos cosas que el alcance cerrado no cubría: elegir la forma del
icono, que trae la migración 006, y pedirle al usuario una contraseña de `sudo`, como excepción
acotada al Principio II. No queda ninguna puerta abierta antes de empezar.

## Technical Context

Todo lo de [la 001](../001-rdp-ssh-server-manager/plan.md#technical-context) sigue valiendo: C# 14
sobre .NET 10, WPF, SSH.NET, SQLite con Dapper, xUnit, y ninguna biblioteca de estilos.

Lo que cambia:

**Dependencias nuevas**: ninguna. El juego de iconos de FR-195 sale de **Fluent UI System Icons**,
que ya se usa así en la 001 —copiado como geometrías, no como paquete— y que es MIT.

**Migración de esquema**: la **migración 006** agrega una columna de clave de icono en `folders` y
otra en `connections` (FR-195), en paralelo a la de color. La exige la enmienda 1.14.0
(`constitution.md:772`) y está aprobada.

**Nada nuevo se ejecuta en el servidor** salvo dos comandos: el sondeo `sudo -n` de FR-184c y la
lectura de `/proc/<pid>/io` del panel de procesos. Los dos entraron por la enmienda 1.13.0.

## Constitution Check

*GATE: pasa antes de la Fase 0 y se revalida al cerrar cada historia.*

| Principio | Veredicto | Por qué |
| --- | --- | --- |
| I — Dominio aislado | pasa | El estado del sondeo de `sudo` y la muestra de proceso son tipos de dominio sin referencias a WPF ni a SSH.NET. |
| II — Ningún secreto fuera del almacén (NO NEGOCIABLE) | pasa con la excepción declarada | FR-184e reusa primero la contraseña de la conexión por `sudo -S -k` (`SshCommandRunner.cs:213`), que FR-095 ya autoriza. Cuando esa no sirve, pide una al usuario y la retiene mientras dure la sesión: eso es la excepción que la enmienda 1.14.0 declara en su título (`constitution.md:527`), y FR-184e transcribe sus cinco reglas como requisitos —sin persistencia, sin línea de comandos, sin registro, búfer en cero al cerrar, por sesión y no por conexión—. No alcanza a nada más: FR-188 y FR-188b sólo pasan la **ruta** de una clave privada. |
| III — Test-first en el núcleo | pasa | El cálculo de CPU instantáneo, el orden alfabético, la resolución de icono y el estado del sondeo son lógica pura y llevan prueba antes que código. |
| IV — WPF y código abierto | pasa | Ninguna dependencia nueva. El juego de iconos son geometrías copiadas, no una biblioteca. La ventana propia de RDP aloja un control **de este proceso**, que es lo que el principio permite. |
| V — Alcance cerrado | pasa | Cuatro orígenes distintos, ver «De dónde sale cada grupo» en la spec. La enmienda 1.13.0 cubre procesos, `sudo`, registros, RDP y herramientas externas. La enmienda 1.14.0 cubre elegir la forma del icono —«Icono elegible», `constitution.md:772`— y con ella FR-195, FR-195a y FR-195c. La cláusula «colorear y ordenar lo que ya se muestra» (`constitution.md:783`) cubre el orden, el color, el icono por tipo de archivo, la etiqueta por menú y la distribución de las ventanas. Los defectos de requisitos ya construidos —FR-173d, FR-183d, FR-189 y familia, FR-193a, FR-194— no amplían nada: el requisito ya está en alcance. |
| VI — Distribución sin privilegios | pasa | No agrega binarios nativos ni servicios. El paquete portable no cambia de forma. |

SC-052, SC-052a, SC-052b y SC-052c son los guardianes del Principio II en esta feature: la
contraseña de `sudo` no aparece en ningún registro, no queda en ningún almacén, deja el búfer en
cero al cerrar la sesión y se vuelve a pedir al reabrir la conexión.

**Complexity Tracking no lleva ninguna desviación**: no queda ninguna.

## Fase 0 — Lo que hay que averiguar antes de escribir código

Tres cosas no se saben, y las tres pueden cambiar el diseño. Cada una es un experimento acotado con
una respuesta binaria.

### R1 — ¿El control ActiveX de RDP sobrevive a un reparent? *(bloquea US5, FR-187)*

`RdpSession` aloja `mstscax.dll` en un `WindowsFormsHost`. Sacar la sesión a una `Window` propia y
devolverla a la pestaña es reparentar ese control entre dos árboles visuales de WPF, cada uno con su
`HwndSource`. Un control ActiveX con afinidad de hilo puede perder la conexión al cambiar de ventana
padre.

**Experimento**: una conexión RDP viva, mover el `WindowsFormsHost` a otra `Window` y volver.
**Respuesta buscada**: sigue conectado / se reconecta / se cae.
**Si se cae**: el requisito se cumple con una ventana propia que se abre **en lugar de** la pestaña
al iniciar la sesión, y el intercambio en caliente se declara imposible con su motivo. No se disimula
con una reconexión.

### R2 — ¿El control de RDP entra con la identidad de Windows? *(bloquea US5, FR-186)*

`RdpSession.Configure` siempre asigna `UserName` y, si hay secreto, `ClearTextPassword`. Para el
inicio de sesión único hace falta NLA con CredSSP delegando el token actual, y no está claro qué
propiedades de `IMsRdpClientAdvancedSettings` lo habilitan ni si funcionan fuera de un equipo unido
al dominio.

**Experimento**: contra un servidor del dominio, dejar usuario y contraseña vacíos con NLA activo.
**Respuesta buscada**: entra / pide credenciales / falla.
**Si falla**: FR-186 dice explícitamente que hay que caer al pedido de credenciales, así que el
requisito se cumple igual; lo que cambia es que el tilde de la interfaz no se ofrece.

### R3 — ¿Cuánto cuesta la segunda muestra del CPU instantáneo? *(afecta US1, FR-183b)*

El porcentaje instantáneo es la diferencia de `utime + stime` entre dos lecturas. Hay dos formas: dos
`ps` separados por el intervalo, o una lectura de `/proc/*/stat` que ya trae los tics acumulados y se
compara contra la muestra anterior del propio panel.

**Experimento**: medir las dos contra un servidor con 400+ procesos.
**Respuesta buscada**: milisegundos y bytes por muestra, y **cuánto núcleo consume en el servidor**.
**El presupuesto no es libre**: SC-018 de la 001 le pone al monitoreo un techo del **1 % de una CPU**,
sigue en alcance, y T511 ya midió que sólo el panel de estado cuesta **1 a 3 % con unos 700
procesos**. Con los dos paneles abiertos hay que entrar igual en ese 1 %.
**Sesgo**: la segunda, porque no duplica el costo por refresco. La primera sólo gana si la lectura de
`/proc` sale más cara de lo esperado. **Si ninguna entra en el presupuesto**, se baja la frecuencia o
se achica lo que se pide; no se sube el techo.

## Fase 1 — Diseño

### Lo que se reusa, y que hoy tiene un solo consumidor

| Pieza | Dónde está | Hoy la usa | La va a usar |
| --- | --- | --- | --- |
| `RunWithSudoFallbackAsync` | `Ssh/SshCommandRunner.cs:180` | Docker, supervisord, procesos | el sondeo de FR-184 y todo botón de escalar |
| `IPlatformLogStreamer.SeguirAsync` | `Ssh/SshCommandRunner.cs:331` | sólo `docker logs -f` | el visor de supervisord (FR-185) |
| `IConnectionRepository.ReorderAsync` | `Infrastructure/Database/ConnectionRepository.cs:119` | arrastre entre conexiones | el arrastre de FR-193b |
| `PaletaIconos` | `Domain/Settings/PaletaIconos.cs` | diez colores | color **e** icono (FR-195) |

### Lo que hay que crear

- **`SondaDeSudo`** en `Ssh/`: ejecuta el sondeo una vez y devuelve uno de tres estados. El estado
  vive en la sesión, no en la base.
- **`MuestraDeProcesos`** y su parser en `Monitoring/`: lee `/proc/*/stat` y `/proc/*/io`, arma el
  árbol padre-hijo por PPID y calcula el porcentaje contra la muestra anterior.
- **`ProcesosPanel`** en `App/Panels/`: la vista. El top de diez del panel de estado conserva las
  siete columnas que le exige FR-173 en `001/spec.md` —PID, usuario, CPU, memoria residente, hilos,
  estado y tiempo corriendo—, no agrega la jerarquía de hijos ni la E/S por proceso, y gana un
  enlace a este panel (FR-183d).
- **`IFolderRepository.ReorderAsync`** y su implementación, que no existen.
- **`JuegoDeIconos`** en `Domain/Settings/`: la lista cerrada de claves de icono, en paralelo a
  `PaletaIconos`. Las geometrías van al diccionario de recursos.
- **Migración 006**: la columna de clave de icono en `folders` y en `connections`.
- **El pedido de contraseña de `sudo`** y el búfer que la sostiene por sesión (FR-184e). El borrado
  copia el patrón de `EntradaDeContrasenaInteractiva.TomarTexto()`
  (`Ssh/EntradaDeContrasenaInteractiva.cs:52`), que pisa con ceros la lista **y** la copia de
  `ToArray` antes de soltarlas.

### Lo que se corrige en lugar de crear

- El orden al crear e importar: `CreateAsync` (`ConnectionService.cs:171`) e
  `ImportadorDeConexiones.CrearAsync` no asignan `SortOrder`, así que todo nace en 0 y salta al
  principio; las carpetas importadas, al revés, van al final (`ImportadorDeConexiones.cs:109`).
- `PuedenReordenarse` (`MainWindow.Acciones.cs:835`), que exige `EsCarpeta: false` de los dos lados.
- `LineaDeComando.Url()` (`Infrastructure/HerramientasExternas.cs:54`), que descarta la ruta de clave
  que su llamador ya resolvió.
- El visor de supervisord, que es una lectura única dentro de `TextViewerWindow`. **Esa ventana sirve
  a dos cosas**: el registro de supervisord y, con `esRegistro: false`, la configuración efectiva de
  nginx en modal (`PanelesPlataforma.cs:617`). El seguimiento en vivo no puede alcanzar al segundo
  uso; si separarlos exige partir la ventana en dos, se parte (FR-185e).
- El listado del explorador SFTP (`Ssh/RemoteFileSession.cs:72`), que muestra los enlaces simbólicos
  como archivos comunes porque su único filtro es el de `.` y `..` (línea 84). Hay que omitirlos y
  decir cuántos se omitieron (FR-189c).

## Project Structure

```text
src/
  CafManagerConection.Domain/
    Connections/        Folder.cs, Connection.cs          + Icono
    Settings/           JuegoDeIconos.cs                  nuevo
  CafManagerConection.Monitoring/
    MuestraDeProcesos.cs, ParserDeProcesos.cs             nuevos
  CafManagerConection.Ssh/
    SondaDeSudo.cs                                        nuevo
    SshCommandRunner.cs                                   seguir archivos, no sólo docker
  CafManagerConection.UseCases/
    Abstractions/Repositories.cs                          + IFolderRepository.ReorderAsync
    Folders/FolderService.cs                              orden alfabético y reordenamiento
  CafManagerConection.Infrastructure/
    Database/FolderRepository.cs                          ReorderAsync
    Database/Migrations/Migration006_Icono.cs             clave de icono en folders y connections
    HerramientasExternas.cs                               clave privada a WinSCP
  CafManagerConection.App/
    Panels/ProcesosPanel.xaml(.cs)                        nuevo
    Panels/FilesPanel.xaml(.cs)                           lista plana -> árbol
    Views/MainWindow.Acciones.cs                          etiqueta y orden en el menú contextual
    Views/FolderSettingsWindow.xaml                       dos columnas y secciones
    Views/ConnectionEditorWindow.xaml                     ídem
    Views/VentanaDeSesion.xaml(.cs)                       la ventana propia de RDP
tests/
  ... una carpeta por historia, con la prueba antes que el código en todo lo que sea lógica pura
```

## Orden de entrega

Cada historia se cierra entera —código, pruebas y verificación manual— antes de empezar la
siguiente. Parar después de cualquiera deja algo usable, pero **no son del todo independientes**:
US4 usa el juego de iconos de US3 para distinguir tipos de archivo (FR-189b), así que US3 va antes.

1. **US1** (P1) — procesos y `sudo`. Es el pedido más repetido y el que más código nuevo trae.
2. **US2** (P2) — registros en vivo. Depende de US1 sólo para el botón de escalar; el resto es propio.
3. **US3** (P3) — árbol. La única que toca el esquema: el juego de iconos y la migración 006 van
   primero dentro de la historia, porque US4 depende de ellos. El resto —orden, arrastre de
   carpetas, «ordenar alfabéticamente», etiqueta por menú, distribución de las ventanas— no
   depende de nada y es la mayor parte.
4. **US4** (P4) — SFTP en árbol.
5. **US5** (P5) — RDP. Va después de R1 y R2; si los dos experimentos dan que no, la historia se
   reduce a maximizar dentro de la aplicación y se dice por qué.
6. **US6** (P6) — herramientas externas. Es media hora y va al final sólo porque es lo más chico.

## Complexity Tracking

| Lo caro | Por qué | Qué lo abarata |
| --- | --- | --- |
| Reparentar el control de RDP | Afinidad de hilo de un ActiveX entre dos `HwndSource` | R1 lo responde antes de escribir la ventana |
| CPU instantáneo sin duplicar el costo | Dos muestras por refresco contra un servidor con miles de procesos | R3 elige entre dos caminos ya identificados |
| El árbol SFTP | `FilesPanel` es una lista plana; el árbol carga por demanda y hay que evitar leer el disco remoto entero | Cargar sólo el nivel que se despliega |
| El aviso de FR-185c | Distinguir «no pasa nada» de «el canal se cortó» exige un latido, no un `catch` | `ContenedorWindow` ya lo resuelve para Docker; se generaliza |

## Riesgos

- **La migración 006 toca el esquema de `folders` y de `connections`.** Agrega una columna que
  admite nulos y no reescribe ninguna fila existente, pero es la primera de esta feature que corre
  contra la base del usuario y la copia de seguridad diaria (enmienda 1.9.0) es lo único que la
  respalda.
- **La contraseña de `sudo` vive en memoria mientras dura la sesión.** Es el costo que la enmienda
  1.14.0 aceptó, y las cinco reglas de FR-184e son lo que lo acota. Un descuido en cualquiera de
  ellas —un `ToString()` en un mensaje de error, un búfer que no se pisa— es una fuga de
  credenciales, que es el único fallo del que la aplicación no se recupera. SC-052, SC-052a, SC-052b
  y SC-052c lo verifican.
- **R1 puede dar que no.** FR-187 quedaría cumplido a medias y hay que decirlo, no disimularlo.
- **El presupuesto de CPU del servidor puede no alcanzar** para tener el panel de estado y el de
  procesos abiertos a la vez dentro del 1 % de SC-018. R3 lo mide antes de escribir nada.
- **El sondeo de `sudo` deja rastro en el servidor.** Una sola vez por sesión es el techo, y hay una
  prueba (SC-051) que lo cuenta.

## Estado

Las seis historias tienen código y prueba, menos el pedido de contraseña de `sudo` de FR-184e —T620
a T626—, que no está escrito: con clave SSH y un `sudo` que pide contraseña, la escalada se declara
imposible.

Los tres experimentos de la fase 0 siguen sin correr. US5 se construyó sobre la rama de R1 en la que
el control de RDP sobrevive al reparent, y comprueba a los 1.500 ms si el traslado cortó la sesión
en lugar de reconectarla por atrás
(`src/CafManagerConection.App/Views/SessionView.xaml.cs:979`).

Lo que queda abierto, y por qué motivo, está al final de `tasks.md`.
