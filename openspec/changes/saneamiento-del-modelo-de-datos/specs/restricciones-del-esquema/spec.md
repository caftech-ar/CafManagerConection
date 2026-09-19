## ADDED Requirements

### Requirement: Los booleanos sólo aceptan 0 y 1

`is_favorite`, `abre_en_ventana_propia`, `private_window`, `auto_start` y `es_rapida` SHALL rechazar
cualquier valor distinto de 0 y 1.

#### Scenario: Valor fuera de dominio

- **WHEN** se intenta escribir 2 en `is_favorite`
- **THEN** la base rechaza la escritura

### Requirement: Los textos obligatorios no pueden estar vacíos

`connections.name`, `connections.host`, `connection_folders.name` y `web_settings.url` SHALL rechazar
la cadena vacía o sólo espacios.

#### Scenario: Nombre en blanco

- **WHEN** se intenta guardar una conexión con nombre «   »
- **THEN** la base rechaza la escritura

### Requirement: El estado de cifrado es coherente con el vault

El arranque SHALL comprobar que, si `vault` está vacía, ningún secreto tiene nonce, y que si tiene
fila, ningún secreto carece de nonce; una incoherencia SHALL registrarse y avisarse.

#### Scenario: Base coherente

- **WHEN** el vault está vacío y todos los secretos tienen nonce nulo
- **THEN** la comprobación pasa sin aviso

#### Scenario: Base mixta

- **WHEN** el vault tiene fila y un secreto tiene nonce nulo
- **THEN** se registra el identificador de la fila y el usuario ve un aviso
