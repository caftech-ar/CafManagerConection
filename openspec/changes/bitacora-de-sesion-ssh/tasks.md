# Tareas

## Ajustes

- [x] 1.1 `SettingKeys`: claves de bitácora activa, carpeta y días de retención, siguiendo el patrón de
  las copias de respaldo; sin carpeta configurada se usa una subcarpeta junto a los datos de la
  aplicación.
- [x] 1.2 `AjustesReservados`: `cmc:bitacoraDeSesion` con tres estados (ausente hereda, encendido,
  apagado).
- [x] 1.3 Resolución del estado efectivo por sesión y pruebas de los seis casos.

## Escritor

- [x] 2.1 Escritor de bitácora: abre el archivo de la sesión, acepta el texto a medida que llega,
  vuelca a disco de forma periódica y cierra al terminar la sesión.
- [x] 2.2 Nombre del archivo con el host adelante, la conexión saneada y el momento de inicio, para que
  agrupe por equipo; reusar el saneado que ya tiene `SessionView.Barra`.
- [x] 2.3 Baja silenciosa ante fallo de escritura, con el motivo al registro técnico y la sesión
  intacta.
- [x] 2.4 Pruebas: se escribe lo que se va del historial, se vuelca sin cerrar, el fallo no propaga.

## Enganche en la sesión

- [x] 3.1 `TerminalBuffer`: avisar cada línea que pasa de la pantalla al historial, ya recortada de
  relleno, sin que el buffer sepa de bitácoras. Es el único punto con texto ya interpretado; el flujo
  de bytes no sirve.
- [x] 3.2 Prueba de que lo dibujado en la pantalla alternativa no genera ese aviso.
- [x] 3.3 Al cerrar la sesión, volcar también el contenido que quedaba en la pantalla.
- [x] 3.4 Arrancar la bitácora sólo si está activa para esa conexión; cerrarla al terminar la sesión,
  tanto al reconectar como al cerrar la pestaña —si no, se pierde la cola sin volcar—.

## Purga

- [x] 4.1 Purga por antigüedad al arrancar la aplicación; sin retención configurada no borra nada.
- [x] 4.2 Prueba de la purga con archivos por dentro y por fuera del plazo.
- [x] 4.3 La purga sólo toca archivos con el nombre y el sello que genera la aplicación, y no corre con
  la bitácora apagada: la carpeta la elige el usuario y puede tener archivos suyos.

## Copias de respaldo

- [x] 5.1 Excluir la carpeta de bitácoras de las copias de respaldo, con su prueba.

## Interfaz

- [x] 6.1 `PreferenciasWindow`: activación, selector de carpeta y días de retención, con la advertencia
  de que la bitácora guarda lo que se tipea, incluidas contraseñas que el servidor no oculte.
- [x] 6.2 `ConnectionEditorWindow`: interruptor de la conexión con su estado heredado a la vista, sólo
  en conexiones SSH.

## Novedades de la versión

- [x] 8.1 Aviso de una sola vez, en el arranque que actualiza la base, contando la franja y la
  bitácora, que vienen activas, dónde se apagan y que la bitácora guarda lo que se tipea. Se recuerda
  la versión avisada en los ajustes para no repetirlo.

## Cierre

- [x] 7.1 `openspec validate bitacora-de-sesion-ssh --strict`.
- [x] 7.2 Suite de los proyectos afectados en verde.
