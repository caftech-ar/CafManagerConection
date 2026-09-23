## ADDED Requirements

### Requirement: Una fila que no se reconoce dibuja un icono genérico y tenue

Cuando un panel no reconoce qué hay del otro lado —un proceso, un puerto, un contenedor— SHALL
dibujar un icono genérico con el color tenue del tema. NO SHALL dejar la celda vacía ni colapsar el
control: la columna de iconos SHALL alinear siempre.

Los iconos reconocidos SHALL pintarse con el color de texto del tema y el genérico con el tenue. La
diferencia de énfasis es lo que evita que el icono se vuelva textura, y SHALL ser de pincel y no de
tono: un panel tiene una sola tabla de reconocimiento, y pedirle además un color por producto
obligaría a consultar una segunda que puede no coincidir con la primera.

#### Scenario: Proceso reconocido

- **WHEN** el panel de procesos lista `nginx`
- **THEN** dibuja el logo de nginx con el color de texto, más fuerte que el de las filas que no se
  reconocen

#### Scenario: Proceso que no se reconoce

- **WHEN** el panel de procesos lista un binario propio que no está en la tabla de conocidos
- **THEN** dibuja el icono genérico en el color tenue, y su nombre queda alineado con el de las
  filas reconocidas

#### Scenario: La columna no se desarma

- **WHEN** una lista mezcla filas reconocidas con filas que no
- **THEN** todos los nombres arrancan en la misma columna

#### Scenario: Puerto a la escucha sin aplicación reconocida

- **WHEN** el panel de puertos lista un puerto cuyo proceso no se reconoce
- **THEN** dibuja el genérico tenue, igual que el panel de procesos

### Requirement: Identidad y estado son dos columnas, no una

En los paneles que muestran las dos cosas, el estado —correcto, advertencia, error— y la identidad
—qué producto es— SHALL ocupar columnas distintas, cada una con su icono. NO SHALL codificarse el
estado en el color del icono de identidad, porque el color ya identifica al producto.

#### Scenario: Contenedor corriendo

- **WHEN** el panel de Docker lista un contenedor de la imagen `postgres:16` que está corriendo
- **THEN** muestra el icono de correcto en la columna de estado y el logo de PostgreSQL en la de
  identidad

#### Scenario: Contenedor detenido

- **WHEN** ese mismo contenedor está detenido
- **THEN** cambia el icono de la columna de estado, y el de identidad sigue siendo el de PostgreSQL

#### Scenario: Contenedor de una imagen que no se reconoce

- **WHEN** el contenedor viene de una imagen propia
- **THEN** la columna de identidad dibuja el genérico tenue y la de estado sigue diciendo cómo está

#### Scenario: Proceso de supervisord

- **WHEN** el panel de supervisord lista un proceso
- **THEN** muestra estado e identidad en columnas separadas, con las mismas reglas que Docker

### Requirement: La identidad de un contenedor sale de su imagen

El panel de Docker SHALL deducir qué es un contenedor de su imagen, no de su nombre: el nombre lo
elige quien levanta el contenedor y la imagen dice qué corre adentro.

#### Scenario: Imagen con versión

- **WHEN** la imagen es `nginx:1.25`
- **THEN** reconoce nginx

#### Scenario: Imagen con registro adelante

- **WHEN** la imagen es `docker.io/library/redis:7`
- **THEN** reconoce Redis

#### Scenario: Nombre que contradice a la imagen

- **WHEN** un contenedor se llama `base-de-datos` pero corre la imagen `nginx`
- **THEN** dibuja nginx, que es lo que corre

### Requirement: En nginx el icono dice qué tipo de sitio es

Cada fila del panel de nginx es un server block, no un producto: el logo de nginx en todas SHALL NO
usarse, porque el panel ya se llama nginx. El icono SHALL decir de qué tipo de sitio se trata,
deducido de los puertos y de la raíz de documentos que el inventario ya lee.

#### Scenario: Sitio seguro

- **WHEN** el server block escucha en el 443 y tiene raíz de documentos
- **THEN** dibuja el icono de protegido

#### Scenario: Sitio plano

- **WHEN** escucha en el 80 y tiene raíz de documentos
- **THEN** dibuja el icono de servicio web

#### Scenario: Server block sin raíz

- **WHEN** no declara raíz de documentos —es un proxy, una redirección, o algo más—
- **THEN** dibuja el genérico tenue, que dice que no sirve archivos sin afirmar por qué

### Requirement: Un túnel muestra si está activo

El panel de túneles SHALL mostrar con un icono si cada túnel está levantado, sin obligar a leer la
fila entera.

#### Scenario: Túnel levantado

- **WHEN** el túnel está activo
- **THEN** su fila lo muestra con el icono de conectado

#### Scenario: Túnel definido pero caído

- **WHEN** el túnel está definido y no está levantado
- **THEN** su fila lo muestra con el icono de desconectado, en el color tenue

### Requirement: Ningún emoji hace de icono

La interfaz NO SHALL usar caracteres de emoji como icono: un emoji lo dibuja la fuente del sistema,
no sigue el tema ni el tamaño del resto, y no se puede pintar con la paleta.

#### Scenario: El panel de inventario

- **WHEN** el panel de inventario indica que está esperando
- **THEN** lo dibuja con el icono de espera del catálogo, y no con un carácter de reloj

#### Scenario: Buscar emojis en la interfaz

- **WHEN** se recorren los XAML buscando caracteres de emoji en texto visible
- **THEN** no hay ninguno
