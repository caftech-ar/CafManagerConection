## Context

`PlanDeSesionRdp.Para` arma una lista de `AjusteDeRdp(Ambito, Propiedad, Valor)` y `Aplicar()` la
escribe sobre uno de tres objetos: el OCX, `AdvancedSettingsN` o `SecuredSettingsN`, todos alcanzables
con reflexión tardía (`TrySetOn`), lo que da gratis el registro de `PropiedadesNoAceptadas`. Los
cuatro ajustes de este cambio no encajan todos ahí: rendimiento y programa inicial sí; escala/DPI vive
en `IMsRdpExtendedSettings`, que no es el dispatch por defecto; y la resolución dinámica no es una
propiedad sino un desdoble de conducta de `FitToTab`, que hoy mezcla `SmartSizing` con
`UpdateSessionDisplaySettings`.

Restricciones fijas del escenario: servidor sin SSH, usuario sin privilegios, sólo el 3389. Todo lo
de este cambio es cliente puro y no las viola.

## Goals / Non-Goals

**Goals:**

- Un mecanismo único para alcanzar interfaces del control que no son el dispatch por defecto, sin
  perder el diagnóstico de `PropiedadesNoAceptadas`.
- Rendimiento, escala/DPI, resolución dinámica y programa inicial elegibles por conexión; los
  heredables por carpeta con la semántica de tres estados ya vigente.
- Separar las dos conductas que hoy oculta `FitToTab`.

**Non-Goals:**

- Multimonitor (necesita `IMsRdpClientNonScriptable5` y selección de monitores).
- Reconexión automática del control (obligaría a reescribir `VigilarEstado`).
- Captura de pantalla del control.
- Publicar RemoteApp en el servidor: fuera de alcance por falta de privilegios.

## Decisions

**Ámbito `Extendida` en lugar de un método suelto.** Se agrega `AmbitoDeRdp.Extendida` y en
`Aplicar()` se resuelve el objeto `IMsRdpExtendedSettings` una vez (como ya se resuelven `avanzados`
y `asegurados`), aplicando cada ajuste con `TrySetOn`. Alternativa descartada: un método aparte
`SetExtendedProperty` en `RdpSession` fuera del bucle de `Aplicar`, que duplicaría el manejo de
`PropiedadesNoAceptadas`. El ámbito mantiene un solo camino de aplicación y un solo lugar de
diagnóstico.

**El desdoble de `FitToTab` es un enum de tres valores, no dos booleanos.** Modo de tamaño:
`Ninguno` / `EscalarPixeles` / `RenegociarResolucion`. Dos booleanos permitirían el estado
contradictorio «escalar y renegociar a la vez», que es justo lo que este cambio elimina. `ConfigureDisplay`
fija `SmartSizing` según el modo; `Resize` sólo llama a `UpdateSessionDisplaySettings` en
`RenegociarResolucion`. Al leer una conexión vieja, `FitToTab = true` mapea a `EscalarPixeles` (su
conducta actual) y `false` a `Ninguno`.

**Programa inicial y RemoteApp, dos niveles del mismo campo.** `StartProgram`/`WorkDir` van al ámbito
Asegurados y funcionan siempre. RemoteApp añade `RemoteProgramMode` (Avanzados) y configura el objeto
`RemoteProgram2` del control; su fallo se traduce en `MotivoDeDesconexion`/`MapDisconnect` a una causa
propia para no confundirlo con red o credenciales. Alternativa descartada: dos capacidades separadas;
comparten campo y UI, y separarlas duplicaría el editor.

**Persistencia por columnas nuevas, sin migración destructiva.** Los ajustes heredables
(rendimiento, tipo de red, escala, modo de tamaño, programa inicial) siguen el patrón ya aceitado:
campo en `RdpSettings` y `FolderSettings`, columna en `ConnectionRepository` y `FolderRepository`,
campo y `MostrarHeredadoValor` en los dos editores. Multimonitor y captura no aplican; nada acá es de
momento-no-heredable, así que todos entran al patrón.

## Risks / Trade-offs

- [La versión del control no expone `IMsRdpExtendedSettings`] → El ajuste cae en
  `PropiedadesNoAceptadas` y la sesión conecta igual; el editor puede advertir que la escala no tuvo
  efecto en esa máquina. No rompe nada.
- [El desdoble de `FitToTab` toca una casilla existente] → Riesgo de cambiar la conducta de conexiones
  guardadas. Mitigación: el mapeo de compatibilidad (`true`→`EscalarPixeles`) preserva exactamente lo
  que hacían, y hay un escenario de migración que lo fija.
- [RemoteApp depende de configuración del servidor que no controlamos] → Se acota a encender la
  bandera y traducir el fallo a una causa clara; no se promete que abra si el admin no lo publicó.
- [`UpdateSessionDisplaySettings` contra servidor RDP < 8.1] → La llamada que falla se traga sin
  degradar la sesión (ya hay `try/catch` en `Resize`); sólo no renegocia.
- [Impuesto de plomería: cada campo heredable toca ~6 archivos] → Es copiar un patrón existente, no
  inventarlo; se agrupa por lotes para que cada uno compile y pruebe entero.

## Migration Plan

Columnas nuevas en las tablas de conexión y carpeta, nulas por omisión (heredar / valor del control).
Sin backfill: una conexión sin valor se comporta como hoy. El único cambio de lectura es el mapeo de
`FitToTab` al enum de modo de tamaño. Rollback: las columnas nuevas se ignoran si se vuelve a una
versión anterior; no hay pérdida de datos existentes.

## Resolved Questions

- **Escala: lista fija.** El control sólo acepta un conjunto discreto; un campo libre validaría contra
  ese mismo conjunto y un valor inválido lo descarta el control en silencio. Escritorio en
  100/125/150/175/200; dispositivo en {100, 140, 180}. Sin entrada libre.
- **Tipo de red: elección explícita.** Deducirlo de la latencia medida agregaría un mecanismo de
  medición y un blanco móvil para un valor que sólo ajusta heurísticas del control. Se elige a mano,
  como todo ajuste RDP, y es heredable.
- **RemoteApp: se queda en este cambio, Lote 3.** Ya está aislado —Lotes 1 y 2 no dependen de él— y
  no amerita un cambio propio pese a depender del servidor y usar `RemoteProgram2`. Si complica la UI
  del editor, se puede diferir sin tocar los otros lotes.
