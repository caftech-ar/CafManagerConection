## Context

Hoy `Connection` tiene un `UserName` y un `Port` (su único protocolo), y `FolderSettings` tiene
`UserName`/`Port`/`Domain` compartidos. Las credenciales de carpeta ya están por protocolo. El
resolver arma `EffectiveSettings.UserName`/`Port` con la cascada conexión → carpetas. El motor de
migración ya es incremental (llega a `user_version = 2`), así que sumar la 3 es directo.

## Goals / Non-Goals

**Goals:**
- Usuario y puerto de carpeta por protocolo, elegidos según el protocolo de cada conexión.
- Migrar sin cambiar el comportamiento de ninguna conexión existente.

**Non-Goals:**
- Tocar el usuario/puerto propios de una conexión (ya son de su único protocolo).
- Cambiar la forma de `EffectiveSettings`: sigue exponiendo un usuario y un puerto resueltos.

## Decisions

**Columnas por protocolo, no campos reservados.** Usuario y puerto son ajustes heredables de primera
clase, tipados y ya con columna; el usuario autorizó tocar el modelo. `folder_settings` reemplaza
`username`/`port` por `rdp_username`/`ssh_username`/`web_username` y `rdp_port`/`ssh_port`/`web_port`.
Alternativa descartada: guardarlos en `custom_fields` como reservados `cmc:` — quedarían destipados y
sin `CHECK` de rango de puerto, para un dato que merece columna.

**La forma de `EffectiveSettings` no cambia.** El resolver recibe el protocolo de la conexión y elige
el campo por protocolo al subir por la cadena; sigue devolviendo un único `UserName`/`Port` resuelto,
así que nada aguas abajo (sesión, editor de conexión) tiene que cambiar.

**Aditivo, con el compartido como fallback.** En lugar de reemplazar y migrar con backfill —que
obligaba a un `DROP COLUMN` y a reescribir seis archivos de prueba que hoy usan el `UserName`/`Port`
compartido—, se **suman** las seis columnas por protocolo y se **conserva** `username`/`port` como
valor de reserva. El resolver toma primero el del protocolo y, si no está, cae en el compartido. Una
carpeta vieja sigue funcionando por el fallback sin tocar sus datos; `Migration003` sólo agrega
columnas (sin baja, sin backfill). Es equivalente en comportamiento al backfill —el compartido se
aplicaba a todos los protocolos, igual que copiarlo a los tres— pero sin operación destructiva. El
editor prefila los campos por protocolo desde el compartido para los protocolos en uso, así el valor
viejo queda a la vista y editable.

## Risks / Trade-offs

- [Perder el usuario/puerto de carpetas existentes al migrar] → El backfill copia a los tres
  protocolos antes de bajar las viejas; hay un escenario que lo fija.
- [`DROP COLUMN` sobre `folder_settings`] → El SQLite que trae Microsoft.Data.Sqlite lo soporta; se
  hace después del backfill, en una transacción, así que o queda todo migrado o nada.
- [El editor de carpeta gana seis campos] → Se agrupan por protocolo, como ya están las credenciales,
  para no alargar la ventana; el usuario RDP va junto al dominio RDP.

## Resolved Questions

- **El editor limita los pares al protocolo cargado.** Muestra el usuario/puerto sólo de los
  protocolos que tienen conexiones bajo esa carpeta (directas o descendientes); esconde los que no
  están en uso, para no llenar la ventana de campos que no aplican. Una carpeta sin conexiones todavía
  muestra los tres, para poder preconfigurarla. Los valores de un protocolo escondido no se tocan al
  guardar: sólo no se muestran.
