---

description: "Fase 0: lo que se midió antes de decidir"
---

# Fase 0 — Investigación

Todo lo de acá se midió en el equipo del usuario: 12 núcleos, Windows 11 x64, .NET 10, compilado en
Release. Los proyectos de prueba fueron descartables y no quedó nada vivo.

## D1 — Qué función de derivación, y con qué parámetros

**Decisión**: PBKDF2-HMAC-SHA512 con 600.000 iteraciones.

**Medición** (mediana de 5 corridas, descartando la primera por el calentamiento del JIT):

| Hash | Iteraciones | Costo |
|---|---|---|
| SHA-256 | 210.000 | 145 ms |
| SHA-256 | 600.000 | 380 ms |
| SHA-256 | 1.000.000 | 626 ms |
| SHA-256 | 2.000.000 | 1.229 ms |
| SHA-512 | 210.000 | 137 ms |
| **SHA-512** | **600.000** | **408 ms** |
| SHA-512 | 1.000.000 | 672 ms |
| SHA-512 | 2.000.000 | 1.395 ms |

**Por qué SHA-512 y no SHA-256**: al mismo costo para nosotros —137 vs 145 ms a 210.000— castiga
algo más al atacante con GPU, porque las operaciones de 64 bits de SHA-512 rinden peor en ese
hardware que las de 32 bits de SHA-256.

**Por qué 600.000 y no 210.000**: 210.000 es el mínimo que recomienda OWASP; 600.000 cuesta 408 ms
en una operación que ocurre una vez por desbloqueo y triplica el costo del atacante. La primera
derivación de la ejecución paga además el calentamiento del JIT, que en los tramos cortos se notó
mucho —301 ms contra 98 en la corrida de calibración—, así que **medir una sola vez lleva a elegir
mal**.

### Alternativa descartada, y lo que costó descartarla

**Argon2id** con 128 MiB, 4 iteraciones y paralelismo 4 costaba 258 ms en el mismo equipo, o sea
menos que la opción elegida y con mucha más resistencia. Se descartó porque **.NET 10 no la trae** y
exigía `Konscious.Security.Cryptography.Argon2` 1.3.1 más su `Blake2`: dos paquetes de terceros,
chicos y sin releases desde junio de 2024, para la primitiva de la que depende toda la seguridad del
vault.

El usuario decidió: «si no está en .NET 10 usá otra cosa, bajá el nivel de seguridad, no es un
sistema de la NASA».

**Qué se perdió, con números y no con adjetivos.** Argon2id es memory-hard: cada intento paralelo
necesita sus 128 MiB, así que una GPU de 24 GB corre del orden de 190 intentos a la vez por más
miles de núcleos que tenga. PBKDF2 no reserva memoria, así que esa misma GPU corre miles de intentos
en paralelo. A igual tiempo de desbloqueo para nosotros, **el atacante con GPU compra entre dos y
tres órdenes de magnitud más intentos por peso con PBKDF2**.

**Y lo que eso significa acá.** Con una clave maestra de 8 caracteres elegida por una persona, el
espacio de búsqueda realista de un ataque de diccionario con mutaciones es tan chico que **ninguna
de las dos funciones alcanza**: la diferencia es entre días y meses, no entre posible e imposible.
Con una frase de cuatro o cinco palabras al azar, **las dos sobran** por muchos órdenes de magnitud.
La conclusión operativa es que el KDF importa mucho menos que el largo de la clave maestra, y por
eso el medidor de fuerza de FR-214 y la sugerencia de frase larga son la parte de esta feature que
más seguridad entrega por línea de código.

Para el modelo de amenaza real de este proyecto —dos instalaciones, y la exposición probable es una
copia en una carpeta sincronizada o una notebook perdida, no un adversario con presupuesto— la
elección es defendible.

## D2 — Cómo se le pasa la clave maestra a la derivación

**Decisión**: la sobrecarga
`Pbkdf2(ReadOnlySpan<char> password, ReadOnlySpan<byte> salt, Span<byte> destination, int iterations, HashAlgorithmName hash)`,
que escribe en un búfer que nosotros damos.

**Está PROHIBIDA en esta feature** la sobrecarga `Pbkdf2(string, byte[], int, HashAlgorithmName, int)`.
Un `string` con la clave maestra queda inmortal en el montón hasta que el recolector lo levante, se
puede mover de lugar y no se puede pisar con ceros: rompe FR-218 sin que nada falle. Es la trampa
más fácil de pisar de todo el diseño, porque es la sobrecarga más cómoda y la que aparece primero al
autocompletar. **Entra como prueba, no como comentario.**

Las seis sobrecargas se verificaron por reflexión sobre el ensamblado en uso.

## D3 — Los parámetros de AES-GCM

**Decisión**: nonce de 12 bytes, etiqueta de 16.

**Medición**: en esta plataforma `AesGcm.NonceByteSizes` es `12..12` —mínimo y máximo iguales—, así
que los 12 bytes de FR-201 no son una elección sino lo único posible. `AesGcm.TagByteSizes` es
`12..16`, y se toma 16, el máximo. `AesGcm.IsSupported` da `True`.

## D4 — DPAPI sin agregar un paquete

**Decisión**: P/Invoke a `CryptProtectData` y `CryptUnprotectData` de `crypt32.dll`, con el ámbito
del usuario actual.

**Por qué no el paquete**: `System.Security.Cryptography.ProtectedData` **no está en el framework**.
Se comprobó compilando: en `net10.0-windows` el tipo está reenviado a ese ensamblado y sin el
paquete el código no compila, con `CS1069` y el mensaje «Este tipo se ha reenviado al ensamblado
System.Security.Cryptography.ProtectedData». El paquete de Microsoft 10.0.0 funciona, pero sigue
siendo una dependencia, y el proyecto ya escribe este mismo tipo de P/Invoke en
`src/CafManagerConection.Infrastructure/Credentials/CredentialManagerNative.cs`.

**Medición del P/Invoke**, con las dos rutas probadas:

- Una clave de 32 bytes da un blob de **178 bytes**, y la ida y vuelta devuelve los mismos bytes.
- Un blob con **un byte cambiado** se rechaza con `CryptographicException` («Error occurred during a
  cryptographic operation») en lugar de devolver basura. Eso es lo que hace seguro el camino de
  FR-239: un blob corrupto se distingue de un blob válido, y se puede caer al pedido de la clave
  maestra sin adivinar.

**Ámbito `CurrentUser` y no `LocalMachine`**: con `LocalMachine` cualquier cuenta del equipo
desenvolvería la clave del vault, que es exactamente la exposición que FR-234 tiene que advertir.

## D5 — Una trampa que desapareció al cambiar de KDF

El borrador con Argon2id tenía un problema de portabilidad que hay que dejar anotado, porque si
alguien vuelve a proponer Argon2id lo va a encontrar de nuevo: **la salida de Argon2 depende del
grado de paralelismo**. Tomarlo de `Environment.ProcessorCount` —que es lo natural— haría que un
vault creado en un equipo de 12 núcleos no se abriera en uno de 4, rompiendo FR-250 sin que
ninguna prueba en una sola máquina lo detecte. La solución era guardar el paralelismo como un
número más de la tabla del vault.

PBKDF2 no tiene ese parámetro. Se guardan la función de hash, la sal y las iteraciones, y **nada
depende del equipo**. Es una simplificación real que se lleva un riesgo entero.

## Lo que quedó sin medir, y por qué

- **La portabilidad entre perfiles de Windows** (FR-250, SC-062) necesita un segundo usuario de
  Windows o una segunda máquina. No se puede automatizar acá: va al `quickstart.md` como
  verificación manual.
- **El fallo de DPAPI por cambio de usuario** (FR-239) es el mismo caso. Lo que sí se pudo probar es
  el fallo por blob corrupto, que recorre el mismo camino de código.
- **Qué hace hoy la 0.1.0 al abrir una base con `schema_version` mayor.** Es una pregunta sobre el
  código que ya existe, no sobre esta feature. Hay que mirar `DatabaseInitializer.cs`; si no aborta
  con un mensaje claro, es un defecto que esta feature descubre y entra como tarea en la fase 2.
