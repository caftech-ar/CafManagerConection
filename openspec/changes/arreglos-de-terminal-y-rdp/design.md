## Context

`TerminalControl` se construye con `new TerminalBuffer(80, 24)`, y `ConectarSshAsync` lee
`_terminal.Columns`/`_terminal.Rows` para armar el `SshSessionRequest` inmediatamente después de crear
el control y asignarlo a `_host.Child`, sin que haya mediado un ciclo de layout. El tamaño real llega
después, por `OnSizeChanged` → `Resize` → `SizeChangedInCells` → `_ssh.Resize`.

`TerminalControl` guarda la selección en `_selectionStart`/`_selectionEnd` como coordenadas de celda y
el desplazamiento en `_scrollOffset`, que se mueven por separado. **Todos** los caminos de
desplazamiento pasan por el mismo método `ScrollBy`: la rueda (`OnMouseWheel`), el teclado (las seis
acciones de historial), y `AcompanarSeleccion`, que desplaza mientras el usuario arrastra la selección
más allá del borde. El arrastre del pulgar de la barra es el único que no pasa por ahí:
`DesplazarPorPulgar` escribe `_scrollOffset` directamente.

Del lado de las herramientas, `HerramientasDisponibles` detecta una vez lo instalado y
`AgregarHerramientasExternas` corta de entrada si el protocolo no es SSH, así que ninguna herramienta
puede aplicar a RDP aunque exista.

## Goals / Non-Goals

**Goals:**
- Que la selección nunca quede pintada sobre texto que no es el que se seleccionó.
- Abrir una conexión RDP en `mstsc` con el mismo gesto que una SSH en PuTTY.

**Non-Goals:**
- Anclar la selección al historial para que acompañe al texto al desplazarse.
- Pasarle la contraseña a `mstsc`.

## Decisions

**Se espera el tamaño; no se corrige después.** El síntoma es el prompt a media pantalla, pero la causa
es que se le pide al servidor un pty de 80×24 que nadie tiene: el servidor dimensiona su bienvenida a 24
filas y el terminal desplaza lo que no entra, y cuando llega el tamaño real el cursor ya quedó donde
quedó. Acomodar el buffer después del `Resize` —mover el cursor, recuperar líneas del historial— es
adivinar qué parte de lo recibido era bienvenida y qué parte no. Se difiere el armado del
`SshSessionRequest` hasta que el terminal tenga medida, que es el único momento en que el dato es
cierto.

**El 80×24 del constructor se queda.** Es un valor razonable para un control que todavía no se midió, y
cambiarlo no arregla nada: el problema no es cuál es el valor inicial sino que se lo use como si fuera
el definitivo.

**Limpiar la selección, no anclarla.** Anclarla al historial —que se desplace con las líneas, como
hacen PuTTY y los terminales modernos— es el comportamiento que más gente espera, pero obliga a
cambiar el sistema de coordenadas de la selección de «fila en pantalla» a «fila en el historial» y a
revisar cada punto que la lee, la pinta y la copia. Limpiar resuelve la molestia concreta con un
cambio acotado. Si más adelante se quiere el anclaje, este cambio no lo estorba.

**La limpieza va en los llamadores, no en `ScrollBy`.** Es la tentación obvia —un solo lugar, todos los
caminos cubiertos— y es un error: `AcompanarSeleccion` llama a `ScrollBy` justamente para extender la
selección más allá del borde visible, así que limpiar ahí rompería esa función. La limpieza se engancha
en `OnMouseWheel`, en `DesplazarPorPulgar` y en las acciones de historial del teclado: los caminos en
los que el usuario desplaza *en vez de* seleccionar.

**El teclado limpia igual que la rueda.** `Ctrl+Inicio` o `RePág` desplazan el historial con la misma
intención que la rueda, y dejar la selección viva sólo ahí sería una inconsistencia difícil de explicar.
El pedido nombraba el scroll del mouse; se extiende al teclado porque es el mismo gesto.

**`mstsc` se detecta como las demás, aunque siempre esté.** Viene con Windows, pero tratarla distinta
—darla por instalada sin buscarla— la sacaría del único lugar donde hoy se resuelve una ruta de
herramienta, y en una instalación sin el cliente (imagen recortada, Windows en modo servidor sin la
característica) la app ofrecería abrir algo que no existe. `BuscadorDeHerramientas` la resuelve por su
ruta del sistema y, si está, aparece.

**Cada herramienta declara sus protocolos.** En lugar de un corte único `!= Ssh`, cada valor de
`HerramientaExterna` dice para qué protocolos sirve, y el menú ofrece las instaladas que aplican al
protocolo de esa conexión. Es el mismo trabajo que el corte actual y deja sumar la próxima herramienta
sin tocar el menú.

**Sin contraseña, y sin archivo `.rdp` temporal.** `mstsc` no toma la contraseña por línea de comandos,
y generar un `.rdp` con credenciales dejaría un archivo con material sensible en el disco. Se abre con
host, puerto y usuario, y el usuario tipea. El proyecto ya tiene `AvisarQueLaHerramientaPideLaContrasena`
para decirlo, que es el mismo trato que reciben hoy FileZilla y WinSCP.

## Risks / Trade-offs

- **`mstsc /v:` con usuario**: el cliente de Windows no acepta el usuario en el destino como PuTTY. Se
  abre con host y puerto, y el usuario queda como dato a completar en el cuadro del cliente.
- Limpiar la selección al desplazar puede molestar a quien selecciona, se desplaza a mirar otra cosa y
  vuelve esperando que siga seleccionado. Es el comportamiento pedido y el que evita el estado
  inconsistente actual.
