## ADDED Requirements

### Requirement: Una copia que falla se ve

Cuando la copia de arranque falla, la barra de estado SHALL mostrar el motivo. «No hizo falta» y
«falló» SHALL distinguirse en el resultado.

#### Scenario: Carpeta de copias inaccesible

- **WHEN** la carpeta de copias no existe o no se puede escribir
- **THEN** la barra de estado muestra el motivo del fallo

#### Scenario: Copia reciente ya existente

- **WHEN** ya hay una copia de hoy
- **THEN** la barra de estado no muestra error
