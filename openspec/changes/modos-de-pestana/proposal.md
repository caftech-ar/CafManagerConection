## Why

Con muchas sesiones abiertas, la tira de pestañas es hoy una sola fila que se desplaza en horizontal.
Para quien abre muchas, encontrar una pestaña obliga a rodar. Otras personas prefieren verlas todas de
un vistazo en varias líneas. Es una preferencia, no un valor único: conviene que la elija el usuario.

## What Changes

- Se agrega una opción global a `AppSettings`, editable en `PreferenciasWindow`, con tres modos para
  la tira de pestañas de sesión:
  1. **Lineal con desplazamiento** (el actual, por omisión).
  2. **Lineal con desplegable de sobra**: las pestañas que no entran a lo ancho se ofrecen en un
     desplegable a la derecha.
  3. **Envuelto en varias líneas**: al superar el ancho, las pestañas se apilan en filas.
- El `TabControl` de sesiones (`_sesiones`) elige su disposición según el modo, y cambiarlo se aplica
  sin reabrir las sesiones.

## Capabilities

### New Capabilities

- `modos-de-visualizacion-de-pestanas`: los tres modos de la tira de pestañas, cuál es el
  predeterminado, y cómo se comporta el desplegable de sobra y el envolvido.

### Modified Capabilities

Ninguna: `openspec/specs/` está vacío.

## Impact

- `CafManagerConection.Domain`: `AppSettings` suma el modo de pestañas (enum).
- `CafManagerConection.App`: `PreferenciasWindow` (elección); `MainWindow`/`Estilos.xaml`
  (disposición del `TabControl` de sesiones); el modo 2 pide un panel a medida que mida el ancho y
  mande la sobra a un desplegable.
- Sin cambios de persistencia más allá de lo que `AppSettings` ya serializa.
