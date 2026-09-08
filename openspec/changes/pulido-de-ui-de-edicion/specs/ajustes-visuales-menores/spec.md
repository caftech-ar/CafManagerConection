## ADDED Requirements

### Requirement: Chips de filtro menos redondeados

Los chips de filtro del árbol (Favoritas, SSH, RDP) SHALL usar el radio de esquina del tema en lugar
de la forma de píldora, para que no desentonen con el resto de los controles.

#### Scenario: Se muestran los chips de filtro

- **WHEN** se muestra la barra de filtros del árbol
- **THEN** los chips tienen esquinas del radio del tema, no de píldora

### Requirement: El campo de filtro de la traza no se achica al enfocarse

El campo de filtro de la consola de traza SHALL mantener su ancho al recibir foco: ocultar el texto de
ayuda no SHALL cambiar su tamaño.

#### Scenario: Se hace foco en el filtro vacío

- **WHEN** el campo de filtro está vacío y el usuario le hace foco (se oculta la ayuda)
- **THEN** el campo conserva el mismo ancho que tenía sin foco
