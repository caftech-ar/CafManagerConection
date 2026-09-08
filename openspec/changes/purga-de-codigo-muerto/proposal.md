## Why

Un barrido de las 3.387 declaraciones de `src/` contra todo el árbol encontró 21 elementos que nadie
usa. Diecinueve son sobra y se borran sin consecuencia. Los otros dos no: son capacidades que se
escribieron enteras y quedaron desconectadas, y hoy el usuario paga por eso —una caída RDP por
certificado no confiable se informa como «el servidor cerró la conexión», y un túnel automático que
falla impide que se levanten los que vienen atrás—.

Dos de los hallazgos estaban vivos sólo porque sus pruebas los sostenían: la suite en verde los
disfrazaba de usados.

## What Changes

- Se borran 13 símbolos de C# que aparecen una sola vez en todo el repositorio: su declaración.
- Se borran las clases `SiVisible`, `SiOculto`, `ColorDeProtocolo` e `IconoDeProtocolo` con sus
  cuatro entradas de `App.xaml`, y el estilo `BotonPaso` de `PreferenciasWindow.xaml`. Ningún
  `{StaticResource}` del árbol los pide.
- Se borran las dos `ProjectReference` a `CafManagerConection.UseCases` que declaran `Rdp` y
  `Terminal` sin usar un solo tipo suyo.
- Se borran `ArbolDeProcesos` (la clase estática) y `TopProcessesParser` con su record
  `ProcessInfo`, junto con las pruebas que los sostenían. Los dos tienen su reemplazo en el mismo
  archivo: `IndiceDeProcesos` y `ColectorDeProcesos`.
- **Cambio de comportamiento**: la sesión RDP se suscribe a `OnDisconnected` y traduce su código con
  `MapDisconnect`. `SessionFailureReason.CertificateUntrusted` pasa a ser alcanzable.
- **Cambio de comportamiento**: el levantado de túneles automáticos vuelve a `TunnelHost.StartAutoAsync`.
  Un túnel que falla deja de cortar a los demás, y los errores se informan todos.

## Capabilities

### New Capabilities

- `diagnostico-de-desconexion-rdp`: qué causa se le informa al usuario cuando una sesión RDP se
  cae, y de qué evento del control sale cada código.
- `tuneles-automaticos`: qué túneles se levantan al abrir la sesión, qué pasa cuando uno falla y
  cómo se informa.

### Modified Capabilities

Ninguna: `openspec/specs/` está vacío.

## Impact

- `CafManagerConection.Rdp`: `RdpSession` se suscribe a un evento nuevo del ActiveX. Es el único
  punto donde el cambio toca COM.
- `CafManagerConection.Ssh`: `TunnelHost.StartAutoAsync` pasa de muerto a ser el camino único.
- `CafManagerConection.App`: `SessionView` delega el levantado de túneles y muestra los errores.
- `CafManagerConection.Monitoring`: se va la mitad de `ArbolDeProcesos.cs` y de `TopProcessesParser`.
- Pruebas: dos nuevas —una por cambio de comportamiento—, y se podan las de los dos subsistemas
  reemplazados. `DatosRealesTests` guarda salida de servidores reales: lo que cubra algo que
  `ParserDeProcesosTests` no cubra se reapunta antes de borrar.
- Sin cambios de esquema, de base ni de formato en disco.
