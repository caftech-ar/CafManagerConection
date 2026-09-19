## Why

Hoy lo único que queda de una sesión SSH es el historial de desplazamiento del terminal y el botón
«Guardar toda la sesión en un archivo», que hay que acordarse de apretar. Las dos cosas están acotadas
al historial en memoria: pasadas las líneas que retiene el terminal, lo anterior ya no existe. Cuando
hace falta saber qué se corrió en un servidor hace dos semanas, no hay dónde mirarlo.

## What Changes

- Se puede activar el **registro continuo de las sesiones SSH**: todo lo que pasa por el terminal se
  escribe a un archivo a medida que llega, sin depender del historial ni de apretar nada.
- Los archivos van a una **carpeta configurable**, uno por sesión, con el nombre de la conexión y el
  momento en que empezó.
- Viene **activa**, y se apaga globalmente desde Preferencias o por conexión, con el mismo criterio de
  herencia que el resto de los ajustes.
- Hay **purga por antigüedad**: los registros más viejos que los días configurados se borran al
  arrancar la aplicación.
- El historial de desplazamiento y el botón de guardar siguen igual: esto se suma, no los reemplaza.

## Capabilities

### New Capabilities

- `bitacora-de-sesion-ssh`: qué se registra de una sesión SSH, dónde se guarda, cómo se activa y
  cuándo se purga.

### Modified Capabilities

Ninguna: `openspec/specs/` está vacío.

## Impact

- `CafManagerConection.App`: enganche en el flujo de bytes de la sesión SSH; escritor de bitácora
  nuevo; `PreferenciasWindow` suma la activación, la carpeta y los días de retención;
  `ConnectionEditorWindow` el interruptor de la conexión.
- `CafManagerConection.Domain`: `SettingKeys` suma las tres claves globales; el ajuste por conexión
  viaja como campo reservado `cmc:`, sin columna ni migración.
- La carpeta de bitácoras queda **excluida de las copias de respaldo** que ya hace la aplicación.
