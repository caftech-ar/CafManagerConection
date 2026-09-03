# Decisiones tomadas

Trece decisiones, con la opción elegida y qué queda de cada una en el repositorio.

---

## Base de datos y arranque

### 1. La copia de seguridad automática: **C, queda como está**

La copia se dispara desde `MainWindow` (`src/CafManagerConection.App/Views/MainWindow.xaml.cs:110`),
que nace en `src/CafManagerConection.App/App.xaml.cs:51`, después de que el `CompositionRoot` de la
línea 49 aplique las migraciones pendientes
(`src/CafManagerConection.App/Bootstrap/CompositionRoot.cs:95`). La 006 es aditiva —dos
`ALTER TABLE ADD COLUMN` anulables— y no hay nada que perder.

### 2. La migración 006: **A, la aplica la aplicación al arrancar**

Escrita en `src/CafManagerConection.Infrastructure/Database/Migrations/Migration006_Icono.cs`,
registrada en `DatabaseInitializer.cs:19` y con 8 pruebas contra bases temporales
(`tests/CafManagerConection.Infrastructure.Tests/Database/Migracion006Tests.cs`). Entra la próxima
vez que se abra CMC. Ninguna base real se toca a mano.

### 3. El repositorio y las releases: **A, se prepara acá y las ejecuta el usuario**

El origen es `caftech-ar/CafManagerConection`. `.github/workflows/release.yml` arma los tres
artefactos, escribe el `.sha256` de cada uno (línea 119) y publica al llegar la etiqueta. Empujar y
etiquetar es del usuario.

## Diseño

### 4. Las ventanas de configuración: **A, dos columnas con secciones con título**

`FolderSettingsWindow.xaml` y `ConnectionEditorWindow.xaml` reparten cada pestaña en tres columnas
—`*` / 16 / `*`— con secciones en el estilo `Tarjeta` y título `Titulo` a 14. La pestaña de acceso
lleva los mismos títulos y el mismo orden en las dos ventanas: «Cuenta» y «Credenciales»
(`ConnectionEditorWindow.xaml:140` y `164`, `FolderSettingsWindow.xaml:96` y `125`). La ventana de
carpetas queda en 880 de ancho con `ResizeMode="NoResize"`.

### 5. Qué instalador ofrece la actualización: **A, marca en el registro**

`installer/CafManagerConection.nsi:291` escribe `TipoDeInstalador` en `HKLM\Software\${NOMBRE}`, y
`src/CafManagerConection.App/Services/SelectorDeInstalador.cs` la lee —`InterpretarMarca`, línea
48— sin escribir nada.

### 6. Por dónde empieza la feature 002: **A, el orden del plan**

`tasks.md` conserva ese orden: fase 0, Foundational, y después US1 a US6 por prioridad.

### 7. Reporte de mouse en el terminal: **A, queda así**

`VtEmulator.cs` consume `?1000`, `?1002` y `?1006` y expone su estado (líneas 435, 439 y 443);
`TerminalControl.cs` no lo lee, así que no traduce clics ni rueda. El terminal se usa con teclado.

### 8. Teclado numérico en modo aplicación: **A, cableado**

`src/CafManagerConection.Terminal/KeyboardMapper.cs:87` manda `ESC O p` a `ESC O y` por el numérico
cuando el programa remoto pidió el modo aplicación y no hay Control ni Alt de por medio.

## Higiene

### 9. Los renombres a `ClaveDeColor`: **A, se hacen**

Sin aplicar: `Etiqueta.Color` (`src/CafManagerConection.Domain/Connections/Etiqueta.cs:34`),
`Folder.IconColor` (`Folder.cs:22`) y `Connection.IconColor` (`Connection.cs:62`) conservan sus
nombres.

### 10. Las pruebas SSH omitidas: **A, apuntadas por variable de entorno**

`tests/CafManagerConection.Ssh.Tests/PruebaDeIntegracionSshAttribute.cs` omite la prueba con el
motivo cuando falta una variable, y `ServidorDePrueba.MotivoDeOmision()` dice cuáles definir y cómo.
Ninguna variable se escribe en el repositorio ni en un `.runsettings`.

### 11. Versión y etiqueta: **A, 0.0.4**

`Directory.Build.props:44` está en 0.0.4, con `AssemblyVersion`, `FileVersion` e
`InformationalVersion` al mismo valor. La última etiqueta del repositorio es `v0.0.3`.

### 12. El techo del 5 % de comentario: **A, techo duro para todos**

`tests/CafManagerConection.Domain.Tests/DensidadDeComentarioTests.cs:7` fija `Techo = 0.05` para
todos los proyectos, sin excepción por proyecto ni trinquete.

### 13. Los commits: **B, uno solo**

Todo sigue en el working tree. Commitear, ramificar, etiquetar y publicar lo hace el usuario.

---

## Lo que estas decisiones dejan abierto

- **La copia de seguridad sigue corriendo después de migrar** (decisión 1). La próxima migración que
  no sea aditiva hay que decidirla de nuevo, y antes de escribirla.
- **Los renombres de la decisión 9 están sin aplicar.** Tocan el mapeo de Dapper —el `SELECT` trae
  la columna `color` y Dapper la asocia por nombre— y enlaces de XAML que no rompen la compilación.
- **El techo de comentario es uno para todos** (decisión 12): cuando un proyecto lo pase, hay que
  podar un comentario legítimo o mejorar el código, no subir el techo.
- **Esperan al usuario**: abrir CMC para que la 006 entre, etiquetar `v0.0.4` para que el flujo
  publique, y el commit único.
