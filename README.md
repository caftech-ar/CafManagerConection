# CafManagerConection (CMC)

Administrador de servidores para Windows 11: sesiones RDP y SSH en pestañas, con carpetas,
herencia de credenciales y configuración, y entradas web.

La interfaz es **WPF**, con el lenguaje visual de **shadcn/ui** sobre la escala `zinc` en modo
claro. Los estilos son plantillas XAML propias: no hay código de dibujado ni bibliotecas de
terceros. Los iconos son geometrías de **Fluent UI System Icons** copiadas al diccionario de
recursos, sin paquete ni dependencia (ver [Licencias de terceros](#licencias-de-terceros)). El
terminal y el cliente RDP van alojados con `WindowsFormsHost`, que es lo único para lo que se
conserva WinForms.

Las reglas que gobiernan el proyecto están en
[`.specify/memory/constitution.md`](.specify/memory/constitution.md). La especificación
funcional, el plan y las tareas están en
[`specs/001-rdp-ssh-server-manager/`](specs/001-rdp-ssh-server-manager/).

---

## Probar la aplicación ahora

Ya hay un binario publicado y verificado:

```text
publish\CafManagerConection\cmc.exe
publish\CafManagerConection-win-x64.zip     (53 MB, portable)
```

Se ejecuta directo. No necesita .NET instalado, no pide privilegios de administrador y no
instala servicios.

### Qué se puede probar visualmente

| Qué mirar | Cómo |
| --- | --- |
| Reparto ajustable | Arrastrar el divisor entre el árbol y las sesiones |
| Alta de conexión | Botón **Nueva conexión** o `Ctrl+N` |
| Los tres protocolos | En el editor, cambiar el desplegable: RDP, SSH y Web muestran campos distintos |
| Árbol | Carpeta, monitor (RDP), consola (SSH) o globo (Web), cada uno con su color, y el destino al lado del nombre |
| Color de los iconos | Menú del árbol → Color de los iconos: diez colores, vista previa inmediata, y el tono se ajusta solo al tema |
| Herencia | Crear una carpeta, darle usuario y puerto, y crear conexiones dentro sin cargarlos |
| Búsqueda sin acentos | Escribir `produccion` y encontrar `Producción` |
| Pestañas de sesión | Doble clic en una conexión; el estado va como prefijo del título |
| Cerrar una sesión | `Ctrl+W`, el botón de la barra, o clic derecho en la pestaña |
| Entrada web | Crear una entrada Web con una URL y abrirla: se abre en el navegador del sistema |
| Atajos de teclado | `Ctrl+F` buscar · `Ctrl+N` nueva · `Ctrl+W` cerrar · `Ctrl+Tab` alternar · `F11` maximizar |
| Instancia única | Ejecutar el .exe dos veces: la segunda trae al frente la primera |
| Estado de la ventana | Mover, redimensionar, cerrar y reabrir |
| Menú contextual | Clic derecho sobre una conexión, una carpeta, o el vacío: cambia según el caso |
| Editar | Clic derecho → Editar, o `F2` sobre lo seleccionado |
| Copiar credenciales | Clic derecho → Copiar contraseña: se borra del portapapeles a los 30 s |
| Credenciales de carpeta | Clic derecho en una carpeta → Credenciales y valores heredados |
| Sesión SSH real | Doble clic en una conexión SSH: terminal con colores, `vim`, `htop`, `tmux` |
| Sesión RDP real | Doble clic en una conexión RDP |
| Fingerprint | Primera conexión SSH a un host: pide aceptar antes de mandar credenciales |
| Fingerprint recordado | Volver a conectar al mismo host: **no** vuelve a preguntar |
| Paneles laterales | Con una sesión SSH abierta, las letras de la derecha: A archivos, T túneles, E estado, D Docker, N nginx, S supervisord |
| Ensanchar un panel | Arrastrar el divisor entre el terminal y el panel |
| SFTP | Panel de archivos: navegar, arrastrar archivos desde el Explorador, descargar |
| Métricas | Panel de estado: CPU, memoria, discos y red |
| Túneles | Clic derecho en una conexión → Túneles…, definir uno y levantarlo desde el panel |
| Docker | Proyectos compose agrupados y contenedores sueltos aparte, con CPU y memoria del instante |
| Arrastrar y soltar | Mover una conexión a otra carpeta: avisa si cambia lo que hereda |

### Verificar que no se filtran secretos

Después de crear una conexión con contraseña:

```powershell
# La credencial esta en el Administrador de credenciales de Windows, con clave cmc:*
cmdkey /list | Select-String 'cmc:'

# ...y NO esta en la base ni en los logs
Select-String -Path "$env:LOCALAPPDATA\CafManagerConection\cmc.db" -Pattern 'TU-CONTRASEÑA' -SimpleMatch
Select-String -Path "$env:LOCALAPPDATA\CafManagerConection\logs\*" -Pattern 'TU-CONTRASEÑA' -SimpleMatch
```

Cero coincidencias en los dos últimos es el único resultado aceptable.

### Dónde viven los datos

```text
%LocalAppData%\CafManagerConection\cmc.db     base de datos
%LocalAppData%\CafManagerConection\logs\      registros, rotación diaria, 30 días
```

Para empezar de cero, cerrar la aplicación y borrar esa carpeta.

---

## Tareas (Taskfile)

El proyecto usa [go-task](https://taskfile.dev). `task --list` muestra todo.

```powershell
task run              # levanta la aplicación desde el código
task watch            # igual, pero reinicia al guardar cambios
task start            # ejecuta el binario ya publicado
task test             # las 397 pruebas
task test:quick       # sólo Domain y UseCases (milisegundos)
task check            # Release + todas las pruebas
task publish          # prueba, publica self-contained y arma el ZIP
task data             # cuántas carpetas, conexiones y credenciales hay
task logs             # últimas líneas del registro
task logs:follow      # sigue el registro en vivo
task creds            # credenciales que CMC guardó (nombres, nunca secretos)
task audit:secrets -- MiClave123    # verifica que no haya secretos en base ni logs
task import:preview -- ruta.xml   # qué se importaría del XML de RDM, sin escribir
task import -- ruta.xml           # importa de verdad (pide confirmación)
task reset:data       # PELIGRO: borra la base local (pide confirmación)
task clean            # borra bin, obj y publish
```

Sin `task`, los comandos equivalentes son `dotnet build`, `dotnet test` y
`dotnet run --project src/CafManagerConection.App`; para publicar, `./build/publicar.ps1`.

---

## Migración desde Remote Desktop Manager

CMC lee el XML que exporta Remote Desktop Manager y crea las carpetas y las conexiones
equivalentes, conservando la jerarquía. La ruta del archivo se pasa como argumento:

```powershell
task import:preview -- ruta/al/export.xml    # muestra qué se importaría, sin escribir
task import -- ruta/al/export.xml            # importa de verdad (pide confirmación)
```

| Tipo en RDM | Resultado |
| --- | --- |
| `SSHShell`, `Putty`, `PortForward` | conexiones SSH |
| `RDPConfigured` | conexiones RDP |
| `WebBrowser` | entradas web |
| `Group` | carpetas, con su jerarquía |
| `Ftp`, `AddOn`, `SessionTool`, `Credential` | omitidos, sin equivalente |

De los reenvíos de puerto se traen sólo los locales: CMC no hace reenvío remoto ni dinámico.

**Las contraseñas no se migran, y no se pueden.** RDM exporta el campo `SafePassword`
cifrado con la clave de su data source, y sin RDM no hay forma de descifrarlo. Hay que
volver a cargarlas; conviene hacerlo **a nivel de carpeta** en lugar de por conexión, para
que la herencia cubra las conexiones que comparten credencial.

El importador está en `tools/CafManagerConection.Import`, deliberadamente **fuera del
producto**: el Principio V mantiene los importadores fuera de alcance, y esto se corre una
sola vez.

---

## Estructura

```text
src/
├── CafManagerConection.Domain           entidades, sin ninguna dependencia
├── CafManagerConection.UseCases         servicios y puertos
├── CafManagerConection.Infrastructure   SQLite, Credential Manager, Serilog
├── CafManagerConection.App              WPF (XAML)
├── CafManagerConection.Rdp              adaptador del cliente RDP de Windows
├── CafManagerConection.Ssh              adaptador SSH.NET
├── CafManagerConection.Terminal         emulador VT y control de terminal propios
├── CafManagerConection.Monitoring       métricas de servidores Linux
└── CafManagerConection.Platform         inventario de Docker, nginx y supervisord
```

La capa de casos de uso se llama `UseCases` y no `Application` a propósito: `Application`
colisiona con `System.Windows.Forms.Application` y produce el error CS0118 en cualquier
archivo del namespace `CafManagerConection.*`. Hay una prueba de arquitectura que impide
que el nombre vuelva.

---

## Estado

**Las 10 historias de usuario están implementadas.** 217 pruebas automatizadas.

| Historia | Estado |
| --- | --- |
| US1 · RDP en pestañas | Control ActiveX de Windows, redirecciones apagadas |
| US2 · SSH con terminal | Emulador VT propio: 16/256/24 bits, pantalla alternativa, UTF-8 |
| US3 · Carpetas y herencia | Cascada completa, con procedencia de cada valor |
| US4 · Multi-sesión | Pestañas, reconexión, estado por sesión |
| US5 · Credenciales | Credential Manager, rotación, copiado con borrado diferido |
| US6 · SFTP | Explorador, transferencias con progreso, conflictos sin sobrescribir |
| US7 · Métricas Linux | CPU, memoria, carga, discos, red — por `/proc`, sin agentes |
| US8 · Túneles | Reenvío local, arranque automático, detección de puerto ocupado |
| US9 · Docker | Contenedores y proyectos compose, sólo lectura |
| US10 · nginx y supervisord | Sitios, configuración efectiva, procesos fallidos |

Más: menú contextual completo, arrastrar y soltar en el árbol, entradas web, atajos de
teclado e instancia única.

También el panel de **puertos a la escucha**: qué tiene abierto el servidor y qué proceso lo
tiene, en sólo lectura (constitución v1.11.0).

Las acciones de escritura sobre Docker y supervisord —iniciar, detener y reiniciar— ya están, con
confirmación previa que nombra el objeto y el servidor, como exige la constitución.

**Pendiente**: color en los visores de registro y de configuración, y la ficha del proceso que
ocupa un puerto, las dos incorporadas al alcance por la enmienda 1.11.0 y todavía sin escribir.

El detalle está en [`tasks.md`](specs/001-rdp-ssh-server-manager/tasks.md).

---

## Notas técnicas que cuesta redescubrir

**El control RDP no se registra con el ProgID que dice la documentación.** El registrado es
`MsTscAx.MsTscAx.N` (hasta la 13 en Windows 11), no `MsRdpClientNNotSafeForScripting`, que es
el nombre de la coclase dentro del type library. Buscar por el segundo no encuentra nada
aunque el control esté perfectamente instalado.

**`COMReference` no funciona con `dotnet build`.** La tarea `ResolveComReference` sólo existe
en el MSBuild de .NET Framework y falla con MSB4803. Por eso `RdpClientHost` deriva de
`AxHost` a mano y habla con el control por enlace tardío: así el proyecto compila con el SDK
y se puede publicar desde la línea de comandos.

**`TreeViewDrawMode.OwnerDrawAll` no dibuja nada propio**, ni expansores ni líneas.
`ShowPlusMinus` y `ShowLines` se ignoran en ese modo.

**`SQLiteConnection.Open()` no falla con un archivo corrupto**: abre perezosamente y el error
aparece en el primer comando. Si la excepción escapa sin cerrar, la conexión queda abierta
bloqueando el archivo.

**`CopyFromScreen` puede capturar negro** cuando el fondo lo compone DWM y no está en el
framebuffer de la pantalla. `PrintWindow` con `PW_RENDERFULLCONTENT` lo pide al compositor.

**`SetForegroundWindow` falla en silencio desde un script.** Windows sólo deja robar el primer
plano a un proceso que acaba de recibir entrada del usuario, así que una tecla enviada con
`SendKeys` desde un script se pierde sin ningún aviso y la misma prueba pasa o falla según lo
que esté haciendo el escritorio. Mandar un ALT suelto con `keybd_event` antes satisface la
condición. Es lo que hacen `scripts/captura.ps1` y `scripts/ventanas.ps1`.

**Para redondear un panel de WinForms no alcanza con dibujarlo**: los controles hijos son
rectangulares y tapan las esquinas. Hay que asignarle una `Region` con la forma, que recorta
el panel y todo lo que tenga adentro.

**Un formulario que oculta campos según el contexto tiene que recomponerse.** Poner
`Visible = false` deja un hueco del tamaño exacto de lo que se escondió; hace falta recolocar
las filas visibles y recalcular el alto de la ventana.

**Guardar el fingerprint no alcanza: hay que compararlo.** La aplicación lo guardaba
correctamente al marcar "recordar", pero la verificación preguntaba en cada conexión porque
nunca comparaba la clave presentada con la guardada. El efecto de fondo es peor que la molestia:
un diálogo que aparece siempre enseña a aceptarlo sin leer, y entonces deja de proteger de un
servidor suplantado, que es lo único para lo que existe. La comparación vive en
`HostKeyPolicy.YaEsConocida`, separada de `SshSession` justamente para poder probarla: dentro
del manejador de `HostKeyReceived` no se llega desde una prueba.

**Esa comparación es ordinal y sensible a mayúsculas.** El fingerprint es base64, donde `a` y
`A` son valores distintos: comparar sin distinguir aceptaría claves que no son la misma.

**El control RDP no se registra con el ProgID que dice la documentación**, y además **estar
registrado no significa que se pueda instanciar**: en Windows 11, `MsTscAx.MsTscAx.13` figura en
el registro apuntando a `mstscax.dll` pero su fábrica devuelve `CLASS_E_CLASSNOTAVAILABLE`; la
12 y anteriores funcionan. Hay que intentar activar cada versión y seguir buscando si falla.

**Al comprobar si un CLSID se puede activar, usar `CoCreateInstance` y no
`Activator.CreateInstance`.** El segundo crea un envoltorio administrado, y liberarlo con
`Marshal.ReleaseComObject` lo deja separado de su objeto COM: el control que se cree después con
el mismo CLSID hereda ese envoltorio inservible.

**Un `AxHost` no se puede probar en un test unitario**: necesita contenedor con ventana y bomba
de mensajes, y sin eso cuelga el proceso de pruebas. Lo comprobable sin interfaz es que el CLSID
elegido se pueda activar.

**En WPF, `PasswordBox` no deriva de `TextBox`**, así que su estilo no puede usar
`BasedOn="{StaticResource {x:Type TextBox}}"`. Falla al inicializar, no al compilar.

**Un `RotateTransform` con nombre no puede ser destino de un `Trigger`.** Hay que asignar el
transform completo con un `Setter`, no su ángulo.

**Con `UseWPF` y `UseWindowsForms` juntos, decenas de nombres quedan ambiguos** —`Point`,
`UserControl`, `ContextMenu`, `KeyEventArgs`, `DragEventArgs`—. Se resuelve quitando
`System.Windows.Forms` y `System.Drawing` de los usings implícitos: el nombre corto pasa a ser
el de WPF y lo poco que necesita WinForms se escribe calificado.

**Un comando remoto de varias líneas tiene que viajar con saltos de línea de Unix.** Los
literales de cadena cruda de C# conservan los saltos del archivo fuente, y en Windows son
CRLF: bash contesta `$'
': command not found` por cada línea y rompe cualquier `if`/`fi`,
así que el guion entero no se ejecuta. El mismo texto pegado a mano en una terminal funciona
perfecto, y por eso el fallo no se ve venir. `SshCommandRunner.RunAsync` normaliza con
`ReplaceLineEndings("
")`.

**El estado de salida de un guion es el de su último comando.** Un guion de detección que
termina en `command -v supervisorctl && echo ...` sale con 1 en cualquier servidor sin
supervisord. Si el llamador descarta el resultado por el estado de salida, pierde también
todo lo que sí detectó. Va `exit 0` al final.

**A qué proyecto compose pertenece un contenedor se pregunta por sus etiquetas**
(`com.docker.compose.project` y `.service`), nunca por el nombre: el separador cambió entre la
v1 y la v2 de Compose, y `container_name` permite ponerle cualquier cosa.

**Docker mezcla las dos convenciones de tamaño**: `KiB`/`MiB`/`GiB` son potencias de 1024 pero
`kB`/`MB`/`GB` son de 1000. Tratar todo como 1024 da un 7 % de error en los valores en GB.

**`Splitter` no puede redimensionar a su propio contenedor.** Redimensiona a un hermano suyo
dentro del mismo padre; si lo que hay que ensanchar es el contenedor donde vive, el arrastre no
hace nada. Hay que manejarlo a mano y calcular el ancho en coordenadas de pantalla, porque al
mover el borde el asa se mueve con él y las coordenadas locales cambian de referencia.

**Un control propio que recibe el tema en el constructor no lo vuelve a mirar.** Al cambiar de
claro a oscuro quedaba con la paleta anterior —el campo de búsqueda blanco sobre una ventana
oscura—. Los controles implementan `IThemed` y el cambio recorre el árbol una sola vez.

**Las barras de desplazamiento no pasan por `OnPaint`**: las dibuja Win32 y no hay forma de
pintarlas desde el control. Lo único elegible es el tema visual, con `SetWindowTheme` y
`Explorer` o `DarkMode_Explorer`, y de qué color salen lo decide
`Application.SetColorMode` — que debe seguir la preferencia **de la aplicación**, no la de
Windows, y sólo tiene efecto antes de crear el primer control. Con Windows en oscuro y la
aplicación en claro, seguir a Windows deja barras negras sobre listas blancas.

---

## Documentos de cierre

| Documento | Qué dice |
| --- | --- |
| [`docs/cobertura.md`](docs/cobertura.md) | Cobertura por capa: 54,2 % total, 70-84 % en las capas de reglas, y por qué los adaptadores están bajos |
| [`docs/revision-constitucional.md`](docs/revision-constitucional.md) | Los seis principios contra el código entregado, con las dos desviaciones declaradas |

---

## Licencias de terceros

### Fluent UI System Icons

Los iconos del árbol (carpeta, RDP, SSH, web) y el icono de la aplicación
(`src/CafManagerConection.App/Assets/cmc.ico`, generado a partir del glifo `window_console`)
son geometrías tomadas de
[Fluent UI System Icons](https://github.com/microsoft/fluentui-system-icons), de Microsoft,
bajo licencia MIT. No se usa el paquete ni la fuente tipográfica: se copiaron los datos del
`path` de cada SVG al diccionario de recursos, así que no hay dependencia en tiempo de
ejecución. La licencia exige conservar este aviso.

```
Copyright (c) 2020 Microsoft Corporation

Permission is hereby granted, free of charge, to any person obtaining a copy of this
software and associated documentation files (the "Software"), to deal in the Software
without restriction, including without limitation the rights to use, copy, modify, merge,
publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons
to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or
substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE
FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
DEALINGS IN THE SOFTWARE.
```

**Al agregar un icono nuevo**, usar la familia de **20px estilo `filled`**. Fluent redibuja
cada tamaño en lugar de escalarlo: los de 16 tienen menos detalle y otro peso óptico, y
mezclarlos deja grosores distintos en la misma columna. Al copiar el `path`, anteponer `F1`
a los datos — SVG rellena con la regla *nonzero* y WPF usa *EvenOdd* por omisión, así que sin
ese prefijo las partes macizas que se solapan salen como agujeros.
