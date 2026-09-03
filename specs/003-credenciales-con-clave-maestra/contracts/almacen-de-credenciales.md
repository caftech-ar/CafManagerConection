---

description: "Contrato de ICredentialStore visto desde el vault"
---

# Contrato: `ICredentialStore` contra el vault

La interfaz **no cambia**. Vive en
`src/CafManagerConection.UseCases/Abstractions/ICredentialStore.cs:7` y su comentario ya decía
«Único lugar donde puede vivir un secreto (Principio II)». Lo que cambia es qué significa cada
método cuando detrás hay un vault en lugar del Administrador de credenciales, y ese cambio de
significado es lo que hay que respetar para que los nueve consumidores sigan funcionando sin
tocarse.

## Lo que cada método significa ahora

| Método | Con el Administrador (1.x) | Con el vault (2.1.0) |
|---|---|---|
| `ReadAsync` | `null` si no existe la entrada | `null` si no existe la fila. **Lanza si el vault está bloqueado.** |
| `WriteAsync` | escribe la entrada | cifra y escribe la fila. **Lanza si el vault está bloqueado.** |
| `DeleteAsync` | borra la entrada | borra la fila. Borrar lo que no existe no es un error, igual que antes. |
| `ExistsAsync` | consulta la entrada | consulta la fila. **Funciona con el vault bloqueado**: saber que hay una credencial guardada no es leerla. |
| `EnumerateKeysAsync` | los nombres de las claves, nunca el secreto | los valores de la columna `clave`, nunca el secreto. **Funciona con el vault bloqueado**, por el mismo motivo. |

## Las cinco reglas del contrato

1. **`ReadAsync` y `WriteAsync` exigen el vault desbloqueado.** No devuelven `null` ni silencio
   cuando está bloqueado: eso haría que la aplicación creyera que no hay credencial guardada y
   ofreciera guardarla de nuevo, que es exactamente el defecto que FR-268 describe para la versión
   vieja. Lanzan una excepción propia que el llamador distingue de «no existe».
2. **`ExistsAsync` y `EnumerateKeysAsync` NO exigen el vault desbloqueado.** Es lo que sostiene
   FR-219: con el vault cerrado, la aplicación tiene que poder mostrar el árbol y decir qué
   conexiones tienen credencial guardada, sin poder leerla. La distinción entre estos dos grupos es
   la decisión de diseño central de este contrato.
3. **`null` de `ReadAsync` sigue significando lo mismo que antes**: no existe, no es un error, y
   dispara el pedido al usuario. Ese comportamiento es de lo que depende FR-039 desde la 001 y no se
   toca.
4. **`WriteAsync` genera un nonce nuevo cada vez**, incluso al sobrescribir la misma clave. Escribir
   dos veces el mismo secreto en la misma clave produce dos textos cifrados distintos.
5. **Ningún método devuelve nunca un secreto en un `string`.** `StoredCredential` ya guarda el
   secreto en un `char[]` que se pisa al desecharlo
   (`src/CafManagerConection.Domain/Credentials/StoredCredential.cs`), y ese contrato se mantiene.

## Quién queda del otro lado

- **`VaultCredentialStore`** es la implementación de producción.
- **`WindowsCredentialStore`** se conserva sin cambios, y deja de estar registrada como
  `ICredentialStore`. El único que la usa es `MigradorDeCredenciales`, que la toma por su tipo
  concreto. Es lo que evita que un `ICredentialStore` inyectado por error vuelva a escribir en el
  Administrador.

## Lo que este contrato le ahorra a la feature

Nueve consumidores resuelven credenciales a través de `ICredentialProvider` y de la herencia de
carpetas. Ninguno se toca. Lo que sí hay que revisar, uno por uno, es **cómo reaccionan a la
excepción de vault bloqueado**, porque antes ese caso no existía: los cuatro caminos que FR-273 y
FR-274 nombran —importar sesiones, copiar al portapapeles, abrir una herramienta externa y conectar—
tienen que ofrecer desbloquear en lugar de fallar sin explicación.
