## ADDED Requirements

### Requirement: La importación no escribe notas de plantilla

Una conexión importada SHALL quedar con `notes` vacío salvo que el origen traiga una nota propia. El
origen y el tipo original SHALL informarse en el resultado de la importación.

#### Scenario: Importar desde RDM

- **WHEN** se importa una conexión de Remote Desktop Manager sin notas
- **THEN** la conexión queda sin notas y el resultado de importación indica origen y tipo

### Requirement: La deduplicación compara por el protocolo real

Reimportar SHALL reconocer como existente una conexión con el mismo host, puerto, usuario y protocolo,
para los tres protocolos.

#### Scenario: Reimportar RDP y Web

- **WHEN** se importa dos veces el mismo archivo con conexiones RDP y Web
- **THEN** la segunda importación no crea duplicados

### Requirement: El dominio importado sólo aplica a RDP

`ConexionImportada.Dominio` SHALL quedar vacío cuando el protocolo no es RDP.

#### Scenario: Entrada SSH con dominio en el origen

- **WHEN** el archivo de origen trae dominio para una entrada SSH
- **THEN** la conexión importada no tiene dominio
