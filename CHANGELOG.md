# Historial de versiones

## 0.2.1

### Novedades

- **Franja de métricas en las sesiones SSH.** Al pie del terminal, con uso de CPU, memoria y disco
  del servidor, su distribución, tráfico de red, carga y tiempo encendido. Se refresca cada cinco
  segundos, con un gráfico de las últimas muestras en CPU y memoria. Viene activa; se apaga en
  Preferencias → Sesiones o por conexión desde su editor.
- **Bitácora de las sesiones SSH.** Todo lo que pasa por el terminal se escribe a un archivo a medida
  que ocurre, sin depender del historial de desplazamiento. Un archivo por sesión, con el host
  adelante para que las del mismo equipo queden juntas. Purga por antigüedad al arrancar. Viene
  activa; se apaga en Preferencias → Sesiones o por conexión.
- **Visor de bitácoras**, en su propia pestaña: listado por fecha, filtro por conexión, lectura del
  contenido desde el final en tramos y búsqueda dentro del archivo.
- **Ping de un subárbol**, en su propia pestaña. Cada equipo se prueba con ICMP y con una conexión a
  su puerto, en paralelo y con concurrencia acotada; el resultado combinado distingue «vivo», «sin
  servicio» —responde el equipo pero el puerto no abre—, «sin respuesta» y «no resuelve el nombre».
  Cada fila se despliega para probar el resto de los puertos configurados de esa conexión: el de su
  dirección web y los extremos de sus túneles.
- **Abrir una conexión RDP en el cliente de Windows**, desde el menú del árbol y desde la barra de la
  sesión, igual que SSH en PuTTY. Cada herramienta externa declara qué protocolos atiende.
- **Elección del instalador al actualizar.** El aviso de versión nueva muestra los instaladores
  publicados con su tipo, su tamaño y qué precisa cada uno, y marca cuál está instalado.
- **Aviso de novedades** la primera vez que una instalación existente arranca con esta versión.
- **Volver a pedir la contraseña de sudo** desde la barra de la sesión, sin reconectar.

### Correcciones

- Las sesiones SSH se abrían pidiendo un pseudo-terminal de 80×24 —el tamaño con el que nace el
  control, leído antes de medirlo—, así que el servidor dimensionaba su bienvenida a 24 filas y el
  prompt quedaba a media pantalla.
- Al achicar el alto del terminal se conservaban las primeras filas y se descartaban las últimas, con
  el prompt entre ellas, sin mandarlas al historial.
- Al angostar el terminal se perdía lo que cada línea tenía más allá del borde derecho, y no volvía al
  ensanchar. El ancho de lo guardado ya no baja.
- La selección del terminal estaba atada a la fila de pantalla: al arrastrar más allá del borde, lo
  que salía de la vista no quedaba seleccionado y se copiaba incompleto. Ahora se ancla a la línea.
- Desplazar el historial con la rueda, el teclado o la barra suelta la selección activa.
- Enter no confirmaba los avisos y confirmaciones: el botón predeterminado era cancelar. Ahora Enter
  ejecuta lo que el diálogo propone, salvo en lo que destruye —eliminar, borrar, sobrescribir, aceptar
  una huella de host nueva—, donde el predeterminado es la opción que no rompe nada.
- El editor de conexiones no aplicaba la carpeta del padre al crear una conexión nueva, así que se
  guardaba en una carpeta distinta a la de su padre.
- Escribir a mano el puerto por omisión del protocolo se guardaba como «heredar», y no había forma de
  forzar el puerto estándar en una conexión cuya carpeta define otro.
- La preferencia «Abrir en ventana propia» de una conexión RDP se borraba sola cuando un traslado de
  ventana salía bien. Que el traslado corte la sesión pasó a ser un ajuste de la aplicación: es una
  propiedad del equipo, no de la conexión.
- La fecha de última conexión no se mostraba en el árbol.
- El visor de bitácoras mostraba el listado pero no el contenido: el campo heredaba el alto fijo del
  estilo de `TextBox` y el archivo entraba en 32 píxeles.

### Cambios de comportamiento

- `SessionFailureReason.CertificateUntrusted` se da de baja: nada lo escribía y ninguna fila del
  historial lo usaba.
- Los túneles automáticos se levantan con `TunnelHost.StartAutoAsync`: un túnel que falla ya no corta
  a los demás y se informan todos los errores.
- La purga de bitácoras sólo borra archivos con el nombre y el sello que genera la aplicación, y no
  corre con la bitácora apagada.
- La contraseña de sudo se sigue pidiendo una sola vez por sesión; habilitar otro intento es explícito.

### Interfaz

- **Modos de pestaña**: lineal con desplazamiento, lineal con desplegable de sobra, o envuelto en
  varias filas. Se cambia sin reabrir las sesiones.
- **Opciones de pantalla RDP**: rendimiento (`PerformanceFlags` y tipo de red), escala de escritorio y
  de dispositivo por el ámbito extendido, resolución dinámica separada del escalado de píxeles, y
  programa inicial con directorio de trabajo, opcionalmente como RemoteApp.
- **Secretos en edición**: se indica si hay contraseña guardada y un ícono la revela por el mismo
  camino que «Copiar contraseña».
- El método de autenticación SSH pasa a «Clave privada» al definir o pegar una clave.
- Las pestañas dejaron de ser sólo sesiones: las de herramienta no cuentan para el título ni para el
  aviso de cierre.

### Modelo de datos

- **Usuario y puerto de carpeta por protocolo** (`rdp_username`/`ssh_username`/`web_username` y
  `rdp_port`/`ssh_port`/`web_port`). La conexión hereda el de su protocolo; el propio sigue ganando.
- **Migración 004** (irreversible, como todas): backfill del usuario y el puerto compartidos a las
  columnas por protocolo y baja de los compartidos; `CHECK` de rango en los puertos, de JSON válido en
  `custom_fields`, de no vacío en `name`, `host` y `url`, y de coherencia entre `outcome` y
  `failure_reason`; `connections` suma `es_rapida` y pierde `documentation_url` y
  `last_connected_at`, y las notas que dejó el importador viejo —«Importado de Rdm como …»— se
  vacían; `rdp_settings` pierde `fit_to_tab` y `start_full_screen` pasa a
  `abre_en_ventana_propia`; `ssh_settings` pierde `encoding`; `ssh_tunnels` pierde `sort_order`.
- El motor de migraciones recrea tablas sin perder filas hijas, sólo aparta la base ante corrupción
  real (`SQLITE_CORRUPT` y `SQLITE_NOTDB`) y avisa antes de crear una nueva.

### Interno

- Colector de métricas liviano para la franja: cinco lecturas de `/proc` por vuelta, `df` una vez por
  minuto y `/etc/os-release` una sola vez al conectar. El panel de Estado conserva el suyo, de 26
  tramos.
- Se borran 13 símbolos que aparecían una sola vez en el repositorio, cuatro convertidores y un estilo
  sin usar, dos referencias de proyecto que no se usaban, y `ArbolDeProcesos` y `TopProcessesParser`,
  reemplazados por `IndiceDeProcesos` y `ColectorDeProcesos`.
- Guardianes nuevos sobre el código: toda confirmación con verbo de borrado lleva su marca, y todo
  campo de texto de varias líneas declara su alto.

## 0.2.0

Versión inicial.
