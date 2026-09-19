## ADDED Requirements

### Requirement: Una migración puede recrear una tabla dentro de la transacción

El motor de migraciones SHALL permitir que una migración baje y vuelva a crear una tabla referenciada
por claves foráneas sin salir de la transacción, con las claves foráneas desactivadas durante la
transacción y comprobadas antes de confirmar, y SHALL abortar la transacción si al final queda alguna
violación. Al terminar, con éxito o no, las claves foráneas SHALL quedar activas otra vez.

#### Scenario: Recrear una tabla con hijas

- **WHEN** una migración recrea `connections` mientras `rdp_settings` la referencia con `ON DELETE CASCADE`
- **THEN** la migración termina, las filas hijas siguen existiendo y apuntando a sus padres, y `user_version` avanza

#### Scenario: Una recreación deja huérfanas

- **WHEN** una migración recrea una tabla y omite copiar filas que otras referencian
- **THEN** la transacción se revierte, `user_version` no cambia y la base queda como estaba

### Requirement: Sólo la corrupción real aparta la base

El inicializador SHALL apartar la base y crear una nueva únicamente cuando el error de SQLite sea de
archivo corrupto o no reconocido como base de datos. Cualquier otro error SHALL propagarse sin tocar
el archivo.

#### Scenario: Archivo corrupto

- **WHEN** abrir la base devuelve el código de error de corrupción
- **THEN** el archivo se mueve a la ruta de preservación y se crea una base nueva

#### Scenario: Disco lleno o sin permisos

- **WHEN** abrir o migrar falla por un error de entrada y salida que no es corrupción
- **THEN** el archivo no se mueve, no se crea base nueva y el error llega a quien arrancó la aplicación

### Requirement: El usuario se entera antes de ver la base vacía

Cuando se aparta una base corrupta, la aplicación SHALL avisar antes de mostrar el árbol y SHALL
ofrecer restaurar desde una copia existente.

#### Scenario: Aviso previo con opción de restaurar

- **WHEN** el arranque apartó una base corrupta y hay copias en la carpeta configurada
- **THEN** se muestra el aviso con la ruta preservada y la opción de restaurar antes de cargar el árbol

### Requirement: Las migraciones se prueban desde cada versión anterior

Por cada versión de esquema publicada SHALL existir una prueba que cree una base en esa versión con
datos, corra el inicializador y verifique que los datos sobreviven y las columnas nuevas quedan en su
valor por omisión.

#### Scenario: Migrar desde la versión 1

- **WHEN** una base creada sólo con el esquema de la versión 1 y una carpeta con ajustes pasa por el inicializador
- **THEN** la carpeta sigue, las columnas agregadas después están en su valor por omisión y `user_version` es la última

#### Scenario: Migrar desde la versión 3

- **WHEN** una base en versión 3 con una carpeta que tiene usuario compartido, una conexión con `custom_fields` de conexión rápida y una fila de historial `Success` con motivo pasa por el inicializador
- **THEN** el usuario quedó en las tres columnas por protocolo, la conexión tiene `es_rapida = 1` y la fila de historial perdió el motivo
