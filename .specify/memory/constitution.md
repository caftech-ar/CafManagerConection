# Constitución de CafManagerConection (CMC)

CMC es un administrador de servidores RDP y SSH para Windows. Este documento fija lo que no se
negocia. Ante un conflicto entre este documento y una decisión puntual de diseño, gana este
documento hasta que se enmiende.

## Core Principles

### I. Dominio aislado de la infraestructura

`Domain` y `UseCases` NO referencian WPF, WinForms, SSH.NET, SQLite, el cliente RDP ni ninguna
API de Windows. Toda dependencia externa entra por una interfaz declarada en `UseCases` e
implementada en `Infrastructure`, `Ssh`, `Rdp`, `Terminal`, `Monitoring` o `Platform`.

El cómputo puro vive en `Domain`, aunque use el BCL: cifrar, derivar una clave, parsear `/proc` o
validar una contraseña no hacen E/S y se prueban con entrada fija y salida fija.

Motivo: el núcleo debe poder ejercitarse en pruebas sin abrir ventanas, sockets ni archivos, que
es lo único que hace viable el Principio III.

### II. Cero secretos en claro (NO NEGOCIABLE)

Las contraseñas, las passphrases y el contenido de las claves privadas viven **cifrados en la base
local**. Nunca en claro, en ningún archivo.

**Dos claves y no una.** Esta separación no es un detalle de implementación: es lo que permite a
la vez que el vault se abra en otra máquina y que cambiar la clave maestra no exija recifrar cada
credencial.

- La **clave del vault** son **32 bytes de `RandomNumberGenerator`**, generados una vez. Es la
  única que cifra credenciales. Nunca se deriva de nada ni se muestra.
- Esa clave se guarda **envuelta**, y hay dos envolturas posibles. **Siempre tiene que existir al
  menos una**: un vault sin ninguna no se abre nunca más, y eso es un defecto, no un estado.

**Envoltura por clave maestra, y es OPCIONAL.** El usuario MAY definir una clave maestra; de ella
se deriva la clave que envuelve a la del vault. Es lo único que permite abrir el vault en otra
máquina o bajo otro usuario de Windows.

**Envoltura por equipo.** La clave del vault MAY guardarse protegida con DPAPI en el ámbito del
usuario actual, para abrir sin preguntar nada. Sin clave maestra ésta es obligatoria, porque si no
el vault no tendría cómo abrirse.

**Cómo se cifra.** No hay margen de elección acá:

- Cada secreto se cifra con **AES-256-GCM** (`AesGcm`, del BCL), con un **nonce de 12 bytes al
  azar por cada cifrado**, que MUST NOT reutilizarse jamás con la misma clave. La etiqueta de
  autenticación se verifica siempre: un texto cifrado que no autentica es un error que se informa,
  nunca un secreto vacío que se acepta.
- La clave que envuelve se deriva con **PBKDF2-HMAC-SHA512** (`Rfc2898DeriveBytes.Pbkdf2`, del
  BCL) y **al menos 600.000 iteraciones**. Bajar ese piso exige enmienda; subirlo, no.
- La clave maestra MUST pasarse a la derivación por la vía que escribe en un búfer propio. La
  sobrecarga que toma `string` está PROHIBIDA: una clave maestra en un `string` queda en el montón
  y no se puede pisar con ceros.
- La sal es de **16 bytes o más**, al azar, una por vault, y se guarda en claro: una sal no es un
  secreto.
- Los parámetros de la derivación se guardan **en la base y en claro**, para que encarecerla más
  adelante no vuelva ilegible lo ya cifrado.
- Que la clave maestra sea la correcta se comprueba **descifrando**. Está PROHIBIDO guardar un
  hash de la clave maestra con ningún propósito: es material atacable offline sin tocar el resto.

**La clave maestra, cuando el usuario define una.** Seis reglas, ninguna negociable:

1. Se pide para desbloquear el vault, y el material derivado vive en memoria mientras el vault
   esté desbloqueado. El desbloqueo termina al salir de la aplicación y al bloquear a mano.
2. **La clave maestra tipeada NO se persiste en ningún lado**: ni en la base, ni en la
   configuración, ni en un archivo temporal, ni bajo DPAPI. Lo único que MAY guardarse para
   recordar el equipo es la clave del vault envuelta por DPAPI. La clave maestra, jamás.
3. NO aparece en ningún registro, mensaje de error ni volcado de excepción, por crítico que sea el
   fallo.
4. Los búferes se pisan con ceros al bloquear y al cerrar, incluido el camino de excepción. Al
   bloquear a mano MUST irse de memoria también la clave del vault y **toda credencial ya
   descifrada**: si algo quedó en una estructura de larga vida, bloquear no bloquea nada.
5. Si el usuario la cancela o la yerra, la aplicación MUST abrir igual y MUST decir qué queda sin
   funcionar. Lo que NO puede hacer es fallar sin explicación, caer a guardar en claro, ni ofrecer
   «seguir sin cifrado».
6. **Perderla es irrecuperable y no hay puerta de atrás.** La aplicación MUST advertirlo con esas
   palabras al momento de establecerla, y MUST exigir que se escriba dos veces.

**Forma de la clave maestra**: 8 caracteres como mínimo, con al menos una letra, un dígito y un
carácter especial. Sin máximo útil —el campo admite 128 o más— y **sin truncar jamás**: una clave
recortada abre el vault hoy y no lo abre cuando el recorte cambie. No se rechaza ningún carácter
Unicode ni el espacio, y los requisitos se muestran **antes** del rechazo.

Y el dato que hay que tener a la vista, porque el piso es un piso: **una clave maestra de 8
caracteres elegida por una persona es el camino de ataque más barato contra todo el vault**. El
KDF encarece cada intento, no reduce cuántos hacen falta. La aplicación MUST mostrar la fuerza de
lo tipeado y sugerir una frase larga, aunque acepte el mínimo.

**Recordar el equipo con DPAPI**, cuando hay clave maestra, está acotado por cinco reglas:

1. Se envuelve la clave del vault y **nunca** la clave maestra tipeada.
2. Se elige explícitamente, sin opción preseleccionada, y la pantalla MUST decir qué cambia:
   cualquier proceso que corra como ese usuario de Windows puede desenvolverla y llegar a todas
   las credenciales sin la clave maestra. Si el usuario cierra sin elegir, **queda apagada**.
3. Lo guardado MUST NOT entrar en ninguna copia de seguridad ni exportación. Si entrara, una copia
   restaurada en la misma máquina se abriría sin la clave maestra.
4. **Bloquear a mano desarma el desbloqueo automático** hasta que se vuelva a tipear la clave
   maestra. Sin esta regla, bloquear es puro teatro.
5. Que DPAPI falle —otro usuario, otra máquina, dato corrupto— es el **camino normal**: se pide la
   clave maestra y NO se presenta como un error.

Está PROHIBIDO:

- Persistir contraseñas, passphrases o material de clave privada **en claro** en la base, en la
  configuración o en archivos temporales.
- Registrar en los logs contraseñas, passphrases, claves privadas, la clave maestra, el texto
  tecleado en una sesión SSH, el contenido de pantalla de una sesión RDP o el portapapeles.
- Registrar la salida de los comandos remotos de monitoreo, las rutas y los nombres de archivo del
  explorador SFTP, o el contenido de los archivos transferidos. Todo eso es contenido de sesión y
  está sujeto a la misma prohibición.
- Exponer un secreto en un mensaje de error, en un volcado de excepción o en la interfaz.
- **Pasar una contraseña por la línea de comandos a cualquier proceso**: en Windows la lee
  cualquier proceso del mismo usuario. `-pw` y `-pwfile` de PuTTY quedan prohibidos, el segundo
  además porque escribe el secreto en un archivo.

Los secretos se mantienen en memoria el menor tiempo posible y no se copian a estructuras de larga
vida. Cualquier revisión de código o de plan verifica este principio antes que cualquier otro.

**Qué amenaza cubre y cuál no.** El archivo de la base contiene todos los secretos, y lo único que
los separa de quien lo tenga es la clave maestra y el KDF. Con «recordar el equipo» encendido, o
sin clave maestra, alcanza además con correr como ese usuario de Windows. Una copia de seguridad
es por lo tanto un archivo con todas las credenciales adentro: se permite porque el texto cifrado
sin su clave no sirve, y por eso la copia MUST llevar el texto cifrado y la envoltura por clave
maestra, y MUST NOT llevar nunca la clave maestra, el material derivado ni el dato de DPAPI.
**Restaurar en otra máquina MUST funcionar con sólo la clave maestra**, y la prueba que lo
verifica MUST restaurar sobre un perfil de Windows distinto del que hizo la copia.

**Excepción única: la contraseña de `sudo`.** Cuando `sudo` pida contraseña y la de la conexión no
sirva —el caso de una conexión por clave SSH—, el sistema MAY pedirle una al usuario y conservarla
en memoria **mientras esa sesión esté abierta**. Acotada por cinco reglas:

1. NO se persiste en ningún lado, ni siquiera en el vault.
2. NO se pasa por la línea de comandos: va por la entrada estándar, como el `sudo -S -k` que ya
   existe.
3. NO aparece en ningún registro, mensaje de error ni volcado.
4. Se borra del búfer con ceros al cerrar la sesión.
5. Vive por sesión y no por conexión: abrir la misma conexión de nuevo la vuelve a pedir.

Motivo: CMC administra servidores de producción; una filtración de credenciales es el único fallo
del que la aplicación no se puede recuperar.

### III. Test-first en el núcleo

`Domain` y `UseCases` SE DESARROLLAN con pruebas primero: la prueba se escribe, falla, y recién
entonces se implementa. Las herramientas son xUnit, NSubstitute y Coverlet.

Estrategia por capa:

- `Infrastructure`: pruebas de integración contra una base SQLite temporal, creada y destruida por
  la prueba. **Nunca contra una base real del usuario.**
- `Ssh`: pruebas contra un contenedor OpenSSH dedicado a pruebas. Sin él se omiten con el motivo y
  las instrucciones; no fallan.
- `Terminal`: pruebas contra fixtures de secuencias ANSI/VT almacenadas.
- `App`: lo que se pueda probar sin abrir una ventana. Lo que no, se verifica a mano y queda
  anotado como tal.

Un defecto que llegó a producción DEBE dejar su guardián: la prueba que lo habría atajado, no un
comentario.

### IV. WPF y bibliotecas open source

La interfaz se construye con **WPF**, con estilos y plantillas declarados en XAML. El aspecto se
define con recursos y disparadores, no con código de dibujado.

WinForms sigue habilitado por **una sola razón**: `WindowsFormsHost`, que aloja las dos piezas sin
equivalente en WPF —el control de terminal, que dibuja una rejilla de celdas con atributos ANSI, y
el host del cliente RDP, que aloja un control ActiveX—. Fuera de esas dos, WinForms no aparece en
la interfaz.

La apariencia sigue el lenguaje visual de **shadcn/ui** sobre la escala `zinc`. El primario es casi
negro y el color queda reservado para los estados de sesión.

Está PROHIBIDO incorporar: DevExpress, Telerik, Krypton, WebView2, HTML, CSS, JavaScript, WinUI 3,
Tauri y Electron. **También cualquier biblioteca de estilos de terceros**: los estilos son propios
y viven en `Themes/Estilos.xaml`. **Y cualquier editor de código o resaltador de sintaxis de
terceros** —AvalonEdit, Scintilla, ICSharpCode, gramáticas TextMate o resaltadores basados en
web—: los registros se pintan con el emulador VT propio y la configuración de nginx con un
tokenizador propio. Las entradas web abren el navegador **del sistema operativo** como proceso
externo; no incorporan ningún motor web. Las herramientas externas se abren igual: como proceso,
en su propia ventana. **Está prohibido alojar la ventana de un proceso ajeno** —`SetParent` o
equivalente—: las dos piezas alojadas enumeradas arriba son las únicas, y una tercera exige una
enmienda que la nombre.

Cuando un control no alcance, la respuesta es **una plantilla o un estilo**, nunca código de
dibujado. Escribir un control que se pinta a sí mismo sólo se admite si no existe ninguno capaz de
cumplir la función: el terminal y el host RDP son los dos únicos casos.

El límite conocido del alojamiento es que un `WindowsFormsHost` no se mezcla con WPF: no admite
transparencia ni superposición. Para un terminal y un RDP que ocupan un panel rectangular eso no
molesta, y es el precio de hospedar el ActiveX de RDP de forma nativa.

**Toda dependencia nueva DEBE ser open source con licencia permisiva (MIT o Apache 2.0) y
justificarse en el plan que la introduce.** Antes de agregar una, se comprueba si el BCL ya
resuelve el problema: la criptografía del Principio II se hace con `AesGcm` y `Rfc2898DeriveBytes`,
y DPAPI por P/Invoke, sin un solo paquete. El cliente RDP de Windows y las API de Windows se usan
como componentes del sistema operativo.

Motivo: la aplicación debe ser liviana, distribuible sin costos de licencia y sin arrastrar un
motor web dentro de un administrador de servidores.

### V. Simplicidad y alcance cerrado (YAGNI)

Se implementa lo especificado y nada más. Ampliar el alcance EXIGE una enmienda previa. Una
funcionalidad fuera de alcance no se agrega «porque es fácil».

Quedan explícitamente FUERA DE ALCANCE:

- Protocolos: SCP, VNC, Telnet.
- Shells locales: PowerShell, CMD.
- Integraciones: navegador embebido, agentes de IA, automatizaciones, tareas programadas o
  recurrentes.
- Colaboración e identidad: sincronización en nube, equipos compartidos, multiusuario, Active
  Directory.
- Plataformas: aplicación móvil.
- Redirecciones RDP: audio, micrófono, discos locales, impresoras, puertos, cámaras, tarjetas
  inteligentes, RemoteApp y RDP Gateway.

**Dentro de alcance, con sus límites:**

- **Árbol de conexiones y carpetas** con herencia de usuario, dominio, puerto, credencial y
  ajustes de protocolo; orden libre, búsqueda, favoritas, etiquetas, y color e icono elegibles por
  elemento dentro de un juego cerrado.
- **Sesiones RDP y SSH** en pestañas. Una sesión RDP MAY maximizarse dentro de la aplicación o
  salir a una **ventana propia**; cerrarla MUST devolver la sesión a su pestaña, no cortarla. Una
  conexión RDP MAY abrirse con las credenciales de la sesión de Windows del usuario, sin pedir ni
  guardar ninguna —lo que **refuerza** el Principio II—; cuando el equipo no esté en el dominio o
  el servidor no confíe, MUST caerse a pedir credenciales, no fallar. Nada de leer el directorio,
  resolver grupos ni descubrir equipos.
- **Emulador VT propio** con secuencias ANSI, 256 colores, el juego gráfico de DEC y teclado en
  modo aplicación.
- **SFTP**: explorador remoto con navegación, transferencia en ambos sentidos **incluidos
  directorios enteros**, crear carpeta, renombrar y eliminar. NO incluye cambio de permisos o
  dueño, enlaces simbólicos ni edición remota.
- **Túneles SSH**: reenvío de puertos locales, por conexión y a pedido. NO incluye reenvío remoto
  inverso ni proxy SOCKS dinámico.
- **Panel de métricas Linux** por `/proc`, `df` y `uname`, sin instalar nada en el servidor.
- **Panel de procesos**, ordenable por CPU y memoria, con los hijos y la E/S de disco. El uso de
  CPU MUST ser el **instantáneo**, por diferencia entre dos muestras: el promedio de vida que
  informa `ps` ordena por el proceso más viejo, no por el que está comiendo la CPU ahora.
- **Puertos a la escucha** y la **ficha del proceso** que ocupa uno. NO incluye conexiones
  establecidas.
- **Gestión de plataforma sobre SSH**: inventario de Docker, nginx y supervisord. Las acciones de
  escritura —levantar, detener, recrear, recargar— existen sólo sobre **contenedores y procesos de
  supervisord**, que son servicios administrados, y cada una exige confirmación explícita. NO
  incluye edición de configuración remota ni generar `docker-compose`.
- **Seguimiento de registros en vivo**: cada visor MUST decir **qué archivos monitorea** y cuándo
  cambiaron, MUST ofrecer forzar una lectura, y MUST avisar cuando aparece una línea de error,
  cuando el archivo deja de poder leerse y cuando se corta el canal. Un registro congelado y un
  servidor tranquilo se ven igual, y esa confusión es el defecto que esto existe para evitar.
- **Escalada con `sudo`**: se MAY sondear al conectar si el usuario remoto puede escalar, y los
  paneles que muestran menos por falta de permiso MUST poder ofrecer reintentar. Cuando no se
  pueda, MUST decirse en lugar de mostrar un botón que no va a funcionar.
- **Entradas web**: guardan una URL y su credencial, abren el navegador del sistema y ofrecen
  copiar usuario y contraseña. NO incluye navegador embebido ni autocompletado.
- **Herramientas externas**: abrir una conexión SSH en PuTTY, FileZilla o WinSCP como proceso
  externo. Se les MAY pasar la **ruta de la clave privada**, que no es un secreto; la contraseña
  la piden ellas. NO incluye alojar su ventana ni **escribir en sus sesiones guardadas**.
- **Importación de sesiones** de PuTTY, WinSCP y FileZilla, **una sola vez y a pedido**. Es una
  migración y no una integración: después, CMC no vuelve a mirar esos archivos. Sólo se convierten
  las sesiones **SFTP y SCP**; el resto MUST informarse como omitido **con su motivo**. Las
  contraseñas MAY traerse preguntando **una vez por importación**, y van cifradas al vault. Con el
  vault cerrado, la importación MUST traer las conexiones y decir que las contraseñas quedaron
  afuera. **Un cuarto origen exige enmienda.**
- **Paleta de comandos guardados**, que se envían a **una** sesión abierta. NO incluye ejecución
  automática, disparadores ni programación horaria.
- **Conexión rápida** a `usuario@host:puerto` sin crear una entrada.
- **Copias de seguridad**: copia local al abrir, como mucho una por día y sólo si cambió,
  conservando las últimas N, más exportar a un archivo elegido. Se escriben con la API de respaldo
  de SQLite, no copiando el archivo. NO incluye servicio, tarea programada ni integración con
  ningún proveedor de nube.
- **Aviso de versión nueva**: consulta anónima de las releases públicas al abrir, en segundo plano
  y como mucho una vez por día.

**El panel de métricas y el de procesos NO son un sistema de monitoreo.** No persisten historial,
no recolectan con la aplicación cerrada y no tienen alertas con umbral configurable. Un aviso
puntual mientras la pantalla está abierta no es una alerta; una regla que el usuario configura y
que dispara sin que nadie mire, sí, y ésa exige enmienda.

**Los paneles de sólo lectura son de sólo lectura.** Matar, reniceear o mandar cualquier señal a un
proceso del sistema está prohibido: un PID suelto no es un servicio administrado.

**Colorear y ordenar lo que ya se muestra NO es ampliar el alcance.** Presentar con color,
umbrales, barras, arcos o minigráficos datos que la aplicación ya recolecta es cómo se dibuja lo
que ya está dentro de alcance; tampoco es alcance nuevo agregar campos a una ficha cuyo inventario
ya se consulta, ni mostrar un dato local que la aplicación ya tiene. Exigir una enmienda para
pintar de rojo un disco al 95 % convertiría este principio en un peaje, y un peaje se esquiva. Lo
que sí exige enmienda es **traer un dato nuevo del servidor, ejecutar algo nuevo allá, o
incorporar una dependencia**.

Motivo: el valor de CMC es ser liviano y predecible; cada función agregada compite con esa promesa.

### VI. Distribución sin privilegios ni servicios

El **ZIP portable** se publica con `dotnet publish` self-contained para `win-x64`. NO DEBE requerir
la instalación previa de .NET, NO DEBE instalar servicios de Windows y NO DEBE exigir privilegios
de administrador para funcionar. Esa garantía no se negocia.

El **instalador** puede publicarse dependiente del framework, y en ese caso DEBE comprobar que el
Escritorio de .NET esté presente y, si falta, decir cuál y ofrecer la página oficial antes de
abortar. Se distribuyen dos: uno liviano y uno completo self-contained para equipos sin internet.
Que el instalador dependa del framework NO habilita que el ZIP portable lo haga.

Los guiones del instalador con texto acentuado DEBEN guardarse en UTF-8 **con BOM**: `makensis`
decide la codificación del fuente por el BOM, y sin él lee los acentos en la página ANSI del
sistema.

La base SQLite y los logs residen bajo `%LocalAppData%\CafManagerConection`. El instalador pone el
ejecutable en `%ProgramFiles%` —y por eso pide elevación **al instalar**, que es distinto de
exigirla para funcionar—, pero los datos permanecen en `%LocalAppData%`. El desinstalador NO DEBE
borrarlos salvo pedido explícito del usuario, y lo que ofrezca borrar MUST describir con exactitud
qué se lleva.

La observabilidad usa Serilog con `Serilog.Sinks.File` y registra: inicio y cierre, apertura de
conexión, resultado, desconexión, errores técnicos y migraciones. Todo registro está subordinado
al Principio II.

El aviso de versión nueva NO DEBE instalar servicio ni tarea programada, NO DEBE descargar ni
instalar nada sin que el usuario lo pida, y NO DEBE enviar dato alguno: la consulta es de lectura y
no lleva versión, equipo ni usuario. Como el instalador NO está firmado, la aplicación DEBE
verificar el SHA-256 publicado antes de ejecutar lo descargado y descartar lo que no coincida.

Motivo: un administrador debe poder descomprimir CMC en cualquier equipo y usarlo, sin pedirle
permisos a nadie ni dejar residuos.

## Restricciones tecnológicas

El stack está fijado y no se cambia sin enmienda:

| Área | Tecnología |
| --- | --- |
| Plataforma | Windows 11 x64 |
| Framework | .NET 10 |
| Lenguaje | C# 14 |
| Interfaz | WPF (XAML) + `WindowsFormsHost` para terminal y RDP |
| Apariencia | shadcn/ui (escala `zinc`) con estilos y plantillas XAML propias |
| RDP | `AxMSTSCLib.AxMsRdpClient11NotSafeForScripting` (incluido en Windows) |
| SSH, SFTP y túneles | SSH.NET |
| Métricas e inventario | Lecturas de `/proc`, `df`, `uname` y CLI remota sobre SSH.NET |
| Emulación VT | Emulador propio |
| Renderizado del terminal | Control WinForms propio |
| Base de datos | SQLite (`Microsoft.Data.Sqlite`) |
| Acceso a datos | Dapper |
| Cifrado de credenciales | AES-256-GCM (`AesGcm`, del BCL) |
| Derivación de clave | PBKDF2-HMAC-SHA512 (`Rfc2898DeriveBytes`, del BCL) |
| Recordar el equipo | DPAPI `CurrentUser`, por P/Invoke a `crypt32` |
| Configuración | System.Text.Json |
| Logs | Serilog + Serilog.Sinks.File |
| Gráficos | OxyPlot.Wpf |
| Iconos | Geometrías `Path` declaradas en XAML |
| Pruebas | xUnit + NSubstitute + Coverlet |
| Especificaciones | Spec Kit |
| Distribución | `dotnet publish` self-contained `win-x64`, más instalador NSIS |

Estructura de la solución:

```text
src/
├── CafManagerConection.App              # WPF: Views, Panels, Controls, Themes, Bootstrap
├── CafManagerConection.Domain           # Connections, Credentials, Sessions, Settings, Monitoring
├── CafManagerConection.UseCases         # Connections, Folders, Credentials, Sessions, Validation
├── CafManagerConection.Infrastructure   # Database, Credentials, Logging, Configuration
├── CafManagerConection.Rdp              # Adaptador del cliente RDP de Windows
├── CafManagerConection.Ssh              # Adaptador SSH.NET: shell, SFTP y túneles
├── CafManagerConection.Terminal         # Emulador VT propio y control de terminal
├── CafManagerConection.Monitoring       # Métricas de servidores Linux por /proc
└── CafManagerConection.Platform         # Inventario de Docker, nginx y supervisord

tests/  — un proyecto espejo por cada uno de los de arriba
```

## Flujo de trabajo y puertas de calidad

El desarrollo sigue el ciclo de Spec Kit: `specify` → `clarify` → `plan` → `tasks` → `implement` →
`analyze`. Ningún trabajo de implementación comienza sin un `plan.md` aprobado y su `tasks.md`
derivado.

Puertas obligatorias:

1. **Puerta constitucional**: todo `plan.md` completa la sección Constitution Check antes de la
   fase de investigación y la revalida después del diseño. Una violación sin justificar detiene el
   plan.
2. **Puerta de esquema**: todo cambio al esquema de la base requiere justificación previa por
   escrito —qué problema resuelve, qué alternativas se descartaron, qué impacto tiene sobre los
   datos existentes— y confirmación explícita antes de escribir o ejecutar nada. Está prohibido
   ejecutar operaciones destructivas contra una base real. Abrir una base cuya versión de esquema
   es **mayor** que la que la aplicación conoce MUST abortar nombrando las dos versiones.
3. **Puerta de secretos**: cualquier cambio que toque credenciales, logs o persistencia se revisa
   contra el Principio II, con tres preguntas concretas: ¿el nonce es nuevo en cada cifrado?, ¿la
   etiqueta de autenticación se verifica antes de usar el texto descifrado?, ¿los búferes se pisan
   con ceros en todos los caminos de salida, incluido el de excepción?

**Comentarios**: el código se explica solo. El comentario no pasa del 5% y sobrevive sólo el que
registra lo que el código no puede decir: un dato medido o un defecto ya pagado, con su número o su
ruta. Hay una prueba que lo hace cumplir.

**Datos reales**: el repositorio es público. No entran direcciones de redes privadas, nombres de
servidores, usuarios, dominios internos ni nombres de clientes, ni en el código, ni en las pruebas,
ni en la documentación. Hay una prueba que lo hace cumplir.

Las especificaciones, los planes, la documentación y los comentarios se escriben en español. Los
identificadores de código permanecen en inglés y no se traducen.

## Governance

**Procedimiento de enmienda**: toda enmienda se documenta en el propio archivo, se versiona según
versionado semántico y registra su fecha en la línea final.

- **MAJOR**: se elimina o redefine un principio de forma incompatible con lo anterior.
- **MINOR**: se agrega un principio o una sección, o se amplía materialmente una guía.
- **PATCH**: aclaraciones, redacción y correcciones sin cambio semántico.

**Cumplimiento**: cada plan y cada revisión verifican los seis principios. Toda desviación —agregar
un proyecto, incorporar una dependencia, saltear una puerta— debe justificarse en la tabla de
Complexity Tracking del plan correspondiente, indicando la alternativa más simple que se descartó y
por qué. Una desviación sin justificación registrada es un defecto, no una decisión.

**Version**: 1.0.0 | **Ratified**: 2026-08-24 | **Last Amended**: 2026-09-03
