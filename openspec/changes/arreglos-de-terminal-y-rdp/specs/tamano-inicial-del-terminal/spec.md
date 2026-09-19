## ADDED Requirements

### Requirement: La sesión SSH se abre con el tamaño real del terminal

El sistema SHALL abrir la sesión SSH pidiendo un pseudo-terminal del tamaño que tiene el terminal en
pantalla, no del tamaño con el que el control se construye. El sistema SHALL esperar a que el terminal
esté medido antes de armar el pedido de sesión.

#### Scenario: Ventana grande

- **WHEN** se conecta con el terminal ocupando 200 columnas por 50 filas
- **THEN** la sesión se abre con un pseudo-terminal de 200 por 50, no de 80 por 24

#### Scenario: El servidor dimensiona su salida al tamaño correcto

- **WHEN** el servidor manda un banner de bienvenida de dieciocho líneas y el terminal tiene cincuenta
  filas
- **THEN** el banner entra entero en pantalla sin desplazar nada

### Requirement: Al conectar, el prompt no queda desplazado

Tras conectar, el sistema SHALL dejar el contenido de la sesión desde el borde superior del terminal, y
SHALL NOT desplazar el historial cuando lo recibido entra en la pantalla.

#### Scenario: La bienvenida entra en pantalla

- **WHEN** lo que manda el servidor al conectar entra en la pantalla del terminal
- **THEN** el prompt queda a continuación de esa salida, sin filas en blanco debajo ni contenido en el
  historial

#### Scenario: La bienvenida no entra en pantalla

- **WHEN** el servidor manda al conectar más líneas de las que entran en la pantalla
- **THEN** el terminal desplaza lo necesario y el prompt queda en la última fila, que es el
  comportamiento correcto

#### Scenario: Redimensionar después de conectar

- **WHEN** se cambia el tamaño de la ventana con la sesión ya establecida
- **THEN** el terminal y el servidor se ajustan al tamaño nuevo, como hasta ahora

### Requirement: Achicar el alto conserva el final de la sesión

Al reducirse el alto del terminal, el sistema SHALL conservar las últimas filas —las que contienen el
cursor y lo más reciente— y SHALL mandar al historial las que salen por arriba, en lugar de descartar
el final de la pantalla.

#### Scenario: Se angosta la ventana con contenido en pantalla

- **WHEN** el terminal tiene veinte filas con contenido, el cursor en la última, y pasa a cinco filas
- **THEN** en pantalla quedan las últimas cinco y el cursor sigue sobre su línea

#### Scenario: Lo que sale por arriba no se pierde

- **WHEN** se reduce el alto y quince filas dejan de entrar en pantalla
- **THEN** esas quince quedan en el historial de desplazamiento

#### Scenario: El cursor está arriba

- **WHEN** el cursor está en la tercera fila y el alto se reduce a diez
- **THEN** no se descarta nada por encima del cursor y el cursor conserva su posición

#### Scenario: Agrandar el alto

- **WHEN** se aumenta el alto del terminal
- **THEN** lo que había se conserva y las filas nuevas aparecen vacías debajo

### Requirement: Achicar el ancho no pierde lo escrito

Al reducirse el ancho del terminal, el sistema SHALL conservar lo que cada línea tiene más allá del
borde derecho, y SHALL volver a mostrarlo cuando el ancho crezca. El sistema SHALL NOT reacomodar el
texto en varias líneas: mientras el terminal esté angosto, la línea se ve cortada.

#### Scenario: Se abre un panel al lado

- **WHEN** una línea de veinticinco caracteres queda con doce columnas de ancho
- **THEN** se ve cortada a doce caracteres

#### Scenario: Se cierra el panel

- **WHEN** después de eso el terminal vuelve a su ancho anterior
- **THEN** la línea se ve entera de nuevo

#### Scenario: Lo oculto también va al historial

- **WHEN** una línea más ancha que la vista sale de la pantalla por desplazamiento
- **THEN** el historial guarda la línea completa, no la parte visible

#### Scenario: Borrar alcanza lo que no se ve

- **WHEN** el servidor borra una línea con el terminal angosto y después se ensancha
- **THEN** la línea sigue vacía: no reaparece lo que había más allá del borde
