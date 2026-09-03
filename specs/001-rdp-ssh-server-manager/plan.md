# Implementation Plan: CafManagerConection (CMC)

**Branch**: `001-rdp-ssh-server-manager` | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/001-rdp-ssh-server-manager/spec.md`

## Summary

CMC es una aplicación de escritorio para Windows 11 que reúne en una sola ventana con
pestañas las sesiones RDP y SSH que hoy el administrador abre con herramientas separadas.
Guarda los servidores organizados en carpetas, deja las credenciales en Windows Credential
Manager y la configuración en SQLite.

El enfoque técnico es una arquitectura por capas con el dominio aislado (Principio I): la
interfaz y los adaptadores de protocolo se conectan al núcleo por interfaces, de modo que la
lógica de conexiones, carpetas y credenciales se prueba sin abrir una ventana ni un socket.

La interfaz es **WPF con plantillas XAML propias**. RDP se resuelve
hospedando el control ActiveX de Windows dentro de un `AxHost`, y el terminal es un control
de WinForms dibujado a mano; los dos van dentro de un `WindowsFormsHost`. Son las **dos
únicas** piezas por las que se conserva WinForms.

### Tres elecciones que el código no explica solo

**1. WPF, y no WinForms.** Con WinForms el cambio de tema repinta cada fila del árbol control
por control, y los controles delatan su origen aunque se dibujen a mano. El ActiveX de RDP se
hospeda en WPF sin problemas, que era el único argumento en contra.

**2. Emulador VT propio, y no VtNetCore.** `VtEmulator.cs` cubre lo que la aplicación usa
—`vim`, `htop`, `tmux`, `less`— con **46 pruebas** contra capturas reales. VtNetCore son 30.000
líneas de un proyecto sin mantenimiento desde julio de 2019, que habría que parchear igual.

**3. Iconos vectoriales, y no mapas de bits por DPI.** Los datos del `path` de cada SVG están
copiados a geometrías XAML: nítidas a cualquier escala, sin selector por DPI, sin archivos y sin
dependencia en tiempo de ejecución.

## Technical Context

**Language/Version**: C# 14 sobre .NET 10 (SDK 10.0.301), `net10.0-windows`

**Primary Dependencies**: SSH.NET (MIT) · `Microsoft.Data.Sqlite` (MIT) · Dapper
(Apache 2.0) · Serilog + `Serilog.Sinks.File` (MIT) · `System.Text.Json` · **OxyPlot.Wpf 2.2.0
(MIT)** · Fluent UI System Icons (MIT, **copiado como geometrías**, no como paquete) · control
ActiveX `mstscax.dll` y Windows Credential Manager, ambos componentes del sistema operativo

**Justificación de OxyPlot.Wpf** (la exige el Principio IV para toda dependencia nueva): el panel
de estado dibujaba las series con `Polyline` y `ProgressBar`, que es lo que WPF da sin ayuda. Eso
alcanza para una raya de tendencia y no para lo que se pide ahora —ejes con tiempo, varias series
comparables, valor bajo el cursor—, y la alternativa sería escribir código de dibujado, que el
mismo Principio IV pone como último recurso.

Se eligió sobre las otras dos candidatas por lo que **no** trae:

| | Licencia | Dependencias | Binarios nativos | Peso |
| --- | --- | --- | --- | --- |
| **OxyPlot.Wpf** | MIT | ninguna | **ninguno** | 796 KB |
| LiveCharts2 | MIT | SkiaSharp | uno por arquitectura | ~10 MB |
| ScottPlot 5 | MIT | SkiaSharp | uno por arquitectura | ~10 MB |

Los binarios nativos son lo que decide: el paquete portable del Principio VI es una carpeta que se
descomprime y corre, y arrastrar un motor de dibujado nativo por arquitectura contradice
directamente el «liviano» del Principio IV. OxyPlot dibuja con WPF, así que hereda el tema y el
escalado por DPI sin nada de por medio.

No es una biblioteca de estilos ni un editor de código ni un motor web: no cae en ninguna de las
tres prohibiciones del Principio IV, y por eso no hace falta enmienda.

No se depende de VtNetCore: el emulador VT es propio.

**Storage**: SQLite en `%LocalAppData%\CafManagerConection\cmc.db`, versionado con el pragma
`user_version`. Los secretos **no** se guardan acá: van a Windows Credential Manager

**Testing**: xUnit + NSubstitute + Coverlet. **1.724 pruebas en verde y 19 omitidas** en 9
proyectos: Infrastructure 337, Platform 336, Domain 329, Terminal 269, UseCases 176, Monitoring 114,
App 95, Ssh 54 (más 19 que exigen un servidor real), Rdp 14. Cobertura en `docs/cobertura.md`:
54,2 % total y 70-84 % en las capas de reglas. Interfaz por validación manual guiada ([quickstart.md](./quickstart.md)) y por
captura automatizada (`scripts/captura.ps1`, `scripts/ventanas.ps1`)

**Target Platform**: Windows 11 x64

**Project Type**: aplicación de escritorio, arquitectura por capas con adaptadores

**UI Stack**: WPF con `ControlTemplate`, `Trigger`, `DataTrigger` y
`HierarchicalDataTemplate` propios. Lenguaje visual de **shadcn/ui** sobre la escala `zinc`,
con primario azul, en tema claro y oscuro. El cambio de tema reemplaza un único diccionario
de recursos (`Paleta.Claro.xaml` ↔ `Paleta.Oscuro.xaml`); los estilos referencian los
pinceles con `DynamicResource`, así que WPF repinta solo, sin recorrer controles y sin cerrar
las sesiones abiertas. Tipografía: Segoe UI en la interfaz, Cascadia Mono con Consolas de
respaldo en el terminal

**Performance Goals**, medidos:

| Tramo | Objetivo | Medido |
| --- | --- | --- |
| Arranque a ventana utilizable | < 2 s | pendiente de medir (T243) |
| Saludo SSH (TCP + claves + auth) | — | 480–850 ms |
| Apertura del canal interactivo | — | 1750–2700 ms (**costo del servidor**, ver abajo) |
| Comando remoto sobre conexión establecida | — | 15–35 ms |
| Panel Docker, primer contenido en pantalla | — | ~220 ms |
| Memoria en reposo | < 150 MB | pendiente de medir (T243) |

El canal interactivo es el tramo más caro y **no es de la aplicación**: en el servidor de
referencia, los guiones de `/etc/update-motd.d/` corren un reporte de Docker Compose sobre 9
proyectos en cada inicio de sesión, y eso se paga entero antes de que el canal quede listo.
Queda registrado acá para que nadie lo persiga dentro del código.

**Instrumentación**: `IAppLogger.WorkCompleted(connectionId, RemoteWork,
elapsed)` cronometra cada tramo remoto de forma permanente. `RemoteWork` es un **enum
cerrado** y no una cadena libre, por el Principio II: un parámetro de texto abriría un canal
por donde podría filtrarse un comando, una ruta o una salida al archivo de log.

**Constraints**: sin privilegios de administrador · sin servicios de Windows · sin
dependencias comerciales ni componentes web · ningún secreto fuera de Windows Credential
Manager · ningún secreto ni contenido de sesión en los logs · distribución self-contained en
carpeta portable

**Scale/Scope**: un usuario por instalación · hasta cientos de conexiones · 8+ sesiones
simultáneas · 9 proyectos de producción y 8 de pruebas · **130 requisitos funcionales** y 21
criterios de éxito sobre 10 historias de usuario · 249 tareas

## Constitution Check

*GATE: debe pasar antes de la Fase 0 y revalidarse después de la Fase 1.*

Evaluado contra [`.specify/memory/constitution.md`](../../.specify/memory/constitution.md)
**v1.13.0**.

| Principio | Estado contra el código entregado |
| --- | --- |
| **I. Dominio aislado** | ✅ `Domain` no referencia WPF, WinForms, SQLite, Dapper ni SSH.NET. La migración de interfaz no lo tocó: es la prueba práctica de que el aislamiento servía |
| **II. Cero secretos fuera del Credential Manager** | ✅ `IAppLogger` sigue siendo una interfaz cerrada, ahora de once métodos. El método nuevo de medición toma un enum, no texto. Auditoría de secretos ejecutada: cero coincidencias |
| **III. Test-first en el núcleo** | ⚠️ **Con una desviación declarada** (`docs/revision-constitucional.md:41`). El ciclo de vida de sesiones está en `src/CafManagerConection.UseCases/Sessions/` —`SessionManager` con `OpenAsync`, `ReconnectAsync` y `CloseAsync`— con 49 pruebas en `tests/CafManagerConection.UseCases.Tests/Sessions/`. Lo que falta es el contrato: `ISessionManager` (`contracts/servicios-de-aplicacion.md:104`) no existe como interfaz en el código. Ver Deuda conocida |
| **IV. WPF y bibliotecas open source** | ✅ Todo el stack es MIT/Apache 2.0. La constitución se enmendó a WPF antes de migrar, como exige el Principio V. OxyPlot.Wpf se agrega con la justificación de arriba: MIT, sin dependencias y sin binarios nativos |
| **V. Simplicidad y alcance cerrado** | ⚠️ **Sin margen.** El alcance se duplicó respecto de la planificación inicial y ya se consumió el colchón. Las funciones nuevas que el usuario viene pidiendo deben evaluarse como feature separada |
| **VI. Distribución sin privilegios ni servicios** | ✅ ZIP portable self-contained de 76,9 MB, verificado en arranque. Datos y logs en `%LocalAppData%` |

**Validación manual declarada (Principio III, excepción de la capa `App`)**:

El Principio III exige que cada plan que toca la interfaz declare explícitamente su guion de
validación manual. Para la interacción del terminal —copiado, pegado, selección y teclas
(FR-030 a FR-030g, FR-032, FR-154, FR-155)— ese guion es el **Escenario 16** de
[quickstart.md](./quickstart.md), que verifica SC-022 gesto por gesto con PuTTY abierto al lado
contra el mismo servidor.

Lo que se puede probar sin ventana está probado y no entra en el guion: la decisión de qué hace
cada tecla y cada botón, el tramo de fila que abarca la selección y el armado del pegado son
funciones puras en `TerminalControl`, con pruebas en `Terminal.Tests`. Al guion manual queda sólo
lo que exige un servidor de verdad y un par de ojos.

Para los paneles remotos incorporados por la enmienda 1.11.0 —semáforo de métricas, visores con
color, ficha de contenedor ampliada, puertos y ficha de proceso (FR-087a a FR-087d, FR-100e a
FR-100g, FR-101a a FR-101c, FR-150b a FR-150e, FR-164 a FR-165e)— el guion son los **escenarios 17
a 19** de [quickstart.md](./quickstart.md).

El reparto es el mismo: lo que decide algo se prueba solo. `NivelDeUso` (Monitoring), `NivelDeLinea`,
`ResaltadorDeNginx`, `DetalleDeProceso` y el lector de PID de `PuertosParser` son funciones puras
con pruebas propias; la garantía de que el resaltado no altera el texto se prueba reconstruyendo el
archivo desde sus tramos, no mirándolo. A los ojos queda el color, el orden de la ficha y el
comportamiento contra un servidor con permisos parciales.

**Puertas de calidad**:

- *Puerta constitucional*: ✅ registrada en `docs/revision-constitucional.md`: pasa con dos
  desviaciones declaradas (T249).
- *Puerta de esquema*: ✅ las migraciones 001 a 005 están escritas y aplicadas
  (`src/CafManagerConection.Infrastructure/Database/Migrations/`). Ver
  [data-model.md](./data-model.md).
- *Puerta de secretos*: ✅ ejecutada, cero coincidencias.

**Resultado**: ⚠️ **Pasa con las dos desviaciones que declara
`docs/revision-constitucional.md`.**

## Project Structure

### Documentation (this feature)

```text
specs/001-rdp-ssh-server-manager/
├── plan.md                              # Este archivo
├── spec.md                              # Especificación funcional
├── research.md                          # Fase 0: decisiones técnicas
├── data-model.md                        # Fase 1: entidades y esquema
├── quickstart.md                        # Fase 1: guía de validación
├── contracts/                           # Fase 1: contratos entre módulos
│   ├── ports-de-sesion.md
│   ├── puertos-de-infraestructura.md
│   ├── puertos-de-plataforma.md
│   └── servicios-de-aplicacion.md
├── checklists/requirements.md
└── tasks.md
```

### Source Code (repository root)

```text
src/
├── CafManagerConection.App               # WPF. Único proyecto de interfaz
│   ├── Views/                           #   MainWindow, SessionView, y los diálogos
│   ├── Panels/                          #   paneles laterales: archivos, estado, túneles,
│   │                                    #   Docker, nginx, supervisord
│   ├── Themes/                          #   Estilos.xaml, Paleta.Claro/Oscuro.xaml,
│   │                                    #   Convertidores.cs, geometrías de iconos
│   ├── ViewModels/
│   ├── Services/                        #   Temas, portapapeles, diálogos, lanzador web,
│   │                                    #   título de la ventana, puerto local de un túnel
│   ├── Assets/                          #   cmc.ico (8 tamaños, generado de Fluent)
│   └── Bootstrap/                       #   App.xaml, CompositionRoot
│
├── CafManagerConection.Domain            # Sin dependencias. Ni una.
├── CafManagerConection.UseCases          # Orquestación. Depende solo de Domain
├── CafManagerConection.Infrastructure    # SQLite/Dapper, Credential Manager, Serilog
├── CafManagerConection.Rdp               # ActiveX de Windows sobre AxHost escrito a mano
├── CafManagerConection.Ssh               # SSH.NET: shell, comandos, SFTP, túneles
├── CafManagerConection.Terminal          # Emulador VT propio y control de terminal
│   ├── VtEmulator.cs                    #   propio, no VtNetCore
│   ├── TerminalBuffer.cs
│   ├── TerminalControl.cs               #   WinForms, dibujado a mano. Selección, teclado y mouse
│   ├── TerminalPalette.cs               #   esquema Campbell
│   ├── TerminalBusqueda.cs              #   búsqueda dentro del terminal (FR-144)
│   ├── MenuOscuro.cs                    #   dibujo del menú contextual con la paleta del terminal
│   └── KeyboardMapper.cs
├── CafManagerConection.Monitoring        # Métricas de Linux por /proc (US7)
│   └── NivelDeUso.cs                    #   tramos y umbrales del semáforo (FR-087a)
└── CafManagerConection.Platform          # Docker, nginx, supervisord, puertos; sólo lectura
    ├── PlatformInventory.cs             #   inventario, sin métodos que escriban
    ├── ControlDeDocker.cs               #   acciones de escritura, aparte y con confirmación
    ├── ControlDeSupervisor.cs           #   idem para supervisord
    ├── DetalleDeContenedor.cs           #   ficha de un contenedor (FR-150a a FR-150e)
    ├── ConsultorDeProcesos.cs           #   ficha de un proceso, sólo lectura (FR-165)
    ├── DetalleDeProceso.cs
    ├── NivelDeLinea.cs                  #   nivel de una línea de registro (FR-100f)
    ├── ResaltadorDeNginx.cs             #   tokenizador propio, sin dependencias (FR-101a)
    ├── AplicacionesConocidas.cs
    └── Parsers.cs

tests/                                    # nueve proyectos: uno por capa, más App.Tests (ver T386)
```

**Structure Decision**: `CafManagerConection.App` es el único proyecto de
interfaz. `Rdp` y `Terminal` también referencian WinForms porque producen controles nativos,
y eso no viola el Principio I: lo que el principio protege es `Domain`, que no depende de
ninguno de los dos.

Para que WPF y WinForms convivan sin ambigüedad de nombres, `App` declara
`<Using Remove="System.Windows.Forms" />` y `<Using Remove="System.Drawing" />`: sin eso,
`Path`, `Color` y `Brush` resuelven a dos tipos distintos y el error de compilación no dice
por qué.

## Complexity Tracking

> Desviaciones respecto de la alternativa más simple, según exige la Gobernanza.

| Violación | Por qué hace falta | Alternativa más simple, y por qué se descartó |
| --- | --- | --- |
| **Emulador VT propio** | Cubre sólo lo que la aplicación usa, con 46 pruebas contra capturas reales de `vim`, `htop`, `tmux` y `less` | *Incorporar VtNetCore*: descartada. Adoptar 30.000 líneas sin mantenimiento desde 2019, para tener que parchearlas igual, resultó más caro que escribir el subconjunto necesario |
| **9 proyectos de producción** | La separación la fija la constitución y aísla el dominio. Se validó en la práctica: la migración a WPF no tocó 6 de los 9 | *Un solo proyecto*: haría imposible verificar el Principio I — un `using` bastaría para que el dominio dependiera de la interfaz |
| **Patrón repositorio sobre SQLite** | Permite probar `UseCases` con dobles en lugar de una base real | *Acceso directo desde los servicios*: convertiría pruebas de milisegundos en pruebas de segundos y acoplaría el núcleo al motor |
| **Hasta tres conexiones SSH por servidor** | SSH.NET no expone SFTP sobre una sesión existente. Se mitiga abriendo las auxiliares a pedido | *Una sola sesión multiplexada*: exigiría forkear SSH.NET y mantener el fork |
| **`IAppLogger` cerrado** | El Principio II prohíbe secretos y contenido de sesión en el log. Once métodos con parámetros explícitos hacen la garantía **auditable** | *`ILogger<T>` genérico*: permite registrar cualquier objeto y traslada el cumplimiento a la disciplina de quien escribe cada línea |
| **`WindowsFormsHost` dentro de WPF** | El ActiveX de RDP y el control de terminal no tienen equivalente WPF | *Reescribir el terminal en WPF*: descartada, no aporta nada visible. *WinUI 3*: hospedar el ActiveX allí es notoriamente más difícil |
| **Iconos copiados en lugar de referenciados** | Son ocho geometrías. Copiar el `path` evita una dependencia entera | *Paquete NuGet*: no hay uno bueno para WPF, y traería miles de iconos para usar ocho |
| **Panel de puertos construido antes de tener requisito** | **Desviación ya ocurrida del Principio V**, no una decisión de diseño: el panel y `AplicacionesConocidas` se implementaron —con pruebas— sin una sola mención en `spec.md`, `plan.md` ni `tasks.md`. La enmienda 1.11.0 lo regularizó con FR-164 y siguientes, y US11 dice en su propio texto por qué llega última | *Retirar el panel*: se descartó porque responde una pregunta legítima y frecuente —qué tiene abierto el servidor y quién lo abrió— y sacarlo sería peor producto. Queda anotado acá y en la constitución: el registro es lo que evita que la próxima función entre por el mismo camino |
| **Dos caminos de dibujado en el visor** | El registro se pinta con el emulador VT propio —ya interpreta ANSI— y la configuración de nginx con texto WPF y tramos de color | *Todo por el terminal*: descartada porque el terminal corta las líneas al ancho de la grilla, y eso rompe SC-026, que exige que lo copiado sea idéntico carácter por carácter al archivo del servidor |

## Deuda conocida

**1. La copia de seguridad se hace después de migrar.** `App.xaml.cs:49` crea el `CompositionRoot`,
que aplica las migraciones pendientes; la copia se dispara desde `MainWindow`, que se crea en la
línea **51**. La copia diaria de FR-156 existe para poder volver atrás y no cubre lo único que no se
puede deshacer. Se deja así a propósito: la migración 006 es aditiva y no tiene nada que perder. Hay
que arreglarlo antes de la primera migración que reescriba o borre datos.

**2. `ISessionManager` no existe como interfaz.** El contrato está escrito en
`contracts/servicios-de-aplicacion.md:104`, pero el código tiene una clase concreta
(`src/CafManagerConection.UseCases/Sessions/SessionManager.cs`) que `MainWindow.xaml.cs:56`
instancia directamente. Mientras no haya interfaz, la ventana no se puede sustituir por un doble
en una prueba.

**3. La forma del icono no se elige.** El glifo sale del protocolo y `PaletaIconos.ClaveDeRecurso`
(`src/CafManagerConection.Domain/Settings/PaletaIconos.cs`) sólo cambia el sufijo de color del
recurso. Elegirlo es FR-195, FR-195a y FR-195c de
`specs/002-procesos-registros-y-arbol/spec.md`, en alcance por la enmienda 1.14.0. La columna que lo
persiste ya existe: `Migration006_Icono.cs` agrega `icon_key` a `connection_folders` y a
`connections`.

**4. El ActiveX de RDP filtra descriptores.** T058 mide 3 descriptores de USER por sesión, de forma
lineal, y no lo evita `Disconnect` ni `RequestClose`: son unas 3.300 conexiones antes de agotar la
cuota de 10.000 del proceso. La prueba de `tests/CafManagerConection.Rdp.Tests/CicloDeVidaTests.cs`
exige un presupuesto de 5 por sesión para cazar una regresión propia sin quedar en rojo permanente.

## Estado y siguiente paso

Las migraciones 001 a 005 están escritas y aplicadas, y las 34 fases de `tasks.md` están cerradas
salvo **diez tareas**, que se reparten en dos grupos.

**Ocho dependen del usuario**: hay que correrlas en su equipo o contra un servidor real, y no se
pueden cerrar desde acá.

- **T243** — el paquete portable en un Windows 11 **limpio**. Medido en la estación de desarrollo:
  76,9 MB de paquete, ventana visible en 1,26 s y 134,3 MB en reposo; los dos objetivos se cumplen.
  Falta el equipo limpio, que es lo que confirma que no falta ninguna dependencia.
- **T244** — la validación manual completa de `quickstart.md`.
- **T246** — comparar la interfaz junto a una aplicación nativa de Windows 11 (SC-015).
- **T336** — el instalador de punta a punta: instalar, abrir desde el menú Inicio, comprobar que la
  aplicación no corre elevada, desinstalar y confirmar que las conexiones siguen estando.
- **T417** — los tres síntomas de los paneles de plataforma, que sólo se ven con una sesión abierta.
- **T462**, **T476**, **T491** — SC-027 a SC-037 contra los tres servidores de referencia.

**Dos ya no son deuda de esta feature**: **T510** —el «top por CPU» ordena por el promedio de toda
la vida del proceso y no por el uso instantáneo— es **FR-173d** de
`specs/002-procesos-registros-y-arbol/spec.md`, y **T511** —los dos `ps -eo` y el `cat
/proc/diskstats` de cada muestra, 1 a 3 % de un núcleo con unos 700 procesos— es el experimento
**R3** de su fase 0, la tarea T602 de `specs/002-procesos-registros-y-arbol/tasks.md`.

**Lo que se pidió y no se construyó se movió a la feature 002**
(`specs/002-procesos-registros-y-arbol/`): panel de procesos, escalada con `sudo`, registros en
vivo, explorador SFTP en árbol, RDP con la identidad de Windows y en ventana propia, orden e iconos
del árbol, y la clave privada a WinSCP. Este documento ya no los planifica. La autorización de cada
grupo sale de tres lugares distintos —las enmiendas 1.13.0 y 1.14.0, la cláusula «colorear y ordenar
lo que ya se muestra» (`constitution.md:783`), y defectos de requisitos ya construidos—; el detalle
por grupo está en `spec.md`, sección «Movidos a la feature 002». Nada queda esperando autorización.

**Fuera de alcance, y por lo tanto fuera de las dos features**: gestión de claves SSH (generar,
importar, instalar la pública), métricas de datos transferidos y velocidad por sesión, recrear
contenedores o recargar la configuración de nginx, y firmar el instalador. Cada una requiere
enmienda constitucional previa (Principio V).

**Comando siguiente**: la feature 002. Su fase 0 son tres experimentos que hay que correr antes de
escribir código.
