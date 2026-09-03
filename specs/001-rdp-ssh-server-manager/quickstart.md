# Guía de validación: CafManagerConection (CMC)

**Feature**: `001-rdp-ssh-server-manager` · **Fase**: 1

Cómo compilar, ejecutar y verificar que CMC cumple lo que promete. Los detalles del diseño
están en [`plan.md`](./plan.md), [`data-model.md`](./data-model.md) y
[`contracts/`](./contracts/); acá solo está lo que hay que **correr** y lo que hay que
**ver**.

---

## Prerrequisitos

| Requisito | Detalle |
| --- | --- |
| Windows 11 x64 | Única plataforma objetivo; no hay compromiso con versiones anteriores |
| SDK de .NET 10 | `dotnet --version` debe devolver `10.*` |
| SDK de Windows | Necesario para el interop del control ActiveX de RDP |
| Docker Desktop | Solo para las pruebas de integración SSH |
| Un servidor Windows con RDP | Para la validación manual de RDP |

Ninguno de estos hace falta para **usar** la aplicación publicada: eso es justamente lo que
verifica el escenario 6.

---

## Compilar y ejecutar

```powershell
dotnet restore
dotnet build -c Debug
dotnet run --project src/CafManagerConection.App
```

La primera ejecución crea `%LocalAppData%\CafManagerConection\cmc.db` y
`%LocalAppData%\CafManagerConection\logs\`.

---

## Pruebas automatizadas

```powershell
# Todas las pruebas
dotnet test

# Con cobertura
dotnet test --collect:"XPlat Code Coverage"

# Por capa
dotnet test tests/CafManagerConection.Domain.Tests
dotnet test tests/CafManagerConection.UseCases.Tests
dotnet test tests/CafManagerConection.Infrastructure.Tests
dotnet test tests/CafManagerConection.Terminal.Tests
```

Las pruebas de `Domain`, `UseCases` y `Terminal` no tocan red ni disco y corren en
segundos. Las de `Infrastructure` crean y destruyen una base SQLite temporal por prueba.

### Pruebas de integración SSH

Necesitan un servidor OpenSSH de verdad. Hay cosas que no se pueden comprobar con dobles: que
el saludo prospere, que la verificación de la clave del host corte **antes** de mandar la
contraseña, que el canal interactivo entregue datos, y que el cambio de tamaño llegue al otro
lado.

```powershell
task sshd:up

dotnet test tests/CafManagerConection.Ssh.Tests

task sshd:down
```

Si el contenedor no está corriendo, estas pruebas se omiten con un mensaje explícito en lugar
de fallar: una máquina sin Docker no tiene nada roto.

Va por `task sshd:up` y no por un `docker run` a mano porque al servidor hay que ajustarle dos
cosas para que la serie sea estable, y `scripts/sshd-prueba.ps1` explica cada una. La más
sorprendente es `PerSourcePenalties no`: desde OpenSSH 9.8 el servidor castiga a las direcciones
que fallan al autenticar y las descarta un rato. Una de las pruebas se autentica mal **a
propósito** —hay que comprobar que eso da un rechazo de credenciales y no otra cosa—, y a partir
de ahí sshd cortaba las conexiones de las pruebas siguientes.

### Prueba de fugas del control RDP

Verifica la regresión de `AxHost` documentada en [`research.md`](./research.md) sección 1.

```powershell
dotnet test tests/CafManagerConection.Rdp.Tests --filter Category=RdpLifecycle
```

**Resultado esperado**: tras abrir y cerrar 50 sesiones, los handles de usuario y GDI del
proceso vuelven a su línea base con una tolerancia acotada. Si crecen de forma sostenida, la
liberación explícita del COM no está funcionando y **no se puede seguir adelante**.

---

## Validación manual

La interfaz WPF se valida a mano en esta versión: es la excepción declarada por el
Principio III de la constitución. Este es el guion. Cada escenario nombra el criterio de
éxito que verifica.

### Escenario 1 — Primera conexión RDP (SC-001, SC-002)

1. Abrir la aplicación por primera vez, sin datos previos.
2. Crear una conexión RDP contra un servidor Windows accesible, con usuario y contraseña.
3. Hacer doble clic sobre ella.

**Esperado**: se abre una pestaña, pasa por "Conectando" y llega a "Conectado" mostrando el
escritorio remoto. La barra de estado muestra `Conectado · usuario@host`. Todo el recorrido,
desde abrir la aplicación por primera vez, toma menos de 3 minutos sin consultar
documentación.

**Verificar además**: abrir el Administrador de credenciales de Windows y confirmar que
existe una entrada `cmc:rdp:<GUID>`. Abrir `cmc.db` con cualquier visor de SQLite y confirmar
que la columna `credential_key` contiene esa clave y que **no hay ninguna contraseña**.

### Escenario 2 — Redirecciones RDP desactivadas (FR-017)

Dentro de la sesión RDP conectada, abrir el Explorador de archivos del servidor remoto.

**Esperado**: no aparecen las unidades del equipo local. Tampoco hay impresoras locales
redirigidas, ni dispositivos de audio del cliente. Con el portapapeles deshabilitado en la
conexión, copiar texto dentro de la sesión no lo deja disponible en Windows local.

### Escenario 3 — Terminal SSH con aplicaciones reales (SC-007, SC-008)

Conectarse por SSH a un servidor Linux y ejecutar, uno por uno:

| Programa | Qué mirar |
| --- | --- |
| `vim` | El archivo se dibuja completo; `:q!` sale limpio; los colores de sintaxis se ven |
| `nano` | La barra de atajos inferior se ve completa y bien alineada |
| `top` | Refresca sin parpadeo ni residuos de la pantalla anterior |
| `htop` | Las barras de color se ven correctas; el mouse selecciona; `F10` sale |
| `less` | El desplazamiento con flechas y `PgUp`/`PgDn` funciona; `q` sale |
| `tmux` | Se pueden dividir paneles y cambiar entre ellos; la barra de estado se ve |

Después, con `tmux` abierto y paneles divididos, **redimensionar la ventana de CMC**.

**Esperado**: el contenido se reajusta al nuevo tamaño sin corrupción visual y sin residuos.
Esto verifica que `ShellStream.ChangeWindowSize` está llegando al servidor.

Por último:

```bash
printf 'Acentos: áéíóú ñÑ ¿? ¡! — símbolos: ✓ ★ €\n'
for i in $(seq 0 255); do printf "\e[38;5;${i}m%3d " $i; done; printf "\e[0m\n"
ls --color=always /
```

**Esperado**: los acentos, la `ñ` y los símbolos se ven correctamente; los 256 colores se
muestran distintos entre sí; `ls` colorea directorios y archivos. Repetir con el tema claro y
con el oscuro.

### Escenario 4 — Verificación de la clave del host (FR-022, FR-023)

1. Conectarse por SSH a un host nuevo. **Esperado**: se muestra el fingerprint en formato
   `SHA256:...` y no se conecta hasta que se acepta.
2. Aceptar y recordar. Reconectar. **Esperado**: no vuelve a preguntar.
3. Cambiar a mano el `known_host_fingerprint` de esa conexión en la base, simulando un
   cambio de identidad del host. Reconectar.

**Esperado**: la conexión se **bloquea** con una advertencia explícita. Verificar en el log
que no se registró ningún intento de autenticación: la credencial no debe haberse enviado.

### Escenario 5 — Errores con mensaje útil (SC-009)

Provocar cada fallo y leer el mensaje:

| Cómo provocarlo | Esperado |
| --- | --- |
| Host inexistente (`192.0.2.1`) | Nombra que el host no está accesible; ofrece reintentar |
| Contraseña incorrecta | Nombra el rechazo de credenciales; ofrece corregirla |
| Puerto cerrado con firewall | Nombra el vencimiento del tiempo de espera |
| Borrar el archivo de clave privada | Nombra el archivo faltante y su ruta |
| Passphrase incorrecta | Distingue el caso de la clave faltante |
| Borrar la credencial del Administrador de credenciales | Ofrece ingresarla de nuevo |

**Esperado en todos**: ningún mensaje es un código de error crudo ni un volcado de excepción.

### Escenario 6 — Sesiones simultáneas y aislamiento (SC-003, SC-012)

1. Abrir 8 sesiones a la vez, mezclando RDP y SSH.
2. Alternar entre pestañas.
3. Desconectar la red del servidor de una sesión SSH.

**Esperado**: el cambio entre pestañas se percibe inmediato. La sesión afectada pasa a
"Desconectado" ofreciendo reconectar, y **las otras siete siguen funcionando** sin
interrupción. La aplicación no se cierra.

### Escenario 7 — Persistencia y estado de la ventana (SC-010)

1. Crear carpetas anidadas con varias conexiones, reordenarlas y agregar notas.
2. Mover y maximizar la ventana; cambiar el tema.
3. Cerrar la aplicación (con sesiones abiertas, para verificar la confirmación de FR-048).
4. Volver a abrirla.

**Esperado**: carpetas, conexiones, orden y notas intactos. La ventana reaparece con el mismo
tamaño, posición y tema. Las sesiones **no** se restauran, que es el comportamiento previsto.

### Escenario 8 — Base de datos corrupta (FR-052)

Con la aplicación cerrada, sobrescribir `cmc.db` con texto cualquiera. Abrir la aplicación.

**Esperado**: informa el problema, indica dónde quedó preservado el archivo original
(`cmc.db.corrupta-<sello>`) y arranca con una base nueva. **No** se cierra ni borra el
archivo dañado.

### Escenario 9 — Auditoría de secretos (SC-006) — bloqueante

Después de recorrer todos los escenarios anteriores:

```powershell
# Ninguno de estos comandos debe devolver una sola línea.
$db = "$env:LocalAppData\CafManagerConection\cmc.db"
$logs = "$env:LocalAppData\CafManagerConection\logs"

# Reemplazar por las contraseñas y passphrases reales usadas en la prueba
Select-String -Path $db -Pattern 'CONTRASEÑA-DE-PRUEBA' -SimpleMatch
Select-String -Path "$logs\*" -Pattern 'CONTRASEÑA-DE-PRUEBA' -SimpleMatch
Select-String -Path "$logs\*" -Pattern 'PASSPHRASE-DE-PRUEBA' -SimpleMatch
Select-String -Path "$logs\*" -Pattern 'BEGIN OPENSSH PRIVATE KEY' -SimpleMatch
```

Revisar además a ojo un archivo de log completo: no debe contener comandos tecleados en SSH,
salida del terminal ni contenido del portapapeles.

**Cero coincidencias es el único resultado aceptable.** Cualquier hallazgo es una violación
del Principio II y detiene la entrega.

### Escenario 10 — Paquete portable (SC-011, SC-004, SC-005)

```powershell
dotnet publish src/CafManagerConection.App -c Release -r win-x64 --self-contained true `
  -o publish/CafManagerConection

Compress-Archive -Path publish/CafManagerConection/* -DestinationPath CafManagerConection.zip
```

Copiar el ZIP a un Windows 11 **sin .NET instalado**, descomprimir y ejecutar como usuario
sin privilegios de administrador.

**Esperado**: arranca hasta una ventana utilizable en menos de 2 segundos. En reposo, sin
sesiones abiertas, el Administrador de tareas muestra menos de 150 MB de memoria. No pide
elevación y no instala ningún servicio.

---

### Escenario 11 — Herencia desde la carpeta (SC-013)

1. Crear una carpeta y definir en ella usuario, puerto y credencial.
2. Crear dentro 20 conexiones SSH indicando sólo nombre y host.
3. Conectarse a cualquiera de ellas.
4. Cambiar la credencial de la carpeta y volver a conectarse.
5. Mover una de las conexiones a otra carpeta con credencial distinta.

**Esperado**: las 20 conectan sin haber cargado usuario, puerto ni contraseña. Tras el cambio,
las 20 usan la credencial nueva sin editarlas. Al mover, la confirmación **advierte** que la
credencial efectiva va a cambiar. En el editor de una conexión, cada campo heredado muestra su
valor y de qué carpeta viene.

### Escenario 12 — Archivos remotos (SC-016)

Con una sesión SSH abierta, desplegar el panel de archivos.

1. Navegar el árbol remoto.
2. Enviar un archivo de 100 MB y traerlo de vuelta con otro nombre.
3. Comparar las sumas de verificación del original y del recuperado.
4. Cancelar una transferencia a mitad de camino.
5. Cerrar el panel.

**Esperado**: las sumas coinciden. La cancelación deja el archivo incompleto identificado como
tal, no presentado como completo. Al cerrar el panel, la sesión de terminal sigue funcionando
sin interrupción.

### Escenario 13 — Estado del servidor (SC-017, SC-018)

Con una sesión SSH a un servidor Linux, desplegar el panel de estado y, en paralelo, ejecutar
en el terminal `top -bn1 | head -3`, `free -m`, `df -h` y `uptime`.

**Esperado**: los valores del panel coinciden con los de los comandos, con la diferencia
atribuible al intervalo de muestreo. Los discos no listan `tmpfs` ni `overlay`. La red no
lista `lo`. Al cerrar el panel, `who` en el servidor muestra una sesión menos: la auxiliar se
cerró. Con `top` en el servidor, el proceso de las lecturas no supera el 1 % de CPU.

Contra un host que no sea Linux, el panel de estado **no se ofrece**.

### Escenario 14 — Túneles (SC-019)

1. Definir un túnel hacia un servicio que sólo escuche en `localhost` del servidor.
2. Levantarlo y abrirlo desde el equipo local.
3. Definir otro túnel con el mismo puerto local y levantarlo.
4. Detener el primero.

**Esperado**: el servicio responde en el puerto local. El segundo túnel informa el conflicto
**nombrando el puerto** y queda detenido. Al detener el primero, el puerto local queda libre
(verificable con `netstat -an`).

### Escenario 15 — Inventario de plataforma (SC-020, SC-021)

Con una sesión SSH a un servidor con Docker, nginx y supervisord, desplegar cada panel y
comparar contra `docker ps -a`, la configuración habilitada de nginx y `supervisorctl status`
ejecutados a mano.

**Esperado**: las listas coinciden. Los compose detectados coinciden con los del disco y sus
servicios aparecen relacionados con sus contenedores. Con un usuario fuera del grupo `docker`,
se reintenta con `sudo` y, si tampoco alcanza, se informa la falta de permisos con claridad.

Contra un servidor sin Docker, sin nginx o sin supervisord, esos paneles **no se ofrecen**.

### Escenario 16 — El terminal se comporta como PuTTY (SC-022)

Se corre con **PuTTY abierto al lado**, contra el mismo servidor y con el mismo tamaño de
ventana. Cada gesto se hace primero en uno y después en el otro, y se anota el resultado. Es un
guion de comparación: la referencia no es lo que dice este documento, es lo que hace PuTTY.

Antes de empezar, en las dos ventanas: `cat /etc/os-release` y algo de salida larga —
`ls -la /etc`— para tener texto e historial con qué trabajar.

| # | Gesto | Esperado en las dos ventanas |
|---|-------|------------------------------|
| 1 | Arrastrar sobre una palabra y soltar | El texto queda en el portapapeles **sin tocar nada más**. Se comprueba pegando en el Bloc de notas |
| 2 | Un clic suelto, sin arrastrar | No cambia el portapapeles: lo pegado en el Bloc de notas sigue siendo lo del paso 1 |
| 3 | Doble clic sobre `/etc/nginx/nginx.conf` | Se toma la ruta entera. *Desviación esperada*: PuTTY la parte en pedazos; CMC no, y es deliberado (FR-154e) |
| 4 | Triple clic sobre una línea larga | Se toma la línea hasta su último carácter escrito, sin la cola de espacios |
| 5 | Shift+clic más allá de una selección | Se extiende la que había; no empieza otra |
| 6 | Ctrl+arrastre sobre una columna de `ls -la` | Se toma sólo esa columna, fila por fila |
| 7 | Botón medio después de seleccionar algo | Extiende la selección hasta donde se hizo clic |
| 8 | Clic derecho | Pega el portapapeles en la línea de comandos |
| 9 | Ctrl+clic derecho | Abre el menú del terminal, con los atajos escritos al lado de cada acción |
| 10 | `ping 8.8.8.8`, seleccionar texto de la salida y apretar **Ctrl+C** | El ping se **interrumpe**. Que haya una selección viva no cambia nada |
| 11 | Ctrl+Ins y Shift+Ins | Copian y pegan. También Ctrl+Shift+C y Ctrl+Shift+V |
| 12 | Shift+RePag, Ctrl+RePag, Ctrl+Shift+RePag y sus contrarios | Una página, una línea y los extremos del historial |

**Pegado multilínea**, que es la única desviación declarada del estándar (FR-030f):

1. Copiar tres líneas de texto cualquiera.
2. Pegar en el prompt del shell —que enciende el modo 2004—: **no** pregunta nada, el shell
   muestra las tres líneas juntas y espera el Enter.
3. Correr `cat > /tmp/prueba.txt` (no enciende el modo 2004) y pegar lo mismo: CMC **pregunta**
   nombrando las tres líneas. PuTTY no pregunta.
4. Cancelar: no se manda nada. Repetir y aceptar: llegan las tres líneas.
5. `Ctrl+D` para cerrar el `cat`, y `rm /tmp/prueba.txt`.

**Esperado**: los doce gestos dan el mismo resultado observable en las dos ventanas, salvo el
paso 3 y la pregunta del pegado multilínea, que son las dos desviaciones deliberadas y están
escritas en la especificación con su motivo.

### Escenario 17 — Puertos y ficha de proceso (SC-023)

Con una sesión SSH a un servidor Linux, abrir el panel de puertos y comparar con `ss -tulpn`
ejecutado a mano en el terminal de la misma sesión.

Después, doble clic en una fila con proceso visible y comparar la ficha contra:

```bash
ps -p <pid> -o comm=,user=,etime=,ppid=,nlwp=,args=
readlink -f /proc/<pid>/exe
readlink -f /proc/<pid>/cwd
```

**Esperado**: la lista coincide; la ficha muestra binario, usuario, tiempo corriendo, padre, hilos
y línea de comando iguales a los del sistema. La ficha **no ofrece ninguna acción** que modifique
el servidor.

Repetir con un usuario **sin** `sudo` sobre un proceso de otro usuario: la ficha aparece igual, con
usuario y tiempo, y arriba dice qué no pudo leer y por qué. Y con un PID que ya no existe —matar un
proceso propio entre la lista y el doble clic—: se informa que no existe y se ofrece refrescar.

Sobre una fila que diga «(sin permiso para verlo)», el doble clic **no abre nada** y lo explica.

### Escenario 18 — Conexión sin credencial guardada (SC-024)

Se corre dos veces, contra dos servidores distintos:

1. Un servidor con el pedido interactivo por teclado habilitado (el valor de fábrica de Debian y
   Ubuntu).
2. Un servidor con ese método **deshabilitado**, que sólo acepta contraseña directa.

En los dos casos: crear la conexión SSH sin guardar la contraseña y abrirla.

**Esperado**: en las dos, la contraseña se pide en la consola del terminal, sin eco, y la sesión
queda conectada. **Sin ningún mensaje de error previo.** En el segundo caso la huella del servidor
se pregunta **una sola vez**, no dos.

Con la contraseña equivocada tres veces seguidas, el intento corta e informa.

### Escenario 19 — Color legible y texto exacto (SC-025, SC-026)

**Estado**: contra un servidor con un disco por encima del 90 %, comprobar que la barra está en
rojo y que al lado del título dice «crítico». Poner la pantalla en escala de grises —Configuración
de Windows, Filtros de color— y comprobar que los tres niveles siguen distinguiéndose.

**Registro**: abrir el registro de un proceso de supervisord que escriba líneas con `ERROR` y con
`WARN`. Comprobar que aparecen en rojo y ámbar, y que un registro con colores ANSI propios
—`docker logs` de un contenedor que colorea— se muestra con **sus** colores y no con códigos
sueltos entre las palabras.

**Configuración**: abrir la configuración efectiva de un sitio de nginx de al menos 200 líneas.
Comprobar que las directivas, bloques, cadenas, números, variables y comentarios se distinguen, que
**no hay ninguna línea cortada** y que «Copiar todo» pegado en un archivo es idéntico al que
devuelve `nginx -T` en el servidor. Se compara con una suma de verificación, no a ojo.

### Escenario 20 — Abrir un puerto en el navegador (SC-027)

Con la sesión conectada, abrir el panel de puertos y probar el botón derecho sobre tres filas
distintas:

**Un puerto TCP que escucha en `0.0.0.0`**: el menú ofrece `https://host:puerto` **primero** y
`http://host:puerto` debajo, con el host de la conexión —no la dirección de escucha—. Elegir uno
abre el navegador del sistema. Se comprueba con un puerto que **no** sea 443 ni 80: el orden no
tiene que depender del número.

**Un puerto TCP que escucha en `127.0.0.1`**: no ofrece ningún esquema, y dice que escucha sólo en
el servidor. Un enlace que no puede funcionar es peor que su ausencia.

**Un puerto UDP**: no ofrece navegador ni túnel.

### Escenario 21 — Túnel desde el panel de puertos (SC-028)

En el servidor, dejar un servicio escuchando sólo en el bucle local —por ejemplo
`python3 -m http.server 8899 --bind 127.0.0.1`—. Refrescar el panel de puertos y elegir «Crear un
túnel a este puerto…» sobre esa fila.

Comprobar que el editor se abre **con los valores cargados**: nombre propuesto a partir del
proceso, puerto local libre —el mismo 8899 si este equipo lo tiene libre—, destino
`localhost:8899` y la casilla de arranque automático marcada. Guardar.

Comprobar entonces, en este orden:

1. El túnel figura activo en el panel de túneles, sin haberlo levantado a mano.
2. `http://localhost:<puerto local>` en el navegador llega al servicio del servidor.
3. Cerrar la pestaña de la sesión y volver a conectarla: el túnel se levanta solo y el navegador
   sigue llegando, **sin definir nada de nuevo**.

Repetir la creación sobre un segundo puerto local-only del mismo servidor y comprobar que el
puerto local propuesto es distinto del primero, incluso antes de haber levantado ninguno.

### Escenario 22 — El estado del servidor dice por qué no puede leer (SC-029)

Contra un servidor Linux con el panel de estado abierto y andando, provocar los dos fallos y
comprobar que el panel nombra la causa **sin** abrir la consola de traza:

**Canal caído**: cortar la red del equipo unos segundos. El panel tiene que mostrar el mensaje del
canal, no «no se pudo leer el estado del servidor».

**Servidor que no es Linux**: conectar a un servidor sin `/proc` legible. El panel tiene que hablar
de `/proc`, que es un problema del servidor y no de la conexión.

Comprobar además que al volver la lectura el mensaje ámbar desaparece, y que contra un servidor con
latencia alta —o con un enlace saturado— la lectura entra en el tiempo límite: eran 3 segundos para
un comando de trece partes y no alcanzaban.

### Escenario 23 — Ningún panel falla en silencio (SC-030)

Provocar un fallo de armado a propósito y comprobar que se ve el motivo, no una remisión a otro
lugar. La forma más directa: aplicar en un XAML de panel un estilo cuyo `TargetType` no
corresponda —es el defecto real que ocurrió— y abrir ese panel.

Comprobar que en el lugar del panel aparece **el texto de la causa** («el TargetType "Button" no
coincide con el tipo de elemento "ToggleButton"») y no «se produjo una excepción al establecer la
propiedad», que es lo que dice la excepción de afuera.

Comprobar además que el botón «Registros» de la consola de traza (F12) abre la carpeta donde está
el detalle completo, y que Preferencias → General muestra esa misma ruta.

Después deshacer el cambio y comprobar que la prueba `EstilosAplicadosTests` lo hubiera atajado
antes de llegar a la pantalla.

### Escenario 24 — Puertos de Docker y del túnel (SC-031, SC-032)

En un servidor con contenedores que publiquen puertos, abrir el panel de puertos y comparar cada
fila cuyo proceso sea `docker-proxy` con la salida de:

```
docker ps --format '{{.Names}} {{.Ports}}'
```

Cada una tiene que nombrar el contenedor correcto, incluidos los de un rango publicado
(`8000-8003->8000-8003/tcp` son cuatro filas y un solo contenedor).

Después, sobre un puerto con túnel activo: la columna «Túnel» muestra el puerto local, y el botón
derecho ofrece abrir `localhost:<puerto local>` en lugar del host del servidor. Bajar el túnel desde
el panel de túneles y comprobar que la fila pasa a decir «(parado)» y que el menú vuelve a explicar
que no hay ruta.

### Escenario 25 — Discos y tipo de sistema de archivos (SC-033)

Con el panel de estado abierto contra cada uno de los tres servidores de referencia, ejecutar en
el servidor:

```
df -PT
```

Comparar dispositivo, tipo de sistema de archivos y porcentaje de ocupación de cada disco que
muestra el panel contra la salida del comando, excluyendo particiones y dispositivos `loop`.
Tienen que coincidir dispositivo por dispositivo.

### Escenario 26 — Interfaces de red (SC-034)

En cada uno de los tres servidores de referencia —incluido el que está detrás de VPN—, ejecutar:

```
ip addr
```

Comparar nombre, MAC, MTU, estado del enlace y direcciones IPv4 e IPv6 de cada interfaz que
muestra el panel. La interfaz `tun`/`tap` del servidor con VPN tiene que aparecer y coincidir; las
interfaces `veth`, `br-` y `docker*` del servidor con Docker Swarm no tienen que aparecer salvo que
se pidan por nombre.

### Escenario 27 — Puerta de enlace y rutas (SC-035)

En cada servidor, ejecutar:

```
ip route
ip -6 route
```

Comparar la puerta de enlace predeterminada, la interfaz y la métrica que muestra el panel contra
la salida de los dos comandos. En el servidor con rutas IPv6 que empiecen con `unreachable` o
`blackhole`, comprobar que el panel no las informa con ese texto en el lugar del destino.

### Escenario 28 — Top de procesos (SC-036)

En cada servidor, ejecutar `top`, ordenar una vez por CPU (tecla `P`) y otra por memoria (tecla
`M`), y comparar los diez primeros de cada orden contra las dos listas del panel: PID, usuario,
%CPU, memoria residente, hilos, estado y tiempo corriendo. En el servidor de más núcleos,
comprobar que un proceso con muchos hilos aparece con un %CPU mayor a 100 sin recortar. El doble
clic sobre una fila abre la ficha de proceso ya existente (FR-165).

### Escenario 29 — Intervalo de muestreo persistente (SC-037)

Cambiar el intervalo de muestreo del panel de estado a un valor distinto del que trae por
omisión, cerrar la conexión y volver a abrirla contra el mismo servidor. El panel tiene que abrir
con el intervalo elegido, no con el de fábrica.

## Criterio de aceptación de la feature

La feature está terminada cuando:

- [ ] `dotnet test` pasa por completo, incluidas las pruebas de integración SSH.
- [ ] La prueba de ciclo de vida RDP no muestra crecimiento sostenido de handles.
- [ ] Los veintinueve escenarios manuales dan el resultado esperado.
- [ ] El escenario 9 devuelve cero coincidencias.
- [ ] El paquete portable corre en un equipo limpio.
