## 1. Los SVG en el repo

- [x] 1.1 Crear `Assets/Iconos` en el proyecto de la aplicación, con las subcarpetas de grupo bajo
      `conceptos` y `logos`, con los nombres en minúscula y con guiones
- [x] 1.2 Traer los 190 conceptos de Tabler 3.47.0 a su carpeta de grupo, sin modificar el dibujo
- [x] 1.3 Traer los 27 logos `brand-*` de Tabler a su carpeta de grupo
- [x] 1.4 Traer los 31 logos de Simple Icons 16.31.0 a su carpeta de grupo
- [x] 1.5 Traer los 2 logos de Devicon 2.17.0 —SQL Server y Oracle— a su carpeta de grupo
- [x] 1.6 Anteponer a cada SVG el comentario de origen con paquete, versión, ruta original y licencia
- [x] 1.7 Declarar los tres paquetes con su versión y su licencia en el README
- [x] 1.8 Verificar que hay 250 archivos y que ninguno quedó fuera de una carpeta de grupo

## 2. El conversor de SVG a geometría

- [x] 2.1 Crear `build/convertir-iconos.ps1`, junto a `publicar.ps1`
- [x] 2.2 Leer un SVG y extraer los recorridos de dibujo con sus atributos de trazo
- [x] 2.3 Descartar el recorrido del lienzo transparente que Tabler antepone a cada icono
- [x] 2.4 Concatenar los recorridos restantes en una geometría cuando comparten atributos de trazo
- [x] 2.5 Fallar nombrando el archivo cuando los recorridos no comparten atributos
- [x] 2.6 Emitir un `ResourceDictionary` por grupo, con el comentario de origen sobre cada geometría
- [x] 2.7 Sumar los 20 diccionarios generados a `App.xaml`, después de la paleta y de los estilos
- [x] 2.8 Dar al script un modo de verificación, para que una prueba detecte un diccionario atrasado
- [x] 2.9 Probar que correrlo dos veces seguidas no cambia ningún generado

## 3. El catálogo en el dominio

- [x] 3.1 Crear `OrigenDeIcono` con paquete, versión, ruta original y licencia
- [x] 3.2 Crear `ModoDePintado` con relleno y trazo, y `FamiliaDeIcono` con concepto y logo
- [x] 3.3 Crear `GrupoDeIconos` con los 20 grupos —14 de conceptos y 6 de logos— y su nombre para
      mostrar
- [x] 3.4 Crear `IconoDelCatalogo` con clave, grupo, familia, etiqueta, sinónimos, modo y origen
- [x] 3.5 Crear `CatalogoDeIconos` con las 250 entradas y la resolución por clave
- [x] 3.6 Escribir los sinónimos de búsqueda de los iconos que los necesitan
- [x] 3.7 Crear `BuscadorDeIconos` que filtra por texto, por grupo y por familia, sin distinguir
      mayúsculas ni acentos
- [x] 3.8 Probar que las claves son únicas y que cada una tiene etiqueta y grupo
- [x] 3.9 Probar que el origen declarado por el catálogo coincide con el comentario del SVG
- [x] 3.10 Probar que cada clave del catálogo resuelve a una geometría, y que no sobra ninguna
- [x] 3.11 Probar el buscador: por etiqueta, sin acento, por sinónimo, por clave y sin coincidencias
- [x] 3.12 Probar que una clave que el catálogo no tiene devuelve nulo en vez de romper

## 4. El control que dibuja

- [x] 4.1 Crear `IconoVectorial` en `src/CafManagerConection.App/Themes`, con las propiedades de
      clave, tamaño y pincel
- [x] 4.2 Resolver la geometría desde la clave, y caer en el icono de desconocido cuando no resuelve
- [x] 4.3 Pintar por relleno o por trazo según el modo que declara el catálogo
- [x] 4.4 Aplicar grosor 1,5 hasta 20 puntos y 2,0 de 24 en adelante, con remates y uniones redondos
- [x] 4.5 Dibujar las siluetas macizas al 88 % del alto de la caja, centradas
- [x] 4.6 Sumar el lienzo al catálogo: sin él no se sabe a cuánto escalar, y Devicon viene en 128
- [x] 4.7 Usar el color de texto del tema cuando no se le da pincel
- [x] 4.8 Redibujar cuando cambia la clave, el tamaño o el pincel, sin recrear el control
- [x] 4.9 Probar el grosor de trazo en los seis tamaños
- [x] 4.10 Probar que un icono de relleno no dibuja contorno y uno de trazo no dibuja relleno
- [x] 4.11 Probar que la geometría entra en la caja venga del lienzo de 24 o del de 128
- [x] 4.12 Probar que el catálogo entero se dibuja sin romperse

## 5. La ventana de muestra

- [x] 5.1 Crear `MuestraDeIconosWindow`, que dibuja el catálogo entero agrupado
- [x] 5.2 Poder cambiar el tamaño de dibujo entre los seis tamaños
- [x] 5.3 Poder cambiar el pincel entre los diez colores de la paleta
- [x] 5.4 Abrirla desde Preferencias, junto a «Color de los iconos»
- [x] 5.5 Revisar a ojo los dos temas y anotar los iconos que no se leen a 16 puntos

## 6. La ventana de selección

- [x] 6.1 Crear `SelectorDeIconosWindow` y su código en `src/CafManagerConection.App/Views`
- [x] 6.2 Dibujar la grilla agrupada, con encabezado por grupo y etiqueta por icono
- [x] 6.3 Sumar el buscador, con el foco puesto ahí al abrir
- [x] 6.4 Sumar el filtro por grupo y el de familia, combinables con el buscador
- [x] 6.5 Dejar fuera los glifos de la propia interfaz, por icono y no por grupo
- [x] 6.6 Sumar la opción de volver al icono por omisión
- [x] 6.7 Marcar el icono actual al abrir
- [x] 6.8 Mostrar la clave y la etiqueta al detener el puntero encima
- [x] 6.9 Avisar cuando la búsqueda no encuentra nada
- [x] 6.10 Recorrer con las flechas, elegir con Enter y cancelar con Escape

## 7. Enganchar la selección

- [x] 7.1 Reemplazar el panel de cuadrados de `ConnectionEditorWindow` por la ventana nueva
- [x] 7.2 Reemplazar el de `FolderSettingsWindow` por la misma ventana
- [x] 7.3 Borrar `ArmarSelectorDeIconos`, `MarcarIconoElegido` y `MuestraDe`
- [x] 7.4 Crear `IconosPorOmision`, que dice qué icono le toca a cada protocolo y a una carpeta
- [x] 7.5 Hacer que `NodoArbol` resuelva contra `CatalogoDeIconos`, conservando la caída al icono del
      protocolo
- [x] 7.6 Borrar `JuegoDeIconos` y adaptar sus pruebas al catálogo
- [x] 7.7 Probar que una clave del juego anterior se dibuja con el icono de su protocolo
- [x] 7.8 Probar que toda clave del catálogo le gana a la del protocolo

## 8. Unificar la iconografía de la interfaz

- [x] 8.1 Crear `IconosDeLaInterfaz`, con la clave del catálogo de cada cosa que la aplicación dibuja
- [x] 8.2 Sumar al catálogo los cuatro que faltaban: `chevron-up`, `chevron-down`, `file-text`
      y `photo`
- [x] 8.3 Reemplazar por `IconoVectorial` los quince `Path` de los XAML
- [x] 8.4 Reemplazar por `IconoVectorial` los `Path` que el código arma, incluidas las tres fábricas
      de plantilla de los paneles
- [x] 8.5 Hacer que `MenuIconos` reciba una clave del catálogo y deje de declarar geometrías
- [x] 8.6 Pasar `IconoDeProceso` a los logos de producto, cayendo en el concepto cuando no hay logo
- [x] 8.7 Pasar `IconosDeAplicacion` a los logos de producto, con la misma caída
- [x] 8.8 Borrar las 41 geometrías escritas a mano y el convertidor `GeometriaPorClave`, que quedó
      sin uso
- [x] 8.9 Quitar del README el aviso de licencia de Fluent UI System Icons
- [x] 8.10 Probar que no queda ninguna geometría de icono declarada a mano en XAML ni en C#
- [x] 8.11 Probar que toda clave que nombra la interfaz existe en el catálogo
- [x] 8.12 Revisar a ojo los iconos de la interfaz en los dos temas y en los tres tamaños que usa

## 9. Cierre

- [x] 9.1 Correr la suite completa
- [x] 9.2 Verificar que el publicado no sumó dependencias ni nativos
- [x] 9.3 Comparar el esquema de la base antes y después: es el mismo
- [x] 9.4 Anotar en el CHANGELOG

## 10. Lo que encontró la revisión

- [x] 10.1 `SessionView.Barra.cs` usaba `using Forma = System.Windows.Shapes`: quedó sin migrar y
      `FindResource` tiraba excepción al abrir cualquier sesión SSH
- [x] 10.2 El selector tomaba `CollectionViewSource.View` antes de fijar la fuente, así que la grilla
      quedaba siempre vacía
- [x] 10.3 Los siete accesos de la barra lateral recibían el sufijo del recurso viejo —«Archivos»,
      «Docker»— en vez de la clave del catálogo
- [x] 10.4 Una clave nula volvió a significar «no dibujes nada»: el panel de puertos ponía un signo
      de pregunta en cada fila sin aplicación reconocida
- [x] 10.5 El icono se centra contra `RenderSize` y no contra el tamaño, para que un contenedor que
      lo estire no lo pegue a la esquina
- [x] 10.6 El editor valida la clave guardada antes de dibujarla, y refresca la muestra al cambiar
      de protocolo
- [x] 10.7 La ventana de muestra dejó de cambiar el tema de toda la aplicación sin persistirlo, y
      cuelga de Preferencias, que es quien la abre en modal
- [x] 10.8 `VentanaPropia` dejó de compartir dibujo con `TerminalExterna`: son botones contiguos
- [x] 10.9 `EnElSelector` pasó a ser del icono y no del grupo: `folder`, `star` y `tag` vuelven a
      poder elegirse, y el chrome sigue afuera
- [x] 10.10 Los dos guardianes de «ninguna geometría a mano» tapan sus huecos: el alias del
      namespace y los trazados escritos en un atributo `Data`
- [x] 10.11 El conversor falla si dos grupos homónimos de ramas distintas producen el mismo
      diccionario
- [x] 10.12 La prueba que lanza `pwsh` tiene tope de tiempo, lee los dos flujos en paralelo y mata
      el proceso si se cuelga
- [x] 10.13 README: avisos MIT de Tabler, Devicon y Fluent —`cmc.ico` sigue siendo de Fluent y se
      sigue distribuyendo—, y los conteos corregidos a 217/31/2
- [x] 10.14 Código muerto: `NodoArbol.Icono`, cuatro constantes sin uso y tres ramas inalcanzables
      del mapa de productos

## 11. Las claves guardadas

- [x] 11.1 Escribir `Migration005_ClavesDeIconoDelCatalogo`, que traduce las 16 claves del juego
      anterior en `connections.icon_key` y `connection_folders.icon_key`
- [x] 11.2 Registrarla en `DatabaseInitializer` y subir `LatestVersion` a 5
- [x] 11.3 Probar las 16 traducciones sobre conexiones y sobre carpetas
- [x] 11.4 Probar que una clave que no es del juego anterior no se toca, y que correrla dos veces no
      cambia nada
- [x] 11.5 Probar que toda clave traducida existe en el catálogo
