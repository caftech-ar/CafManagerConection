## Why

Los avisos y confirmaciones de la aplicación —confirmar un pegado, aceptar un mensaje, responder una
pregunta— no responden a Enter. Hay que ir al botón con el mouse o tabular hasta él, para algo que
aparece muchas veces por sesión y que en cualquier otra aplicación se cierra con Enter.

## What Changes

- Enter confirma el diálogo: ejecuta la acción del botón principal.
- Escape cancela, como ya hace hoy.
- En las confirmaciones destructivas el foco arranca en el botón que no rompe nada, así que Enter
  cancela en lugar de confirmar.

## Capabilities

### New Capabilities

- `confirmar-con-enter`: cómo responden al teclado los avisos y confirmaciones de la aplicación.

### Modified Capabilities

Ninguna: `openspec/specs/` está vacío.

## Impact

- `CafManagerConection.App`: `MessageWindow` —que es la base de `Dialogos.Confirmar`, `Informar` y
  `Advertir`—, y los diálogos propios que no pasan por ella: `TextPromptWindow`, `HostKeyWindow`,
  `ConflictWindow`, `PastePrivateKeyWindow`, `PedidoDeContrasenaDeSudoWindow`, `InstaladorWindow`.
- Sin cambios de esquema ni de dominio.
