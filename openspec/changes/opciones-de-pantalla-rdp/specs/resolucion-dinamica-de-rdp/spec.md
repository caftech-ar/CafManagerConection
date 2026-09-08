## ADDED Requirements

### Requirement: Dos modos de ajuste de tamaño, excluyentes

El sistema SHALL ofrecer dos conductas separadas para cuando la pestaña de la sesión cambia de
tamaño, hoy fundidas en la única casilla `FitToTab`:

- **Escalar píxeles**: el servidor mantiene su resolución y el cliente estira o encoge la imagen para
  llenar la pestaña. Viaja como `SmartSizing = true`.
- **Renegociar resolución**: el servidor redibuja a la resolución de la pestaña. Viaja como
  `SmartSizing = false` más una llamada a `UpdateSessionDisplaySettings` con las nuevas medidas.

Las dos conductas SHALL ser mutuamente excluyentes: activar una desactiva la otra. El sistema SHALL
ofrecer también «ninguna» (resolución fija, sin escalar ni renegociar).

#### Scenario: El usuario elige escalar píxeles

- **WHEN** la conexión elige el modo «escalar píxeles» y se agranda la pestaña
- **THEN** `SmartSizing` está en `true` y el cliente estira la imagen sin cambiar la resolución del
  servidor

#### Scenario: El usuario elige renegociar resolución

- **WHEN** la conexión elige el modo «renegociar resolución» y se agranda la pestaña
- **THEN** `SmartSizing` está en `false` y `Resize` llama a `UpdateSessionDisplaySettings` con el
  nuevo ancho y alto

#### Scenario: Migración desde el ajuste `FitToTab` existente

- **WHEN** una conexión guardada tenía `FitToTab = true`
- **THEN** al leerla queda en el modo «escalar píxeles», que es la conducta que `FitToTab = true`
  producía hasta ahora

### Requirement: El servidor no soporta renegociación

El sistema SHALL tolerar que el servidor no acepte `UpdateSessionDisplaySettings` (servidores RDP
anteriores a 8.1): la llamada que falla no SHALL cerrar ni degradar la sesión, que sigue con la
resolución con la que se conectó.

#### Scenario: `UpdateSessionDisplaySettings` falla contra un servidor viejo

- **WHEN** el modo es «renegociar resolución» y el servidor rechaza `UpdateSessionDisplaySettings`
- **THEN** la sesión sigue conectada a su resolución actual y el fallo no se informa como caída
