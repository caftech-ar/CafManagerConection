## ADDED Requirements

### Requirement: Usuario y puerto de carpeta por protocolo

Una carpeta SHALL definir el usuario y el puerto heredables por protocolo (RDP, SSH, Web), no uno solo
compartido. El dominio sigue siendo sólo de RDP.

#### Scenario: Distinto usuario por protocolo en la misma carpeta

- **WHEN** una carpeta define usuario RDP «admin» y usuario SSH «root»
- **THEN** los dos valores conviven y cada uno se guarda por separado

### Requirement: La conexión hereda el del protocolo que le corresponde

Al resolver el usuario y el puerto efectivos de una conexión sin valor propio, el sistema SHALL tomar
de la carpeta el usuario y el puerto del protocolo de esa conexión, subiendo por la cadena de carpetas
como con el resto de la herencia. El valor propio de la conexión SHALL ganar sobre el heredado.

#### Scenario: Una conexión SSH hereda el usuario SSH de la carpeta

- **WHEN** una conexión SSH sin usuario propio está en una carpeta con usuario RDP «admin» y usuario
  SSH «root»
- **THEN** su usuario efectivo es «root»

#### Scenario: Una conexión RDP en la misma carpeta hereda el usuario RDP

- **WHEN** una conexión RDP sin usuario propio está en esa misma carpeta
- **THEN** su usuario efectivo es «admin»

#### Scenario: El usuario propio gana

- **WHEN** una conexión SSH tiene usuario propio «deploy» y su carpeta define usuario SSH «root»
- **THEN** su usuario efectivo es «deploy»

#### Scenario: El puerto sube por la cadena por protocolo

- **WHEN** una conexión SSH sin puerto propio cuelga de una carpeta sin puerto SSH, cuyo ancestro
  define puerto SSH 2222
- **THEN** su puerto efectivo es 2222

### Requirement: Las carpetas existentes conservan su comportamiento

El sistema SHALL conservar el usuario y el puerto compartidos de una carpeta como valor de reserva:
cuando un protocolo no define el suyo, la resolución SHALL caer en el compartido. Así ninguna conexión
existente cambia el usuario ni el puerto con el que conecta, sin tocar los datos guardados.

#### Scenario: Una carpeta con sólo el valor compartido

- **WHEN** una carpeta tiene usuario «operador» compartido y ningún usuario por protocolo, y una
  conexión SSH sin usuario propio cuelga de ella
- **THEN** el usuario efectivo de esa conexión es «operador»

#### Scenario: El valor por protocolo gana sobre el compartido

- **WHEN** esa misma carpeta define además usuario SSH «root»
- **THEN** el usuario efectivo de la conexión SSH pasa a ser «root», y el compartido sigue de reserva
  para los protocolos que no lo definan

### Requirement: El editor limita los campos a los protocolos en uso

El editor de carpeta SHALL mostrar el usuario y el puerto sólo de los protocolos que tienen conexiones
bajo esa carpeta (directas o descendientes), y SHALL esconder los que no están en uso. Una carpeta sin
conexiones SHALL mostrar los tres. Esconder un protocolo NO SHALL borrar sus valores guardados.

#### Scenario: Carpeta sólo con conexiones SSH

- **WHEN** se edita una carpeta cuyas conexiones son todas SSH
- **THEN** se muestran el usuario y el puerto SSH, y no los de RDP ni Web

#### Scenario: Carpeta con RDP y SSH

- **WHEN** se edita una carpeta que tiene conexiones RDP y SSH
- **THEN** se muestran los pares de RDP y de SSH, y no el de Web

#### Scenario: Carpeta vacía

- **WHEN** se edita una carpeta sin conexiones
- **THEN** se muestran los tres pares para poder preconfigurarla

#### Scenario: Guardar no borra el protocolo escondido

- **WHEN** una carpeta tiene usuario Web guardado pero ninguna conexión Web, y se guarda la edición
- **THEN** el usuario Web guardado se conserva
