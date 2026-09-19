## Why

El estado de un servidor sólo se ve abriendo el panel de Estado, que ocupa una columna entera al lado
del terminal. Para lo que uno mira todo el tiempo —cuánta CPU, cuánta memoria, cuánto disco queda— eso
es mucho gesto y mucho espacio.

El motor de lectura ya existe (`MetricsCollector`, `ServerSnapshot`), pero está atado al panel: se
construye cuando el panel se abre y se detiene cuando se cierra.

## What Changes

- Las sesiones SSH suman una **franja al pie del terminal** con las métricas del servidor en vivo: CPU,
  memoria, disco, red, carga y tiempo encendido.
- Se refresca cada **5 segundos**, configurable.
- Viene **activa**, y se apaga globalmente desde Preferencias o por conexión desde su editor.
- El muestreo de la franja usa un **comando liviano propio**, no el de 26 tramos del panel de Estado.
- Los valores se colorean según su tramo de uso, como presentación. **No hay alarmas, ni avisos, ni
  notificaciones**: la franja muestra, no interrumpe.

## Capabilities

### New Capabilities

- `barra-de-metricas-ssh`: qué muestra la franja de métricas de una sesión SSH, cada cuánto se
  refresca, y cómo se activa global y por conexión.

### Modified Capabilities

Ninguna: `openspec/specs/` está vacío.

## Impact

- `CafManagerConection.Monitoring`: colector liviano nuevo, con su comando y su parseo, al lado del
  `MetricsCollector` actual, que no cambia.
- `CafManagerConection.App`: `SessionView.xaml` suma la franja al `DockPanel` que ya existe;
  `SessionView` pasa a ser dueña del muestreo; `PreferenciasWindow` suma la activación y el intervalo;
  `ConnectionEditorWindow` el interruptor de la conexión.
- `CafManagerConection.Domain`: `SettingKeys` suma las claves globales; el ajuste por conexión viaja
  como campo reservado `cmc:`, sin columna ni migración.
- Sustituye al cambio `salud-del-servidor-en-sesion`, que quedaba sin implementar.
