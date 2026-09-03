#requires -Version 7
<#
.SYNOPSIS
    Lista las ventanas que abre la aplicación, con su clase y su tamaño.

.DESCRIPTION
    Sirve para diagnosticar diálogos que no aparecen, y también para comprobar que uno NO
    aparece: por ejemplo que la verificación del fingerprint no vuelva a preguntar en un
    servidor ya conocido. Admite abrir una conexión con doble clic antes de mirar.
#>
param(
    [string[]]$Teclas = @(),

    # Dobles clics antes de listar, en coordenadas relativas a la ventana: 'x,y'.
    [string[]]$DoblesClics = @(),

    [int]$EsperaMs = 5000,

    # Espera después de los clics, antes de listar. Una sesión SSH tarda varios segundos.
    [int]$EsperaFinalMs = 3000
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms

$src = @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
public static class Enumerador {
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr p);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowTextW(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassNameW(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] static extern void mouse_event(uint f, uint x, uint y, uint d, IntPtr e);
    [DllImport("user32.dll")] static extern void keybd_event(byte k, byte s, uint f, IntPtr e);
    delegate bool EnumProc(IntPtr h, IntPtr p);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }

    /// Windows sólo deja robar el primer plano a un proceso que acaba de recibir entrada del
    /// usuario. Un ALT suelto satisface esa condición.
    public static void Enfocar(IntPtr h) {
        keybd_event(0x12, 0, 0, IntPtr.Zero);
        keybd_event(0x12, 0, 2, IntPtr.Zero);
        SetForegroundWindow(h);
        System.Threading.Thread.Sleep(250);
    }

    /// Dos pulsaciones dentro del umbral de doble clic del sistema.
    public static void DoubleClick(int x, int y) {
        SetCursorPos(x, y);
        System.Threading.Thread.Sleep(120);
        mouse_event(0x02, 0, 0, 0, IntPtr.Zero);
        mouse_event(0x04, 0, 0, 0, IntPtr.Zero);
        System.Threading.Thread.Sleep(60);
        mouse_event(0x02, 0, 0, 0, IntPtr.Zero);
        mouse_event(0x04, 0, 0, 0, IntPtr.Zero);
    }

    public static bool Rect(IntPtr h, out RECT r) { return GetWindowRect(h, out r); }

    public static List<string> Listar(uint pid) {
        var salida = new List<string>();
        EnumWindows((h, _) => {
            uint p2; GetWindowThreadProcessId(h, out p2);
            if (p2 != pid) return true;
            var t = new StringBuilder(256); GetWindowTextW(h, t, 256);
            var c = new StringBuilder(256); GetClassNameW(h, c, 256);
            RECT r; GetWindowRect(h, out r);
            salida.Add(string.Format("visible={0,-5} {1,5}x{2,-5} clase={3,-46} titulo='{4}'",
                IsWindowVisible(h), r.Right - r.Left, r.Bottom - r.Top, c, t));
            return true;
        }, IntPtr.Zero);
        return salida;
    }
}
'@
if (-not ('Enumerador' -as [type])) { Add-Type -TypeDefinition $src }

$exe = Join-Path $PSScriptRoot '..\src\CafManagerConection.App\bin\Debug\net10.0-windows\cmc.exe'
if (-not (Test-Path $exe)) { throw "No se encontró el ejecutable. Corré: task build" }

$p = Start-Process $exe -PassThru
try {
    while ($p.MainWindowHandle -eq 0) { Start-Sleep -Milliseconds 200; $p.Refresh() }
    Start-Sleep -Milliseconds $EsperaMs

    $r = New-Object Enumerador+RECT
    [Enumerador]::Rect($p.MainWindowHandle, [ref]$r) | Out-Null

    foreach ($c in $DoblesClics) {
        [Enumerador]::Enfocar($p.MainWindowHandle)
        $partes = $c -split ','
        [Enumerador]::DoubleClick($r.Left + [int]$partes[0], $r.Top + [int]$partes[1])
        Start-Sleep -Milliseconds 1200
    }

    foreach ($t in $Teclas) {
        [Enumerador]::Enfocar($p.MainWindowHandle)
        [System.Windows.Forms.SendKeys]::SendWait($t)
        Start-Sleep -Milliseconds 1500
    }

    Start-Sleep -Milliseconds $EsperaFinalMs

    Write-Host "Ventanas del proceso $($p.Id):" -ForegroundColor Cyan
    [Enumerador]::Listar($p.Id) | ForEach-Object { Write-Host "  $_" }
}
finally {
    if (-not $p.HasExited) { Stop-Process -Id $p.Id -Force }
}
