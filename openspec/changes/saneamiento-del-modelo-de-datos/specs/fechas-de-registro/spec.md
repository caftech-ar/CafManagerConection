## ADDED Requirements

### Requirement: Las fechas guardadas vuelven a la entidad

Al cargar una conexión, una carpeta o una etiqueta, el sistema SHALL devolver `CreatedAt` y `UpdatedAt`
con los valores guardados en la base, nunca con la hora de carga.

#### Scenario: Ida y vuelta de las fechas

- **WHEN** se guarda una conexión, pasa una hora y se vuelve a cargar
- **THEN** `CreatedAt` y `UpdatedAt` son los que se guardaron, no la hora de la carga

### Requirement: La fecha de modificación cambia sólo ante un cambio real

`UpdatedAt` SHALL avanzar únicamente cuando cambia algún dato de la entidad. Cargar, volver a guardar
sin cambios, o registrar una conexión exitosa NO SHALL modificarla.

#### Scenario: Guardar sin cambios

- **WHEN** se abre el editor de una conexión y se guarda sin tocar nada
- **THEN** `UpdatedAt` queda igual que antes

#### Scenario: Renombrar

- **WHEN** se cambia el nombre de una carpeta
- **THEN** `UpdatedAt` de esa carpeta avanza y `CreatedAt` no cambia

### Requirement: Un solo formato de fecha en toda la base

Las columnas `created_at`, `updated_at` y `attempted_at` de todas las tablas SHALL tener el mismo
formato ISO-8601 en UTC con sufijo `Z`, incluida la semilla de etiquetas. Los ajustes de
actualizaciones conservan su desfase horario y quedan fuera.

#### Scenario: Semilla de etiquetas

- **WHEN** se crea una base nueva
- **THEN** las etiquetas de fábrica tienen `created_at` y `updated_at` en el mismo formato que el resto de la base

#### Scenario: Etiquetas existentes al migrar

- **WHEN** una base tiene etiquetas con fecha en el formato viejo con espacio
- **THEN** después de migrar están en el formato ISO con `Z`

### Requirement: Las fechas se muestran

El editor SHALL mostrar «Creada» y «Modificada» de una conexión existente en su pestaña Avanzado, y
el editor de carpeta SHALL mostrarlas para la carpeta.

#### Scenario: Editar una conexión existente

- **WHEN** se abre el editor de una conexión guardada y se va a la pestaña Avanzado
- **THEN** se ven la fecha de creación y la de última modificación

#### Scenario: Crear una conexión nueva

- **WHEN** se abre el editor para dar de alta una conexión
- **THEN** las dos fechas no se muestran, porque todavía no existen
