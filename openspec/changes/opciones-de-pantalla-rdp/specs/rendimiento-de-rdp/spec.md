## ADDED Requirements

### Requirement: Banderas de rendimiento de la sesión RDP

El sistema SHALL permitir elegir, por conexión, qué elementos visuales del escritorio remoto se
desactivan para aliviar un enlace lento: fondo de escritorio, temas visuales, animaciones de menús,
suavizado de fuentes, contenido de ventana al arrastrar y composición del escritorio. La elección
SHALL viajar al control como la propiedad `PerformanceFlags` (máscara de bits) del ámbito Avanzados.

El ajuste SHALL ser heredable por carpeta con la misma semántica de tres estados que los demás
ajustes RDP: sin valor propio, hereda del ancestro; sin ancestro que lo defina, el control usa su
valor por omisión.

#### Scenario: El usuario apaga fondo y animaciones para un enlace lento

- **WHEN** una conexión tiene marcadas «sin fondo» y «sin animaciones» y el resto sin marcar
- **THEN** `PlanDeSesionRdp` incluye un ajuste `PerformanceFlags` en el ámbito Avanzados con esos dos
  bits encendidos y los demás apagados

#### Scenario: La conexión no define rendimiento y lo hereda de la carpeta

- **WHEN** la conexión no fija banderas de rendimiento y su carpeta ancestro fija «sin fondo»
- **THEN** la sesión se conecta con «sin fondo» resuelto desde la carpeta

#### Scenario: El control no acepta la propiedad

- **WHEN** el control instalado no expone `PerformanceFlags`
- **THEN** la conexión igual se establece y `PerformanceFlags` queda registrada en
  `PropiedadesNoAceptadas`

### Requirement: Tipo de conexión de red

El sistema SHALL permitir declarar el tipo de red del enlace (por ejemplo LAN, banda ancha, satélite)
para que el control ajuste su experiencia de usuario acorde. La elección SHALL viajar como
`NetworkConnectionType` del ámbito Avanzados y SHALL ser heredable por carpeta.

#### Scenario: Se declara un enlace de banda ancha

- **WHEN** la conexión declara un tipo de red de banda ancha
- **THEN** `PlanDeSesionRdp` incluye `NetworkConnectionType` con el valor correspondiente en el
  ámbito Avanzados

#### Scenario: No se declara tipo de red

- **WHEN** ni la conexión ni sus ancestros declaran tipo de red
- **THEN** `PlanDeSesionRdp` no incluye `NetworkConnectionType` y el control usa su valor por omisión
