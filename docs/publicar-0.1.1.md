# Publicar 0.1.1 pisando todo el historial

El commit que hay hoy (`5ce2f57`) lleva una línea `Co-Authored-By: Claude` que no corresponde. Esta
secuencia lo borra: el 0.1.1 queda como **único commit** del repositorio y no hay nada anterior.

**Es destructivo.** Hacé el paso 0 antes que nada.

---

## 0. Respaldo

```powershell
cd C:\Cosas\CafTech\CafTech\RemoteManager
git bundle create ..\cmc-antes-de-0.1.1.bundle --all
```

Guardalo fuera del repositorio. Si algo sale mal: `git clone ..\cmc-antes-de-0.1.1.bundle`.

## 1. Verificar

```powershell
$env:MSBUILDDISABLENODEREUSE = '1'
dotnet build CafManagerConection.slnx -c Release -p:UseSharedCompilation=false
dotnet test  CafManagerConection.slnx -c Release -p:UseSharedCompilation=false --no-build
dotnet build-server shutdown
```

**0 advertencias, 0 errores, 0 fallas.** Van a ser 2.856 pruebas con 26 omitidas —las de integración
SSH, que se saltean sin `CMC_SSH_PRUEBA_HOST`—. El número exacto no es el criterio; que no haya
ninguna roja, sí.

Entre ellas corren los dos guardianes que importan acá: `SinDatosRealesTests`, que recorre el
repositorio buscando datos de redes reales, y `DensidadDeComentarioTests`.

Si algo falla, **pará y avisame**.

## 2. Un commit inicial nuevo, sin nada detrás

```powershell
git checkout --orphan limpio
git add -A
git commit -F .git\COMMIT_MENSAJE_0.1.1.txt
```

`--orphan` crea una rama sin padre. Comprobá que quedó **un solo** commit, sin la firma:

```powershell
git log --oneline
git log -1 --format='%b' | Select-String -SimpleMatch 'Claude'
```

La segunda no tiene que devolver nada.

## 3. Reemplazar main

```powershell
git branch -D main
git branch -m main
```

## 4. Borrar la release y las etiquetas viejas

La release `v0.1.0` apunta al commit que estamos borrando, así que su página seguiría mostrándolo.

```powershell
gh release delete v0.1.0 --repo caftech-ar/CafManagerConection --yes --cleanup-tag
git tag -d v0.1.0
git tag -d v0.0.3
```

`--cleanup-tag` borra también la etiqueta del remoto. Comprobalo:

```powershell
gh release list --repo caftech-ar/CafManagerConection
git ls-remote --tags origin
git tag --list
```

Las tres tienen que salir vacías.

## 5. Subir, pisando lo que hay

```powershell
git push --force origin main
```

Acá va `--force` y no `--force-with-lease`: el objetivo es justamente descartar lo que hay en el
remoto, y `--with-lease` se negaría porque la rama nueva no tiene relación con la vieja.

## 6. Etiquetar 0.1.1

```powershell
git tag -a v0.1.1 -m "0.1.1"
git push origin v0.1.1
```

**Después** del `push` de `main`, no antes: la etiqueta dispara el flujo que compila desde ese
commit. Su primer paso verifica que la etiqueta coincida con `<Version>` de `Directory.Build.props`
—hoy `0.1.1`— y aborta si no.

## 7. Mirar que salga

```powershell
gh run watch --repo caftech-ar/CafManagerConection
```

Compila, corre las pruebas, arma los dos instaladores y el ZIP portable, calcula los tres `.sha256`
y publica. No hace falta que compiles instaladores a mano.

---

## Lo que hay que saber

**El commit viejo no desaparece del todo de GitHub.** El `push --force` lo saca de la rama, pero
GitHub deja los commits inalcanzables accesibles por su SHA durante un tiempo: alguien con
`5ce2f57` en la mano todavía puede abrirlo por URL hasta que GitHub haga su recolección. Borrar la
release del paso 4 quita el único lugar donde ese SHA quedaba enlazado. Si querés la garantía dura
de que se fue, hay que pedírselo a GitHub Support; la alternativa es borrar y recrear el
repositorio.

**La nota de la release va a ser una línea.** El flujo la arma con `git log --pretty=format:'- %s'`,
o sea sólo el asunto. Con un commit único queda ese asunto más el bloque de «Cuál bajar». El cuerpo
largo del mensaje no aparece ahí; si lo querés, editá la release en GitHub después.

**Antes de abrir el build nuevo, copiá tu base**: `%LocalAppData%\CafManagerConection`. La
migración 007 se aplica sola al abrir, y la primera ejecución trae al vault las credenciales que
tengas en el Administrador de credenciales de Windows y las borra de ahí después de verificarlas.

**El respaldo del paso 0 contiene el commit con la firma.** Es su razón de ser, pero por eso mismo
no lo dejes en una carpeta sincronizada ni lo subas a ningún lado.
