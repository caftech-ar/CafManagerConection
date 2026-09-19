## ADDED Requirements

### Requirement: Las columnas sin lector no existen

El esquema SHALL quedar sin `rdp_settings.fit_to_tab`, `folder_settings.rdp_fit_to_tab`,
`ssh_settings.encoding`, `connections.documentation_url` ni `ssh_tunnels.sort_order`, y el dominio
SHALL quedar sin sus propiedades. `vault.creado_en` queda fuera de este cambio.

#### Scenario: Modo de tamaño sin fit_to_tab

- **WHEN** una conexión RDP no define modo de tamaño en sus campos propios
- **THEN** el modo efectivo es «escalar píxeles», igual que antes de quitar la columna

#### Scenario: Túneles ordenados por nombre

- **WHEN** una conexión tiene varios túneles
- **THEN** el editor y el panel los muestran en el mismo orden, alfabético por nombre

### Requirement: Los índices sin consulta no existen

`ix_connections_search` e `ix_connections_favorite` NO SHALL existir. Una prueba SHALL fijar la lista
de índices del esquema.

#### Scenario: Lista de índices

- **WHEN** se crea una base nueva
- **THEN** la lista de índices coincide exactamente con la que fija la prueba

### Requirement: Túneles sólo en conexiones SSH

El menú «Túneles…» SHALL mostrarse únicamente para conexiones SSH.

#### Scenario: Conexión RDP

- **WHEN** se abre el menú contextual de una conexión RDP
- **THEN** no aparece «Túneles…»

### Requirement: Sin código sostenido sólo por pruebas o sin efecto

El código SHALL quedar sin `FolderSettings.IsEmpty`, `ITunnelRepository.GetAllAsync` ni
`IConnectionHistoryRepository.GetForConnectionAsync`. `FolderRow.ToDomain` SHALL construir la
carpeta sin delegar en una segunda función. `AjustesDeActualizacion` SHALL quedar sin el parámetro
`Origen`, y el origen de releases SHALL salir de la constante del repositorio sin guardas.

#### Scenario: Origen de actualizaciones

- **WHEN** se consulta el origen de releases
- **THEN** sale de la constante del repositorio, sin guardas de origen vacío ni malformado
