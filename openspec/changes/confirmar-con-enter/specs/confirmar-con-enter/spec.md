## ADDED Requirements

### Requirement: Enter confirma y Escape cancela

Los avisos y confirmaciones de la aplicación SHALL ejecutar la acción de su botón principal cuando se
pulse Enter, y SHALL cerrarse sin hacer nada cuando se pulse Escape.

#### Scenario: Confirmar un pegado

- **WHEN** aparece la confirmación de pegado y se pulsa Enter
- **THEN** el pegado se hace, igual que si se hubiera pulsado el botón

#### Scenario: Cancelar con Escape

- **WHEN** aparece una confirmación y se pulsa Escape
- **THEN** se cierra sin ejecutar la acción

#### Scenario: Aviso de un solo botón

- **WHEN** aparece un aviso que sólo se puede aceptar y se pulsa Enter
- **THEN** se cierra

### Requirement: En lo destructivo, Enter no rompe nada

En una confirmación cuya acción principal borra o descarta algo, el foco inicial SHALL estar en la
opción que no destruye, de modo que Enter cancele.

#### Scenario: Confirmar un borrado

- **WHEN** aparece la confirmación de eliminar una conexión y se pulsa Enter sin mover el foco
- **THEN** no se elimina nada: se cancela

#### Scenario: Confirmar explícitamente

- **WHEN** en esa misma confirmación se pulsa el botón de eliminar, o se tabula hasta él y se pulsa
  Enter
- **THEN** se elimina

### Requirement: Un campo de texto se queda con el Enter que necesita

En un diálogo que pide escribir algo, Enter SHALL confirmar lo escrito, salvo en un campo de varias
líneas, donde SHALL insertar un salto de línea.

#### Scenario: Pedido de una línea

- **WHEN** se escribe el nombre en un pedido de texto de una línea y se pulsa Enter
- **THEN** se acepta lo escrito

#### Scenario: Campo de varias líneas

- **WHEN** el foco está en un campo de varias líneas y se pulsa Enter
- **THEN** se inserta un salto de línea y el diálogo sigue abierto
