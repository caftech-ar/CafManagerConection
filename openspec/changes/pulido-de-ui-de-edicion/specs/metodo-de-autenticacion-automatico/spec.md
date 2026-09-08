## ADDED Requirements

### Requirement: La clave privada fija el método en «Clave privada»

El sistema SHALL poner el método de autenticación en «Clave privada» cuando aparece una ruta de clave
privada SSH (escrita, elegida con el examinador o pegada) y el método estaba en «Automático» o en
«Contraseña». No SHALL cambiar nada si el usuario ya había elegido «Clave privada».

#### Scenario: Se pega una clave con el método en automático

- **WHEN** el método está en «Automático» y el usuario pega o elige una clave privada
- **THEN** el método pasa a «Clave privada»

#### Scenario: Se define una clave con el método en contraseña

- **WHEN** el método está en «Contraseña» y aparece una ruta de clave privada
- **THEN** el método pasa a «Clave privada»

#### Scenario: Se borra la ruta de la clave

- **WHEN** el usuario borra la ruta de la clave privada
- **THEN** el método no se fuerza: queda donde estaba
