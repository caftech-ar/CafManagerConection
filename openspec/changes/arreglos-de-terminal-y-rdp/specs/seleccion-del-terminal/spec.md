## ADDED Requirements

### Requirement: La selección del terminal se limpia al desplazar el historial

El sistema SHALL limpiar la selección activa cuando el usuario desplace el historial del terminal con
la rueda del mouse, con el teclado, o arrastrando el pulgar de la barra de desplazamiento.

#### Scenario: Rueda del mouse

- **WHEN** hay texto seleccionado y se gira la rueda del mouse sobre el terminal
- **THEN** la selección se limpia y no queda nada pintado como seleccionado

#### Scenario: Arrastre del pulgar de la barra

- **WHEN** hay texto seleccionado y se arrastra el pulgar de la barra de desplazamiento
- **THEN** la selección se limpia

#### Scenario: Teclado

- **WHEN** hay texto seleccionado y se desplaza el historial con las teclas de página o de inicio y fin
  del historial
- **THEN** la selección se limpia

#### Scenario: El arrastre de la propia selección no la limpia

- **WHEN** se arrastra el mouse con el botón apretado más allá del borde superior del terminal, lo que
  desplaza el historial para seguir seleccionando
- **THEN** la selección se mantiene y se extiende, como hasta ahora

#### Scenario: Desplazar sin selección

- **WHEN** no hay nada seleccionado y se desplaza el historial
- **THEN** no cambia nada más que el desplazamiento
