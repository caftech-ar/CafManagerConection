## ADDED Requirements

### Requirement: Una carpeta tiene usuario y puerto sólo por protocolo

`folder_settings` SHALL guardar el usuario y el puerto únicamente en las columnas por protocolo. Las
columnas compartidas `username` y `port` NO SHALL existir, y la resolución de herencia NO SHALL tener
ningún valor de reserva compartido.

#### Scenario: El compartido se promueve al migrar

- **WHEN** una carpeta tenía usuario compartido «operador» y ningún usuario por protocolo antes de la migración
- **THEN** después de migrar tiene «operador» en RDP, SSH y Web, y las columnas compartidas ya no existen

#### Scenario: El compartido no pisa un valor por protocolo existente

- **WHEN** una carpeta tenía usuario compartido «operador» y usuario SSH «root»
- **THEN** después de migrar el SSH sigue siendo «root» y RDP y Web quedan en «operador»

#### Scenario: Vaciar un usuario por protocolo lo deja vacío

- **WHEN** el usuario borra el usuario SSH de una carpeta y guarda
- **THEN** una conexión SSH sin usuario propio bajo esa carpeta no hereda ningún usuario de ella

### Requirement: Los puertos por protocolo tienen rango

Las columnas `rdp_port`, `ssh_port` y `web_port` SHALL rechazar valores fuera de 1 a 65535, igual que
el puerto de una conexión.

#### Scenario: Puerto fuera de rango

- **WHEN** se intenta guardar una carpeta con puerto SSH 70000
- **THEN** el guardado falla con el mensaje de rango y nada se escribe
