## ADDED Requirements

### Requirement: custom_fields sólo acepta JSON válido

Las columnas `custom_fields` de `connections` y `folder_settings` SHALL rechazar cualquier texto que no
sea JSON válido.

#### Scenario: Escritura inválida

- **WHEN** se intenta guardar `custom_fields` con texto que no es JSON
- **THEN** la base rechaza la escritura

#### Scenario: Fila inválida previa a la migración

- **WHEN** antes de migrar una fila tiene `custom_fields` con texto que no es JSON
- **THEN** después de migrar esa fila tiene `custom_fields` nulo y la migración terminó

### Requirement: Un JSON ilegible se registra

Si al interpretar un texto de `custom_fields` la deserialización falla, `Serializacion` SHALL devolver
un diccionario vacío y SHALL registrar el error con el identificador de la fila.

#### Scenario: Texto ilegible

- **WHEN** se interpreta un texto que no es JSON con un registrador presente
- **THEN** el resultado es vacío y el registrador recibió una entrada con el identificador

### Requirement: La marca de conexión rápida es una columna

`connections.es_rapida` SHALL indicar si la conexión es una conexión rápida. El filtrado del árbol y
la limpieza de huérfanas SHALL usar la columna y no `custom_fields`.

#### Scenario: Migración de la marca

- **WHEN** antes de migrar una conexión tiene la clave reservada de conexión rápida en `custom_fields`
- **THEN** después de migrar tiene `es_rapida = 1` y la clave ya no está en `custom_fields`

### Requirement: Las claves propias de carpeta no distinguen mayúsculas

`FolderSettings.CustomFields` SHALL comparar claves sin distinguir mayúsculas, igual que `Connection`.

#### Scenario: Ida y vuelta de carpeta

- **WHEN** se guarda una carpeta con una clave propia y se vuelve a cargar
- **THEN** la clave se encuentra aunque se consulte con otra capitalización
