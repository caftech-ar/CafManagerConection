#requires -Version 7
<#
.SYNOPSIS
    Verifica que ningún secreto haya llegado a los registros, ni a la base cuando hay clave maestra.

.DESCRIPTION
    Los registros nunca pueden contener un secreto, haya clave maestra o no: ahí un hallazgo es
    siempre una filtración.

    La base depende del modo. Con clave maestra el secreto está cifrado y encontrarlo en claro es
    una filtración. Sin clave maestra está en claro a propósito, así que encontrarlo es lo esperado
    y no detiene nada.

    Busca los términos que se le pasen, más algunos patrones que nunca deberían aparecer.

.EXAMPLE
    ./scripts/auditar-secretos.ps1 MiContraseña123
#>
param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$Terminos
)

$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'sqlite.ps1')

$root = Join-Path $env:LOCALAPPDATA 'CafManagerConection'
$db = Join-Path $root 'cmc.db'
$logs = Join-Path $root 'logs'

Write-Host ''
Write-Host 'Auditoría de secretos' -ForegroundColor Cyan

if (-not (Test-Path $db)) {
    Write-Host '  No hay base todavía. Ejecutá la aplicación primero.' -ForegroundColor Yellow
    exit 0
}

$cn = Open-CmcDb -Db $db -Raiz (Join-Path $PSScriptRoot '..')

if (-not $cn) {
    exit 1
}

try {
    $conClave = Test-CmcClaveMaestra -Conexion $cn
}
finally {
    $cn.Close()
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

$archivosDeRegistro = @()
if (Test-Path $logs) {
    $archivosDeRegistro = @((Get-ChildItem $logs -Filter *.log -ErrorAction SilentlyContinue).FullName)
}

Write-Host "  Modo:               $(if ($conClave) { 'con clave maestra' } else { 'sin clave maestra' })"
Write-Host "  Registros:          $($archivosDeRegistro.Count)"
Write-Host "  Patrones buscados:  $($patrones.Count)"
Write-Host ''

function Find-Patrones {
    param([string[]]$Archivos, [string[]]$Patrones)

    $encontrados = @()

    foreach ($archivo in $Archivos) {
        foreach ($patron in $Patrones) {
            $r = Select-String -Path $archivo -Pattern $patron -SimpleMatch -ErrorAction SilentlyContinue
            if ($r) {
                # Se informa el archivo y el patrón recortado, nunca la línea completa: volcarla
                # sería filtrar el secreto en la salida de la propia auditoría.
                $encontrados += [pscustomobject]@{
                    Archivo = Split-Path $archivo -Leaf
                    Patron  = if ($patron.Length -gt 12) { $patron.Substring(0, 6) + '…' } else { $patron }
                    Veces   = @($r).Count
                }
            }
        }
    }

    return $encontrados
}

$hallazgos = @(Find-Patrones -Archivos $archivosDeRegistro -Patrones $patrones)

if ($conClave) {
    $hallazgos += @(Find-Patrones -Archivos @($db) -Patrones $patrones)
}
else {
    $enLaBase = @(Find-Patrones -Archivos @($db) -Patrones $patrones)

    if ($enLaBase.Count -gt 0) {
        Write-Host '  Sin clave maestra los secretos están en claro en la base, a propósito.' -ForegroundColor DarkGray
        Write-Host '  Poné una clave maestra si el archivo va a salir de este equipo.' -ForegroundColor DarkGray
        Write-Host ''
    }
}

if ($hallazgos.Count -eq 0) {
    Write-Host '  Cero coincidencias donde no puede haberlas. La comprobación pasa.' -ForegroundColor Green
    Write-Host ''
    exit 0
}

Write-Host '  HALLAZGOS: hay secretos donde no debería haberlos.' -ForegroundColor Red
$hallazgos | Format-Table -AutoSize
Write-Host '  Es una filtración de secretos y detiene la entrega.' -ForegroundColor Red
Write-Host ''
exit 1
