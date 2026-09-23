## ADDED Requirements

### Requirement: Elegir icono abre una ventana de selección

En la ventana de la conexión y en la de la carpeta, elegir el icono SHALL abrir
`SelectorDeIconosWindow`, que muestra el catálogo agrupado. El panel de cuadrados fijos SHALL
desaparecer de las dos ventanas.

#### Scenario: Abrir desde la conexión

- **WHEN** en la ventana de la conexión se pulsa el icono actual
- **THEN** se abre la ventana de selección, con el catálogo agrupado y el icono actual marcado

#### Scenario: Abrir desde la carpeta

- **WHEN** en la ventana de la carpeta se pulsa el icono actual
- **THEN** se abre la misma ventana de selección

#### Scenario: Elegir uno

- **WHEN** se pulsa un icono de la grilla
- **THEN** la ventana se cierra y la ventana que la abrió muestra el icono nuevo

#### Scenario: Salir sin elegir

- **WHEN** se cierra la ventana con Escape o con el botón de cancelar
- **THEN** el icono de la conexión o de la carpeta no cambia

### Requirement: El buscador encuentra por nombre, etiqueta y sinónimos

El buscador SHALL filtrar por la clave del icono, por su etiqueta en español y por sus sinónimos,
sin distinguir mayúsculas ni acentos, mostrando las coincidencias parciales.

#### Scenario: Buscar por etiqueta

- **WHEN** se escribe «servidor»
- **THEN** aparecen los iconos del grupo de servidores cuya etiqueta contiene esa palabra

#### Scenario: Buscar sin acento

- **WHEN** se escribe «maquina virtual» sin acento
- **THEN** aparece el icono cuya etiqueta es «máquina virtual»

#### Scenario: Buscar por sinónimo

- **WHEN** se escribe «vm»
- **THEN** aparece el icono de máquina virtual, aunque su etiqueta no diga «vm»

#### Scenario: Buscar por nombre del archivo

- **WHEN** se escribe «terminal-2»
- **THEN** aparece ese icono

#### Scenario: Sin coincidencias

- **WHEN** lo escrito no coincide con ningún icono
- **THEN** la ventana lo dice con un mensaje, y no queda una grilla vacía sin explicación

### Requirement: Se puede filtrar por grupo y por familia

La ventana SHALL permitir acotar la grilla a un grupo del catálogo, y SHALL permitir ver sólo
conceptos, sólo logos, o las dos familias. Los filtros SHALL combinarse con lo escrito en el buscador.

#### Scenario: Filtrar por grupo

- **WHEN** se elige el grupo «Red»
- **THEN** la grilla muestra únicamente los iconos de ese grupo

#### Scenario: Sólo logos

- **WHEN** se elige ver sólo logos
- **THEN** la grilla muestra únicamente los logos de producto

#### Scenario: Filtro y búsqueda juntos

- **WHEN** se elige el grupo «Motores de datos» y se escribe «sql»
- **THEN** la grilla muestra sólo los iconos de ese grupo que coinciden con «sql»

#### Scenario: Limpiar los filtros

- **WHEN** se limpian el buscador y los filtros
- **THEN** vuelve a verse el catálogo entero agrupado

### Requirement: La grilla muestra los iconos agrupados y etiquetados

Sin filtro de grupo, la grilla SHALL mostrar los iconos bajo el encabezado de su grupo, en el orden
del catálogo. Cada icono SHALL mostrar su etiqueta y SHALL ofrecer su clave al detenerse encima.

#### Scenario: Recorrer el catálogo

- **WHEN** se abre la ventana sin escribir nada
- **THEN** se ven los grupos uno debajo del otro, cada uno con su encabezado y sus iconos

#### Scenario: Ver de qué es un icono

- **WHEN** se detiene el puntero sobre un icono
- **THEN** aparece su etiqueta y su clave

### Requirement: La grilla no ofrece los iconos de la propia interfaz

Los glifos con los que se dibuja la aplicación —buscar, filtrar, ordenar, expandir, las acciones de
las barras— SHALL quedar fuera de la ventana de selección: no identifican una conexión ni una
carpeta. Quedar afuera SHALL ser propiedad del icono y no de su grupo, porque «Organización y
navegación» mezcla esos glifos con `folder`, `star` y `tag`, que sí sirven.

#### Scenario: Buscar un icono de interfaz

- **WHEN** se escribe «filtrar», que es un glifo de la propia interfaz
- **THEN** no aparece en la grilla

#### Scenario: Buscar la carpeta, que está en el mismo grupo

- **WHEN** se escribe «carpeta»
- **THEN** aparece `folder`, que es el icono por omisión de toda carpeta

#### Scenario: Recorrer los grupos

- **WHEN** se despliega la lista de grupos
- **THEN** «Acciones» no está, porque no aporta ni un icono elegible, y «Organización y navegación»
  sí, porque aporta ocho

### Requirement: Se puede volver al icono por omisión

La ventana SHALL ofrecer una opción para no elegir icono, que devuelve la conexión o la carpeta al
icono que le toca por su protocolo.

#### Scenario: Quitar el icono elegido

- **WHEN** una conexión SSH con icono elegido a mano elige la opción de omisión
- **THEN** su clave de icono queda nula y vuelve a dibujarse con el icono de terminal

### Requirement: La ventana se maneja con el teclado

Al abrirse, el foco SHALL estar en el buscador. Las flechas SHALL recorrer la grilla, Enter SHALL
elegir el icono enfocado y Escape SHALL cerrar sin cambiar nada.

#### Scenario: Escribir al abrir

- **WHEN** se abre la ventana y se escribe
- **THEN** lo escrito va al buscador sin tener que pulsar nada antes

#### Scenario: Elegir con el teclado

- **WHEN** se escribe, se baja con las flechas hasta un icono y se pulsa Enter
- **THEN** ese icono queda elegido y la ventana se cierra

#### Scenario: Cancelar con Escape

- **WHEN** se pulsa Escape
- **THEN** la ventana se cierra sin cambiar el icono

### Requirement: Abrir la ventana no traba la aplicación

La ventana SHALL abrirse y responder al buscador sin demora perceptible con el catálogo entero
cargado.

#### Scenario: Abrir con el catálogo completo

- **WHEN** se abre la ventana
- **THEN** la grilla aparece dibujada, sin una espera visible

#### Scenario: Escribir en el buscador

- **WHEN** se escribe letra por letra en el buscador
- **THEN** la grilla se filtra a medida que se escribe, sin trabarse
