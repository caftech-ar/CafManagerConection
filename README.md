# CafManagerConection (CMC)

Administrador de servidores para Windows: sesiones RDP y SSH en pestañas, con carpetas, herencia de
credenciales y entradas web. Interfaz WPF; el terminal y el cliente RDP van alojados con
`WindowsFormsHost`, que es lo único para lo que se conserva WinForms. Versión 0.2.0.

Cuatro clases de afirmación de este documento se cruzan contra el código en cada corrida de las
pruebas: la versión que declara, los nombres de tarea que menciona, los atajos que lista y las
rutas `src/…` y `tests/…` que cita. Todo lo demás —las rutas de datos, los números medidos, las
notas técnicas— se comprueba a mano.

## Estructura

Nueve proyectos. Las flechas son las `ProjectReference` reales:

```
Domain ──────────────► (nada: ni proyectos ni paquetes)
UseCases ────────────► Domain
Infrastructure ──────► UseCases          Dapper · Microsoft.Data.Sqlite · Serilog
Monitoring ──────────► UseCases          métricas de Linux por /proc
Platform ────────────► UseCases          inventario de Docker, nginx y supervisord
Rdp ─────────────────► UseCases          ActiveX de Windows por enlace tardío
Ssh ─────────────────► UseCases          SSH.NET
Terminal ────────────► UseCases          emulador VT propio
App ─────────────────► los ocho          WPF · OxyPlot.Wpf
```

Ningún adaptador referencia a otro: cuando dos necesitan hablar, el puerto se declara en
`src/CafManagerConection.UseCases/Abstractions` y los dos dependen del puerto. Hay una prueba que
lo impide.

La capa de casos de uso se llama `UseCases` y no `Application` a propósito: `Application` colisiona
con `System.Windows.Forms.Application` y produce el error CS0118 en cualquier archivo del namespace
`CafManagerConection.*`. Hay una prueba de arquitectura que impide que el nombre vuelva.

## Cómo se compila y se corre

El proyecto usa [go-task](https://taskfile.dev). `task --list` muestra todo.

```powershell
task run              # levanta la aplicación desde el código
task watch            # igual, pero reinicia al guardar cambios
task start            # ejecuta el binario ya publicado
task test             # toda la suite
task test:quick       # sólo Domain y UseCases
task check            # Release más todas las pruebas
task publish          # prueba, publica self-contained y arma el ZIP
task data             # qué archivos hay en la carpeta de datos, con tamaño y fecha
task logs             # últimas líneas del registro
task creds            # dónde hay contraseña guardada, nunca el secreto
task audit:secrets    # verifica que ningún secreto llegó a los registros
task reset:data       # PELIGRO: borra la carpeta de datos entera
task clean            # borra bin, obj y publish
```

Sin `task`: `dotnet build`, `dotnet test` y
`dotnet run --project src/CafManagerConection.App`. El ejecutable publicado se llama `cmc.exe`.

## Dónde viven los datos

```text
%LocalAppData%\CafManagerConection\cmc.db   base de datos
%LocalAppData%\CafManagerConection\logs\    registros, rotación diaria, 30 archivos
%LocalAppData%\CafManagerConection\copias\  copias de la base, a la carpeta que se elija en Preferencias
```

Una contraseña vive en la fila que la usa —la conexión, o los ajustes de la carpeta de la que se
hereda— y en ningún otro lado. **La clave maestra es opcional y es lo único que protege**: con clave
maestra el secreto se cifra con AES-256-GCM bajo una clave derivada de ella, y el archivo no se abre
sin ella en ningún equipo; sin clave maestra el secreto se guarda en claro, y cualquiera que tenga
el archivo lo lee.

Hay dos modos y nada más: no existe ningún tercer camino que abra los secretos sin tipear la clave
maestra. Ponerla, cambiarla o quitarla recifra todas las contraseñas de una sola vez, dentro de una
transacción; guardar o borrar una escribe únicamente su fila.

Una clave privada SSH pegada en la aplicación no va a la base: se escribe en
`%USERPROFILE%\.ssh` con los permisos que exige OpenSSH, y la base guarda la ruta.

Para empezar de cero, cerrar la aplicación y borrar la carpeta de datos.

## Atajos

Los que atiende la ventana principal:

| Atajo | Qué hace |
| --- | --- |
| `Ctrl+F` | buscar en el árbol |
| `Ctrl+N` | nueva conexión |
| `Ctrl+K` | conexión rápida |
| `Ctrl+W` | cerrar la pestaña actual |
| `Ctrl+B` | mostrar u ocultar el panel lateral |
| `Ctrl+Tab` | alternar entre sesiones abiertas |
| `F11` | maximizar |
| `F12` | consola de traza |
| `Enter` | conectar lo elegido en el árbol |
| `F2` | editar lo elegido |
| `Delete` | eliminar lo elegido |

Una contraseña copiada al portapapeles se borra a los 30 segundos.

Una sesión SSH admite ocho paneles laterales, y cada uno aparece sólo si el servidor lo admite:
archivos por SFTP, estado, túneles, Docker, nginx, supervisord, puertos a la escucha y procesos.

## Notas técnicas que cuesta redescubrir

**`COMReference` no funciona con `dotnet build`.** La tarea `ResolveComReference` sólo existe en el
MSBuild de .NET Framework y falla con MSB4803. Por eso `RdpClientHost` deriva de `AxHost` a mano y
habla con el control por enlace tardío: así el proyecto compila con el SDK y se puede publicar desde
la línea de comandos.

**El control RDP no se registra con el ProgID que dice la documentación.** El registrado es
`MsTscAx.MsTscAx.N`, no `MsRdpClientNNotSafeForScripting`, que es el nombre de la coclase dentro del
type library: buscar por el segundo no encuentra nada aunque el control esté instalado. Y estar
registrado no significa que se pueda instanciar: en Windows 11 la 13 figura en el registro apuntando
a `mstscax.dll` pero su fábrica devuelve `CLASS_E_CLASSNOTAVAILABLE`, y la 12 y anteriores
funcionan. Hay que intentar activar cada versión y seguir buscando si falla.

**Al comprobar si un CLSID se puede activar, usar `CoCreateInstance` y no
`Activator.CreateInstance`.** El segundo crea un envoltorio administrado, y liberarlo con
`Marshal.ReleaseComObject` lo deja separado de su objeto COM: el control que se cree después con el
mismo CLSID hereda ese envoltorio inservible.

**Un `AxHost` no se puede probar en un test unitario**: necesita contenedor con ventana y bomba de
mensajes, y sin eso cuelga el proceso de pruebas. Lo comprobable sin interfaz es que el CLSID
elegido se pueda activar.

**`SqliteConnection.Open()` no falla con un archivo corrupto**: abre perezosamente y el error
aparece en el primer comando. Si la excepción escapa sin cerrar, la conexión queda abierta
bloqueando el archivo.

**Guardar la huella de la clave del servidor no alcanza: hay que compararla.** La aplicación la
guardaba al marcar «recordar», pero preguntaba en cada conexión porque nunca comparaba la clave
presentada con la guardada. El efecto de fondo es peor que la molestia: un diálogo que aparece
siempre enseña a aceptarlo sin leer, y entonces deja de proteger de un servidor suplantado, que es
lo único para lo que existe. La comparación vive en `HostKeyPolicy.YaEsConocida`, separada de
`SshSession` para poder probarla: dentro del manejador de `HostKeyReceived` no se llega desde una
prueba. Y es ordinal y sensible a mayúsculas, porque la huella es base64 y ahí una minúscula y su
mayúscula son valores distintos.

**Mica en WPF no se enciende con un solo atributo, son cuatro pasos.**
`DWMWA_SYSTEMBACKDROP_TYPE` sin extender el marco no dibuja nada en el área cliente, y extenderlo
con `DwmExtendFrameIntoClientArea` sin vaciar además el `BackgroundColor` del `CompositionTarget`
deja el panel lateral negro: WPF pinta ahí su propio fondo opaco antes que DWM. Recién con los
cuatro —modo oscuro, backdrop, marco extendido y fondo vaciado— aparece el tinte. Están en
`Temas.AplicarFondoMica`, y si DWM no concede el backdrop el fondo se deja opaco a propósito:
transparentar sin nada detrás deja la ventana negra, que es peor que no tener Mica. Extender el
marco a toda el área cliente además saca ClearType de todo el texto y engrosa el del terminal, que
es una ventana GDI hija.

**Con `UseWPF` y `UseWindowsForms` juntos, decenas de nombres quedan ambiguos** —`Point`,
`UserControl`, `ContextMenu`, `KeyEventArgs`, `DragEventArgs`—. Se resuelve quitando
`System.Windows.Forms` y `System.Drawing` de los usings implícitos: el nombre corto pasa a ser el de
WPF y lo poco que necesita WinForms se escribe calificado.

**En WPF, `PasswordBox` no deriva de `TextBox`**, así que su estilo no puede usar
`BasedOn="{StaticResource {x:Type TextBox}}"`. Falla al inicializar, no al compilar.

**Un `RotateTransform` con nombre no puede ser destino de un `Trigger`.** Hay que asignar el
transform completo con un `Setter`, no su ángulo.

**Una plantilla propia que se come una parte obligatoria no rompe la compilación ni tira ninguna
excepción**: el control se dibuja y la parte que falta simplemente no existe. Un `MenuItem` sin
`Popup` dibujaba la fila del submenú y no la desplegaba nunca.

**WPF no enlaza contra los miembros de un tipo no público y no avisa**: sin excepción, sin registro,
la ventana abre y las columnas salen en blanco. Y rechaza un `Style` cuyo `TargetType` no coincide
con el elemento recién al cargar el XAML, también sin error de compilación.

**`SetForegroundWindow` falla en silencio desde un script.** Windows sólo deja robar el primer plano
a un proceso que acaba de recibir entrada del usuario, así que una tecla enviada con `SendKeys` desde
un script se pierde sin aviso y la misma prueba pasa o falla según lo que esté haciendo el
escritorio. Mandar un ALT suelto con `keybd_event` antes satisface la condición.

**`CopyFromScreen` puede capturar negro** cuando el fondo lo compone DWM y no está en el framebuffer
de la pantalla. `PrintWindow` con `PW_RENDERFULLCONTENT` lo pide al compositor.

**Un comando remoto de varias líneas tiene que viajar con saltos de línea de Unix.** Los literales
de cadena cruda de C# conservan los saltos del archivo fuente, y en Windows son CRLF: bash contesta
`command not found` por cada línea y rompe cualquier `if`/`fi`, así que el guion entero no se
ejecuta. El mismo texto pegado a mano en una terminal funciona perfecto, y por eso el fallo no se ve
venir. `SshCommandRunner.RunAsync` normaliza con `ReplaceLineEndings`.

**El estado de salida de un guion es el de su último comando.** Un guion de detección que termina en
`command -v supervisorctl && echo ...` sale con 1 en cualquier servidor sin supervisord. Si el
llamador descarta el resultado por el estado de salida, pierde también todo lo que sí detectó. Va
`exit 0` al final.

**No alcanza con buscar `supervisorctl` en el `PATH`**: es muy común instalar supervisor en un
virtualenv de Python, y ahí el binario no queda en el `PATH` aunque supervisord esté corriendo. Se
resuelve desde el proceso vivo, leyendo `/proc` por PID y no la salida de `pgrep -af`: esa busca en
la línea de comando de todo el sistema, y el guion de detección se encontraba a sí mismo y terminaba
leyendo su propio texto como configuración. En `/proc` los argumentos vienen separados por NUL, así
que una ruta con espacios llega entera.

**`supervisorctl` devuelve 3 cuando hay procesos detenidos, y eso no es falta de permiso**: es una
respuesta válida. Tomarlo por error escondía el panel justo en el caso más común, que es tener algo
caído y querer verlo.

**A qué proyecto compose pertenece un contenedor se pregunta por sus etiquetas**
(`com.docker.compose.project` y `.service`), nunca por el nombre: el separador cambió entre la v1 y
la v2 de Compose, y `container_name` permite ponerle cualquier cosa.

**Docker mezcla las dos convenciones de tamaño**: `KiB`/`MiB`/`GiB` son potencias de 1024 pero
`kB`/`MB`/`GB` son de 1000. Tratar todo como 1024 da un 7 % de error en los valores en GB.

**`docker ps --no-trunc` da el identificador de 64 caracteres y `docker stats` el de 12.**

**El instalador mostraba mojibake donde debía ir un acento.** `makensis` decide la codificación del
fuente por el BOM, y sin BOM lo lee en la página ANSI del sistema. `Unicode true` no alcanza: eso
rige el ejecutable generado, no cómo se lee el `.nsi`.

## Lo que vigila la suite sobre el propio repositorio

Además de probar el comportamiento, hay guardianes que leen los archivos del repositorio y fallan por
lo que encuentran escrito. La última columna dice si el guardián tiene además una prueba que le
siembra el defecto: sin ella pasa en verde mientras el defecto está, así que las que faltan son
deuda. En «plantillas completas» y «recursos pedidos» hay una prueba que comprueba que la búsqueda
encuentra algo, que ataja que el patrón se rompa pero no que el defecto pase.

| Guardián | Qué impide | Se prueba |
| --- | --- | --- |
| densidad de comentario | que un proyecto pase el orden de magnitud de comentario; el techo es 15% por proyecto y el más alto hoy es `Domain.Tests`, en 4% | sí |
| citas de requisito | que un comentario nombre un identificador de un documento que no existe | sí |
| citas de reglas | que se nombre por número un documento de reglas o uno de sus apartados | sí |
| citas por número de línea | que se cite `archivo.ext` y un número, que se corre con la primera edición | sí |
| rutas de los documentos | que este README enlace a un archivo `src/…` o `tests/…` que ya no está | sí |
| coherencia de este README | que declare una versión, un nombre de tarea o un atajo que el código no tiene, o que aparezca un segundo documento | sí |
| arquitectura | que el dominio dependa de algo, que un adaptador dependa de otro, que vuelva el namespace `Application` | sí |
| datos de una red real | que un archivo lleve una IP, un host o un usuario de la red de la empresa; el repositorio es público | sí |
| nombres del almacén de Windows | que un texto mande al usuario a buscar su contraseña al Administrador de credenciales, donde no está | sí |
| plantillas completas | que una plantilla propia se coma una parte obligatoria del control | no |
| recursos pedidos | que se pida desde XAML o desde código un pincel o una geometría que no está declarada | no |
| estilos aplicados | que un `Style` se aplique a un elemento que su `TargetType` no acepta | no |
| estado inicial en el XAML | que un `IsChecked="True"` dispare su manejador cuando los campos con `x:Name` todavía son null | no |
| tipos enlazables | que WPF tenga que enlazar contra los miembros de un tipo no público | no |
| juego de iconos | que un icono del juego apunte a una geometría que `Estilos.xaml` no declara | no |
| pinceles de la paleta | que un color de la paleta no tenga pincel en los dos temas | no |
| instalador legible | que un `.nsi` con texto acentuado quede sin BOM y `makensis` lo lea en la página ANSI | no |

Que un secreto no llegue en claro a la base ni al registro no lo ataja un guardián: lo comprueba
`task audit:secrets`, que hay que correr a mano con la contraseña que se usó en las pruebas.

## Licencias de terceros

### Fluent UI System Icons

Los iconos del árbol y de los paneles, y el icono de la aplicación
(`src/CafManagerConection.App/Assets/cmc.ico`), son geometrías tomadas de
[Fluent UI System Icons](https://github.com/microsoft/fluentui-system-icons), de Microsoft, bajo
licencia MIT. No se usa el paquete ni la fuente tipográfica: se copiaron los datos del `path` de
cada SVG al diccionario de recursos, así que no hay dependencia en tiempo de ejecución. La licencia
exige conservar este aviso.

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

**Al agregar un icono nuevo**, usar la familia de 20px estilo `filled`. Fluent redibuja cada tamaño
en lugar de escalarlo: los de 16 tienen menos detalle y otro peso óptico, y mezclarlos deja grosores
distintos en la misma columna. Al copiar el `path`, anteponer el prefijo F1 a los datos: SVG rellena con la
regla *nonzero* y WPF usa *EvenOdd* por omisión, así que sin ese prefijo las partes macizas que se
solapan salen como agujeros.

### Paquetes

`Dapper`, `Microsoft.Data.Sqlite`, `Serilog`, `Serilog.Sinks.File`, `SSH.NET` y `OxyPlot.Wpf`, cada
uno bajo su propia licencia.
