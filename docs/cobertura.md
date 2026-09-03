# Cobertura de pruebas por capa

**Fecha**: 2026-08-25 · **Herramienta**: Coverlet (`XPlat Code Coverage`) · **397 pruebas**

Medido con `dotnet test --collect:"XPlat Code Coverage"` sobre los ocho proyectos de pruebas.
Un mismo ensamblado aparece en varios informes —cada proyecto de pruebas arrastra sus
dependencias—, así que se toma la unión: una línea cubierta por cualquier proyecto cuenta como
cubierta.

| Capa | Líneas | Cubiertas | Cobertura |
| --- | ---: | ---: | ---: |
| Monitoring | 319 | 267 | **83,7 %** |
| UseCases | 736 | 587 | **79,8 %** |
| Infrastructure | 960 | 744 | **77,5 %** |
| Platform | 521 | 363 | **69,7 %** |
| Domain | 800 | 511 | **63,9 %** |
| Terminal | 998 | 394 | 39,5 % |
| Rdp | 355 | 51 | 14,4 % |
| Ssh | 705 | 6 | 0,9 % |
| **Total** | **5.394** | **2.923** | **54,2 %** |

## Cómo leer estos números

El total no dice mucho por sí solo. Lo que importa es **dónde** está la cobertura, y acá está
donde tiene que estar: las cuatro capas que concentran las reglas del producto —Monitoring,
UseCases, Infrastructure y Platform— están entre el 70 % y el 84 %.

Las tres de abajo son adaptadores, y su número bajo tiene una causa concreta que conviene
entender antes de intentar subirlo.

### Ssh, 0,9 %

Es casi todo código que sólo se ejercita con una conexión real: apertura de sesión, SFTP,
túneles, reenvío de puertos. Las pruebas que existen cubren la única pieza que es una función
pura, `HostKeyPolicy`, y no por casualidad: se extrajo a propósito de `SshSession` **porque**
la comparación de la clave de host vivía dentro de un manejador de eventos de SSH.NET, en
medio del intercambio de claves, donde ninguna prueba llega. Ese defecto —la aplicación
guardaba el fingerprint y nunca lo comparaba— existió justamente porque esa línea no se podía
probar.

Subir este número exige un contenedor OpenSSH en la batería (T090). Es la tarea pendiente de
mayor valor de las que quedan.

### Rdp, 14,4 %

El grueso es interoperación COM con el ActiveX de Windows, que no se puede instanciar en un
proceso de pruebas sin una ventana y un bucle de mensajes. Lo que sí está cubierto es lo que se
pudo separar: la resolución del CLSID y el mapeo de errores. Esa separación tampoco fue
gratuita —salió de investigar por qué RDP no conectaba nunca— y hoy es lo que detectaría una
regresión en la elección del control.

### Terminal, 39,5 %

Casi la mitad del proyecto es `TerminalControl`: dibujado con GDI, selección con el mouse y
manejo de teclado. Es interfaz, y entra en la excepción declarada del Principio III. El
emulador VT y el búfer —que es donde vive la lógica— están bien cubiertos por las 52 pruebas
del proyecto.

## Dónde falta de verdad

**Domain al 63,9 % es el número que más llama la atención**, porque es la capa que el Principio
III exige cubrir primero y no tiene ninguna excusa técnica. La causa es que buena parte del
dominio son propiedades y validaciones de entidades que sólo se ejercitan por el camino que
usa la interfaz. No es urgente, pero es la deuda más barata de pagar de esta lista.

## Reproducir

```powershell
dotnet test --collect:"XPlat Code Coverage" --results-directory $env:TEMP\cov
```

Los informes quedan en formato Cobertura, uno por proyecto de pruebas.
