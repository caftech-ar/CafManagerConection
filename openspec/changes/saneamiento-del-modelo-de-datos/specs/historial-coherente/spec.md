## ADDED Requirements

### Requirement: Un resultado exitoso no lleva motivo de falla

Una fila de historial con resultado `Success` SHALL tener `failure_reason` nulo, y una con `Failed`
SHALL tenerlo. El esquema SHALL imponerlo y quien anota SHALL cumplirlo.

#### Scenario: Sesión que falló y después conectó

- **WHEN** una sesión falla la autenticación, reintenta y conecta
- **THEN** su fila de historial es `Success` sin motivo

#### Scenario: Sesión que falló sin motivo identificado

- **WHEN** una sesión pasa a error sin que la superficie informe un motivo
- **THEN** su fila de historial es `Failed` con motivo `Other`

#### Scenario: Migración limpia las filas viejas

- **WHEN** antes de migrar hay filas `Success` con motivo
- **THEN** después de migrar esas filas no tienen motivo y la restricción está activa

### Requirement: Alta y poda del historial son atómicas

Registrar una fila y podar las que exceden la retención por conexión SHALL ocurrir en una sola
transacción.

#### Scenario: Interrupción entre alta y poda

- **WHEN** la poda falla después de insertar
- **THEN** la fila insertada tampoco queda

### Requirement: El resumen de la ventana dice cuántos hay en total

La ventana de historial SHALL mostrar «los últimos N de M» cuando el total supera el límite que carga.

#### Scenario: Más filas que el límite

- **WHEN** hay 700 filas y la ventana carga 500
- **THEN** el resumen dice «últimos 500 de 700»
