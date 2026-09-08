## ADDED Requirements

### Requirement: Tres modos de la tira de pestañas

El sistema SHALL ofrecer una preferencia global con tres modos para la tira de pestañas de sesión, y
el predeterminado SHALL ser el lineal con desplazamiento (el actual). La preferencia SHALL guardarse
como una clave de `application_settings` (sin cambio de esquema).

#### Scenario: Valor por omisión

- **WHEN** nunca se eligió un modo
- **THEN** la tira usa el modo lineal con desplazamiento

#### Scenario: Cambiar el modo se aplica sin reabrir

- **WHEN** el usuario cambia el modo en Preferencias con sesiones abiertas
- **THEN** la tira cambia de disposición sin cerrar ni reconectar las sesiones

### Requirement: Modo lineal con desplazamiento

En este modo el sistema SHALL disponer las pestañas en una sola fila y, cuando no entran, la fila
SHALL poder desplazarse; es el comportamiento actual.

#### Scenario: Más pestañas que ancho

- **WHEN** hay más pestañas de las que entran a lo ancho
- **THEN** la fila se puede desplazar para alcanzarlas

### Requirement: Modo lineal con desplegable de sobra

En este modo el sistema SHALL mostrar en la fila las pestañas que entran y SHALL ofrecer las que no
entran en un desplegable a la derecha; elegir una del desplegable la activa.

#### Scenario: Sobra que no entra

- **WHEN** hay más pestañas de las que entran a lo ancho
- **THEN** las que no entran aparecen en un desplegable a la derecha, y elegir una la activa

#### Scenario: Entran todas

- **WHEN** todas las pestañas entran a lo ancho
- **THEN** no se muestra el desplegable de sobra

### Requirement: Modo envuelto en varias líneas

En este modo el sistema SHALL apilar las pestañas en varias filas al superar el ancho disponible,
todas visibles a la vez, sin desplazamiento ni desplegable.

#### Scenario: Superar el ancho

- **WHEN** las pestañas superan el ancho disponible
- **THEN** se apilan en varias filas sin desplazamiento ni desplegable
