## Why

El ping abre una ventana aparte, que tapa la aplicación y no se puede dejar al lado mientras se
trabaja. Y las bitácoras que la aplicación escribe no se pueden leer desde ella: hay que ir a buscar
los archivos a mano.

Las dos son herramientas para mirar mientras se hace otra cosa, que es justo lo que el sistema de
pestañas ya resuelve para las sesiones.

## What Changes

- El **ping pasa a abrirse como pestaña**, al lado de las sesiones, con su propio título.
- El sondeo del ping deja de mirar sólo el puerto del protocolo: cada fila se puede **desplegar** para
  ver el resto de los puertos que esa conexión tiene configurados —el de la web, los extremos de sus
  túneles— y saber cuáles abren.
- El menú contextual de una conexión suma **«Ver bitácoras»**, que abre otra pestaña con las
  bitácoras que hay, por fecha, filtrables por conexión, y al elegir una se lee su contenido ahí
  mismo, con búsqueda.

## Capabilities

### New Capabilities

- `ping-en-pestana`: dónde vive el sondeo y qué puertos ofrece analizar por conexión.
- `visor-de-bitacoras`: cómo se listan y se leen las bitácoras escritas.

### Modified Capabilities

Ninguna: `openspec/specs/` está vacío.

## Impact

- `CafManagerConection.App`: `PingWindow` pasa de ventana a contenido de pestaña; `MainWindow` aprende
  a abrir pestañas que no son sesiones; `MainWindow.Acciones` suma la entrada de bitácoras; visor
  nuevo.
- `CafManagerConection.App/Services/SondeoDeHosts`: sondear varios puertos por conexión, no uno.
- Depende de `bitacora-de-sesion-ssh`, que es la que escribe lo que el visor lee.
- Sin cambios de esquema ni de dominio.
