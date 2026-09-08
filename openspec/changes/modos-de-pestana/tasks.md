# Tareas

- [x] 1.1 Enum de modo de pestaña en `Domain/Settings` y clave nueva en `SettingKeys` (`tabs.mode`),
  con el lineal-con-desplazamiento por omisión.
- [x] 1.2 Leer/guardar la preferencia por el mismo camino que los demás ajustes de `application_settings`.
- [x] 1.3 Selector del modo en `PreferenciasWindow`.
- [x] 1.4 Modo 1: la disposición actual (ScrollViewer + StackPanel).
- [x] 1.5 Modo 3: `TabPanel` nativo (envuelve en filas).
- [x] 1.6 Modo 2: panel a medida que muestra las que entran y manda la sobra a un desplegable a la
  derecha; recalcula al redimensionar; elegir del desplegable activa la pestaña.
- [x] 1.7 Aplicar el modo al construir `MainWindow` y al cambiarlo en Preferencias, conservando la
  selección y sin reabrir sesiones.
- [x] 1.8 Pruebas de lo testeable sin UI (lectura/guardado de la preferencia, valor por omisión).

## Cierre

- [x] 2.1 `openspec validate modos-de-pestana --strict`.
- [x] 2.2 Pruebas de los proyectos afectados en verde.
