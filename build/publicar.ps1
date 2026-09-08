#requires -Version 7
<#
.SYNOPSIS
    Publica CafManagerConection como carpeta portable y genera el ZIP de distribución.

.DESCRIPTION
    Publicación self-contained para win-x64, deliberadamente SIN archivo único y SIN recorte:

      - Archivo único extrae a un directorio temporal, y eso convive mal con la carga de
        mstscax.dll y la activación del control ActiveX de RDP.
      - El recorte rompe WinForms y el interop COM, que dependen de reflexión, y el fallo
        aparece recién en ejecución.

    El paquete pesa más que una aplicación recortada. Es un intercambio consciente:
    previsibilidad sobre tamaño.
#>
[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [string]$OutputRoot = (Join-Path $PSScriptRoot '..' 'publish')
)

$ErrorActionPreference = 'Stop'
$repo = Resolve-Path (Join-Path $PSScriptRoot '..')
$destino = Join-Path $OutputRoot 'CafManagerConection'

Write-Host 'Compilando y ejecutando las pruebas...' -ForegroundColor Cyan
dotnet test (Join-Path $repo 'CafManagerConection.slnx') --configuration $Configuration --nologo
if ($LASTEXITCODE -ne 0) {
    throw 'Las pruebas fallaron. No se publica.'
}

Write-Host 'Publicando...' -ForegroundColor Cyan
if (Test-Path $destino) {
    Remove-Item $destino -Recurse -Force
}

dotnet publish (Join-Path $repo 'src' 'CafManagerConection.App') `
    --configuration $Configuration `
    --runtime win-x64 `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:PublishTrimmed=false `
    --output $destino `
    --nologo

if ($LASTEXITCODE -ne 0) {
    throw 'Falló la publicación.'
}

$zip = Join-Path $OutputRoot 'CafManagerConection-win-x64.zip'
if (Test-Path $zip) {
    Remove-Item $zip -Force
}

Write-Host 'Empaquetando...' -ForegroundColor Cyan
Compress-Archive -Path (Join-Path $destino '*') -DestinationPath $zip

$tamano = [math]::Round((Get-Item $zip).Length / 1MB, 1)
Write-Host ''
Write-Host "Listo." -ForegroundColor Green
Write-Host "  Carpeta portable: $destino"
Write-Host "  ZIP:              $zip ($tamano MB)"
Write-Host ''
Write-Host 'Para usarlo: descomprimir y ejecutar cmc.exe.'
Write-Host 'No requiere .NET instalado ni privilegios de administrador.'
