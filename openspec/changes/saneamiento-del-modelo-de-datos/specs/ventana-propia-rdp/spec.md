## ADDED Requirements

### Requirement: La bandera se llama como lo que guarda

`rdp_settings.abre_en_ventana_propia` y `RdpSettings.AbreEnVentanaPropia` SHALL reemplazar a
`start_full_screen` y `StartFullScreen` conservando los valores existentes.

#### Scenario: Migración conserva el valor

- **WHEN** una conexión tenía la bandera vieja encendida
- **THEN** después de migrar `abre_en_ventana_propia` está encendida

### Requirement: La bandera se puede apagar

El editor SHALL mostrar la casilla «Abrir en ventana propia» en la pestaña RDP, y una sesión que se
traslada con éxito a una pestaña SHALL apagarla.

#### Scenario: Apagar desde el editor

- **WHEN** el usuario destilda la casilla y guarda
- **THEN** la próxima sesión abre en pestaña

#### Scenario: Traslado exitoso

- **WHEN** una sesión con la bandera encendida se traslada a la pestaña sin fallar
- **THEN** la bandera queda apagada
