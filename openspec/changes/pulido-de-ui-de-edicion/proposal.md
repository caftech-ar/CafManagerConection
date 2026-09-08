## Why

Cuatro asperezas de la interfaz de edición y de la traza, chicas por separado, que hoy hacen ruido:
una contraseña ya guardada no se puede ver ni confirmar que existe; definir una clave privada SSH no
cambia el método de autenticación, así que hay que acordarse de tocar el combo; los chips de filtro
son píldoras muy redondeadas que desentonan; y el campo de filtro de la traza se achica al hacerle
foco.

## What Changes

- **Secretos en edición**: junto a cada `PasswordBox` (RDP/SSH/Web) se muestra si ya hay una
  contraseña guardada, y un ícono de ojo la revela descifrándola del vault por el mismo camino que
  «Copiar contraseña» (`Credentials.ReadAsync`). El ojo también muestra lo que se está tecleando.
- **Método de autenticación automático**: al definir o pegar una clave privada SSH, el método pasa a
  «Clave privada» si estaba en «Automático» o «Contraseña».
- **Chips menos redondos**: `ChipDeFiltro` baja su `CornerRadius` de 11 (píldora) al radio del tema.
- **Filtro de traza estable**: el campo de filtro de `ConsolaDeTraza` deja de encogerse al enfocarse.

## Capabilities

### New Capabilities

- `revelado-de-secretos-en-edicion`: cómo se indica que un campo de contraseña ya tiene un secreto
  guardado y cómo se revela, incluido el caso del vault bloqueado.
- `metodo-de-autenticacion-automatico`: cuándo el método SSH pasa solo a «Clave privada» al aparecer
  una clave, sin pisar una elección explícita del usuario.
- `ajustes-visuales-menores`: la forma de los chips de filtro y la estabilidad del campo de filtro de
  la traza.

### Modified Capabilities

Ninguna: `openspec/specs/` está vacío.

## Impact

- `CafManagerConection.App`: `ConnectionEditorWindow` y `FolderSettingsWindow` (revelado y método
  automático); `Themes/Estilos.xaml` (`ChipDeFiltro`); `Views/ConsolaDeTraza.xaml` (filtro).
- Reusa `Credentials.ReadAsync` y el manejo de `VaultCerradoException` que ya usa «Copiar contraseña».
- Sin cambios de dominio, de persistencia ni de esquema.
