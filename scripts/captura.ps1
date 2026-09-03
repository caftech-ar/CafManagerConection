#requires -Version 7
<#
.SYNOPSIS
    Levanta la aplicación, expande el árbol y saca una captura de la ventana.

.DESCRIPTION
    Sirve para revisar la interfaz sin tener que abrirla a mano. Espera a que la ventana
    exista, la trae al frente, opcionalmente manda teclas, y guarda un PNG.
#>
param(
    [string]$Salida = 'captura.png',
    [int]$EsperaMs = 4000,
    [string[]]$Teclas = @(),

    # Clics a dar antes de capturar, en coordenadas relativas a la ventana: 'x,y'.
    # Sirve para ejercitar controles dibujados a mano, como los chevrons del árbol, que
    # no son controles de Windows y por eso no se pueden alcanzar con el teclado.
    [string[]]$Clics = @(),

    # Capturar la ventana que tiene el foco en vez de la principal. Hace falta para los
    # diálogos modales, que son ventanas propias y no aparecen en MainWindowHandle.
    [switch]$VentanaActiva,

    # Dobles clics, en el mismo formato 'x,y'. Es lo que activa una conexión del árbol.
    [string[]]$DoblesClics = @(),

    # Espera antes de capturar, después de mandar clics y teclas. Abrir una sesión SSH real
    # tarda varios segundos, y con la espera corta se captura la pantalla anterior.
    [int]$EsperaFinalMs = 700,

    # Clics derechos, mismo formato 'x,y'. Abren los menús contextuales.
    [string[]]$ClicsDerechos = @(),

    # Capturar la pantalla entera. Hace falta para los menús contextuales y los desplegables:
    # son ventanas propias y PrintWindow sobre la principal no los incluye.
    [switch]$PantallaCompleta
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms, System.Drawing

$src = @'
using System;
using System.Runtime.InteropServices;
public static class Win {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] static extern void keybd_event(byte k, byte s, uint f, IntPtr e);

    /// Windows sólo deja robar el primer plano a un proceso que acaba de recibir entrada del
    /// usuario. Un ALT suelto satisface esa condición y hace que SetForegroundWindow funcione
    /// desde un script; sin él la tecla siguiente se pierde según lo que haga el escritorio.
    public static void Enfocar(IntPtr h) {
        keybd_event(0x12, 0, 0, IntPtr.Zero);
        keybd_event(0x12, 0, 2, IntPtr.Zero);
        SetForegroundWindow(h);
        System.Threading.Thread.Sleep(200);
    }
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc cb, IntPtr p);
    private delegate bool EnumProc(IntPtr h, IntPtr p);

    /// Ventana visible del proceso que no sea la principal: el diálogo modal abierto.
    /// Buscarla por PID es más confiable que GetForegroundWindow, que devuelve cualquier
    /// cosa si algo robó el foco entre que se mandó la tecla y que se capturó.
    public static IntPtr DialogoDe(uint pid, IntPtr principal) {
        IntPtr encontrada = IntPtr.Zero;
        EnumWindows((h, _) => {
            uint p2;
            GetWindowThreadProcessId(h, out p2);
            if (p2 == pid && h != principal && IsWindowVisible(h)) {
                RECT r;
                GetWindowRect(h, out r);
                if (r.Right - r.Left > 60 && r.Bottom - r.Top > 40) { encontrada = h; return false; }
            }
            return true;
        }, IntPtr.Zero);
        return encontrada;
    }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int c);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
    public const uint PW_RENDERFULLCONTENT = 0x2;
    [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint x, uint y, uint d, IntPtr e);
    public const uint LEFTDOWN = 0x02, LEFTUP = 0x04, RIGHTDOWN = 0x08, RIGHTUP = 0x10;

    public static void RightClick(int x, int y) {
        SetCursorPos(x, y);
        System.Threading.Thread.Sleep(120);
        mouse_event(RIGHTDOWN, 0, 0, 0, IntPtr.Zero);
        System.Threading.Thread.Sleep(60);
        mouse_event(RIGHTUP, 0, 0, 0, IntPtr.Zero);
    }
    /// Dos pulsaciones dentro del umbral de doble clic del sistema. Separarlas más —como
    /// hace el bucle de clics simples— las convierte en dos clics sueltos, que sobre el árbol
    /// seleccionan pero no activan nada.
    public static void DoubleClick(int x, int y) {
        SetCursorPos(x, y);
        System.Threading.Thread.Sleep(120);
        mouse_event(LEFTDOWN, 0, 0, 0, IntPtr.Zero);
        mouse_event(LEFTUP, 0, 0, 0, IntPtr.Zero);
        System.Threading.Thread.Sleep(60);
        mouse_event(LEFTDOWN, 0, 0, 0, IntPtr.Zero);
        mouse_event(LEFTUP, 0, 0, 0, IntPtr.Zero);
    }

    public static void Click(int x, int y) {
        SetCursorPos(x, y);
        System.Threading.Thread.Sleep(120);
        mouse_event(LEFTDOWN, 0, 0, 0, IntPtr.Zero);
        System.Threading.Thread.Sleep(60);
        mouse_event(LEFTUP, 0, 0, 0, IntPtr.Zero);
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }
}
'@
if (-not ('Win' -as [type])) { Add-Type -TypeDefinition $src }

$exe = Join-Path $PSScriptRoot '..\src\CafManagerConection.App\bin\Debug\net10.0-windows\cmc.exe'
if (-not (Test-Path $exe)) { throw "No se encontró el ejecutable. Corré: task build" }

$p = Start-Process $exe -PassThru
try {
    $limite = [Diagnostics.Stopwatch]::StartNew()
    while ($p.MainWindowHandle -eq 0 -and $limite.ElapsedMilliseconds -lt 15000) {
        Start-Sleep -Milliseconds 200
        $p.Refresh()
    }
    if ($p.MainWindowHandle -eq 0) { throw 'La ventana nunca apareció.' }

    Start-Sleep -Milliseconds $EsperaMs
    [Win]::ShowWindow($p.MainWindowHandle, 3) | Out-Null   # maximizada
    [Win]::SetForegroundWindow($p.MainWindowHandle) | Out-Null
    Start-Sleep -Milliseconds 900

    $r = New-Object Win+RECT
    [Win]::GetWindowRect($p.MainWindowHandle, [ref]$r) | Out-Null

    foreach ($c in $DoblesClics) {
        [Win]::Enfocar($p.MainWindowHandle)
        $partes = $c -split ','
        [Win]::DoubleClick($r.Left + [int]$partes[0], $r.Top + [int]$partes[1])
        Start-Sleep -Milliseconds 1200
    }

    foreach ($c in $Clics) {
        # Mismo cuidado que con las teclas: si la ventana no está activa, el primer clic se
        # gasta en activarla y el control nunca lo recibe.
        [Win]::Enfocar($p.MainWindowHandle)
        $partes = $c -split ','
        [Win]::Click($r.Left + [int]$partes[0], $r.Top + [int]$partes[1])
        Start-Sleep -Milliseconds 700
    }

    foreach ($c in $ClicsDerechos) {
        [Win]::Enfocar($p.MainWindowHandle)
        $partes = $c -split ','
        [Win]::RightClick($r.Left + [int]$partes[0], $r.Top + [int]$partes[1])
        Start-Sleep -Milliseconds 900
    }

    foreach ($t in $Teclas) {
        # El foco se reafirma antes de cada tecla: Windows se lo puede llevar en cualquier
        # momento, y una tecla mandada sin foco se pierde sin aviso. Esto hacía que la misma
        # prueba pasara o fallara según lo que estuviera haciendo el escritorio.
        [Win]::Enfocar($p.MainWindowHandle)
        [System.Windows.Forms.SendKeys]::SendWait($t)
        Start-Sleep -Milliseconds 500
    }

    Start-Sleep -Milliseconds $EsperaFinalMs

    $objetivo = $p.MainWindowHandle
    if ($VentanaActiva) {
        $dialogo = [Win]::DialogoDe($p.Id, $p.MainWindowHandle)
        if ($dialogo -ne [IntPtr]::Zero) { $objetivo = $dialogo }
        else { Write-Host 'No se encontró ningún diálogo abierto; se captura la principal.' -ForegroundColor Yellow }
    }
    [Win]::GetWindowRect($objetivo, [ref]$r) | Out-Null
    $w = $r.Right - $r.Left
    $h = $r.Bottom - $r.Top

    # Si la ventana quedo fuera de la pantalla o con tamano invalido, se captura la
    # pantalla completa: es preferible una captura de mas que una imagen negra.
    $pantalla = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
    if ($w -le 0 -or $h -le 0 -or $r.Left -lt -100 -or $r.Top -lt -100) {
        $r.Left = $pantalla.Left; $r.Top = $pantalla.Top
        $w = $pantalla.Width; $h = $pantalla.Height
    }

    if ($PantallaCompleta) {
        $r.Left = $pantalla.Left; $r.Top = $pantalla.Top
        $w = $pantalla.Width; $h = $pantalla.Height
    }

    $bmp = New-Object System.Drawing.Bitmap($w, $h)
    $g = [System.Drawing.Graphics]::FromImage($bmp)

    # PrintWindow con PW_RENDERFULLCONTENT pide la ventana al compositor. CopyFromScreen
    # devuelve negro sobre el material Mica, porque ese fondo lo compone DWM y no esta en
    # el framebuffer de la pantalla.
    if ($PantallaCompleta) {
        $g.CopyFromScreen($r.Left, $r.Top, 0, 0, $bmp.Size)
    }
    else {
        $hdc = $g.GetHdc()
        $ok = [Win]::PrintWindow($objetivo, $hdc, [Win]::PW_RENDERFULLCONTENT)
        $g.ReleaseHdc($hdc)

        if (-not $ok) {
            $g.CopyFromScreen($r.Left, $r.Top, 0, 0, $bmp.Size)
        }
    }
    $bmp.Save((Join-Path (Get-Location) $Salida), [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()

    Write-Host "Captura guardada en $Salida ($w x $h)" -ForegroundColor Green
}
finally {
    if (-not $p.HasExited) { Stop-Process -Id $p.Id -Force }
}
