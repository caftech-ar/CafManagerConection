## 1. Borrar los huérfanos

- [x] 1.1 `ActualizacionesService.GuardarAjustesAsync`
- [x] 1.2 `enum TipoDeElemento` de `Etiqueta.cs`
- [x] 1.3 `Defaults.MetricsHistoryPoints`
- [x] 1.4 `Serializacion.EtiquetasATexto` y `Serializacion.TextoAEtiquetas`
- [x] 1.5 `MetricsCollector.IsLinuxAsync`
- [x] 1.6 `SnapshotHistory.Latest` y `SnapshotHistory.NetworkSeries`
- [x] 1.7 `SshSession.EscaladaDeSudo` y `SshSession.SondeosDeSudo`
- [x] 1.8 `TerminalControl.TextoBuscado`
- [x] 1.9 `VtEmulator.Rebind`
- [x] 1.10 `ImportadorDeConexiones.AlgoEntro`
- [x] 1.11 `RemoteFileSession.UniqueName`
- [x] 1.12 Las clases `SiVisible`, `SiOculto`, `ColorDeProtocolo` e `IconoDeProtocolo`, y sus cuatro entradas de `App.xaml`
- [x] 1.13 El estilo `BotonPaso` de `PreferenciasWindow.xaml`
- [x] 1.14 Las dos `ProjectReference` a `UseCases` de `Rdp.csproj` y `Terminal.csproj`
- [x] 1.15 Compilar: 0 errores, 0 advertencias

`Rdp` usaba `Domain.Credentials` y `Domain.Sessions` por la referencia transitiva de `UseCases`: se
reemplazó por una referencia directa a `Domain`, que es la que de verdad tiene. `Terminal` no usaba
ningún tipo de otro proyecto y quedó sin ninguna.

## 2. Devolver el levantado de túneles a TunnelHost

- [x] 2.1 `SessionView.LevantarTunelesAutomaticosAsync` llama a `TunnelHost.StartAutoAsync`
- [x] 2.2 La vista informa en la barra de estado los errores que devuelve, con el nombre de cada túnel
- [x] 2.3 Prueba en `Ssh.Tests`: el primero falla y los demás quedan activos
- [x] 2.4 Prueba en `Ssh.Tests`: sin fallos no se informa ningún error
- [x] 2.5 Correr `Ssh.Tests` y `App.Tests`

Las cuatro pruebas de `TunelesAutomaticosTests` ocupan puertos locales de verdad para que `StartAsync`
falle antes de tocar la red: por eso corren sin servidor.

## 3. Borrar la clase estática ArbolDeProcesos

- [x] 3.1 Separar en `ArbolDeProcesosTests` lo que prueba `IndiceDeProcesos` y `NodoDeProceso`
- [x] 3.2 Borrar los casos que prueban `Armar()` y `HijosDirectos()` de la estática
- [x] 3.3 Borrar la clase `ArbolDeProcesos`
- [x] 3.4 Correr `Monitoring.Tests` y comprobar que la cuenta baja sólo por lo que se fue

De 18 casos quedaron 9. Los que probaban el bosque —`Armar()`— se fueron con la clase; los que
probaban `NodoDeProceso` se reapuntaron a `IndiceDeProcesos.Subarbol`, que da el mismo nodo.

## 4. Borrar TopProcessesParser y ProcessInfo

- [x] 4.1 Revisar qué casos de `DatosRealesTests` cubren algo que `ParserDeProcesosTests` no cubra
- [x] 4.2 Reapuntar ese material al `ColectorDeProcesos`
- [x] 4.3 Borrar `TopProcessesParser`, el record `ProcessInfo` y las pruebas que quedaron sin objeto
- [x] 4.4 Correr `Monitoring.Tests`

No hubo nada que reapuntar. `DatosDeSistemaParser.UsuariosPorUid` sigue vivo —lo llama
`ParserDeProcesos`— y sus dos únicas pruebas estaban entre las que se iban, pero
`ParserDeProcesosTests` ya cubre las dos afirmaciones por el camino que corre:
`El_uid_se_traduce_al_nombre_del_passwd` y `Un_uid_que_no_esta_en_el_passwd_se_muestra_como_numero`.
Se fueron también las muestras `TopCpuServidor2` y `TopCpuServidor3`, que eran salida de `ps` y ya
no las lee nadie.

## 5. Diagnóstico de desconexión RDP

Bloqueado. El estimado con el que se decidió cablear no contemplaba lo que hace falta de verdad:
`RdpClientHost` es reflexión tardía pura sobre el OCX y no tiene sumidero de eventos COM, y los
códigos de `OnDisconnected` no se leen como propiedad.

- [ ] 5.1 Resolver la pregunta abierta del diseño
- [ ] 5.2 Según la salida: cablear `OnDisconnected`, o cortar `MapDisconnect` con el valor del enum

## 6. Cierre

- [x] 6.1 Compilación entera en 0 errores y 0 advertencias
- [x] 6.2 Suite completa en verde: 2.876 superadas, 26 omitidas —las de integración SSH, que piden servidor—
- [x] 6.3 Bajar los servidores de compilación y comprobar que no quedan procesos vivos
- [x] 6.4 Entregar los comandos de git
