## ADDED Requirements

### Requirement: Registro continuo de la sesión SSH

Con la bitácora activa para una conexión SSH, el sistema SHALL escribir a un archivo, a medida que
llega, todo lo que pasa por el terminal de esa sesión, en texto plano y sin los códigos de control del
terminal.

#### Scenario: Lo que se va del historial queda registrado

- **WHEN** una sesión produce más líneas que las que retiene el historial de desplazamiento
- **THEN** la bitácora conserva también las líneas que el terminal ya descartó

#### Scenario: Sin códigos de control

- **WHEN** el servidor manda secuencias de color y de posición
- **THEN** la bitácora guarda el texto resultante, sin esas secuencias

#### Scenario: Programa de pantalla completa

- **WHEN** se usa un programa que toma la pantalla alternativa, como un editor o un visor
- **THEN** lo que se dibuja ahí no se registra, y al salir la bitácora sigue con lo que pasa en la
  línea de comandos

#### Scenario: Cierre de la sesión

- **WHEN** la sesión se cierra
- **THEN** la bitácora se completa con el contenido que quedaba en la pantalla y el archivo se cierra

#### Scenario: Sesión que no es SSH

- **WHEN** la sesión es RDP
- **THEN** no se escribe ninguna bitácora

### Requirement: Un archivo por sesión en la carpeta configurada

El sistema SHALL escribir un archivo por sesión en la carpeta configurada, con un nombre que empiece
por el host, siga por el nombre de la conexión —los dos saneados— y termine con el momento en que la
sesión empezó, de modo que las sesiones contra un mismo equipo queden juntas al ordenar por nombre.

#### Scenario: Varias conexiones al mismo equipo

- **WHEN** dos conexiones distintas apuntan al mismo host
- **THEN** sus bitácoras empiezan con el mismo host y quedan contiguas al ordenar

#### Scenario: Dos sesiones al mismo servidor

- **WHEN** se abren dos sesiones a la misma conexión en momentos distintos
- **THEN** cada una escribe su propio archivo

#### Scenario: Reconexión

- **WHEN** una sesión se reconecta
- **THEN** la sesión nueva escribe un archivo nuevo

#### Scenario: Nombre de conexión con caracteres no válidos

- **WHEN** la conexión se llama con caracteres que no se admiten en un nombre de archivo
- **THEN** el nombre se sanea y el archivo se escribe igual

### Requirement: Lo registrado sobrevive a una caída

El sistema SHALL volcar a disco lo registrado de forma periódica durante la sesión, sin esperar a que
la sesión se cierre.

#### Scenario: La aplicación se cierra sin cerrar la sesión

- **WHEN** la aplicación termina de forma abrupta con una sesión activa
- **THEN** la bitácora conserva lo registrado hasta el último volcado, y se pierde a lo sumo lo que
  seguía en la pantalla sin haber pasado al historial

### Requirement: Un fallo de escritura no corta la sesión

Si la escritura de la bitácora falla, el sistema SHALL dar de baja la bitácora de esa sesión, SHALL
dejar el motivo en el registro técnico y SHALL mantener la sesión funcionando.

#### Scenario: No se puede escribir

- **WHEN** la carpeta de bitácoras deja de estar disponible durante una sesión
- **THEN** la sesión sigue conectada y operativa, y el fallo queda registrado

### Requirement: Activación global y por conexión

Preferencias SHALL ofrecer la activación global de la bitácora, la carpeta donde se guarda y los días
de retención. Una conexión SSH SHALL poder encender o apagar la bitácora por su cuenta; mientras no lo
haga, SHALL seguir el ajuste global. La bitácora SHALL estar activa por omisión.

#### Scenario: Instalación nueva

- **WHEN** no se configuró nada
- **THEN** se escribe bitácora de las sesiones SSH, en la carpeta por omisión

#### Scenario: Se activa sin elegir carpeta

- **WHEN** se activa la bitácora sin indicar una carpeta
- **THEN** se usa una subcarpeta junto a los datos de la aplicación, y la bitácora funciona

#### Scenario: La conexión apaga lo global

- **WHEN** la bitácora está activa globalmente y una conexión la apaga
- **THEN** esa conexión no deja bitácora y las demás sí

#### Scenario: La conexión enciende sobre lo global apagado

- **WHEN** la bitácora está apagada globalmente y una conexión la enciende
- **THEN** sólo esa conexión deja bitácora

### Requirement: Purga por antigüedad

Al arrancar la aplicación, el sistema SHALL borrar de la carpeta de bitácoras los archivos más viejos
que los días de retención configurados.

#### Scenario: Retención de treinta días

- **WHEN** la retención es de treinta días y hay bitácoras de hace cuarenta
- **THEN** ésas se borran al arrancar y las de menos de treinta días quedan

#### Scenario: Sin retención configurada

- **WHEN** no hay días de retención configurados
- **THEN** no se borra nada

#### Scenario: La carpeta tiene archivos que no son bitácoras

- **WHEN** en la carpeta hay archivos de texto viejos que no escribió la aplicación
- **THEN** no se borran: sólo se purgan los que tienen el nombre y el sello de tiempo que genera la
  aplicación

#### Scenario: La bitácora está apagada

- **WHEN** la bitácora no está activa
- **THEN** no se purga nada al arrancar

### Requirement: Las bitácoras quedan fuera de las copias de respaldo

El sistema SHALL NOT incluir la carpeta de bitácoras en las copias de respaldo que hace la aplicación.

#### Scenario: Copia de respaldo con bitácoras presentes

- **WHEN** se hace una copia de respaldo y hay bitácoras escritas
- **THEN** la copia no las incluye

### Requirement: Se avisa una sola vez lo que estrena la versión

El sistema SHALL avisar, cuando una instalación existente arranque por primera vez con la versión que
estrena la franja de métricas y la bitácora, qué se sumó, que viene activo y dónde se apaga —global o
por conexión—, y SHALL advertir que la bitácora guarda también lo que se tipea. El sistema SHALL NOT
repetir ese aviso en arranques posteriores de la misma versión.

#### Scenario: Primer arranque de la versión nueva

- **WHEN** una instalación que ya se venía usando arranca con la versión nueva y la base se actualiza
- **THEN** se muestra el aviso con lo que se sumó y cómo apagarlo

#### Scenario: Arranques siguientes

- **WHEN** esa misma instalación vuelve a arrancar con la misma versión
- **THEN** no se muestra ningún aviso

#### Scenario: Instalación nueva

- **WHEN** la base se crea desde cero, sin actualización
- **THEN** no se muestra el aviso: no hay novedad que contar
