## Context

`TerminalControl` guarda la selección en `_selectionStart`/`_selectionEnd` como `Point`, donde la Y es
la **fila de la pantalla** (0 a `Rows - 1`). `_scrollOffset` dice cuántas líneas del historial están
corridas hacia arriba, y `FilaEnPantalla(fila)` resuelve, para cada fila visible, si el contenido sale
del historial o del buffer de pantalla.

`AcompanarSeleccion` desplaza mientras se arrastra más allá del borde, y ya funciona: el problema no es
el desplazamiento sino que, al desplazarse, la fila 0 pasa a mostrar otro texto y el ancla sigue
señalando la fila 0.

## Goals / Non-Goals

**Goals:**
- Que arrastrar más allá del borde seleccione lo que se recorre, y que copiarlo lo traiga entero.
- No tocar lo que ya anda: soltar la selección al desplazar con rueda, teclado o barra.

**Non-Goals:**
- Reacomodar el texto al cambiar el ancho (reflow). Una línea larga sigue cortándose en pantalla.
- Selección rectangular a través del historial más allá de lo que ya hace.

## Decisions

**La selección se ancla a la línea absoluta.** El ancla deja de ser «fila 3 de la pantalla» y pasa a
ser «línea N contando desde el principio del historial». Es la única forma de que sobreviva a un
desplazamiento: la línea N sigue siendo la misma después de correr el historial, mientras que la fila 3
no. La conversión a fila de pantalla se hace sólo al pintar, que es donde importa.

**El origen de la numeración es el principio del historial.** `_buffer.Scrollback.Count` cambia cuando
entran líneas nuevas y cuando el historial se recorta por el tope. Numerar desde el principio hace que
la línea N se corra bajo los pies de la selección cada vez que se recorta. Numerar desde el final —«N
líneas hacia atrás desde la última»— tiene el problema simétrico con cada línea nueva. Se elige el
principio y se ajustan las anclas cuando el recorte descarta líneas, que es un evento raro y acotado,
frente a líneas nuevas que llegan todo el tiempo.

**Lo que se sale del historial recorta la selección.** Si la selección abarca líneas que el tope del
historial descarta, la parte que ya no existe se pierde y el resto se conserva. La alternativa —soltar
la selección entera— castiga al usuario por algo que no hizo.

**Se conserva el soltar al desplazar.** Anclar al historial hace *posible* que la selección acompañe al
texto al desplazarse, pero eso no es lo que se pidió: con la rueda, el teclado o la barra, la selección
se suelta, como quedó resuelto antes. Lo que cambia es sólo el arrastre.

## Risks / Trade-offs

- **Toca todo lo que lee la selección.** El pintado, la copia, la selección por palabra y por línea y
  `SelectAll` pasan por el mismo sistema de coordenadas; hay que moverlos juntos o queda inconsistente.
  Las pruebas de selección que ya existen son la red.
- Copiar una selección larga recorre el historial, no sólo la pantalla. Con el tope en 10.000 líneas es
  barato, pero deja de ser una operación acotada a lo visible.
