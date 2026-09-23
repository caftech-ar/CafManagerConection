## ADDED Requirements

### Requirement: Los SVG viven en el repo agrupados por tema

Los 250 iconos SHALL estar bajo `Assets/Iconos` del proyecto de la aplicación como archivos `.svg`
sin modificar respecto del paquete de origen, repartidos en `conceptos/<grupo>/` y `logos/<grupo>/`
según su grupo del catálogo. El nombre del archivo SHALL ser el del paquete de origen, sin inventar
ni renombrar.

#### Scenario: Ubicar un concepto

- **WHEN** se busca el icono de servidor, que pertenece al grupo «Servidores y hosts»
- **THEN** está en `Assets/Iconos/conceptos/servidores-y-hosts/server.svg`

#### Scenario: Ubicar un logo

- **WHEN** se busca el logo de PostgreSQL, que pertenece al grupo «Motores de datos»
- **THEN** está en `Assets/Iconos/logos/motores-de-datos/postgresql.svg`

#### Scenario: Un icono que el catálogo no nombra

- **WHEN** hace falta un icono que no está en el catálogo cerrado
- **THEN** no se agrega un SVG suelto: se agrega primero la entrada al catálogo, con su grupo y su
  origen

### Requirement: La conversión a geometría la hace un script

Un conversor SHALL traducir cada SVG a un `StreamGeometry` y emitir un diccionario de recursos por
grupo. Su salida SHALL versionarse, para que el build y el diseñador no dependan de él. La
aplicación NO SHALL parsear SVG en ejecución, ni referenciar una biblioteca de SVG.

#### Scenario: Correr el conversor

- **WHEN** se corre el conversor
- **THEN** existe un `Iconos.<Grupo>.xaml` por cada grupo, con una geometría por icono del grupo

#### Scenario: Agregar un SVG

- **WHEN** se suma un SVG a una carpeta de grupo y se corre el conversor
- **THEN** su geometría aparece en el diccionario de ese grupo sin editar ningún XAML a mano

#### Scenario: Correrlo de nuevo sin cambios

- **WHEN** se corre dos veces seguidas sin tocar ningún SVG
- **THEN** ningún diccionario cambia

#### Scenario: Un diccionario que quedó atrás

- **WHEN** se edita un SVG y no se corre el conversor
- **THEN** la suite falla nombrando el diccionario desactualizado

### Requirement: Los trazos múltiples de un icono se unen en una geometría

Los conceptos de Tabler traen varios elementos de dibujo que comparten grosor, remate y unión de
trazo. El conversor SHALL concatenarlos en una sola geometría, y SHALL descartar el rectángulo
transparente que Tabler usa como lienzo.

#### Scenario: Icono de varios trazos

- **WHEN** se convierte un icono cuyo SVG tiene ocho elementos de dibujo
- **THEN** el diccionario tiene una sola geometría con los ocho recorridos

#### Scenario: Lienzo transparente

- **WHEN** el SVG trae el recorrido del lienzo de 24×24 sin pintura
- **THEN** ese recorrido no entra en la geometría

#### Scenario: Un SVG que no se puede unir

- **WHEN** un SVG trae elementos con grosores o remates distintos entre sí
- **THEN** el conversor falla nombrando el archivo, en vez de emitir una geometría que se dibuja mal

### Requirement: Cada icono del catálogo se describe a sí mismo

`CatalogoDeIconos` SHALL exponer, por cada uno de los 250 iconos: clave estable, grupo, familia
—concepto o logo—, etiqueta en español, sinónimos de búsqueda, modo de pintado —relleno o trazo— y
su origen.

#### Scenario: Consultar un concepto

- **WHEN** se pide la entrada de `server`
- **THEN** devuelve grupo «Servidores y hosts», familia concepto, etiqueta «servidor» y modo trazo

#### Scenario: Consultar un logo sólido

- **WHEN** se pide la entrada de `postgresql`
- **THEN** devuelve familia logo y modo relleno

#### Scenario: Consultar un logo de trazo

- **WHEN** se pide la entrada de `brand-docker`, que viene de la misma familia de trazo que los
  conceptos
- **THEN** devuelve familia logo y modo trazo

### Requirement: Cada icono dice de dónde salió

Cada icono SHALL llevar su procedencia en tres lugares, y los tres SHALL coincidir: el SVG del repo,
la entrada del catálogo y la geometría generada. La procedencia SHALL nombrar paquete de origen,
versión, ruta dentro del paquete y licencia.

#### Scenario: Abrir el SVG

- **WHEN** se abre `Assets/Iconos/conceptos/servidores-y-hosts/server.svg`
- **THEN** arriba del dibujo hay un comentario que dice de qué paquete, versión, ruta y licencia
  salió, sin tocar el dibujo en sí

#### Scenario: Preguntárselo al catálogo

- **WHEN** se pide el origen de la entrada de `server`
- **THEN** devuelve paquete `@tabler/icons`, versión `3.47.0`, ruta `icons/outline/server.svg` y
  licencia MIT

#### Scenario: Leer la geometría generada

- **WHEN** se abre el diccionario generado de un grupo
- **THEN** cada geometría tiene encima un comentario con el origen del icono que dibuja

#### Scenario: Origen que no coincide

- **WHEN** el origen que declara el catálogo no coincide con el del comentario del SVG
- **THEN** la prueba falla nombrando el icono

#### Scenario: Rastrear un logo de Devicon

- **WHEN** se pide el origen del logo de SQL Server
- **THEN** devuelve paquete `devicon`, versión `2.17.0`, ruta
  `icons/microsoftsqlserver/microsoftsqlserver-line.svg` y licencia MIT

### Requirement: Toda clave del catálogo tiene su geometría

Cada clave que `CatalogoDeIconos` publica SHALL resolver a una geometría existente en los
diccionarios generados. Una clave sin geometría SHALL hacer fallar la suite de pruebas.

#### Scenario: Recorrer el catálogo

- **WHEN** se recorren las 250 claves y se busca la geometría de cada una
- **THEN** las 250 resuelven, y no sobra ninguna geometría generada

#### Scenario: Una clave huérfana

- **WHEN** el catálogo nombra una clave cuyo SVG no está bajo `Assets/Iconos`
- **THEN** la prueba falla nombrando la clave

### Requirement: Una clave desconocida no rompe la interfaz

Al resolver la clave de icono guardada de una conexión o de una carpeta, una clave que el catálogo
no reconozca SHALL caer en el icono por omisión que corresponde a su protocolo, y las carpetas en el
icono de carpeta. La interfaz NO SHALL quedar sin icono ni lanzar una excepción.

#### Scenario: Clave que ya no existe

- **WHEN** una conexión SSH guardada tiene una clave de icono que el catálogo nuevo no contiene
- **THEN** se dibuja con el icono de terminal, el de omisión de SSH

#### Scenario: Carpeta con clave desconocida

- **WHEN** una carpeta tiene una clave de icono que el catálogo no contiene
- **THEN** se dibuja con el icono de carpeta

#### Scenario: Sin icono elegido

- **WHEN** una conexión nunca eligió icono y su clave es nula
- **THEN** se dibuja con el icono de omisión de su protocolo, como hasta ahora

### Requirement: La licencia de cada origen viaja con los iconos

El README SHALL declarar la licencia de cada paquete de origen. La versión NO SHALL escribirse ahí,
porque el README sólo nombra la versión del producto: vive en el comentario de cada SVG y en el
catálogo. Los logos de producto SHALL usarse sólo para identificar ese producto.

#### Scenario: Revisar la licencia

- **WHEN** se busca bajo qué licencia entró cada paquete
- **THEN** el README declara la de Tabler, la de Simple Icons y la de Devicon

#### Scenario: Revisar la versión

- **WHEN** se busca de qué versión salió un icono
- **THEN** la dicen su SVG y su entrada del catálogo, y la suite exige que coincidan

#### Scenario: Uso de una marca

- **WHEN** se elige el logo de un producto para un icono de la aplicación o de una ventana
- **THEN** no se usa: los logos identifican al producto conectado, no a la aplicación
