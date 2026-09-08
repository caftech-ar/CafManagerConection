## ADDED Requirements

### Requirement: Los túneles marcados para arrancar solos se levantan al abrir la sesión

Al quedar conectada una sesión SSH, el sistema SHALL levantar todos los túneles de esa conexión que
tengan `AutoStart` en verdadero, y ninguno de los que no lo tengan.

#### Scenario: Sólo se levantan los marcados

- **WHEN** la conexión define tres túneles y dos tienen `AutoStart` en verdadero
- **THEN** se levantan esos dos y el tercero queda sin levantar

#### Scenario: Sin túneles automáticos no se hace nada

- **WHEN** ningún túnel de la conexión tiene `AutoStart` en verdadero
- **THEN** no se abre ningún reenvío y la sesión queda lista igual

### Requirement: Un túnel que falla no impide levantar los demás

El sistema SHALL intentar levantar cada túnel automático aunque uno anterior haya fallado, y SHALL
devolver el motivo de cada fallo junto con el nombre del túnel que lo sufrió.

#### Scenario: El primero falla y los demás se levantan

- **WHEN** se levantan tres túneles automáticos y el primero falla porque su puerto local está tomado
- **THEN** el segundo y el tercero quedan activos
- **AND** se informa un error, que nombra al primero

#### Scenario: Fallan varios

- **WHEN** fallan dos de los tres túneles automáticos
- **THEN** el tercero queda activo
- **AND** se informan los dos errores, cada uno con el nombre de su túnel

#### Scenario: Ninguno falla

- **WHEN** los tres túneles automáticos se levantan sin problema
- **THEN** no se informa ningún error

### Requirement: El usuario se entera de los túneles que no se levantaron

Cuando algún túnel automático falla, el sistema SHALL decírselo al usuario en la barra de estado de
la sesión, y NO SHALL limitarse a anotarlo en el registro técnico.

#### Scenario: Se avisa en pantalla

- **WHEN** un túnel automático no se pudo levantar
- **THEN** la sesión informa qué túnel fue y por qué
