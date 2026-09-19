# Tareas

## Sondeo

- [x] 1.1 Generalizar `SondaDePuerto` a cualquier host: sumar el host como parámetro conservando el
  tiempo de espera y la cancelación; los llamadores de túneles pasan `127.0.0.1`.
- [x] 1.2 Sonda ICMP nueva sobre `System.Net.NetworkInformation.Ping`, con tiempo de espera y
  cancelación, que distingue «responde», «no responde», «no resuelve» y «no concluyente».
- [x] 1.3 Servicio de sondeo que, por conexión, lanza las dos pruebas en paralelo y combina el
  resultado en el estado único.
- [x] 1.4 Ejecución del lote con concurrencia acotada en 16, informe de cada resultado a medida que
  llega, y cancelación conjunta.
- [x] 1.5 Pruebas de la tabla de combinación de estados, incluidas «no resuelve» y «no concluyente».

## Interfaz

- [x] 2.1 `MainWindow.Acciones`: entrada «Hacer ping» en el menú de carpeta, sobre `nodo.Recorrer()`, y
  en el de una conexión, sobre esa sola.
- [x] 2.2 Ventana con la tabla: conexión, host, puerto y estado con semáforo; ayuda emergente con el
  detalle de cada prueba.
- [x] 2.3 Botón «Repetir» y línea de resumen con recuento por estado y tiempo total.
- [x] 2.4 Cancelar los sondeos al cerrar la ventana.
- [x] 2.5 Aviso cuando la carpeta no tiene conexiones sondeables, sin abrir la ventana.
- [x] 2.6 Listar las conexiones Web con el estado «no se sondea» y su motivo, sin lanzarles pruebas.

## Cierre

- [x] 3.1 `openspec validate ping-de-carpeta --strict`.
- [x] 3.2 Suite de los proyectos afectados en verde.
