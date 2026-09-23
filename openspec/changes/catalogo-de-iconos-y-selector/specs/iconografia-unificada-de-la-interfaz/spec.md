## ADDED Requirements

### Requirement: Un solo estilo visual en toda la pantalla

Todos los iconos que la aplicación dibuja SHALL venir del catálogo. NO SHALL quedar geometrías de
icono declaradas a mano en los diccionarios de estilos ni en el código.

#### Scenario: Revisar los estilos

- **WHEN** se buscan geometrías de icono en `Estilos.xaml`
- **THEN** no hay ninguna: las 30 que había migraron al catálogo

#### Scenario: Revisar el código

- **WHEN** se buscan geometrías de icono escritas en C#
- **THEN** no hay ninguna

#### Scenario: Dos iconos juntos

- **WHEN** se ve un icono de la barra al lado de un icono del árbol
- **THEN** los dos son del mismo estilo, con el mismo peso de trazo

### Requirement: Cada glifo propio tiene su reemplazo nombrado

Cada uno de los 30 iconos que hoy declara la aplicación SHALL tener una clave del catálogo que lo
reemplaza, elegida por lo que el icono significa y no por su parecido.

#### Scenario: Reemplazo de un icono de panel

- **WHEN** se busca con qué queda el icono del panel de archivos
- **THEN** hay una clave del catálogo asignada, y la interfaz la usa

#### Scenario: Un glifo sin reemplazo en el catálogo

- **WHEN** un glifo propio no tiene concepto equivalente en el catálogo cerrado
- **THEN** queda anotado como pendiente y no se reemplaza por uno que signifique otra cosa

### Requirement: Los mapas de producto pasan a los logos reales

El mapa de proceso a icono y el de aplicación conocida a icono SHALL usar el logo del producto cuando
el catálogo lo tiene, y el concepto genérico cuando no.

#### Scenario: Proceso con logo

- **WHEN** el panel de procesos encuentra `nginx`
- **THEN** lo dibuja con el logo de nginx, no con un icono genérico de servidor web

#### Scenario: Proceso sin logo

- **WHEN** el panel encuentra un proceso cuyo producto no tiene logo en el catálogo
- **THEN** lo dibuja con el concepto que corresponde a su clase de aplicación

#### Scenario: Proceso desconocido

- **WHEN** el panel encuentra un proceso que no reconoce
- **THEN** lo dibuja con el concepto de aplicación, como hasta ahora

### Requirement: El color de los iconos sigue funcionando igual

La paleta de diez colores y los pinceles por tema SHALL seguir vigentes. El color elegido a mano en
una conexión, una carpeta o una etiqueta SHALL pintar el icono nuevo igual que pintaba el viejo.

#### Scenario: Conexión con color elegido

- **WHEN** una conexión tiene el color violeta elegido a mano
- **THEN** su icono nuevo se dibuja violeta

#### Scenario: Color por omisión del protocolo

- **WHEN** una conexión RDP no eligió color
- **THEN** su icono se dibuja con el color de omisión de RDP

#### Scenario: Etiqueta con color

- **WHEN** una etiqueta tiene un color de la paleta
- **THEN** se sigue dibujando con ese color

### Requirement: La migración no cambia lo que hay guardado

Cambiar los glifos de la interfaz NO SHALL tocar el esquema de la base, ni las claves de icono ni las
de color guardadas en las conexiones, las carpetas y las etiquetas.

#### Scenario: Abrir una base anterior

- **WHEN** se abre una base guardada antes de la migración
- **THEN** las conexiones conservan su color, y las que tienen icono elegido lo conservan o caen en
  el de su protocolo si su clave ya no existe

#### Scenario: Revisar el esquema

- **WHEN** se compara el esquema de la base antes y después
- **THEN** es el mismo: ninguna tabla ni columna cambió
