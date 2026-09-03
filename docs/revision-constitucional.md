# Revisión de los seis principios contra el código entregado

**Fecha**: 2026-08-25 · **Constitución**: v1.6.0 · **397 pruebas en verde** · **T249**

Cada principio se evaluó contra el código, no contra la intención. Donde hay una desviación
está declarada, con su motivo y su costo.

---

## I. Dominio aislado de la infraestructura — ✅ cumple

`CafManagerConection.Domain` tiene **cero** referencias a paquetes y **cero** a proyectos.
Ningún archivo del dominio importa WPF, WinForms, SQLite, Dapper ni SSH.NET.

Este principio se puso a prueba de verdad durante la migración de WinForms a WPF: **6 de los 9
proyectos no se tocaron**, incluido el dominio entero. Un aislamiento que sobrevive a un cambio
de interfaz completo no es una declaración, es un hecho verificado.

---

## II. Cero secretos fuera de Windows Credential Manager — ✅ cumple

- No hay ningún secreto literal en el código.
- `IAppLogger` sigue siendo una **interfaz cerrada de 12 métodos**, todos con parámetros
  explícitos. No existe —y no debe existir— ninguna vía para registrar un objeto arbitrario.
- El método de medición que se agregó este mes toma un **enum cerrado** (`RemoteWork`) y no una
  cadena. Fue deliberado: un parámetro de texto habría abierto un canal por donde podía
  filtrarse un comando, una ruta remota o la salida de un panel al archivo de log.
- La auditoría de secretos (`task audit:secrets`) se ejecutó: **cero coincidencias** en la base
  y en los registros.
- Las credenciales viven en el Administrador de credenciales bajo claves `cmc:*`; la base sólo
  guarda la clave, nunca el secreto.

Un detalle que refuerza el principio: al borrar una conexión, **primero** se borra la
credencial y sólo si eso funciona se borra la conexión. Al revés quedaría un secreto huérfano
en el sistema operativo, invisible desde la aplicación y sin forma de limpiarlo desde ella. Hay
una prueba que lo fija.

---

## III. Test-first en el núcleo — ⚠️ cumple con una desviación declarada

**Dónde cumple**: Domain, UseCases, Infrastructure, Monitoring, Platform y Terminal. La
cobertura por capa está en [`cobertura.md`](./cobertura.md): las cuatro capas de reglas del
producto van del 70 % al 84 %.

**La desviación**: el contrato `ISessionManager` está escrito en
`contracts/servicios-de-aplicacion.md` y no existe como interfaz; `MainWindow.xaml.cs:56` instancia
la clase concreta. La lógica sí salió de las vistas: `SessionManager`, `SessionRegistry` y
`CredentialProvider` viven en `src/CafManagerConection.UseCases/` y tienen pruebas propias.

Lo que sí se extrajo este mes, y por qué importa:

- **`SessionRegistry`** — qué sesiones hay, en qué estado y cuántas por conexión. Antes se
  deducía recorriendo la tira de pestañas de la ventana principal.
- **`CredentialProvider`** — FR-039 nunca se había implementado: una conexión sin credencial se
  intentaba igual y el servidor la rechazaba, sin que nada dijera que faltaba la contraseña.
- **`WindowPlacement.EsVisibleEn`** — la comprobación de FR-047 estaba dentro de la ventana y
  usaba `System.Windows.Forms.Screen`. Su síntoma al fallar es de los peores que puede dar una
  aplicación de escritorio: arranca, no falla, y no se ve por ningún lado.

**Lo que falta**: `OpenAsync` y `ReconnectAsync`, que exigen abstraer los controles visuales
—construir una sesión implica alojar el ActiveX de RDP o el control de terminal—. Se dejó
pendiente a propósito para no mezclar un cambio riesgoso con los verificables.

**El patrón que se repite**: cada defecto encontrado este mes estaba en una línea que no se
podía probar. El fingerprint que nunca se comparaba vivía dentro de un manejador de SSH.NET.
El CLSID de RDP se elegía sin verificar. La detección de plataforma fallaba por CRLF en un
literal. Ninguno era difícil; todos eran invisibles.

---

## IV. WPF y bibliotecas open source — ✅ cumple

Cinco paquetes, todos con licencia permisiva:

| Paquete | Versión | Licencia |
| --- | --- | --- |
| SSH.NET | 2026.0.0 | MIT |
| Microsoft.Data.Sqlite | 10.0.11 | MIT |
| Dapper | 2.1.79 | Apache 2.0 |
| Serilog | 4.4.0 | MIT |
| Serilog.Sinks.File | 7.0.0 | MIT |

Fluent UI System Icons (MIT) entra como geometrías copiadas al diccionario de recursos, sin
paquete ni dependencia en tiempo de ejecución; su aviso de copyright está en el README.

**Dos decisiones del diseño se revirtieron** y ambas van en la dirección del principio:
incorporar VtNetCore se reemplazó por un emulador propio —menos código, y código que se
entiende—, y los iconos como mapas de bits por DPI se reemplazaron por geometrías vectoriales.

---

## V. Simplicidad y alcance cerrado — ⚠️ sin margen

Es el principio bajo tensión, y conviene decirlo sin adornos: **el alcance se duplicó respecto
de la planificación inicial y el colchón está consumido**.

A favor: cada ampliación tuvo enmienda constitucional previa, que es lo que el principio exige,
y la sección `Out of Scope` de la especificación sigue siendo explícita.

Donde se aplicó bien este mes:

- **Los servicios de compose no se traen.** Al medir el panel de Docker se encontró que 9
  llamadas —870 ms— alimentaban datos que ninguna pantalla mostraba. Se apagaron.
- **`custom_fields` sin interfaz.** La columna existe para no volver a migrar, pero no se
  construyó un editor para una función que nadie pidió usar.
- **Un solo nivel de jerarquía.** Profundidad arbitraria habría complicado el árbol y el
  arrastre sin un caso que lo pidiera; además vuelve imposible el ciclo por construcción.

**Recomendación**: lo que sigue —gestión de claves SSH, métricas de transferencia, instalador
firmado, acciones de escritura sobre Docker— debería evaluarse como **feature separada**, no
como ampliación de ésta.

---

## VI. Distribución sin privilegios ni servicios — ✅ cumple

`build/publicar.ps1` publica con `--self-contained true`, `PublishSingleFile=false` y
`PublishTrimmed=false`. Las dos últimas están justificadas en `research.md` §10: el recorte
rompe el interop COM y la reflexión de WPF.

- No requiere .NET instalado.
- No instala servicios de Windows.
- No pide privilegios de administrador.
- Datos y registros en `%LocalAppData%\CafManagerConection`.

---

## Resultado

**Pasa, con dos desviaciones declaradas**: Principio III en el ciclo de vida de sesiones, y
Principio V sin margen para más alcance.

Ninguna de las dos es un incumplimiento silencioso: las dos están escritas acá, en `plan.md` y
en `tasks.md`, con lo que falta para cerrarlas.
