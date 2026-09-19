## ADDED Requirements

### Requirement: Borrar una etiqueta de fábrica avisa que lo es

La confirmación de borrado SHALL indicar cuando la etiqueta es de fábrica y que restablecer la repone
vacía, sin recuperar las asignaciones.

#### Scenario: Borrar Producción

- **WHEN** el usuario borra la etiqueta «Producción»
- **THEN** la confirmación dice que es de fábrica, cuántos elementos pierden la marca y que restablecer no las recupera

### Requirement: Guardar una etiqueta maneja el error de la base

Si la base rechaza el alta o la edición de una etiqueta, la ventana SHALL mostrar el motivo y seguir
abierta.

#### Scenario: Código duplicado que la UI no detectó

- **WHEN** la base rechaza el guardado por el índice único
- **THEN** la ventana muestra el error y no se cierra

### Requirement: Sin miembros sostenidos sólo por pruebas

`Etiqueta.EsValida` y `CatalogoDeEtiquetas.Quitar` NO SHALL existir.

#### Scenario: Validación al guardar

- **WHEN** se guarda una etiqueta con color fuera de la paleta
- **THEN** el catálogo la rechaza con el mismo motivo que antes
