## ADDED Requirements

### Requirement: La selección se ancla al texto, no a la fila de pantalla

El sistema SHALL guardar los extremos de la selección referidos a la línea del historial, de modo que
un desplazamiento del historial no cambie qué texto está seleccionado.

#### Scenario: Arrastrar más allá del borde superior

- **WHEN** se arrastra la selección hacia arriba pasando el borde y el terminal desplaza el historial
- **THEN** queda seleccionado todo el texto recorrido, incluido el que salió de la pantalla

#### Scenario: Copiar lo que no entra en pantalla

- **WHEN** se copia una selección que abarca más líneas de las que entran en la pantalla
- **THEN** se copia entera, no sólo la parte visible

#### Scenario: El texto seleccionado no cambia al desplazarse

- **WHEN** hay una selección hecha por arrastre y el historial se desplaza mientras se sigue arrastrando
- **THEN** los extremos siguen señalando el mismo texto

### Requirement: Desplazar sin seleccionar sigue soltando la selección

El sistema SHALL seguir limpiando la selección cuando el usuario desplace el historial con la rueda,
con el teclado o arrastrando la barra.

#### Scenario: Rueda con selección activa

- **WHEN** hay texto seleccionado y se gira la rueda
- **THEN** la selección se limpia

### Requirement: Lo que sale del historial recorta la selección

Cuando el historial descarte líneas por llegar a su tope, el sistema SHALL conservar la parte de la
selección que sigue existiendo y SHALL descartar la que ya no.

#### Scenario: El tope del historial alcanza a la selección

- **WHEN** hay una selección vieja y el historial descarta las líneas donde empezaba
- **THEN** la selección se recorta a lo que queda, sin soltarse entera

#### Scenario: La selección entera se va del historial

- **WHEN** el historial descarta todas las líneas que la selección abarcaba
- **THEN** no queda nada seleccionado
