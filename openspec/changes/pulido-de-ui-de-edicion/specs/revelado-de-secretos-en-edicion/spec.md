## ADDED Requirements

### Requirement: Indicar que ya hay un secreto guardado

En la edición de una conexión o carpeta, cada campo de contraseña (RDP, SSH, Web) SHALL indicar si ya
existe un secreto guardado para ese protocolo, sin revelarlo. El indicador SHALL basarse en la
bandera de existencia que ya conoce el modelo (`TieneSecreto`), no en leer el vault.

#### Scenario: El protocolo ya tiene contraseña guardada

- **WHEN** se abre la edición de una conexión RDP que tiene una contraseña guardada
- **THEN** el campo de contraseña RDP muestra que ya hay un secreto guardado

#### Scenario: El protocolo no tiene contraseña

- **WHEN** se abre la edición de una conexión sin contraseña guardada
- **THEN** el campo de contraseña no muestra indicador de secreto guardado

### Requirement: Revelar el secreto con el ícono de ojo

Cada campo de contraseña SHALL ofrecer un ícono de ojo que alterna entre ocultar y mostrar. Al
mostrar, si el usuario está tecleando un valor nuevo se ve ese valor; si el campo está vacío y hay un
secreto guardado, se revela el guardado descifrándolo del vault por el mismo camino que «Copiar
contraseña» (`Credentials.ReadAsync`).

#### Scenario: Revelar lo que se está tecleando

- **WHEN** el usuario escribe una contraseña nueva y activa el ojo
- **THEN** ve en claro lo que escribió, y al desactivarlo vuelve a ocultarse

#### Scenario: Revelar el secreto guardado

- **WHEN** el campo está vacío, hay un secreto guardado y el usuario activa el ojo con el vault abierto
- **THEN** se muestra el secreto guardado descifrado

#### Scenario: El vault está bloqueado

- **WHEN** el usuario activa el ojo para revelar un secreto guardado y el vault está cerrado
- **THEN** se ofrece desbloquearlo y, si desbloquea, se revela; si no, el campo queda oculto sin error

### Requirement: El secreto revelado no persiste en pantalla

Revelar un secreto SHALL ser una acción momentánea de la edición: el valor mostrado no SHALL quedar
escrito como cambio a menos que el usuario lo edite, y guardar sin tocarlo SHALL dejar el secreto
guardado tal como estaba.

#### Scenario: Revelar y guardar sin editar

- **WHEN** el usuario revela el secreto guardado y guarda sin cambiarlo
- **THEN** el secreto guardado no cambia
