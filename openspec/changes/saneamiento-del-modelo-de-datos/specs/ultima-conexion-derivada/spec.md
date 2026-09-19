## ADDED Requirements

### Requirement: La última conexión se deriva del historial

La columna `connections.last_connected_at` NO SHALL existir. La «última conexión» de una conexión
SHALL ser el mayor `attempted_at` de sus filas de historial con resultado `Success`.

#### Scenario: Conexión con historial

- **WHEN** una conexión tiene tres filas de historial y la más reciente exitosa es de ayer
- **THEN** el tooltip muestra ayer como última conexión

#### Scenario: Conexión sin éxito registrado

- **WHEN** una conexión sólo tiene filas `Failed` o ninguna
- **THEN** el tooltip indica que no hay conexiones exitosas registradas

### Requirement: Abrir una entrada Web registra historial

Abrir una entrada Web en el navegador SHALL registrar una fila de historial con resultado `Success`,
sin duración y sin motivo.

#### Scenario: Apertura Web

- **WHEN** el usuario abre una entrada Web desde el árbol
- **THEN** el historial gana una fila `Success` para esa conexión y la última conexión la refleja
