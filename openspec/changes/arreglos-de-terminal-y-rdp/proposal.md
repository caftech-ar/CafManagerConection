## Why

Tres molestias chicas, sin relación entre sí más que el tamaño, que hoy no tienen dónde entrar:

- Al conectar a un servidor SSH, el prompt queda a media pantalla con el banner desplazado arriba. La
  sesión se abre pidiendo un pty de 80×24 —el tamaño con el que nace `TerminalControl`, leído antes de
  que el control se haya medido—, el servidor dimensiona su salida a esas 24 filas, y recién después
  llega el tamaño real.

- En el terminal, una selección hecha con el mouse queda pintada sobre celdas que ya no le
  corresponden cuando se desplaza el historial. La selección y el desplazamiento se llevan por
  separado, así que lo seleccionado deja de coincidir con lo que se ve.
- Las conexiones SSH se pueden abrir en PuTTY, WinSCP o FileZilla desde el menú del árbol. Las RDP no
  tienen equivalente, aunque `mstsc` viene con Windows: `AgregarHerramientasExternas` corta con un
  `return` en cuanto el protocolo no es SSH.

## What Changes

- La sesión SSH se abre con el tamaño real del terminal, no con el 80×24 inicial: el prompt queda
  arriba y no se desplaza nada que hubiera entrado en pantalla.
- Al desplazar el historial del terminal con la rueda o arrastrando la barra, la selección activa se
  limpia. El desplazamiento que provoca la propia selección al arrastrar más allá del borde —que hoy
  es deliberado— sigue funcionando igual.
- `mstsc` entra como herramienta externa, disponible para las conexiones RDP en el menú del árbol y en
  la barra de la sesión, igual que PuTTY para SSH. Cada herramienta declara con qué protocolos sirve,
  en lugar del corte único por SSH.

## Capabilities

### New Capabilities

- `tamano-inicial-del-terminal`: con qué tamaño se abre una sesión SSH y qué se espera ver al conectar.
- `seleccion-del-terminal`: qué pasa con la selección del terminal cuando se desplaza el historial.
- `herramienta-externa-rdp`: cómo se abre una conexión RDP en el cliente de Windows, y cómo se decide
  qué herramienta externa aplica a qué protocolo.

### Modified Capabilities

Ninguna: `openspec/specs/` está vacío.

## Impact

- `CafManagerConection.App`: `SessionView.ConectarSshAsync` espera a que el terminal se haya medido
  antes de armar el `SshSessionRequest`.
- `CafManagerConection.Terminal`: `TerminalControl` limpia la selección en los caminos de
  desplazamiento del usuario.
- `CafManagerConection.Infrastructure`: `HerramientaExterna` suma `Mstsc`; `LineaDeComando` su línea;
  `BuscadorDeHerramientas` su resolución; las herramientas declaran sus protocolos.
- `CafManagerConection.App`: `MainWindow.Acciones` deja de cortar por SSH y filtra por protocolo;
  `SessionView.Barra` ofrece las herramientas de la sesión RDP.
- Sin cambios de esquema ni de dominio.
