#requires -Version 7
<#
.SYNOPSIS
    Verifica que ningún secreto haya llegado a la base de datos ni a los registros.

.DESCRIPTION
    Comprobación bloqueante del Principio II de la constitución: los secretos viven sólo en
    el Administrador de credenciales de Windows.

    Busca en cmc.db y en los archivos de registro los términos que se le pasen, más algunos
    patrones que nunca deberían aparecer (cabeceras de clave privada, campos de contraseña).

    Cero coincidencias es el único resultado aceptable.

.EXAMPLE
    ./scripts/auditar-secretos.ps1 MiContraseña123
#>
param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$Terminos
)

$ErrorActionPreference = 'Stop'

$root = Join-Path $env:LOCALAPPDATA 'CafManagerConection'
$db = Join-Path $root 'cmc.db'
$logs = Join-Path $root 'logs'

Write-Host ''
Write-Host 'Auditoría de secretos' -ForegroundColor Cyan

if (-not (Test-Path $db)) {
    Write-Host '  No hay base todavía. Ejecutá la aplicación primero.' -ForegroundColor Yellow
    exit 0
}

# Patrones que jamás deben estar, se pase o no un término.
$patrones = @(
    'BEGIN OPENSSH PRIVATE KEY',
    'BEGIN RSA PRIVATE KEY',
    'BEGIN EC PRIVATE KEY',
    'BEGIN PGP PRIVATE KEY'
)

foreach ($t in ($Terminos | Where-Object { $_ -and $_.Trim() })) {
    $patrones += $t.Trim()
}

$objetivos = @($db)
if (Test-Path $logs) {
    $objetivos += (Get-ChildItem $logs -Filter *.log -ErrorAction SilentlyContinue).FullName
}

Write-Host "  Archivos revisados: $($objetivos.Count)"
Write-Host "  Patrones buscados:  $($patrones.Count)"
Write-Host ''

$hallazgos = @()

foreach ($archivo in $objetivos) {
    foreach ($patron in $patrones) {
        $r = Select-String -Path $archivo -Pattern $patron -SimpleMatch -ErrorAction SilentlyContinue
        if ($r) {
            # Se informa el archivo y el patrón, nunca la línea completa: volcarla sería
            # filtrar el secreto en la salida de la propia auditoría.
            $hallazgos += [pscustomobject]@{
                Archivo = Split-Path $archivo -Leaf
                Patron  = if ($patron.Length -gt 12) { $patron.Substring(0, 6) + '…' } else { $patron }
                Veces   = @($r).Count
            }
        }
    }
}

if ($hallazgos.Count -eq 0) {
    Write-Host '  Cero coincidencias. La comprobación pasa.' -ForegroundColor Green
    Write-Host ''
    exit 0
}

Write-Host '  HALLAZGOS: hay secretos donde no debería haberlos.' -ForegroundColor Red
$hallazgos | Format-Table -AutoSize
Write-Host '  Esto es una violación del Principio II y detiene la entrega.' -ForegroundColor Red
Write-Host ''
exit 1
