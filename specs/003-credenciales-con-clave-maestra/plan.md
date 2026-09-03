---

description: "Plan de implementación: credenciales cifradas con una clave maestra"
---

# Implementation Plan: Credenciales cifradas con una clave maestra

**Feature**: `003-credenciales-con-clave-maestra` | **Fecha**: 2026-09-03 |
**Spec**: [spec.md](./spec.md)

**Constitución**: 2.1.0

## Summary

Los secretos salen de Windows Credential Manager y pasan a la base local, cifrados con AES-256-GCM
bajo una clave del vault de 32 bytes al azar. Esa clave vive envuelta por otra que sale de PBKDF2
sobre la clave maestra que el usuario tipea y que no se guarda nunca. Opcionalmente, y apagado por
omisión, la clave del vault se guarda además envuelta por DPAPI del usuario actual para desbloquear
sin preguntar.

**La palanca que hace barato todo esto ya existe.** `ICredentialStore`
(`src/CafManagerConection.UseCases/Abstractions/ICredentialStore.cs:7`) tiene cinco métodos y su
comentario dice «Único lugar donde puede vivir un secreto (Principio II)». Una implementación nueva
detrás de esa interfaz cambia el almacén sin tocar `CredentialProvider`, la herencia de carpetas, el
pedido de credencial al conectar, ni ninguno de los nueve consumidores. `WindowsCredentialStore`
no se borra: queda como la fuente que lee el migrador de la 0.1.1.

## Technical Context

**Language/Version**: C# 14 sobre .NET 10 (`net10.0-windows`)

**Primary Dependencies**: **ninguna nueva.** Todo sale del BCL de .NET 10:
`Rfc2898DeriveBytes.Pbkdf2`, `AesGcm`, `RandomNumberGenerator`, `CryptographicOperations`.

DPAPI es la excepcion aparente: `ProtectedData` **no** esta en el framework —en `net10.0-windows`
el tipo esta reenviado a un ensamblado aparte y sin el paquete el codigo no compila (`CS1069`),
comprobado con un proyecto de prueba—. En lugar de agregar el paquete, se usa **P/Invoke a
`CryptProtectData` y `CryptUnprotectData` de `crypt32.dll`**, que es el mismo patron que
`src/CafManagerConection.Infrastructure/Credentials/CredentialManagerNative.cs` ya usa contra el
Administrador de credenciales. Probado: 32 bytes dan un blob de 178, la ida y vuelta funciona, y un
blob con un byte cambiado se rechaza con `CryptographicException` en lugar de devolver basura.

**Storage**: SQLite, la misma base. Migración **007**, la primera no aditiva del proyecto.

**Testing**: xUnit + NSubstitute. Las pruebas de cifrado son deterministas y no necesitan servidor;
las de DPAPI y las de portabilidad entre perfiles de Windows sí necesitan el equipo del usuario.

**Target Platform**: Windows 11 x64

**Project Type**: aplicación de escritorio

**Performance Goals**: el desbloqueo tarda unos 400 ms de derivación. Medido en este equipo, no
estimado: ver [research.md](./research.md).

**Constraints**: la derivación es un solo hilo saturado durante ese tiempo, así que corre fuera del
hilo de interfaz. Fuera del desbloqueo, el costo por credencial es un AES-GCM de unos bytes.

**Scale/Scope**: dos instalaciones, y del orden de 130 credenciales en la más cargada.

## Constitution Check

### Antes de la investigación

| Principio | Estado | Nota |
|-----------|--------|------|
| I · Dominio aislado | ✅ | La criptografía y el modelo del vault son `Domain`, sin E/S. El acceso a SQLite y a DPAPI queda en `Infrastructure`. |
| II · Cero secretos en claro | ✅ | Esta feature **es** la aplicación del principio 2.1.0. FR-200 a FR-207 y FR-230 a FR-240 lo recogen requisito por requisito. |
| III · Test-first en el núcleo | ✅ | `Domain` y `UseCases` van con prueba primero. La criptografía se presta: entrada fija, salida fija. |
| IV · WPF y open source | ✅ | **Cero dependencias nuevas.** Todo del BCL, y DPAPI por P/Invoke con el patrón que el proyecto ya usa. Complexity Tracking queda sin una sola fila. |
| V · Alcance cerrado | ✅ | Es una redefinición del almacén, no una ampliación. No entra ningún dato nuevo del servidor. US5 (cambiar la clave maestra) es lo único que no estaba en el pedido y está marcado como tal en la spec. |
| VI · Sin privilegios ni servicios | ✅ | DPAPI `CurrentUser` corre como el usuario. Nada de servicios. |

### Puertas obligatorias

1. **Puerta constitucional** — pasada arriba, revalidada al pie de este documento.
2. **Puerta de esquema** — ⛔ **ABIERTA. Bloquea escribir la migración 007.** La justificación
   escrita está en la sección siguiente; falta la confirmación explícita del usuario. La
   constitución la exige y no se saltea.
3. **Puerta de secretos** — se aplica a todo lo de esta feature. Las tres preguntas que la 2.1.0
   agregó son las que verifican las pruebas SC-064 y SC-065.

## Puerta de esquema: justificación de la migración 007

La constitución exige, antes de escribir o ejecutar nada: qué problema resuelve, qué alternativas
se descartaron, y qué impacto tiene sobre los datos existentes.

### Qué problema resuelve

Hoy la base guarda referencias opacas: `rdp_credential_key`, `ssh_credential_key` y
`web_credential_key` en `connections`, y `credential_key` en `connection_folders`
(`Migration001_InitialSchema.cs:26-28,49`). Son punteros al Administrador de credenciales. Sin
esquema nuevo no hay dónde poner el texto cifrado, su nonce, la envoltura de la clave del vault, la
sal, los parámetros del KDF ni el verificador.

### Qué se agrega

- Una tabla para el **vault**: una sola fila con la envoltura de la clave, su nonce, la sal, las
  iteraciones y el hash del KDF, el verificador con su nonce, y la versión del formato.
- Una tabla para las **credenciales**: la clave lógica —la misma cadena `cmc:*` que ya se usa, para
  no tocar la resolución de herencia—, el usuario, el dominio, el secreto cifrado y su nonce.

Las cuatro columnas `*_credential_key` **no se tocan y no se borran**: siguen siendo la clave lógica
que resuelve la herencia. Eso es lo que mantiene la migración 007 casi aditiva y deja intacta toda
la lógica de `SettingsResolver` y `CredentialProvider`.

### Alternativas descartadas

| Alternativa | Por qué no |
|---|---|
| Cifrar la base entera con SQLCipher o similar | Cambia el proveedor de datos, es una dependencia nativa, rompe el publish self-contained y el API de respaldo de SQLite que usa `ServicioDeCopias.cs:128`. Y cifra de más: las conexiones y las carpetas no son secretos, y FR-219 exige poder mostrarlas con el vault bloqueado. |
| Guardar el secreto cifrado en las columnas que ya existen | Mezcla un puntero con un dato cifrado en la misma columna, y no hay dónde poner el nonce. Un nonce compartido o derivado del identificador es exactamente lo que FR-201 prohíbe. |
| Un archivo aparte para el vault, fuera de SQLite | Dos archivos que hay que mantener consistentes, y la copia de seguridad —que usa el API de respaldo de SQLite— dejaría de cubrirlo. FR-250 exige que la copia se abra en otra PC; con dos archivos hay dos formas de perder la mitad. |
| Derivar la clave de cifrado directo de la clave maestra, sin clave del vault | Cambiar la clave maestra obligaría a recifrar las 130 credenciales, y un corte a mitad deja la base a medio convertir. Es el motivo por el que la 2.1.0 fijó el modelo de dos claves. |

### Impacto sobre los datos existentes

- **Ninguna columna se borra y ninguna se reescribe.** La 007 crea dos tablas y sube el número de
  versión.
- Las credenciales existentes **no están en la base**: están en el Administrador de credenciales. La
  migración 007 no las mueve; eso lo hace el migrador de la 0.1.1, que es código de aplicación y no
  de esquema, y que borra del Administrador sólo después de leer desde el vault (FR-261).
- **Volver atrás no está protegido, y ahora se sabe.** Una base con la 007 aplicada, abierta por una
  0.1.0, **abre igual**: el filtro de `DatabaseInitializer.cs:78` no encuentra migraciones que
  aplicar y el método retorna sin decir nada. Esa versión no conoce las tablas del vault, busca las
  credenciales en el Administrador —que el migrador ya vació—, concluye que ninguna conexión tiene
  credencial y **vuelve a escribir secretos en el Administrador**. Hoy no molesta porque todas las
  migraciones fueron aditivas; con el vault sí. Es un defecto del código existente que esta feature
  vuelve peligroso, y entró como FR-268.
- **La copia de seguridad corre después de migrar** —deuda ya registrada en la 002: `App.xaml.cs:49`
  crea el `CompositionRoot`, que migra, y la copia se dispara desde el `MainWindow` de la línea 51—.
  **La 007 es la primera migración donde esto importa de verdad**, porque es la primera que no es
  puramente aditiva en su efecto sobre el modelo. Se arregla en esta feature: la copia va antes.

**Esta puerta queda abierta hasta que el usuario la confirme.** Sin esa confirmación no se escribe
`Migration007_Vault.cs`.

## Project Structure

### Documentación

```text
specs/003-credenciales-con-clave-maestra/
├── plan.md              # Este archivo
├── spec.md              # 54 FR, 11 SC, 5 historias
├── research.md          # Fase 0: las mediciones y las decisiones que fijan
├── data-model.md        # Fase 1: las dos tablas y las entidades
├── quickstart.md        # Fase 1: cómo se valida a mano
├── contracts/
│   └── almacen-de-credenciales.md
├── checklists/
│   └── requirements.md
└── tasks.md             # Fase 2, la escribe /speckit-tasks
```

### Código

```text
src/CafManagerConection.Domain/Credentials/
├── ParametrosDeDerivacion.cs       # hash, sal, iteraciones. Sin paralelismo: PBKDF2 no tiene
├── PoliticaDeClaveMaestra.cs       # FR-211 a FR-214: forma y fuerza. Sin E/S, todo verificable
├── SobreCifrado.cs                 # nonce + texto cifrado, con su serialización
└── EstadoDelVault.cs               # sin crear / bloqueado / desbloqueado, y las transiciones

src/CafManagerConection.UseCases/Credentials/
├── Vault.cs                        # crear, desbloquear, bloquear, cambiar la clave maestra
├── VaultCredentialStore.cs         # implementa ICredentialStore contra el vault
└── MigradorDeCredenciales.cs       # 0.1.1: del Administrador al vault (FR-260 a FR-265)

src/CafManagerConection.Infrastructure/Credentials/
├── DerivacionPbkdf2.cs             # el único lugar que llama a Rfc2898DeriveBytes
├── CifradoAesGcm.cs                # el único lugar que toca AesGcm
├── DispositivoRecordadoDpapi.cs    # P/Invoke a crypt32; el único lugar que toca DPAPI
├── RepositorioDelVault.cs          # Dapper contra las dos tablas nuevas
└── WindowsCredentialStore.cs       # se conserva: es la fuente del migrador

src/CafManagerConection.Infrastructure/Database/Migrations/
└── Migration007_Vault.cs           # ⛔ BLOQUEADA por la puerta de esquema

src/CafManagerConection.App/Views/
├── ClaveMaestraWindow.xaml(.cs)    # crear y desbloquear, con medidor de fuerza
└── CambiarClaveMaestraWindow.xaml(.cs)

tests/  — un archivo espejo por cada uno de los de arriba
```

**Structure Decision**: no se agrega ningún proyecto a la solución ni una dependencia. Las cuatro
capas que toca ya existen y cada pieza cae donde el Principio I la manda: lo que no hace E/S en
`Domain`, la orquestación en `UseCases`, y cada primitiva criptográfica encapsulada en un solo
archivo de `Infrastructure`, para que cambiar el KDF más adelante sea cambiar un archivo y una
fila de la tabla del vault.

## Complexity Tracking

**Sin filas.** El Constitution Check no dejó ninguna violación que justificar.

Un borrador de este plan traía dos dependencias nuevas —`Konscious.Security.Cryptography.Argon2`
para Argon2id y `System.Security.Cryptography.ProtectedData` para DPAPI— y las justificaba acá. El
usuario las rechazó: «si no está en .NET 10 usá otra cosa, bajá el nivel de seguridad, no es un
sistema de la NASA». Las dos se resolvieron con el BCL y un P/Invoke, así que la tabla quedó vacía
en lugar de justificada, que es mejor. El costo de seguridad de ese cambio está medido en
[research.md](./research.md) y dicho en el Principio II, no escondido acá.

## Fase 0 — Investigación

Cerrada. Todo lo que había que decidir tenía respuesta medible y se midió en este equipo, de 12
núcleos. Los números completos están en [research.md](./research.md). El resumen:

- **PBKDF2-HMAC-SHA512 con 600.000 iteraciones**, que cuesta 408 ms. El piso de la constitución son
  600.000 y subirlo no exige enmienda. SHA-512 y no SHA-256: al mismo costo para nosotros, castiga
  algo más al atacante con GPU, porque sus operaciones de 64 bits rinden peor ahí.
- **Se usa la sobrecarga `Pbkdf2(ReadOnlySpan<char>, ReadOnlySpan<byte>, Span<byte>, int,
  HashAlgorithmName)`**, que escribe en un búfer propio. La sobrecarga que toma `string` está
  PROHIBIDA en esta feature: una clave maestra en un `string` queda inmortal en el montón y no se
  puede pisar, lo que rompe FR-218. Es la trampa más fácil de pisar de todo el diseño y por eso
  entra como prueba, no como comentario.
- **La etiqueta de AES-GCM se fija en 16 bytes y el nonce en 12.** El nonce no es una elección:
  `AesGcm.NonceByteSizes` es 12..12 en esta plataforma, así que FR-201 describe lo único posible.
- **DPAPI por P/Invoke funciona**: 32 bytes dan un blob de 178, y un blob con un byte cambiado se
  rechaza con `CryptographicException`.
- **Desapareció una trampa que el borrador con Argon2id tenía.** Argon2 incluye el paralelismo en el
  cálculo, así que tomarlo de `Environment.ProcessorCount` habría hecho que el vault no se abriera
  en una máquina con otra cantidad de núcleos —justo lo que FR-250 exige—. PBKDF2 no tiene ese
  parámetro: se guardan hash, sal e iteraciones, y nada depende del equipo.

## Fase 1 — Diseño

Cerrada. Los artefactos:

- [data-model.md](./data-model.md) — las dos tablas, las entidades y las transiciones de estado.
- [contracts/almacen-de-credenciales.md](./contracts/almacen-de-credenciales.md) — el contrato de
  `ICredentialStore` visto desde el vault, con lo que cambia de significado respecto del
  Administrador de credenciales.
- [quickstart.md](./quickstart.md) — cómo se valida a mano, incluida la prueba de portabilidad
  entre perfiles de Windows, que no se puede automatizar en este equipo.

### Revalidación de la puerta constitucional después del diseño

| Principio | Estado | Nota |
|-----------|--------|------|
| I | ✅ | Las tres primitivas —KDF, AES-GCM y DPAPI— quedaron en un archivo cada una, en `Infrastructure`. `Domain` no toca ninguna. |
| II | ✅ | El diseño cumple los 54 FR. Los dos hallazgos que la fase de diseño agregó y que no eran obvios en la spec: la sobrecarga de `Pbkdf2` que toma `string` rompe FR-218, y la copia de seguridad tiene que correr antes de migrar. |
| III | ✅ | `PoliticaDeClaveMaestra`, `ParametrosDeDerivacion` y `SobreCifrado` son puros y se prueban primero. |
| IV | ✅ | Cero dependencias nuevas después del rechazo del usuario. |
| V | ✅ | Sin proyectos nuevos, sin alcance nuevo. |
| VI | ✅ | Sin privilegios, sin servicios. |
| **Puerta de esquema** | ⛔ | **Sigue abierta.** Es lo único que bloquea, y bloquea sólo `Migration007_Vault.cs`. |

## Lo que este plan deja pendiente a propósito

- **La confirmación de la puerta de esquema.** Es del usuario.
- **El valor por omisión del desbloqueo automático** quedó en «se elige al crear el vault, apagado
  si no se elige», anotado en los supuestos de la spec. Se preguntó y no hubo respuesta.
- ~~Qué hace hoy la 0.1.0 con una base de versión mayor.~~ **Resuelto durante el diseño, y es un
  defecto.** `DatabaseInitializer.Migrate()` (`DatabaseInitializer.cs:78`) filtra
  `Migrations.Where(m => m.Version > from)`; con la base más nueva que la aplicación ese filtro no
  devuelve nada y el método retorna normalmente. Entró como FR-268 y SC-071.
- **US5, cambiar la clave maestra**, no estaba en el pedido. Se puede sacar sin tocar las otras
  cuatro historias.
