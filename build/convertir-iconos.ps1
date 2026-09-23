#requires -Version 7
<#
.SYNOPSIS
    Convierte los SVG de Assets/Iconos en diccionarios de StreamGeometry para WPF.

.DESCRIPTION
    La fuente de cada icono es su SVG; los Themes/Iconos.*.xaml que este script escribe son
    derivados y se regeneran. Se corre a mano cuando cambian los iconos, no en cada compilación:
    el XAML generado se versiona, así el build y el diseñador no dependen de este script.

    Un icono de trazo —los de Tabler— trae varios recorridos que comparten grosor, remate y unión,
    y se concatenan en una sola geometría. Uno de relleno —Simple Icons, Devicon— trae la silueta
    maciza. El prefijo F1 fija el relleno Nonzero: con el EvenOdd de WPF cada trazo interno abre un
    agujero.

.PARAMETER Verificar
    No escribe: falla si algún XAML generado quedó distinto de lo que produciría esta corrida.
    Es lo que corre la prueba que vigila que el generado siga los SVG.
#>
[CmdletBinding()]
param(
    [string]$RaizDeIconos = (Join-Path $PSScriptRoot '..' 'src' 'CafManagerConection.App' 'Assets' 'Iconos'),
    [string]$RaizDeTemas = (Join-Path $PSScriptRoot '..' 'src' 'CafManagerConection.App' 'Themes'),
    [switch]$Verificar
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RECORRIDO_DEL_LIENZO = 'M0 0h24v24H0z'

function ConvertTo-NombreDeRecurso {
    <#
    .SYNOPSIS
        Convierte un nombre de carpeta con guiones en el sufijo PascalCase del diccionario.
    #>
    param([Parameter(Mandatory)][string]$Carpeta)

    ($Carpeta -split '-' | ForEach-Object {
        if ($_.Length -eq 0) { '' } else { $_.Substring(0, 1).ToUpperInvariant() + $_.Substring(1) }
    }) -join ''
}

function Read-Icono {
    <#
    .SYNOPSIS
        Lee un SVG y devuelve su clave, su origen, su modo de pintado y su geometría.
    .PARAMETER Archivo
        El SVG a leer.
    .OUTPUTS
        Objeto con Clave, Origen, Modo y Geometria.
    #>
    param([Parameter(Mandatory)][System.IO.FileInfo]$Archivo)

    $texto = [System.IO.File]::ReadAllText($Archivo.FullName)

    $origen = [regex]::Match($texto, '<!--\s*(?<o>.+?)\s*-->').Groups['o'].Value
    if (-not $origen) {
        throw "«$($Archivo.Name)» no declara su origen. Todo SVG lleva arriba su comentario de procedencia."
    }

    $cabecera = [regex]::Match($texto, '<svg\b[^>]*>', 'Singleline').Value
    $modo = if ($cabecera -match 'stroke="currentColor"') { 'Trazo' } else { 'Relleno' }

    $recorridos = @([regex]::Matches($texto, '<path\b[^>]*?\bd="(?<d>[^"]*)"[^>]*>') |
        ForEach-Object { $_.Groups['d'].Value.Trim() } |
        Where-Object { $_ -and -not $_.StartsWith($RECORRIDO_DEL_LIENZO) })

    if ($recorridos.Count -eq 0) {
        throw "«$($Archivo.Name)» no tiene ningún recorrido que dibujar."
    }

    if ($modo -eq 'Trazo') {
        foreach ($clave in 'stroke-width', 'stroke-linecap', 'stroke-linejoin') {
            if ($cabecera -notmatch "$clave=") {
                throw "«$($Archivo.Name)» dibuja por trazo pero no declara $clave en el <svg>: sus recorridos no se pueden unir."
            }
        }
    }

    $propios = @([regex]::Matches($texto, '<path\b(?<attrs>[^>]*)>') |
        ForEach-Object { [regex]::Matches($_.Groups['attrs'].Value, '\b(?<n>[a-zA-Z-]+)="') } |
        ForEach-Object { $_ } |
        ForEach-Object { $_.Groups['n'].Value } |
        Where-Object { $_ -notin 'd', 'fill', 'stroke' } |
        Select-Object -Unique)

    if ($propios.Count -gt 0) {
        throw "«$($Archivo.Name)» trae atributos por recorrido ($($propios -join ', ')): no se pueden unir en una geometría."
    }

    [pscustomobject]@{
        Clave     = [System.IO.Path]::GetFileNameWithoutExtension($Archivo.Name)
        Origen    = $origen
        Modo      = $modo
        Geometria = 'F1 ' + ($recorridos -join ' ')
    }
}

function Write-Diccionario {
    <#
    .SYNOPSIS
        Arma el texto del ResourceDictionary de un grupo.
    .PARAMETER Iconos
        Los iconos ya leídos del grupo.
    .OUTPUTS
        El XAML como una sola cadena.
    #>
    param(
        [Parameter(Mandatory)][object[]]$Iconos
    )

    $lineas = [System.Collections.Generic.List[string]]::new()
    $lineas.Add('<!-- Generado por build/convertir-iconos.ps1 desde Assets/Iconos. No editar a mano. -->')
    $lineas.Add('<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"')
    $lineas.Add('                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">')
    $lineas.Add('')

    foreach ($icono in $Iconos | Sort-Object Clave) {
        $lineas.Add("    <!-- $($icono.Origen) -->")
        $lineas.Add("    <StreamGeometry x:Key=""Icono.$($icono.Clave)"">$($icono.Geometria)</StreamGeometry>")
        $lineas.Add('')
    }

    $lineas.Add('</ResourceDictionary>')
    ($lineas -join "`n") + "`n"
}

$carpetas = Get-ChildItem -Path $RaizDeIconos -Directory -Recurse |
    Where-Object { Get-ChildItem -Path $_.FullName -Filter '*.svg' -File }

if (-not $carpetas) {
    throw "No hay ningún SVG bajo «$RaizDeIconos»."
}

$diferencias = [System.Collections.Generic.List[string]]::new()
$escritos = @{}
$totalDeIconos = 0

foreach ($carpeta in $carpetas) {
    $iconos = @(Get-ChildItem -Path $carpeta.FullName -Filter '*.svg' -File | ForEach-Object { Read-Icono $_ })
    $totalDeIconos += $iconos.Count

    $nombre = "Iconos.$(ConvertTo-NombreDeRecurso $carpeta.Name).xaml"

    # El barrido es recursivo y el nombre sale de la hoja: dos grupos homónimos en ramas distintas
    # escribirían el mismo archivo y el segundo pisaría al primero sin que nadie se entere.
    if ($escritos.ContainsKey($nombre)) {
        throw "«$($carpeta.FullName)» y «$($escritos[$nombre])» producen el mismo diccionario «$nombre». Renombrá uno de los dos grupos."
    }

    $escritos[$nombre] = $carpeta.FullName

    $destino = Join-Path $RaizDeTemas $nombre
    $contenido = Write-Diccionario -Iconos $iconos

    if ($Verificar) {
        $actual = if (Test-Path $destino) { [System.IO.File]::ReadAllText($destino) } else { '' }
        if ($actual -ne $contenido) { $diferencias.Add($nombre) }
        continue
    }

    [System.IO.File]::WriteAllText($destino, $contenido, [System.Text.UTF8Encoding]::new($false))
    Write-Host ("  {0,-46} {1,3} iconos" -f $nombre, $iconos.Count)
}

if ($Verificar) {
    if ($diferencias.Count -gt 0) {
        throw "Estos diccionarios no coinciden con sus SVG: $($diferencias -join ', '). Corré build/convertir-iconos.ps1."
    }
    Write-Host "Los $totalDeIconos iconos están al día."
    return
}

Write-Host ''
Write-Host "$totalDeIconos iconos en $($carpetas.Count) diccionarios."
