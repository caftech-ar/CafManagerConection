# Qué le cuesta al servidor el panel de estado

**Criterio a cumplir (SC-018):** menos del 1 % de una CPU con muestreo cada 5 segundos.

**Resultado:** entre **0,05 % y 0,07 %** de una CPU. Se cumple con dos órdenes de magnitud de
margen.

## Qué se midió

El panel de estado lee todo de una sola vez, con un único comando encadenado
(`MetricsCollector.Comando`): `/proc/stat`, `/proc/meminfo`, `/proc/loadavg`, `/proc/uptime`,
`/proc/net/dev`, `df`, `hostname`, `/etc/os-release`, `uname`, `date`, `who`, la cantidad de
procesos y los servicios fallados de systemd.

Se corrió ese comando exacto en bucle dentro de un Linux y se leyó el tiempo de CPU que
consumió, con el builtin `times` del shell —que reporta el tiempo de usuario y de sistema del
propio shell y de todos sus hijos—.

Como el costo de recorrer `/proc` depende de cuántos procesos haya, se midió con tres cargas
distintas, llenando la máquina de procesos de relleno.

## Números

| Procesos en el servidor | CPU por lectura | Con muestreo cada 5 s |
|---|---|---|
| 17 | 2,55 ms | 0,051 % de una CPU |
| 217 | 2,80 ms | 0,056 % de una CPU |
| 817 | 3,65 ms | 0,073 % de una CPU |

Escala suave: multiplicar por 48 la cantidad de procesos encarece la lectura un 43 %. Aun
extrapolando a un servidor absurdamente cargado, el consumo se mantiene muy por debajo del 1 %.

### Lo que viaja por la red

**1,66 KB por lectura**, y —esto es lo importante— **no crece con la cantidad de procesos**: con
17 procesos son 1659 bytes y con 817 son 1660. La diferencia de un byte es el número de procesos,
que tiene un dígito más.

Es consecuencia de una decisión de diseño: el comando manda `ls -d /proc/[0-9]* | wc -l`, o sea
el **conteo**, no la lista. Si mandara la lista, un servidor con 800 procesos escupiría varios
kilobytes en cada muestreo.

Con muestreo cada 5 segundos eso da unos **330 bytes por segundo**. Sobre un enlace 3G de una
estación remota es despreciable, y es lo que hace que el panel se pueda dejar abierto ahí sin
molestar a nadie.

## Qué no cubre esta medición

Se dice explícitamente para que nadie le atribuya más de lo que mide:

- **Se midió en un contenedor Alpine sobre la estación de trabajo, no en un servidor de
  producción.** Los comandos son idénticos y el costo lo domina la lectura de `/proc`, que no
  depende de la distribución. Aun así, el número exacto en un servidor concreto puede variar.
- **No incluye lo que le cuesta a `sshd` cifrar la respuesta.** Son 1,66 KB por muestreo: al lado
  de lo que ya cuesta mantener el canal abierto, no mueve la aguja.
- **No incluye el costo de los paneles de Docker, nginx ni supervisord.** Esos no se muestrean
  por reloj: se consultan al abrir el panel y con el botón de refrescar (FR-107), justamente
  porque son caros. `docker stats` solo tarda unos 2,6 segundos, porque Docker necesita dos
  muestras del cgroup de cada contenedor para calcular el porcentaje de CPU.

## Cómo repetirla

```powershell
task sshd:up
```

Después, dentro del contenedor, correr el comando de `MetricsCollector.Comando` en un bucle y
leer `times`. Para medir con carga, llenar la máquina de procesos primero:

```sh
i=0; while [ $i -lt 600 ]; do sleep 300 & i=$((i+1)); done
```
