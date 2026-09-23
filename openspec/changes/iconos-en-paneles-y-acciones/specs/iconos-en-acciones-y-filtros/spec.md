## ADDED Requirements

### Requirement: El icono de una acción señala, no acompaña

Un control que hace algo SHALL llevar icono cuando el icono ayuda a encontrarlo o a no equivocarse, y
NO SHALL llevarlo por consistencia sola. Si todas las entradas de un menú llevan icono, ninguna
resalta y el icono deja de ser señal.

#### Scenario: Una acción que se busca de un vistazo

- **WHEN** una acción es la principal de su menú, o borra algo
- **THEN** lleva icono

#### Scenario: Una acción del montón

- **WHEN** una acción no es la principal ni destruye nada
- **THEN** no lleva icono, y queda alineada con las que sí

### Requirement: En el menú del árbol todas las entradas llevan icono

El menú contextual del árbol SHALL tener icono en todas sus entradas. Catorce de dieciocho ya lo
tenían, así que la línea del menú está tomada: lo que falta es completarla. Las entradas destacadas
—conectar, abrir en el navegador, abrir todas— SHALL llevarlo, porque hoy son justamente las únicas
sin uno.

#### Scenario: Conectar

- **WHEN** se abre el menú de una conexión
- **THEN** la entrada de conectar lleva su icono y va en negrita, como la acción principal

#### Scenario: Eliminar

- **WHEN** se abre el menú
- **THEN** la entrada de eliminar lleva su icono con el pincel destructivo

#### Scenario: Ninguna entrada queda sin icono

- **WHEN** se recorre el menú de una conexión, el de una carpeta y el de una entrada web
- **THEN** todas sus entradas llevan icono

### Requirement: Los tres filtros del árbol se ven iguales

Los filtros de Favoritas, SSH y RDP SHALL llevar cada uno su icono, del mismo tamaño y en la misma
posición. Hoy sólo Favoritas lo tiene.

#### Scenario: Los tres juntos

- **WHEN** se mira la fila de filtros
- **THEN** los tres llevan icono a la izquierda de su texto, del mismo tamaño

#### Scenario: El de cada protocolo

- **WHEN** se mira el filtro de SSH y el de RDP
- **THEN** llevan el mismo icono con el que el árbol dibuja una conexión de ese protocolo

### Requirement: Los botones de barra y los destructivos llevan icono

Un botón de barra de herramientas de un panel SHALL llevar icono, porque es chico y se escanea. Un
botón que borra o descarta algo SHALL llevarlo con el pincel destructivo. Un botón de diálogo que no
destruye —Guardar, Cancelar, Aceptar— NO SHALL llevarlo.

#### Scenario: Barra de un panel

- **WHEN** un panel tiene botones de refrescar, iniciar o detener
- **THEN** cada uno lleva su icono

#### Scenario: Confirmar un borrado

- **WHEN** un diálogo ofrece eliminar
- **THEN** ese botón lleva el icono de eliminar con el pincel destructivo, y el de cancelar no lleva
  ninguno

#### Scenario: Guardar

- **WHEN** un diálogo ofrece guardar y cancelar
- **THEN** ninguno de los dos lleva icono: con dos botones y su texto no hay nada que encontrar

### Requirement: Cada cosa que dibuja la interfaz se nombra en un solo lugar

Toda clave de icono que use un panel, un menú, un filtro o un botón SHALL declararse en
`IconosDeLaInterfaz`. Ninguna pantalla SHALL escribir una clave del catálogo a mano.

#### Scenario: Agregar un icono a una pantalla

- **WHEN** una pantalla necesita dibujar algo nuevo
- **THEN** se agrega la constante y la pantalla la usa

#### Scenario: Buscar claves sueltas

- **WHEN** se recorren los XAML y el código de la aplicación buscando claves del catálogo escritas
  como texto
- **THEN** no hay ninguna fuera de `IconosDeLaInterfaz` y de los mapas de producto
