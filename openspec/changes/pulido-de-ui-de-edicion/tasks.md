# Tareas

## Revelado de secretos

- [x] 1.1 En `ConnectionEditorWindow` y `FolderSettingsWindow`, junto a cada `PasswordBox` mostrar el
  indicador «tiene secreto guardado» a partir de `TieneSecreto`.
- [x] 1.2 Ícono de ojo que alterna oculto/visible sobre lo que se teclea (`PasswordBox` ⇄ `TextBox`).
- [x] 1.3 Con el campo vacío y secreto guardado, el ojo revela el guardado vía `Credentials.ReadAsync`
  con la `ReferenciaDeSecreto` del protocolo; manejar `VaultCerradoException` ofreciendo desbloquear,
  igual que «Copiar contraseña».
- [x] 1.4 Revelar no marca cambio: guardar sin editar deja el secreto como estaba; limpiar el buffer
  descifrado tras usarlo.

## Método de autenticación automático

- [x] 2.1 En el handler de la ruta de clave privada (escribir/examinar/pegar) de los dos editores,
  poner el método en «Clave privada» si estaba en «Automático» o «Contraseña».

## Ajustes visuales

- [x] 3.1 `ChipDeFiltro` en `Estilos.xaml`: `CornerRadius` del tema en vez de 11.
- [x] 3.2 `ConsolaDeTraza.xaml`: el `_filtro` mantiene ancho al enfocarse (Stretch o ancho fijo).

## Cierre

- [x] 4.1 `openspec validate pulido-de-ui-de-edicion --strict`.
- [x] 4.2 Pruebas de los proyectos afectados en verde.
