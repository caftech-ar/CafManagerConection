## Why

Al seleccionar con el mouse y arrastrar más allá del borde, el terminal desplaza el historial para
acompañar —eso ya funciona—, pero lo que queda fuera de la pantalla se pierde: la selección se corre
sola y lo copiado sale incompleto.

La causa es que la selección se guarda en **coordenadas de pantalla**. Cuando el historial se desplaza,
la fila 0 pasa a tener otro contenido, y el ancla de la selección se queda apuntando a la fila, no al
texto que el usuario marcó.

## What Changes

- La selección pasa a anclarse a la **línea del historial**, no a la fila de la pantalla. Arrastrar
  más allá del borde selecciona de verdad todo lo que se recorre, y copiarlo trae todo.
- Se conserva lo que ya anda: desplazar con la rueda, el teclado o la barra sigue soltando la
  selección, porque ahí el usuario desplaza en vez de seleccionar.

## Capabilities

### New Capabilities

- `seleccion-anclada-al-historial`: a qué queda atada una selección del terminal y qué se copia
  cuando abarca líneas que ya no están en pantalla.

### Modified Capabilities

Ninguna: `openspec/specs/` está vacío.

## Impact

- `CafManagerConection.Terminal`: `TerminalControl` cambia el sistema de coordenadas de
  `_selectionStart`/`_selectionEnd`, y con él todo lo que lee la selección: el pintado
  (`EstaSeleccionado`), la copia (`SelectedText`), la selección por palabra y por línea, `SelectAll` y
  `AcompanarSeleccion`.
- Sin cambios de esquema, de dominio ni de interfaz.
