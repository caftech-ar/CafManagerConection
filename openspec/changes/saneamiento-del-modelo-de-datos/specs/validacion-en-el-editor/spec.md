## ADDED Requirements

### Requirement: Un solo máximo de keep-alive

El máximo de segundos de keep-alive SSH SHALL ser una única constante del dominio (3600) usada por el
editor de conexión, el editor de carpeta, el validador y el texto de error. El servicio de carpetas
SHALL validar antes de escribir.

#### Scenario: Valor sobre el máximo en carpeta

- **WHEN** se guarda una carpeta con keep-alive 7200
- **THEN** el editor muestra «entre 0 y 3600» y no llega a la base

### Requirement: Los errores de validación se muestran en su pestaña

Notas de más de 4000 caracteres SHALL mostrarse como error en la pestaña Avanzado antes de intentar
guardar, sin llegar a la excepción del dominio.

#### Scenario: Notas demasiado largas

- **WHEN** las notas superan 4000 caracteres y el usuario guarda
- **THEN** la pestaña Avanzado muestra el error y la conexión no se guarda

### Requirement: El puerto por omisión no se persiste

Si el puerto tecleado es el puerto por omisión del protocolo, la conexión SHALL guardarse sin puerto
propio.

#### Scenario: SSH con 22

- **WHEN** se guarda una conexión SSH con puerto 22
- **THEN** `port` queda nulo y el puerto efectivo sigue siendo 22

### Requirement: El tooltip de una entrada Web no muestra puerto

El tooltip del árbol SHALL mostrar la URL de una entrada Web y no un host con puerto.

#### Scenario: Entrada Web

- **WHEN** se posa el puntero sobre una entrada Web
- **THEN** el tooltip muestra la URL sin agregar «:443»

### Requirement: La ventana privada exige navegador

La casilla de ventana privada SHALL estar deshabilitada mientras no haya un navegador elegido, y el
editor SHALL avisar si el ejecutable no está en el catálogo que conoce el modificador de incógnito.

#### Scenario: Sin navegador

- **WHEN** el campo de navegador está vacío
- **THEN** la casilla de ventana privada está deshabilitada

#### Scenario: Navegador desconocido

- **WHEN** el navegador es un ejecutable fuera del catálogo y la casilla está tildada
- **THEN** el editor avisa que el modo privado no se va a aplicar
