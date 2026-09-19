## Why

La contraseña de `sudo` se pide **una sola vez por sesión**, y con razón: `ContrasenaDeSudoDeSesion`
lo dice en su propio comentario —«una contraseña equivocada repetida bloquea la cuenta»—.

El problema es que no hay vuelta atrás. Si ese único pedido se cancela, o la ventana se cierra sin
querer, `YaSePidio` queda marcado y **la sesión entera se queda sin `sudo`**: cada panel que lo
necesite falla con «sudo: a password is required» y no ofrece nada. La única salida es reconectar.

## What Changes

- La barra de acciones de una sesión SSH suma **«Volver a pedir la contraseña de sudo»**, que habilita
  un pedido más.
- El pedido automático no cambia: sigue siendo uno solo por sesión, y sólo el usuario puede habilitar
  otro.

## Capabilities

### New Capabilities

- `reintento-de-sudo`: cuándo se puede volver a pedir la contraseña de `sudo` de una sesión.

### Modified Capabilities

Ninguna: `openspec/specs/` está vacío.

## Impact

- `CafManagerConection.Ssh`: `ContrasenaDeSudoDeSesion` suma la forma de habilitar otro intento.
- `CafManagerConection.App`: `SessionView.Barra` suma la acción.
- Sin cambios de esquema ni de dominio.
