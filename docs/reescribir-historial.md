# Reescribir el historial y publicar 0.1.0

Los corrés vos. Yo no ejecuto nada que modifique el repositorio.

**Es destructivo y no se puede deshacer desde el remoto.** Los cinco commits actuales desaparecen y
con ellos la release `v0.0.3`. Hacé el paso 0 antes que nada.

---

## 0. Respaldo, por si algo sale mal

```powershell
cd C:\Cosas\CafTech\CafTech\RemoteManager
git bundle create ..\cmc-historial-viejo.bundle --all
```

Deja un archivo con los cinco commits y las etiquetas. Si te arrepentís a mitad de camino, se
recupera con `git clone ..\cmc-historial-viejo.bundle`. **Guardalo fuera del repositorio**: tiene
adentro todo lo que estamos sacando.

## 1. Verificar antes de tocar nada

```powershell
$env:MSBUILDDISABLENODEREUSE = '1'
dotnet build CafManagerConection.slnx -c Release -p:UseSharedCompilation=false
dotnet test  CafManagerConection.slnx -c Release -p:UseSharedCompilation=false --no-build
dotnet build-server shutdown
```

Tiene que dar **0 advertencias, 0 errores y 0 fallas**. Van a ser unas 2.664 pruebas con 26
omitidas: las omitidas son las de integración SSH, que se saltean sin `CMC_SSH_PRUEBA_HOST`. El
número exacto no es el criterio; que no haya ninguna roja, sí.

Entre esas pruebas está `SinDatosRealesTests`, que recorre los 300 y pico archivos del repositorio
buscando direcciones privadas, prefijos IPv6 privados, direcciones MAC de hardware, dominios no
reservados y catorce términos que ya se filtraron una vez. **Si esa pasa, el árbol está limpio.**

Si algo falla, **pará acá** y avisame.

## 2. Un commit inicial nuevo, sin historia

```powershell
git checkout --orphan limpio
git add -A
git commit -F .git\COMMIT_MENSAJE_0.1.0.txt
```

`--orphan` crea una rama sin padre: lo que commitées ahí no tiene historia detrás. El árbol de
trabajo no se toca, así que `git add -A` toma exactamente lo que hay ahora.

Comprobá que quedó **un solo** commit y que están todos los archivos:

```powershell
git log --oneline
git ls-files | Measure-Object -Line
```

## 3. Reemplazar main

```powershell
git branch -D main
git branch -m main
```

La primera borra la rama vieja con sus cinco commits; la segunda renombra `limpio` a `main`.

## 4. Borrar la etiqueta y la release viejas

`v0.0.3` apunta a un commit que ya no existe. Si no se va, queda un puntero al historial viejo y
GitHub lo sigue mostrando.

```powershell
gh release delete v0.0.3 --repo caftech-ar/CafManagerConection --yes --cleanup-tag
git tag -d v0.0.3
```

`--cleanup-tag` borra también la etiqueta en el remoto. Comprobalo:

```powershell
gh release list --repo caftech-ar/CafManagerConection
git ls-remote --tags origin
```

Las dos tienen que salir vacías.

## 5. Subir, pisando lo que hay

```powershell
git push --force-with-lease origin main
```

`--force-with-lease` en lugar de `--force`: si alguien empujó algo al remoto que vos no tenés, se
niega en lugar de borrarlo. Acá no debería pasar, pero es gratis.

## 6. Etiquetar 0.1.0

```powershell
git tag -a v0.1.0 -m "0.1.0"
git push origin v0.1.0
```

**Después** del `push` de `main`, no antes: el flujo de GitHub Actions se dispara con la etiqueta y
compila desde ese commit, así que si la etiqueta llega primero apunta a algo que el remoto no tiene.

## 7. Mirar que la release salga

```powershell
gh run watch --repo caftech-ar/CafManagerConection
```

El flujo compila, corre las pruebas, arma los dos instaladores y el ZIP portable, calcula los tres
`.sha256` y publica. **No hace falta que compiles instaladores a mano.**

Su primer paso verifica que la etiqueta coincida con `<Version>` de `Directory.Build.props` —hoy
`0.1.0`— y aborta si no.

---

## Lo que hay que saber antes

**La nota de la release va a ser una línea.** El flujo la arma con `git log --pretty=format:'- %s'`,
o sea sólo el asunto de cada commit. Con un commit único, la nota queda con ese asunto más el bloque
de «Cuál bajar». El cuerpo del mensaje —que es largo y describe el producto— **no** aparece ahí. Si
querés la descripción completa en la release, editala en GitHub después, o pegá el cuerpo a mano.

**El respaldo del paso 0 tiene los datos viejos.** Es su razón de ser, pero por eso mismo no lo dejes
en una carpeta sincronizada ni lo subas a ningún lado.

**Lo ya publicado sigue publicado.** Reescribir el historial saca el dato de tu repositorio; no lo
borra de una copia que alguien haya clonado, ni de la caché de GitHub, ni de un servicio que archive
repositorios públicos. Por eso la contraseña del contenedor de pruebas ya no existe en el árbol y el
guión la genera al azar en cada arranque.
