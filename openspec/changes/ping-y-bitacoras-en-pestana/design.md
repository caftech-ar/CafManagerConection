## Context

`MainWindow` tiene un `TabControl` (`_sesiones`) cuyos elementos hoy son siempre sesiones: el cierre,
el menú de pestaña y el recuento las tratan como tales. `PingWindow` es una `Window` que se abre con
`Show()` sobre la principal.

`SondeoDeHosts` sondea **un** puerto por conexión: el `EffectivePort` del protocolo. La conexión tiene
más puertos alrededor —la URL de una entrada web, los extremos de sus túneles— que hoy no se miran.

Las bitácoras se escriben con el nombre `host-conexión-yyyyMMdd-HHmmss.txt` en la carpeta configurada,
que `PoliticaDeBitacora.EsBitacora` sabe reconocer.

## Goals / Non-Goals

**Goals:**
- Dejar el ping y las bitácoras abiertos mientras se trabaja en una sesión.
- Ver de un vistazo qué puertos de lo configurado están abiertos.
- Leer una bitácora sin salir de la aplicación.

**Non-Goals:**
- Escanear puertos que no estén configurados en alguna conexión. No es un escáner de red.
- Monitoreo: el ping sigue siendo una foto con botón de repetir.
- Editar o borrar bitácoras desde el visor; de eso se ocupa la purga.

## Decisions

**Las pestañas dejan de ser sólo sesiones.** Es el cambio de fondo: `MainWindow` maneja pestañas de
sesión en varios lugares —cerrar, renombrar, contar, recorrer para reconectar—. Se introduce la idea de
pestaña de herramienta, que se cierra y se titula como cualquier otra pero que no tiene conexión ni
estado. Sin esto, cada herramienta nueva es otra ventana suelta.

**Sólo se sondean puertos que ya están configurados.** El despliegue muestra los puertos que salen de
la propia conexión: el del protocolo, el de su URL si es web, y los extremos de sus túneles. Barrer un
rango convertiría la herramienta en un escáner de red, que es otra cosa y que en una red corporativa
tiene consecuencias.

**El despliegue se sondea a pedido.** La fila trae su estado principal con la primera pasada; los
puertos de adentro se prueban recién al desplegarla. Sondear todo de entrada multiplica las conexiones
salientes por conexión configurada, para un dato que casi siempre no se mira.

**El host se separa con `@`, no con un guion.** El nombre lo escribe `PoliticaDeBitacora` y el visor lo
lee: con `host-conexión-sello`, un host con guiones y una conexión con guiones no se pueden separar.
Con `host@conexión-sello` el primer `@` es siempre el corte, porque el saneado lo reemplaza en las dos
partes. Ordenar la carpeta por nombre sigue agrupando por equipo, que era el motivo de poner el host
adelante.

**El visor lista archivos, no una base.** Las bitácoras son archivos en una carpeta, y el visor los
lee de ahí: nombre, fecha y tamaño salen del sistema de archivos, y la conexión y el host salen del
propio nombre, que ya los lleva. No hace falta índice ni tabla, y una bitácora copiada a mano a esa
carpeta se lista igual.

**El contenido se lee en trozos.** Una bitácora de una sesión larga puede pesar cientos de megas.
Cargarla entera en memoria para mostrarla congelaría la aplicación, así que el visor muestra el final
—que es lo que casi siempre se busca— y carga hacia atrás a pedido.

## Risks / Trade-offs

- **Las pestañas de herramienta tocan código que hoy asume sesión.** Hay que revisar cada lugar que
  recorre `_sesiones.Items` y decidir qué hace con una pestaña que no es una sesión.
- **El despliegue abre varias conexiones salientes por fila.** Con el sondeo a pedido y un puñado de
  puertos por conexión queda acotado, pero desplegar muchas filas seguidas se acumula.
- El visor deja a la vista contraseñas que la bitácora haya capturado. Es lo que ya está en el archivo;
  el visor no lo empeora, pero lo hace más fácil de encontrar.
