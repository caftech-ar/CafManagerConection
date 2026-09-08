## ADDED Requirements

### Requirement: La caída de una sesión RDP se informa con su causa

Cuando una sesión RDP conectada se cae, el sistema SHALL informar una causa del conjunto cerrado de
`SessionFailureReason`, con un texto para el usuario y una acción sugerida, y SHALL guardar el
código crudo que entregó el control en el detalle técnico.

#### Scenario: El servidor rechaza las credenciales

- **WHEN** el control informa un código de desconexión por credenciales rechazadas
- **THEN** la causa es `AuthenticationRejected`
- **AND** el detalle técnico conserva el código

#### Scenario: No se llega al servidor

- **WHEN** el control informa un código de desconexión por servidor inalcanzable
- **THEN** la causa es `HostUnreachable`

#### Scenario: Código que no está en la tabla

- **WHEN** el control informa un código que ninguna rama contempla
- **THEN** la causa es `UnexpectedDisconnect`
- **AND** el detalle técnico conserva el código, para poder agregarlo a la tabla después

### Requirement: El certificado no confiable se distingue de un cierre cualquiera

El sistema SHALL informar `SessionFailureReason.CertificateUntrusted` cuando el control atribuya la
caída a que el certificado del servidor no es de confianza, y NO SHALL informarla como un cierre
genérico del servidor.

#### Scenario: Caída por certificado

- **WHEN** el control informa un código de desconexión por certificado no confiable
- **THEN** la causa es `CertificateUntrusted`
- **AND** el usuario ve que puede aceptar el certificado desde la edición de la conexión

#### Scenario: El historial lo muestra

- **WHEN** una sesión terminó con `CertificateUntrusted`
- **THEN** el historial de conexiones lo muestra como «Certificado no confiable»

### Requirement: Ninguna causa del conjunto es inalcanzable

Todo valor de `SessionFailureReason` que la interfaz sepa mostrar SHALL poder producirse desde algún
camino del código que corre.

#### Scenario: Cada valor mostrado tiene quien lo produzca

- **WHEN** se recorre el conjunto de causas que el historial sabe traducir a texto
- **THEN** cada una de ellas la produce al menos un camino alcanzable

### Requirement: La caída se informa una sola vez

El sistema SHALL informar una única causa por caída, aunque el control la anuncie por más de una vía.

#### Scenario: Dos vías, un solo aviso

- **WHEN** la sesión se cae y el control lo anuncia por el evento de desconexión y además el sondeo
  de estado detecta la caída
- **THEN** se informa una sola causa y la sesión pasa a `Error` una sola vez
