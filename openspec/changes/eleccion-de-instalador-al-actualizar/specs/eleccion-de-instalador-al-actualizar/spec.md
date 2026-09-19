## ADDED Requirements

### Requirement: El aviso de versión nueva muestra los instaladores disponibles

Cuando la versión disponible traiga más de un instalador, el aviso SHALL mostrarlos con su tipo
—liviano o completo—, su tamaño y qué precisa cada uno, y SHALL permitir elegir cuál descargar.

#### Scenario: Publicación con los dos instaladores

- **WHEN** la versión disponible trae el instalador liviano y el completo
- **THEN** el aviso muestra los dos con su tamaño, indica que el liviano precisa .NET instalado, y deja
  elegir

#### Scenario: Publicación con un solo instalador

- **WHEN** la versión disponible trae un único instalador
- **THEN** se ofrece ése y no se presenta ninguna elección

#### Scenario: Publicación sin instalador

- **WHEN** la versión disponible no trae ningún instalador reconocible
- **THEN** el aviso lo informa y no ofrece descargar

### Requirement: El tipo instalado queda marcado

El aviso SHALL indicar cuál de los instaladores ofrecidos corresponde al tipo que está instalado en la
máquina, cuando ese dato esté disponible.

#### Scenario: Está instalado el liviano

- **WHEN** la marca del registro dice que el tipo instalado es el liviano
- **THEN** el aviso señala el liviano como el instalado

#### Scenario: No hay marca del tipo instalado

- **WHEN** la marca del registro no existe o no se puede leer
- **THEN** no se señala ninguno como instalado y la elección queda abierta

### Requirement: La elección por omisión no cambia

Si el usuario no elige, el sistema SHALL descargar el instalador que coincide con el tipo instalado y,
si no hay marca o no hay coincidencia, el primero disponible, igual que hasta ahora.

#### Scenario: Se acepta sin elegir

- **WHEN** está instalado el completo y se acepta actualizar sin tocar la elección
- **THEN** se descarga el instalador completo

### Requirement: Cambiar de tipo es una decisión explícita

Cuando el instalador elegido sea de un tipo distinto al instalado, el sistema SHALL advertirlo antes de
descargar.

#### Scenario: Elegir el completo teniendo el liviano

- **WHEN** está instalado el liviano y se elige el completo
- **THEN** se advierte que se está cambiando de tipo de instalación antes de descargar
