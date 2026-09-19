## ADDED Requirements

### Requirement: El ping vive en una pestaña

El sistema SHALL abrir el sondeo como una pestaña más, junto a las sesiones, y SHALL NOT abrirlo en una
ventana aparte.

#### Scenario: Hacer ping sobre una carpeta

- **WHEN** se elige «Hacer ping» sobre una carpeta
- **THEN** se abre una pestaña con el sondeo, titulada con el nombre de la carpeta

#### Scenario: Trabajar mientras se sondea

- **WHEN** hay un sondeo abierto
- **THEN** se puede cambiar a una sesión y volver, y el sondeo conserva sus resultados

#### Scenario: Cerrar la pestaña

- **WHEN** se cierra la pestaña del sondeo con sondeos en vuelo
- **THEN** los pendientes se cancelan, igual que hoy al cerrar la ventana

#### Scenario: Dos sondeos a la vez

- **WHEN** se hace ping sobre dos carpetas distintas
- **THEN** cada uno abre su pestaña y conviven

### Requirement: Cada fila se puede desplegar para ver el resto de sus puertos

Una fila del sondeo SHALL poder desplegarse para mostrar los demás puertos que esa conexión tiene
configurados —el de su dirección web si la tiene, y los extremos de sus túneles—, con el resultado de
sondear cada uno. El sistema SHALL sondear esos puertos recién al desplegarse la fila, y SHALL NOT
sondear puertos que no salgan de la configuración de la conexión.

#### Scenario: Desplegar una conexión con túneles

- **WHEN** se despliega la fila de una conexión SSH que tiene dos túneles definidos
- **THEN** se listan el puerto de la sesión y los de los túneles, cada uno con si acepta la conexión

#### Scenario: Una conexión sin nada más que su puerto

- **WHEN** se despliega la fila de una conexión que no tiene ni web ni túneles
- **THEN** se muestra sólo su puerto, ya sondeado

#### Scenario: No se sondea de más

- **WHEN** una fila no se despliega
- **THEN** de esa conexión sólo se sondea el puerto de su protocolo

#### Scenario: No es un escáner

- **WHEN** se despliega cualquier fila
- **THEN** sólo se prueban puertos que están configurados en esa conexión, sin barrer rangos
