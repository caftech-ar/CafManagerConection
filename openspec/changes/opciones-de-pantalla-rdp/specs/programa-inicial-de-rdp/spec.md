## ADDED Requirements

### Requirement: Programa inicial en lugar del escritorio

El sistema SHALL permitir declarar, por conexión, un programa que el servidor abre al iniciar la
sesión en lugar del escritorio completo, con su directorio de trabajo opcional. Viajan como
`StartProgram` y `WorkDir` del ámbito Asegurados. Vacío el programa inicial, la sesión abre el
escritorio como hasta ahora.

#### Scenario: Se declara un programa inicial con directorio de trabajo

- **WHEN** la conexión declara un programa inicial y su directorio de trabajo
- **THEN** `PlanDeSesionRdp` incluye `StartProgram` y `WorkDir` en el ámbito Asegurados y el servidor
  abre ese programa al conectar

#### Scenario: No se declara programa inicial

- **WHEN** la conexión no declara programa inicial
- **THEN** `PlanDeSesionRdp` no incluye `StartProgram` y la sesión abre el escritorio completo

### Requirement: RemoteApp cuando el servidor lo publica

El sistema SHALL permitir marcar que el programa inicial se abra como RemoteApp (ventana suelta, sin
escritorio) encendiendo `RemoteProgramMode` y configurando el objeto `RemoteProgram2` del control.
RemoteApp SHALL depender de que un administrador lo haya publicado en el servidor; el sistema NO
puede publicarlo con un usuario sin privilegios.

Cuando el servidor no tiene publicado el RemoteApp pedido, la sesión SHALL fallar con un mensaje que
distinga esa causa —RemoteApp no publicado en el servidor— de un fallo de red o de credenciales.

#### Scenario: RemoteApp publicado en el servidor

- **WHEN** la conexión pide RemoteApp de un programa que el servidor tiene publicado
- **THEN** la sesión abre ese programa como ventana suelta, sin escritorio de fondo

#### Scenario: RemoteApp no publicado en el servidor

- **WHEN** la conexión pide RemoteApp de un programa que el servidor no publicó
- **THEN** la sesión no se establece y se informa que el RemoteApp no está publicado en el servidor,
  no una caída genérica

#### Scenario: Programa inicial sin RemoteApp

- **WHEN** la conexión declara programa inicial pero no marca RemoteApp
- **THEN** `RemoteProgramMode` queda apagado y el programa abre dentro de un escritorio remoto normal
