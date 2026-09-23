## Why

El juego de iconos que puede elegir el usuario tiene 16 entradas, y el selector que las muestra es un
panel plano de cuadraditos sin buscador ni grupos. Alcanza para 16 y no para más: cualquier conexión
que no sea escritorio, terminal, base o web termina con el icono genérico de aplicación.

Hay un catálogo cerrado y verificado de 246 iconos —186 conceptos de Tabler y 60 logos de producto
de Tabler, Simple Icons y Devicon— listo para entrar, más los cuatro que la interfaz necesita y el
catálogo no traía. Lo que falta es dónde guardarlos, cómo pintarlos y cómo encontrarlos entre 250.

## What Changes

- Los 250 SVG entran al repo bajo `Assets/Iconos` del proyecto de la aplicación, separados en
  carpetas por grupo, como fuente versionada y legible.
- Un script los traduce a `StreamGeometry`, un diccionario de recursos por grupo. El XAML generado se
  versiona: el build no depende del script y en ejecución no se parsea ningún SVG.
- `CatalogoDeIconos` reemplaza a `JuegoDeIconos` en el dominio: cada icono lleva clave, grupo,
  familia (concepto o logo), etiqueta, sinónimos de búsqueda, modo de pintado, lado del lienzo y si
  se ofrece en el selector.
- **BREAKING para el aspecto, no para los datos**: los conceptos de Tabler se pintan por trazo y los
  logos por relleno. El juego actual es todo relleno, así que el control que dibuja iconos deja de
  asumir `Fill` y pasa a decidir según el icono.
- `SelectorDeIconosWindow` reemplaza al panel plano de `ConnectionEditorWindow` y
  `FolderSettingsWindow`: buscador por nombre y sinónimos, filtro por grupo y por familia.
- La iconografía propia de la interfaz —los 30 glifos Fluent de `Estilos.xaml`— migra a Tabler para
  que no convivan dos estilos. Va en una capacidad aparte, que se puede dejar sin implementar sin
  romper las otras tres.
- Las 16 claves de icono del juego anterior se traducen a las del catálogo con una migración, para
  que nadie pierda el icono que había elegido. Una clave que el catálogo igual no reconozca cae en
  el icono por omisión del protocolo.

## Capabilities

### New Capabilities

- `catalogo-de-iconos`: dónde viven los 250 SVG, cómo se agrupan, cómo se convierten a geometrías y
  qué datos lleva cada icono para poder buscarlo y pintarlo.
- `dibujo-de-iconos-vectoriales`: el control que pinta un icono del catálogo en cualquier color y
  tamaño, decidiendo relleno o trazo según el icono y el grosor de trazo según el tamaño.
- `seleccion-de-icono-con-filtros`: la ventana de selección con buscador, filtro por grupo y por
  familia, que reemplaza al panel de 16 cuadrados.
- `iconografia-unificada-de-la-interfaz`: la migración de los glifos propios de la interfaz al mismo
  estilo del catálogo, para que no haya dos familias visuales en pantalla.

### Modified Capabilities

<!-- Ninguna: no hay specs publicadas en openspec/specs/ que cambien de requisito. -->

## Impact

**Se agrega**

- Los SVG bajo `src/CafManagerConection.App/Assets/Iconos`, en `conceptos/<grupo>` y `logos/<grupo>`.
- `build/convertir-iconos.ps1` — la conversión de SVG a geometría.
- Los `Iconos.<Grupo>.xaml` de `src/CafManagerConection.App/Themes` — generados, no se editan a mano.
- `src/CafManagerConection.Domain/Settings/CatalogoDeIconos.cs` y `BuscadorDeIconos.cs`.
- `Migration005_ClavesDeIconoDelCatalogo`, en
  `src/CafManagerConection.Infrastructure/Database/Migrations`.
- `IconoVectorial`, el control que dibuja, en `src/CafManagerConection.App/Themes`.
- `IconosDeLaInterfaz`, con la clave del catálogo de cada cosa que la aplicación dibuja, en
  `src/CafManagerConection.App/Themes`.
- `IconosPorOmision`, con la que le toca a cada protocolo y a una carpeta, en
  `src/CafManagerConection.Domain/Settings`.
- `SelectorDeIconosWindow` y `MuestraDeIconosWindow`, en `src/CafManagerConection.App/Views`.

**Se modifica**

- `src/CafManagerConection.App/Views/ConnectionEditorWindow.xaml.cs` — pierde
  `ArmarSelectorDeIconos`, `MarcarIconoElegido` y `MuestraDe`; abre la ventana nueva.
- `src/CafManagerConection.App/Views/FolderSettingsWindow.xaml.cs` — lo mismo.
- `src/CafManagerConection.App/ViewModels/NodoArbol.cs` — resuelve contra el catálogo nuevo.
- `src/CafManagerConection.App/Panels/MenuIconos.cs` — recibe una clave y deja de asumir relleno.
- `src/CafManagerConection.App/Views/PreferenciasWindow.xaml` — abre el catálogo completo.
- `src/CafManagerConection.App/Themes/Estilos.xaml` — pierde las 30 geometrías propias en la última
  capacidad.
- `src/CafManagerConection.Domain/Monitoring/IconoDeProceso.cs` y
  `src/CafManagerConection.App/Services/IconosDeAplicacion.cs` — pasan a los logos de producto reales.

**Se elimina**

- `JuegoDeIconos`, de `src/CafManagerConection.Domain/Settings`, absorbido por `CatalogoDeIconos`.

**Queda intacto**

- `PaletaIconos` y los diez pinceles de color por tema: el color del icono ya funciona y no cambia.
- `Connection.ClaveDeIcono` y `Folder.ClaveDeIcono`: mismo tipo, misma columna.
- El esquema de la base. Ninguna tabla ni columna se toca: la migración sólo reescribe valores.

**Dependencias**

Ninguna nueva. La aplicación es WPF y dibuja vectores de forma nativa: no entra `Svg.Skia`, ni
`SharpVectors`, ni ningún nativo al publicado.
