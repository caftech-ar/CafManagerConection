## Why

Las credenciales de una carpeta ya están separadas por protocolo (`rdp_secreto`, `ssh_secreto`,
`web_secreto`), pero el usuario y el puerto son uno solo, compartido. En una carpeta que mezcla RDP y
SSH eso no alcanza: el usuario de administración de RDP no es el de SSH, y los puertos por defecto
difieren. Hoy un único usuario/puerto de carpeta se aplica a conexiones de protocolos distintos.

## What Changes

- El usuario y el puerto heredables de una carpeta pasan a ser **por protocolo**: `RdpUserName` /
  `SshUserName` / `WebUserName` y `RdpPort` / `SshPort` / `WebPort`. El dominio ya es sólo de RDP y
  queda igual.
- La resolución de herencia elige, para cada conexión, el usuario y el puerto del protocolo de esa
  conexión. La conexión sigue teniendo su propio usuario y puerto (una conexión es de un solo
  protocolo), y ganan sobre lo heredado como hasta ahora.
- **Cambio de esquema** (autorizado): `folder_settings` **suma** seis columnas por protocolo y
  conserva `username`/`port` como valor de reserva. El resolver prefiere el del protocolo y, si no
  está, cae en el compartido, así ninguna carpeta existente cambia su comportamiento sin tocar datos.

## Capabilities

### New Capabilities

- `usuario-y-puerto-por-protocolo`: cómo se define y se hereda el usuario y el puerto por protocolo en
  las carpetas, y cómo la conexión elige el que corresponde a su protocolo.

### Modified Capabilities

Ninguna: `openspec/specs/` está vacío.

## Impact

- `CafManagerConection.Domain`: `FolderSettings` suma los seis campos por protocolo y conserva
  `UserName`/`Port` como reserva; `Connection` no cambia (ya es de un solo protocolo).
- `CafManagerConection.UseCases`: `SettingsResolver` resuelve usuario y puerto según el protocolo de
  la conexión; `EffectiveSettings` no cambia su forma (sigue exponiendo un usuario y un puerto
  resueltos).
- `CafManagerConection.Infrastructure`: `Migration003` sobre `folder_settings` (seis columnas nuevas,
  sin baja ni backfill); `FolderRepository` y el motor de migración.
- `CafManagerConection.App`: `FolderSettingsWindow` muestra usuario y puerto por protocolo, agrupados
  como ya están las credenciales.
- Alcance: sólo las carpetas. El usuario y el puerto propios de una conexión ya pertenecen a su único
  protocolo y no se tocan.
