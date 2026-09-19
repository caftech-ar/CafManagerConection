# Tareas

## Investigación previa

- [x] 0.1 Mirar `installer/CafManagerConection.nsi`: si admite instalar un tipo sobre el otro sin
  desinstalar primero, y ajustar el texto de la advertencia según lo que se encuentre.

## Selección

- [x] 1.1 `SelectorDeInstalador`: sumar la enumeración de los candidatos con su tipo, reusando la
  clasificación existente; `Elegir` queda como está.
- [x] 1.2 `ActivoDeRelease`: sumar el tamaño —hoy sólo tiene nombre y dirección de descarga— y leerlo en
  `ConsultorDeReleases`; tolerar que la publicación no lo informe, mostrando el instalador sin tamaño.
- [x] 1.3 Pruebas: dos instaladores, uno solo, ninguno, y nombres que no permiten distinguir el tipo.

## Aviso

- [x] 2.1 Presentar los candidatos con tipo, tamaño y condición, marcando el tipo instalado cuando la
  marca del registro exista.
- [x] 2.2 Preseleccionar el que elige hoy la selección automática.
- [x] 2.3 Advertir antes de descargar cuando el elegido no sea del tipo instalado.
- [x] 2.4 Informar sin ofrecer descarga cuando no hay instalador reconocible.

## Cierre

- [x] 3.1 `openspec validate eleccion-de-instalador-al-actualizar --strict`.
- [x] 3.2 Suite de los proyectos afectados en verde.
