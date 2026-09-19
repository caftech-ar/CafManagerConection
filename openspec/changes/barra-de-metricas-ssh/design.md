## Context

`MetricsCollector` lee el servidor con **un comando de 26 tramos** (`MetricsCollector.Tramos`) que
incluye `lscpu`, `sensors`, `ip route`, `systemctl --failed` y `lsblk`, y devuelve un `ServerSnapshot`
completo. Se construye dentro de `CrearPanelAsync`, en la rama del panel de Estado, así que vive y
muere con ese panel; `AjustarRelojDeEstado` además detiene el muestreo cuando el panel no se ve.

`NivelDeUso` ya clasifica un porcentaje en Normal / Advertencia / Crítico (75 y 90) y la carga por
núcleos, y devuelve la etiqueta de texto del tramo.

En la vista, `_marcoSesion` ya es un `DockPanel` con dos franjas ancladas arriba —acciones y búsqueda—
y el terminal ocupando el centro. Las dos usan `SuperficieConsola` (`#2A2A31`), `BordeConsola`
(`#60606B`), esquinas de 7 y una sombra marcada (desenfoque 12, profundidad 3, opacidad 0,55).

El fondo del terminal es `#0C0C0C`, y `ConectarSshAsync` ya le asigna ese mismo color a
`_marcoSesion.Background`. O sea que el marco de la sesión ya es el negro del terminal; lo que se
despega hoy son las franjas, porque su `#2A2A31` es visiblemente más claro.

El terminal es un `WindowsFormsHost`.

## Goals / Non-Goals

**Goals:**
- Ver CPU, memoria y disco sin abrir ningún panel y sin buscarlos.
- Que los números se muevan: refresco del orden de los 5 segundos.
- Poder activarlo para todo o para un equipo puntual.

**Non-Goals:**
- **Alarmas, avisos y notificaciones.** La franja muestra; no interrumpe ni dispara nada.
- Umbrales configurables.
- Historial, gráficos de largo plazo o registro de métricas.
- Cualquier protocolo que no sea SSH.

## Decisions

**Un colector liviano aparte, no el de 26 tramos.** Es la decisión que sostiene todo el resto. Correr
el comando del panel cada 5 segundos, por cada sesión abierta, para pintar una franja, es
desproporcionado: `sensors`, `lscpu` y `lsblk` no cambian nunca y `systemctl --failed` no tiene nada
que ver con esto. La franja usa un comando propio de cinco lecturas —`/proc/stat`, `/proc/meminfo`,
`/proc/loadavg`, `/proc/uptime`, `/proc/net/dev`— que son archivos virtuales y salen sin costo.

**El `df` va aparte, cada doce vueltas.** Es el único caro del conjunto: contra un servidor con montajes
de red puede tardar. El espacio libre no cambia en cinco segundos, así que se lee una vez por minuto y
la franja muestra el último valor mientras tanto.

**La distribución se lee una sola vez, al conectar.** `/etc/os-release` no está en `/proc` y, sobre
todo, no cambia mientras dura la sesión: leerlo cada cinco segundos sería pedirle al servidor un dato
que ya se sabe. Queda una tercera cadencia, de una sola vez, junto al resto de lo que identifica al
servidor y no se mueve.

| Qué | Cadencia |
|-----|----------|
| `/proc/stat`, `meminfo`, `loadavg`, `uptime`, `net/dev` | cada 5 s |
| `df` | cada 60 s |
| `/etc/os-release` | una vez, al conectar |

**La distribución se abrevia.** `SystemInfoParser.ParseDistribution` ya devuelve el `PRETTY_NAME`, que
viene largo: «Ubuntu 22.04.3 LTS», «Oracle Linux Server 8.9», «Debian GNU/Linux 12». En la franja queda
el nombre y la versión —«Ubuntu 22.04», «Oracle 8.9», «Debian 12»—, que identifica igual y ocupa el
ancho de una métrica en lugar de dos. El nombre completo sigue disponible en el panel de Estado.
Alternativa descartada: un ícono por distribución, que se reconoce más rápido pero obliga a dibujar y
mantener uno por cada una y a decidir qué hacer con las no previstas.

**El colector sube del panel a la sesión.** La franja existe sin el panel, así que el muestreo no puede
ser propiedad del panel. La sesión pasa a ser dueña del colector liviano y de su reloj. El panel de
Estado sigue con el suyo, de 26 tramos, sin cambios: son dos lecturas con propósitos distintos y no
vale la pena forzar que compartan una.

**Color sí, alarma no.** Los valores se pintan con el tramo de `NivelDeUso` —un disco al 93 % se ve
rojo— porque eso es lo que hace legible una franja de un vistazo. Es presentación, no aviso: nada se
dispara, nada aparece, nada pide atención. La distinción importa porque es exactamente lo que se pidió
sacar.

**Mismo aspecto y mismo alto que la barra de acciones.** La franja usa el estilo de `_barraSesion` tal
cual: `SuperficieConsola` de fondo, `BordeConsola` de borde, esquinas de 7, sombra de 12/3/0,55 y el
mismo margen lateral, espejado. El alto también se iguala a mano: padding vertical de 2 y contenido de
22 px, que son los del estilo `AccionDeSesion` que usan los botones de arriba. Las dos quedan como superficies de la misma familia sobre el fondo del terminal, una
arriba y otra abajo, y la sesión se lee como una sola pieza. Alternativa probada y descartada: darle el
`#0C0C0C` del terminal para que se fundiera con la consola — se pierde la simetría con la barra de
arriba, que es lo que hace que la ventana se vea armada.

**El efecto es visual; la franja está anclada.** Ocupa su lugar abajo en el `DockPanel`, como las otras
dos. No se superpone al terminal y no tiene por qué: es un `WindowsFormsHost`, o sea un `HwndHost`, y el
contenido hospedado se dibuja siempre por encima de cualquier elemento WPF que lo solape, así que una
franja realmente flotante ahí quedaría tapada y no hay z-order que lo corrija.

**Una sola fila, de unos 26 px.** Todo entra en una línea: la franja le saca al terminal lo mínimo, que
es el único espacio que importa en esa ventana. Alternativa descartada: dos filas, con el contexto
abajo, que duplica el alto para mostrar datos que no se miran seguido.

**Qué entra y en qué orden.** La distribución a la izquierda, porque identifica el servidor y no es una
métrica; después, por cuánto se miran: CPU, memoria, el peor punto de montaje, red, y al final la carga
y el tiempo encendido en texto tenue. `ServerSnapshot` trae bastante más —presión PSI, E/S por
dispositivo, temperaturas, swap— y todo eso se queda en el panel de Estado, que es donde hay lugar para
leerlo.

**Cuando no entra, se cae por prioridad.** Con un panel abierto al lado la sesión se angosta. Se ocultan
primero el tiempo encendido y la distribución —los dos son datos que no cambian y se miran una vez al
conectar—, después la carga, después la red; CPU, memoria y disco se mantienen hasta el final. Alternativa descartada: achicar el texto,
que termina en una franja ilegible; o desplazamiento horizontal, que obliga a interactuar con algo que
existe para mirarse de reojo.

**Un sparkline para CPU y memoria.** Es lo que hace que la franja se lea como estado y no como una
planilla, y sale barato: las muestras ya están en memoria, alcanza con guardar las últimas N y
dibujarlas. Disco y tiempo encendido no lo llevan porque no se mueven a esta escala.

## Risks / Trade-offs

- **Dos colectores contra el mismo servidor.** Con el panel de Estado abierto corren los dos: el liviano
  cada 5 s y el completo a su intervalo. Es el precio de no degradar ninguno de los dos; si pesa, la
  salida es que la franja se alimente del completo mientras el panel esté abierto.
- **Una sesión por servidor multiplica las lecturas.** Diez sesiones abiertas son diez comandos cada
  5 segundos. Son lecturas de `/proc`, baratas, pero la contención real es poder apagarlo por conexión.
- El primer valor de CPU y de red tarda dos muestras en aparecer: los dos se calculan por diferencia.
  La franja arranca mostrándolos sin dato, no en cero.
