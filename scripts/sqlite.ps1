#requires -Version 7
<#
.SYNOPSIS
    Abre cmc.db en sólo lectura desde PowerShell.

.DESCRIPTION
    Lo comparten los guiones que consultan la base. Devuelve la conexión abierta, o $null con el
    motivo ya impreso.
#>

function Open-CmcDb {
    param([Parameter(Mandatory)][string]$Db, [Parameter(Mandatory)][string]$Raiz)

    $bin = Join-Path $Raiz 'src\CafManagerConection.App\bin\Debug\net10.0-windows'

    if (-not (Test-Path (Join-Path $bin 'Microsoft.Data.Sqlite.dll'))) {
        Write-Host 'Falta compilar: corré `task build` antes.' -ForegroundColor Yellow
        return $null
    }

    # La nativa e_sqlite3 se resuelve desde el directorio del proceso, no desde donde esté el .dll
    # administrado: sin este Push-Location, Init() falla con «Unable to load DLL 'e_sqlite3'».
    Push-Location $bin
    try {
        Add-Type -Path (Join-Path $bin 'SQLitePCLRaw.core.dll')
        Add-Type -Path (Join-Path $bin 'SQLitePCLRaw.provider.e_sqlite3.dll')
        Add-Type -Path (Join-Path $bin 'SQLitePCLRaw.batteries_v2.dll')
        Add-Type -Path (Join-Path $bin 'Microsoft.Data.Sqlite.dll')
        [SQLitePCL.Batteries_V2]::Init()

        $cn = New-Object Microsoft.Data.Sqlite.SqliteConnection("Data Source=$Db;Mode=ReadOnly")
        $cn.Open()
        return $cn
    }
    finally {
        Pop-Location
    }
}

function Test-CmcClaveMaestra {
    param([Parameter(Mandatory)]$Conexion)

    $cmd = $Conexion.CreateCommand()
    $cmd.CommandText = 'SELECT COUNT(1) FROM vault WHERE id = 1'
    return [int]$cmd.ExecuteScalar() -gt 0
}
