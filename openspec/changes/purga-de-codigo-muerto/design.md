## Context

El barrido cruzó las 3.387 declaraciones de `src/` contra todo el árbol, contando apariciones del
identificador en `.cs` y `.xaml`. Los 19 borrados no admiten discusión: aparecen una sola vez. Los
dos cambios de comportamiento sí, y los dos tienen la misma forma —la capacidad se escribió entera y
quedó desconectada—, pero cuestan muy distinto.

`RdpClientHost` es reflexión tardía pura sobre el OCX: `InvokeMember` para leer y escribir
propiedades, y nada más. No hereda la plomería de eventos de `AxHost` ni implementa
`IConnectionPointContainer`. El proyecto evita el interop generado a propósito, y está anotado en el
propio archivo: la tarea de MSBuild de `COMReference`/`aximp` sólo existe en .NET Framework y con
`dotnet build` falla con MSB4803.

`TunnelHost` no tiene ninguna de esas ataduras: es C# sobre SSH.NET, con arnés de prueba en
`Ssh.Tests`.

## Goals / Non-Goals

**Goals:**

- Que ningún símbolo público de `src/` quede sin quien lo nombre fuera de su declaración.
- Que ningún valor de `SessionFailureReason` que la interfaz sepa mostrar sea inalcanzable.
- Que un túnel automático que falla no impida levantar los que vienen atrás.
- Que la lógica de «qué túneles levantar y qué hacer si uno falla» viva donde se puede probar.

**Non-Goals:**

- Revisar los 22 símbolos del grado 4 —los que sólo usan las pruebas—. Van de a uno y aparte.
- Encender los analizadores de estilo ni escribir los guardianes propuestos.
- Revisar el mapeo de `ExtendedDisconnectReason` que ya corre. Se conserva tal cual.
- Cambiar el esquema de la base o el formato en disco.

## Decisions

### El levantado de túneles vuelve a `TunnelHost`, no se borra

`SessionView.LevantarTunelesAutomaticosAsync` filtra por `AutoStart` y hace `await StartAsync` en un
`foreach` dentro de un solo `try`: el primer túnel que lanza corta el resto. `StartAutoAsync` hace el
mismo filtro pero acumula los errores y sigue, que es lo que su `<summary>` promete.

Se elige mover en vez de borrar por dos motivos: borrar consagra la pérdida del comportamiento, y la
decisión de dominio queda en un `.xaml.cs`, donde ninguna prueba la alcanza. La vista pasa a llamar
`StartAutoAsync` y a mostrar la lista de errores que devuelve.

Alternativa descartada: dejar la lógica en la vista y sólo corregir el `try`. Arregla el
comportamiento pero no la fuga de capa, y deja el método muerto igual.

### El diagnóstico de desconexión RDP exige un sumidero de eventos COM

`MapDisconnect` traduce los códigos que el ActiveX entrega en `IMsTscAxEvents::OnDisconnected`
—260, 516, 2308, 2825, 1288, 3591 y compañía—. **Esos códigos no son legibles como propiedad**: el
control no expone un `DisconnectReason`, sólo `ExtendedDisconnectReason`, que es otra numeración y es
la que ya lee `MotivoDeDesconexion`.

La detección de caída de hoy no es un evento: es un `Timer` de 250 ms que sondea `Connected` y, al
verlo caer a 0 después de haber arrancado, llama a `MotivoDeDesconexion`. Para alcanzar
`CertificateUntrusted` hay que suscribirse al evento, y para suscribirse a un evento de un OCX
alojado por reflexión hay que armar el sumidero a mano: `IConnectionPointContainer.FindConnectionPoint`
sobre el IID de `IMsTscAxEvents` más un objeto que implemente `IDispatch` y despache por `dispid`.

Esto es interop nuevo en el único proyecto que toca COM, y contradice el estimado con el que se tomó
la decisión. Queda en Open Questions.

### Los dos subsistemas reemplazados se borran con sus pruebas, pero el material se rescata primero

`ArbolDeProcesos` estática y `TopProcessesParser` viven porque sus pruebas los llaman.
`ArbolDeProcesosTests` prueba además `IndiceDeProcesos` y `NodoDeProceso`, que siguen vivos: se
separa lo que se queda antes de borrar. `DatosRealesTests` guarda salida textual de servidores
reales que costó conseguir; se revisa qué casos cubren algo que `ParserDeProcesosTests` no cubra y
se reapuntan al `ColectorDeProcesos` en vez de tirarlos con el parser.

### Las dos `ProjectReference` se borran sin reemplazo

`Rdp` usa `Domain.Credentials` y `Domain.Sessions`; `Terminal`, sólo `System.*`. Ninguno toca un tipo
de `UseCases`. Quitar la referencia no cambia nada en tiempo de ejecución y achica el grafo.

## Risks / Trade-offs

- **Borrar algo que se alcanza por reflexión o por enlace de datos** → La detección es textual. Se
  comprobó cada símbolo en `.cs` y `.xaml`, pero un nombre armado en tiempo de ejecución no se ve.
  Mitigación: `TreatWarningsAsErrors` está en `true`, la suite corre entera al cerrar, y cada paso va
  en su propio commit para poder revertir uno solo.
- **Perder cobertura al podar las pruebas de los subsistemas reemplazados** → Mitigación: separar y
  reapuntar antes de borrar, y comprobar que la cuenta de pruebas de `Monitoring.Tests` baje sólo por
  las que prueban lo que se fue.
- **Que el aviso de caída RDP se duplique** → El sondeo de `Connected` y el evento pueden disparar
  para la misma caída. Mitigación: la primera vía que llegue gana y la segunda no vuelve a informar;
  hay un escenario de spec que lo exige.
- **Que el sumidero de eventos COM se lleve puesto el ciclo de vida del control** → Es el área que ya
  costó dos defectos anotados en el propio archivo (`InvalidComObjectException` al desmontar y al
  cerrar). Mitigación: si se hace, se desengancha antes de `ReleaseCom`, y las pruebas de
  `CicloDeVidaTests` y `DesmontajeDeSesionTests` tienen que seguir en verde.

## Migration Plan

Un commit por paso, en este orden:

1. Borrar los 19 huérfanos. Sin prueba que los toque; si algo faltaba, no compila.
2. Devolver el levantado de túneles a `TunnelHost` y agregar su prueba.
3. Separar `ArbolDeProcesosTests` y borrar `ArbolDeProcesos` estática.
4. Rescatar lo que corresponda de `DatosRealesTests` y borrar `TopProcessesParser` con `ProcessInfo`.
5. El diagnóstico de desconexión RDP, cuando se resuelva la pregunta abierta.

Vuelta atrás: cada paso se revierte solo con `git revert` de su commit. Ninguno toca datos del
usuario ni el esquema de la base.

## Open Questions

- **¿Se paga el sumidero de eventos COM?** El estimado con el que se eligió cablear `OnDisconnected`
  era «una prueba y comprobar que no se dupliquen los avisos». El costo real incluye escribir interop
  de punto de conexión a mano en el proyecto que aloja el ActiveX. Las salidas posibles:
  cablear igual, cortar `MapDisconnect` y el valor del enum, o dejar el paso 5 fuera de este cambio y
  tratarlo como un defecto aparte con su propia propuesta.
