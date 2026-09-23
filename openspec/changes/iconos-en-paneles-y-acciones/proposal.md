## Why

El catálogo de 250 iconos ya está y el árbol lo usa, pero los paneles de la sesión quedaron a mitad
de camino: tres muestran iconos, tres no muestran ninguno, y los que sí tienen no dicen lo mismo.

Y hay un defecto concreto. En el panel de procesos, un proceso que no se reconoce **no dibuja icono y
la columna se desarma**: `VisibilidadDelIcono` devuelve `Collapsed`, que ocupa cero, así que el
nombre salta a la izquierda. En el de puertos el hueco se reserva pero queda mudo. Dos paneles, dos
comportamientos para el mismo caso.

Debajo de eso hay algo más de fondo: **tres cosas distintas se llaman «icono»** y hoy están
mezcladas. Identidad —qué es esto—, estado —cómo está— y acción —qué hace este control—. El panel de
Docker tiene una columna llamada `Icono` que es estado: un `postgres` y un `nginx` corriendo se
dibujan idénticos, con el mismo tilde verde, y el panel nunca dice qué es cada contenedor.

## What Changes

- **El desconocido deja de ser un hueco.** Un proceso, un puerto o un contenedor que no se reconoce
  se dibuja con un icono genérico en el color tenue del tema. La columna alinea, y «no sé qué es»
  queda dicho en vez de mudo.
- **BREAKING para una regla escrita**: `IconoDeProceso` documenta hoy que «mostrar un icono genérico
  a todo hace que ninguno signifique nada». Ese contrato se reescribe: lo que evita que el icono se
  vuelva textura no es la ausencia, es el énfasis. El genérico se pinta más apagado que los
  reconocidos.
- **Docker y supervisord suman una columna de identidad**, separada de la de estado. Docker la deduce
  de la imagen del contenedor, que la fila ya trae; supervisord, del nombre del proceso.
- **nginx estrena iconos**: cada fila es un server block, no un producto, así que el icono dice de
  qué tipo de sitio se trata —seguro, sitio plano, sin raíz— con lo que el parser ya lee.
- **Los tres filtros del árbol emparejan**: Favoritas ya tiene icono, SSH y RDP no.
- **El menú del árbol se completa**: tenía 14 de 18, y las cuatro que faltaban eran justamente las
  acciones destacadas. Queda entero.
- **Los botones de barra de herramientas y los destructivos llevan icono.** Los de diálogo —Guardar,
  Cancelar, Aceptar— se quedan con su texto.
- **Túneles muestra si el túnel está activo**, que hoy hay que deducir leyendo la fila.
- **Se va el último emoji de la interfaz**: `PanelInventario` dibuja `⏳` como texto; pasa a ser el
  icono del catálogo.

## Capabilities

### New Capabilities

- `identidad-y-estado-en-los-paneles`: qué dibuja cada panel de la sesión, cómo se separan identidad
  y estado, y qué pasa cuando no se reconoce lo que hay del otro lado.
- `iconos-en-acciones-y-filtros`: dónde lleva icono un control que hace algo —menús, botones,
  filtros— y dónde no, para que el icono siga siendo señal.

### Modified Capabilities

<!-- Ninguna: no hay specs publicadas en openspec/specs/ que cambien de requisito. -->

## Impact

**Se modifica**

- `src/CafManagerConection.Domain/Monitoring/IconoDeProceso.cs` — devuelve el genérico en vez de
  nulo, y su contrato cambia.
- `src/CafManagerConection.App/Panels/ProcesosPanel.xaml` y su código — se va el `Collapsed` que
  desarma la columna.
- `src/CafManagerConection.App/Panels/PanelesPlataforma.cs` — Docker, supervisord, nginx y puertos:
  columna de identidad y el genérico.
- `src/CafManagerConection.App/Panels/TunnelsPanel.xaml` — estado del túnel.
- `src/CafManagerConection.App/Panels/PanelInventario.xaml` — el emoji pasa a icono.
- `src/CafManagerConection.App/Views/MainWindow.xaml` — los filtros de SSH y RDP.
- `src/CafManagerConection.App/Views/MainWindow.Acciones.cs` — cuatro entradas del menú.
- `src/CafManagerConection.App/Themes/IconosDeLaInterfaz.cs` — las claves que faltan.
- Los XAML con botones de barra de herramientas y con botones destructivos.

**Queda intacto**

- El catálogo y los 250 SVG: este cambio usa lo que ya está, no suma ni un icono.
- `IconoVectorial`: el control ya hace todo lo que hace falta.
- El esquema de la base y los datos. Nada de esto se guarda.

**Dependencias**

Ninguna nueva.
