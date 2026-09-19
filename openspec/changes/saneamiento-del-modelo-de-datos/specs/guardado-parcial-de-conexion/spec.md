## ADDED Requirements

### Requirement: La sesión no reescribe la conexión entera

Recordar la huella del host SSH o que una sesión RDP abre en ventana propia SHALL escribir únicamente
esa columna. Una edición hecha mientras la sesión estaba abierta SHALL sobrevivir.

#### Scenario: Editar con la sesión abierta

- **WHEN** se abre una sesión SSH, se renombra la conexión desde el editor y luego la sesión acepta la huella del host
- **THEN** la conexión conserva el nombre nuevo y gana la huella

### Requirement: Guardar no borra los ajustes de protocolo

Actualizar una conexión SHALL conservar la fila de ajustes de su protocolo aunque el registro que
llega no la traiga, actualizando sólo lo que viene.

#### Scenario: Registro sin ajustes

- **WHEN** se actualiza una conexión SSH con un registro que no trae `SshSettings`
- **THEN** `known_host_fingerprint` y `private_key_path` siguen en la base
