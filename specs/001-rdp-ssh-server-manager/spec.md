# Feature Specification: CafManagerConection (CMC) — administrador de servidores RDP y SSH en pestañas

**Feature Branch**: `001-rdp-ssh-server-manager`

**Created**: 2026-08-24

**Status**: Draft

**Input**: User description: "CafManagerConection (CMC): administrador liviano de servidores para Windows 11 con sesiones RDP y SSH en pestañas. Un único lugar donde tener los servidores organizados en carpetas, abrirlos con doble clic y trabajar con varias sesiones simultáneas dentro de una sola ventana con pestañas."

> **Nota de idioma**: el contenido está redactado en español. Los títulos de sección se
> conservan en inglés porque son la estructura que consumen los comandos posteriores de
> Spec Kit (`/speckit-clarify`, `/speckit-plan`, `/speckit-tasks`, `/speckit-analyze`).

## Clarifications

### Session 2026-08-24

Respondida por el usuario:

- Q: Con la validación de certificado activa, ¿qué hace CMC ante un certificado RDP no confiable? → A: advertir mostrando el detalle y el motivo, y dejar que el usuario continúe por esta vez o recuerde la decisión para esa conexión (FR-016a)

- Q: ¿Qué pasa al hacer doble clic en una conexión que ya tiene una sesión abierta? → A: llevar el foco a la pestaña existente; abrir otra sesión queda como acción explícita del menú contextual (FR-044a)
- Q: ¿Qué aspecto debe tener la interfaz? → A: lo más parecido posible a una aplicación nativa de Windows 11 con lenguaje Fluent, manteniendo WinForms y usando P/Invoke a las API de composición de Windows (FR-065 a FR-070). **Revisado 2026-08-25**: la interfaz se migró a **WPF** con plantillas XAML propias y el lenguaje visual de **shadcn/ui**; WinForms se conserva sólo para hospedar el ActiveX de RDP y el control de terminal
- Q: ¿Hasta dónde llega el trabajo de apariencia? → A: completo — ventana y todos los controles, incluido un control de pestañas propio al estilo de Windows Terminal
- Q: La capa `Application` colisiona con `System.Windows.Forms.Application`, ¿cómo se resuelve? → A: renombrar el proyecto a `CafManagerConection.UseCases` (constitución v1.1.0)
- Q: ¿Dónde viven las pruebas del adaptador RDP? → A: en un proyecto propio `CafManagerConection.Rdp.Tests` (constitución v1.1.0)
- Q: ¿Orden de trabajo? → A: retirar el riesgo técnico primero (prueba de concepto del terminal e interop RDP) antes de construir volumen
- Q: ¿SFTP entra en alcance? → A: sí, como explorador de archivos con transferencias; SCP sigue fuera (constitución v1.2.0, US6)
- Q: SSH.NET no comparte conexión entre terminal y SFTP, ¿cómo se maneja? → A: conexión SFTP aparte, abierta al abrir el panel y cerrada al cerrarlo, reutilizando credencial y fingerprint
- Q: ¿Panel de métricas de servidores Linux? → A: sí, sin agentes, por conexión SSH auxiliar y lecturas de `/proc`, muestreo cada 5 s sólo con el panel visible e historial corto en memoria (US7)
- Q: ¿Qué hace el panel de estado ante un host que no es Linux? → A: se detecta y el panel no se ofrece para esa conexión
- Q: ¿Cómo se organizan terminal, archivos y estado en una sesión SSH? → A: terminal siempre visible, con los demás como paneles laterales desplegables (FR-070a)
- Q: ¿Túneles SSH? → A: sí, reenvío de puerto local definido por conexión (constitución v1.2.0, US8)
- Q: ¿Gestión de Docker, nginx y supervisord? → A: sí, empezando por inventario de sólo lectura; las acciones de escritura quedan para una etapa posterior (US9 y US10)
- Q: ¿Cómo accede CMC a Docker? → A: línea de comandos sobre SSH, con `sudo` si hace falta, y por la API a través de un túnel cuando esté disponible
- Q: ¿Docker, nginx y supervisord van en una feature aparte? → A: no, dentro de esta misma feature

**Segunda ronda de clarificación (asumidas, pendientes de confirmación)**: el usuario no
estuvo disponible. Se resolvieron con el criterio más conservador —el que no destruye datos
y el que no carga al servidor— y se documentan para que las revise.

- Q: Al transferir, si el archivo ya existe en el destino, ¿qué hace? → A (asumida): preguntar, ofreciendo sobrescribir, omitir o conservar ambos renombrando, con casilla de "aplicar a todos" (FR-106)
- Q: Una carpeta con conexiones RDP y SSH mezcladas tiene una sola credencial heredable, ¿cómo se resuelve? → A (asumida): una credencial por protocolo, con clave `cmc:folder:<id>:<rdp|ssh>` (FR-064)
- Q: ¿Con qué frecuencia se refresca el inventario de Docker, nginx y supervisord? → A (asumida): consulta al abrir el panel y después sólo con un botón de refrescar (FR-107)
- Q: Si se cae la sesión SSH principal con paneles abiertos, ¿qué pasa con sus conexiones auxiliares? → A (asumida): se cierran con ella y los paneles quedan inactivos hasta reconectar (FR-108)
- Q: ¿Qué pasa con los túneles activos al cerrar la aplicación? → A (asumida): se cuentan junto a las sesiones en la advertencia de cierre y se liberan sus puertos (FR-109)

**Tercera ronda de clarificación (respondidas por el usuario)**:

- Q: ¿Accesibilidad y manejo por teclado? → A: navegación completa por teclado y atajos para lo frecuente; sin compromiso de soporte de lectores de pantalla en la v1 (FR-111)
- Q: ¿Qué pasa al abrir una segunda instancia de la aplicación? → A: instancia única; la segunda trae al frente la existente y termina (FR-112)
- Q: ¿Y si se edita una conexión con una sesión abierta? → A: los cambios se aplican en la próxima conexión; la pestaña indica que hay cambios pendientes (FR-113)
- Q: ¿Cómo se respalda o migra la configuración? → A: copiando el archivo de base a mano; se documenta. Las credenciales no viajan, porque viven en el Credential Manager del equipo

**Decisiones asumidas, pendientes de confirmación**: el usuario no estuvo disponible para
responderlas. Se resolvieron con el criterio más conservador y se documentan acá para que las
revise; revertir cualquiera es una edición puntual.

- Q: ¿Cuánto se conservan los archivos de registro? → A (asumida): rotación diaria y retención de 30 días (FR-057a)
- Q: ¿Hay un tope de sesiones simultáneas? → A (asumida): la aplicación no impone ninguno; SC-003 fija 8 como objetivo verificable, no como máximo
- Q: ¿Qué se hereda desde una carpeta? → A (asumida): credencial, usuario, dominio, puerto y los ajustes específicos de cada protocolo (FR-058 a FR-064)
- Q: ¿Cómo se marca que un valor es heredado o propio? → A (asumida): casilla "heredar" explícita por campo, mostrando al lado el valor heredado
- Q: ¿La herencia atraviesa varios niveles de carpetas? → A (asumida): sí, cascada completa hasta la raíz
- Q: ¿Qué pasa al mover una conexión que hereda a otra carpeta? → A (asumida): recalcular contra la nueva carpeta, advirtiendo en la confirmación si cambia algún valor efectivo

Ya estaba resuelto en la especificación y no requirió pregunta: el comportamiento tras
suspender y reanudar el equipo (ver Edge Cases).

### Session 2026-08-25

Contexto: la migración 2 —color de icono, conexiones hijas y metadatos de catálogo— estaba
diseñada en `data-model.md` sin ningún requisito funcional detrás. Estas cinco decisiones
cierran ese hueco.

**Confirmado por el usuario:**

- Q: ¿Se habilitan color por elemento y conexiones hijas? → A: sí, las dos cosas (migración 2)
- Q: ¿Qué otros datos de catálogo se agregan? → A: los que eviten migrar de nuevo por cada campo

**Adoptado por recomendación, sin confirmación explícita** *(revisar si algo no coincide con
lo que esperabas)*:

- Q: ¿Qué hereda una conexión hija de su padre? → A: host, usuario, dominio y credencial, con
  la misma cascada que las carpetas. Protocolo y puerto son **siempre propios**: heredarlos
  haría nacer un servicio web como SSH en el puerto 22 (FR-125, FR-126)
- Q: ¿Una conexión hija puede tener hijas a su vez? → A: no, un solo nivel. El caso real es
  «servidor y sus servicios»; permitir profundidad arbitraria complica el árbol y el arrastrar
  y soltar sin un uso que lo pida. Además vuelve **imposible** el ciclo, que era el riesgo
  anotado en `data-model.md` (FR-127)
- Q: ¿Qué hace el entorno además de distinguirse visualmente? → A: nada más. Marca visible y
  permanente en el árbol y en la barra de la sesión, **sin** confirmación al conectar. Un
  diálogo antes de cada conexión a producción se acepta por reflejo en una semana —es el mismo
  defecto que tenía el aviso de fingerprint— y deja de proteger (FR-130)
- Q: ¿Los campos propios (`custom_fields`) se editan desde la aplicación? → A: no en esta
  versión. La columna existe para que un dato nuevo no exija otra migración, pero sin interfaz:
  una caja de texto con JSON crudo invita a datos malformados, y un editor de pares
  clave-valor es trabajo real por una función que nadie pidió usar todavía (FR-133)
- Q: ¿Las etiquetas salen de una lista controlada? → A: no, texto libre separado por comas,
  con autocompletado de las etiquetas ya en uso. Da consistencia sin necesitar una pantalla de
  administración de etiquetas (FR-129)

### Session 2026-08-31

Contexto: el usuario pidió que el terminal SSH «se comporte como PuTTY» en teclas, copiado,
pegado y selección. FR-030 sólo decía «seleccionar, copiar y pegar», sin nombrar un gesto ni
un atajo: no había requisito contra el cual implementar ni verificar. Estas cinco decisiones
fijan el estándar conducta por conducta.

**Confirmado por el usuario:**

- Q: ¿Qué hace el clic derecho dentro del terminal? → A: pega, y el menú contextual pasa a
  **Ctrl+clic derecho**. El clic derecho ya pegó antes y se retiró porque dejaba sin salida a
  quien seleccionaba y hacía clic derecho esperando «Copiar»; con el copiado automático de
  FR-030a esa maniobra ya no hace falta, y el menú sigue existiendo con Ctrl. Esta decisión
  revierte deliberadamente la de T312 y su motivo ya no aplica (FR-030b, FR-030d)

**Decisiones asumidas, pendientes de confirmación**: el usuario no estuvo disponible para
responder las cuatro restantes. Se resolvieron adoptando el comportamiento de PuTTY salvo
donde apartarse tiene un motivo escrito, y se documentan acá para que las revise; revertir
cualquiera es una edición puntual.

- Q: ¿La selección con el botón izquierdo copia sola al portapapeles? → A (asumida): sí, al
  soltar. Es el núcleo del comportamiento de PuTTY y el par natural del clic derecho que pega
  (FR-030a)
- Q: ¿Ctrl+C copia o interrumpe? → A (asumida): interrumpe **siempre**, como PuTTY. Con
  FR-030a habría casi siempre una selección viva, y un Ctrl+C que copia en lugar de cortar
  deja al usuario sin manera de parar un comando corriendo. Copiar queda en la selección, en
  Ctrl+Ins y en Ctrl+Shift+C (FR-030c)
- Q: ¿Qué atajos de copiar y pegar valen? → A (asumida): los dos juegos. Ctrl+Ins y Shift+Ins
  porque son los que documenta PuTTY, y Ctrl+Shift+C y Ctrl+Shift+V porque ya están
  implementados, anunciados en el menú y son los de cualquier terminal actual (FR-030c)
- Q: ¿Qué otras conductas del estándar entran? → A (asumida): selección rectangular con
  Ctrl+arrastre, botón medio que extiende la selección, `bracketed paste` para que pegar en
  `vim` o en un shell con autoindentado no salga escalonado, y las teclas de historial de
  PuTTY sumadas a las que ya hay. Los separadores de palabra quedan fijos, más anchos que los
  de PuTTY y sin hacerlos configurables (FR-030e, FR-030f, FR-154d, FR-154e, FR-155)

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Conectarme a un servidor Windows por RDP desde una lista guardada (Priority: P1)

Como administrador de sistemas, quiero guardar un servidor Windows con su dirección, su
usuario y su contraseña una sola vez, y después abrirlo con doble clic para ver su
escritorio dentro de una pestaña de la aplicación, sin tener que recordar la IP ni volver
a tipear credenciales.

**Why this priority**: es el caso de uso más frecuente del administrador y el que elimina
de inmediato el dolor actual de abrir `mstsc.exe` una y otra vez tipeando datos. Por sí
sola, esta historia ya reemplaza el flujo de trabajo existente para servidores Windows y
constituye un producto usable.

**Independent Test**: se prueba por completo creando una conexión RDP contra un servidor
Windows real, cerrando y reabriendo la aplicación, y conectándose con doble clic; entrega
valor porque el administrador ya puede trabajar en ese servidor sin herramientas externas.

**Acceptance Scenarios**:

1. **Given** la aplicación abierta sin ninguna conexión guardada, **When** el usuario crea
   una conexión RDP indicando nombre, host, puerto, dominio opcional, usuario y contraseña,
   **Then** la conexión aparece en la lista de servidores y la contraseña queda guardada en
   el almacén de credenciales del sistema operativo.
2. **Given** una conexión RDP guardada, **When** el usuario hace doble clic sobre ella,
   **Then** se abre una pestaña rotulada con el nombre de la conexión, se muestra el estado
   "Conectando", y al completarse la autenticación se ve el escritorio remoto dentro de la
   pestaña con el estado "Conectado".
3. **Given** una sesión RDP conectada, **When** el usuario redimensiona la ventana de la
   aplicación y la conexión tiene activada la opción de ajustar la resolución al tamaño de
   la pestaña, **Then** el escritorio remoto se reajusta al nuevo tamaño sin cortar el
   contenido ni dejar barras negras.
4. **Given** una sesión RDP conectada, **When** el usuario cierra la pestaña, **Then** la
   sesión remota se desconecta y la pestaña desaparece sin afectar a otras sesiones.
5. **Given** una conexión RDP cuyo servidor está apagado, **When** el usuario intenta
   conectarse, **Then** la pestaña muestra el estado "Error" con un mensaje que indica que
   el host no está accesible y ofrece la acción de reintentar.
6. **Given** una conexión RDP guardada, **When** el usuario la abre, **Then** la sesión no
   expone al servidor remoto los discos locales, el audio, el micrófono, las impresoras,
   los puertos, las cámaras ni las tarjetas inteligentes del equipo del administrador.
7. **Given** una conexión RDP con el portapapeles deshabilitado, **When** el usuario copia
   texto dentro de la sesión remota, **Then** ese texto no queda disponible en el
   portapapeles de Windows local.

---

### User Story 2 - Trabajar en un servidor Linux por SSH con un terminal integrado (Priority: P2)

Como administrador, quiero abrir una sesión SSH en una pestaña de la misma aplicación y
usar un terminal que se comporte como el de cualquier cliente serio, para editar archivos
con `vim` o `nano`, mirar procesos con `top` o `htop`, leer con `less` y trabajar dentro de
`tmux` sin que la pantalla se corrompa.

**Why this priority**: completa la mitad Linux del parque de servidores y elimina la
segunda herramienta externa. Va después de RDP porque el terminal es la parte de mayor
riesgo técnico y conviene construirlo sobre una aplicación que ya funciona.

**Independent Test**: se prueba conectándose a un servidor Linux y ejecutando una batería
fija de programas de pantalla completa, verificando colores, acentos y redimensionamiento;
entrega valor porque el administrador ya puede operar sus servidores Linux desde CMC.

**Acceptance Scenarios**:

1. **Given** la aplicación abierta, **When** el usuario crea una conexión SSH con host,
   puerto, usuario y autenticación por contraseña, **Then** la conexión queda guardada y la
   contraseña se almacena en el almacén de credenciales del sistema operativo.
2. **Given** una conexión SSH con autenticación por clave privada, **When** el usuario
   indica la ruta de la clave y su passphrase, **Then** la conexión queda guardada, la
   passphrase se almacena en el almacén de credenciales y la base local guarda solo la ruta
   del archivo de clave, nunca su contenido.
3. **Given** una conexión SSH a un host al que nunca se conectó, **When** el usuario se
   conecta, **Then** la aplicación muestra el fingerprint de la clave del host y no
   establece la sesión hasta que el usuario lo acepta o lo rechaza explícitamente.
4. **Given** una conexión SSH con un fingerprint ya aceptado, **When** el servidor presenta
   un fingerprint distinto, **Then** la conexión se bloquea, no se envía ninguna credencial
   y se muestra una advertencia que explica que la identidad del host cambió.
5. **Given** una sesión SSH conectada, **When** el usuario ejecuta `vim`, `nano`, `top`,
   `htop`, `less` o `tmux`, **Then** la aplicación se dibuja completa y correctamente, y
   responde al teclado incluyendo `Ctrl`, `Alt`, `Shift`, las flechas y las teclas de
   función.
6. **Given** una sesión SSH conectada, **When** el usuario redimensiona la ventana o el
   panel, **Then** el tamaño del terminal se comunica al servidor remoto y las aplicaciones
   de pantalla completa se redibujan al nuevo tamaño sin corrupción visual.
7. **Given** una sesión SSH conectada, **When** el servidor emite texto con colores ANSI,
   con una paleta de 256 colores y con caracteres Unicode (acentos, `ñ`, símbolos),
   **Then** todo se muestra con el color y el glifo correctos.
8. **Given** una sesión SSH conectada, **When** el usuario selecciona texto con el mouse y
   suelta el botón, **Then** el texto ya está en el portapapeles de Windows sin apretar nada
   más; **And when** hace clic derecho o aprieta Shift+Ins, **Then** el contenido del
   portapapeles se envía a la sesión remota; **And when** aprieta Ctrl+C con un comando
   corriendo, **Then** el comando se interrumpe, haya o no una selección hecha.
9. **Given** una sesión SSH con salida extensa, **When** el usuario se desplaza hacia
   arriba, **Then** puede revisar la salida anterior hasta el límite de historial
   configurado, y al volver al final el terminal sigue funcionando normalmente.
10. **Given** una conexión SSH cuya clave privada fue movida o borrada, **When** el usuario
    intenta conectarse, **Then** se muestra un mensaje que identifica el archivo faltante y
    ofrece corregir la ruta, sin dejar la pestaña en un estado ambiguo.

---

### User Story 3 - Organizar en carpetas, heredar su configuración y encontrar rápido (Priority: P3)

Como administrador con muchos servidores, quiero agruparlos en carpetas por entorno
(Producción, Desarrollo), definir una sola vez en cada carpeta la credencial y los parámetros
que comparten sus servidores, y encontrar cualquiera escribiendo parte de su nombre, host o
usuario, para no perder tiempo recorriendo una lista larga ni cargando veinte veces la misma
contraseña.

**Why this priority**: mejora fuertemente la usabilidad diaria, pero solo tiene sentido
cuando ya existen conexiones que organizar. Con pocas conexiones, la lista plana alcanza.

**Independent Test**: se prueba definiendo credencial, usuario y puerto en una carpeta,
creando dentro una veintena de conexiones que los heredan, moviéndolas entre carpetas y
buscándolas por distintos criterios; entrega valor porque reduce el alta de un servidor a
escribir su nombre y su host, y la localización a un par de pulsaciones.

**Acceptance Scenarios**:

1. **Given** la lista de servidores, **When** el usuario crea una carpeta y le da un
   nombre, **Then** la carpeta aparece en el árbol y puede contener conexiones y otras
   carpetas.
2. **Given** una conexión dentro de una carpeta, **When** el usuario la mueve a otra
   carpeta, **Then** la conexión queda bajo la carpeta destino y esa ubicación persiste al
   reiniciar la aplicación.
3. **Given** varias conexiones guardadas, **When** el usuario escribe texto en el buscador,
   **Then** el árbol muestra únicamente las conexiones cuyo nombre, host o usuario contienen
   ese texto, junto con las carpetas que las contienen.
4. **Given** una conexión existente, **When** el usuario la duplica, **Then** se crea una
   copia editable con todos sus parámetros, con un nombre que la distingue del original, y
   compartiendo o copiando su credencial según lo que el usuario elija.
5. **Given** una carpeta que contiene conexiones, **When** el usuario intenta eliminarla,
   **Then** la aplicación advierte cuántas conexiones se eliminarán y solo procede tras la
   confirmación explícita del usuario.
6. **Given** una conexión guardada, **When** el usuario escribe notas libres sobre ella,
   **Then** esas notas quedan asociadas a la conexión y se pueden consultar y editar más
   tarde.
7. **Given** conexiones ya usadas, **When** el usuario mira la lista, **Then** puede ver
   cuándo fue la última vez que se conectó a cada una.
8. **Given** una carpeta con varias conexiones, **When** el usuario reordena las conexiones
   dentro de ella, **Then** ese orden se conserva entre ejecuciones.
9. **Given** una carpeta, **When** el usuario define en ella un usuario, un puerto y una
   credencial, **Then** las conexiones que cree dentro de esa carpeta toman esos valores de
   forma predeterminada, sin volver a cargarlos.
10. **Given** una carpeta con veinte conexiones SSH que heredan su credencial, **When** el
    usuario cambia la credencial de la carpeta, **Then** las veinte pasan a usar la nueva sin
    editarlas una por una, y la aplicación informa cuántas resultan afectadas.
11. **Given** una conexión que hereda el puerto de su carpeta, **When** el usuario desmarca
    la casilla de herencia de ese campo y escribe otro puerto, **Then** esa conexión usa su
    valor propio y las demás siguen heredando.
12. **Given** una conexión que hereda su credencial, **When** el usuario la mueve a una
    carpeta con otra credencial, **Then** la confirmación del movimiento advierte que la
    credencial efectiva va a cambiar, y al aceptar la conexión hereda la de la nueva carpeta.
13. **Given** una conexión con un campo marcado como heredado, **When** el usuario abre su
    editor, **Then** ve el valor que hereda y de qué carpeta proviene.

---

### User Story 4 - Trabajar con varias sesiones a la vez en una sola ventana (Priority: P4)

Como administrador que compara configuraciones entre servidores, quiero tener varias
sesiones RDP y SSH abiertas simultáneamente en pestañas de la misma ventana, cambiar entre
ellas, reconectar la que se cayó y poner una a pantalla completa cuando necesito espacio.

**Why this priority**: es lo que convierte la herramienta en un administrador de servidores
y no en un cliente de una sesión por vez, pero requiere que al menos un protocolo ya
funcione de punta a punta.

**Independent Test**: se prueba abriendo simultáneamente varias sesiones de ambos
protocolos, alternando entre ellas, forzando la caída de una y reconectándola; entrega
valor porque el administrador deja de manejar un escritorio lleno de ventanas sueltas.

**Acceptance Scenarios**:

1. **Given** una sesión abierta, **When** el usuario abre otra conexión, **Then** se agrega
   una pestaña nueva y la sesión anterior sigue conectada y funcionando en segundo plano.
2. **Given** varias pestañas abiertas, **When** el usuario cambia de pestaña, **Then** la
   sesión seleccionada se muestra de inmediato y la barra de estado refleja su estado y su
   destino (por ejemplo, "Conectado · admin@192.0.2.20").
3. **Given** una sesión que se desconectó de forma inesperada, **When** el usuario elige
   reconectar, **Then** la aplicación reintenta la conexión con los mismos parámetros y
   credenciales, sin pedirlos de nuevo si siguen disponibles.
4. **Given** una sesión activa, **When** el usuario activa la pantalla completa, **Then** la
   sesión ocupa toda la pantalla; **And when** sale de pantalla completa, **Then** vuelve a
   su pestaña con el mismo estado de conexión.
5. **Given** la aplicación con sesiones abiertas, **When** el usuario la cierra, **Then** se
   le advierte cuántas sesiones activas se van a cerrar y solo se cierra tras su
   confirmación.
6. **Given** la aplicación cerrada previamente en una posición y tamaño determinados,
   **When** el usuario la vuelve a abrir, **Then** la ventana reaparece con el mismo tamaño,
   la misma posición y el mismo tema (claro u oscuro).
7. **Given** una conexión con una sesión abierta, **When** el usuario intenta eliminar esa
   conexión, **Then** la aplicación advierte que hay una sesión activa y solo elimina tras
   la confirmación, cerrando la sesión asociada.

---

### User Story 5 - Mantener las credenciales guardadas bajo control (Priority: P5)

Como administrador responsable de la seguridad, quiero poder actualizar o borrar la
contraseña, la passphrase o la clave asociada a una conexión cuando roto credenciales, y
quiero estar seguro de que ningún secreto quedó escrito en archivos legibles.

**Why this priority**: es indispensable para el uso sostenido en el tiempo, pero no impide
el primer día de trabajo; las credenciales se cargan al crear la conexión en las historias
anteriores.

**Independent Test**: se prueba rotando la contraseña de una conexión existente,
borrándola, e inspeccionando la base local y los archivos de registro para confirmar que no
contienen secretos; entrega valor porque habilita el cumplimiento de la política de
credenciales de la organización.

**Acceptance Scenarios**:

1. **Given** una conexión con contraseña guardada, **When** el usuario la edita y escribe
   una contraseña nueva, **Then** la credencial almacenada se reemplaza y la siguiente
   conexión usa la nueva.
2. **Given** una conexión con credencial guardada, **When** el usuario elimina la
   credencial, **Then** la conexión se conserva y en el siguiente intento la aplicación
   pide la credencial al momento de conectar.
3. **Given** una conexión cuya credencial fue borrada por fuera de la aplicación, **When**
   el usuario se conecta, **Then** se le informa que la credencial guardada ya no existe y
   se le ofrece ingresarla, con la opción de volver a guardarla.
4. **Given** un recorrido completo por todas las funciones de la aplicación, **When** se
   inspeccionan la base de datos local y los archivos de registro, **Then** no aparece
   ninguna contraseña, passphrase ni contenido de clave privada, ni el texto tecleado en las
   sesiones SSH, ni el contenido de pantalla de las sesiones RDP, ni el portapapeles.
5. **Given** una conexión eliminada, **When** el usuario revisa el almacén de credenciales
   del sistema, **Then** la credencial asociada a esa conexión también fue eliminada.

---

### User Story 6 - Enviar y traer archivos del servidor sin salir de la sesión (Priority: P6)

Como administrador, quiero abrir un panel de archivos junto al terminal de una sesión SSH,
ver el árbol remoto y mover archivos en ambos sentidos, para no tener que abrir un cliente
SFTP aparte cada vez que necesito subir una configuración o bajar un log.

**Why this priority**: extiende una sesión SSH que ya funciona y elimina otra herramienta
externa. Depende por completo de US2.

**Independent Test**: conectarse a un servidor Linux, abrir el panel de archivos, navegar,
subir un archivo, bajarlo de vuelta y verificar que llegó íntegro.

**Acceptance Scenarios**:

1. **Given** una sesión SSH conectada, **When** el usuario abre el panel de archivos,
   **Then** se establece la conexión SFTP reutilizando credencial, usuario, puerto y
   fingerprint ya aceptado, sin volver a pedir datos, y se muestra el directorio del usuario.
2. **Given** el panel de archivos abierto, **When** el usuario navega por el árbol remoto,
   **Then** ve nombre, tamaño y fecha de modificación de cada entrada.
3. **Given** un archivo local, **When** el usuario lo envía al servidor, **Then** se muestra
   el progreso de la transferencia y se puede cancelar.
4. **Given** un archivo remoto, **When** el usuario lo trae, **Then** llega íntegro al equipo
   local con su contenido idéntico.
5. **Given** el panel de archivos abierto, **When** el usuario crea una carpeta, renombra o
   elimina una entrada, **Then** la operación se refleja en el servidor tras confirmarla.
6. **Given** el panel de archivos abierto, **When** el usuario lo cierra, **Then** la
   conexión SFTP se cierra y la sesión de terminal sigue intacta.
7. **Given** una transferencia en curso, **When** falla por permisos o por corte de red,
   **Then** se informa qué archivo falló y por qué, sin abortar el resto de la cola.

---

### User Story 7 - Ver el estado del servidor Linux de un vistazo (Priority: P7)

Como administrador, quiero un panel que me muestre CPU, memoria, carga, uptime, discos y red
del servidor al que estoy conectado, para saber cómo está sin tener que tipear `top`, `df` y
`free` cada vez.

**Why this priority**: es la funcionalidad más independiente del conjunto; nada depende de
ella. Aporta mucho valor visible con riesgo técnico bajo, porque sólo lee.

**Independent Test**: conectarse a un servidor Linux, abrir el panel de estado y verificar
que los valores coinciden con los que devuelven `top`, `free -m`, `df -h` y `uptime`
ejecutados a mano.

**Acceptance Scenarios**:

1. **Given** una sesión SSH a un servidor Linux, **When** el usuario abre el panel de estado,
   **Then** se abre una conexión SSH auxiliar y a los pocos segundos se muestran CPU,
   memoria, carga, uptime, discos y red.
2. **Given** el panel de estado abierto, **When** pasa el tiempo, **Then** los valores se
   actualizan cada 5 segundos y se conservan visibles los últimos 5 minutos de CPU, memoria
   y red.
3. **Given** el panel de estado abierto, **When** el usuario lo cierra o cambia de panel,
   **Then** el muestreo se detiene y la conexión auxiliar se cierra.
4. **Given** una consulta de métricas que todavía no terminó, **When** llega el momento de la
   siguiente, **Then** la anterior se cancela en lugar de acumularse.
5. **Given** una sesión SSH a un host que no es Linux, **When** el usuario mira la sesión,
   **Then** el panel de estado no se ofrece para esa conexión.
6. **Given** el panel de estado abierto, **When** el usuario mira los discos, **Then** ve
   sólo sistemas de archivos reales, sin `tmpfs`, `devtmpfs`, `overlay`, `squashfs`, `proc`
   ni `sysfs`.
7. **Given** el panel de estado abierto, **When** el usuario mira la red, **Then** ve las
   interfaces con tráfico, sin `lo` ni interfaces virtuales inactivas, y puede elegir cuáles
   mostrar.

---

### User Story 8 - Mapear puertos del servidor en mi equipo (Priority: P8)

Como administrador, quiero definir en una conexión SSH qué puertos del servidor remoto se
mapean a puertos de mi equipo, y levantarlos cuando los necesito, para poder abrir en mi
navegador o mi cliente de base de datos un servicio que sólo escucha dentro del servidor.

**Why this priority**: habilita el acceso a la API de Docker de US9 y resuelve por sí sola un
caso frecuente. Depende de US2.

**Independent Test**: definir un túnel contra un servicio que sólo escuche en `localhost` del
servidor, levantarlo y comprobar que responde en el puerto local correspondiente.

**Acceptance Scenarios**:

1. **Given** una conexión SSH, **When** el usuario define un túnel indicando puerto local,
   host y puerto remotos, **Then** la definición queda guardada junto a la conexión.
2. **Given** un túnel definido, **When** el usuario lo levanta, **Then** el puerto local
   acepta conexiones y el tráfico llega al destino remoto.
3. **Given** un túnel activo, **When** el usuario lo detiene o cierra la sesión SSH,
   **Then** el puerto local se libera.
4. **Given** un túnel cuyo puerto local ya está ocupado, **When** el usuario intenta
   levantarlo, **Then** se informa el conflicto indicando el puerto, sin dejar el túnel en un
   estado ambiguo.
5. **Given** varios túneles definidos en una conexión, **When** el usuario los mira,
   **Then** ve cuáles están activos y cuáles no.
6. **Given** un túnel marcado para levantarse automáticamente, **When** la sesión SSH
   conecta, **Then** el túnel se levanta solo.

---

### User Story 9 - Ver qué hay corriendo en Docker (Priority: P9)

Como administrador de servidores con Docker, quiero ver los contenedores de un servidor, su
estado, y qué archivos `docker-compose` existen con los servicios que definen, para saber qué
está corriendo sin tipear `docker ps` ni buscar los compose a mano.

**Why this priority**: alto valor para el parque con Docker, pero es la funcionalidad más
extensa y conviene construirla sobre SSH, túneles y paneles ya funcionando.

**Independent Test**: conectarse a un servidor con Docker y verificar que la lista de
contenedores y su estado coinciden con `docker ps -a`, y que los compose detectados coinciden
con los que hay en el disco.

**Acceptance Scenarios**:

1. **Given** una sesión SSH a un servidor con Docker, **When** el usuario abre el panel de
   Docker, **Then** ve los contenedores con nombre, imagen, estado, puertos publicados y
   tiempo de ejecución.
2. **Given** un usuario remoto que no pertenece al grupo `docker`, **When** se consulta
   Docker, **Then** el sistema reintenta con `sudo` y, si tampoco puede, informa con
   claridad que faltan permisos en lugar de mostrar un panel vacío.
3. **Given** un servidor sin Docker instalado, **When** el usuario mira la sesión, **Then**
   el panel de Docker no se ofrece para esa conexión.
4. **Given** un servidor con archivos `docker-compose`, **When** el usuario abre el panel,
   **Then** ve qué compose existen, dónde están y qué servicios define cada uno.
5. **Given** un compose detectado, **When** el usuario lo mira, **Then** ve qué servicios
   están corriendo y cuáles no, relacionando cada servicio con su contenedor.
6. **Given** un túnel disponible hacia la API de Docker, **When** el sistema consulta,
   **Then** usa la API en lugar de interpretar la salida de texto de la línea de comandos.

---

### User Story 10 - Ver los sitios de nginx y los procesos de supervisord (Priority: P10)

Como administrador, quiero ver qué sitios tiene publicados nginx en un servidor y con qué
configuración, y qué procesos administra supervisord y cuáles están caídos, para diagnosticar
sin recorrer directorios de configuración a mano.

**Why this priority**: completa el inventario de la plataforma. Es la última porque el
diagnóstico puntual se puede hacer desde el terminal mientras tanto.

**Independent Test**: conectarse a un servidor con nginx y supervisord y verificar que los
sitios listados coinciden con los habilitados y que los estados de proceso coinciden con
`supervisorctl status`.

**Acceptance Scenarios**:

1. **Given** un servidor con nginx, **When** el usuario abre el panel correspondiente,
   **Then** ve los sitios habilitados con sus nombres de servidor, puertos en escucha y raíz
   de documentos.
2. **Given** un sitio de nginx, **When** el usuario lo selecciona, **Then** puede ver su
   configuración efectiva en modo lectura.
3. **Given** un servidor con supervisord, **When** el usuario abre el panel, **Then** ve cada
   proceso administrado con su estado y su tiempo de ejecución, destacando los que fallaron.
4. **Given** un servidor sin nginx o sin supervisord, **When** el usuario mira la sesión,
   **Then** el panel correspondiente no se ofrece.
5. **Given** archivos de configuración que el usuario remoto no puede leer, **When** se
   intenta el inventario, **Then** se informa la falta de permisos indicando qué no se pudo
   leer.

---

### User Story 11 - Saber qué está escuchando en un servidor y quién lo abrió (Priority: P11)

Como administrador, quiero ver qué puertos tiene abiertos un servidor y qué proceso tiene cada
uno, y poder averiguar de ese proceso qué binario es, quién lo corre y desde cuándo, para
responder «¿qué es esto que está escuchando en el 8080?» sin abrir una sesión y ponerme a
encadenar comandos.

**Why this priority**: es la última porque el panel de puertos **ya estaba construido** cuando
se escribió esta historia. Se documenta acá para regularizarlo (constitución v1.11.0): el
Principio V exige requisito antes de código, y este caso fue al revés. La ficha de proceso es
lo único genuinamente nuevo.

**Independent Test**: conectarse a un servidor Linux, abrir el panel de puertos y comparar la
lista con `ss -tulpn` ejecutado a mano; después hacer doble clic en una fila y comparar los
datos de la ficha con `ps -p <pid> -o ...` y `readlink /proc/<pid>/exe`.

**Acceptance Scenarios**:

1. **Given** un servidor Linux con servicios escuchando, **When** el usuario abre el panel de
   puertos, **Then** ve una fila por socket en escucha con su puerto, protocolo, dirección de
   escucha y el proceso que lo tiene.
2. **Given** un proceso reconocido —nginx, sshd, postgres—, **When** el usuario mira su fila,
   **Then** ve además el nombre legible de la aplicación al lado del nombre del proceso.
3. **Given** una fila del panel, **When** el usuario hace doble clic, **Then** se abre la ficha
   del proceso con su PID, el binario que está corriendo, el usuario que lo ejecuta, hace cuánto
   corre, su línea de comando y su directorio de trabajo.
4. **Given** un usuario remoto sin permisos para leer los datos de un proceso ajeno,
   **When** abre la ficha, **Then** ve los datos que sí se pudieron leer y un aviso que nombra
   los que no y por qué, en lugar de un error o campos vacíos.
5. **Given** un proceso que terminó entre la consulta de la lista y la apertura de la ficha,
   **When** el usuario hace doble clic, **Then** se informa que el proceso ya no existe y se
   ofrece refrescar la lista.
6. **Given** la ficha de un proceso abierta, **When** el usuario la recorre, **Then** no
   encuentra ninguna acción que modifique el servidor: no hay matar, ni señalar, ni cambiar
   prioridad.

---

### Edge Cases

- **Host inalcanzable**: el nombre no resuelve o el equipo no responde. La pestaña queda en
  estado de error con un mensaje que nombra la causa y ofrece reintentar; no se reintenta
  en bucle de forma automática.
- **Credenciales rechazadas**: el servidor rechaza usuario o contraseña. Se informa el
  rechazo sin revelar cuál de los dos datos falló y se ofrece corregir la credencial.
- **Tiempo de espera agotado**: la conexión no se completa dentro del límite. Se informa el
  vencimiento y se distingue del rechazo de credenciales.
- **Desconexión inesperada**: el servidor cierra la sesión o se pierde la red. La pestaña
  pasa a "Desconectado" indicando el motivo informado por el servidor cuando existe, y
  ofrece reconectar sin perder la configuración de la conexión.
- **Archivo ya existente en el destino**: se pide una decisión —sobrescribir, omitir o
  conservar ambos— antes de escribir nada, con opción de aplicarla al resto de la cola. Nunca
  se sobrescribe en silencio.
- **Caída de la sesión SSH con paneles abiertos**: las conexiones auxiliares de archivos,
  métricas e inventario se cierran junto con la principal y sus paneles quedan inactivos hasta
  que el usuario reconecte.
- **Cierre de la aplicación con túneles activos**: se cuentan junto a las sesiones en la
  advertencia previa y sus puertos locales se liberan al cerrar.
- **Consulta remota que excede su tiempo límite**: se cancela a los 3 segundos las métricas y
  a los 10 el inventario, se informa, y no se encola ninguna consulta pendiente.
- **Transferencia interrumpida**: si se corta la red durante una transferencia, se informa qué
  archivo quedó incompleto y la cola continúa con el resto. No se deja un archivo parcial
  presentado como completo.
- **Espacio insuficiente o permisos denegados al escribir**: se informa el motivo por archivo,
  sin abortar el resto de la cola.
- **Puerto local ocupado al levantar un túnel**: se informa el conflicto nombrando el puerto y
  el túnel queda detenido, nunca en un estado intermedio.
- **Servidor sin `/proc` legible, sin Docker, sin nginx o sin supervisord**: el panel
  correspondiente simplemente no se ofrece para esa conexión.
- **Comando remoto que no responde**: la consulta de métricas o de inventario tiene un tiempo
  límite; si lo supera se cancela y se informa, sin encolar consultas ni congelar el panel.
- **Usuario remoto sin permisos para Docker o para leer la configuración de nginx**: se
  informa qué falta, en lugar de mostrar un inventario incompleto en silencio.
- **Campo heredado que nadie define**: si ni la conexión ni ninguna carpeta ascendente
  definen un campo heredable, el valor efectivo queda vacío y se aplica el comportamiento del
  campo vacío: si es el usuario o la credencial, se piden al conectar; si es el puerto, se usa
  el predeterminado del protocolo.
- **Carpeta con credencial eliminada**: las conexiones que la heredaban vuelven a resolver
  hacia arriba; si ninguna carpeta ascendente define una, se pide la credencial al conectar.
- **Mover una carpeta completa**: sus conexiones descendientes recalculan lo que heredan
  contra la nueva ubicación, con la misma advertencia previa que al mover una conexión.
- **Certificado RDP no confiable**: con la validación activa, la conexión se detiene y se
  muestran el motivo y los datos del certificado. El usuario decide continuar por esta vez o
  recordar la decisión para esa conexión; nunca se continúa sin decisión explícita.
- **Fingerprint del host SSH cambiado**: se bloquea la conexión, no se envía ninguna
  credencial y se advierte explícitamente que puede tratarse de un servidor distinto. El
  usuario debe actualizar el fingerprint conocido de forma deliberada para volver a
  conectarse.
- **Clave privada inexistente o passphrase incorrecta**: se distingue un caso del otro; el
  primero identifica la ruta que falta, el segundo indica que la passphrase no desbloquea la
  clave.
- **Cierre de la aplicación con sesiones abiertas**: se advierte la cantidad de sesiones
  activas y se pide confirmación antes de cerrarlas.
- **Eliminar una conexión o una carpeta con sesiones abiertas**: se advierte y se pide
  confirmación; al confirmar, las sesiones asociadas se cierran.
- **Eliminar una carpeta con conexiones**: se informa cuántas conexiones se van a eliminar
  antes de confirmar.
- **Credencial referenciada inexistente**: la conexión sigue siendo utilizable pidiendo la
  credencial al momento de conectar.
- **Base de datos local corrupta o inaccesible**: la aplicación no se cierra en silencio;
  informa el problema, indica la ubicación del archivo afectado y permite iniciar con una
  base nueva sin destruir la anterior.
- **Nombre de conexión duplicado dentro de la misma carpeta**: se advierte al usuario; se
  permite guardar porque la conexión se identifica internamente y no por su nombre.
- **Conexión sin credencial guardada**: se pide la credencial al conectar, con la opción de
  guardarla para la próxima vez.
- **Sesión abierta cuando el equipo se suspende**: al reanudar, las sesiones caídas se
  muestran como desconectadas con la acción de reconectar disponible.
- **Servidor que no ofrece `keyboard-interactive`**: la contraseña se pide igual, en la consola
  del terminal, usando el método que el servidor sí acepte. No se corta la conexión por no haber
  podido preguntar (FR-039a).
- **Proceso que termina entre la lista de puertos y la ficha**: se informa que ya no existe y se
  ofrece refrescar; no se muestra una ficha con campos vacíos.
- **Socket sin proceso visible**: el puerto se lista igual, indicando que el proceso no es
  visible con los permisos actuales. Un puerto abierto que no aparece es peor que uno incompleto.
- **Registro sin una sola línea**: el visor dice que el registro está vacío, y lo distingue de
  «no se pudo leer». Un área en blanco se lee como un defecto de la aplicación.
- **Configuración con sintaxis que no se reconoce**: se muestra completa y sin color. El
  resaltado es una ayuda, no una condición para poder leer el archivo.

## Requirements *(mandatory)*

### Functional Requirements

#### Organización de conexiones

- **FR-001**: El sistema MUST permitir crear, renombrar y eliminar carpetas, y anidar
  carpetas dentro de otras carpetas.
- **FR-002**: El sistema MUST permitir crear una conexión indicando su protocolo (RDP o
  SSH), su nombre, su host o dirección IP, su puerto y su usuario.
- **FR-003**: El sistema MUST proponer el puerto 3389 para RDP y el 22 para SSH, y permitir
  cambiarlo.
- **FR-004**: El sistema MUST permitir editar, duplicar, eliminar y mover una conexión entre
  carpetas.
- **FR-005**: El sistema MUST permitir reordenar las conexiones dentro de una carpeta y
  conservar ese orden entre ejecuciones.
- **FR-006**: El sistema MUST permitir asociar notas de texto libre a una conexión.
- **FR-007**: El sistema MUST filtrar el árbol de servidores según un texto de búsqueda que
  se compare, sin distinguir mayúsculas ni acentos, contra el nombre, el host y el usuario
  de cada conexión.
- **FR-008**: El sistema MUST registrar la fecha y la hora de la última conexión exitosa de
  cada conexión y mostrarla al usuario.
- **FR-009**: El sistema MUST registrar un historial de los intentos de conexión con su
  resultado, y MUST permitir consultarlo.
- **FR-010**: El sistema MUST pedir confirmación antes de eliminar una carpeta que contiene
  conexiones, informando cuántas se eliminarán.
- **FR-011**: El sistema MUST conservar todas las conexiones, carpetas y su organización
  entre ejecuciones de la aplicación.

#### Sesiones RDP

- **FR-012**: El sistema MUST abrir una sesión RDP dentro de una pestaña de la ventana
  principal, tanto con doble clic sobre la conexión como desde su menú contextual.
- **FR-013**: El sistema MUST permitir configurar por conexión RDP un dominio opcional.
- **FR-014**: El sistema MUST permitir habilitar o deshabilitar el portapapeles compartido
  por conexión RDP, y MUST respetar esa configuración durante toda la sesión.
- **FR-015**: El sistema MUST permitir configurar por conexión RDP si la resolución remota
  se ajusta al tamaño de la pestaña.
- **FR-016**: El sistema MUST permitir configurar por conexión RDP si las advertencias de
  certificado del servidor se ignoran o se validan, con la validación como comportamiento
  predeterminado.
- **FR-016a**: Con la validación activa, ante un certificado no confiable el sistema MUST
  detener la conexión y mostrar el motivo del rechazo junto con los datos del certificado
  (emisor, destinatario y vigencia), ofreciendo continuar solo por esta vez o recordar la
  decisión para esa conexión. Recordar la decisión equivale a activar la opción de FR-016.
  El sistema MUST NOT continuar sin una decisión explícita del usuario.
- **FR-017**: El sistema MUST NOT exponer al servidor remoto los discos locales, el audio,
  el micrófono, las impresoras, los puertos, las cámaras ni las tarjetas inteligentes del
  equipo local, y MUST NOT ofrecer RemoteApp ni conexión a través de RDP Gateway.
- **FR-018**: El sistema MUST permitir cerrar, reconectar y poner a pantalla completa una
  sesión RDP.
- **FR-019**: El sistema MUST soportar varias sesiones RDP simultáneas e independientes.

#### Sesiones SSH y terminal

- **FR-020**: El sistema MUST abrir una sesión SSH interactiva dentro de una pestaña, con un
  terminal integrado en la aplicación.
- **FR-021**: El sistema MUST soportar autenticación SSH por contraseña y por clave privada,
  con passphrase opcional cuando la clave está cifrada.
- **FR-022**: El sistema MUST mostrar el fingerprint del host la primera vez que se conecta
  a él y MUST requerir la aceptación explícita del usuario antes de continuar.
- **FR-023**: El sistema MUST bloquear la conexión y advertir al usuario cuando el
  fingerprint presentado por el host difiere del fingerprint conocido, y MUST NOT enviar
  credenciales en ese caso.
- **FR-024**: El sistema MUST permitir configurar un intervalo de keep-alive por conexión
  SSH para sostener sesiones inactivas.
- **FR-025**: El sistema MUST usar codificación UTF-8 en las sesiones SSH.
- **FR-026**: El terminal MUST renderizar correctamente aplicaciones de pantalla completa
  basadas en secuencias de control, incluyendo al menos `vim`, `nano`, `top`, `htop`, `less`
  y `tmux`.
- **FR-026a**: El terminal MUST interpretar la designación del juego de caracteres gráficos de DEC
  —`ESC ( 0`, `ESC ( B`, y las invocaciones SO y SI— y dibujar con ella los bordes que mandan
  `dialog`, `whiptail` y cualquier programa basado en ncurses. Sin esto, un cuadro de `dialog` se
  dibuja como `lqqqk` y el borde no se distingue del texto.
- **FR-027**: El terminal MUST soportar colores ANSI y paletas de 256 colores, con un tema
  claro y uno oscuro alineados con el tema de la aplicación.
- **FR-028**: El terminal MUST mostrar el cursor y reflejar su posición y visibilidad según
  lo indique la aplicación remota.
- **FR-029**: El terminal MUST soportar caracteres Unicode, incluyendo acentos y símbolos.
- **FR-030**: El terminal MUST permitir seleccionar texto con el mouse, copiarlo al
  portapapeles y pegar texto del portapapeles en la sesión, siguiendo el comportamiento de
  **PuTTY** en gestos y atajos. PuTTY es la referencia porque es la herramienta que el
  administrador ya tiene en los dedos; cada desviación deliberada queda escrita en el
  requisito que la introduce, con su motivo.
- **FR-030a**: Al soltar el botón izquierdo tras seleccionar, el texto seleccionado MUST
  quedar en el portapapeles de Windows sin ninguna acción adicional. Una selección vacía
  (un clic sin arrastre) MUST NOT tocar el portapapeles.
- **FR-030b**: El botón derecho dentro del área de texto MUST pegar el contenido del
  portapapeles en la sesión. El botón medio MUST extender la selección existente. Es el modo
  «Compromise» de PuTTY, que es su comportamiento de fábrica.
- **FR-030c**: Los atajos de teclado MUST ser: `Ctrl+Ins` y `Ctrl+Shift+C` para copiar,
  `Shift+Ins` y `Ctrl+Shift+V` para pegar. `Ctrl+C` MUST enviar siempre la interrupción al
  servidor remoto y MUST NOT copiar, aunque haya una selección activa. Motivo: con el
  copiado automático de FR-030a casi siempre hay selección viva; un `Ctrl+C` que copiara en
  lugar de interrumpir dejaría sin manera de cortar un comando corriendo.
- **FR-030d**: El menú contextual del terminal MUST abrirse con `Ctrl`+clic derecho, y MUST
  seguir mostrando al lado de cada acción su atajo. Motivo: el menú es lo único que enseña
  que los atajos existen; que el botón derecho pase a pegar no puede costar ese
  descubrimiento.
- **FR-030e**: El terminal MUST soportar `bracketed paste` (modo 2004): cuando la aplicación
  remota lo pide, el texto pegado MUST ir envuelto en las marcas de inicio y fin. Motivo: sin
  esto, pegar varias líneas en `vim` o en un shell con autoindentado sale escalonado, y el
  shell ejecuta cada línea a medida que llega.
- **FR-030f**: Cuando el texto a pegar contiene más de una línea y la aplicación remota
  **no** pidió `bracketed paste`, el sistema MUST pedir confirmación mostrando cuántas líneas
  son. Motivo: sin las marcas, cada salto de línea es una orden ejecutada; es una desviación
  deliberada respecto de PuTTY y el único punto donde el estándar se corrige.
- **FR-030g**: Al pegar, los finales de línea `CRLF` y `LF` MUST normalizarse a `CR`, que es
  lo que produce la tecla Enter en una sesión interactiva.
- **FR-031**: El terminal MUST mantener un historial de desplazamiento hacia atrás
  (scrollback) con un límite configurable de líneas.
- **FR-032**: El terminal MUST transmitir a la sesión remota las teclas modificadoras
  `Ctrl`, `Alt` y `Shift`, las teclas de función y las teclas de navegación, con la única
  excepción de las combinaciones que la aplicación se reserva. Esa lista MUST ser cerrada y
  MUST estar documentada: `F12`, `Ctrl+F`, `Ctrl+Ins`, `Shift+Ins`, `Ctrl+Shift+C`,
  `Ctrl+Shift+V`, `Ctrl+Shift+A`, `Ctrl+Shift+P`, `Shift+RePag`, `Shift+AvPag`,
  `Ctrl+RePag`, `Ctrl+AvPag`, `Ctrl+Shift+RePag`, `Ctrl+Shift+AvPag`, `Ctrl+Shift+Inicio`,
  `Ctrl+Shift+Fin` y las de zoom (`Ctrl` con rueda, más, menos y cero). Todo lo demás va al
  servidor.
- **FR-033**: El sistema MUST comunicar al servidor remoto el nuevo tamaño del terminal cada
  vez que la pestaña cambia de dimensiones.
- **FR-034**: El sistema MUST soportar varias sesiones SSH simultáneas e independientes.

#### Credenciales

- **FR-035**: El sistema MUST almacenar las contraseñas, las passphrases y el material de
  clave privada exclusivamente en el almacén de credenciales del sistema operativo, y MUST
  guardar en su base local únicamente una referencia opaca a esa credencial.
- **FR-036**: El sistema MUST almacenar, para una conexión RDP, usuario, dominio y
  contraseña; para una conexión SSH con contraseña, usuario y contraseña; y para una
  conexión SSH con clave, usuario, ruta de la clave y passphrase opcional.
- **FR-037**: El sistema MUST permitir actualizar y eliminar la credencial asociada a una
  conexión sin eliminar la conexión.
- **FR-038**: El sistema MUST eliminar del almacén de credenciales la credencial asociada a
  una conexión cuando esa conexión se elimina.
- **FR-039**: El sistema MUST pedir la credencial al momento de conectar cuando no hay una
  guardada o la guardada ya no existe, ofreciendo guardarla.
- **FR-039a**: Para una conexión SSH por contraseña sin credencial guardada, el pedido MUST
  hacerse en la consola del propio terminal, sin eco, como PuTTY, y MUST funcionar **cualquiera
  sea el método de autenticación que ofrezca el servidor**: tanto `keyboard-interactive` como
  `password` a secas. El sistema MUST NOT fallar la conexión por no haber podido preguntar.
  Motivo: hay servidores —bastantes— configurados con `KbdInteractiveAuthentication no`. Contra
  ellos, un cliente que sólo sepa preguntar por `keyboard-interactive` no pregunta nada: corta
  con «no hay método de autenticación disponible», que no nombra la causa real y no le deja al
  usuario ninguna salida.
- **FR-039b**: Tras tres contraseñas rechazadas seguidas, el sistema MUST cortar el intento e
  informarlo, en lugar de seguir preguntando. Motivo: muchos servidores cierran la conexión por
  su cuenta y el usuario queda mirando un pedido que ya no lleva a ningún lado.
- **FR-040**: El sistema MUST NOT registrar en sus archivos de log contraseñas, passphrases,
  contenido de claves privadas, el texto tecleado en sesiones SSH, el contenido de pantalla
  de sesiones RDP ni el contenido del portapapeles.

#### Ventana, pestañas y estado

- **FR-041**: La ventana principal MUST presentar un buscador y una acción de nueva conexión
  en la parte superior, el árbol de carpetas y servidores a la izquierda, las pestañas de
  sesiones a la derecha y una barra de estado en la parte inferior.
- **FR-041a**: El título de la ventana MUST decir `CMC` cuando no hay sesiones, y
  `CMC - <conexión activa>` cuando hay una. MUST agregar entre paréntesis el estado de esa
  sesión cuando no está conectada, y la cantidad de sesiones cuando hay más de una. Motivo:
  minimizada, el título es lo único que se ve de la aplicación; con un título fijo, ocho
  ventanas de sesiones distintas se ven iguales en la barra de tareas.
- **FR-041b**: El icono de la aplicación MUST estar dibujado a medida para cada tamaño del
  archivo `.ico` —de 16 a 256 px— y MUST NOT obtenerse reescalando una sola imagen grande. En
  las medidas chicas el trazo MUST caer sobre píxeles enteros. Motivo: un trazo de 1,25 px no
  existe, el suavizado lo reparte entre dos columnas y lo que queda en la barra de tareas es un
  cuadrado oscuro que parece vacío.
- **FR-042**: La barra de estado MUST mostrar el estado de la sesión activa junto con su
  usuario y su host.
- **FR-043**: Cada pestaña MUST mostrar el nombre de su conexión y su estado actual
  (conectando, conectado, desconectado o error).
- **FR-044**: El sistema MUST permitir abrir varias pestañas, alternar entre ellas y cerrar
  una pestaña cerrando su sesión.
- **FR-044a**: Al abrir con doble clic una conexión que ya tiene una sesión abierta, el
  sistema MUST llevar el foco a la pestaña existente en lugar de crear una segunda sesión.
  Abrir una sesión adicional de la misma conexión MUST estar disponible como acción explícita
  del menú contextual.
- **FR-045**: El sistema MUST permitir reconectar una sesión caída reutilizando los
  parámetros y las credenciales de su conexión.
- **FR-046**: El sistema MUST permitir poner la sesión activa a pantalla completa y volver
  al modo normal conservando el estado de la conexión.
- **FR-047**: El sistema MUST recordar el tamaño, la posición y el estado maximizado de la
  ventana, así como el tema seleccionado, entre ejecuciones.
- **FR-048**: El sistema MUST advertir y pedir confirmación antes de cerrar la aplicación
  cuando hay sesiones activas, informando cuántas son.
- **FR-049**: El sistema MUST advertir y pedir confirmación antes de eliminar una conexión o
  una carpeta que tiene sesiones activas.

#### Errores y resiliencia

- **FR-050**: Ante cualquier fallo de conexión, el sistema MUST mostrar un mensaje que
  identifique la causa en lenguaje entendible y proponga una acción, sin exponer códigos de
  error crudos como único contenido.
- **FR-051**: El sistema MUST distinguir entre host inalcanzable, credenciales rechazadas,
  tiempo de espera agotado y desconexión posterior a la conexión.
- **FR-052**: El sistema MUST informar al usuario cuando su base de datos local está
  corrupta o es inaccesible, indicar el archivo afectado y permitir continuar con una base
  nueva sin destruir la anterior.
- **FR-053**: El sistema MUST permitir advertir sobre un nombre de conexión duplicado dentro
  de una misma carpeta sin impedir guardarlo.
- **FR-054**: Un fallo en una sesión MUST NOT afectar a las demás sesiones abiertas ni
  cerrar la aplicación.

#### Distribución y funcionamiento

- **FR-055**: El sistema MUST funcionar sin privilegios de administrador y sin instalar
  servicios en el equipo.
- **FR-056**: El sistema MUST guardar su base de datos, su configuración y sus registros en
  el perfil local del usuario de Windows.
- **FR-057**: El sistema MUST registrar en sus archivos de log el inicio y el cierre de la
  aplicación, la apertura de una conexión, su resultado, las desconexiones, los errores
  técnicos y las migraciones de la base de datos, siempre respetando FR-040.
- **FR-057a**: El sistema MUST rotar los archivos de registro por día y conservarlos 30 días,
  eliminando automáticamente los más antiguos.

#### Herencia desde la carpeta

- **FR-058**: El sistema MUST permitir definir en una carpeta una credencial y valores de
  configuración que sus conexiones descendientes hereden: usuario, dominio, puerto y los
  ajustes específicos de cada protocolo (para SSH, tipo de autenticación, ruta de clave
  privada y keep-alive; para RDP, portapapeles, ajuste a la pestaña y política de
  certificado).
- **FR-059**: Cada campo heredable de una conexión MUST poder marcarse como heredado o como
  propio de forma explícita e independiente del resto. El valor predeterminado al crear una
  conexión dentro de una carpeta MUST ser heredar.
- **FR-060**: El sistema MUST resolver el valor efectivo de un campo heredado recorriendo el
  árbol hacia arriba —conexión, carpeta contenedora, carpeta padre, hasta la raíz— y tomando
  el primer valor definido. Si ninguno lo define, el campo queda sin valor y se aplica el
  comportamiento previsto para un campo vacío.
- **FR-061**: El sistema MUST mostrar, junto a cada campo marcado como heredado, el valor
  efectivo que está heredando y de qué carpeta proviene.
- **FR-062**: Al mover una conexión a otra carpeta, el sistema MUST recalcular sus valores
  heredados contra la nueva ubicación, y MUST advertir en la confirmación del movimiento si
  eso cambia algún valor efectivo o la credencial que se usará.
- **FR-063**: Al eliminar o modificar la configuración de una carpeta, el sistema MUST
  advertir cuántas conexiones descendientes cambian su valor efectivo como consecuencia.
- **FR-064**: La credencial de una carpeta MUST almacenarse con las mismas garantías que la
  de una conexión: el secreto vive en el almacén de credenciales del sistema operativo y la
  base local guarda únicamente una referencia opaca con el formato
  `cmc:folder:<identificador de la carpeta>:<rdp|ssh>`.
- **FR-064a**: Una carpeta MUST poder definir una credencial heredable **por protocolo**, y
  cada conexión MUST heredar la de su propio protocolo. Una carpeta que contiene conexiones
  RDP y SSH mezcladas puede definir ambas, o sólo una. Motivo: RDP almacena además el dominio,
  y el usuario de un Windows de dominio rara vez coincide con el de un servidor Linux.

#### Apariencia

- **FR-065**: La ventana principal MUST aplicar el la tarjeta del layout inset de Windows 11 como fondo,
  esquinas redondeadas y barra de título coherente con el tema claro u oscuro activo.
- **FR-066**: El sistema MUST usar el color de acento configurado en Windows para los
  elementos de selección, foco y estado activo.
- **FR-067**: El sistema MUST usar la tipografía del sistema de Windows 11 para la interfaz,
  respetando el escalado por DPI de cada monitor.
- **FR-068**: El árbol de servidores, la tira de pestañas, los botones, los campos de texto y
  los menús contextuales MUST dibujarse con la estética de shadcn/ui, incluyendo estados de reposo,
  apuntado, presionado, foco y deshabilitado.
- **FR-069**: La tira de pestañas MUST ser un control propio al estilo de Windows Terminal,
  con esquina superior redondeada, botón de cierre por pestaña, indicador del estado de
  conexión y acción de nueva pestaña.
- **FR-070**: Todos los diálogos de la aplicación MUST ser formularios propios con la misma
  estética; el sistema MUST NOT usar `MessageBox` del sistema, que no respeta el tema oscuro.
- **FR-070a**: Una sesión SSH MUST presentar el terminal siempre visible, con los paneles de
  archivos, estado, Docker, nginx y supervisord desplegables a los costados, sin ocultarlo.

#### Transferencia de archivos (SFTP)

- **FR-071**: El sistema MUST ofrecer, sobre una conexión SSH, un explorador de archivos
  remoto que permita navegar el árbol de directorios y ver nombre, tamaño y fecha de
  modificación de cada entrada.
- **FR-072**: El sistema MUST establecer la conexión de archivos al abrir el panel,
  reutilizando la credencial, el usuario, el puerto y el fingerprint aceptado de la conexión
  SSH, sin volver a pedirlos, y MUST cerrarla al cerrar el panel.
- **FR-073**: El sistema MUST permitir enviar y traer archivos y carpetas en ambos sentidos,
  mostrando el progreso de cada transferencia y permitiendo cancelarla.
- **FR-074**: El sistema MUST permitir crear carpetas, renombrar y eliminar entradas remotas,
  pidiendo confirmación antes de eliminar.
- **FR-075**: Ante el fallo de una transferencia, el sistema MUST informar qué archivo falló
  y por qué, y MUST continuar con el resto de la cola.
- **FR-076**: El fallo del panel de archivos MUST NOT afectar a la sesión de terminal de la
  misma conexión.
- **FR-077**: El sistema MUST NOT registrar en los logs rutas, nombres de archivo ni
  contenido de los archivos transferidos.
- **FR-078**: El sistema MUST NOT ofrecer cambio de permisos o de dueño, enlaces simbólicos
  ni edición remota de archivos.

#### Panel de estado del servidor Linux

- **FR-079**: El sistema MUST mostrar, para una conexión SSH a un servidor Linux, un panel
  con uso de CPU, memoria total/usada/disponible, carga del sistema, tiempo encendido, uso de
  discos y tráfico de red.
- **FR-080**: El sistema MUST obtener las métricas sin instalar ningún agente ni servicio en
  el servidor, leyendo `/proc`, `df` y `uname` mediante una conexión SSH auxiliar,
  independiente de la del terminal y de la de archivos.
- **FR-081**: El sistema MUST calcular el uso de CPU por diferencia entre dos lecturas de
  `/proc/stat`, y la memoria usada como `MemTotal` menos `MemAvailable`. El sistema MUST NOT
  interpretar la salida de `top` ni de `free`, cuyo formato varía según distribución, versión
  e idioma.
- **FR-082**: El sistema MUST calcular el tráfico de red por diferencia entre dos lecturas de
  `/proc/net/dev`, y MUST consultar los discos con un formato de salida estable y sin
  traducir.
- **FR-083**: El sistema MUST excluir del panel de discos los sistemas de archivos virtuales
  (`tmpfs`, `devtmpfs`, `overlay`, `squashfs`, `proc`, `sysfs`) y los montajes repetidos que
  genera Docker, y MUST excluir de la red la interfaz de bucle local y las interfaces
  virtuales sin tráfico. El usuario MUST poder elegir qué interfaces se muestran.
- **FR-084**: El sistema MUST muestrear cada 5 segundos mientras el panel está visible,
  MUST detener el muestreo y cerrar la conexión auxiliar cuando deja de estarlo, y MUST
  cancelar una consulta pendiente antes de lanzar la siguiente.
- **FR-085**: El sistema MUST conservar en memoria los últimos 60 puntos de CPU, memoria y
  red, equivalentes a 5 minutos, y MUST NOT persistir métricas en la base de datos.
- **FR-086**: El sistema MUST detectar si el host es Linux y MUST NOT ofrecer el panel de
  estado cuando no lo sea.
- **FR-087**: El sistema MUST mostrar además hostname, distribución, versión del kernel,
  fecha del servidor, usuarios conectados, cantidad de procesos y servicios fallidos cuando
  el servidor los exponga.
- **FR-087a**: Cada medida con techo conocido —CPU, memoria, intercambio y cada disco— MUST
  mostrarse con una barra o un arco de progreso y con un color por tramo: normal hasta 74 %,
  advertencia de 75 a 89 %, crítico de 90 % en adelante. Los tramos MUST ser los mismos en todo
  el panel: un disco al 80 % y una CPU al 80 % no pueden verse distintos.
- **FR-087b**: El tramo MUST distinguirse además del color, con texto o forma, por la misma
  razón que FR-100d: quien no distingue bien los colores tiene que poder leer el estado. El
  color MUST mantener contraste legible en el tema claro y en el oscuro.
- **FR-087c**: La carga promedio MUST compararse contra la cantidad de núcleos del servidor y
  no contra un número fijo. Motivo: una carga de 4 es normal en una máquina de ocho núcleos y
  es una máquina ahogada en una de dos; pintar de rojo un número sin dividirlo por los núcleos
  es un semáforo que miente.
- **FR-087d**: Las medidas sin techo conocido —tráfico de red, cantidad de procesos, uptime—
  MUST NOT mostrarse con barra de progreso ni con color de estado. Motivo: un porcentaje exige
  un máximo, y el de la red no existe; una barra inventada sobre un máximo inventado es peor
  que un número.

#### Túneles SSH

- **FR-088**: El sistema MUST permitir definir, por conexión SSH, uno o más túneles de
  reenvío de puerto local, indicando puerto local, host remoto y puerto remoto, con un nombre
  descriptivo.
- **FR-089**: El sistema MUST persistir las definiciones de túnel junto a su conexión.
- **FR-090**: El sistema MUST permitir levantar y detener cada túnel a pedido, y MUST mostrar
  cuáles están activos.
- **FR-091**: El sistema MUST permitir marcar un túnel para que se levante automáticamente al
  conectar la sesión SSH.
- **FR-092**: El sistema MUST liberar los puertos locales al detener un túnel o al cerrar la
  sesión SSH que lo sostiene.
- **FR-093**: Si el puerto local ya está ocupado, el sistema MUST informarlo indicando el
  puerto y MUST NOT dejar el túnel en un estado indeterminado.

#### Inventario de Docker

- **FR-094**: El sistema MUST listar, para una conexión SSH a un servidor con Docker, los
  contenedores con nombre, imagen, estado, puertos publicados y tiempo de ejecución.
- **FR-095**: El sistema MUST consultar Docker por línea de comandos sobre la conexión SSH,
  usando un formato de salida estable; MUST reintentar con `sudo` cuando el usuario remoto no
  pertenezca al grupo `docker`; y MUST informar la falta de permisos con claridad si tampoco
  así puede consultarlo.
- **FR-096**: Cuando exista un túnel disponible hacia la API de Docker, el sistema MUST
  preferir la API por sobre la interpretación de texto de la línea de comandos.
- **FR-097**: El sistema MUST detectar los archivos `docker-compose` presentes en el servidor
  e informar su ubicación y los servicios que define cada uno.
- **FR-098**: El sistema MUST relacionar cada servicio definido en un compose con su
  contenedor, indicando cuáles están corriendo y cuáles no.
- **FR-099**: El sistema MUST detectar si el servidor tiene Docker y MUST NOT ofrecer el
  panel cuando no lo tenga.
- **FR-100**: El panel de Docker MUST ser de sólo lectura. Toda acción que modifique el estado
  del servidor MUST exigir confirmación explícita, MUST nombrar el objeto sobre el que actúa y
  MUST vivir fuera de `PlatformInventory`, cuya garantía de sólo lectura es la ausencia de
  métodos de escritura.
- **FR-100a**: El panel de supervisord MUST permitir iniciar, detener y reiniciar un proceso
  desde el menú contextual del elemento, con confirmación explícita previa (FR-100). Después de
  cada acción MUST volver a consultar el estado en lugar de asumir el resultado.
- **FR-100b**: El sistema MUST permitir ver las últimas líneas del registro de un proceso de
  supervisord sin salir de la aplicación. Es una lectura y no requiere confirmación.
- **FR-100c**: El nombre de proceso que se envía al servidor MUST validarse contra un conjunto
  cerrado de caracteres antes de incorporarse a una línea de comando. El nombre proviene de la
  salida de un comando remoto y el comando se ejecuta con sudo: sin validación, un nombre con
  sintaxis de shell sería ejecución arbitraria como root.
- **FR-100d**: El estado de cada proceso de supervisord MUST distinguirse en tres niveles
  —corriendo, advertencia y falla— con icono además de color. Un proceso detenido a propósito no
  puede verse igual que uno que se rindió, y quien no distingue bien los colores tiene que poder
  leer el estado.
- **FR-100e**: El visor de registros MUST interpretar las secuencias de color ANSI que traiga el
  registro y mostrarlas como color, en lugar de mostrarlas como texto. Motivo: supervisord y
  Docker entregan tal cual lo que el proceso escribió, y muchos programas colorean su salida; hoy
  esos códigos se ven como basura entre las palabras.
- **FR-100f**: Cuando el registro **no** trae color propio, el visor MUST clasificar cada línea
  por nivel a partir de su texto —error, advertencia, resto— y colorear en consecuencia,
  distinguiendo el nivel también sin color, como exige FR-100d. La clasificación MUST NOT
  modificar el texto mostrado.
- **FR-100g**: El visor de registros MUST ser de sólo lectura y MUST permitir copiar y buscar.
  El coloreado MUST NOT alterar lo que se copia: lo que va al portapapeles es el texto tal como
  lo entregó el servidor.

#### Inventario de nginx y supervisord

- **FR-101**: El sistema MUST listar los sitios habilitados de nginx con sus nombres de
  servidor, puertos en escucha y raíz de documentos, y MUST permitir ver la configuración
  efectiva de un sitio en modo lectura.
- **FR-101a**: La configuración efectiva MUST mostrarse **completa**, sin recortes ni elipsis, y
  con resaltado de sintaxis que distinga al menos: directivas, nombres de bloque (`server`,
  `location`, `upstream`), cadenas entre comillas, números y unidades, variables (`$host`) y
  comentarios. Los comentarios MUST quedar visiblemente atenuados respecto del resto.
- **FR-101b**: El resaltado MUST conservar el texto exacto: no reordena, no reindenta y no
  corrige nada. Lo que se copia es el archivo tal como lo devolvió el servidor. Motivo: esta
  vista se usa para diagnosticar, y una vista que "mejora" lo que muestra deja de servir para
  eso.
- **FR-101c**: Un archivo cuya sintaxis no se reconozca MUST mostrarse igual, sin color, y no
  vacío ni con error. El resaltado es una ayuda, no una condición para leer.
- **FR-102**: El sistema MUST listar los procesos administrados por supervisord con su estado
  y su tiempo de ejecución, destacando los que hayan fallado.
- **FR-103**: El sistema MUST detectar si el servidor tiene nginx o supervisord y MUST NOT
  ofrecer el panel correspondiente cuando no los tenga.
- **FR-104**: Cuando el usuario remoto no pueda leer un archivo de configuración, el sistema
  MUST informar qué no pudo leerse en lugar de mostrar un inventario incompleto en silencio.
- **FR-105**: El sistema MUST NOT registrar en los logs el contenido de los archivos de
  configuración leídos ni la salida de los comandos de inventario.

#### Comportamiento transversal de los paneles remotos

- **FR-106**: Antes de transferir un archivo cuyo nombre ya existe en el destino, el sistema
  MUST pedir una decisión al usuario, ofreciendo sobrescribir, omitir o conservar ambos
  renombrando el entrante, con la opción de aplicar la misma decisión al resto de la cola.
  El sistema MUST NOT sobrescribir ningún archivo sin decisión explícita.
- **FR-107**: El inventario de Docker, nginx y supervisord MUST consultarse al abrir su panel
  y, después, únicamente cuando el usuario lo solicite mediante una acción de refrescar. El
  sistema MUST NOT consultarlo de forma periódica: son datos que cambian de a minutos u horas
  y cada consulta ejecuta comandos en el servidor.
- **FR-108**: Cuando una sesión SSH se desconecta, el sistema MUST cerrar las conexiones
  auxiliares que dependen de ella —archivos, métricas e inventario— y dejar sus paneles
  inactivos hasta que el usuario reconecte. El sistema MUST NOT dejar conexiones auxiliares
  abiertas en el servidor tras la caída de su sesión principal.
- **FR-109**: El sistema MUST contar los túneles activos junto con las sesiones en la
  advertencia previa al cierre de la aplicación, y MUST liberar sus puertos locales al cerrar.
- **FR-110**: Toda consulta remota de métricas o de inventario MUST tener un tiempo límite:
  3 segundos para una muestra de métricas y 10 segundos para una consulta de inventario. Al
  vencer, la consulta se cancela y se informa, sin encolar consultas ni congelar el panel.
- **FR-111**: Toda función de la aplicación MUST ser accesible con el teclado: recorrido con
  `Tab`, navegación del árbol y de las pestañas con las flechas, activación con `Enter`, y
  atajos para las acciones frecuentes —buscar, nueva conexión, cerrar pestaña, alternar entre
  pestañas y pantalla completa—. Los controles propios MUST mostrar un indicador de foco
  visible. El soporte de lectores de pantalla no forma parte de esta versión.
- **FR-112**: La aplicación MUST admitir una sola instancia por usuario de Windows. Al
  intentar abrir una segunda, el sistema MUST traer al frente la ventana existente y terminar
  el proceso nuevo, sin abrir la base de datos.
#### Entradas web

- **FR-114**: El sistema MUST admitir un tercer tipo de conexión, `Web`, que se crea, edita,
  duplica, mueve, ordena, busca y elimina como cualquier otra, y que guarda una dirección URL.
- **FR-115**: Al abrir una entrada web, el sistema MUST abrir su URL en un navegador del
  sistema operativo, como proceso externo. El navegador MUST ser configurable por entrada:
  el predeterminado del sistema es el valor por omisión, y el usuario MUST poder elegir en su
  lugar un navegador concreto instalado en el equipo.
- **FR-115a**: El sistema MUST permitir marcar una entrada web para que se abra en una
  ventana privada o de incógnito. Motivo: permite tener abiertos a la vez dos paneles del
  mismo servicio con usuarios distintos, que es un caso habitual al administrar.
- **FR-116**: El sistema MUST NOT incorporar ningún navegador ni motor web dentro de la
  aplicación, y en consecuencia MUST NOT autocompletar formularios de inicio de sesión.
- **FR-117**: El sistema MUST permitir copiar al portapapeles, por separado, el usuario y la
  contraseña de una entrada web.
- **FR-118**: La credencial de una entrada web MUST almacenarse en el almacén de credenciales
  del sistema operativo con la clave `cmc:web:<identificador de la conexión>`, con las mismas
  garantías que las demás.
- **FR-119**: Una entrada web MUST NOT abrir una pestaña de sesión: no tiene sesión que
  administrar. Su apertura es una acción puntual.
- **FR-120**: Una carpeta MUST poder definir una credencial web heredable, con la clave
  `cmc:folder:<identificador>:web`, que hereden las entradas web que contiene.

#### Copiado rápido desde el árbol

- **FR-121**: El menú contextual de cualquier conexión del árbol MUST ofrecer copiar al
  portapapeles, por separado y en texto plano: el host o dirección IP, el usuario efectivo y
  la contraseña. Para una entrada web, el host se reemplaza por la URL.
- **FR-122**: El usuario y la contraseña que se copian MUST ser los **efectivos**, resueltos
  por la cascada de herencia, no sólo los propios de la conexión.
- **FR-123**: Tras copiar una contraseña, el sistema MUST vaciar el portapapeles a los 30
  segundos, siempre que su contenido siga siendo el copiado. El sistema MUST indicar al
  usuario que la contraseña se copió y que se borrará. Motivo: el portapapeles es legible por
  cualquier proceso del equipo; el copiado es deliberado, dejarlo ahí para siempre no.
- **FR-124**: El sistema MUST NOT registrar en los logs qué se copió ni su contenido, ni
  siquiera el hecho de haber copiado una contraseña.

- **FR-113**: Al editar una conexión con una sesión abierta, el sistema MUST conservar la
  sesión con los valores con los que se estableció y MUST indicar en su pestaña que hay
  cambios pendientes de aplicarse en la próxima conexión. El sistema MUST NOT reconectar por
  su cuenta.

#### Conexiones hijas, color y catálogo (migración 2)

- **FR-125**: El sistema MUST permitir que una conexión cuelgue de otra, para representar los
  servicios que corren en un servidor —por ejemplo un panel HTTP en otro puerto—. La hija MUST
  mostrarse anidada bajo su padre en el árbol.
- **FR-126**: Una conexión hija MUST heredar de su padre el host, el usuario, el dominio y la
  credencial, con la misma cascada y el mismo significado de `NULL` que la herencia desde una
  carpeta. El sistema MUST NOT heredar el protocolo ni el puerto: son siempre propios de la
  hija.
- **FR-127**: El sistema MUST admitir **un solo nivel** de anidamiento entre conexiones: una
  conexión que ya tiene padre MUST NOT poder ser padre de otra. Esto hace imposible un ciclo
  por construcción.
- **FR-128**: Al eliminar una conexión que tiene hijas, el sistema MUST informar cuántas se
  van a eliminar con ella y MUST eliminarlas sólo tras la confirmación del usuario.
- **FR-129**: El sistema MUST permitir etiquetar carpetas y conexiones con texto libre
  separado por comas, ofreciendo autocompletado con las etiquetas ya en uso, y MUST incluir
  las etiquetas en la búsqueda.
- **FR-130**: El sistema MUST permitir marcar el entorno de una conexión —producción,
  preproducción, desarrollo o laboratorio—, heredable desde la carpeta igual que el resto de
  la configuración. El entorno MUST distinguirse visualmente en la fila del árbol y en la
  barra de la sesión abierta. El sistema MUST NOT pedir una confirmación adicional al conectar
  por el entorno: un aviso que aparece siempre se acepta sin leer y deja de proteger.
- **FR-131**: El sistema MUST permitir una descripción corta por carpeta y por conexión,
  visible en el árbol, distinta del campo de notas ya existente (FR-005), que es texto largo.
- **FR-132**: El sistema MUST permitir marcar conexiones como favoritas y filtrar por ellas.
- **FR-133**: El sistema MUST poder guardar datos adicionales por conexión, con nombre y
  valor definidos por quien los use, de modo que incorporar un dato nuevo no obligue a
  cambiar la estructura de almacenamiento. En esta versión el sistema MUST NOT ofrecer
  interfaz para editarlos.
- **FR-134**: El sistema MUST permitir asociar a una conexión una dirección de documentación
  y abrirla en el navegador del sistema.
- **FR-135**: El árbol MUST mostrar un icono propio por protocolo —RDP, SSH y web— y otro para
  las carpetas, con una forma distinta por protocolo y no sólo un color distinto, para que
  quien no distingue bien los colores igual pueda separarlos. El color del icono MUST ser
  configurable desde una paleta cerrada y MUST resolverse en **dos escalones**: color propio
  del elemento y, si no lo define, color global de su protocolo
  (`src/CafManagerConection.Infrastructure/Database/SettingsStore.cs:149`). El color MUST NOT
  tomarse de la carpeta: se elige para distinguir un elemento de sus hermanos, y heredarlo los
  vuelve idénticos. FR-195b de la feature 002 lo confirma.

- **FR-136**: Al abrir un panel del servidor, el sistema MUST mostrar la barra lateral de
  inmediato con un indicador de carga, y reemplazarlo por los datos cuando lleguen. MUST NOT
  esperar la respuesta del servidor antes de abrirla.
- **FR-137**: Mientras una consulta o una acción de un panel está en curso, el sistema MUST
  impedir que se dispare otra vez desde la interfaz. Vale tanto para el botón de refresco como
  para el doble clic y el menú contextual de la tabla.
- **FR-138**: El sistema MUST ofrecer una consola de traza, abrible y cerrable con **F12**,
  que muestre cada intercambio del canal auxiliar de comandos: momento, servidor, tipo,
  duración, estado de salida, lo enviado y lo recibido. MUST permitir filtrar, pausar la
  grabación, limpiar lo grabado y copiar lo que se esté viendo.
- **FR-138a**: La traza MUST vivir únicamente en memoria, en un búfer acotado, y MUST NOT
  escribirse en el registro de la aplicación ni en la base: contiene la salida de comandos
  remotos, que el Principio II clasifica como contenido de sesión.
- **FR-138b**: La traza MUST NOT incluir lo que el usuario teclea en el terminal ni lo que el
  servidor le contesta por él, y MUST NOT exponer una contraseña: la escalada a sudo se anota
  describiendo que la contraseña se escribió en la entrada estándar, sin su valor.
- **FR-139**: Una sesión SSH MUST ofrecer una barra de acciones **arriba** del terminal, del
  ancho de su contenedor y siempre visible, con copiar toda la sesión al portapapeles, guardarla
  en un archivo, borrar el historial de desplazamiento, restablecer el terminal y elevar a root.
  Las acciones MUST mostrarse como iconos, con su nombre en el globo de ayuda.
- **FR-139a**: La barra MUST tener aspecto flotante sobre el marco del terminal —relleno un tono
  más claro, borde, esquinas redondeadas y margen a los cuatro lados— y MUST NOT taparle
  contenido: va sobre el marco, no sobre el área de texto.
- **FR-139b**: Al ejecutar una acción de la barra, el foco MUST volver al terminal, para poder
  seguir tecleando sin un clic intermedio.
- **FR-139c**: La barra MUST incluir reconectar: cierra la conexión con el servidor y la vuelve a
  abrir **sin cerrar la pestaña**. MUST pedir confirmación antes, porque corta lo que esté
  corriendo del otro lado. Al reconectar, el sistema MUST descartar el canal de comandos, el
  inventario y los paneles ya construidos —apuntan a una conexión que ya no existe— y volver a
  detectar qué admite el servidor; MUST conservar la decisión sobre la clave del host, para no
  volver a preguntar la huella del mismo servidor.
- **FR-141**: La barra lateral de accesos a los paneles MUST tener el mismo aspecto flotante que
  la de acciones, con su propio relleno —distinto del fondo que tiene detrás— y el borde recorriendo
  el alto completo de la sesión. Sus colores MUST salir de la paleta del tema, porque se apoya
  sobre el fondo de la aplicación y no sobre el marco del terminal.
- **FR-142**: El terminal MUST mostrar una barra de desplazamiento vertical cuando haya historial,
  y MUST NOT mostrarla cuando no lo haya. El pulgar MUST reflejar tanto la posición como la
  proporción de lo visible sobre el total, y MUST poder arrastrarse para desplazar la vista.
- **FR-140**: El contador de transferencia de la barra inferior MUST indicar qué mide: es el
  acumulado del terminal de la sesión que se está mirando, y el tráfico del canal de comandos
  se cuenta aparte, en la consola de traza.

- **FR-143**: El sistema MUST poder abrir una conexión SSH en PuTTY, FileZilla o WinSCP, como
  **proceso externo** en su propia ventana, desde el menú contextual de la conexión y desde la
  barra de acciones de la sesión. MUST NOT alojar la ventana de esas herramientas.
- **FR-143a**: El sistema MUST detectar qué herramientas están instaladas **una sola vez por
  arranque y en segundo plano**, y MUST NOT hacerlo en el camino de la carga de la ventana. Una
  herramienta que no esté instalada MUST NOT aparecer en el menú ni en la barra.
- **FR-143b**: El sistema MUST pasar a la herramienta externa únicamente host, usuario, puerto y
  —si la hay— la ruta de la clave privada. MUST NOT pasarle la contraseña por ningún medio:
  ni en la línea de comandos, ni en un archivo, ni escribiendo en sus sesiones guardadas.
- **FR-144**: El terminal MUST permitir buscar texto en su historial, resaltar las coincidencias e
  ir a la anterior y la siguiente.
- **FR-145**: El sistema MUST permitir cambiar el tamaño de la letra del terminal de una sesión
  abierta, e informar el tamaño nuevo de la rejilla al servidor.
- **FR-146**: El sistema MUST permitir abrir una segunda sesión sobre la misma conexión desde la
  pestaña y desde el árbol.
- **FR-147**: El sistema MUST ofrecer una paleta de comandos guardados: crear comandos con nombre,
  elegir uno, editarlo antes de enviarlo y enviarlo a la sesión abierta. MUST distinguir enviar de
  sólo escribir sin ejecutar. MUST NOT ejecutar nada por su cuenta ni enviar a varias sesiones a
  la vez.
- **FR-148**: Cuando una conexión SSH falle por no acordar algoritmos con el servidor, el mensaje
  MUST decir qué faltó acordar, no sólo que no se pudo conectar.
- **FR-149**: El sistema MUST permitir conectar a `usuario@host:puerto` sin crear una entrada en el
  árbol. La credencial MUST NOT persistirse salvo que el usuario guarde la conexión.

- **FR-150**: El panel de Docker MUST permitir iniciar, detener y reiniciar un contenedor desde su
  menú contextual, con confirmación previa que nombre el contenedor y el servidor. MUST NOT
  ofrecer acciones sobre la cabecera de un proyecto, que no es un contenedor.
- **FR-150a**: El doble clic sobre un contenedor MUST abrir su ficha, con estado, salud, reinicios
  acumulados, tiempo arriba, consumo de CPU y memoria, red, disco, política de reinicio, puertos
  publicados, volúmenes y las últimas líneas de su registro. La ficha MUST ofrecer las mismas
  acciones de escritura.
- **FR-150b**: Los datos de la ficha MUST presentarse agrupados en secciones con título
  —identidad, estado, recursos, red, almacenamiento— y no como una lista corrida de pares. Dentro
  de cada sección, los valores que tienen estado —salud, política de reinicio, estado del
  contenedor— MUST distinguirse con color y forma, con los mismos tres niveles de FR-100d.
- **FR-150c**: La ficha MUST incluir además: identificador corto, imagen con su etiqueta, digest
  corto de la imagen, fecha de creación, comando y argumentos con que arrancó, directorio de
  trabajo, redes a las que está conectado con su dirección IP, y el `docker-compose` y el
  servicio al que pertenece cuando corresponda.
- **FR-150d**: La ficha MUST NOT mostrar las variables de entorno del contenedor. Motivo: es
  donde viven las contraseñas de base de datos, las claves de API y los tokens en la enorme
  mayoría de los despliegues. Mostrarlas las pone en pantalla, en una captura y en el
  portapapeles, y eso es exactamente lo que el resto de la aplicación evita.
- **FR-150e**: Cuando el registro del contenedor esté vacío o no se haya podido leer, la ficha
  MUST decirlo con un texto que distinga los dos casos, en lugar de dejar el área en blanco. Un
  área vacía se lee como un defecto de la aplicación, no como un contenedor que no escribió nada.
- **FR-151**: El menú contextual de cerrar sesiones MUST alcanzar sólo la cabecera de la pestaña,
  y MUST NOT aparecer al hacer clic derecho dentro del contenido de la sesión.
- **FR-152**: La consola de traza MUST seguir la última entrada de forma continua, con un
  interruptor visible para desactivarlo. MUST NOT dejar de seguir por su cuenta.
- **FR-153**: Los paneles de inventario MUST mostrar cuándo se consultó por última vez, junto al
  botón de actualizar, y MUST recalcular esa leyenda al volver el panel a la vista.

- **FR-154**: La marca de selección del terminal MUST cubrir exactamente las celdas seleccionadas,
  también cuando la selección empieza o termina en medio de un tramo de texto del mismo color.
- **FR-154a**: El doble clic MUST seleccionar la palabra bajo el puntero y el triple clic la línea
  hasta su último carácter escrito. Rutas, direcciones IP, nombres de host y `usuario@servidor`
  MUST tomarse como una sola palabra.
- **FR-154b**: Con Shift, un clic MUST extender la selección existente en lugar de empezar otra.
- **FR-154c**: Arrastrando fuera del borde superior o inferior, la vista MUST acompañar para poder
  seleccionar más de lo que entra en pantalla.
- **FR-154d**: Con `Ctrl` apretado, el arrastre MUST hacer una selección **rectangular**: se toman
  sólo las columnas entre el inicio y el fin, fila por fila. Es lo que hace usable copiar una
  columna de `docker ps` o de `ps aux`, donde la selección por líneas se lleva todo lo demás.
- **FR-154e**: Los caracteres que separan palabras para FR-154a MUST quedar fijos y MUST NOT ser
  configurables: la lista deja fuera la barra, el punto, los dos puntos, el guion, la arroba y la
  virgulilla. Es deliberadamente más ancha que la de PuTTY para que una ruta, una IP o un
  `usuario@servidor` se tomen enteros, y es la única desviación del estándar que mejora el
  resultado en lugar de discutirlo. PuTTY la hace configurable; acá no hace falta y por el
  Principio V no se agrega.
- **FR-155**: El terminal MUST permitir recorrer el historial con el teclado: una página con
  Shift+RePag y Shift+AvPag, una línea con Ctrl+RePag y Ctrl+AvPag, y los extremos con
  Ctrl+Shift+RePag y Ctrl+Shift+AvPag, que son las de PuTTY. Ctrl+Shift+Inicio y Ctrl+Shift+Fin
  MUST seguir funcionando para los extremos: ya están implementadas y quitarlas no le sirve a
  nadie.

- **FR-156**: El sistema MUST hacer una copia de la base al abrir la aplicación, como mucho una
  por día y sólo si la base cambió desde la anterior, conservando las últimas N configurables.
  MUST NOT instalar un servicio ni una tarea programada, ni copiar con la aplicación cerrada.
- **FR-156a**: Las copias MUST escribirse con la API de respaldo de SQLite y MUST NOT hacerse
  copiando el archivo de la base.
- **FR-156b**: La rotación MUST borrar únicamente archivos con el nombre que escribe el propio
  sistema, y MUST NOT tocar nada más de la carpeta elegida.
- **FR-156c**: El sistema MUST permitir elegir la carpeta de las copias, y MUST NOT integrarse con
  ningún proveedor de nube: si la carpeta elegida está sincronizada, la sincroniza esa
  herramienta.
- **FR-156d**: Un fallo al copiar MUST NOT impedir que la aplicación abra ni interrumpir el uso.
- **FR-157**: El sistema MUST ofrecer una ventana de preferencias que muestre la ruta de la base,
  la de los registros y dónde están las contraseñas, con acceso a cada carpeta, más exportar la
  base a un archivo elegido.
- **FR-158**: Las preferencias MUST listar las credenciales `cmc:*` guardadas, resueltas contra la
  conexión o la carpeta a la que pertenecen y marcando las huérfanas. MUST mostrar sólo los
  nombres, nunca el secreto. MAY abrir el Administrador de credenciales de Windows, que no admite
  filtro.
- **FR-159**: El sistema MUST comprobar, **en cada inicio**, si hay una versión más nueva
  publicada en las releases del repositorio, y MUST proponer actualizar cuando la haya. La
  comprobación MUST correr en segundo plano y MUST NOT demorar el arranque ni impedir trabajar si
  falla. El límite de una consulta por día se retiró a pedido del usuario: la consulta es anónima,
  de sólo lectura y cuesta un pedido HTTP.
- **FR-159a**: La consulta MUST ser anónima y de sólo lectura. MUST NOT enviar la versión en uso,
  el nombre del equipo, el usuario ni ningún otro dato. MUST NOT requerir token ni cuenta.
- **FR-159b**: El origen MUST ser fijo —`caftech-ar/CafManagerConection`— y MUST NOT poder
  cambiarse: ni desde las preferencias, ni editando la base a mano. Las preferencias MUST mostrarlo
  de sólo lectura. Apuntar la comprobación a otro repositorio permitiría que la aplicación se
  ofrezca actualizar desde un origen que no es el del proyecto, y eso es un camino de instalación
  de código ajeno con el nombre de CMC.
- **FR-159c**: La release MUST publicarse automáticamente al crear una etiqueta `vX.Y.Z`, y el
  proceso MUST verificar que la etiqueta coincida con la versión declarada en el código, fallando
  si no coinciden. Dos números que hay que mover juntos son dos números que alguna vez se mueven
  por separado, y ahí el instalador y el aviso de versión nueva comparan valores distintos.
- **FR-160**: Cuando haya una versión más nueva, el sistema MUST avisarlo sin interrumpir lo que
  el usuario esté haciendo, mostrando la versión y las novedades publicadas. MUST NOT descargar ni
  instalar nada sin que el usuario lo pida.
- **FR-160a**: El sistema MUST permitir posponer el aviso y MUST NOT volver a mostrarlo para la
  misma versión hasta el próximo día.
- **FR-161**: Al aceptar la actualización, el sistema MUST descargar el instalador y MUST
  verificar su SHA-256 contra el publicado en la release **antes** de ejecutarlo. Un archivo cuyo
  hash no coincida MUST NOT ejecutarse y MUST borrarse.
- **FR-161a**: El instalador NO está firmado con un certificado de código, así que el hash es la
  única garantía de integridad y por eso la verificación no es opcional. Si la release no publica
  el hash, el sistema MUST NOT ejecutar lo descargado y MUST ofrecer abrir la página de la release.
- **FR-162**: El sistema MUST ofrecer buscar actualizaciones a pedido, sin esperar a la
  comprobación diaria, e informar también cuando ya se está en la última versión.
- **FR-163**: La comparación de versiones MUST ser numérica por componente y MUST NOT comparar
  como texto: `0.0.10` es posterior a `0.0.9`, y comparadas como texto no lo son.

#### Puertos a la escucha y procesos (US11)

Alcance incorporado por la enmienda constitucional 1.11.0. El panel de puertos **ya estaba
construido** cuando se escribieron estos requisitos: se documentan para regularizarlo, y queda
anotado como el caso que el Principio V existe para evitar.

- **FR-164**: El sistema MUST listar, para una conexión SSH a un servidor Linux, los sockets en
  los que el servidor está escuchando, con puerto, protocolo, dirección de escucha y el proceso
  que lo tiene. El panel MUST consultarse al abrirlo y refrescarse sólo a pedido, como el resto
  del inventario (FR-107).
- **FR-164a**: El panel MUST NOT listar conexiones establecidas. Motivo: un servidor con tráfico
  devuelve cientos y ahogan las diez o quince líneas que responden la pregunta que se vino a
  hacer.
- **FR-164b**: Cuando el proceso corresponda a una aplicación reconocida, el sistema MUST mostrar
  su nombre legible junto al nombre del proceso, sin reemplazarlo: el nombre del proceso es el
  que sirve para buscarlo en el servidor.
- **FR-164c**: El panel MUST ser de sólo lectura: MUST NOT abrir, cerrar ni redirigir ningún
  puerto del servidor.
- **FR-164d**: Cuando el usuario remoto no pueda ver a qué proceso pertenece un socket, el
  sistema MUST listar igual el puerto e indicar que el proceso no es visible con los permisos
  actuales, en lugar de omitir la fila. Motivo: un puerto abierto que no aparece es peor que uno
  que aparece incompleto — la pregunta era justamente qué está abierto.
- **FR-165**: El doble clic sobre una fila MUST abrir la ficha del proceso que tiene ese socket,
  con: PID, ruta del binario en ejecución, usuario efectivo que lo corre, hace cuánto está
  corriendo, línea de comando completa, directorio de trabajo, proceso padre y cantidad de hilos.
  La ficha MUST nombrar el puerto desde el que se la abrió.
- **FR-165a**: La ficha MUST usar `sudo` cuando el usuario remoto lo tenga permitido y MUST
  funcionar sin él cuando no: en ese caso MUST mostrar los datos que sí pudo leer y nombrar los
  que no, con el motivo. MUST NOT fallar entera por un dato que no se pudo obtener.
- **FR-165b**: El identificador de proceso que se incorpore a una línea de comando MUST validarse
  contra un conjunto cerrado de caracteres antes de enviarse. Motivo: el dato sale de la salida
  de un comando remoto y vuelve a entrar en otro que puede correr con `sudo`; sin validación eso
  es ejecución arbitraria como root. Es la misma regla de FR-100c.
- **FR-165c**: La ficha MUST ser de sólo lectura. MUST NOT ofrecer matar el proceso, mandarle
  ninguna señal ni cambiarle la prioridad. Motivo: las acciones de escritura que la aplicación sí
  tiene son sobre contenedores y procesos de supervisord, que son servicios administrados y
  tienen quien los levante; un PID suelto del sistema no lo es, y un `kill` equivocado en
  producción no tiene deshacer.
- **FR-165d**: Cuando el proceso ya no exista al abrir la ficha, el sistema MUST decirlo y
  ofrecer refrescar la lista, en lugar de mostrar una ficha vacía o un error crudo.
- **FR-165e**: El sistema MUST NOT registrar en los logs la línea de comando, la ruta del
  binario ni ningún otro dato que devuelva esta consulta. Motivo: una línea de comando lleva
  contraseñas en los argumentos más seguido de lo que debería, y FR-105 ya prohíbe registrar la
  salida de los comandos de inventario.

#### Desplazamiento

- **FR-166**: Toda barra de desplazamiento horizontal MUST mover el contenido en el sentido del
  arrastre: hacia la derecha muestra lo que está a la derecha. Motivo: parece obvio y no lo es
  —una barra vertical y una horizontal necesitan configuraciones opuestas en el mismo control—,
  y una barra invertida no falla ni avisa: simplemente hace lo contrario de lo que uno pide.
- **FR-166a**: Una barra de desplazamiento MUST aparecer únicamente cuando haya contenido fuera
  de la vista en ese eje. Un área vacía MUST NOT mostrar ninguna.

- **FR-167**: El panel de puertos MUST ofrecer, sobre la fila elegida, abrir ese puerto en el
  navegador del sistema y copiar la dirección. MUST ofrecer los dos esquemas, `https` primero,
  sin deducirlos del número de puerto: un puerto no dice qué esquema habla, y adivinar mal lleva
  el navegador a un error que no explica su causa.
- **FR-167a**: La dirección MUST armarse con el host de la conexión, no con la dirección de
  escucha del socket. El sistema MUST NOT comprobar previamente que el puerto responda.
- **FR-167b**: Un puerto que escuche sólo en el bucle local del servidor MUST NOT ofrecer
  abrirlo en el navegador, y MUST decir por qué. Un puerto UDP MUST NOT ofrecerlo tampoco.
- **FR-168**: El panel de puertos MUST ofrecer crear un túnel al puerto elegido, tanto para los
  que escuchan sólo en el servidor —que es el caso que lo motiva— como para los alcanzables.
  Un puerto UDP MUST NOT ofrecerlo: el reenvío de puerto local es TCP.
- **FR-168a**: El sistema MUST proponer un puerto local libre en este equipo. MUST proponer el
  mismo número que el remoto cuando esté libre, y MUST considerar tomados tanto los puertos a la
  escucha del equipo como los que ya reservaron otros túneles definidos.
- **FR-168b**: El puerto local propuesto MUST NOT caer en la franja efímera del sistema
  operativo. Un túnel ahí compite con las conexiones salientes del equipo y falla de forma
  intermitente.
- **FR-168c**: El túnel MUST presentarse en el editor de túneles con los valores ya cargados y
  MUST requerir confirmación antes de guardarse: el puerto local y el nombre son decisiones del
  usuario.
- **FR-168d**: Al guardarse, el túnel MUST levantarse en la sesión abierta, sin esperar a la
  próxima conexión. La opción de levantarlo automáticamente en las próximas conexiones MUST
  venir propuesta, y MUST poder desmarcarse.
- **FR-168e**: Cuando el túnel se guarde pero no se pueda levantar, el sistema MUST decir las
  dos cosas: que quedó guardado y por qué no se levantó ahora.
- **FR-168f**: Tras levantar un túnel, el sistema MUST comprobar que el puerto local acepte una
  conexión y MUST distinguir «el túnel está y el servicio contesta» de «el túnel está y el
  servicio no contesta». Que el servidor acepte el reenvío no dice nada sobre el servicio.
- **FR-168g**: El panel de puertos MUST poder copiar la línea de consola equivalente al túnel,
  para reproducirlo fuera de esta aplicación.
- **FR-168h**: El panel de puertos MUST mostrar, en la fila del puerto, el puerto local del túnel
  definido y si está levantado o parado.
- **FR-167c**: Cuando haya un túnel activo hacia el puerto, abrir en el navegador MUST usar el
  puerto local del túnel y no el host del servidor: el túnel es el destino que se sabe alcanzable.
- **FR-164e**: Cuando el proceso dueño de un puerto sea el reenviador de Docker, el panel MUST
  nombrar el contenedor que publica ese puerto. «docker-proxy» sólo dice «alguno de los
  contenedores», que no es la pregunta que el panel contesta.
- **FR-170**: Cuando un panel no se pueda armar, el sistema MUST mostrar el motivo en el lugar del
  panel. MUST NOT remitir a un lugar donde el motivo no está, y ningún camino de armado MUST
  poder terminar sin dejar registro de por qué falló.
- **FR-170a**: El sistema MUST indicar dónde escribe sus registros y MUST permitir abrir esa
  carpeta, desde las preferencias y desde la consola de traza. Lo que falla antes de que haya una
  llamada al servidor no aparece en la traza, y quien la mira vacía es quien necesita el archivo.
- **FR-169**: Cuando no se pueda leer el estado del servidor, el panel MUST mostrar el motivo
  que dio el canal de comandos. MUST NOT remitir al usuario a la consola de traza para averiguar
  algo que el sistema ya tiene, y MUST distinguir un servidor que contesta sin datos de un canal
  que falló.
- **FR-169a**: El tiempo límite de la consulta de estado MUST alcanzar para un servidor con
  latencia real. Una lectura en curso MUST NOT encimarse con la siguiente: el muestreo MUST
  saltear el turno en lugar de encolar lecturas.
- **FR-171**: El panel de estado MUST mostrar las interfaces de red del servidor con su
  configuración: nombre, dirección MAC, MTU, estado del enlace, direcciones IPv4 e IPv6. El
  estado MUST reflejar el enlace y no la dirección: una placa conectada sin configurar MUST
  aparecer igual.
- **FR-171a**: Las interfaces de contenedor (`veth`, con prefijo `br-`, `docker*`, o con
  `master` asignado) MUST quedar fuera del panel salvo que el usuario las pida por nombre.
  Motivo: en un servidor con Docker Swarm son cuarenta interfaces que entierran a las dos que
  importan.
- **FR-171b**: Un túnel VPN (`tun`, `tap`) MUST NOT tratarse como interfaz de contenedor.
  Motivo: en un servidor detrás de VPN es la interfaz por la que llega la conexión, y
  excluirla oculta justamente la que hay que ver.
- **FR-171c**: El estado "levantada" de una interfaz MUST decidirse por la bandera `LOWER_UP`
  del portador y no por el texto de `state`. Motivo: el estado operativo de un túnel es
  `UNKNOWN` aunque funcione.
- **FR-172**: El panel de estado MUST mostrar la tabla de rutas con destino, puerta de
  enlace, interfaz, métrica y marca de interfaz caída (`linkdown`), y MUST mostrar además los
  servidores DNS y el dominio de búsqueda configurados.
- **FR-172a**: Al interpretar `ip -6 route`, el sistema MUST saltear el tipo de ruta
  (`unreachable`, `blackhole`, …) cuando encabece la línea en lugar del destino. Motivo: sin
  saltearlo la ruta se informa con destino "unreachable", que no es una red.
- **FR-173**: El panel de estado MUST mostrar los diez procesos que más consumen por CPU y
  los diez que más consumen por memoria, con PID, usuario, porcentaje de CPU, memoria
  residente, cantidad de hilos, estado y tiempo corriendo. El doble clic sobre una fila MUST
  abrir la ficha de proceso de FR-165.
- **FR-173a**: El porcentaje de CPU de un proceso MUST NOT acotarse a 100. Motivo: un proceso
  con muchos hilos lo supera de forma legítima —se midió 341 % con 82 hilos en 8 núcleos—, y
  ése es exactamente el proceso que hay que ver.
- **FR-173b**: Cuando el usuario del proceso sea un número en lugar de un nombre, el sistema
  MUST mostrarlo tal cual viene, sin intentar resolverlo. Motivo: un UID que no resuelve a
  nombre es típico de procesos en contenedores.
- **FR-173c**: El panel MUST NOT pedir la línea de comando del proceso, sólo el nombre del
  ejecutable. Misma regla que FR-165e y el Principio II: una línea de comando lleva
  contraseñas en los argumentos.
- **FR-174**: El panel de estado MUST mostrar la presión de recursos de `/proc/pressure` para
  CPU, disco y memoria. Motivo: es la medida que dice si el servidor sufre, no cuánto se usa
  —una CPU al 100 % con presión cero es una máquina trabajando a pleno, y un 40 % con presión
  alta es una máquina donde los procesos hacen cola.
- **FR-174a**: Cuando el núcleo no informe presión (sin `CONFIG_PSI`), el sistema MUST decir
  que no está disponible y MUST NOT informar cero. Motivo: cero es una afirmación sobre el
  servidor, y no tenerla no es lo mismo que no sufrir.
- **FR-175**: El panel de estado MUST permitir elegir el intervalo de muestreo desde el
  propio panel, entre un conjunto cerrado de 2, 5, 10, 30 y 60 segundos, y MUST recordarlo
  entre sesiones.
- **FR-175a**: El intervalo MUST NOT aceptarse como campo libre. Motivo: el costo de la
  consulta lo paga el servidor y no la aplicación; un valor tipeado de más lo consultaría sin
  pausa.
- **FR-175b**: El mínimo del conjunto MUST ser 2 segundos. Motivo: es el piso técnico —CPU y
  red se calculan por diferencia entre dos lecturas, y un intervalo menor no deja tiempo entre
  ellas.
- **FR-176**: El panel de estado MUST mostrar la entrada y salida de disco por dispositivo
  —lectura, escritura y porcentaje de ocupación— calculadas de `/proc/diskstats` por
  diferencia entre dos lecturas.
- **FR-176a**: El panel MUST mostrar sólo dispositivos enteros y no sus particiones. Motivo:
  `sda` y `sda4` cuentan la misma actividad, y sumarlas duplica el total.
- **FR-176b**: Los dispositivos `loop` MUST quedar fuera. Motivo: en un servidor Ubuntu con
  snaps son ocho dispositivos que entierran a los dos que interesan.
- **FR-177**: El panel de estado MUST mostrar el tipo de sistema de archivos de cada disco,
  obtenido de `df -PT`. El intérprete MUST ubicar cada campo por el encabezado de la salida y
  MUST NOT contarlos por posición fija: `-T` agrega columnas a la derecha del formato
  habitual, y contar campos se rompe con ese agregado.
- **FR-178**: El panel de estado MUST mostrar la memoria de intercambio (swap) cuando el
  servidor la tenga configurada, y MUST NOT mostrarla cuando no. Motivo: una barra en cero con
  «0 B de 0 B» se lee como un problema y no lo es.
- **FR-179**: El panel de estado MUST mostrar el modelo de procesador. Cuando
  `/proc/cpuinfo` no traiga la línea `model name` —comprobado contra un servidor aarch64 real
  donde el comando no devuelve nada—, el sistema MUST caer a `lscpu` o, en su defecto, al par
  implementador/parte.
- **FR-180**: El panel de estado MUST mostrar las temperaturas de `lm-sensors` cuando esté
  instalado, tomando sólo las medidas `_input` y descartando los umbrales `_max` y `_crit`.
  Motivo: esos umbrales son configuración del sensor, y colarlos daría un «100 °C» que no
  existe.
- **FR-181**: El panel de estado MUST mostrar un gráfico de tendencia de CPU y memoria en la
  misma escala de 0 a 100. Motivo: son las dos medidas que se comparan entre sí cuando algo va
  mal.

- **FR-182**: El sistema MUST poder importar, **una sola vez y a pedido**, las sesiones guardadas de
  PuTTY, WinSCP y FileZilla, creando conexiones propias. Después de importar MUST NOT volver a leer
  esos archivos ni ese registro: es una migración, no una integración.
- **FR-182a**: MUST convertir sólo las sesiones **SFTP y SCP**, que van sobre SSH. Las de FTP, FTPS,
  WebDAV, S3, telnet, rlogin, serial y raw MUST NOT importarse.
- **FR-182b**: Las contraseñas guardadas MAY traerse al Administrador de credenciales de Windows,
  preguntando **una vez por importación** y nunca por omisión silenciosa. La decodificación MUST
  verificarse contra la clave que el propio formato incluye, y si no verifica MUST NOT guardarse
  nada: una contraseña equivocada en el almacén del sistema se descubre recién cuando la conexión
  falla, y nadie la relaciona con la importación.
- **FR-182c**: Lo que no se pueda importar MUST informarse **con su motivo**. Una lista corta sin
  explicación se lee como un fallo de la aplicación.
- **FR-182d**: El sistema MUST NOT crear, modificar ni borrar sesiones en esas herramientas.
- **FR-182e**: La importación MUST reconstruir la jerarquía de carpetas del origen y MUST NOT
  duplicar una conexión que ya exista con el mismo host, usuario y puerto efectivo.

- **FR-191**: Las superficies que muestran salida de consola —registros y terminal— MUST declarar
  sus colores como recursos con nombre, iguales en los dos temas. Son deliberadamente oscuras
  porque los colores ANSI que manda el servidor están pensados para fondo oscuro; lo que MUST NOT
  pasar es que sean colores fijos escritos en cada pantalla, que es como se ve un recuadro negro
  pegado dentro de una ventana clara.
- **FR-192**: El panel de túneles de la sesión MUST permitir borrar y editar un túnel, no sólo
  levantarlo y bajarlo. Borrar MUST pedir confirmación y MUST bajar el túnel si está activo: dejar
  el reenvío vivo sin definición deja un puerto local ocupado por algo que ya no figura en ninguna
  lista.

#### Movidos a la feature 002

Ninguno tiene código propio en esta feature. Viven en
`specs/002-procesos-registros-y-arbol/spec.md`, con sus mismos números, y allá algunos crecieron
con derivados nuevos. **La autorización de cada grupo sale de un lugar distinto**:

- **Enmienda 1.13.0**, que los nombra uno por uno: FR-183 y derivados, FR-184 a FR-184d,
  FR-185 y derivados, FR-186, FR-187, FR-188 y FR-188a.
- **Enmienda 1.14.0**: **FR-195**, **FR-195a** y **FR-195c** —elegir la forma del icono, que agrega
  un atributo persistido y la migración 006— y **FR-184e**, la contraseña de `sudo` en memoria de
  sesión, que entra como excepción acotada al Principio II declarada en el título de esa enmienda
  (`constitution.md:527`) y sujeta a cinco reglas.
- **La cláusula «colorear y ordenar lo que ya se muestra NO es ampliar el alcance»**
  (`constitution.md:783`), que no exige enmienda: FR-190, FR-193 y el color del icono. Ninguno
  trae un dato nuevo del servidor ni una dependencia.
- **Defectos de requisitos ya construidos**, que tampoco son alcance nuevo: FR-173d —el top
  ordena por el promedio de toda la vida del proceso y no por el uso instantáneo—, FR-189 y
  derivados —la transferencia de carpetas ya la exige FR-073 desde la enmienda 1.2.0—, FR-194,
  y FR-196 y FR-196a, que apilan sus campos en un `StackPanel` vertical
  (`src/CafManagerConection.App/Views/FolderSettingsWindow.xaml:51` y
  `src/CafManagerConection.App/Views/ConnectionEditorWindow.xaml:89`).

La lista completa:

- **FR-173d** — el top del panel de estado, con el mismo CPU instantáneo del panel de procesos.
- **FR-183** a **FR-183d** — panel de procesos del servidor.
- **FR-184** a **FR-184b** — escalada con `sudo`.
- **FR-185** a **FR-185c** — seguimiento de registros en vivo.
- **FR-186**, **FR-187** — RDP con la identidad de Windows, y maximizar o salir a una ventana propia.
- **FR-188**, **FR-188a** — clave privada a las herramientas externas.
- **FR-189** a **FR-189b** — explorador SFTP en árbol.
- **FR-190** — asignar la etiqueta desde el menú contextual.
- **FR-193** a **FR-196a** — orden, icono, color y distribución de las ventanas del árbol.

De ese último grupo, **FR-195b** —que el icono y el color no se hereden— describe lo que la
aplicación ya hace: ni `FolderSettings` ni `SettingsResolver.Resolve`
(`src/CafManagerConection.UseCases/Inheritance/SettingsResolver.cs`) conocen el color, que se
elige por elemento con `src/CafManagerConection.Domain/Settings/PaletaIconos.cs`. Lo que falta,
y por eso el requisito se fue, es elegir la forma del icono: hoy el glifo sale del protocolo.

Lo que sí quedó construido de esa tanda está acá: FR-026a (juego gráfico de DEC), FR-182 y familia
(importación), FR-191 (colores de consola) y FR-192 (borrar y editar un túnel).

### Key Entities

- **Carpeta**: agrupación con nombre que contiene conexiones y, opcionalmente, otras
  carpetas. Tiene un orden dentro de su carpeta padre, elegido por el usuario. Puede definir una
  credencial y valores de configuración que sus descendientes heredan. Puede además tener
  descripción, etiquetas, icono y color propios; el icono y el color no se heredan.
- **Conexión hija**: una conexión que cuelga de otra en lugar de colgar de una carpeta.
  Representa un servicio del servidor padre y hereda de él host, usuario, dominio y
  credencial. Un solo nivel: una hija no puede tener hijas.
- **Configuración heredable**: conjunto de valores que tanto una carpeta como una conexión
  pueden definir —usuario, dominio, puerto, credencial y los ajustes de cada protocolo—.
  En una conexión, cada campo indica además si toma su valor de la carpeta o si es propio.
- **Valor efectivo**: el valor que finalmente se usa al conectar, resultado de recorrer el
  árbol hacia arriba hasta encontrar el primer valor definido. No se almacena: se calcula.
- **Conexión**: servidor guardado. Atributos comunes: identificador, carpeta contenedora,
  nombre, protocolo (RDP o SSH), host, puerto, usuario, referencia a su credencial, notas,
  fecha de creación, fecha de última modificación, fecha de última conexión y orden dentro
  de la carpeta.
- **Configuración RDP**: parámetros propios de una conexión RDP: dominio opcional,
  portapapeles habilitado, ajuste de resolución a la pestaña, política ante advertencias de
  certificado y preferencia de pantalla completa.
- **Configuración SSH**: parámetros propios de una conexión SSH: tipo de autenticación, ruta
  de la clave privada, fingerprint conocido del host, intervalo de keep-alive y codificación.
- **Referencia de credencial**: identificador opaco que vincula una conexión con la entrada
  correspondiente en el almacén de credenciales del sistema operativo. Nunca contiene el
  secreto.
- **Sesión**: instancia viva de una conexión abierta en una pestaña. Tiene un estado
  (conectando, conectado, desconectado, error), un instante de inicio y, cuando corresponde,
  un motivo de finalización.
- **Evento de historial**: registro de un intento de conexión, con la conexión asociada, el
  instante, el resultado y el motivo cuando falló.
- **Preferencias de la aplicación**: tamaño, posición y estado de la ventana, tema
  seleccionado y límite de líneas del historial del terminal.
- **Túnel**: reenvío de puerto definido en una conexión SSH. Tiene nombre, puerto local, host
  y puerto remotos, y si se levanta automáticamente al conectar. Se persiste.
- **Instantánea del servidor**: conjunto de métricas leídas en un instante (CPU, memoria,
  carga, uptime, discos, red y datos del sistema). Vive sólo en memoria; se conservan los
  últimos 60 puntos por métrica.
- **Capacidades del servidor**: qué expone un host remoto — si es Linux, si tiene Docker,
  nginx o supervisord. Se detecta una vez por sesión y determina qué paneles se ofrecen.
- **Contenedor**, **Servicio de compose**, **Sitio de nginx** y **Proceso de supervisord**:
  entidades de sólo lectura, leídas del servidor en cada consulta. Ninguna se persiste.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Con la aplicación abierta, un administrador llega a una sesión conectada en
  dos acciones (localizar la conexión y abrirla) y en menos de 10 segundos desde que la
  abre, en condiciones de red normales.
- **SC-002**: Un administrador que nunca usó la herramienta crea su primera conexión y se
  conecta correctamente en menos de 3 minutos, sin consultar documentación.
- **SC-003**: La aplicación sostiene al menos 8 sesiones simultáneas (mezcla de RDP y SSH)
  sin degradación perceptible, con un cambio entre pestañas que se percibe inmediato (menos
  de 300 milisegundos).
- **SC-004**: La aplicación arranca hasta una ventana utilizable en menos de 2 segundos en
  un equipo de trabajo típico.
- **SC-005**: En reposo, sin sesiones abiertas, el consumo de memoria de la aplicación se
  mantiene por debajo de 150 MB.
- **SC-006**: Tras un recorrido completo por todas las funciones, la inspección de la base
  de datos local y de todos los archivos de registro no revela ninguna contraseña,
  passphrase, clave privada, texto tecleado en SSH, contenido de pantalla RDP ni contenido
  del portapapeles. Cero hallazgos es el único resultado aceptable.
- **SC-007**: `vim`, `nano`, `top`, `htop`, `less` y `tmux` se dibujan completos y responden
  al teclado en el terminal integrado; al redimensionar la ventana, las seis aplicaciones se
  redibujan al nuevo tamaño sin dejar residuos visuales.
- **SC-008**: Un archivo con acentos, `ñ` y símbolos Unicode, y una salida con 256 colores,
  se muestran con los glifos y los colores correctos.
- **SC-009**: El 100% de los fallos de conexión de los tipos previstos (host inalcanzable,
  credenciales rechazadas, tiempo de espera agotado, fingerprint cambiado, clave faltante,
  passphrase incorrecta) presentan un mensaje que nombra la causa y propone una acción.
- **SC-010**: Al cerrar y reabrir la aplicación, el 100% de las conexiones, carpetas, su
  organización, el tamaño y la posición de la ventana y el tema se conservan.
- **SC-011**: La aplicación se ejecuta descomprimiendo una carpeta portable en un equipo
  Windows 11 recién instalado, sin requerir ninguna instalación previa, sin privilegios de
  administrador y sin instalar servicios.
- **SC-012**: Una sesión que falla o se cae no interrumpe ninguna otra sesión abierta ni
  cierra la aplicación, en el 100% de los casos probados.
- **SC-013**: Un administrador define la credencial, el usuario y el puerto una sola vez en
  una carpeta y crea 20 conexiones SSH dentro sin volver a cargar ninguno de esos datos.
  Cambiar después la credencial de la carpeta actualiza las 20 en una sola operación.
- **SC-014**: Con el tema oscuro activo, ninguna ventana, diálogo o menú de la aplicación se
  muestra en claro. La ventana principal presenta la tarjeta del layout inset, esquinas redondeadas y usa
  el color de acento configurado en Windows.
- **SC-015**: Puesta junto a una aplicación nativa de Windows 11, la interfaz de CMC no
  desentona: tipografía, espaciados, radios de esquina y estados de apuntado y foco son
  coherentes con ella. Se verifica por comparación visual directa.
- **SC-016**: Un archivo de 100 MB enviado al servidor y traído de vuelta llega con contenido
  idéntico al original, verificable por suma de verificación.
- **SC-017**: Los valores de CPU, memoria, carga, uptime y discos que muestra el panel de
  estado coinciden con los que devuelven las herramientas del sistema ejecutadas a mano en
  ese momento, con una diferencia atribuible sólo al intervalo de muestreo.
- **SC-018**: Con el panel de estado abierto y muestreando cada 5 segundos, la carga que CMC
  agrega al servidor es despreciable: menos del 1 % de una CPU del servidor.
- **SC-019**: Un servicio que sólo escucha en `localhost` del servidor queda accesible desde
  el equipo local a través de un túnel definido en la conexión, y el puerto local queda
  liberado al detenerlo.
- **SC-020**: La lista de contenedores, sitios de nginx y procesos de supervisord que muestra
  CMC coincide con la que devuelven `docker ps -a`, la configuración habilitada de nginx y
  `supervisorctl status` ejecutados a mano.
- **SC-021**: En un servidor sin Docker, sin nginx o sin supervisord, los paneles
  correspondientes no se ofrecen, sin mensajes de error ni paneles vacíos.
- **SC-022**: Con PuTTY abierto al lado contra el mismo servidor, los doce gestos del estándar
  —seleccionar y soltar, doble clic, triple clic, Shift+clic, Ctrl+arrastre, botón medio, clic
  derecho, Ctrl+clic derecho, Ctrl+C sobre un comando corriendo, Ctrl+Ins, Shift+Ins y las cuatro
  combinaciones de historial— producen en CMC el mismo resultado observable que en PuTTY, salvo
  la confirmación de pegado multilínea de FR-030f, que es una desviación declarada. Se verifica
  gesto por gesto.
- **SC-023**: Contra un servidor Linux, la lista de puertos que muestra CMC coincide con la que
  devuelven las herramientas del sistema ejecutadas a mano, y el doble clic sobre una fila muestra
  binario, PID, usuario y tiempo corriendo que coinciden con los de ese PID según el sistema. Con
  un usuario sin `sudo`, la ficha muestra al menos PID y proceso, y nombra explícitamente lo que
  no pudo leer.
- **SC-024**: Una conexión SSH por contraseña sin credencial guardada llega a sesión conectada
  escribiendo la contraseña en la consola, tanto contra un servidor que ofrece el pedido
  interactivo por teclado como contra uno que sólo acepta contraseña directa. En los dos casos,
  sin ningún mensaje de error previo.
- **SC-025**: En los dos temas, los tres niveles de estado del panel de estado, del visor de
  registros y de la ficha de contenedor se distinguen entre sí **con la pantalla en escala de
  grises**, y ninguna combinación de texto sobre su fondo baja de una relación de contraste de
  4,5:1.
- **SC-026**: En un archivo de configuración de nginx de al menos 200 líneas, el texto que
  muestra el visor es idéntico carácter por carácter al que devuelve el servidor: se verifica
  copiando lo mostrado y comparándolo con el archivo original.
- **SC-027**: En el panel de puertos, el botón derecho sobre un puerto TCP alcanzable ofrece
  `https` y `http`, en ese orden; sobre uno que escucha sólo en el servidor no ofrece ninguno y
  dice por qué; sobre uno UDP no ofrece ninguno de los dos ni el túnel.
- **SC-028**: Desde el panel de puertos, un servicio que escucha sólo en `localhost` del
  servidor queda accesible en el navegador de este equipo sin escribir ningún número a mano, y
  sigue estándolo al reabrir la conexión sin volver a definir nada.
- **SC-029**: Cuando el estado del servidor no se puede leer, el panel muestra un motivo que
  nombra la causa —tiempo excedido, autenticación rechazada, comando inexistente— sin necesidad
  de abrir la consola de traza.
- **SC-030**: Ningún panel puede fallar sin decir por qué. Se verifica provocando un fallo de
  armado: el motivo aparece en el lugar del panel, con el texto de la causa real y no el de la
  excepción que la envuelve.
- **SC-031**: En un servidor con contenedores que publican puertos, cada fila del panel de puertos
  cuyo proceso sea el reenviador de Docker nombra el contenedor correcto, verificado contra
  `docker ps --format '{{.Names}} {{.Ports}}'` ejecutado a mano.
- **SC-032**: Con un túnel activo hacia un puerto, el menú de esa fila abre el navegador en el
  puerto local del túnel, y la fila muestra ese puerto local.
- **SC-033**: En cada uno de los tres servidores de referencia, los discos que muestra el
  panel de estado —dispositivo, tipo de sistema de archivos y porcentaje de ocupación—
  coinciden con `df -PT` ejecutado a mano, excluyendo `loop` y particiones.
- **SC-034**: En cada uno de los tres servidores de referencia, la dirección IPv4 e IPv6 de
  cada interfaz mostrada coincide con `ip addr`, incluida la interfaz VPN en el servidor que
  la usa.
- **SC-035**: En cada uno de los tres servidores de referencia, la puerta de enlace
  predeterminada que muestra el panel coincide con la de `ip route` (o `ip -6 route` cuando
  corresponda).
- **SC-036**: En cada uno de los tres servidores de referencia, los diez procesos que el
  panel ordena por CPU y los diez que ordena por memoria coinciden con los que devuelve `top`
  ordenado por el mismo criterio.
- **SC-037**: Cambiar el intervalo de muestreo del panel de estado y reabrir la conexión
  conserva el intervalo elegido, sin volver al valor por omisión.
- **SC-041**: Cambiando de tema claro a oscuro y de vuelta, ninguna de las ventanas de la aplicación
  queda con texto ilegible ni con un bloque de color que no corresponda al tema. Se verifica ventana
  por ventana, y además con una prueba automática: ningún color literal en `Views/` ni en `Panels/`.
- **SC-044**: Las sesiones importadas de PuTTY, WinSCP y FileZilla coinciden una a una con las que
  muestran esas herramientas, y reimportar no crea duplicados.

Los criterios **SC-038, SC-039, SC-040, SC-042, SC-043** y **SC-045 a SC-048** se movieron con sus
requisitos a `specs/002-procesos-registros-y-arbol/spec.md`.

## Out of Scope

Lo siguiente queda deliberadamente fuera de la versión 1. Incorporar cualquiera de estos
puntos requiere una enmienda previa a la constitución del proyecto (Principio V).

- **Protocolos**: SCP, VNC y Telnet. SFTP y los túneles de reenvío de puerto local **sí**
  están en alcance (US6 y US8). No se incluyen el reenvío remoto inverso ni el proxy SOCKS
  dinámico.
- **Monitoreo permanente**: el panel de estado y el de procesos muestran el servidor mientras la
  aplicación está conectada. No recolectan con la aplicación cerrada, no conservan historia más allá
  de unos minutos en memoria y no persisten métricas. No reemplazan a Prometheus ni a Grafana.
  Tampoco hay alertas con umbral configurable ni notificación fuera de la aplicación; el aviso en
  pantalla de FR-185c (feature 002) —una línea de error, un archivo que dejó de leerse, un canal cortado— es parte
  del visor y dura lo que dura la sesión.
- **Escritura sobre la plataforma**: iniciar, detener y reiniciar un contenedor (FR-150) y un
  proceso de supervisord (FR-100a) están en alcance, con confirmación explícita. Todo lo demás no:
  recrear, recargar la configuración de nginx, cambiar límites, editar el `docker-compose` o
  modificar cualquier definición del servidor. El panel de nginx es de sólo lectura completo.
- **Edición de configuración remota**: no se editan archivos de configuración del servidor
  ni se generan archivos `docker-compose` desde la aplicación.
- **Shells locales**: no se ofrece una consola local (PowerShell ni CMD); CMC solo abre
  sesiones contra servidores remotos.
- **Redirecciones RDP**: audio, micrófono, discos locales, impresoras, puertos, cámaras y
  tarjetas inteligentes; tampoco RemoteApp ni conexión a través de RDP Gateway.
- **Automatización**: agentes de IA, scripts programados y ejecución desatendida sobre uno o varios
  servidores. La paleta de comandos guardados (FR-147) **sí** está en alcance: es un atajo para
  escribir en el terminal de una sesión abierta, con el usuario mirando, y no ejecuta nada sola.
- **Colaboración e identidad corporativa**: sincronización en la nube, carpetas o conexiones
  compartidas entre personas y soporte multiusuario. De Active Directory, lo único en alcance es
  abrir una sesión RDP con las credenciales de la sesión de Windows del usuario (FR-186, feature 002): no se lee
  el directorio, no se resuelven grupos y no se descubren equipos.
- **Migración desde otras herramientas**: importar sesiones de PuTTY, WinSCP y FileZilla
  entró por la enmienda 1.12.0. Importar desde **cualquier otra** herramienta sigue fuera,
  y agregar un cuarto origen exige otra enmienda.
- **Otras plataformas**: aplicación móvil y cualquier sistema operativo distinto de
  Windows 11.
- **Navegador embebido**: no se incorpora ninguna vista web dentro de la aplicación.

## Assumptions

Estas decisiones se tomaron como valores predeterminados razonables ante detalles no
especificados. Cada una puede revisarse antes de la implementación.

- **Un solo usuario**: una instalación por usuario de Windows, sin perfiles compartidos, sin
  multiusuario y sin sincronización entre equipos.
- **Red directa**: los servidores son accesibles por red directa, sin gateway y sin salto
  intermedio (jump host). Los túneles de reenvío de puerto local **sí** están en alcance (US8,
  FR-088 a FR-092 y FR-192): los abre CMC sobre una sesión ya establecida, y no son un camino
  para llegar al servidor.
- **Sin privilegios elevados**: la aplicación no requiere privilegios de administrador para
  ninguna de sus funciones, y no corre elevada. La excepción es instalarla y desinstalarla:
  el instalador escribe en `$PROGRAMFILES64` y pide elevación
  (`installer/CafManagerConection.nsi`), y por eso FR-161 actualiza ejecutando el instalador en
  lugar de escribir sobre la carpeta instalada.
- **Distribución portable y con instalador**: el ZIP portable es self-contained y sigue siendo la
  forma de usar CMC sin instalar nada. Además, por la enmienda 1.8.0 de la constitución se
  distribuyen **dos instaladores** desde el mismo guion `installer/CafManagerConection.nsi`: el
  liviano, dependiente del Escritorio de .NET, y uno self-contained para equipos sin internet o
  donde no se pueda instalar el runtime (`task installer` y `task installer:completo` del
  `Taskfile.yml`).
- **Carpetas anidadas**: se admite anidamiento de carpetas sin un límite fijo de
  profundidad, porque es lo habitual en herramientas de este tipo y el costo de soportarlo
  es bajo.
- **Sesiones no restauradas**: al cerrar la aplicación las sesiones se cierran; al volver a
  abrirla no se restauran automáticamente. Lo que sí sobrevive al cierre son las preferencias:
  el estado de la ventana, el tema y los ajustes que su propio requisito manda recordar, como el
  intervalo de muestreo del panel de estado (FR-175).
- **Scrollback predeterminado**: el historial del terminal se limita a 10.000 líneas por
  sesión, valor configurable por el usuario.
- **Retención del historial de conexiones**: se conservan los últimos 100 eventos por
  conexión; los más antiguos se descartan.
- **Tiempo de espera de conexión**: 30 segundos como valor predeterminado antes de declarar
  vencido un intento de conexión.
- **Keep-alive SSH predeterminado**: 60 segundos, configurable por conexión, y desactivable.
- **Certificados RDP**: la validación de las advertencias de certificado está activada de
  forma predeterminada; ignorarlas es una decisión explícita por conexión.
- **Duplicar una conexión**: la copia reutiliza la misma credencial guardada de forma
  predeterminada; el usuario puede asignarle una credencial propia al editarla.
- **Idioma de la interfaz**: español, sin soporte multilenguaje en la versión 1.
- **Sin reintentos automáticos**: una conexión fallida no se reintenta sola; la reconexión
  es siempre una acción del usuario, para no bloquear cuentas por reintentos repetidos.
- **Respaldo y migración desde la aplicación**: por la enmienda 1.9.0, la aplicación hace copias
  locales de la base y **exporta la base a un archivo elegido** desde la ventana de preferencias
  (FR-156 y FR-157; `src/CafManagerConection.Infrastructure/Database/ServicioDeCopias.cs` y
  `src/CafManagerConection.App/Views/PreferenciasWindow.xaml.cs`). Las credenciales **no** viajan
  en la copia: viven en el Credential Manager del equipo original y hay que volver a cargarlas en
  el destino. Lo que sigue fuera es la sincronización con cualquier proveedor de nube: CMC escribe
  en la carpeta que le indiquen y no sabe si está sincronizada.
- **Herencia sólo hacia abajo**: una carpeta define valores para sus descendientes; nunca al
  revés. Una conexión no modifica la configuración de su carpeta.
- **La herencia no se materializa**: los valores heredados se calculan al usarlos, no se
  copian a la conexión. Cambiar la carpeta cambia el comportamiento de todos sus
  descendientes que heredan, que es justamente el objetivo.
- **Techo de la apariencia de shadcn/ui** *(revisado 2026-08-25)*: con **WPF** y plantillas
  XAML propias se alcanza el lenguaje visual de shadcn/ui de forma completa, incluidos tema
  claro y oscuro conmutables en caliente. El techo que queda es el del propio WPF frente a
  WinUI 3 en efectos de composición, que no afecta a nada de lo especificado acá. El supuesto
  anterior —que esto se lograba con WinForms y dibujo propio— quedó obsoleto: se migró a WPF
  porque con WinForms el cambio de tema se veía lento y los controles delataban su origen.
- **Sin tope de sesiones simultáneas**: la aplicación no impone un límite propio; el techo
  real lo ponen los recursos del equipo. SC-003 fija 8 sesiones como objetivo verificable, no
  como máximo.
- **Umbrales de estado fijos, no configurables** *(2026-09-01)*: 75 % y 90 % vienen de la
  práctica corriente de monitoreo. No se ofrecen configurables: es una pantalla para mirar de
  reojo, no un sistema de alertas, y el Principio V ya excluye crecer en esa dirección. Si un
  servidor tiene otro criterio, quien lo administra lo sabe y lee el número, que está al lado.
- **Un solo idioma para clasificar registros** *(2026-09-01)*: la clasificación por nivel de
  FR-100f reconoce las marcas habituales en inglés —`ERROR`, `WARN`, `FATAL`, `CRITICAL`— y las
  de los formatos de syslog. No intenta reconocer registros en otros idiomas: acertar poco y
  fallar seguido es peor que no colorear, y por eso FR-100f exige que el texto no se modifique
  nunca.
- **La ficha de proceso lee, no interpreta**: muestra lo que el sistema devuelve sobre un PID.
  No deduce a qué servicio pertenece, no lo relaciona con unidades de systemd y no arma un árbol
  de procesos; para eso está el terminal, que ya está abierto al lado. Vale para la **ficha de
  FR-165**, alcanzada desde un puerto, y no para el panel de procesos de la feature 002, donde
  FR-183 sí despliega los hijos de un proceso
  (`specs/002-procesos-registros-y-arbol/spec.md`).
