# Tareas

## Pestañas que no son sesiones

- [x] 1.1 `MainWindow`: abrir y cerrar pestañas de herramienta, con título propio y su botón de cerrar.
- [x] 1.2 Revisar cada lugar que recorre `_sesiones.Items` dando por hecho que son sesiones —cerrar,
  renombrar, contar, reconectar, restaurar— y decidir qué hace con una que no lo es.
- [x] 1.3 Pruebas de que una pestaña de herramienta no rompe el manejo de sesiones.

## Ping en pestaña

- [x] 2.1 `PingWindow` pasa de `Window` a contenido de pestaña, conservando tabla, repetir, resumen y
  cancelación al cerrar.
- [x] 2.2 Abrir una pestaña por sondeo, titulada con la carpeta o la conexión de origen.
- [x] 2.3 Comprobar que dos sondeos conviven y que cambiar de pestaña no pierde los resultados.

## Puertos de una conexión

- [x] 3.1 Reunir los puertos configurados de una conexión: el del protocolo, el de su dirección web si
  la tiene, y los extremos de sus túneles.
- [x] 3.2 `SondeoDeHosts`: sondear un conjunto de puertos de un host, no uno solo.
- [x] 3.3 Fila desplegable que sondea sus puertos recién al abrirse, y los muestra con su resultado.
- [x] 3.4 Pruebas: qué puertos salen de cada tipo de conexión, y que sin desplegar no se sondea de más.

## Visor de bitácoras

- [x] 4.1 Listar la carpeta de bitácoras usando `PoliticaDeBitacora.EsBitacora`, y sacar conexión,
  host y fecha del propio nombre del archivo. El separador entre host y conexión pasó a ser `@`: con
  un guion no había forma de saber dónde termina un host con guiones y empieza una conexión con
  guiones.
- [x] 4.2 Pestaña con el listado —conexión, host, fecha, tamaño—, de la más reciente a la más vieja.
- [x] 4.3 Filtro por conexión, que arranca puesto cuando se abre desde el menú de una conexión.
- [x] 4.4 Lector del contenido por partes, empezando por el final, sin bloquear la interfaz. El campo
  declara `Height="Auto"`: el estilo implícito de `TextBox` fija el alto de un control de una línea y
  el contenido entraba en 32 px.
- [x] 4.9 Guardián sobre los XAML: un campo con barra vertical o que acepta Enter tiene que soltar el
  alto del estilo implícito.
- [x] 4.5 Búsqueda dentro de la bitácora abierta.
- [x] 4.6 Entrada «Ver bitácoras» en el menú contextual de una conexión.
- [x] 4.7 Manejar la carpeta inexistente, la conexión sin registros y el archivo borrado mientras se
  miraba.
- [x] 4.8 Pruebas del parseo del nombre y del listado.

## Cierre

- [x] 5.1 `openspec validate ping-y-bitacoras-en-pestana --strict`.
- [x] 5.2 Comprobar a mano contra un servidor real: sondear una carpeta, desplegar una fila con
  túneles, y leer una bitácora de una sesión larga.
- [x] 5.3 Suite de los proyectos afectados en verde.
