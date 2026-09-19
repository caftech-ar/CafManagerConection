# Tareas

## Diálogo base

- [x] 1.1 `MessageWindow`: que el botón principal sea el predeterminado y Enter lo ejecute, sin perder
  el Escape que ya cancela.
- [x] 1.2 Confirmaciones destructivas: `Dialogos.Confirmar` recibe si la acción destruye, y en ese caso
  el foco arranca en cancelar.
- [x] 1.3 Marcar como destructivas las confirmaciones de borrado que ya existen —conexión, carpeta,
  etiqueta, túnel— y `ConfirmarEnCascada`.

## Diálogos propios

- [x] 2.1 `TextPromptWindow`: Enter acepta lo escrito.
- [x] 2.2 `HostKeyWindow`, `ConflictWindow`, `PastePrivateKeyWindow`,
  `PedidoDeContrasenaDeSudoWindow`, `InstaladorWindow`: revisar que Enter confirme y Escape cancele.
  En las dos de riesgo el predeterminado es el que no rompe: cancelar en la huella de host, conservar
  ambos en el conflicto de archivos.
- [x] 2.3 Los campos de varias líneas se quedan con su Enter: `AcceptsReturn` ya lo resuelve, se
  comprobó que el estilo `CampoMultilinea` y la paleta de comandos lo tienen.

## Cierre

- [x] 3.1 Guardián sobre el código: toda confirmación con verbo de borrado lleva `destructivo: true`.
  Las ventanas WPF no se instancian en la suite —piden una `Application` con los estilos cargados—,
  así que se vigila lo que se olvida. Encontró dos sin marcar al escribirlo.
- [x] 3.2 `openspec validate confirmar-con-enter --strict`.
- [x] 3.3 Suite de los proyectos afectados en verde.
