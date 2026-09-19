## ADDED Requirements

### Requirement: Franja de métricas al pie de la sesión SSH

Una sesión SSH con la franja activa SHALL mostrar, al pie del terminal, la distribución del servidor y
sus métricas: uso de CPU, uso de memoria, ocupación del punto de montaje más comprometido, tráfico de
red, carga y tiempo encendido, en una sola fila. La franja SHALL usar el mismo tratamiento visual que la
barra de acciones de la sesión, y SHALL NOT superponerse al terminal.

#### Scenario: Sesión SSH con la franja activa

- **WHEN** la sesión conecta y la franja está activa para esa conexión
- **THEN** la franja aparece al pie del terminal con las métricas del servidor

#### Scenario: La franja acompaña a la barra de acciones

- **WHEN** la sesión muestra la barra de acciones arriba y la franja abajo
- **THEN** las dos tienen el mismo fondo, borde, esquinas, sombra y alto

#### Scenario: Sesión que no es SSH

- **WHEN** la sesión es RDP
- **THEN** no se muestra ninguna franja de métricas

#### Scenario: La franja no está activa

- **WHEN** la franja no está activa para esa conexión
- **THEN** no se muestra, y el terminal ocupa ese espacio

### Requirement: La distribución se muestra abreviada y se lee una sola vez

La franja SHALL mostrar el nombre y la versión de la distribución del servidor, abreviados desde el
nombre completo que informa el sistema. El sistema SHALL leer ese dato una sola vez al establecerse la
sesión, y SHALL NOT volver a pedirlo en cada refresco.

#### Scenario: Nombre largo

- **WHEN** el servidor informa «Ubuntu 22.04.3 LTS»
- **THEN** la franja muestra «Ubuntu 22.04»

#### Scenario: Otra distribución

- **WHEN** el servidor informa «Oracle Linux Server 8.9»
- **THEN** la franja muestra «Oracle 8.9»

#### Scenario: El servidor no informa la distribución

- **WHEN** no se puede leer la distribución
- **THEN** la franja no muestra ese dato y el resto de las métricas funciona igual

#### Scenario: No se relee

- **WHEN** la franja lleva varios refrescos
- **THEN** la distribución se leyó una sola vez y no forma parte del comando periódico

### Requirement: Historia reciente de CPU y memoria

La franja SHALL mostrar, junto al valor de CPU y al de memoria, un gráfico de las últimas muestras
tomadas en esa sesión.

#### Scenario: Un pico que ya pasó

- **WHEN** la CPU tuvo un pico hace unas muestras y ahora está baja
- **THEN** el gráfico de CPU lo muestra, aunque el valor actual no lo refleje

#### Scenario: Pocas muestras todavía

- **WHEN** la sesión lleva menos muestras que las que entran en el gráfico
- **THEN** se dibujan las que haya, sin rellenar con ceros

### Requirement: Refresco en vivo

El sistema SHALL refrescar las métricas de la franja cada cinco segundos, y el intervalo SHALL ser
configurable.

#### Scenario: Los valores se mueven

- **WHEN** la franja está activa y el uso del servidor cambia
- **THEN** los valores mostrados se actualizan en el intervalo configurado

#### Scenario: Primeras muestras

- **WHEN** la sesión acaba de conectar y todavía no hay dos lecturas
- **THEN** el uso de CPU y el tráfico de red se muestran sin dato, no en cero, porque se calculan por
  diferencia

#### Scenario: La lectura falla

- **WHEN** una lectura del servidor falla o no llega
- **THEN** la franja conserva los últimos valores y no rompe la sesión

### Requirement: Lectura liviana, separada de la del panel

El muestreo de la franja SHALL usar un comando propio, acotado a lo que la franja muestra, y SHALL NOT
usar el comando completo del panel de Estado. La ocupación de disco SHALL leerse con menos frecuencia
que el resto.

#### Scenario: Con el panel de Estado cerrado

- **WHEN** la franja está activa y el panel de Estado nunca se abrió
- **THEN** la franja se alimenta igual, con su propia lectura

#### Scenario: Frecuencia del disco

- **WHEN** la franja se refresca cada cinco segundos
- **THEN** la ocupación de disco se lee aproximadamente una vez por minuto, y entre lecturas se muestra
  el último valor conocido

### Requirement: Color por tramo de uso, sin avisos

El sistema SHALL colorear cada métrica según su tramo de uso. El sistema SHALL NOT emitir
notificaciones, avisos ni alertas de ningún tipo a partir de estas métricas.

#### Scenario: Un valor en tramo crítico

- **WHEN** el punto de montaje más comprometido está al 93 %
- **THEN** ese valor se muestra con el color del tramo crítico, y no se emite ningún aviso

#### Scenario: Conectar a un servidor comprometido

- **WHEN** se conecta a un servidor cuya primera lectura da varios recursos en tramo crítico
- **THEN** la franja los muestra con su color y no aparece ninguna notificación

### Requirement: La franja se adapta al ancho disponible

Cuando el ancho no alcance para todas las métricas, el sistema SHALL ocultarlas por prioridad desde la
de menor interés, conservando CPU, memoria y disco.

#### Scenario: Panel abierto al lado

- **WHEN** se abre un panel que angosta la sesión
- **THEN** se ocultan primero el tiempo encendido y la distribución, después la carga y después la red,
  y CPU, memoria y disco se mantienen

### Requirement: Activación global y por conexión

Preferencias SHALL ofrecer la activación global de la franja y su intervalo de refresco, sólo para
sesiones SSH. Una conexión SSH SHALL poder encender o apagar la franja por su cuenta; mientras no lo
haga, SHALL seguir el ajuste global. La franja SHALL estar activa por omisión.

#### Scenario: Instalación nueva

- **WHEN** no se configuró nada
- **THEN** las sesiones SSH muestran la franja

#### Scenario: Apagado global

- **WHEN** la franja está apagada globalmente y ninguna conexión la enciende
- **THEN** ninguna sesión la muestra ni muestrea por este motivo

#### Scenario: La conexión apaga lo global

- **WHEN** la franja está activa globalmente y una conexión la apaga
- **THEN** esa sesión no la muestra y las demás sí

#### Scenario: La conexión enciende sobre lo global apagado

- **WHEN** la franja está apagada globalmente y una conexión la enciende
- **THEN** sólo esa sesión la muestra
