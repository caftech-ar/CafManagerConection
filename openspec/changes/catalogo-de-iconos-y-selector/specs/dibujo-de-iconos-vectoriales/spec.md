## ADDED Requirements

### Requirement: Un solo control dibuja cualquier icono del catálogo

`IconoVectorial` SHALL dibujar un icono a partir de su clave del catálogo, su tamaño en unidades
independientes del dispositivo y el pincel con el que se pinta. Quien lo usa NO SHALL nombrar un
archivo, una geometría ni un recurso.

#### Scenario: Dibujar por clave

- **WHEN** se declara el control con la clave `server`, tamaño 16 y un pincel
- **THEN** dibuja el icono de servidor de 16 puntos con ese pincel

#### Scenario: Cambiar la clave en caliente

- **WHEN** cambia la clave del control mientras está en pantalla
- **THEN** dibuja el icono nuevo sin recrear el control

#### Scenario: Clave desconocida

- **WHEN** se le da una clave que el catálogo no tiene
- **THEN** dibuja el icono de desconocido, y no queda un hueco vacío

#### Scenario: Sin clave

- **WHEN** no se le da ninguna clave
- **THEN** no dibuja nada: no hay icono que mostrar, y un signo de pregunta en cada fila sin icono
  hace que ninguno signifique nada

### Requirement: El relleno y el trazo los decide el icono

El control SHALL pintar por relleno los iconos cuyo modo es relleno, y por trazo los iconos cuyo modo
es trazo. Quien usa el control NO SHALL tener que saber cuál es cuál.

#### Scenario: Concepto de trazo

- **WHEN** se dibuja `server`, que es de trazo
- **THEN** se pinta el contorno con el pincel, y el interior queda sin pintar

#### Scenario: Logo sólido

- **WHEN** se dibuja `postgresql`, que es de relleno
- **THEN** se pinta la silueta maciza con el pincel, y no se dibuja contorno

#### Scenario: Mismo pincel, los dos modos

- **WHEN** se dibujan un concepto de trazo y un logo de relleno con el mismo pincel
- **THEN** los dos quedan del mismo color

### Requirement: El grosor del trazo depende del tamaño

En los iconos de trazo, el grosor SHALL ser 1,5 hasta 20 puntos inclusive, y 2,0 de 24 puntos en
adelante. Los remates y las uniones de trazo SHALL ser redondeados.

#### Scenario: Icono chico

- **WHEN** se dibuja un icono de trazo a 16 puntos
- **THEN** el grosor del trazo es 1,5

#### Scenario: Icono de barra

- **WHEN** se dibuja un icono de trazo a 24 puntos
- **THEN** el grosor del trazo es 2,0

#### Scenario: Icono grande

- **WHEN** se dibuja un icono de trazo a 64 puntos
- **THEN** el grosor del trazo es 2,0

### Requirement: Las siluetas macizas se dibujan más chicas para pesar igual

Un icono de relleno SHALL ocupar el 88 % del alto de su caja, centrado. Un icono de trazo SHALL
ocupar la caja entera.

#### Scenario: Logo junto a un concepto

- **WHEN** se dibujan un logo de relleno y un concepto de trazo, los dos de 24 puntos, uno al lado
  del otro
- **THEN** el logo ocupa 21 puntos de alto y se ve del mismo peso visual que el concepto

### Requirement: El color entra desde afuera y sigue al tema

El pincel SHALL ser una propiedad del control, y NO SHALL estar escrito dentro del icono. Un control
al que se le da un pincel del tema SHALL repintarse solo cuando el tema cambia, sin reiniciar la
aplicación ni volver a armar la pantalla.

#### Scenario: Color de la paleta

- **WHEN** se le da a un icono el pincel azul de la paleta
- **THEN** se dibuja azul, en el tono que el tema activo define para azul

#### Scenario: Cambiar de tema

- **WHEN** la aplicación pasa de tema claro a tema oscuro con iconos en pantalla
- **THEN** todos los iconos toman el tono del tema nuevo, en el acto

#### Scenario: Sin pincel

- **WHEN** no se le da pincel a un icono
- **THEN** usa el color de texto del tema

### Requirement: El icono se dibuja nítido en cualquier resolución

El icono SHALL dibujarse como vector a la resolución real de la pantalla. NO SHALL haber mapas de
bits intermedios, ni caché por tamaño, ni recálculo al mover la ventana entre pantallas de distinta
resolución.

#### Scenario: Pantalla al 100 %

- **WHEN** se dibuja un icono de 16 puntos en una pantalla al 100 %
- **THEN** se ve nítido

#### Scenario: Pantalla al 150 %

- **WHEN** se dibuja el mismo icono en una pantalla al 150 %
- **THEN** se ve nítido, y el trazo no se empasta

#### Scenario: Mover la ventana entre pantallas

- **WHEN** la ventana pasa de una pantalla al 100 % a otra al 200 %
- **THEN** los iconos se redibujan nítidos sin que la aplicación haga nada

### Requirement: Las entradas de menú usan el mismo control

Las entradas de menú con icono SHALL dibujarlo con `IconoVectorial` y una clave del catálogo, y NO
SHALL declarar una geometría propia.

#### Scenario: Entrada de menú con icono

- **WHEN** se arma una entrada de menú con la clave de iniciar y un pincel
- **THEN** dibuja ese icono del catálogo, del tamaño de los iconos de menú

#### Scenario: Entrada de menú sin icono

- **WHEN** se arma una entrada de menú sin clave
- **THEN** no dibuja icono, y la entrada se alinea igual que las demás
