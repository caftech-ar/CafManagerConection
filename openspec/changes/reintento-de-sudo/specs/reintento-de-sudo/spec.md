## ADDED Requirements

### Requirement: La contraseña de sudo se pide una sola vez por sesión

El sistema SHALL pedir la contraseña de `sudo` como mucho una vez por sesión por su cuenta, aunque
varios paneles la necesiten, porque repetir una contraseña equivocada bloquea la cuenta.

#### Scenario: Dos paneles la necesitan

- **WHEN** dos paneles escalan a la vez y ninguno tiene la contraseña
- **THEN** se abre un solo pedido

#### Scenario: Ya se pidió y se canceló

- **WHEN** el pedido se canceló y otro panel vuelve a necesitar `sudo`
- **THEN** no se abre un pedido nuevo por su cuenta

### Requirement: El usuario puede habilitar otro intento

La sesión SSH SHALL ofrecer una acción para volver a pedir la contraseña de `sudo`. Después de usarla,
el siguiente panel que necesite escalar SHALL volver a abrir el pedido.

#### Scenario: Recuperarse de una cancelación

- **WHEN** se canceló el pedido y después se usa la acción de volver a pedirla
- **THEN** el próximo panel que necesite `sudo` abre el pedido de nuevo

#### Scenario: No hace falta reconectar

- **WHEN** se usa esa acción
- **THEN** la sesión sigue abierta: no se corta ni se vuelve a conectar

#### Scenario: Lo que hubiera guardado se descarta

- **WHEN** había una contraseña guardada en la sesión y se pide volver a preguntarla
- **THEN** la guardada se descarta antes de pedir la nueva
