## Why

La publicación trae dos instaladores: el liviano, que precisa .NET instalado en la máquina, y el
completo, que lo lleva adentro. `SelectorDeInstalador` ya los distingue y ya lee del registro cuál está
instalado, pero **elige solo** y no lo dice. Quien quiera pasar del liviano al completo —o al revés,
para no bajar decenas de megas— no tiene cómo.

## What Changes

- Al avisar de una versión nueva, el aviso **muestra los dos instaladores disponibles**, con su tamaño
  y qué precisa cada uno, y marca cuál es el tipo instalado.
- El usuario elige cuál bajar. Si no elige, se usa el que coincide con lo instalado, que es lo que pasa
  hoy.
- Si la publicación trae uno solo, se ofrece ése, sin elección que hacer.

## Capabilities

### New Capabilities

- `eleccion-de-instalador-al-actualizar`: cómo se presentan los instaladores de una versión nueva y
  cómo se elige cuál se descarga.

### Modified Capabilities

Ninguna: `openspec/specs/` está vacío.

## Impact

- `CafManagerConection.App`: `SelectorDeInstalador` suma la enumeración de los candidatos con su tipo,
  además de la elección automática que ya hace; el aviso de actualización presenta la elección.
- `CafManagerConection.Infrastructure`: `ActivoDeRelease` hoy es sólo `Nombre` y `UrlDeDescarga`, así
  que suma el tamaño, y `ConsultorDeReleases` lo lee de la publicación.
- **A verificar antes de implementar**: si el instalador NSIS admite instalar un tipo sobre el otro sin
  desinstalar primero. Ver `design.md`.
