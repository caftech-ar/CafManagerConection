## Context

El `TabControl` de sesiones (`_sesiones`) usa un template propio: `ScrollViewer` horizontal con un
`StackPanel` como `IsItemsHost`. Es el modo 1. La preferencia es clave/valor en
`application_settings` (`SettingKeys`), así que agregar el modo no toca esquema.

## Goals / Non-Goals

**Goals:**
- Tres disposiciones de la tira elegibles por el usuario, con el actual por omisión.
- Cambiar el modo sin reabrir sesiones.

**Non-Goals:**
- Reordenar o fijar pestañas.
- Tocar el contenido de la sesión o su ciclo de vida.

## Decisions

**El modo cambia el panel de ítems, no el `TabControl`.** Cada modo es una disposición distinta del
host de ítems:
- Modo 1: `ScrollViewer` + `StackPanel` horizontal (el actual).
- Modo 3: `TabPanel` de WPF, que envuelve en filas de forma nativa.
- Modo 2: un `Panel` a medida que mide el ancho, muestra las que entran y deja el resto para un botón
  desplegable a la derecha. Es el único que hay que escribir; los otros dos son paneles existentes.

Alternativa descartada: tres `ControlTemplate` completos del `TabControl` y cambiar `Template` por
modo. Cambiar sólo el `ItemsPanel`/host es menos superficie y no duplica el resto del template.

**La preferencia se lee al construir la ventana y al cambiarla en Preferencias.** Un cambio en
Preferencias reasigna la disposición del host sobre las pestañas ya abiertas, sin tocar sus `Content`.

## Risks / Trade-offs

- [El desplegable de sobra (modo 2) necesita medir el ancho y recalcular al redimensionar] → Se
  recalcula en el `ArrangeOverride`/`SizeChanged` del panel a medida; el costo es de layout, no de
  sesión.
- [El `TabPanel` nativo (modo 3) tiene su propia lógica de filas] → Se acepta su comportamiento por
  omisión en vez de reimplementarlo.
- [Cambiar el host en caliente] → Hay que conservar la selección actual al reasignar la disposición.
