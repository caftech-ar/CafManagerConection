## ADDED Requirements

### Requirement: Cada herramienta externa declara los protocolos que atiende

El sistema SHALL determinar qué herramientas externas ofrecer según el protocolo de la conexión,
tomando los protocolos que cada herramienta declara, en lugar de limitar la oferta a un único
protocolo.

#### Scenario: Conexión SSH

- **WHEN** se abre el menú contextual de una conexión SSH con PuTTY y `mstsc` instalados
- **THEN** se ofrece abrir en PuTTY y no se ofrece `mstsc`

#### Scenario: Conexión RDP

- **WHEN** se abre el menú contextual de una conexión RDP con PuTTY y `mstsc` instalados
- **THEN** se ofrece abrir en el cliente de Escritorio remoto y no se ofrece PuTTY

#### Scenario: Ninguna herramienta aplica

- **WHEN** ninguna de las herramientas instaladas atiende el protocolo de la conexión
- **THEN** el menú no muestra la sección de herramientas externas ni su separador

### Requirement: Abrir una conexión RDP en el cliente de Windows

El sistema SHALL ofrecer abrir una conexión RDP en `mstsc`, desde el menú contextual del árbol y desde
la barra de acciones de la sesión, pasándole el host y el puerto efectivos de la conexión.

#### Scenario: Abrir desde el árbol

- **WHEN** se elige «Abrir en Escritorio remoto» sobre una conexión RDP al host `srv` puerto 3390
- **THEN** se lanza `mstsc` apuntando a `srv:3390`

#### Scenario: El cliente no está disponible

- **WHEN** `mstsc` no se encuentra en el sistema
- **THEN** la opción no aparece en ningún menú

### Requirement: La contraseña no se le pasa al cliente externo

El sistema SHALL NOT pasarle la contraseña de la conexión a `mstsc`, ni por línea de comandos ni
escribiéndola en un archivo. El sistema SHALL avisar que la herramienta la va a pedir.

#### Scenario: Conexión RDP con contraseña guardada

- **WHEN** se abre en `mstsc` una conexión RDP que tiene contraseña guardada
- **THEN** la contraseña no sale de la aplicación y se avisa que el cliente la va a pedir
