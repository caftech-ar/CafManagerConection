## Context

El catálogo de 250 iconos, `IconoVectorial` y `IconosDeLaInterfaz` ya están y funcionan. Este cambio
no agrega ni un icono: pone los que hay donde faltaban.

Lo que hay hoy, panel por panel:

| Panel | Identidad | Estado | Nota |
|---|---|---|---|
| Árbol | sí | punto de color | Completo |
| Archivos (SFTP) | sí | — | Completo |
| Procesos | sí | — | **Colapsa cuando no reconoce y desarma la columna** |
| Puertos | sí | — | Reserva el hueco pero lo deja mudo |
| Docker | **no** | sí | La columna se llama `Icono` y es estado |
| supervisord | **no** | sí | Igual |
| nginx | no | no | Cuatro columnas de texto |
| Túneles | no | no | El estado hay que deducirlo |
| Estado del servidor | no | no | Métricas |
| Inventario | `⏳` | — | El único emoji de la interfaz |

Y en los controles: la barra de sesión tiene icono en sus 10 botones, el menú del árbol en 14 de 18,
y de los tres filtros sólo Favoritas.

## Goals / Non-Goals

**Goals**

- Que ninguna columna de iconos se desarme.
- Que un panel diga qué hay del otro lado, no sólo si está bien.
- Que el icono siga siendo señal: donde está, se nota; donde no aporta, no está.
- Que todo lo que la interfaz dibuja se nombre en un solo lugar.

**Non-Goals**

- Sumar iconos al catálogo. Este cambio usa los 250 que están.
- Tocar `IconoVectorial`. Ya hace todo lo que hace falta.
- Ampliar el parser de nginx. Lo que el inventario ya lee alcanza para tres tipos de sitio; leer
  `proxy_pass` y `return` para distinguir proxy de redirección es el único punto que tocaría la
  lectura del servidor, y queda afuera.
- Guardar nada. Ninguna de estas decisiones se persiste.

## Decisions

### El desconocido se dibuja tenue, no se esconde

`IconoDeProceso` documenta hoy lo contrario: «Sin coincidencia devuelve `null`: mostrar un icono
genérico a todo hace que ninguno signifique nada». El principio es correcto y el remedio estaba
errado.

Lo que hace que un icono deje de ser señal no es que esté, es que todos pesen igual. Con el genérico
más apagado que los reconocidos, una lista de cuarenta procesos sigue teniendo sólo cinco iconos que
se leen, y los otros treinta y cinco dicen «no sé» sin gritar.

Y hay un costo concreto del remedio viejo, que es el que se ve: `Collapsed` ocupa cero, así que la
columna se desarma y el ojo pierde la referencia.

*Alternativa descartada*: reservar el hueco sin dibujar nada. Alinea, pero no distingue «no lo
reconozco» de «todavía no cargó».

El genérico es la caja de «aplicación / servicio», no el signo de pregunta de «desconocido». En un
servidor con cuarenta procesos, treinta y cinco llevan ese icono: treinta y cinco signos de pregunta
se leen como «acá hay un problema», y no lo hay —son procesos normales que la tabla de conocidos no
nombra—. La caja dice «esto es algo que corre» sin afirmar qué, que es exactamente lo que se sabe.

*Alternativa descartada*: un punto. Es lo más callado, pero un punto no es un icono: no dice nada,
sólo alinea, y eso ya lo hacía el hueco reservado.

El énfasis es de pincel, no de tono: reconocido va con el color de texto y el genérico con el tenue.
Pintar cada producto de su color obligaría a consultar `AplicacionesConocidas` además de
`IconoDeProceso`, que son dos tablas de reconocimiento distintas y pueden no coincidir sobre el mismo
proceso. Una sola tabla por panel.

### Identidad y estado no comparten columna

Un contenedor querría decir dos cosas: qué es y cómo está. La respuesta más compacta sería un solo
icono con un distintivo en la esquina, que es lo que proponía el brief original del catálogo.

Van dos columnas. Cuesta unos catorce puntos de ancho y ahorra el código de composición: dos
`IconoVectorial`, que ya existe y está probado, contra un dibujo compuesto que hay que armar, alinear
y probar aparte. El distintivo se puede hacer después si el ancho llega a molestar.

*Alternativa descartada*: un solo icono de identidad pintado con el color del estado. El color ya lo
usa `PaletaIconos` para identificar al producto, así que los dos usos chocarían.

### La identidad de un contenedor sale de la imagen

`AFila` ya tiene `c.Image`, y `AplicacionesConocidas.Reconocer` compara por subcadena y se queda con
la clave más larga, así que `nginx:1.25` y `docker.io/library/redis:7` resuelven sin tocar nada.

El nombre del contenedor no sirve: lo elige quien lo levanta y miente seguido —un contenedor llamado
`base-de-datos` puede estar corriendo un nginx—.

### En nginx el icono habla del sitio, no del producto

Poner el logo de nginx en cada fila del panel de nginx no agrega nada: el panel ya se llama nginx.
Lo que distingue una fila de otra es qué hace ese server block.

`NginxSite` trae `ListenPorts` y `DocumentRoot`, y con eso salen tres casos: escucha en el 443 y
sirve archivos, escucha en claro y sirve archivos, o no sirve archivos. El tercero engloba proxy
inverso y redirección sin distinguirlos, y el genérico tenue dice exactamente eso: no sirve archivos,
y no se afirma por qué.

### El menú del árbol se completa, no se poda

La primera lectura de este cambio decía que el menú tenía un icono de veintitrés y proponía llegar a
cuatro. Estaba mal medido: la cuenta ignoraba los argumentos nombrados. El menú tiene **catorce de
dieciocho**, y ya eligió su línea hace tiempo.

Lo que chirría no es que haya muchos iconos: es que **las tres entradas destacadas —conectar, abrir
en el navegador, abrir todas las conexiones— son justamente las que no tienen**, mientras que catorce
del montón sí. Está al revés.

Así que se completan las cuatro que faltan y el menú queda entero. Podarlo a cuatro habría sido
sacarle el icono a diez entradas que ya lo tenían, por una regla que el menú nunca siguió.

*Dónde sí aplica la regla*: en los botones, que es donde no hay nada decidido todavía.

La misma regla en los botones: los de barra de herramientas sí, porque son chicos y se escanean; los
destructivos sí, porque ahí importa no equivocarse; los de diálogo no, porque con dos botones y su
texto no hay nada que encontrar. Un ✓ en Guardar además chocaría con el ✓ de «correcto» del catálogo.

### Ningún emoji hace de icono

`PanelInventario` dibuja `⏳` como texto. Un emoji lo dibuja la fuente del sistema: no sigue el tema,
no sigue el tamaño del resto y no se puede pintar con la paleta. Es el único que queda y se va.

## Risks / Trade-offs

**El genérico puede volverse ruido igual** → Depende del tono. Si `TextoTenue` no alcanza a separarse
de los reconocidos, hay que bajarle opacidad al genérico; se decide mirándolo, no antes.

**Dos columnas cuestan ancho** → Unos catorce puntos en paneles que ya van apretados. Si molesta, el
distintivo en la esquina sigue siendo la salida, y el catálogo ya tiene los iconos de estado.

**Reconocer por subcadena acierta de más** → `Reconocer` se queda con la clave más larga, que es lo
que evita que `postmaster` matchee `master`. Una imagen propia que contenga por casualidad el nombre
de un producto conocido va a mostrar ese logo. Es preferible a no mostrar nada, y el usuario ve la
imagen en la columna de al lado.

**El tercer caso de nginx agrupa cosas distintas** → Proxy y redirección se dibujan igual, con el
genérico. Es honesto: el icono dice «no sirve archivos», que es cierto de los dos.

*Alternativa descartada*: suponer que es proxy, que acierta la mayoría de las veces. Cuando es una
redirección el icono miente, y un icono que miente es peor que uno que no dice: se cree sin
verificar.

## Open Questions

- **El tono del genérico.** `TextoTenue` es la primera apuesta. Se ajusta viéndolo contra una lista
  real de procesos, no antes.
- **Estado del servidor.** El panel de métricas no tiene iconos y no es obvio que los necesite: CPU,
  memoria y disco ya vienen rotulados. Queda afuera hasta que alguien lo pida.
- **El distintivo en la esquina.** Si el ancho de dos columnas molesta en algún panel, es el plan B.
  No se implementa por las dudas.
