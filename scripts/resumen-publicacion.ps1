#requires -Version 7
<#
.SYNOPSIS
    Informa el resultado de la publicación: dónde quedó el ZIP y cuánto pesa.
#>
param([Parameter(Mandatory)][string]$Zip)

if (-not (Test-Path $Zip)) {
    Write-Host "No se encontró el paquete en $Zip" -ForegroundColor Red
    exit 1
}

$item = Get-Item $Zip
$mb = [math]::Round($item.Length / 1MB, 1)

Write-Host ''
Write-Host 'Publicación lista' -ForegroundColor Green
Write-Host "  Paquete   $($item.FullName)"
Write-Host "  Tamaño    $mb MB"
Write-Host ''
Write-Host '  Descomprimir y ejecutar cmc.exe.'
Write-Host '  No requiere .NET instalado ni privilegios de administrador.'
Write-Host ''
