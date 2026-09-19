# Tareas

## Tamaño inicial de la sesión

- [x] 0.1 `ConectarSshAsync`: esperar a que el terminal esté medido antes de armar el
  `SshSessionRequest`, para pedir el pty con las columnas y filas reales y no con el 80×24 del
  constructor.
- [x] 0.2 Comprobar contra un servidor con banner largo: el prompt queda arriba, sin filas en blanco
  debajo ni contenido en el historial cuando todo entraba en pantalla.
- [x] 0.3 Comprobar que redimensionar la ventana con la sesión establecida sigue ajustando terminal y
  servidor.
- [x] 0.4 `TerminalBuffer.Resize`: al achicar el alto, conservar las últimas filas en vez de las
  primeras y archivar las que salen por arriba; extraer el recorte de relleno que ya hacía `ScrollUp`.
- [x] 0.5 Pruebas del redimensionado: se conserva el final, el cursor sigue su línea, lo que sale va al
  historial, agrandar no pierde nada y el cursor arriba no descarta lo de encima.
- [x] 0.6 `TerminalBuffer`: separar el ancho guardado del ancho visible, para que angostar la ventana
  no borre lo que queda fuera del borde derecho. Alcanza a `ScrollUp` —el `Array.Copy` usaba `Columns`
  de paso—, `ScrollDown`, `InsertLines`, `DeleteLines`, `InsertChars`, `DeleteChars`, `ClearLine` sin
  columna final y el archivado al historial.
- [x] 0.7 Pruebas del ancho: la línea se ve cortada, vuelve entera al ensanchar, el historial guarda la
  línea completa y borrar alcanza lo que no se ve.

## Selección del terminal

- [x] 1.1 `TerminalControl`: limpiar la selección en `OnMouseWheel`, en `DesplazarPorPulgar` y en las
  acciones de historial del teclado. **No** dentro de `ScrollBy`: `AcompanarSeleccion` también lo
  llama y ahí la selección tiene que sobrevivir.
- [x] 1.2 Pruebas: rueda, teclado y pulgar limpian; el arrastre más allá del borde mantiene y extiende.

## Herramienta externa para RDP

- [x] 2.1 `HerramientaExterna`: sumar `Mstsc` y declarar, por herramienta, los protocolos que atiende.
- [x] 2.2 `LineaDeComando`: línea de `mstsc` con host y puerto, sin usuario ni contraseña.
- [x] 2.3 `BuscadorDeHerramientas`: resolver la ruta de `mstsc`; si no está, la herramienta no se
  ofrece.
- [x] 2.4 `MainWindow.Acciones.AgregarHerramientasExternas`: reemplazar el corte por SSH por el filtro
  según los protocolos declarados; no mostrar separador si no queda ninguna.
- [x] 2.5 Barra de la sesión RDP: sumar el botón con `AgregarAccionDeRdp`, que es donde se arma esa
  barra (`SessionView.xaml.cs`), no en `SessionView.Barra`, que es sólo de SSH.
- [x] 2.6 Reusar `AvisarQueLaHerramientaPideLaContrasena` para `mstsc`.
- [x] 2.7 Pruebas de la línea de comandos y del filtro por protocolo.

## Cierre

- [x] 3.1 `openspec validate arreglos-de-terminal-y-rdp --strict`.
- [x] 3.2 Suite de los proyectos afectados en verde.
