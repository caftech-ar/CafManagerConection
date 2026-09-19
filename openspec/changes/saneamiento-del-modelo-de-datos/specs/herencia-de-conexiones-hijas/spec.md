## ADDED Requirements

### Requirement: Una hija está siempre en la carpeta de su padre

Toda conexión con `parent_connection_id` SHALL tener el mismo `folder_id` que su padre. El editor
SHALL derivar la carpeta del padre elegido y no permitir elegir otra.

#### Scenario: Elegir un padre en el editor

- **WHEN** en el editor se elige como padre una conexión de la carpeta «Producción»
- **THEN** el combo de carpeta se deshabilita mostrando «Producción» y la conexión se guarda en esa carpeta

#### Scenario: Migración corrige las hijas existentes

- **WHEN** antes de migrar una hija está en una carpeta distinta a la de su padre
- **THEN** después de migrar está en la carpeta del padre

### Requirement: Mover un padre arrastra a sus hijas

Mover a otra carpeta una conexión que tiene hijas SHALL mover también a las hijas.

#### Scenario: Mover padre con hijas

- **WHEN** se mueve a «Pruebas» un padre con dos hijas en «Producción»
- **THEN** las dos hijas quedan en «Pruebas» y siguen colgando del padre
