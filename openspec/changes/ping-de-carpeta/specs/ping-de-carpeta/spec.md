## ADDED Requirements

### Requirement: Sondeo desde el menú del árbol

El menú contextual de una carpeta SHALL ofrecer «Hacer ping», que abre una ventana con una fila por
cada conexión del subárbol de esa carpeta, incluidas las de las subcarpetas. El menú contextual de una
conexión SHALL ofrecer lo mismo para esa sola conexión.

#### Scenario: Carpeta con subcarpetas

- **WHEN** se hace ping sobre una carpeta con tres conexiones propias y una subcarpeta con dos
- **THEN** la ventana lista las cinco conexiones

#### Scenario: Una conexión suelta

- **WHEN** se hace ping sobre una conexión desde su menú contextual
- **THEN** la ventana se abre con esa única fila

#### Scenario: Carpeta sin conexiones

- **WHEN** se hace ping sobre una carpeta cuyo subárbol no tiene conexiones
- **THEN** se informa que no hay nada que sondear y no se abre la ventana

#### Scenario: Conexiones Web en el subárbol

- **WHEN** el subárbol tiene conexiones Web
- **THEN** se listan con el estado «no se sondea» y el motivo, y no se les hace ninguna prueba

#### Scenario: Carpeta sólo con conexiones Web

- **WHEN** el subárbol no tiene más que conexiones Web
- **THEN** se informa que no hay nada que sondear y no se abre la ventana

### Requirement: Dos pruebas por host, combinadas en un estado

Por cada conexión el sistema SHALL lanzar en paralelo una prueba ICMP contra su host y un intento de
conexión TCP contra su puerto efectivo, y SHALL combinar ambos resultados en un único estado.

#### Scenario: Responde las dos pruebas

- **WHEN** el host responde el ICMP y el puerto acepta la conexión
- **THEN** el estado es «vivo»

#### Scenario: No responde ICMP pero el puerto abre

- **WHEN** el ICMP no obtiene respuesta y el puerto acepta la conexión
- **THEN** el estado es «vivo», y el detalle indica que no responde ping

#### Scenario: Responde ICMP pero el puerto no abre

- **WHEN** el host responde el ICMP y el puerto rechaza o no responde
- **THEN** el estado es «sin servicio»

#### Scenario: No responde ninguna

- **WHEN** ni el ICMP ni el puerto obtienen respuesta
- **THEN** el estado es «sin respuesta»

#### Scenario: El nombre no resuelve

- **WHEN** el host no se puede resolver a una dirección
- **THEN** el estado es «no resuelve el nombre» y no se cuenta como caída

#### Scenario: La prueba ICMP no es concluyente

- **WHEN** la prueba ICMP falla por falta de permisos en lugar de por falta de respuesta
- **THEN** el estado lo decide el sondeo del puerto, y el detalle aclara que el ICMP no se pudo probar

### Requirement: El detalle de cada prueba queda a la vista

La fila SHALL mostrar, en su ayuda emergente, el resultado de cada prueba por separado y el puerto
sondeado, para que el estado combinado se pueda contrastar.

#### Scenario: Ayuda emergente de una fila sin servicio

- **WHEN** se apunta una fila en estado «sin servicio» de una conexión SSH al puerto 22
- **THEN** la ayuda indica que el ICMP respondió y que el puerto 22 no aceptó la conexión

### Requirement: Foto del momento, sin refresco automático

La ventana SHALL sondear una vez al abrirse y SHALL ofrecer un botón para repetir el sondeo completo.
El sistema SHALL NOT repetir el sondeo por su cuenta.

#### Scenario: Repetir

- **WHEN** se pulsa «Repetir»
- **THEN** todas las filas vuelven al estado «sondeando» y se sondean de nuevo

### Requirement: Sondeo en paralelo acotado y cancelable

El sistema SHALL sondear los hosts en paralelo con un límite de 16 sondeos simultáneos, SHALL ir
mostrando cada resultado a medida que llega, y SHALL cancelar los sondeos pendientes al cerrarse la
ventana.

#### Scenario: Carpeta grande

- **WHEN** se sondea una carpeta con 40 hosts que no responden, con tiempo de espera de un segundo
- **THEN** el sondeo completo termina en el orden de los tres segundos, no de los cuarenta

#### Scenario: Resultados progresivos

- **WHEN** el sondeo está en curso
- **THEN** las filas ya resueltas muestran su estado y las pendientes muestran que están sondeándose

#### Scenario: Cerrar durante el sondeo

- **WHEN** se cierra la ventana con sondeos en vuelo
- **THEN** los pendientes se cancelan

### Requirement: Resumen del sondeo

La ventana SHALL mostrar el total de conexiones sondeadas, cuántas quedaron en cada estado y cuánto
tardó el sondeo.

#### Scenario: Fin del sondeo

- **WHEN** termina un sondeo de catorce conexiones con once vivas, una sin servicio, una sin respuesta
  y una sin resolución de nombre
- **THEN** el resumen refleja ese recuento y el tiempo total
