## Context

`SelectorDeInstalador` ya reconoce los instaladores de una publicación (`.exe` que contienen `setup`),
ya clasifica cada uno en liviano o completo por su nombre, y ya lee de
`HKLM\Software\CafManagerConection\TipoDeInstalador` cuál está instalado —marca que escribe el NSIS al
instalar, elevado—. Con eso elige uno y devuelve ése.

## Goals / Non-Goals

**Goals:**
- Que se vea qué se está por bajar y que se pueda cambiar.
- No cambiar lo que pasa hoy para quien no elige nada.

**Non-Goals:**
- Cambiar la forma en que se detecta o se descarga la publicación.
- Instalar .NET desde la aplicación.
- Elegir entre arquitecturas u otras variantes.

## Decisions

**Enumerar, además de elegir.** `SelectorDeInstalador.Elegir` se queda como está —es la elección por
omisión y hay código que depende de ella—; se suma una operación que devuelve los candidatos con su
tipo, para que el aviso los muestre. La lógica de clasificación no se duplica.

**El tipo instalado se marca, no se impone.** La marca del registro deja de ser sólo el criterio de
elección automática y pasa a ser también información: el aviso indica cuál es el que está instalado,
así el cambio de tipo es una decisión consciente y no un accidente.

**Se dice qué precisa cada uno, con el tamaño.** «Liviano» y «completo» no significan nada sin eso. El
tamaño sale de la publicación; la condición —que el liviano precisa .NET en la máquina— es texto fijo.

**No se verifica si .NET está presente.** Detectarlo de forma confiable es leer el registro y las
carpetas de instalación de los tiempos de ejecución, para un caso en el que la aplicación que hace la
comprobación está corriendo sobre .NET y por lo tanto lo tiene. El riesgo real es bajar el liviano para
otra máquina, y ahí la comprobación local tampoco serviría. Se dice la condición y se confía.

## Risks / Trade-offs

- **Cambiar de tipo ya estaba resuelto en el instalador.** `installer/CafManagerConection.nsi` tiene
  `QuitarInstalacionAnterior`, que desinstala la versión previa antes de copiar, y su comentario dice
  que existe justamente porque «las dos variantes no tienen los mismos archivos». Así que pasar de
  liviano a completo funciona sin pasos extra; el aviso al cambiar de tipo queda como información, no
  como advertencia de riesgo.
- Una publicación con instaladores mal nombrados —sin `completo` en el nombre del completo— los
  clasifica a los dos como livianos. Ya pasa hoy; la elección lo hace visible en vez de esconderlo.
