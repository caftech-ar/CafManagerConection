## Why

Antes de abrir una sesión no hay forma de saber si el host está en pie. Se descubre conectando: se
espera el tiempo de espera completo y recién ahí falla. En una carpeta con decenas de servidores eso
se repite host por host.

## What Changes

- El menú contextual de una carpeta suma **«Hacer ping»**, que recorre el subárbol completo —como ya
  hace «Abrir todas las conexiones»— y abre una ventana con una fila por conexión. El de una conexión
  suelta ofrece lo mismo para esa sola.
- Cada host se sondea con **dos pruebas en paralelo**: ICMP y un intento de conexión TCP al puerto
  efectivo de la conexión. El resultado combinado se muestra como un único semáforo, y el detalle de
  cada prueba queda en la ayuda emergente.
- Es una **foto del momento**, con botón «Repetir». No hay refresco automático ni monitoreo.
- Los sondeos corren con concurrencia acotada y las filas se pintan a medida que llegan. Cerrar la
  ventana cancela lo que quede en vuelo.
- Las conexiones Web se listan pero no se sondean: su destino real es la URL del detalle, no el host
  del resumen.

## Capabilities

### New Capabilities

- `ping-de-carpeta`: cómo se sondea un subárbol de conexiones y cómo se combina el resultado de las
  dos pruebas en un estado por host.

### Modified Capabilities

Ninguna: `openspec/specs/` está vacío.

## Impact

- `CafManagerConection.App`: `MainWindow.Acciones` suma la entrada al menú de carpeta; ventana nueva
  con la tabla de resultados; servicio de sondeo nuevo.
- `CafManagerConection.App/Services/SondaDePuerto`: generalizar de `127.0.0.1` a cualquier host.
- Sin cambios de esquema ni de dominio: `ConnectionSummary` ya expone `Host` y `EffectivePort`, que es
  todo lo que la ventana necesita.
