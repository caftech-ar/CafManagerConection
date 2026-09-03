<#
.SYNOPSIS
    Resume el instalador recien generado.

.DESCRIPTION
    Existe por la misma razon que resumen-publicacion.ps1: el comando que imprime esto lleva dos
    puntos y llaves, y metido a mano en el Taskfile rompe el analisis del YAML.
#>
param(
    [Parameter(Mandatory = $true)]
    [string] $Ruta
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $Ruta)) {
    Write-Host "No se genero el instalador en $Ruta" -ForegroundColor Red
    exit 1
}

$archivo = Get-Item -LiteralPath $Ruta
$mb = [math]::Round($archivo.Length / 1MB, 1)
$version = (Get-Item -LiteralPath $Ruta).VersionInfo.ProductVersion

Write-Host ''
Write-Host "  Instalador  $($archivo.FullName)"
Write-Host "  Tamano      $mb MB"

if ($version) {
    Write-Host "  Version     $version"
}

Write-Host ''
