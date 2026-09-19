## Context

El árbol ya entrega `ConnectionSummary` con `Host` y `EffectivePort` resueltos, y
`NodoArbol.Recorrer()` ya baja por el subárbol (lo usa `AbrirTodasAsync`). `SondaDePuerto.RespondeAsync`
ya hace un intento TCP con tiempo de espera y cancelación, pero tiene el host clavado en `127.0.0.1`
porque se escribió para verificar túneles locales.

## Goals / Non-Goals

**Goals:**
- Responder «a cuál me puedo conectar» de una carpeta entera en un par de segundos.
- No mentir cuando el ICMP está filtrado.

**Non-Goals:**
- Medir ancho de banda. Un ping no lo da.
- Monitoreo continuo, alarmas o historial de disponibilidad.
- Descubrir hosts que no estén en el árbol.

## Decisions

**Las dos pruebas siempre, en paralelo; la cascada es de presentación.** Encadenarlas —ping y, si
falla, puerto— tarda el doble en el peor caso y pierde el caso que más cuesta en la práctica: el host
responde ping pero el servicio está caído, y la fila sale verde. Lanzadas juntas, el tiempo es el del
sondeo más lento (~1 s) y aparece el estado intermedio.

| ICMP | Puerto | Estado                |
|------|--------|-----------------------|
| sí   | sí     | vivo                  |
| no   | sí     | vivo                  |
| sí   | no     | sin servicio          |
| no   | no     | sin respuesta         |
| —    | —      | no resuelve el nombre |

**El ICMP no decide solo.** Windows Server no responde ping con su firewall por omisión, y muchas
redes corporativas lo filtran por política. Una ventana que sólo pinguea pinta en rojo máquinas que
andan bien, que es la peor falla posible para algo cuyo único trabajo es decir si está vivo.

**Concurrencia acotada en 16.** De 16 a «todas juntas» se ganan un par de segundos y se paga que el
patrón —decenas de ICMP y SYN simultáneos contra un rango contiguo— se parezca a un escaneo de red
para un IDS. También acota el caso de una carpeta de cientos de hosts.

**Las conexiones Web quedan afuera.** El destino real de una conexión Web es `WebSettings.Url`, que
sólo se obtiene con `GetDetailAsync`: el `Host` del resumen es apenas un respaldo —así lo trata
`DireccionParaCopiar`— y su puerto efectivo no tiene por qué ser el de la URL. Sondearlas con el host y
el puerto del resumen daría un resultado sobre un destino que no es el que abre la conexión, que es
peor que no mostrarlas. Resolver la URL de cada una obligaría a una consulta de detalle por conexión
antes de empezar a sondear, para un dato que un ping tampoco contesta bien (un servidor web puede
aceptar el puerto y devolver 500). Se listan en la ventana con el estado «no se sondea» y el motivo, en
lugar de desaparecer sin explicación de una carpeta mixta.

## Risks / Trade-offs

- **El ICMP puede pedir privilegios.** `System.Net.NetworkInformation.Ping` no los pide en Windows
  para el caso normal, pero si la prueba falla por permiso hay que distinguirlo de «no responde»: se
  informa como prueba no concluyente y el estado lo decide el puerto.
- **«Sin servicio» puede ser un falso positivo**: un firewall que filtra el puerto pero deja pasar el
  ICMP se ve igual que un servicio caído. La ayuda emergente dice qué probó exactamente.
