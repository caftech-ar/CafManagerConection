# Tareas

## Lote 1 — Mecanismo y ajustes sin choques

- [x] 1.1 Agregar `AmbitoDeRdp.Extendida` y `RdpClientHost.TrySetExtended` sobre
  `IMsRdpExtendedSettings`, registrando en `PropiedadesNoAceptadas` lo que el control no acepte.
- [x] 1.2 `RdpSessionRequest` reemplaza `FitToTab` por `ModoDeTamano` y suma rendimiento, tipo de red,
  escala, programa inicial y RemoteApp.
- [x] 1.3 En `PlanDeSesionRdp.Para`, `PerformanceFlags` y `NetworkConnectionType` (Avanzados).
- [x] 1.4 En `PlanDeSesionRdp.Para`, `StartProgram` y `WorkDir` (Asegurados) cuando hay programa.
- [x] 1.5 Persistencia por campos reservados `cmc:` (no columnas por ajuste): helpers en
  `AjustesReservados` y `OpcionesDePantallaRdp` en Domain; `FolderSettings` suma `CustomFields`;
  `Migration002` suma la columna `custom_fields` a `folder_settings` y el inicializador migra en orden.
- [x] 1.6 Controles en `ConnectionEditorWindow` y `FolderSettingsWindow` con su herencia.
- [x] 1.7 Pruebas de `PlanDeSesionRdp`: rendimiento a máscara de bits, tipo de red, programa inicial.

## Lote 2 — Escala y resolución dinámica

- [x] 2.1 Escala en `OpcionesDePantallaRdp` con validación de rango (escritorio 100–200;
  dispositivo 100/140/180) y persistencia por campo reservado.
- [x] 2.2 En `PlanDeSesionRdp.Para`, la escala va al ámbito `Extendida`.
- [x] 2.3 `ModoDeTamanoRdp` (`Ninguno`/`EscalarPixeles`/`RenegociarResolucion`) reemplaza a
  `FitToTab` en el pedido; el modo cae en el viejo `FitToTab` heredado cuando nadie lo define.
- [x] 2.4 `SmartSizing` sale del modo; `Resize` renegocia sólo en `RenegociarResolucion` y traga el
  fallo del servidor viejo sin degradar la sesión.
- [x] 2.5 Editor: el selector de modo reemplaza a la casilla «ajustar resolución», con su herencia.
- [x] 2.6 Pruebas: escala en `Extendida`; `SmartSizing` por modo; herencia y derivación del modo.

## Lote 3 — RemoteApp

- [x] 3.1 `RemoteProgramMode` (Avanzados) y configuración del objeto `RemoteProgram2` cuando se pide
  RemoteApp con programa.
- [x] 3.2 Un corte antes de conectar con RemoteApp se reinterpreta como «no publicado en el
  servidor», distinto de red y credenciales.
- [x] 3.3 Casilla «abrir como RemoteApp» en los dos editores.
- [x] 3.4 Pruebas: `RemoteProgramMode` según la casilla; RemoteApp sin programa no prende nada.

## Cierre

- [x] 4.1 `openspec validate opciones-de-pantalla-rdp --strict`.
- [x] 4.2 Suite de los proyectos afectados en verde (Domain 1030, UseCases 205, Infrastructure 370,
  Rdp 51; 0 fallos).
- [ ] 4.3 Verificar contra el control instalado que escala y RemoteApp no quedan en
  `PropiedadesNoAceptadas` en una máquina de referencia; si quedan, dejarlo anotado en la conexión.
