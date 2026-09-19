## ADDED Requirements

### Requirement: Todo miembro de un enum persistido tiene productor

El código SHALL quedar sin `SessionFailureReason.CredentialMissing`,
`BanderasDeRendimientoRdp.SinSombraDelCursor` ni `SettingKeys.ConnectionTimeoutSeconds`. La conducta
del reintento por identidad de Windows SHALL conservarse.

#### Scenario: Reintento por identidad de Windows

- **WHEN** una sesión RDP falla con un motivo que conviene caer al pedido de credenciales
- **THEN** se pide la credencial igual que antes de quitar el miembro

### Requirement: El modo de pestaña se mapea por valor

El selector de modo de pestaña SHALL guardar y leer el valor del enum, no la posición del elemento en
el combo. El parseo de `tabs.mode` SHALL vivir en el servicio de ajustes junto al del tema.

#### Scenario: Reordenar el combo

- **WHEN** se cambia el orden de los elementos del combo en la vista
- **THEN** elegir «Envuelto en filas» sigue guardando «EnvueltoEnFilas»

### Requirement: Todas las claves de ajustes viven en un solo lugar

Las claves `updates.lastCheckedAt`, `updates.postponedVersion` y `updates.postponedAt` SHALL estar en
`SettingKeys`.

#### Scenario: Una sola definición

- **WHEN** se busca el literal de una clave de actualizaciones en el código
- **THEN** aparece una sola vez, en `SettingKeys`
