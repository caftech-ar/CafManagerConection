# Investigación técnica: CafManagerConection (CMC)

**Feature**: `001-rdp-ssh-server-manager` · **Fecha**: 2026-08-24 · **Fase**: 0 ·
**Reconciliado**: 2026-08-25

Este documento resuelve las incógnitas técnicas del plan antes de pasar al diseño. Cada
entrada registra la decisión tomada, por qué se tomó y qué alternativas se descartaron.

> **Tres decisiones de este documento se revirtieron durante la implementación.** Están
> marcadas con un bloque **Revisión 2026-08-25** en su sección: §2 (emulador VT), §7 (tema)
> y §8 (iconografía). El razonamiento original se conserva a propósito —muestra qué se
> sabía en su momento— pero la decisión vigente es la de la revisión.

Resumen de los tres hallazgos que cambiaban el plan respecto de la propuesta inicial:

1. **VtNetCore está abandonado**: su paquete NuGet fue retirado del listado, no tiene dueño
   y su última publicación es de julio de 2019. No se puede depender de él como paquete.
   Se decidió incorporar su código fuente al repositorio (permitido por su licencia MIT).
   **Revertido**: se escribió un emulador propio. Ver §2.
2. **El interop ActiveX de RDP tiene una regresión conocida en .NET 8 y posteriores**: desde
   .NET 8, `AxHost.Dispose()` ya no libera de forma determinística el control COM. Con
   pestañas que se abren y cierran repetidamente esto filtra recursos, así que el ciclo de
   vida debe manejarse de forma explícita y verificarse con una prueba de estrés.
3. **`ShellStream.ChangeWindowSize` existe en SSH.NET**: el requisito de propagar el
   redimensionamiento al servidor (FR-033) no necesita ningún fork ni parche.

---

## 1. Hospedaje del cliente RDP de Windows

**Decisión**: hospedar el control ActiveX `mstscax.dll` en WinForms mediante una clase
propia derivada de `System.Windows.Forms.AxHost`, generando el interop con
`<COMReference WrapperTool="aximp">` en el `.csproj` estilo SDK. Toda la interacción con el
control queda encapsulada dentro de `CafManagerConection.Rdp`; ningún otro proyecto
referencia tipos COM.

**Rationale**:

- `AxHost` es el mecanismo soportado por WinForms para alojar un control ActiveX, y los
  proyectos SDK-style de .NET admiten `COMReference` con `WrapperTool="aximp"` desde
  .NET Core 3.0, lo que evita depender del `AxImp.exe` del SDK de .NET Framework como paso
  manual previo a la compilación.
- Es el mismo enfoque que usa un producto comercial en producción: el proyecto MsRdpEx de
  Devolutions incluye un `RdpAxHost` escrito a mano sobre `AxHost`, lo que confirma que el
  patrón es viable y que existe un plan B si la generación automática del wrapper falla.
- Encapsular el COM en un solo proyecto satisface el Principio I: `Domain` y `Application`
  hablan con una interfaz `IRdpSession`, no con `IMsRdpClient`.

**Riesgo identificado y mitigación**: el issue `dotnet/winforms#12056` documenta que a
partir de .NET 8 el `Dispose()` de `AxHost` pasó de `Marshal.FinalReleaseComObject` a
`Marshal.ReleaseComObject`, por lo que el destructor del OCX ya no se invoca. En una
aplicación que abre y cierra decenas de pestañas RDP por jornada esto acumula objetos COM
vivos. Mitigación obligatoria:

- Implementar el cierre de una sesión RDP como una secuencia explícita: desconectar el
  control, desuscribir todos sus manejadores de eventos, quitarlo de su contenedor,
  `Dispose()` y liberación explícita de las referencias COM retenidas.
- Incluir en la validación una prueba de estrés que abra y cierre 50 sesiones RDP
  consecutivas verificando que los handles de usuario/GDI y la memoria del proceso vuelvan
  a su línea base. Esta prueba condiciona SC-003 y SC-012.

**Alternativas consideradas**:

- **`AxImp.exe` como paso previo manual**: descartada por requerir el SDK de .NET Framework
  instalado en cada máquina de compilación y por dejar binarios generados fuera del control
  del proyecto. Queda como plan B.
- **`AxHost` escrito a mano** (enfoque de MsRdpEx): descartada como opción inicial por el
  volumen de código de interop a mantener. Es el plan B si `COMReference` no produce un
  wrapper utilizable para `MsRdpClient11`.
- **FreeRDP u otra implementación del protocolo**: descartada por decisión explícita del
  usuario y por el Principio IV.
- **Lanzar `mstsc.exe` y reparentar su ventana**: descartada porque impide controlar el
  estado de la conexión, capturar errores con precisión y desactivar las redirecciones
  exigidas por FR-017.

---

## 2. Emulación de terminal VT

**Decisión**: incorporar el código fuente de VtNetCore al repositorio, bajo
`src/CafManagerConection.Terminal/Vt/`, conservando su licencia MIT y su aviso de copyright,
en lugar de referenciar el paquete NuGet. Se toma como base la variante mantenida por
BastionZero (`VtNetCorePatched`), que corrige defectos sobre el original.

**Rationale**:

- El paquete `VtNetCore` en NuGet está **sin listar**, declara explícitamente que **no tiene
  dueño y no recibe mantenimiento**, y su última versión (1.0.30) es de **julio de 2019**.
  Un paquete retirado puede dejar de resolverse en cualquier momento y no admite parches:
  depender de él pondría la funcionalidad central del producto sobre una base que no
  controlamos.
- Su licencia MIT permite explícitamente la incorporación del código fuente.
- El terminal es la parte de mayor riesgo del producto y la que más defectos de detalle va a
  requerir corregir (secuencias raras, anchos de carácter, modos de teclado). Tener el
  código en el repositorio convierte cada defecto en un arreglo local en lugar de un
  bloqueo.
- El código apunta a .NET Standard 2.0, compatible con .NET 10, y no arrastra dependencias
  de UI: encaja en un proyecto que no viola el Principio I.

**Costo aceptado**: el proyecto asume el mantenimiento de un cuerpo de código de terceros.
Se acota exigiendo que el código incorporado quede aislado en su propia carpeta, sin
mezclarse con código propio, y que toda corrección se documente.

**Alternativas consideradas**:

- **Referenciar el paquete NuGet sin listar**: descartada por el riesgo de indisponibilidad
  y por la imposibilidad de parchear.
- **Escribir un emulador VT propio desde cero**: descartada. Implementar VT100 más las
  extensiones XTerm de forma correcta es un proyecto en sí mismo y contradice el
  Principio V.
- **Usar un componente de terminal comercial**: prohibido por el Principio IV.
- **Empotrar Windows Terminal o `conpty`**: descartada; `conpty` sirve para procesos locales,
  no para una sesión SSH remota, y no resuelve la emulación del lado del cliente.

### Revisión 2026-08-25 — decisión revertida

**Decisión vigente**: emulador VT **propio**, en `src/CafManagerConection.Terminal/VtEmulator.cs`.
No se incorpora VtNetCore ni ningún código de terceros.

**Qué cambió el razonamiento**: la alternativa «escribir un emulador propio» se había
descartado por Principio V, midiéndola contra *implementar VT100 más XTerm de forma
completa*. Esa comparación estaba mal planteada. La aplicación no necesita un emulador
completo: necesita el subconjunto que usan `vim`, `htop`, `tmux` y `less`.

Medido contra ese alcance real, la cuenta se da vuelta. Incorporar VtNetCore significaba
adoptar unas 30.000 líneas ajenas, sin mantenimiento desde 2019, **y parchearlas igual**
—que era el motivo declarado para incorporarlas—. El emulador propio es menos código, es
código que se entiende, y tiene **46 pruebas** contra capturas reales de esas cuatro
herramientas.

**Costo que desaparece**: ya no se asume el mantenimiento de un cuerpo de código de terceros
ni hace falta aislarlo en su propia carpeta con su aviso de copyright.

**Lo que hay que vigilar**: una secuencia de escape que el emulador no maneje se ve como
basura en pantalla. La mitigación es la misma que antes —corregir y agregar la captura como
fixture—, sólo que ahora sobre código propio.

---

## 3. Renderizado del terminal en WinForms

> **Nota 2026-08-25**: sigue vigente. El control de terminal es de WinForms y se dibuja a
> mano, pero desde la migración va **hospedado en WPF** dentro de un `WindowsFormsHost`,
> junto con el ActiveX de RDP. Son las dos únicas piezas por las que se conserva WinForms.
>
> Un detalle que costó encontrar: `WindowsFormsHost` fija el tamaño de su hijo por su cuenta,
> así que la **distribución interna de un panel intermedio de WinForms no se aplica**. El
> margen alrededor del terminal se probó con 48 px de `Padding` en un panel contenedor y no
> movió un píxel; hay que ponerlo como `Margin` del host, del lado de WPF.

**Decisión**: control propio derivado de `Control`, con `DoubleBuffered` activo, que dibuja
en `OnPaint` con GDI+ usando `TextRenderer` (GDI) para el texto, una fuente monoespaciada
con métricas medidas una sola vez por tamaño de fuente, y repintado por regiones sucias en
lugar de repintar la pantalla completa en cada actualización.

**Rationale**:

- `TextRenderer` (GDI) da un espaciado de celda estable y predecible, que es lo que necesita
  una grilla de terminal; `Graphics.DrawString` (GDI+) aplica un espaciado propio que
  desalinea las columnas.
- Dibujar por celdas y agrupar tramos contiguos que comparten color de frente, color de
  fondo y atributos reduce drásticamente la cantidad de llamadas de dibujo, que es lo que
  determina la fluidez con `top` o `htop` refrescando.
- El repintado por regiones sucias es lo que permite sostener las 8 sesiones simultáneas de
  SC-003 sin saturar la CPU.

**Puntos que la prueba de concepto debe medir**: fluidez con `htop` refrescando una vez por
segundo a pantalla completa, ancho correcto de los caracteres de doble ancho (CJK) y de los
emojis, y comportamiento del cursor sobre caracteres combinantes.

**Alternativas consideradas**:

- **Direct2D/DirectWrite**: descartada para la versión 1 por complejidad e interop
  adicional. Es la ruta de escape si GDI no alcanza el rendimiento objetivo.
- **`RichTextBox` o `TextBox`**: descartada; no permiten control de celda, cursor ni
  atributos por carácter.
- **`DataGridView`**: descartada por rendimiento y por no ser un modelo de celda de texto.

---

## 4. Transporte SSH

**Decisión**: SSH.NET, versión `2026.0.0`. La sesión se implementa con `SshClient` +
`ShellStream`; el redimensionamiento usa `ShellStream.ChangeWindowSize(columns, rows, width,
height)`; la verificación del host usa el evento `HostKeyReceived` marcando la conexión como
no confiable hasta que el usuario acepta el fingerprint; el keep-alive usa
`SshClient.KeepAliveInterval`.

**Rationale**:

- La versión `2026.0.0` fue publicada en agosto de 2026 y declara soporte para .NET 8 y
  superiores, de modo que la biblioteca está activa y es compatible con .NET 10. Es lo
  opuesto al caso de VtNetCore.
- `ChangeWindowSize` resuelve FR-033 sin fork ni parches: era la única duda abierta que
  podía obligar a mantener una versión propia de la biblioteca.
- `HostKeyReceived` permite decidir la confianza **antes** de que se envíe ninguna
  credencial, que es exactamente lo que exigen FR-022 y FR-023.
- Licencia MIT, conforme al Principio IV.

**Decisiones de detalle**:

- La lectura del `ShellStream` se hace en una tarea en segundo plano; los bytes recibidos se
  entregan al emulador VT y el repintado se despacha al hilo de interfaz. Ninguna operación
  de red ocurre en el hilo de interfaz.
- El fingerprint se persiste en formato legible y estable (`SHA256:` en Base64, el mismo
  formato que muestra OpenSSH) para que el administrador pueda compararlo a ojo contra el
  que le informa el servidor.
- La desconexión ordenada cierra el `ShellStream` antes que el `SshClient`, y la tarea de
  lectura se cancela con un `CancellationToken` para no quedar bloqueada en el `Read`.

**Alternativas consideradas**:

- **Implementar SSH propio**: descartada explícitamente por el usuario y por el Principio V.
- **Depender de PuTTY/plink**: descartada; obligaría a un binario externo y a manejar la
  sesión por tubería, perdiendo el control del estado de la conexión.

---

## 5. Almacenamiento de credenciales

**Decisión**: acceso directo a Windows Credential Manager por P/Invoke sobre `CredWriteW`,
`CredReadW`, `CredDeleteW` y `CredFree`, usando credenciales de tipo genérico
(`CRED_TYPE_GENERIC`) con persistencia local de la máquina para el usuario actual
(`CRED_PERSIST_LOCAL_MACHINE`). La clave (`TargetName`) sigue el formato
`cmc:<protocolo>:<id-de-conexión>`.

**Rationale**:

- Es lo que fija el Principio II y evita cifrado propio, que sería la peor decisión posible
  en este dominio.
- Usar el identificador estable de la conexión —y no su nombre— en la clave evita que
  renombrar una conexión huérfane su credencial.
- Los datos se reparten según FR-036: el usuario va en `UserName` y el secreto en
  `CredentialBlob`. Para SSH con clave privada, la ruta del archivo se guarda en SQLite
  (no es un secreto) y la passphrase en el blob.

**Restricción a respetar**: el `CredentialBlob` tiene un límite de 2560 bytes (5 × 512). Las
contraseñas y passphrases están holgadamente por debajo, pero el límite prohíbe guardar allí
el **contenido** de una clave privada. Esto refuerza la decisión ya tomada de guardar solo
la **ruta** de la clave.

**Decisiones de detalle**:

- El blob se limpia de memoria (`Array.Clear`) apenas se consume, y las cadenas de secreto
  no se retienen en campos de larga vida.
- Toda memoria devuelta por `CredReadW` se libera con `CredFree` en un `finally`.
- Una credencial ausente no es una excepción inesperada: es un resultado previsto que
  dispara el flujo de FR-039.

**Alternativas consideradas**:

- **DPAPI (`ProtectedData`) sobre un archivo propio**: descartada; obliga a inventar el
  formato de almacenamiento y deja los secretos en un archivo del que somos responsables.
- **Cifrado propio con contraseña maestra**: descartada por el Principio II y porque
  agregaría una superficie criptográfica que no sabemos auditar.
- **Un paquete de terceros que envuelva el Credential Manager**: descartada por el
  Principio V; son cuatro funciones y la dependencia no se justifica.

---

## 6. Persistencia local

**Decisión**: SQLite mediante `Microsoft.Data.Sqlite` y Dapper, con el archivo en
`%LocalAppData%\CafManagerConection\cmc.db`. El versionado de esquema usa el pragma
`user_version` y un conjunto de migraciones incrementales aplicadas en el arranque dentro de
una transacción.

**Rationale**:

- `user_version` es un entero que vive dentro del propio archivo de base: no hace falta una
  tabla de control ni un motor de migraciones externo, lo que respeta el Principio V.
- Aplicar las migraciones dentro de una transacción evita dejar la base a medio migrar si el
  proceso muere en el medio.
- Dapper mantiene el acceso a datos como SQL explícito y legible, sin el peso de un ORM
  completo para seis tablas.

**Decisiones de detalle**:

- `PRAGMA foreign_keys = ON` en cada conexión: SQLite las desactiva por omisión, y de ellas
  depende que borrar una carpeta arrastre lo que contiene.
- `journal_mode = WAL`, que reduce el bloqueo entre la escritura del historial de conexiones
  y la lectura del árbol.
- Las fechas se guardan en UTC en formato ISO-8601 y se muestran en hora local.
- El arranque detecta una base ilegible o corrupta, la renombra a `cmc.db.corrupta-<sello>`
  y crea una nueva, informando al usuario: es lo que exige FR-052 y evita destruir datos que
  podrían recuperarse.

**Alternativas consideradas**:

- **Entity Framework Core**: descartada por peso y por arrastrar un modelo de migraciones
  que excede lo necesario.
- **Archivos JSON como almacén principal**: descartados; no dan consultas ni integridad
  referencial, y el historial de conexiones crece.
- **`connection_history` como archivo aparte**: descartada; complica el borrado en cascada
  sin beneficio.

---

## 7. Tema claro y oscuro

**Decisión**: usar el modo de color nativo llamando a `Application.SetColorMode(...)` en el
arranque, antes de crear cualquier control, y complementarlo con dibujo propio
(owner-drawn) en los controles cuya apariencia el modo nativo no cubre.

**Rationale**:

- En .NET 10 esta API dejó de ser experimental, y admite modo claro, oscuro o seguir la
  configuración de Windows.
- La aplicación apunta a Windows 11, que es donde el modo oscuro nativo funciona.

**Limitaciones conocidas que el diseño debe absorber**:

- El modo de color debe fijarse **antes** de instanciar cualquier control; la secuencia de
  arranque tiene que respetarlo.
- No todos los controles responden al modo oscuro: los `MessageBox` del sistema siguen
  mostrándose en claro. Los diálogos propios de la aplicación (confirmaciones de borrado,
  advertencias de fingerprint) se implementan como formularios propios y no como
  `MessageBox`, para que el tema sea consistente.
- El tema del terminal se define en la paleta del control de terminal, no lo hereda del
  sistema: hay que mapear explícitamente los 16 colores ANSI base a una paleta clara y una
  oscura.

**Alternativas consideradas**:

- **Dibujar todo por cuenta propia**: descartada por volumen de trabajo.
- **Una biblioteca de temas de terceros**: prohibida por el Principio IV.

### Revisión 2026-08-25 — decisión revertida

**Decisión vigente**: dos diccionarios de recursos XAML, `Paleta.Claro.xaml` y
`Paleta.Oscuro.xaml`. Cambiar de tema reemplaza **un** diccionario; los estilos referencian
los pinceles con `DynamicResource` y WPF vuelve a resolverlos solo.

**Qué cambió el razonamiento**: `Application.SetColorMode` desapareció con WinForms. Pero el
motivo real para abandonar ese enfoque fue de rendimiento, no de migración: con el modo
nativo más dibujo propio, cambiar de tema obligaba a repintar todos los elementos del árbol
uno por uno y se **veía** lento. Con diccionarios de recursos no hay que recorrer nada, y las
sesiones abiertas ni se enteran.

**Limitaciones que siguen valiendo**: el tema del terminal no lo hereda del sistema —los 16
colores ANSI se mapean explícitamente— y va **siempre oscuro**, sin seguir el tema de la
aplicación, porque los esquemas de color de `vim`, `htop` y casi cualquier CLI están
calibrados para fondo oscuro. Es lo mismo que hacen Windows Terminal y VS Code.

**Limitación nueva, específica de WPF**: las propiedades de rasterizado de texto
(`TextOptions`) se heredan por el árbol **visual**, y un `Popup` —menú contextual, tooltip,
desplegable— es otra raíz visual. Hay que fijarlas en cada uno además de en la ventana, o su
texto sale más fino que el resto de la interfaz.

---

## 8. Iconografía

**Decisión**: incorporar al repositorio únicamente los iconos de Fluent UI System Icons que
la interfaz usa, en formato PNG a las escalas 100 %, 125 %, 150 % y 200 %, como recursos
incrustados, seleccionando la escala según el DPI de la ventana.

**Rationale**:

- WinForms no dibuja SVG de forma nativa, y agregar un renderizador de SVG sería una
  dependencia nueva que el Principio V no justifica para una veintena de iconos.
- Pre-generar las escalas evita el escalado en tiempo de ejecución, que es lo que produce
  iconos borrosos en pantallas de alto DPI.
- Licencia MIT, conforme al Principio IV.

**Alternativas consideradas**:

- **Una fuente de iconos**: descartada; complica el alineado y el color por icono.
- **Convertir SVG en tiempo de ejecución**: descartada por la dependencia adicional.

### Revisión 2026-08-25 — decisión revertida

**Decisión vigente**: los datos del `path` de cada SVG de Fluent se copian a un
`StreamGeometry` en `Themes/Estilos.xaml`. Sin PNG, sin escalas, sin selector por DPI y sin
dependencia en tiempo de ejecución.

**Qué cambió el razonamiento**: la premisa era «WinForms no dibuja SVG de forma nativa». WPF
dibuja geometrías vectoriales de forma nativa, así que toda la maquinaria de escalas
pre-generadas dejó de tener sentido. Son ocho iconos: copiar ocho cadenas de `path` es más
barato que mantener treinta y dos archivos PNG.

**Tres cosas que hay que saber para agregar un icono**:

- **Usar la familia de 20 px, estilo `filled`.** Fluent **redibuja** cada tamaño en lugar de
  escalarlo: los de 16 tienen menos detalle y otro peso óptico, y mezclar tamaños deja
  grosores distintos en la misma columna.
- **Anteponer `F1` a los datos.** SVG rellena con la regla *nonzero* y `PathGeometry` de WPF
  usa *EvenOdd* por omisión; sin ese prefijo, las partes macizas que se solapan salen como
  agujeros.
- **Mirar el icono a 16 px antes de aceptarlo.** El glifo obvio no siempre sobrevive: para
  RDP se descartó `desktop` —a 16 px su relleno macizo se lee como una pila o un teléfono, y
  pesaba el doble que los demás— y se usa `view_desktop`, que tiene el marco hueco.

El icono de la aplicación (`Assets/cmc.ico`, ocho tamaños de 16 a 256) se genera del mismo
modo, componiendo el glifo `window_console` sobre un cuadrado redondeado azul.

---

## 9. Registro de eventos

**Decisión**: Serilog con `Serilog.Sinks.File`, archivos rotativos por día en
`%LocalAppData%\CafManagerConection\logs\`, con retención acotada, y un conjunto reducido de
métodos de registro propios que son la **única** vía por la que la aplicación escribe logs.

**Rationale**:

- El Principio II prohíbe registrar secretos y contenido de sesión. La forma de garantizarlo
  no es la disciplina de quien escribe cada línea, sino que no exista una forma fácil de
  registrar un objeto entero.
- Concentrar el registro en pocos métodos con parámetros explícitos (identificador de
  conexión, host, usuario, resultado) hace que sea revisable: alcanza con auditar esos
  métodos para saber qué puede llegar al archivo.

**Decisiones de detalle**:

- Prohibido registrar objetos de dominio completos con destructuring: los tipos de
  configuración de conexión y de credencial nunca se pasan enteros al registrador.
- Los tipos que contienen secretos sobrescriben `ToString()` para devolver un marcador
  redactado, de modo que un registro accidental no filtre nada.
- El registro de la sesión SSH consigna únicamente eventos de conexión, jamás el flujo de
  datos del terminal.

**Alternativa considerada**: `Microsoft.Extensions.Logging` a secas, descartada porque el
sink de archivo con rotación y retención exige de todos modos un proveedor adicional.

---

## 10. Distribución

**Decisión**: `dotnet publish -c Release -r win-x64 --self-contained true`, sin
`PublishSingleFile`, empaquetado como ZIP portable.

**Rationale**:

- Self-contained cumple SC-011: el equipo destino no necesita instalar nada.
- **No** usar archivo único es deliberado: el interop COM y la carga de `mstscax.dll`
  conviven mal con la extracción a un directorio temporal que hace el modo de archivo único,
  y depurar un fallo de activación COM en ese escenario es costoso. Una carpeta con las
  dependencias visibles es más simple y cumple igual el requisito.
- **No** usar recorte (`PublishTrimmed`) es igualmente deliberado: WinForms y el interop COM
  dependen de reflexión, y el recorte introduce fallos que solo aparecen en ejecución.

**Consecuencia aceptada**: el paquete resultante pesa más que una aplicación recortada. Es
un intercambio consciente: previsibilidad sobre tamaño.

**Alternativa considerada**: publicación dependiente del framework, descartada porque
obligaría a instalar .NET en cada servidor de trabajo, incumpliendo SC-011.

---

## 11. Estrategia de pruebas

**Decisión**: la validación se reparte en cuatro niveles, según lo que fija el Principio III.

| Nivel | Qué cubre | Cómo |
| --- | --- | --- |
| Unitario | `Domain` y `Application` | xUnit + NSubstitute, sin tocar red, disco ni UI |
| Integración de datos | `Infrastructure` | base SQLite temporal creada y destruida por cada prueba |
| Integración SSH | `Ssh` | contenedor OpenSSH dedicado, con usuarios de prueba y claves generadas al vuelo |
| Terminal | `Terminal` | fixtures de secuencias ANSI/VT guardadas como archivos, verificando el estado final del búfer |

**Sobre los fixtures del terminal**: se capturan de sesiones reales ejecutando cada programa
objetivo (`vim`, `nano`, `top`, `htop`, `less`, `tmux`) y se guardan como flujos de bytes.
Cada prueba alimenta el emulador con el flujo y compara el búfer resultante —texto,
atributos y posición del cursor— contra un estado esperado. Esto vuelve reproducible en
milisegundos lo que de otro modo exigiría un servidor y un par de ojos.

**Sobre la UI**: se valida manualmente en esta versión, que es la excepción declarada por el
Principio III. El guion de validación manual es `quickstart.md` y cubre cada criterio de
éxito que no se puede automatizar.

**Alternativa considerada**: automatización de UI con WinAppDriver o similar. Descartada
para la versión 1 por costo de puesta a punto y fragilidad, no por falta de valor.

---

## 12. Concurrencia y modelo de hilos

**Decisión**: cada sesión (RDP o SSH) es propietaria de sus recursos y se comunica con la
interfaz mediante eventos despachados al hilo de interfaz. Ninguna operación de red se
ejecuta en el hilo de interfaz, y ninguna estructura de la interfaz se toca desde un hilo de
segundo plano.

**Rationale**:

- El control ActiveX de RDP tiene afinidad de hilo: debe crearse, usarse y destruirse en el
  hilo de interfaz. Su conexión es asíncrona por diseño y notifica el resultado por eventos.
- La sesión SSH sí necesita un hilo propio para leer el flujo de datos de forma continua.
- Aislar cada sesión es lo que hace verdadero SC-012: el fallo de una no puede tumbar a las
  demás. Cada tarea de sesión captura sus excepciones y las convierte en un cambio de estado
  de esa sesión.

**Decisiones de detalle**:

- Las excepciones de una tarea de sesión nunca escalan como excepciones no observadas: se
  traducen a un estado `Error` con su motivo.
- El cierre de la aplicación cancela todas las tareas de sesión y espera un plazo acotado
  antes de forzar el cierre, para no colgar el proceso.

---

## Puntos abiertos que resuelve la prueba de concepto

Ninguno de estos bloquea el diseño, pero todos deben verificarse con código antes de
comprometerse con el resto de la implementación. Corresponden al cambio técnico que el
usuario propuso llamar `validate-ssh-terminal-stack`.

1. Fluidez del renderizado GDI con `htop` a pantalla completa refrescando continuamente.
2. Corrección del ancho de celda para caracteres de doble ancho y emojis.
3. Comportamiento de `tmux` con sus secuencias de teclado y sus modos alternativos de
   pantalla.
4. Fidelidad del interop `COMReference`/`aximp` para `MsRdpClient11` y ausencia de fugas al
   abrir y cerrar 50 sesiones.
5. Calidad real del código incorporado de VtNetCore frente a los seis programas objetivo, y
   cuántas correcciones exige.

---

## Fuentes

- [Aximp.exe (Windows Forms ActiveX Control Importer)](https://learn.microsoft.com/en-us/dotnet/framework/tools/aximp-exe-windows-forms-activex-control-importer)
- [Considerations When Hosting an ActiveX Control on a Windows Form](https://learn.microsoft.com/en-us/dotnet/desktop/winforms/controls/considerations-when-hosting-an-activex-control-on-a-windows-form)
- [dotnet/winforms#12056 — .NET 8 upgrade results in missing destructor call of ActiveX control](https://github.com/dotnet/winforms/issues/12056)
- [Devolutions/MsRdpEx — RdpAxHost.cs](https://github.com/Devolutions/MsRdpEx/blob/master/dotnet/AxInterop.MSTSCLib/RdpAxHost.cs)
- [VtNetCore en NuGet (sin listar, sin mantenimiento)](https://www.nuget.org/packages/VtNetCore)
- [darrenstarr/VtNetCore](https://github.com/darrenstarr/VtNetCore)
- [bastionzero/VtNetCorePatched](https://github.com/bastionzero/VtNetCorePatched)
- [SSH.NET en NuGet](https://www.nuget.org/packages/SSH.NET)
- [SSH.NET — clase ShellStream](https://sshnet.github.io/SSH.NET/api/Renci.SshNet.ShellStream.html)
- [sshnet/SSH.NET#40 — Terminal window resizing](https://github.com/sshnet/SSH.NET/issues/40)
- [Application.SetColorMode(SystemColorMode)](https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.application.setcolormode?view=windowsdesktop-10.0)
- [What's new in WinForms for .NET 10](https://learn.microsoft.com/en-us/dotnet/desktop/winforms/whats-new/net100)
