# Tareas

## Dominio

- [x] 1.1 `FolderSettings`: agregar `RdpUserName`/`SshUserName`/`WebUserName` y
  `RdpPort`/`SshPort`/`WebPort`; conservar `UserName`/`Port` como reserva; ajustar `IsEmpty`.

## Herencia

- [x] 2.1 `SettingsResolver`: resolver usuario y puerto tomando el campo del protocolo de la conexión
  y, si no está, el compartido, al subir por la cadena; el propio de la conexión sigue ganando.
  `EffectiveSettings` no cambia forma.
- [x] 2.2 Pruebas de resolución: RDP y SSH en la misma carpeta heredan usuarios distintos; el propio
  gana; el compartido queda de reserva; el puerto sube por la cadena por protocolo.

## Persistencia

- [x] 3.1 `Migration003`: agregar las seis columnas a `folder_settings` (sin baja ni backfill).
- [x] 3.2 `FolderRepository`: leer/escribir las seis columnas.
- [x] 3.3 Prueba de ida y vuelta de los campos por protocolo.

## Interfaz

- [x] 4.1 `FolderSettingsWindow`: usuario y puerto por protocolo, agrupados (usuario RDP junto al
  dominio RDP), con su visualización de herencia.
- [x] 4.2 Mostrar sólo los pares de los protocolos con conexiones bajo la carpeta (directas o
  descendientes); una carpeta vacía muestra los tres. Esconder un protocolo no borra sus valores.

## Cierre

- [x] 5.1 `openspec validate usuario-y-puerto-por-protocolo --strict`.
- [x] 5.2 Suite de los proyectos afectados en verde.
