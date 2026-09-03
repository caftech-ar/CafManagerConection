# Feature Specification: Procesos, registros y árbol

**Feature Branch**: `002-procesos-registros-y-arbol`

**Created**: 2026-09-02

**Status**: planificada

**Input**: los pedidos del usuario del 2026-09-01 y del 2026-09-02. Los requisitos conservan la
numeración con la que nacieron en `specs/001-rdp-ssh-server-manager/spec.md`; ese documento los
apunta y no los repite. De dónde viene la autorización de cada grupo está abajo: no es de un solo
lugar.

## Por qué esta feature existe aparte de la 001

La 001 es el producto: unos 300 requisitos, 33 fases y un `tasks.md` de 142 KB que es el registro de
lo construido. Los requisitos de acá son lo único de esa tanda que **no** está construido.
Separarlos deja un plan que se puede leer y ejecutar, y deja la 001 como lo que ya es: el acta de lo
que existe.

**De dónde sale cada grupo**, porque no es de un solo lugar:

- Panel de procesos, escalada con `sudo`, registros en vivo, RDP con la identidad de Windows y en
  ventana propia, y clave privada a las herramientas externas: **enmienda 1.13.0**, que los nombra
  uno por uno. La transferencia recursiva por SFTP también, pero **no es ninguno de los requisitos
  de acá**: FR-189 la deja donde está, en FR-073 desde la enmienda 1.2.0.
- Elegir la forma del icono (FR-195, FR-195a, FR-195c) y pedirle al usuario una contraseña de
  `sudo` (FR-184e): **enmienda 1.14.0**. El icono entra como alcance nuevo, «Icono elegible» dentro
  del Principio V (`constitution.md:772`), y arrastra la migración 006. La contraseña de `sudo`
  entra como **excepción acotada al Principio II**, declarada en el título de esa misma enmienda
  (`constitution.md:527`) y sujeta a las cinco reglas que FR-184e transcribe.
- Orden del árbol, color del icono, icono por tipo de archivo del explorador (FR-189b), etiqueta
  desde el menú contextual y distribución de las ventanas: **no necesitan enmienda**. La
  constitución dice que «colorear y ordenar lo que ya se muestra NO es ampliar el alcance»
  (`constitution.md:783`), y ninguno trae un dato nuevo del servidor ni una dependencia.
- **Defectos de requisitos ya construidos**, que no necesitan enmienda porque el requisito ya está
  en alcance y lo que falla es cómo se cumplió: FR-173d es un defecto de FR-173 —así lo registra
  T510 de `001/tasks.md`, que lo movió a esta feature en lugar de arreglarlo allá—; FR-183d
  acota qué le toca al top de FR-173 y qué al panel nuevo; FR-189, FR-189a y FR-189c son defecto y
  presentación de FR-073 a FR-078, ya en alcance: el árbol y la confirmación de destino son cómo se
  muestra una transferencia que ya está exigida, y omitir los enlaces simbólicos es cerrar lo que
  FR-078 prohíbe (`001/spec.md`); FR-193a y FR-194 son defectos del orden que FR-005 ya exige
  conservar (`001/spec.md`).

Lo que sí quedó hecho de la misma tanda se queda en la 001 y no se repite acá: FR-026a (juego gráfico
de DEC en el terminal), FR-182 y familia (importación de PuTTY, WinSCP y FileZilla), FR-191 (colores
de consola declarados como recursos) y FR-192 (borrar y editar un túnel).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Ver qué consume el servidor, y mirar con privilegios cuando haga falta (Priority: P1)

Como administrador, quiero abrir un panel de procesos del servidor, ordenarlo por CPU o por memoria,
desplegar los hijos del que sospecho y ver su entrada y salida de disco, para encontrar la fuga sin
abrir una sesión y tipear `htop`. Y cuando un panel me muestre menos de lo que hay porque mi usuario
no alcanza, quiero un botón que reintente con privilegios, y que ese botón no aparezca cuando el
servidor no me los va a dar.

**Why this priority**: es el pedido que más veces volvió y el que resuelve el trabajo real —encontrar
qué se comió la memoria— sin salir de la aplicación. La escalada va junta porque sin ella el panel de
puertos y el de procesos muestran la mitad en cualquier servidor donde el usuario no es root, y esa
mitad se ve igual que un servidor tranquilo.

**Independent Test**: contra un servidor con un proceso que consume de más, el panel lo pone primero
al ordenar por CPU y muestra sus hijos y su E/S. Contra un servidor donde el usuario no puede usar
`sudo`, ningún panel ofrece escalar y todos dicen por qué.

**Acceptance Scenarios**:

1. **Given** una sesión SSH abierta, **When** el usuario abre el panel de procesos y ordena por
   memoria, **Then** el primero de la lista es el de mayor RSS y la lista se refresca en el intervalo
   elegido sin volver a ordenarse sola.
2. **Given** un proceso con hijos, **When** el usuario lo despliega, **Then** ve sus descendientes
   directos con su propio consumo, y el consumo del padre no los incluye dos veces.
3. **Given** un proceso recién arrancado que está consumiendo el 90 % de un núcleo, **When** se ordena
   por CPU, **Then** aparece arriba, aunque un proceso viejo tenga un promedio de vida mayor.
4. **Given** un servidor donde el usuario tiene `sudo` sin contraseña, **When** se abre la sesión,
   **Then** el sondeo se hace una sola vez y los paneles que lo necesiten muestran el botón de
   reintentar con privilegios.
5. **Given** un servidor donde el usuario no está en `sudoers`, **When** se abre la sesión, **Then**
   ningún panel muestra ese botón y el panel de puertos dice que no puede ver los procesos ajenos
   porque el usuario no puede escalar.
6. **Given** el panel de procesos abierto, **When** el usuario busca una acción para matar o cambiar
   la prioridad de un proceso, **Then** no existe ninguna.
7. **Given** el panel de estado abierto, **When** el usuario mira su top de diez, **Then** sigue
   mostrando las siete columnas de FR-173 —PID, usuario, CPU, memoria, hilos, estado y tiempo
   corriendo—, el porcentaje de CPU es el instantáneo, y hay cómo saltar al panel de procesos
   (FR-173d, FR-183d).
8. **Given** un servidor donde `sudo` pide contraseña y la de la conexión sirve, **When** un panel
   escala, **Then** entra sin preguntarle nada al usuario (FR-184e).
9. **Given** un servidor donde `sudo` pide contraseña y la de la conexión **no** sirve —una conexión
   por clave SSH, por ejemplo—, **When** un panel intenta escalar, **Then** el sistema le pide al
   usuario una contraseña de `sudo`, la usa por la entrada estándar y la conserva en memoria para el
   resto de esa sesión, sin volver a pedirla en el panel siguiente (FR-184e).
10. **Given** ese mismo pedido en pantalla, **When** el usuario lo cancela o la contraseña que
    escribe tampoco sirve, **Then** el sistema declara la escalada imposible y dice por qué, y el
    texto que muestra no contiene lo que el usuario escribió (FR-184e, regla 3).
11. **Given** una sesión en la que el usuario ya escribió la contraseña de `sudo`, **When** cierra
    esa sesión y vuelve a abrir la misma conexión, **Then** el sistema se la pide de nuevo: no
    quedó guardada en ningún lado (FR-184e, reglas 1 y 5).

---

### User Story 2 - Enterarme de lo que pasa en un registro sin quedarme mirándolo (Priority: P2)

Como administrador, quiero que cualquier visor de registro siga el archivo en vivo, me diga qué
archivos está mirando y cuándo cambió cada uno, me deje forzar una lectura, y me avise cuando aparece
un error o cuando el registro deja de llegar.

**Why this priority**: hoy sólo el registro de un contenedor se sigue en vivo; el de supervisord es
una foto que hay que cerrar y volver a pedir. Y un registro congelado se ve exactamente igual que un
servidor tranquilo, que es el modo de fallar más caro que tiene un visor.

**Independent Test**: con un visor abierto, escribir una línea en el archivo del servidor la hace
aparecer sin tocar nada; borrar el archivo produce un aviso.

**Acceptance Scenarios**:

1. **Given** un visor de registro abierto sobre un proceso de supervisord, **When** el proceso escribe
   una línea, **Then** aparece en el visor sin que el usuario cierre y vuelva a abrir la ventana.
2. **Given** cualquier visor de registro abierto, **When** el usuario lo mira, **Then** ve la ruta de
   cada archivo que se está siguiendo y cuándo cambió por última vez.
3. **Given** un visor siguiendo un archivo, **When** el archivo se borra o rota en el servidor,
   **Then** el visor lo dice en el lugar del contenido, y no se queda mostrando lo último que leyó
   como si siguiera vigente.
4. **Given** un visor siguiendo un archivo, **When** aparece una línea de error, **Then** el visor la
   señala aunque el usuario esté mirando otra pestaña.
5. **Given** un visor que perdió el canal SSH, **When** el usuario aprieta forzar lectura, **Then** el
   visor lo reintenta y dice si pudo o no.
6. **Given** la configuración efectiva de nginx abierta en su ventana modal, **When** el usuario la
   mira, **Then** es texto estático: no se refresca solo, no declara ningún archivo seguido y no
   ofrece forzar lectura, aunque comparta la ventana con el visor de supervisord (FR-185e).

---

### User Story 3 - Ordenar el árbol como quiero y reconocer cada servidor de un vistazo (Priority: P3)

Como administrador con decenas de conexiones, quiero arrastrarlas al orden que me sirve —también las
carpetas—, pedirle a una carpeta que ordene su contenido alfabéticamente, elegir el icono y el color
de cada cosa, y cambiar la etiqueta desde el menú contextual sin abrir el editor entero.

**Why this priority**: es lo que se toca todos los días. Hoy sólo se reordenan las conexiones entre
sí, una carpeta nueva cae siempre al final, el glifo lo decide el protocolo y cambiar una etiqueta
obliga a abrir una ventana modal con cuatro pestañas.

**Independent Test**: reordenar una carpeta y una conexión, cerrar la aplicación y volver a abrirla:
las dos siguen donde quedaron.

**Acceptance Scenarios**:

1. **Given** una carpeta con tres conexiones, **When** el usuario arrastra la tercera entre la primera
   y la segunda, **Then** queda ahí, y sigue ahí después de reiniciar la aplicación.
2. **Given** dos carpetas hermanas, **When** el usuario arrastra la segunda arriba de la primera,
   **Then** cambian de orden. Hoy no se puede.
3. **Given** una carpeta con su contenido desordenado a mano, **When** el usuario elige «ordenar
   alfabéticamente» en su menú contextual, **Then** se ordenan sus hijos directos y no el contenido de
   las carpetas que cuelgan de ella.
4. **Given** una conexión nueva o importada, **When** aparece en el árbol, **Then** está en su lugar
   alfabético dentro de su carpeta y no al final.
5. **Given** una conexión dentro de una carpeta, **When** el usuario la arrastra a otra carpeta y el
   guardado falla, **Then** el árbol vuelve a mostrarla donde estaba y dice por qué, en lugar de
   quedar mostrada en la carpeta nueva para volver sola en el refresco siguiente.
5b. **Given** una conexión, **When** el usuario la arrastra a otra carpeta, **Then** se le pide
   confirmación con la ruta completa del destino; **When** en cambio la acomoda entre sus hermanas
   de la misma carpeta, **Then** no se le pregunta nada.
6. **Given** una carpeta con icono y color propios, **When** el usuario le pone un icono distinto a dos
   conexiones que cuelgan de ella, **Then** cada una conserva el suyo y ninguna toma el de la carpeta.
7. **Given** una conexión en el árbol, **When** el usuario abre su menú contextual, **Then** puede
   elegir una etiqueta de la lista sin abrir el editor.
8. **Given** la ventana de configuración de una carpeta, **When** el usuario abre la pestaña «Acceso»,
   **Then** los campos están agrupados en secciones con título y no apilados en una columna a lo alto
   (FR-196).
9. **Given** la ventana de configuración de una conexión, **When** el usuario la compara con la de
   una carpeta, **Then** los grupos que las dos tienen se llaman igual, están en el mismo orden
   dentro de la pestaña y se ven con la misma forma de recuadro, aunque las pestañas no sean las
   mismas (FR-196a).
10. **Given** el selector de icono abierto, **When** el usuario lo recorre, **Then** encuentra al
    menos un icono para base de datos, web, correo, archivos, respaldo, contenedor, cortafuegos y
    monitoreo, además de los genéricos (FR-195a), y el paquete de la aplicación no incorporó ninguna
    biblioteca nueva para dibujarlos (FR-195c).

---

### User Story 4 - Mover archivos navegando el árbol remoto (Priority: P4)

Como administrador, quiero ver el árbol de directorios del servidor, bajar un archivo o una carpeta
eligiendo dónde guardarlos, y subir confirmando a qué directorio remoto van a parar.

**Why this priority**: el panel de archivos existe pero es una lista plana con una caja de texto para
la ruta, y subir manda al directorio abierto sin preguntar nada, que es la forma más barata de dejar
un archivo en el lugar equivocado de un servidor de producción.

**Independent Test**: un directorio de al menos 50 archivos y 3 niveles sube y baja completo, y el
árbol refleja lo que hay del otro lado.

**Acceptance Scenarios**:

1. **Given** una sesión SSH abierta, **When** el usuario abre el panel de archivos, **Then** ve un
   árbol de directorios que puede desplegar sin escribir rutas.
2. **Given** un archivo en el árbol remoto, **When** el usuario pide bajarlo, **Then** se le pregunta
   dónde guardarlo antes de empezar.
3. **Given** un directorio abierto en el árbol, **When** el usuario pide subir, **Then** se le confirma
   a qué directorio remoto va antes de transferir nada.
4. **Given** un directorio remoto con archivos de varios tipos y algún enlace simbólico, **When** se
   lista, **Then** cada tipo se distingue por icono y color, ningún enlace aparece como un archivo
   común, y el listado dice cuántos omitió (FR-189b, FR-189c).

---

### User Story 5 - Trabajar cómodo en una sesión RDP (Priority: P5)

Como administrador en un equipo de dominio, quiero abrir un servidor Windows con las credenciales con
las que ya estoy logueado, sin escribir ni guardar ninguna; y quiero poder maximizar la sesión dentro
de la aplicación o sacarla a una ventana propia para usarla en el otro monitor.

**Why this priority**: no bloquea a nadie —la conexión funciona—, pero la contraseña que no se guarda
es la que no se puede filtrar, y una sesión RDP encajada en una pestaña con el árbol al costado es
incómoda justo en el caso en que RDP se usa de verdad, que es trabajar un rato largo.

**Independent Test**: una sesión sale a una ventana propia y vuelve a su pestaña sin reconectarse; el
tiempo conectado no se reinicia.

**Acceptance Scenarios**:

1. **Given** un equipo unido al dominio y un servidor que confía en él, **When** el usuario abre una
   conexión RDP marcada para usar la identidad de Windows, **Then** entra sin que se le pida nada y sin
   que se guarde ninguna credencial.
2. **Given** un equipo fuera del dominio, **When** el usuario abre esa misma conexión, **Then** se le
   piden las credenciales como siempre y la conexión no falla.
3. **Given** una sesión RDP conectada, **When** el usuario la saca a una ventana propia, **Then** la
   sesión sigue viva, no se reconecta, y la pestaña queda reservada.
4. **Given** una sesión RDP en su ventana propia, **When** el usuario cierra esa ventana, **Then** la
   sesión vuelve a su pestaña en lugar de cortarse.
5. **Given** una sesión RDP en su pestaña, **When** el usuario la maximiza dentro de la aplicación,
   **Then** el escritorio remoto ocupa toda la ventana sin el árbol al costado, y volver la deja como
   estaba sin reconectar (FR-187).

---

### User Story 6 - Abrir la herramienta externa sin que me pida lo que ya sabe (Priority: P6)

Como administrador que a veces necesita WinSCP o FileZilla, quiero que se abran apuntando al servidor
y con la clave privada ya cargada, y que cuando la conexión sea por contraseña se me diga que la
herramienta la va a pedir, en lugar de descubrirlo cuando aparece el cuadro.

**Why this priority**: es lo más chico de la feature y ya funciona a medias —PuTTY recibe la clave,
las otras dos no—, pero es el pedido que más rápido se paga.

**Independent Test**: abrir WinSCP desde una conexión con clave privada entra sin preguntar nada.

**Acceptance Scenarios**:

1. **Given** una conexión SSH con clave privada, **When** el usuario abre WinSCP desde el menú
   contextual, **Then** WinSCP entra sin pedir la clave.
2. **Given** una conexión SSH con contraseña, **When** el usuario abre cualquiera de las tres
   herramientas, **Then** el sistema le dice que la herramienta va a pedir la contraseña, y no intenta
   pasársela por ningún medio.

---

### Edge Cases

- **El sondeo de `sudo` en un servidor donde el usuario no está en `sudoers`** deja una línea en el
  registro del sistema y, con la configuración por omisión, dispara un correo a root. Por eso se
  sondea una sola vez por sesión y nunca por panel.
- **Un proceso que termina entre las dos muestras** del cálculo de CPU instantáneo: no se informa un
  porcentaje negativo ni se lo deja en la lista con el valor de la muestra anterior.
- **Un servidor con miles de procesos**: se pide el conjunto acotado que se muestra, no la tabla
  entera.
- **Un archivo de registro que rota** mientras se lo sigue: el visor lo dice y vuelve a engancharse al
  archivo nuevo, en lugar de seguir leyendo un descriptor que ya no crece.
- **Una carpeta con un solo hijo**: «ordenar alfabéticamente» no hace nada, y no por eso queda
  deshabilitada.
- **Arrastrar una carpeta dentro de sí misma o de un descendiente suyo**: se rechaza.
- **El control ActiveX de RDP tiene afinidad de hilo**: sacarlo a una ventana propia y devolverlo es
  reparentar un control de WinForms alojado. Si eso no se puede hacer sin reconectar, el requisito no
  se cumple con una reconexión disimulada: se informa.
- **FileZilla no acepta una clave privada por línea de comandos.** Se le pasa el destino y se avisa que
  la va a pedir; no se escribe en su configuración, que FR-182d prohíbe.

## Requirements *(mandatory)*

### Functional Requirements

#### Panel de procesos y escalada con `sudo` (US1)

- **FR-183**: El sistema MUST ofrecer un panel de procesos del servidor, ordenable por CPU y por
  memoria, con los hijos del proceso elegido y su entrada y salida de disco.
- **FR-183a**: El panel MUST ser de **sólo lectura**. MUST NOT ofrecer matar, señalar ni cambiar la
  prioridad de ningún proceso.
- **FR-183b**: El porcentaje de CPU MUST ser el **instantáneo**, calculado por diferencia entre dos
  muestras. El promedio de vida que informa `ps` MUST NOT usarse para ordenar: muestra el proceso
  más viejo y ocupado históricamente, no el que está consumiendo ahora.
- **FR-183c**: El panel MUST NOT persistir historial, MUST NOT recolectar con la aplicación cerrada
  y MUST NOT tener alertas con umbral configurable.
- **FR-183d**: El panel de procesos MUST NOT reemplazar al top de diez del panel de estado, que es
  un resumen de una sola muestra dentro de una pantalla que además tiene discos, red y temperaturas.
  El top MUST conservar las siete columnas que FR-173 le exige —PID, usuario, CPU, memoria residente,
  hilos, estado y tiempo corriendo—: FR-173 está en alcance y construido, y este requisito no lo
  deroga. Lo que el top MUST NOT hacer es agregar las columnas propias del panel de procesos —la
  jerarquía de hijos y la entrada y salida por proceso—, y MUST poder llevar a ese panel.
- **FR-173d**: El porcentaje de CPU del top MUST calcularse igual que en el panel de procesos
  (FR-183b), por diferencia entre dos muestras. Motivo: el `%CPU` que informa `ps` es el promedio de
  toda la vida del proceso, así que el top ordenado por ese valor muestra el proceso más viejo y
  ocupado históricamente y no el que está consumiendo ahora, que es lo único que se mira en un panel
  de estado.
- **FR-184**: Al conectar, el sistema MUST determinar si el usuario remoto puede escalar con `sudo`
  sin contraseña.
- **FR-184a**: Los paneles que muestren menos por falta de permiso MUST ofrecer reintentar con
  privilegios cuando la escalada sea posible, y MUST decir que no se puede cuando no lo sea. MUST
  NOT mostrar un botón que se sabe que va a fallar.
- **FR-184b**: Lo que se ejecute con privilegios **por la escalada de FR-184a** MUST ser de sólo
  lectura. Las acciones de escritura que ya están en alcance —iniciar, detener y reiniciar
  contenedores y procesos de supervisord, FR-100a y FR-150— conservan su confirmación explícita y
  su propio camino de escalada (FR-095); este requisito no las alcanza.
- **FR-184c**: El sondeo de `sudo` MUST hacerse **una sola vez por sesión**, con `sudo -n`, y su
  resultado MUST quedar disponible para todos los paneles de esa sesión. MUST NOT repetirse por panel
  ni por comando. Motivo: un `sudo` fallido de un usuario que no está en `sudoers` deja una línea en
  el registro del servidor y, con la configuración por omisión de `sudoers`, manda un correo a root;
  repetirlo por panel convierte un sondeo en una alarma de seguridad.
- **FR-184d**: El sondeo MUST distinguir tres resultados: puede escalar sin contraseña, puede escalar
  pero se la piden, y no puede escalar. El tercero MUST decirse con esas palabras y MUST NOT
  confundirse con el segundo: «no estás en sudoers» y «sudo te va a pedir la contraseña» exigen cosas
  distintas del usuario.
- **FR-184e**: Cuando `sudo` pida contraseña, el sistema MUST reintentar primero con la
  **contraseña de la conexión** por la entrada estándar (`sudo -S -k`), que es el camino que FR-095
  ya autoriza y que `SshCommandRunner.cs:213` ya implementa. Cuando esa no sirva —el caso de una
  conexión por clave SSH—, el sistema MAY pedirle al usuario una contraseña de `sudo` y conservarla
  en memoria mientras esa sesión esté abierta. Si el usuario cancela el pedido, o si la contraseña
  que escribe tampoco sirve, la escalada MUST declararse imposible.

  Esto es la excepción al Principio II declarada en el título de la enmienda 1.14.0
  (`constitution.md:527`), y vale sólo para esto. Las cinco reglas que la acotan son requisitos:

  1. La contraseña de `sudo` MUST NOT persistirse en ningún lado: ni en SQLite, ni en la
     configuración, ni en un archivo temporal, ni en el Administrador de credenciales de Windows.
  2. MUST NOT pasarse por la línea de comandos. Va por la entrada estándar del proceso remoto, como
     el `sudo -S -k` de `SshCommandRunner.cs:213`. Motivo: en el servidor, la línea de comandos de
     un proceso la lee cualquiera con un `ps`.
  3. MUST NOT aparecer en ningún registro, mensaje de error ni volcado de excepción, por crítico que
     sea el fallo. Es lo que verifica SC-052.
  4. MUST borrarse del búfer al cerrar la sesión, pisándolo con ceros antes de soltarlo. El patrón a
     seguir es `TomarTexto()` de `src/CafManagerConection.Ssh/EntradaDeContrasenaInteractiva.cs:52`,
     que pisa con ceros la lista y **también** la copia que devuelve `ToArray` (líneas 58 a 63):
     limpiar sólo una de las dos deja el secreto en la otra.
  5. MUST vivir **por sesión y no por conexión**: cerrar la sesión y volver a abrir la misma conexión
     MUST volver a pedirla. Guardarla para la próxima sesión exigiría otra enmienda.

#### Seguimiento de registros en vivo (US2)

- **FR-185**: Todo visor de registro MUST poder seguir el archivo en vivo, no sólo el de
  contenedores.
- **FR-185a**: Todo visor MUST mostrar **qué archivos está monitoreando** y cuándo cambió cada uno
  por última vez.
- **FR-185b**: Todo visor MUST ofrecer forzar una lectura.
- **FR-185c**: El sistema MUST avisar cuando aparezca una línea de error, cuando el archivo deje de
  poder leerse y cuando se corte el canal. Un registro congelado y un servidor tranquilo se ven
  igual, y distinguirlos es la razón de este requisito.
- **FR-185d**: El alcance de «todo visor» son **dos**: el registro de un contenedor
  (`ContenedorWindow`) y el de un proceso de supervisord (`TextViewerWindow`, abierto desde
  `PanelesPlataforma.cs:816`). El de supervisord es el único que hoy es una lectura única. La consola
  de traza de la aplicación (`ConsolaDeTraza`) queda **fuera de FR-185, FR-185a y FR-185b**: no sigue
  archivos sino comandos, no tiene ruta que declarar y no hay lectura que forzar. Sí le alcanza
  FR-185c en lo que puede: ya marca las filas con fallo.
- **FR-185e**: `TextViewerWindow` se usa además, con `esRegistro: false`, para mostrar la
  configuración efectiva de nginx (`PanelesPlataforma.cs:617`), que es texto estático y se abre en
  modal. El seguimiento en vivo MUST NOT alcanzar a ese uso. Si separarlos exige partir la ventana en
  dos, se parte: hacer que una ventana modal siga un archivo que no es un registro es peor.

#### Árbol: orden, icono, color y ventanas (US3)

- **FR-193**: El árbol MUST permitir cualquier orden dentro de una carpeta, elegido por el usuario y
  conservado entre ejecuciones. Ese orden MUST valer también para las carpetas y no sólo para las
  conexiones, que es el alcance con el que quedó FR-005: hoy `PuedenReordenarse` exige
  `EsCarpeta: false` en el origen y en el destino, y `IFolderRepository` no tiene `ReorderAsync`.
- **FR-193a**: Al crear o al importar, el elemento MUST insertarse en su lugar **alfabético** dentro
  de su carpeta, y MUST recibir un `SortOrder` propio.

  El defecto de hoy son dos, distintos de lo que parece. **Las conexiones**: ni `CreateAsync`
  (`ConnectionService.cs:171`) ni `ImportadorDeConexiones.CrearAsync` asignan `SortOrder`, así que
  toda conexión nueva o importada nace en **0**; como los dos repositorios ordenan
  `ORDER BY sort_order, name` (`ConnectionRepository.cs:18`, `FolderRepository.cs:29`), se amontonan
  todas en el cero y saltan al principio, por delante del orden que el usuario acomodó a mano.
  **Las carpetas**, al revés: `ImportadorDeConexiones.cs:109` les da `orden = arbol.Count(...)`, o
  sea el final. (`ConnectionService.cs:370`, `SortOrder = o.SortOrder + 1`, no es ninguno de los dos
  casos: es `DuplicateAsync` (línea 356), y poner la copia junto al original está bien.)

  Motivo del alfabético: es el único orden que no obliga a acomodar cada alta a mano, y una
  importación de PuTTY, WinSCP o FileZilla puede traer decenas de sesiones de una vez.
- **FR-193b**: Arrastrar un elemento y soltarlo entre otros dos MUST dejarlo en esa posición.
  Soltarlo sobre una carpeta MUST moverlo dentro de ella.
- **FR-193c**: El menú contextual de una carpeta MUST ofrecer ordenar su contenido alfabéticamente.
  MUST alcanzar sólo a sus hijos directos: reordenar un subárbol entero de una vez destruye el orden
  manual de cada carpeta interna y no hay cómo devolverlo.
- **FR-194**: Mover una conexión o una carpeta MUST persistirse antes de mostrarse. Cuando la
  persistencia falle, el árbol MUST volver al estado guardado y MUST decir por qué. MUST NOT quedar
  mostrando un movimiento que la base no tiene: eso se ve como un elemento que vuelve solo a su lugar
  anterior en el siguiente refresco, sin que nada haya avisado.
- **FR-194a**: Mover un elemento **a otra carpeta** MUST pedir confirmación, diciendo el nombre del
  elemento y la **ruta completa** de la carpeta de destino. Cuando lo movido es una carpeta, MUST
  decir además cuántas conexiones y cuántas subcarpetas se van con ella. **Acomodar un elemento
  dentro de su misma carpeta MUST NOT preguntar nada**: eso es orden, no un movimiento, y una
  confirmación por cada arrastre vuelve inusable el reordenamiento que pide FR-193.
  FR-062 ya exigía advertir en esa confirmación si el cambio de carpeta altera algún valor heredado;
  eso sigue, dentro del mismo diálogo y no en uno aparte.

- **FR-195**: Una carpeta y una conexión MUST poder
  elegir su icono dentro de un juego provisto por la aplicación, y su color dentro de la paleta. Las
  dos cosas MUST ser independientes entre sí. Hoy sólo se elige el color: el glifo es fijo y sale del
  protocolo, y `PaletaIconos.ClaveDeRecurso` sólo cambia el sufijo de color del recurso.
- **FR-195a**: El juego MUST cubrir los usos habituales de un servidor —base de datos, web, correo,
  archivos, respaldo, contenedor, cortafuegos, monitoreo— además de los genéricos.
- **FR-195b**: El icono y el color MUST NOT heredarse: cada elemento define el suyo o cae en el de la
  aplicación, y ninguno toma el de su carpeta. Todo lo demás que hoy se hereda (FR-058) MUST seguir
  heredándose. Motivo: el icono está para distinguir un elemento de sus hermanos, y heredarlo los
  vuelve idénticos, que es exactamente lo contrario de para qué se elige.

  Este requisito **deroga el escalón intermedio de FR-135** de la 001 (`001/spec.md`), que exige la
  cascada «color propio del elemento, color de su carpeta, color global del protocolo»: la
  cascada queda en dos escalones, elemento y global del protocolo. El código ya sigue a FR-195b, así
  que ese escalón nunca se cumplió: `SettingsResolver` no conoce el color, `NodoArbol` toma el
  `IconColor` propio de cada elemento (`NodoArbol.cs:253` y `:257`) y lo que falta cae en los colores
  globales por protocolo de `AppSettings.GetIconColorsAsync` (`MainWindow.xaml.cs:118`).
- **FR-195c**: El juego de iconos MUST dibujarse con recursos propios de la aplicación. MUST NOT
  incorporarse una biblioteca de terceros para mostrarlo: el Principio IV lo prohíbe. Copiar el
  trazado de un juego libre —con su licencia y su atribución en la documentación— no es incorporar una
  biblioteca y está permitido.
- **FR-190**: El árbol MUST permitir asignar la etiqueta de una conexión o carpeta desde su menú
  contextual, sin abrir el editor.
- **FR-196**: Dentro de cada pestaña, la ventana de configuración de una carpeta MUST agrupar sus
  campos en secciones con título y MUST NOT apilarlos en una sola columna corrida a lo alto de la
  pestaña. La pestaña «Acceso» es la que lo pide: hoy encadena usuario, dominio, puerto, sus tres
  avisos de valor heredado y tres contraseñas por protocolo en un `StackPanel` vertical
  (`FolderSettingsWindow.xaml:51`, dentro de la pestaña que abre en la 41), y nada separa lo que se
  hereda de lo que es credencial.
- **FR-196a**: Lo mismo MUST aplicarse a la ventana de una conexión, que tiene el mismo defecto: su
  pestaña «Protocolo» (`ConnectionEditorWindow.xaml:79`) encadena los campos en un `StackPanel`
  vertical (línea 89), igual que la pestaña «Acceso» de la carpeta. Las dos MUST compartir la misma
  **gramática de secciones** —los mismos títulos para los mismos grupos de campos, en el mismo
  orden dentro de la pestaña, con la misma forma de recuadro—, de modo que reconocer un grupo en
  una alcance para encontrarlo en la otra. MUST NOT exigirse que tengan los mismos campos ni las
  mismas pestañas: la conexión tiene General, Protocolo y Avanzado, y la carpeta tiene General,
  Acceso, RDP y SSH, y eso no cambia.

#### Explorador SFTP (US4)

- **FR-189**: El explorador SFTP MUST mostrar el árbol de directorios. La transferencia de
  carpetas **ya está exigida por FR-073 desde la enmienda 1.2.0**: si no funciona, es un defecto de
  FR-073 y no alcance nuevo.
- **FR-189c**: Los enlaces simbólicos MUST omitirse del listado **y el listado MUST decir cuántos
  omitió**. Hoy no se omiten: `RemoteFileSession.ListAsync`
  (`src/CafManagerConection.Ssh/RemoteFileSession.cs:72`) los deja pasar como archivos comunes,
  porque su único filtro es el de `.` y `..` (línea 84) y un enlace llega con `IsDirectory` en falso
  (línea 88). Mostrarlos así es ofrecerlos, que es lo que FR-078 de la 001 prohíbe (`001/spec.md`);
  omitirlos cierra ese hueco. Lo que agrega este requisito es decir cuántos se omitieron: un listado
  que calla lo que sacó se ve igual que un directorio con menos archivos.
- **FR-189a**: Bajar MUST preguntar dónde guardar. Subir MUST confirmar el directorio remoto de
  destino antes de empezar.
- **FR-189b**: El explorador MUST distinguir tipos de archivo con icono y color.

#### Sesión RDP (US5)

- **FR-186**: Una conexión RDP MAY abrirse con las credenciales de la sesión de Windows del usuario,
  sin pedir ni guardar ninguna. Cuando el equipo no esté en el dominio o el servidor no confíe, el
  sistema MUST caer al pedido de credenciales, no fallar.
- **FR-187**: Una sesión RDP MUST poder maximizarse dentro de la aplicación y MUST poder salir a una
  ventana propia. Cerrar esa ventana MUST devolver la sesión a su pestaña, no cortarla.

#### Herramientas externas (US6)

- **FR-188**: Al abrir PuTTY, WinSCP o FileZilla, el sistema MAY pasarles la **ruta del archivo de
  clave privada** de la conexión, para que la herramienta entre sin preguntar. La contraseña sigue
  gobernada por FR-143b y **no se pasa por ningún medio**, tampoco por archivo: en Windows la línea
  de comandos de un proceso la lee cualquier proceso del mismo usuario, y un archivo con la
  contraseña es exactamente lo que el Principio II prohíbe persistir. Ofrecerlo requeriría una
  enmienda que declare la excepción al Principio II en su título, no una ampliación de alcance: la
  que declaró la 1.14.0 alcanza sólo a la contraseña de `sudo` de FR-184e y a nada más.
- **FR-188a**: Cuando el usuario pida abrir una herramienta externa en una conexión que se
  autentica por contraseña, el sistema MUST decir que la herramienta va a pedirla, en lugar de
  fallar en silencio o de intentar entregarla.
- **FR-188b**: A WinSCP MUST pasársele la ruta de la clave privada junto con el destino; hoy
  `LineaDeComando.Url()` (`Infrastructure/HerramientasExternas.cs:54`) arma sólo
  `sftp://usuario@host:puerto/` y descarta la clave que `MainWindow.Acciones.cs:196` ya resolvió. A
  FileZilla MUST NOT pasársele: no acepta una clave por línea de comandos, y escribirla en su
  configuración está prohibido por FR-182d.

### Key Entities

- **Resultado del sondeo de `sudo`**: uno de tres estados —sin contraseña, con contraseña, imposible—,
  calculado una vez al abrir la sesión y consultado por los paneles. No se persiste: vale para esa
  sesión.
- **Muestra de proceso**: identificador, padre, usuario, nombre, memoria residente, tiempo de CPU
  acumulado y bytes leídos y escritos, en un instante. El porcentaje de CPU no es un atributo de la
  muestra sino la diferencia entre dos.
- **Archivo seguido**: la ruta que un visor está mirando, el momento del último cambio detectado y el
  estado del canal. Un visor puede seguir más de uno.
- **Icono**: la forma del glifo de una carpeta o una conexión, elegida de un juego cerrado. Es
  independiente del color, y ninguno de los dos se hereda. Persiste como una clave de texto en la
  base local, en paralelo a la clave de color.
- **Contraseña de `sudo` de la sesión**: la que el usuario escribe cuando la de la conexión no
  sirve. Vive en memoria mientras la sesión esté abierta, no se persiste en ningún almacén y se pisa
  con ceros al cerrarla (FR-184e).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-038a**: El porcentaje de CPU que informa el panel para un proceso dado no difiere en más de
  5 puntos del que informa `top -b -n 2` corriendo al mismo tiempo en el servidor.
- **SC-038**: Contra un servidor con una fuga conocida, el panel de procesos la señala: el proceso
  que crece aparece primero al ordenar por memoria, y su consumo de CPU coincide con el que muestra
  `top` ejecutado a mano en ese momento —no con el promedio de vida que informa `ps`—.
- **SC-039**: En un servidor donde el usuario no puede usar `sudo`, ningún panel ofrece un botón de
  escalar privilegios, y cada uno dice que no se puede en lugar de mostrar menos sin explicación.
- **SC-040**: Con un visor de registro abierto, borrar el archivo en el servidor produce un aviso en
  menos de 30 segundos. Sin el aviso, el visor se ve igual que con un servicio en silencio.
- **SC-042**: Un directorio de al menos 50 archivos y 3 niveles sube y baja completo por SFTP, y el
  árbol resultante coincide archivo por archivo, verificado por suma de verificación.
- **SC-043**: Una sesión RDP sale a una ventana propia y vuelve a su pestaña sin reconectarse: la
  sesión remota no se corta, verificable porque las ventanas abiertas del escritorio remoto siguen
  como estaban.
- **SC-045**: Arrastrando una carpeta y una conexión a una posición nueva, cerrando la aplicación y
  volviendo a abrirla, las dos siguen donde quedaron.
- **SC-046**: Importando veinte sesiones de una herramienta externa, aparecen en orden alfabético
  dentro de su carpeta sin que el usuario toque nada.
- **SC-047**: Con la base en sólo lectura, mover una conexión a otra carpeta deja el árbol como
  estaba y muestra el motivo. Ninguna conexión queda mostrada en una carpeta que la base no tiene.
- **SC-048**: Dentro de una carpeta con icono y color propios, dos conexiones hermanas con iconos
  distintos conservan cada una el suyo: ninguna toma el de la carpeta.
- **SC-049**: Con un visor de registro de supervisord abierto, una línea escrita en el servidor
  aparece en pantalla en menos de 5 segundos sin que el usuario toque nada.
- **SC-050**: En un servidor con al menos 400 procesos, el panel ordena por CPU y por memoria y
  refresca en el intervalo elegido pintando cada refresco en menos de 300 ms, el mismo techo que
  SC-003 le pone al resto de la interfaz.
- **SC-050a**: El costo de cada muestra en el servidor no pasa del **1 % de un núcleo**, que es el
  techo que SC-018 ya le puso al monitoreo y que sigue en alcance. Se mide con el panel de estado y
  el de procesos abiertos a la vez, que es el peor caso. T511 de la 001 midió que sólo el panel de
  estado ya cuesta 1 a 3 % con unos 700 procesos: si la suma no entra, se baja la frecuencia o se
  achica lo que se pide, no se sube el techo.
- **SC-051**: Abrir una sesión SSH ejecuta exactamente un `sudo` de sondeo. Se verifica contando las
  líneas de `sudo` en el registro del servidor antes y después.
- **SC-052**: Ningún registro de la aplicación —ni su archivo de registro, ni la consola de traza,
  ni el texto de un error— contiene la contraseña que se le pasa a `sudo` por la entrada estándar. Se
  verifica con una prueba automática que busca el valor conocido en todo lo que la aplicación
  escribe.
- **SC-052a**: Después de una sesión en la que el usuario escribió una contraseña de `sudo` conocida,
  ese valor no aparece en la base SQLite, ni en el archivo de configuración, ni en el Administrador
  de credenciales de Windows, ni en ningún archivo bajo `%LocalAppData%\CafManagerConection`. Se
  verifica buscando el valor conocido en los tres almacenes.
- **SC-052b**: Al cerrar la sesión, el búfer que sostenía la contraseña de `sudo` queda en cero. Se
  verifica leyendo el búfer después del cierre, igual que se verifica `TomarTexto()` en
  `src/CafManagerConection.Ssh/EntradaDeContrasenaInteractiva.cs:52`.
- **SC-052c**: Cerrando la sesión y volviendo a abrir la misma conexión, el sistema pide la
  contraseña de `sudo` de nuevo: no queda ninguna sesión que la herede.
- **SC-053**: Abrir WinSCP desde una conexión con clave privada entra sin pedir nada. Abrirlo desde
  una conexión con contraseña muestra antes el aviso de que la herramienta la va a pedir.

## Assumptions

- El servidor remoto es Linux con `/proc`. El panel de procesos y el sondeo de `sudo` no aplican a RDP.
- El intervalo de muestreo del panel de procesos es el mismo que el usuario ya elige para el panel de
  estado (FR-175); no se agrega un segundo control.
- Las ventanas de configuración se redistribuyen en dos columnas dentro de cada pestaña, con las
  secciones encerradas en recuadros con título. Cambiar esta decisión o la del intervalo no cambia
  ningún requisito, sólo cómo se cumple.

## Dependencias

- Reusa `SshCommandRunner.RunWithSudoFallbackAsync`
  (`src/CafManagerConection.Ssh/SshCommandRunner.cs:180`), que ya implementa el camino de escalada de
  FR-095 y que consumen `ControlDeDocker`, `ControlDeSupervisor` y `ConsultorDeProcesos`. El tramo
  que manda la contraseña de la conexión por la entrada estándar está en `ConSudoYContrasenaAsync`,
  desde la línea 207, y el `sudo -S -k` en la 213.
- Reusa `IPlatformLogStreamer.SeguirAsync` (`SshCommandRunner.cs:331`), que hoy sólo sirve a
  `docker logs -f`.
- Necesita un `ReorderAsync` en `IFolderRepository`, que no existe: la interfaz va de la línea 19 a
  la 30 de `src/CafManagerConection.UseCases/Abstractions/Repositories.cs` y no lo declara.
  `IConnectionRepository` sí lo tiene, en la línea 56 del mismo archivo.
- Necesita la **migración 006** para guardar el icono de una carpeta y de una conexión: una columna
  de clave de icono en `folders` y otra en `connections`, en paralelo a la de color. Es la que exige
  la enmienda 1.14.0 (`constitution.md:772`).
- Reusa el patrón de borrado de `EntradaDeContrasenaInteractiva.TomarTexto()`
  (`src/CafManagerConection.Ssh/EntradaDeContrasenaInteractiva.cs:52`) para la contraseña de `sudo`
  de FR-184e: pisa con ceros la lista y la copia que devuelve `ToArray` antes de soltarlas.
