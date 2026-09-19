## ADDED Requirements

### Requirement: Toda consulta nombra sus columnas

Ningún repositorio SHALL usar `SELECT *`.

#### Scenario: Columna nueva sin mapear

- **WHEN** se agrega una columna a una tabla y no al DTO
- **THEN** la consulta sigue devolviendo exactamente las columnas que el DTO declara

### Requirement: El mapeo entre columnas y propiedades está declarado

Dapper SHALL configurarse una sola vez, en la capa que declara los DTOs, para emparejar `snake_case`
con `PascalCase`, y los DTOs SHALL usar nombres en `PascalCase`. La configuración SHALL estar activa
también cuando los repositorios se usan sin la aplicación, como en las pruebas.

#### Scenario: Columna con guión bajo

- **WHEN** una columna `tag_id` se lee en un DTO con propiedad `TagId`
- **THEN** el valor llega, no un nulo silencioso

### Requirement: Un texto ajeno al enum no tira la aplicación

Leer `protocol` o `auth_method` con un texto que no es un miembro del enum SHALL registrarse y caer a
un valor de reserva, en vez de lanzar.

#### Scenario: Fila editada a mano

- **WHEN** una fila trae `auth_method = 'Otro'`
- **THEN** la conexión carga con método automático y el log tiene una entrada

### Requirement: La ida y vuelta cubre todas las columnas

La prueba de ida y vuelta SHALL cubrir todas las columnas de `connections` que el repositorio lee y
escribe (todas menos las del secreto, que son del vault) y una fila completa de cada tabla de
protocolo.

#### Scenario: Borrar una columna sin tocar el DTO

- **WHEN** se quita una columna de `connections` y no del dominio
- **THEN** la prueba de ida y vuelta falla
