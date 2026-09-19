# Tareas

## Ajustes

- [x] 1.1 `SettingKeys`: claves de franja activa e intervalo de refresco.
- [x] 1.2 `AjustesReservados`: `cmc:barraDeMetricas` con tres estados (ausente hereda, encendido,
  apagado).
- [x] 1.3 Resolución del estado efectivo por sesión y pruebas de los seis casos.

## Colector liviano

- [x] 2.1 Comando de cinco lecturas —`/proc/stat`, `/proc/meminfo`, `/proc/loadavg`, `/proc/uptime`,
  `/proc/net/dev`— con la misma separación por marca que ya usa `MetricsCollector`.
- [x] 2.2 Parseo a un resultado propio y acotado; reusar los parsers existentes donde ya estén.
- [x] 2.3 CPU y red por diferencia entre dos lecturas; sin dato mientras haya una sola.
- [x] 2.4 `df` en su propia vuelta, una vez por minuto, conservando el último valor entre lecturas.
- [x] 2.5 Lectura única al conectar de `/etc/os-release`, reusando `SystemInfoParser.ParseDistribution`,
  fuera del comando periódico.
- [x] 2.6 Abreviar el nombre completo a nombre y versión —«Ubuntu 22.04.3 LTS» a «Ubuntu 22.04»,
  «Oracle Linux Server 8.9» a «Oracle 8.9»— con sus pruebas, incluida una distribución no prevista.
- [x] 2.7 Una lectura fallida no corta el muestreo ni la sesión: se conserva lo último bueno.
- [x] 2.8 Pruebas del parseo, del cálculo por diferencia y de la cadencia del disco.

## Muestreo en la sesión

- [x] 3.1 La sesión es dueña del colector liviano y de su reloj; arranca sólo si la franja está activa
  para esa conexión.
- [x] 3.2 Detener y liberar el colector al terminar la sesión.
- [x] 3.3 Comprobar que el panel de Estado sigue con su propio colector de 26 tramos, sin cambios.

## Franja

- [x] 4.1 `SessionView.xaml`: franja anclada abajo en el `DockPanel` de `_marcoSesion`, con el mismo
  estilo que `_barraSesion` —`SuperficieConsola`, `BordeConsola`, esquinas de 7, sombra de 12/3/0,55 y
  el mismo margen lateral y el mismo alto—. Anclada, no superpuesta: el terminal es un `WindowsFormsHost` y taparía
  cualquier elemento que lo solape.
- [x] 4.2 Una sola fila, de unos 26 px, con la distribución a la izquierda y los indicadores en orden:
  CPU, memoria, peor punto de montaje, red, y carga y tiempo encendido en texto tenue al final.
- [x] 4.3 Color por tramo con `NivelDeUso`, sin ningún aviso asociado.
- [x] 4.4 Sparkline de las últimas muestras para CPU y memoria.
- [x] 4.5 Ocultar por prioridad al angostarse: primero tiempo encendido y distribución, después carga,
  después red.
- [x] 4.6 Estado sin dato mientras no haya lectura, distinto de un cero.

## Interfaz de ajustes

- [x] 5.1 `PreferenciasWindow`: activación global e intervalo, aclarando que aplica sólo a SSH.
- [x] 5.2 `ConnectionEditorWindow`: interruptor de la conexión con su estado heredado a la vista, sólo
  en conexiones SSH.

## Cierre

- [x] 6.1 `openspec validate barra-de-metricas-ssh --strict`.
- [x] 6.2 Ver la franja andando contra un servidor real, con el panel de Estado abierto y cerrado.
- [x] 6.3 Suite de los proyectos afectados en verde.
