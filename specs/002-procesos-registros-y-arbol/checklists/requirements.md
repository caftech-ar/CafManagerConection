# Specification Quality Checklist: Procesos, registros y árbol

**Purpose**: validar la especificación antes de implementar

**Feature**: [spec.md](../spec.md)

## Content Quality

- [x] Sin detalles de implementación (lenguajes, marcos, interfaces)
- [x] Centrada en el valor para el usuario
- [x] Legible por alguien que no programa
- [x] Todas las secciones obligatorias completas

**Salvedad, deliberada**: varios requisitos citan un archivo y una línea del código actual
(`SshCommandRunner.cs:213`, `ConnectionService.cs:171`, `MainWindow.Acciones.cs:196`,
`FolderSettingsWindow.xaml:51`). No son detalles de implementación de lo que se va a construir: son
la prueba de lo que hoy está mal, y sin ellas el requisito se lee como una opinión. La regla del
proyecto es que una afirmación sin un número ni una ruta se borra; acá vale igual.

## Requirement Completeness

- [x] No quedan marcas `[NEEDS CLARIFICATION]`
- [x] Los requisitos son verificables y no ambiguos
- [x] Los criterios de éxito son medibles
- [x] Los criterios de éxito no dependen de la implementación
- [x] Los escenarios de aceptación están definidos para las seis historias
- [x] Los casos de borde están identificados
- [x] El alcance está acotado
- [x] Dependencias y supuestos identificados

## Feature Readiness

- [x] Cada requisito funcional tiene al menos un escenario de aceptación
- [x] Las historias cubren los flujos principales
- [x] Los criterios de éxito son alcanzables con lo especificado
- [x] Ningún detalle de implementación se filtró como requisito

## Lo que el usuario aprobó, y con qué condiciones

Las tres preguntas que frenó la especificación están contestadas y la constitución está en 1.14.0.

1. **Elegir la forma del icono: aprobado.** Entra en alcance como «Icono elegible» dentro del
   Principio V (`constitution.md:772`). FR-195, FR-195a y FR-195c ya no llevan ninguna marca de
   bloqueo. La cláusula «colorear y ordenar lo que ya se muestra» (`constitution.md:783`) no
   alcanzaba: la forma es un atributo persistido nuevo, y por eso hizo falta la enmienda.
2. **Migración 006: aprobada.** Agrega la columna de clave de icono en `folders` y en `connections`,
   en paralelo a la de color. Se escribe y se aplica.
3. **Contraseña de `sudo`: se pide y no se guarda.** Al usuario se le planteó que retenerla mientras
   dure la sesión contradice el Principio II y eligió esa opción igual, así que la 1.14.0 le declara
   una **excepción acotada en el título** (`constitution.md:527`) —exactamente la forma que la 1.13.0
   le exigió al `-pwfile` de PuTTY y que allí se rechazó—. La excepción vale sólo para `sudo`: la
   contraseña a las herramientas externas sigue prohibida por completo (FR-188).

   Las cinco reglas que la acotan están transcriptas en FR-184e como requisitos verificables, no
   resumidas: sin persistencia en ningún almacén —tampoco el Administrador de credenciales—, sin
   línea de comandos, sin registro ni mensaje de error, búfer pisado con ceros al cerrar la sesión
   siguiendo a `EntradaDeContrasenaInteractiva.TomarTexto()`
   (`src/CafManagerConection.Ssh/EntradaDeContrasenaInteractiva.cs:52`), y vigencia por sesión y no
   por conexión.

   Las verifican SC-052 (no aparece en ningún registro), SC-052a (no queda en la base, en la
   configuración ni en el Administrador de credenciales), SC-052b (el búfer queda en cero) y
   SC-052c (reabrir la conexión la vuelve a pedir). Los escenarios 9, 10 y 11 de US1 son su prueba
   de aceptación.

## Dos decisiones tomadas sin el usuario

Las tres grandes —icono, migración y contraseña de `sudo`— las contestó él. Quedan dos que no,
anotadas en «Assumptions». Cambiar cualquiera **no cambia ningún requisito**, sólo cómo se cumple:

1. **Organización**: una sola feature 002 con las seis historias, en lugar de partirla en 002 y 003.
2. **Distribución de las ventanas** (FR-196): dos columnas dentro de cada pestaña, con las secciones
   en recuadros con título.

## Notes

Los requisitos conservan la numeración con la que nacieron en la 001. No se renumeraron, así que las
referencias cruzadas desde la 001 siguen resolviendo.

Esta especificación pasó una revisión adversarial que encontró 26 problemas, incluidos dos críticos
—el de FR-184e y el de la enmienda faltante— y seis afirmaciones falsas sobre el código que los
propios requisitos usaban como prueba. Todas están corregidas.
