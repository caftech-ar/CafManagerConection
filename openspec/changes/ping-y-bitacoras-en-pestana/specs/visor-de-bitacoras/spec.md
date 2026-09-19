## ADDED Requirements

### Requirement: Ver las bitácoras desde la aplicación

El menú contextual de una conexión SHALL ofrecer «Ver bitácoras», que abre una pestaña con las
bitácoras escritas, listadas de la más reciente a la más vieja, filtradas por esa conexión.

#### Scenario: Abrir desde una conexión

- **WHEN** se elige «Ver bitácoras» sobre una conexión que tiene registros
- **THEN** se abre una pestaña con los suyos, el más reciente arriba

#### Scenario: Una conexión sin bitácoras

- **WHEN** esa conexión no tiene ninguna
- **THEN** la pestaña lo dice, y deja quitar el filtro para ver las demás

#### Scenario: La carpeta no existe

- **WHEN** todavía no se escribió ninguna bitácora
- **THEN** la pestaña informa que no hay registros, sin fallar

### Requirement: Filtrar el listado

El listado SHALL poder filtrarse por conexión y SHALL mostrar de cada bitácora la conexión, el host, la
fecha y el tamaño.

#### Scenario: Quitar el filtro

- **WHEN** se quita el filtro de conexión
- **THEN** se listan las bitácoras de todas las conexiones

#### Scenario: Filtrar por otra conexión

- **WHEN** se elige otra conexión en el filtro
- **THEN** el listado pasa a mostrar las de ésa

### Requirement: Leer una bitácora sin salir de la aplicación

Al elegir una bitácora del listado, el sistema SHALL mostrar su contenido en la misma pestaña,
empezando por el final, y SHALL permitir recorrerlo y buscar dentro.

#### Scenario: Abrir un registro

- **WHEN** se elige una bitácora del listado
- **THEN** se muestra su contenido, con el final a la vista

#### Scenario: Un archivo grande

- **WHEN** la bitácora pesa cientos de megas
- **THEN** se muestra igual, cargando de a partes, sin que la aplicación deje de responder

#### Scenario: Buscar dentro

- **WHEN** se busca un texto en la bitácora abierta
- **THEN** se recorren las coincidencias

#### Scenario: El archivo ya no está

- **WHEN** se elige una bitácora que la purga borró mientras la pestaña estaba abierta
- **THEN** se informa y el listado se actualiza, sin fallar
