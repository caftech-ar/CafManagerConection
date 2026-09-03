---

description: "Fase 1: entidades, tablas y transiciones del vault"
---

# Modelo de datos

## Entidades del dominio

### `ParametrosDeDerivacion`

Lo que hace falta para volver a derivar la misma clave envolvente a partir de la clave maestra.
Se guarda **en claro**: no hay nada secreto acá y sin esto el vault no se puede abrir nunca más.

| Campo | Tipo | Regla |
|---|---|---|
| `Hash` | texto | `SHA512`. Se guarda el nombre para poder subir de función sin romper lo viejo. |
| `Sal` | 16 bytes | De `RandomNumberGenerator`. Una por vault, generada al crearlo. |
| `Iteraciones` | entero | ≥ 600.000 (FR-205). |

No hay campo de paralelismo, y eso es a propósito: PBKDF2 no lo tiene, así que **nada de la
derivación depende del equipo**. Ver D5 en [research.md](./research.md).

### `SobreCifrado`

Un texto cifrado con su nonce. Es el único envase que sale de `CifradoAesGcm`.

| Campo | Tipo | Regla |
|---|---|---|
| `Nonce` | 12 bytes | Nuevo en cada cifrado (FR-201). Los 12 no son elección: `AesGcm.NonceByteSizes` es 12..12. |
| `Cifrado` | bytes | Incluye la etiqueta de 16 bytes al final. |

Invariante que se prueba: dos cifrados del mismo texto con la misma clave **nunca** dan el mismo
nonce. Es la única forma de detectar por prueba que alguien cacheó el nonce.

### `PoliticaDeClaveMaestra`

Pura, sin E/S, y es la entidad más fácil de probar de toda la feature. Responde dos cosas: si una
clave maestra cumple el mínimo (FR-211), y qué fuerza tiene para el medidor (FR-214).

Reglas: 8 caracteres o más; al menos una letra, al menos un dígito, al menos un carácter especial;
se aceptan hasta 128 o más caracteres sin recortar; se acepta cualquier carácter Unicode, incluido
el espacio (FR-213).

### `EstadoDelVault`

```text
SinCrear ──crear──▶ Desbloqueado ──bloquear──▶ Bloqueado ──clave maestra──▶ Desbloqueado
                                                    ▲                            │
                                                    └────── cerrar la app ────────┘

Bloqueado ──dispositivo recordado y NO desarmado──▶ Desbloqueado
```

Tres reglas de las transiciones, todas verificables:

1. De `Bloqueado` a `Desbloqueado` por el dispositivo recordado **sólo** si no hubo un bloqueo
   manual desde el último desbloqueo con clave maestra (FR-238). El desarme es una marca en memoria,
   no en disco: cerrar y abrir la aplicación vuelve a habilitar el desbloqueo automático.
2. Al entrar en `Bloqueado` se pisan con ceros la clave envolvente, la clave del vault y toda
   credencial descifrada que estuviera viva (FR-218, FR-231).
3. `SinCrear` no acepta guardar credenciales. No hay estado «desbloqueado sin vault».

## Esquema — migración 007

⛔ **Bloqueada por la puerta de esquema.** La justificación está en
[plan.md](./plan.md#puerta-de-esquema-justificación-de-la-migración-007) y falta la confirmación del
usuario. Esto es el diseño, no el archivo.

```sql
CREATE TABLE vault (
    id                INTEGER PRIMARY KEY CHECK (id = 1),
    formato           INTEGER NOT NULL,
    kdf_hash          TEXT    NOT NULL,
    kdf_sal           BLOB    NOT NULL,
    kdf_iteraciones   INTEGER NOT NULL,
    clave_nonce       BLOB    NOT NULL,
    clave_envuelta    BLOB    NOT NULL,
    verificador_nonce BLOB    NOT NULL,
    verificador       BLOB    NOT NULL,
    creado_en         TEXT    NOT NULL
);

CREATE TABLE vault_credenciales (
    clave        TEXT PRIMARY KEY,
    usuario      TEXT NOT NULL,
    dominio      TEXT NULL,
    secreto_nonce BLOB NOT NULL,
    secreto      BLOB NOT NULL,
    guardado_en  TEXT NOT NULL
);
```

### Por qué así

- **`CHECK (id = 1)`**: hay un vault y sólo uno. Sin esa restricción, un defecto que inserte una
  segunda fila deja la base con dos claves envueltas y ninguna forma de saber cuál es la buena.
- **`formato`**: la versión del formato del vault, separada de `user_version` de SQLite. Sirven a
  cosas distintas: `user_version` dice qué tablas hay, `formato` dice cómo está cifrado lo que hay
  adentro. Subir el KDF más adelante toca `formato`, no el esquema.
- **`vault_credenciales.clave`** es la **misma cadena `cmc:*`** que hoy vive en
  `connections.rdp_credential_key`, `ssh_credential_key`, `web_credential_key` y
  `connection_folders.credential_key`. Esas cuatro columnas **no se tocan**: siguen siendo la clave
  lógica de la herencia. Es lo que deja intactos `SettingsResolver` y `CredentialProvider`.
- **El nonce en su propia columna** y no pegado al texto cifrado: es lo que hace que una prueba
  pueda leer los nonces de todas las filas y afirmar que no se repite ninguno.
- **Nada de `ON DELETE CASCADE` desde `connections`**: borrar una conexión ya borra su credencial
  por código (FR-038 hoy, FR-037 en la spec nueva), y una cascada que borre un secreto sin pasar por
  el código deja el vault sin registro de qué se fue.

### El verificador

Un texto conocido y fijo, cifrado con la clave envolvente al crear el vault. Desbloquear es
descifrarlo: si la etiqueta de AES-GCM no autentica, la clave maestra es otra. **No se guarda
ningún hash de la clave maestra** (FR-207): eso sería material atacable offline sin tocar el resto
del vault.

Consecuencia que hay que distinguir en los mensajes, y que es la razón de FR-256: si el verificador
autentica pero la `clave_envuelta` no, la clave maestra es correcta y el vault está dañado. Decirle
al usuario «clave incorrecta» en ese caso lo manda a probar contraseñas durante una hora en lugar de
ir a la copia de seguridad.

## Un defecto que este diseño destapó

`DatabaseInitializer.Migrate()` (`src/CafManagerConection.Infrastructure/Database/DatabaseInitializer.cs:78`)
aplica `Migrations.Where(m => m.Version > from)`. Cuando la base es **más nueva** que la aplicación,
ese filtro no devuelve nada, `applied` queda en `false` y el método **retorna normalmente**: la
aplicación vieja abre la base nueva y sigue trabajando.

Hoy no molesta porque todas las migraciones fueron aditivas. Con el vault sí molesta, y de una forma
que no se ve venir: una 0.1.0 abriendo una base ya migrada por la 0.1.1 no conoce las tablas del
vault, así que busca las credenciales en el Administrador —que el migrador ya vació— y concluye que
ninguna conexión tiene credencial. Ofrece guardarlas de nuevo, y **vuelve a escribir secretos en el
Administrador de credenciales**, que es justo el almacén del que estamos saliendo. No se destruye
nada, pero el usuario queda con la mitad de las credenciales en cada lado y sin saber por qué.

Es un defecto del código que ya existe, no de esta feature, y esta feature es la que lo vuelve
peligroso. Entra como requisito: **abrir una base con `user_version` mayor que la última migración
conocida tiene que abortar con un mensaje que nombre las dos versiones**, en lugar de seguir.
