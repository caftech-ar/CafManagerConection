#requires -Version 7
<#
.SYNOPSIS
    Lista las credenciales que CMC guardó en el Administrador de credenciales de Windows.

.DESCRIPTION
    Muestra sólo los nombres de las claves (cmc:*), nunca los secretos: Windows no los expone
    por línea de comandos, y este proyecto tampoco lo haría (Principio II).

    Vive en un archivo y no como una línea dentro del Taskfile por una razón concreta: el
    intérprete de go-task expande `$r` como variable de entorno antes de que PowerShell vea el
    comando, y lo dejaba vacío. El síntoma era un error de sintaxis de PowerShell —«Missing
    condition in if statement»— que no tenía nada que ver con la causa.
#>
$ErrorActionPreference = 'Stop'

$claves = cmdkey /list |
    Select-String 'cmc:' |
    ForEach-Object { $_.Line.Trim() }

if (-not $claves) {
    Write-Host 'CMC todavía no guardó ninguna credencial.'
    return
}

Write-Host "Credenciales guardadas por CMC ($($claves.Count)):" -ForegroundColor Cyan

foreach ($linea in $claves) {
    # De "Destino: LegacyGeneric:target=cmc:ssh:{id}" queda sólo la clave.
    $clave = if ($linea -match 'target=(cmc:.*)$') { $Matches[1] } else { $linea }

    $tipo = switch -Regex ($clave) {
        '^cmc:folder:' { 'carpeta  ' }
        '^cmc:rdp:'    { 'RDP      ' }
        '^cmc:ssh:'    { 'SSH      ' }
        '^cmc:web:'    { 'web      ' }
        default        { '         ' }
    }

    Write-Host "  $tipo $clave"
}

Write-Host ''
Write-Host 'Sólo se muestran los nombres. Los secretos no salen de Windows.' -ForegroundColor DarkGray
