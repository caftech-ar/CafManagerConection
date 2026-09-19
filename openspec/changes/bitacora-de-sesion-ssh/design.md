## Context

`SessionView.Barra.GuardarEnArchivo` ya guarda `terminal.TextoCompleto` a un `.txt`: texto plano, sin
escapes, a pedido, y limitado a lo que el terminal retiene (`TerminalPreferences.ScrollbackLines`,
10.000 por omisión). La aplicación ya sabe manejar una carpeta configurable con retención: las copias
de respaldo tienen `CopiasActivas`, `CopiasCarpeta` y `CopiasCuantas` en `SettingKeys`.

## Goals / Non-Goals

**Goals:**
- Que quede en disco todo lo que pasó por una sesión, sin pedirlo y sin perder lo que se va del
  historial.
- Que se pueda leer y buscar con las herramientas de siempre.

**Non-Goals:**
- Reproducir la sesión como una película, con tiempos.
- Registrar sesiones RDP.
- Indexar, buscar dentro de la aplicación o mostrar las bitácoras en la interfaz.

## Decisions

**Texto plano, sin escapes.** Se escribe lo que se vería, no los bytes crudos. La bitácora existe para
leerla y buscarla después, y un archivo con los códigos de color y de posición del servidor es
ilegible en cualquier editor. Alternativa descartada: los dos archivos, plano y crudo, que duplica
disco y código de escritura para un caso que nadie pidió.

**Se engancha donde la línea entra al historial, no en el flujo de bytes.** No existe un «flujo de
texto ya interpretado» al que suscribirse: al terminal entran bytes (`TerminalControl.Write`) y el
emulador los aplica sobre el buffer. El punto que sí existe es `TerminalBuffer`, donde una línea que
sale de la pantalla se agrega al historial ya recortada de relleno. Enganchar ahí da tres cosas de
arriba:

- el texto ya viene renderizado y sin secuencias de control, que es exactamente lo que se quiere
  guardar;
- llega línea por línea, o sea escritura en flujo real, no un volcado al final;
- **los programas de pantalla completa no ensucian nada**. `vim`, `htop` y `less` usan la pantalla
  alternativa —`VtEmulator` ya atiende `1049`, `1047` y `47`—, y lo que pasa ahí no entra al historial.
  El riesgo de «cientos de pantallas repetidas» que tendría un enganche sobre los bytes desaparece solo.

El precio es que lo que está en la pantalla visible todavía no se escribió, así que la bitácora se
completa con el contenido de la pantalla al cerrarse la sesión.

**La carpeta por omisión va con los datos de la aplicación.** Una subcarpeta donde ya vive la base, así
la bitácora funciona apenas se activa, sin configurar nada. Descartada Documentos: es más cómoda para
leer un registro a mano, pero en muchas máquinas la sincroniza OneDrive, y estas bitácoras pueden tener
texto sensible. La carpeta sigue siendo configurable.

**Un archivo por sesión, con el host adelante.** El nombre es host, conexión y momento de inicio, los
dos primeros saneados como ya lo hace `GuardarEnArchivo`. El host va primero para que al ordenar la
carpeta por nombre queden juntas todas las sesiones contra un mismo equipo, incluso las de conexiones
distintas que apuntan al mismo servidor. Reconectar abre archivo nuevo, porque es una sesión nueva.

**Escritura a medida que llega, con vaciado periódico.** No se acumula en memoria para escribir al
cerrar: si la aplicación se cae o la máquina se apaga, lo registrado hasta ahí tiene que estar. Tampoco
se vacía a disco en cada línea. Un vaciado por tiempo deja la bitácora al día sin pegarle al disco en
una sesión que escupe salida.

**Un fallo de escritura no toca la sesión.** Si el disco se llena o la carpeta deja de estar, la
bitácora de esa sesión se da de baja, el motivo queda en el registro técnico y la sesión sigue
andando. Nadie pierde una conexión porque no se pudo escribir un archivo.

**Purga por antigüedad, al arrancar.** Días configurados, no cantidad de archivos: lo que importa es
cuánto hacia atrás se puede mirar. Se hace al arrancar y no con un reloj de fondo, que es donde menos
molesta.

**La carpeta queda fuera de las copias de respaldo.** Las bitácoras crecen sin techo y contienen texto
sensible; meterlas en la copia multiplicaría su tamaño y desparramaría ese contenido. No hizo falta
excluirlas: `ServicioDeCopias` copia sólo la base, y lista `cmc-*.db`, así que un `.txt` no entra ni
aunque las dos carpetas coincidan. La purga, del otro lado, borra sólo `*.txt` y no toca las copias.

## Risks / Trade-offs

- **La bitácora guarda lo que se tipea.** `sudo` oculta su contraseña, pero un `mysql -p…`, un
  `export TOKEN=…` o cualquier prompt que no oculte quedan escritos en claro. No hay forma confiable de
  filtrarlo desde el terminal: no se sabe qué parte de la salida es un pedido de contraseña. Se
  mitiga con la purga, se dice sin vueltas en Preferencias y se cuenta en el aviso de novedades de la
  versión que la estrena, porque viene activa.
- **Crece rápido.** Una sesión con salida continua puede dejar cientos de megas por día. La retención
  es la única contención; no hay límite por archivo.
- **Lo que está en pantalla al momento de una caída se pierde**, porque todavía no pasó al historial.
  Es a lo sumo una pantalla de texto, y el resto de la sesión está escrito.
- **Lo que se hizo dentro de `vim` o `htop` no queda registrado.** Es la contracara de que no ensucien:
  la bitácora cuenta lo que pasó por la línea de comandos, no lo que se editó adentro de un programa de
  pantalla completa.
- Un programa que repinta en la pantalla normal, sin usar la alternativa, sí deja líneas repetidas.
