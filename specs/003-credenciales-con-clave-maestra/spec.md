---

description: "Especificación: credenciales cifradas con una clave maestra"
---

# Feature Specification: Credenciales cifradas con una clave maestra

**Feature Branch**: `003-credenciales-con-clave-maestra`

**Created**: 2026-09-03

**Status**: Draft

**Constitución**: 2.1.0 — el Principio II se redefinió para esta feature. Todo lo de acá tiene que
leerse contra él, no contra la versión 1.x.

**Input**: Los once puntos del usuario, más dos aclaraciones posteriores: la clave maestra la
define el usuario con un mínimo de 8 caracteres alfanuméricos y al menos un carácter especial; y
son dos instalaciones en total, con el migrador muerto en 0.1.2.

## Por qué existe esta feature

Hasta la 0.1.0 los secretos vivían en el Administrador de credenciales de Windows y la base local
guardaba sólo una referencia opaca `cmc:*`. El usuario decidió sacarlos de ahí. Lo que se gana:
el archivo de la base queda cifrado en reposo, el vault se abre en otra máquina con sólo la clave
maestra, y bloquear a mano saca las claves de memoria. Lo que cuesta: el archivo pasa a contener
todos los secretos, y perder la clave maestra es perderlos.

## User Scenarios & Testing

### User Story 1 — Crear el vault y desbloquearlo (Priority: P1)

La primera vez que CMC abre sin vault, pide una clave maestra, la exige dos veces, advierte que
perderla es irrecuperable y ofrece —sin nada preseleccionado— recordar el dispositivo. Desde
entonces, cada arranque pide la clave maestra y con ella se abre el vault.

**Why this priority**: sin esto no hay dónde guardar una credencial. Todas las demás historias
suponen un vault que existe y se abre.

**Independent Test**: con una instalación limpia, crear el vault, guardar la contraseña de una
conexión, cerrar CMC, abrirlo de nuevo, dar la clave maestra y conectar con esa credencial.
Verificable sin migración, sin DPAPI y sin copias.

**Acceptance Scenarios**:

1. **Given** una instalación sin vault, **When** CMC abre, **Then** pide crear la clave maestra
   antes de permitir guardar cualquier credencial, mostrando los requisitos de forma antes de que
   se rechace nada.
2. **Given** el pedido de creación, **When** el usuario escribe `abc12345` (sin carácter
   especial), **Then** no se acepta y se dice cuál requisito falta, sin borrar lo tipeado.
3. **Given** el pedido de creación, **When** las dos escrituras no coinciden, **Then** no se crea
   nada y se dice que no coinciden.
4. **Given** el pedido de creación, **When** el usuario escribe una frase de 60 caracteres con
   espacios, **Then** se acepta completa y sin recortar.
5. **Given** un vault creado, **When** CMC abre, **Then** pide la clave maestra y no muestra
   ninguna credencial hasta tenerla.
6. **Given** el pedido de desbloqueo, **When** la clave maestra es incorrecta, **Then** lo dice y
   deja reintentar, sin distinguir en el mensaje una clave equivocada de una base corrupta más de
   lo que hace falta.
7. **Given** el pedido de desbloqueo, **When** el usuario lo cancela, **Then** CMC **abre igual**,
   muestra el árbol de conexiones y dice qué queda sin funcionar.
8. **Given** el vault bloqueado, **When** el usuario abre una conexión RDP con identidad de
   Windows o una SSH por clave sin passphrase, **Then** la conexión funciona: no necesita el vault.

---

### User Story 2 — Traer las credenciales que ya existen (Priority: P2)

Al abrir la 0.1.1 por primera vez, CMC encuentra las credenciales `cmc:*` en el Administrador de
credenciales, las guarda cifradas en el vault, verifica que se leen desde ahí y sólo entonces las
borra del Administrador.

**Why this priority**: son dos instalaciones reales con credenciales cargadas. Sin esta historia,
actualizar es empezar de cero a mano.

**Independent Test**: sobre una copia de una base de la 0.1.0 con credenciales en el
Administrador, correr la 0.1.1, y comprobar que todas las conexiones siguen conectando y que en el
Administrador ya no queda ninguna `cmc:*`.

**Acceptance Scenarios**:

1. **Given** una base de la 0.1.0 con N credenciales `cmc:*`, **When** la 0.1.1 abre por primera
   vez y el usuario crea la clave maestra, **Then** las N quedan en el vault y se informa cuántas
   se trajeron.
2. **Given** la migración en curso, **When** una credencial se escribe en el vault, **Then** se
   vuelve a leer y descifrar desde el vault **antes** de borrarla del Administrador.
3. **Given** la migración en curso, **When** una credencial no se puede leer del Administrador,
   **Then** se informa cuál y por qué, no se borra, y la migración sigue con las demás.
4. **Given** una migración interrumpida a mitad de camino, **When** CMC vuelve a abrir, **Then**
   retoma las que faltan y no duplica ni pierde ninguna.
5. **Given** el usuario cancela la creación de la clave maestra, **When** la migración iba a
   correr, **Then** no se migra ni se borra nada, y las credenciales siguen en el Administrador.
6. **Given** una versión 0.1.2 o posterior, **When** encuentra credenciales `cmc:*` en el
   Administrador, **Then** lo dice y **no** las borra ni las ignora, indicando que hay que pasar
   por la 0.1.1.
7. **Given** una base ya migrada por la 0.1.1, **When** la abre una versión anterior que no conoce
   ese esquema, **Then** aborta nombrando las dos versiones y **no** ofrece guardar credenciales de
   nuevo.

---

### User Story 3 — Recordar este dispositivo, y bloquear cuando quiera (Priority: P3)

Con «recordar este dispositivo» encendido, los arranques siguientes abren el vault sin preguntar
nada. «Bloquear» saca las claves de memoria y vuelve a pedir la clave maestra. «Olvidar este
dispositivo» borra lo que permitía el desbloqueo automático.

**Why this priority**: es la comodidad que el usuario pidió explícitamente, y es lo que hace
tolerable una clave maestra larga. No es P1 porque el producto funciona sin ella.

**Independent Test**: encenderla, cerrar y abrir CMC y comprobar que no pregunta; bloquear a mano
y comprobar que pregunta; olvidar el dispositivo y comprobar que vuelve a preguntar en cada
arranque.

**Acceptance Scenarios**:

1. **Given** el vault recién creado, **When** se ofrece recordar el dispositivo, **Then** no hay
   opción preseleccionada y la pantalla dice qué cambia al encenderla.
2. **Given** la pantalla de esa elección, **When** el usuario la cierra sin elegir, **Then** queda
   **apagada**.
3. **Given** «recordar» encendido, **When** CMC abre, **Then** el vault se desbloquea sin pedir
   nada.
4. **Given** «recordar» encendido, **When** el usuario bloquea a mano, **Then** el desbloqueo
   automático queda **desarmado** y el próximo desbloqueo pide la clave maestra, incluso sin
   cerrar CMC.
5. **Given** el vault desbloqueado, **When** el usuario bloquea a mano, **Then** no queda en
   memoria la clave del vault ni ninguna credencial ya descifrada.
6. **Given** «recordar» encendido en este equipo, **When** la base se copia a otra PC o la abre
   otro usuario de Windows, **Then** el desbloqueo automático no funciona, se pide la clave
   maestra, y eso **no** se presenta como un error.
7. **Given** «recordar» encendido, **When** el usuario elige «olvidar este dispositivo», **Then**
   lo guardado para el desbloqueo automático desaparece y cada arranque vuelve a preguntar.
8. **Given** el vault desbloqueado, **When** pasan horas de uso normal, **Then** sigue
   desbloqueado: no hay vencimiento por tiempo.

---

### User Story 4 — Llevarse el vault a otra computadora (Priority: P4)

Una copia de seguridad o una exportación restaurada en otra PC —u otro usuario de Windows— se abre
con sólo la clave maestra.

**Why this priority**: es la garantía que justifica todo el modelo de dos claves, y es lo que
convierte a la clave maestra en algo que vale la pena recordar. Va después de las tres anteriores
porque se ejercita rara vez, no porque importe menos.

**Independent Test**: hacer una copia en un perfil de Windows, restaurarla en otro, y abrirla con
la clave maestra sola.

**Acceptance Scenarios**:

1. **Given** un vault con credenciales y «recordar» encendido, **When** se hace una copia de
   seguridad o una exportación, **Then** el archivo **no** contiene nada que permita abrirlo sin
   la clave maestra.
2. **Given** esa copia, **When** se restaura bajo un usuario de Windows distinto, **Then** se abre
   con la clave maestra y todas las credenciales se leen.
3. **Given** esa copia, **When** se intenta abrir sin la clave maestra, **Then** no se abre por
   ningún camino.
4. **Given** un vault cuyo envoltorio de la clave del vault se corrompió, **When** se intenta
   abrir con la clave maestra correcta, **Then** se dice que el vault no se puede abrir y que la
   copia de seguridad es el camino, sin sugerir que la clave maestra esté mal.

---

### User Story 5 — Cambiar la clave maestra (Priority: P5)

El usuario cambia la clave maestra dando la actual y la nueva dos veces. Las credenciales no se
tocan.

**Why this priority**: no estaba en el pedido. Sale casi gratis del modelo de dos claves —cambiar
la clave maestra sólo vuelve a envolver la clave del vault— y sin ella una clave maestra
comprometida obliga a rehacer el vault a mano. Queda última a propósito.

**Independent Test**: cambiarla, cerrar CMC, abrir con la nueva, y comprobar que las credenciales
de antes siguen leyéndose y que la anterior ya no abre.

**Acceptance Scenarios**:

1. **Given** el vault desbloqueado, **When** el usuario cambia la clave maestra, **Then** se pide
   la actual, se exige la nueva dos veces y se le aplica la misma política de forma.
2. **Given** el cambio hecho, **When** CMC abre, **Then** la clave nueva abre el vault y la
   anterior no.
3. **Given** el cambio hecho, **When** se leen las credenciales, **Then** están todas: ninguna se
   recifró.
4. **Given** «recordar este dispositivo» encendido, **When** se cambia la clave maestra, **Then**
   lo guardado para el desbloqueo automático se rehace o se descarta, y en ningún caso queda
   apuntando a la clave anterior.
5. **Given** el cambio a mitad de camino y un corte, **When** CMC vuelve a abrir, **Then** abre
   con una de las dos claves —nunca con ninguna— y dice cuál quedó vigente.

---

### Edge Cases

- **La clave maestra se perdió.** No hay recuperación. La aplicación tiene que decirlo cuando se
  crea, no cuando se pierde. Lo único que queda es borrar el vault y volver a cargar las
  credenciales a mano; las conexiones y las carpetas no se pierden, porque no son secretos.
- **El vault está bloqueado y el usuario importa sesiones de PuTTY o WinSCP.** Las conexiones
  entran; las contraseñas quedan afuera y se dice cuántas y por qué.
- **El vault está bloqueado y el usuario quiere copiar una contraseña al portapapeles.** No se
  puede: se ofrece desbloquear.
- **Dos ventanas o dos hilos piden desbloquear a la vez.** Se pide una sola vez.
- **El blob del desbloqueo automático existe pero está corrupto.** Se pide la clave maestra, se
  descarta el blob y no se trata como error.
- **La base es de la 0.1.0 y el vault no existe todavía, pero el Administrador tampoco tiene
  credenciales.** Se crea el vault y no se informa ninguna migración: cero no es un fallo.
- **La derivación no se puede completar** en el equipo. Se dice por qué y no se baja el costo en
  silencio: con el costo bajado, el vault que se cifró con el costo alto no abre.
- **El usuario tipea una clave maestra con acentos, `ñ` o emoji.** Se acepta tal cual, y la misma
  clave abre el vault en otra máquina con otra configuración regional.

## Requirements

### Almacenamiento y cifrado

- **FR-200**: El sistema MUST guardar las contraseñas, las passphrases y el material de clave
  privada cifrados en la base local, y MUST NOT guardarlos en claro en ningún archivo.
- **FR-201**: El sistema MUST cifrar cada secreto con AES-256-GCM, con un nonce de 12 bytes
  generado al azar en cada cifrado, y MUST NOT reutilizar un nonce con la misma clave.
- **FR-202**: El sistema MUST verificar la etiqueta de autenticación antes de usar cualquier texto
  descifrado, y MUST informar el fallo en lugar de devolver un secreto vacío.
- **FR-203**: El sistema MUST cifrar las credenciales con una clave del vault de 32 bytes generada
  al azar una sola vez, y MUST NOT derivarla de la clave maestra.
- **FR-204**: El sistema MUST guardar la clave del vault envuelta con AES-256-GCM bajo la clave
  derivada de la clave maestra, y esa envoltura MUST ser lo único necesario, además de la clave
  maestra, para abrir el vault.
- **FR-205**: El sistema MUST derivar la clave que envuelve con PBKDF2-HMAC-SHA512 y al menos
  600.000 iteraciones.
- **FR-206**: El sistema MUST guardar la sal —de 16 bytes o más, al azar— y los parámetros de
  la derivación —función de hash e iteraciones— en la base y en claro, para que subir el costo más
  adelante no vuelva ilegible lo ya cifrado.
- **FR-207**: El sistema MUST comprobar la clave maestra descifrando un verificador guardado, y
  MUST NOT guardar un hash de la clave maestra con ningún propósito.
- **FR-208**: Cuando la derivación no se pueda completar, el sistema MUST informarlo y MUST NOT
  reducir los parámetros para seguir.
- **FR-209**: El sistema MUST NOT pasar la clave maestra a la derivación como `string`, y MUST usar
  la vía que escribe en un búfer que el propio sistema pueda pisar con ceros.

### La clave maestra

- **FR-210**: El sistema MUST pedirle al usuario que defina la clave maestra la primera vez que
  necesite un vault, y MUST exigir que la escriba dos veces iguales.
- **FR-211**: La clave maestra MUST tener 8 caracteres o más, con al menos una letra, al menos un
  dígito y al menos un carácter especial.
- **FR-212**: El sistema MUST mostrar los requisitos de forma antes de rechazar nada, y al
  rechazar MUST decir cuál falta sin borrar lo escrito.
- **FR-213**: El sistema MUST aceptar claves maestras de al menos 128 caracteres, MUST NOT
  recortar lo tipeado y MUST NOT rechazar ningún carácter Unicode, incluido el espacio.
- **FR-214**: El sistema MUST mostrar una indicación de fuerza de lo tipeado y MUST sugerir una
  frase larga, aceptando de todos modos el mínimo de FR-211.
- **FR-215**: El sistema MUST advertir, al crear la clave maestra, que perderla es irrecuperable y
  que no hay forma de recuperar las credenciales sin ella.
- **FR-216**: El sistema MUST NOT persistir la clave maestra tipeada en ningún medio: ni en la
  base, ni en la configuración, ni en un archivo temporal, ni en el Administrador de credenciales,
  ni bajo DPAPI.
- **FR-217**: El sistema MUST NOT escribir la clave maestra ni la clave del vault en ningún
  registro, mensaje de error, volcado de excepción ni elemento de la interfaz.
- **FR-218**: El sistema MUST pisar con ceros los búferes de la clave maestra y de la clave del
  vault al bloquear y al cerrar, incluido el camino de excepción.
- **FR-219**: Cuando el usuario cancele el pedido de la clave maestra, el sistema MUST abrir de
  todos modos, MUST mostrar lo que no es secreto —conexiones, carpetas, ajustes— y MUST decir qué
  queda sin funcionar.
- **FR-220**: El sistema MUST NOT ofrecer ninguna forma de seguir sin cifrado.
- **FR-221**: Cuando dos partes de la aplicación necesiten el vault a la vez, el sistema MUST
  pedir la clave maestra una sola vez.

### Sesión desbloqueada, bloqueo y recordar el dispositivo

- **FR-230**: El sistema MUST mantener el vault desbloqueado mientras la aplicación siga abierta,
  y MUST NOT vencer el desbloqueo por tiempo.
- **FR-231**: El sistema MUST ofrecer bloquear el vault a mano, y al bloquear MUST quitar de
  memoria la clave del vault y toda credencial ya descifrada.
- **FR-232**: El sistema MAY guardar la clave del vault protegida con DPAPI en el ámbito del
  usuario actual, para desbloquear sin preguntar en los arranques siguientes.
- **FR-233**: Lo guardado según FR-232 MUST ser la clave del vault y MUST NOT ser la clave maestra
  ni nada derivado de ella que permita probarla.
- **FR-234**: El sistema MUST ofrecer la elección de FR-232 al crear el vault, sin opción
  preseleccionada, explicando que con ella encendida cualquier proceso que corra como ese usuario
  de Windows puede llegar a las credenciales sin la clave maestra.
- **FR-235**: Cuando el usuario cierre esa elección sin elegir, el sistema MUST dejarla apagada.
- **FR-236**: El sistema MUST permitir encenderla y apagarla en cualquier momento después.
- **FR-237**: Lo guardado según FR-232 MUST vivir fuera del archivo del vault y MUST NOT incluirse
  en ninguna copia de seguridad ni exportación.
- **FR-238**: Cuando el usuario bloquee a mano, el sistema MUST desarmar el desbloqueo automático
  hasta que se vuelva a tipear la clave maestra.
- **FR-239**: Cuando el desbloqueo automático falle —otro usuario de Windows, otra máquina, perfil
  recreado, dato corrupto—, el sistema MUST pedir la clave maestra y MUST NOT presentarlo como un
  error.
- **FR-240**: El sistema MUST ofrecer «olvidar este dispositivo», que borra lo guardado según
  FR-232 sin tocar el vault.

### Portabilidad, copias y cambio de clave

- **FR-250**: Una copia de seguridad o una exportación MUST poder abrirse en otra máquina y bajo
  otro usuario de Windows con sólo la clave maestra.
- **FR-251**: Una copia de seguridad o una exportación MUST NOT contener la clave maestra, la
  clave derivada ni lo guardado según FR-232.
- **FR-252**: El sistema MUST permitir cambiar la clave maestra pidiendo la actual y la nueva dos
  veces, aplicándole a la nueva la política de FR-211 a FR-213.
- **FR-253**: Al cambiar la clave maestra, el sistema MUST volver a envolver la clave del vault y
  MUST NOT recifrar las credenciales.
- **FR-254**: Al cambiar la clave maestra con el desbloqueo automático encendido, el sistema MUST
  rehacer o descartar lo guardado según FR-232, y MUST NOT dejarlo atado a la clave anterior.
- **FR-255**: Si el cambio de clave maestra se interrumpe, el sistema MUST quedar abrible con
  exactamente una de las dos claves y MUST decir cuál quedó vigente.
- **FR-256**: Cuando la envoltura de la clave del vault no se pueda descifrar con una clave maestra
  que el verificador acepta, el sistema MUST informar que el vault está dañado y señalar la copia
  de seguridad, sin sugerir que la clave maestra sea incorrecta.

### Migración desde el Administrador de credenciales

- **FR-260**: La versión 0.1.1 MUST migrar, en su primera ejecución con el vault ya creado, las
  credenciales `cmc:*` del Administrador de credenciales al vault.
- **FR-261**: El sistema MUST leer y descifrar cada credencial desde el vault antes de borrarla
  del Administrador de credenciales.
- **FR-262**: Cuando una credencial no se pueda leer del Administrador, el sistema MUST informar
  cuál y por qué, MUST NOT borrarla y MUST seguir con las demás.
- **FR-263**: El sistema MUST informar al terminar cuántas credenciales se trajeron y cuántas
  quedaron pendientes con su motivo.
- **FR-264**: Una migración interrumpida MUST poder retomarse en el arranque siguiente sin
  duplicar ni perder credenciales.
- **FR-265**: Si el usuario no crea la clave maestra, el sistema MUST NOT migrar ni borrar nada.
- **FR-266**: Desde la versión 0.1.2 el migrador MUST NOT existir.
- **FR-267**: Una versión sin migrador que encuentre credenciales `cmc:*` en el Administrador de
  credenciales MUST informarlo indicando que hay que pasar por la 0.1.1, y MUST NOT borrarlas ni
  continuar en silencio.
- **FR-268**: Al abrir una base cuya versión de esquema es mayor que la última que la aplicación
  conoce, el sistema MUST abortar con un mensaje que nombre las dos versiones, y MUST NOT continuar.
  Hoy no lo hace: `DatabaseInitializer.Migrate()` no encuentra migraciones que aplicar y retorna
  normalmente, así que una versión vieja abre una base nueva, no ve las tablas del vault, concluye
  que ninguna conexión tiene credencial y **vuelve a escribir secretos en el Administrador de
  credenciales**.

### Lo que el vault no alcanza

- **FR-270**: Una conexión que no necesite un secreto guardado —RDP con la identidad de Windows,
  SSH por clave sin passphrase— MUST funcionar con el vault bloqueado.
- **FR-271**: La contraseña de `sudo` por sesión MUST seguir viviendo sólo en memoria y MUST NOT
  guardarse en el vault.
- **FR-272**: La credencial de una conexión rápida MUST seguir viviendo sólo en memoria mientras
  la conexión no se guarde.
- **FR-273**: Con el vault bloqueado, importar sesiones de PuTTY, WinSCP o FileZilla MUST crear
  las conexiones y MUST informar cuántas contraseñas quedaron sin traer y por qué.
- **FR-274**: Con el vault bloqueado, copiar al portapapeles el usuario y la contraseña efectivos
  MUST ofrecer desbloquear en lugar de fallar sin explicación.
- **FR-275**: El sistema MUST NOT pasar una contraseña por la línea de comandos a ningún proceso
  externo, y `-pw` y `-pwfile` de PuTTY MUST seguir sin usarse.
- **FR-276**: Las preferencias MUST listar las credenciales guardadas en el vault, en reemplazo del
  listado de claves `cmc:*` del Administrador que definía FR-158.

### Key Entities

- **Vault**: el conjunto cifrado de credenciales dentro de la base local. Tiene una clave del vault
  envuelta, una sal, los parámetros de la derivación y un verificador.
- **Clave del vault**: 32 bytes al azar. Cifra las credenciales. Existe envuelta en la base y, si
  el usuario lo eligió, envuelta también por DPAPI fuera de la base.
- **Clave maestra**: lo que el usuario tipea. No se guarda. Sólo produce la clave que envuelve.
- **Credencial**: usuario, dominio opcional, y el secreto cifrado con su nonce. Referenciada desde
  una conexión o una carpeta con la misma semántica de herencia que ya existe.
- **Marca de dispositivo recordado**: lo que permite el desbloqueo automático. Vive fuera del
  vault, no entra en las copias, y se borra al olvidar el dispositivo.

## Success Criteria

- **SC-060**: Un usuario nuevo crea el vault, guarda una credencial y reconecta con ella tras
  reiniciar CMC, sin leer documentación.
- **SC-061**: El archivo de la base, copiado a otra máquina, no revela ninguna credencial sin la
  clave maestra: se verifica intentando abrirlo por todos los caminos que la aplicación ofrece.
- **SC-062**: Una copia de seguridad restaurada bajo un usuario de Windows distinto del que la
  generó se abre con sólo la clave maestra y entrega todas las credenciales.
- **SC-063**: Con el desbloqueo automático encendido, CMC llega a la ventana principal sin pedir
  nada; con él apagado, siempre pide la clave maestra.
- **SC-064**: Bloquear a mano deja el proceso sin la clave del vault y sin ninguna credencial
  descifrada en memoria, verificado sobre un volcado del proceso.
- **SC-065**: Ni la clave maestra ni la clave del vault aparecen en los archivos de Serilog, en la
  consola de traza ni en el texto de un error forzado, buscando sus valores conocidos.
- **SC-066**: Las credenciales de una instalación de la 0.1.0 quedan todas en el vault tras abrir
  la 0.1.1, y en el Administrador de credenciales no queda ninguna `cmc:*`.
- **SC-067**: Una migración cortada por la mitad y retomada deja exactamente las mismas
  credenciales que una migración corrida de una vez.
- **SC-068**: Cambiar la clave maestra deja las credenciales legibles y la clave anterior sin
  servir, sin recifrar ninguna credencial.
- **SC-069**: Una clave maestra que no cumple FR-211 nunca se acepta, y el motivo se muestra antes
  del rechazo.
- **SC-070**: Con el vault bloqueado, CMC abre, muestra el árbol y conecta una sesión que no
  necesita secreto guardado.
- **SC-071**: Una versión que no conoce el esquema del vault, abierta contra una base ya migrada,
  aborta con un mensaje que nombra las dos versiones y no escribe ninguna credencial en el
  Administrador de credenciales.

## Assumptions

- **Dos instalaciones en total.** El usuario lo confirmó. Por eso el migrador no necesita cubrir
  saltos de versión arbitrarios: cubre 0.1.0 → 0.1.1 y muere en 0.1.2. El detector de FR-267 se
  conserva igual porque cuesta un mensaje.
- **El desbloqueo automático se elige explícitamente y su valor a prueba de fallos es apagado.**
  El pedido decía «intentar desbloquear automáticamente usando DPAPI», que admite dos lecturas
  —encendido de fábrica, o encendido por el usuario—. Se preguntó y no hubo respuesta, así que se
  eligió la que no decide la postura de seguridad por el usuario: se elige al crear el vault, sin
  nada preseleccionado, y si no elige queda apagado. Cambiarlo a encendido de fábrica es un
  cambio de FR-235 y de un valor por omisión, nada más.
- **Cambiar la clave maestra (US5) no estaba en el pedido.** Se agrega porque el modelo de dos
  claves la hace casi gratis y porque sin ella una clave maestra comprometida obliga a rehacer el
  vault a mano. Es la prioridad más baja y se puede sacar sin tocar nada de las otras cuatro.
- **No hay vencimiento del desbloqueo por inactividad.** El pedido dice «mantener la sesión
  desbloqueada durante largo tiempo». Un vencimiento configurable sería alcance nuevo.
- **Cero dependencias nuevas.** Un borrador fijaba Argon2id, que no viene en .NET 10 y exigía un
  paquete de terceros. El usuario lo rechazó: «si no está en .NET 10 usá otra cosa, bajá el nivel de
  seguridad, no es un sistema de la NASA». Queda PBKDF2-HMAC-SHA512 del BCL, y DPAPI por P/Invoke
  como ya hace `CredentialManagerNative.cs`. El costo de seguridad de ese cambio está medido en el
  `research.md` del plan: a igual tiempo de desbloqueo, PBKDF2 le sale a un atacante con GPU entre
  dos y tres órdenes de magnitud más barato por intento. **Lo que decide la seguridad de este modelo
  es el largo de la clave maestra**, no el KDF, y por eso FR-214 no es un adorno.
- **Las conexiones, las carpetas y los ajustes no son secretos** y siguen legibles con el vault
  bloqueado. Es lo que sostiene FR-219 y FR-270.
- **La base sigue siendo SQLite en el perfil del usuario.** Esta feature no cambia dónde vive ni
  cómo se migra el esquema.
- **El ámbito de DPAPI es `CurrentUser` y no `LocalMachine`.** Con `LocalMachine` cualquier cuenta
  del equipo desenvolvería la clave del vault, que es exactamente lo que FR-234 advierte.
