## 1. El genérico tenue

- [x] 1.1 Sumar a `IconosDeLaInterfaz` la clave del genérico y las que falten para este cambio
- [x] 1.2 Hacer que `IconoDeProceso.ClaveDeIcono` devuelva el genérico en vez de nulo, y reescribir
      su anotación: lo que evita la textura es el énfasis, no la ausencia
- [x] 1.3 Quitar `VisibilidadDelIcono` de `ProcesosPanel`, que es lo que desarma la columna
- [x] 1.4 Pintar el genérico con el color tenue del tema, y los reconocidos con el de su producto
- [x] 1.5 Hacer lo mismo en el panel de puertos, que hoy reserva el hueco pero lo deja mudo
- [x] 1.6 Probar que una lista mezclada alinea todos los nombres en la misma columna
- [x] 1.7 Probar que un proceso reconocido y uno que no se distinguen por el pincel

## 2. Docker: qué es, además de cómo está

- [x] 2.1 Sumar a `Fila` la identidad, separada del estado que ya tiene
- [x] 2.2 Deducirla de `c.Image` con `AplicacionesConocidas.Reconocer`
- [x] 2.3 Agregar la columna a la plantilla, a la izquierda del nombre
- [x] 2.4 Caer en el genérico tenue cuando la imagen no se reconoce
- [x] 2.5 Probar que `nginx:1.25` y `docker.io/library/redis:7` resuelven
- [x] 2.6 Probar que gana la imagen y no el nombre del contenedor

## 3. supervisord: lo mismo

- [x] 3.1 Sumar la identidad a su `Fila`, deducida del nombre del proceso
- [x] 3.2 Agregar la columna con las mismas reglas que Docker
- [x] 3.3 Probar que un proceso con nombre propio cae en el genérico

## 4. nginx: qué tipo de sitio es

- [x] 4.1 Sumar la identidad a su `Fila`, deducida de `ListenPorts` y `DocumentRoot`
- [x] 4.2 Protegido si escucha en el 443 y sirve archivos; servicio web si escucha en claro y sirve
      archivos; genérico tenue si no sirve archivos
- [x] 4.3 Agregar la columna a la izquierda de los puertos
- [x] 4.4 Probar los tres casos, incluido el server block sin raíz

## 5. Túneles

- [x] 5.1 Mostrar con un icono si el túnel está levantado
- [x] 5.2 El caído va en el color tenue, no en rojo: definido y apagado no es una falla

## 6. El emoji que queda

- [x] 6.1 Reemplazar el `⏳` de `PanelInventario` por el icono de espera del catálogo
- [x] 6.2 Probar que no queda ningún carácter de emoji en el texto visible de los XAML

## 7. Los filtros del árbol

- [x] 7.1 Darle icono al filtro de SSH y al de RDP, del mismo tamaño y posición que el de Favoritas
- [x] 7.2 Usar el mismo icono con el que el árbol dibuja una conexión de ese protocolo

## 8. El menú del árbol

- [x] 8.1 Darle icono a las cuatro entradas que no lo tienen, empezando por las tres destacadas
- [x] 8.2 Probar que ninguna entrada del menú queda sin icono
- [x] 8.3 El de eliminar sigue con el pincel destructivo

## 9. Los botones

- [x] 9.1 Listar los botones de barra de herramientas de los paneles y darle icono a cada uno
- [x] 9.2 Darle icono a los botones que borran o descartan, con el pincel destructivo
- [x] 9.3 Dejar sin icono los botones de diálogo que no destruyen
- [x] 9.4 Probar que ningún botón de diálogo que no destruye lleva icono

## 10. Que nada quede suelto

- [x] 10.1 Probar que toda clave de icono que usa la interfaz sale de `IconosDeLaInterfaz`
- [x] 10.2 Probar que toda constante de `IconosDeLaInterfaz` existe en el catálogo
- [x] 10.3 Revisar a ojo el énfasis contra una lista real: el genérico se separa de los reconocidos
      en los dos temas y la columna alinea

## 11. Cierre

- [x] 11.1 Correr la suite completa
- [x] 11.2 Anotar en el CHANGELOG
