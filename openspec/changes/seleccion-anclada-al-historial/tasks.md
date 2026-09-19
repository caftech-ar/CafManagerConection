# Tareas

## Coordenadas

- [x] 1.1 `TerminalBuffer`: numeración absoluta de líneas —el total archivado desde que arrancó la
  sesión— y ajuste de ese origen cuando el tope recorta el historial.
- [x] 1.2 `TerminalControl`: pasar `_selectionStart`/`_selectionEnd` a línea absoluta más columna, con
  la conversión a fila de pantalla en un solo lugar.

## Lo que lee la selección

- [x] 2.1 Pintado (`EstaSeleccionado`, `FilaEnPantalla`) sobre las coordenadas nuevas.
- [x] 2.2 `SelectedText` recorre el historial, no sólo la pantalla.
- [x] 2.3 Selección por palabra, por línea y `SelectAll`.
- [x] 2.4 `AcompanarSeleccion` extiende sobre la línea absoluta mientras desplaza.
- [x] 2.5 Recortar la selección cuando el historial descarta las líneas que abarcaba.

## Pruebas

- [x] 3.1 Arrastrar más allá del borde selecciona lo recorrido y lo copia entero.
- [x] 3.2 Desplazar con rueda, teclado y barra sigue soltando la selección.
- [x] 3.3 El recorte del historial recorta la selección, y si se la lleva entera no queda nada.
- [x] 3.4 Las pruebas de selección que ya existen siguen en verde.

## Cierre

- [x] 4.1 `openspec validate seleccion-anclada-al-historial --strict`.
- [x] 4.2 Comprobar a mano: seleccionar arrastrando hacia arriba varias pantallas y pegar el resultado.
- [x] 4.3 Suite de los proyectos afectados en verde.
