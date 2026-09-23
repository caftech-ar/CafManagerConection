## Context

La aplicación es WPF sobre .NET 10. WinForms está habilitado en el proyecto por una sola razón,
escrita en el propio `csproj`: alojar el control ActiveX de RDP y el control de terminal dentro de un
`WindowsFormsHost`. No hay un `Form`, ni un `TreeView` de WinForms, ni un `ImageList` en todo el repo.

Eso importa porque el brief de origen describía una app WinForms y llegaba a decisiones que acá no
aplican: rasterizar con `Svg.Skia` a `Bitmap`, cachear por `(clave, píxeles, color, trazo)`, una
`ImageList` por tamaño y volver a renderizar cuando cambia la resolución. WPF dibuja vectores de
forma nativa y es independiente de la resolución: todo eso sobra, y agregarlo sería perder nitidez y
sumar un nativo al publicado a cambio de nada.

Lo que sí existe hoy:

| Pieza | Dónde | Qué hace |
|---|---|---|
| 30 geometrías `Icono*` | `Themes/Estilos.xaml` | Fluent UI System Icons, relleno, lienzo 20×20 |
| 10 pinceles de color | `Themes/Paleta.{Claro,Oscuro}.xaml` | Un tono por color y por tema |
| `JuegoDeIconos` | `Domain/Settings/` | 16 iconos elegibles: clave → nombre de recurso |
| `PaletaIconos` | `Domain/Settings/` | 10 colores: clave → nombre de pincel |
| `ArmarSelectorDeIconos` | `Views/ConnectionEditorWindow.xaml.cs` | Panel plano de 16 cuadrados de 30px |
| `NodoArbol.ClaveDeIcono` | `ViewModels/` | Resuelve el elegido, y cae en el del protocolo |
| `IconoDeProceso`, `IconosDeAplicacion` | `Domain/Monitoring/`, `App/Services/` | Producto → glifo y color |

No hay ni un PNG de iconografía en `src/`: sólo `cmc.ico` y los dos logos, que son marca de la
aplicación. El objetivo no es sacar mapas de bits —no los hay— sino cambiar el origen de las
geometrías y pasar de 16 iconos elegibles a 250.

## Goals / Non-Goals

**Goals**

- Los 250 SVG versionados en el repo, legibles y agrupados por tema, cada uno diciendo de dónde salió.
- Conversión a geometría por script, sin biblioteca de SVG ni parseo en ejecución.
- Un solo control que dibuja cualquier icono en cualquier color y tamaño, y que decide solo si va
  por relleno o por trazo.
- Una ventana de selección usable con el catálogo entero: buscador, filtro por grupo y por familia.
- Que nadie pierda el icono que ya había elegido.

**Non-Goals**

- Rasterizar a mapas de bits. No hace falta y empeora el resultado.
- Agregar dependencias. Ni `Svg.Skia`, ni `SharpVectors`, ni nativos.
- Crear un modelo de dominio de tipos de nodo. El árbol tiene carpetas y conexiones; inventar
  `NodeType` sería modelo que nadie consume.
- Tocar el esquema de la base.
- Iconos de varios colores. El catálogo entero es monocromo y el color lo pone quien dibuja.

## Decisions

### Convertir los SVG con un script, no en ejecución

Los SVG entran como archivos y `build/convertir-iconos.ps1` los traduce a `StreamGeometry`. El XAML
que produce se versiona, así que el build y el diseñador no dependen del script; una prueba lo corre
en modo verificación y falla si un diccionario quedó atrás de sus SVG.

*Por qué*: el dato que se necesita —el recorrido del dibujo— ya está escrito en el atributo `d` de
cada elemento del SVG. Extraerlo es texto, no interpretación. Parsear SVG en ejecución exigiría
`SharpVectors` y pagaría el costo en cada arranque para llegar al mismo resultado.

*Alternativa descartada*: guardar los SVG como recursos incrustados y parsearlos al vuelo. Suma una
dependencia y retrasa el arranque a cambio de nada.

*Alternativa descartada*: pegar las 250 geometrías a mano en un XAML. Es lo que hay hoy con 30, y ya
cuesta: nadie sabe de dónde salió cada una ni cómo actualizarla.

### Unir los recorridos de un icono en una sola geometría

Los conceptos de Tabler traen entre uno y diez elementos de dibujo. Todos comparten grosor, remate
redondo y unión redonda, así que se concatenan sin perder nada, y el control aplica esos atributos
una sola vez.

*Riesgo asumido*: si alguna vez entra un SVG cuyos elementos no comparten atributos, concatenarlos lo
dibujaría mal en silencio. Por eso el conversor falla nombrando el archivo, en vez de emitir algo
roto. De los 250 de hoy, ninguno cae en ese caso.

Además se descarta el primer recorrido de todo icono de Tabler: es el rectángulo de 24×24 sin pintura
que marca el lienzo. Si entrara, el trazo dibujaría un marco alrededor de cada icono.

### El modo de pintado es un dato del icono, no del que lo usa

Un diccionario de `clave → Geometry` no alcanza, y ésta es la diferencia más grande con el esqueleto
del brief. Los 217 iconos de Tabler se pintan por trazo; los 31 de Simple Icons y los 2 de Devicon
son siluetas macizas y se pintan por relleno. Poner un contorno a una silueta maciza la convierte en
una mancha, y rellenar un icono de trazo tapa el dibujo.

`IconoVectorial` lee el modo desde el catálogo y decide. Quien lo usa da clave, tamaño y pincel.

*Alternativa descartada*: convertir las siluetas macizas a contorno para unificar. No se puede sin
rehacer el dibujo a mano, y son logos de marca: alterarlos es peor que mezclar dos modos.

### El color entra como pincel y el tema lo resuelve

Nada de reemplazar `currentColor` dentro del SVG. El control recibe un pincel; si es un pincel del
tema, WPF lo repinta solo cuando el tema cambia. Los diez colores de `PaletaIconos` siguen tal cual.

*Por qué no lo del brief*: reemplazar el color dentro del texto del SVG obliga a regenerar el icono
cada vez que cambia el tema y a cachear por color. Acá el color es una propiedad del dibujo, no del
icono.

### La procedencia se escribe en los tres lugares

Cada SVG lleva arriba un comentario con paquete, versión, ruta de origen y licencia. La entrada del
catálogo lleva los mismos cuatro datos. El diccionario generado los arrastra como comentario sobre
cada geometría. Una prueba verifica que coincidan. El README declara los tres paquetes, que es donde
el repositorio admite documentación.

*Por qué en los tres*: quien abre un SVG suelto quiere saber si puede tocarlo; quien lee el catálogo
quiere saber qué licencia le toca; quien mira el generado quiere saber por qué está ahí. Un único
archivo de licencias al costado responde la pregunta legal pero no la pregunta de «¿de dónde salió
este dibujo?» cuando se está parado sobre él.

*Costo asumido*: el origen se repite. Es el precio de que el dato viaje con el archivo.

### El catálogo vive en el dominio, las geometrías en la aplicación

`CatalogoDeIconos` es datos —clave, grupo, familia, etiqueta, sinónimos, modo, origen— y va en
`Domain`, junto a `PaletaIconos`, que ya está ahí. Las geometrías son WPF y van en `App/Themes`.
Así el buscador se prueba sin levantar una ventana, que es como se prueban hoy `JuegoDeIconos` y
`PaletaIconos`.

`JuegoDeIconos` desaparece: sus 16 entradas son un subconjunto de las 250 y su trabajo —clave a
recurso— lo hace el catálogo.

### Estar en el selector es propiedad del icono, no del grupo

Los glifos con los que se dibuja la aplicación —buscar, filtrar, ordenar, minimizar, expandir— no
identifican una conexión y no se ofrecen. Siguen en el catálogo, porque la interfaz los necesita.

El corte no puede ser por grupo: «Acciones» es chrome entero, pero «Organización y navegación»
mezcla el chrome con `folder`, `star`, `tag` y `bookmark`, que son justamente de los que más sentido
tienen para una conexión. Cortar por grupo dejaba sin `folder` a las carpetas, que es su icono por
omisión. Así que `EnElSelector` es del icono: 223 de los 250 se ofrecen.

### Las claves guardadas se traducen, y además se cae con elegancia

Dos cosas distintas, y hacen falta las dos.

`NodoArbol.ClaveDeIcono` valida contra el catálogo antes de usar la clave guardada: si no la
reconoce, cae en la del protocolo. Eso cubre cualquier clave desconocida, venga de donde venga, y no
deja que la interfaz se rompa.

Pero caer con elegancia no alcanza: la elección del usuario se perdía en silencio y la clave muerta
volvía a escribirse en la base al guardar la conexión. Las 16 claves del juego anterior tienen
equivalente directo, así que `Migration005_ClavesDeIconoDelCatalogo` las traduce en
`connections.icon_key` y `connection_folders.icon_key`.

*Alternativa descartada*: traducir al leer, con un mapa en memoria. No toca datos, pero el código
vive para siempre y el valor muerto se queda en la base.

La migración no cambia el esquema: ni una tabla ni una columna. Sólo reescribe 16 valores.

*Alternativa descartada*: un mapa que traduzca las 16 claves viejas a claves nuevas. Sería código que
existe para siempre por un puñado de conexiones, y el comportamiento sin él ya es correcto.

*Alternativa descartada*: migrar la base. Toca datos del usuario para resolver algo que se resuelve
solo.

## Risks / Trade-offs

**250 geometrías cargadas al arranque** → Un diccionario por grupo, combinados en `App.xaml`. Cada
geometría es una cadena de pocos cientos de caracteres; el conjunto pesa menos que las capturas de
`capturas/`. Si el arranque se notara, los diccionarios de grupo se cargan cuando se abre el selector
y no antes.

**El cambio de estilo se ve en toda la app** → Los glifos actuales son Fluent macizos; los nuevos son
Tabler de trazo. Es un cambio de aspecto grande, y por eso la migración de la interfaz es una
capacidad aparte, la última: las otras tres se pueden implementar y usar sin tocar ni un glifo
existente, y el cambio de estilo se decide viéndolo.

**Dos estilos conviviendo mientras tanto** → Entre la segunda y la cuarta capacidad, el árbol muestra
iconos de trazo del catálogo y las barras muestran glifos macizos. Es visible y es temporal. La
alternativa —hacer todo junto— impide revisar nada por separado.

**Un SVG que el conversor no sepa unir** → La compilación falla nombrando el archivo. Preferido a
emitir una geometría que se dibuja mal y que nadie mira hasta que un usuario la ve.

**Los logos son marcas de terceros** → Identifican al producto conectado. No se usan para la
aplicación, ni para ventanas, ni para el instalador. Queda escrito en la spec.

**El buscador con el catálogo entero y filtrado al escribir** → Es una lista en memoria de 250 registros;
filtrar por texto es trivial. Lo caro sería redibujar 223 controles en cada tecla: la grilla se
virtualiza.

## Migration Plan

Cuatro capacidades, cada una entregable y verificable sola, en este orden:

1. **`catalogo-de-iconos`** — los SVG, el conversor, el catálogo. No se ve nada en pantalla; se
   verifica con pruebas: las 250 claves resuelven y los orígenes coinciden.
2. **`dibujo-de-iconos-vectoriales`** — el control. Se verifica con una ventana de muestra que dibuja
   el catálogo entero en los seis tamaños y en los dos temas. Esa ventana es la prueba visual del
   paso anterior.
3. **`seleccion-de-icono-con-filtros`** — la ventana de selección, en la conexión y en la carpeta. Es
   lo primero que cambia para el usuario, y no toca ningún icono existente de la interfaz.
4. **`iconografia-unificada-de-la-interfaz`** — los 30 glifos propios y los mapas de producto. Es el
   paso irreversible y el que hay que mirar con los ojos.

**Vuelta atrás**: hasta el punto 3 inclusive, el sistema viejo sigue en su lugar y volver atrás es
revertir archivos nuevos. El punto 4 borra las geometrías de `Estilos.xaml`; volver atrás de ahí es
revertir ese cambio, y no hay datos involucrados en ninguno de los dos sentidos.

**Datos**: una sola migración, la 005, que traduce las 16 claves del juego anterior. El esquema no
cambia. Volver atrás de ella deja claves del catálogo donde antes había claves viejas, y esas caen
en el icono del protocolo: se pierde la elección, no se rompe nada.

## Open Questions

- **La migración de estilo, ¿va?** El punto 4 cambia el aspecto de toda la aplicación. Se decide
  viendo el punto 2 en pantalla, no antes. Si la respuesta es que no, el catálogo sirve igual para
  el selector y los puntos 1 a 3 quedan en pie.
- **Los glifos propios sin equivalente.** De los 30 actuales hay varios —el de elevar, el de
  restablecer, el de borrar historial— cuyo concepto puede no estar en el catálogo cerrado. Se listan
  al empezar el punto 4; si falta alguno, se anota como pendiente y no se reemplaza por uno que
  signifique otra cosa.
- **Los sinónimos de búsqueda.** El catálogo trae etiqueta pero no sinónimos: «vm» para máquina
  virtual, «db» para base de datos, «fw» para cortafuegos. Se escriben a mano al armar el catálogo y
  conviene revisarlos con el usuario, porque son las palabras con las que él va a buscar.
