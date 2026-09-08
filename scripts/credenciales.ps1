#requires -Version 7
<#
.SYNOPSIS
    Lista dónde guardó CMC una contraseña.

.DESCRIPTION
    Muestra sólo la conexión o la carpeta y el usuario, nunca el secreto. Con clave maestra el
    secreto está cifrado con AES-256-GCM y esta consulta ni siquiera tiene la clave para abrirlo;
    sin clave maestra está en claro, y por eso esta consulta tampoco lo trae.
#>
$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'sqlite.ps1')

$db = Join-Path $env:LOCALAPPDATA 'CafManagerConection\cmc.db'

if (-not (Test-Path $db)) {
    Write-Host 'Todavía no hay base. Ejecutá la aplicación al menos una vez.'
    return
}

$cn = Open-CmcDb -Db $db -Raiz (Join-Path $PSScriptRoot '..')

if (-not $cn) {
    return
}

try {
    $conClave = Test-CmcClaveMaestra -Conexion $cn

    $cmd = $cn.CreateCommand()
    $cmd.CommandText = @'
SELECT 'conexión' AS tipo, protocol AS protocolo, name AS donde, COALESCE(username, '') AS usuario
FROM connections WHERE secreto IS NOT NULL
UNION ALL
SELECT 'carpeta', 'Rdp', f.name, COALESCE(s.username, '')
FROM folder_settings s JOIN connection_folders f ON f.id = s.folder_id
WHERE s.rdp_secreto IS NOT NULL
UNION ALL
SELECT 'carpeta', 'Ssh', f.name, COALESCE(s.username, '')
FROM folder_settings s JOIN connection_folders f ON f.id = s.folder_id
WHERE s.ssh_secreto IS NOT NULL
UNION ALL
SELECT 'carpeta', 'Web', f.name, COALESCE(s.username, '')
FROM folder_settings s JOIN connection_folders f ON f.id = s.folder_id
WHERE s.web_secreto IS NOT NULL
ORDER BY tipo, donde
'@

    $r = $cmd.ExecuteReader()

    $filas = @()
    while ($r.Read()) {
        $filas += [pscustomobject]@{
            Tipo      = $r.GetString(0)
            Protocolo = $r.GetString(1)
            Donde     = $r.GetString(2)
            Usuario   = $r.GetString(3)
        }
    }

    $r.Close()

    if (-not $filas) {
        Write-Host 'Todavía no hay ninguna contraseña guardada.'
        return
    }

    Write-Host "Contraseñas guardadas ($($filas.Count)):" -ForegroundColor Cyan
    $filas | Format-Table -AutoSize

    if ($conClave) {
        Write-Host 'Están cifradas con la clave maestra y no se muestran nunca.' -ForegroundColor DarkGray
    }
    else {
        Write-Host 'NO hay clave maestra: están en claro en la base.' -ForegroundColor Yellow
    }
}
finally {
    $cn.Close()
}
