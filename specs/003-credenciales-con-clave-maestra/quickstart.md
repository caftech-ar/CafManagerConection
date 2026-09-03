---

description: "Fase 1: cómo se valida esta feature a mano"
---

# Validación

Lo automatizable va en las pruebas y no acá. Esto es lo que hay que mirar en pantalla o probar
contra un segundo perfil de Windows, más el orden en que conviene hacerlo.

## Antes de empezar

```powershell
$env:MSBUILDDISABLENODEREUSE = '1'
dotnet build CafManagerConection.slnx -c Release -p:UseSharedCompilation=false
dotnet test  CafManagerConection.slnx -c Release -p:UseSharedCompilation=false --no-build
dotnet build-server shutdown
```

**Y antes de tocar nada, guardá una copia de tu base.** Está en
`%LocalAppData%\CafManagerConection`. Con las credenciales todavía en el Administrador de
credenciales, esa copia sola no alcanza para volver atrás: exportá también la lista de claves
`cmc:*` que tenés guardadas, para saber qué habría que recargar si algo sale mal.

## 1 · Crear el vault (US1)

Con una carpeta de datos vacía, para no arriesgar la real:

```powershell
$env:LOCALAPPDATA_CMC_PRUEBA = "$env:TEMP\cmc-prueba"   # si la aplicación lo soporta
```

Qué mirar:

- Los requisitos de la clave maestra se ven **antes** de escribir, no como error después (FR-212).
- `abc12345` se rechaza por falta de carácter especial, y **lo tipeado no se borra** (FR-212).
- Una frase de 60 caracteres con espacios se acepta entera (FR-213).
- La advertencia de que perderla es irrecuperable está a la vista, con esas palabras (FR-215).
- El medidor de fuerza se mueve, y una frase larga marca más que ocho caracteres con un signo
  (FR-214).
- La elección de recordar el dispositivo **no tiene nada preseleccionado** (FR-234), y cerrar la
  ventana sin elegir la deja apagada (FR-235).
- El desbloqueo tarda unos 400 ms y **la ventana no se congela** mientras deriva.

## 2 · Abrir con el vault bloqueado (US1, FR-219)

Cancelá el pedido de la clave maestra al arrancar:

- CMC abre. Se ve el árbol, las carpetas y los ajustes.
- Dice qué queda sin funcionar, en lugar de callarse o de fallar.
- Una conexión RDP con identidad de Windows, o una SSH por clave sin passphrase, **conecta**
  (FR-270).
- Copiar la contraseña al portapapeles ofrece desbloquear (FR-274).
- Importar sesiones de PuTTY trae las conexiones y dice cuántas contraseñas quedaron afuera
  (FR-273).

## 3 · Recordar el dispositivo y bloquear (US3)

En este orden, porque el punto 4 es el que se olvida:

1. Encendé «recordar este dispositivo». Cerrá CMC, abrilo: **no pregunta nada** (FR-232).
2. Bloqueá a mano. **Sin cerrar CMC**, intentá usar una credencial: pide la clave maestra (FR-238).
   Si se desbloquea solo, el bloqueo es decorativo y el defecto es este.
3. Cerrá y abrí: vuelve a desbloquear solo, porque el desarme es por ejecución y no por disco.
4. «Olvidar este dispositivo», cerrá y abrí: pregunta (FR-240).

## 4 · La prueba que no se puede automatizar acá (US4, SC-062)

**Necesita un segundo usuario de Windows en este equipo, o una segunda máquina.** Es la
verificación que sostiene todo el modelo de dos claves, así que no se puede saltear.

1. Con «recordar este dispositivo» **encendido**, hacé una copia de seguridad desde CMC.
2. Iniciá sesión con otro usuario de Windows, o copiá la base a otra PC.
3. Abrí CMC ahí y restaurá la copia.
4. **Tiene que pedir la clave maestra** —el dato de DPAPI del otro usuario no sirve, y eso no es un
   error (FR-239)— y con ella **tienen que leerse todas las credenciales** (FR-250).
5. Comprobá que en el archivo de la copia **no hay** nada que permita abrirla sin la clave maestra
   (FR-251): el dato del dispositivo recordado vive fuera de la base y no debería viajar.

Si el paso 4 pide la clave maestra y falla con ella, el modelo está mal armado y no hay que seguir.

## 5 · Migrar (US2)

**Sobre una copia de tu base real, nunca sobre la original.**

- Todas las conexiones que tenían credencial siguen conectando.
- En el Administrador de credenciales de Windows —`control /name Microsoft.CredentialManager`, o
  `cmdkey /list`— **no queda ninguna entrada `cmc:*`**.
- El resumen dice cuántas se trajeron, y si algo quedó afuera dice cuál y por qué (FR-263).
- Cortá la aplicación a mitad de la migración y volvé a abrirla: retoma y no duplica (FR-264).

## 6 · El defecto de la versión vieja (FR-268, SC-071)

Con una base ya migrada por la 0.1.1, abrila con un ejecutable de la 0.1.0:

- **Tiene que abortar** nombrando las dos versiones de esquema.
- Lo que **no** puede pasar es que abra, muestre las conexiones sin credencial y ofrezca guardarlas:
  eso vuelve a escribir secretos en el Administrador de credenciales. Es lo que hace hoy, y por eso
  esta verificación existe.

## 7 · Que no se filtre nada (SC-065)

Con una clave maestra conocida y distinguible —por ejemplo `Zorro-Verde-2026!`— usá la aplicación
un rato: desbloqueá, conectá, copiá una contraseña, forzá un error de conexión. Después:

```powershell
Select-String -Path "$env:LOCALAPPDATA\CafManagerConection\logs\*.log" -SimpleMatch 'Zorro-Verde-2026!'
```

No tiene que aparecer ni una vez, ni en los logs, ni en la consola de traza, ni en el texto de
ningún error. Lo mismo para la contraseña de una conexión.

## 8 · Al terminar

```powershell
dotnet build-server shutdown
```

Y si levantaste el contenedor de pruebas SSH para algo, bajalo: `scripts/sshd-prueba.ps1 -Down`.
