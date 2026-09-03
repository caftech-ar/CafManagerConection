# Dibuja el icono de la aplicacion y escribe Assets\cmc.ico y Assets\icono.png.
#
# El icono se genera, no se edita: son cuatro formas y dos colores, y tenerlo como codigo
# permite cambiar el trazo o la tinta sin abrir un editor de mapas de bits ni depender de que
# alguien conserve el archivo fuente. Correr este script deja los assets como estan versionados.
#
# Cada medida se dibuja NATIVA. Un trazo pensado en 256 px y reescalado a 16 se convierte en una
# mancha gris: el antialias reparte el color entre pixeles vecinos y no queda ni una linea neta.
# Dibujando en 16 el mismo trazo cae sobre pocos pixeles y sigue leyendose.

param(
    [string]$Destino = (Join-Path $PSScriptRoot '..\src\CafManagerConection.App\Assets')
)

Add-Type -AssemblyName System.Drawing

$ErrorActionPreference = 'Stop'
$Destino = (Resolve-Path $Destino).Path

# Violeta de la aplicacion, el mismo de los acentos de la interfaz, sobre un negro con sesgo
# violeta. Un negro puro al lado del violeta se ve azulado por contraste.
$Tinta = [Drawing.Color]::FromArgb(255, 167, 94, 255)
$Fondo = [Drawing.Color]::FromArgb(255, 12, 8, 20)

function Rect-Redondeado {
    param([single]$x, [single]$y, [single]$w, [single]$h, [single]$r)

    $p = New-Object Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

function New-Pluma {
    param([Drawing.Color]$color, [single]$ancho)

    $p = New-Object Drawing.Pen($color, $ancho)
    $p.StartCap = 'Round'; $p.EndCap = 'Round'; $p.LineJoin = 'Round'
    return $p
}

function New-Icono {
    param([int]$S)

    $bmp = New-Object Drawing.Bitmap($S, $S, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([Drawing.Color]::Transparent)

    $fondoPath = Rect-Redondeado 0 0 $S $S ($S * 0.22)
    $brocha = New-Object Drawing.SolidBrush($Fondo)
    $g.FillPath($brocha, $fondoPath)
    $brocha.Dispose()

    $g.SetClip($fondoPath)

    # De 64 para arriba hay pixeles de sobra y el dibujo puede ser el «de catalogo»: marca
    # comoda, trazo fino, resplandor. De 48 para abajo eso se deshace, y hay que dibujar otra
    # cosa: la marca ocupa mas tile, el trazo engorda y el resplandor se va. No es la misma
    # imagen achicada, es un dibujo distinto para el mismo simbolo, que es como se hacen los
    # juegos de iconos que se leen.
    $compacto = $S -le 48

    if (-not $compacto) {
        # Resplandor: degradado radial de verdad. Fingirlo con varias pasadas anchas y
        # translucidas no da un resplandor sino tres contornos duros, uno adentro del otro.
        $r = $S * 0.52
        $circulo = New-Object Drawing.Drawing2D.GraphicsPath
        $circulo.AddEllipse(($S / 2 - $r), ($S / 2 - $r), ($r * 2), ($r * 2))
        $halo = New-Object Drawing.Drawing2D.PathGradientBrush($circulo)
        $halo.CenterColor = [Drawing.Color]::FromArgb(26, $Tinta.R, $Tinta.G, $Tinta.B)
        $halo.SurroundColors = @([Drawing.Color]::FromArgb(0, $Tinta.R, $Tinta.G, $Tinta.B))
        $g.FillPath($halo, $circulo)
        $halo.Dispose()
        $circulo.Dispose()
    }

    # El trazo se redondea a pixeles enteros: 1,25 px de trazo no existen, el antialias los
    # reparte entre dos columnas de pixeles y lo que queda es gris palido. Ese fue el defecto
    # que hacia parecer que la aplicacion no tenia icono en la barra de tareas.
    $trazo = if ($compacto) {
        [Math]::Max(2, [Math]::Round($S * 0.135))
    }
    else {
        $S * 0.078
    }

    # La marca ocupa mas lugar en las medidas chicas: con el margen del dibujo grande, en 16 px
    # quedan cuatro pixeles de simbolo perdidos en un cuadrado oscuro.
    $g1 = if ($compacto) { 0.20 } else { 0.285 }   # x de las puntas del chevron
    $g2 = if ($compacto) { 0.48 } else { 0.475 }   # x del vertice
    $arriba = if ($compacto) { 0.24 } else { 0.30 }
    $abajo = if ($compacto) { 0.76 } else { 0.68 }
    $curDesde = if ($compacto) { 0.60 } else { 0.575 }
    $curHasta = if ($compacto) { 0.84 } else { 0.735 }

    $chevron = New-Object Drawing.Drawing2D.GraphicsPath
    $chevron.AddLines([Drawing.PointF[]]@(
        (New-Object Drawing.PointF(($S * $g1), ($S * $arriba))),
        (New-Object Drawing.PointF(($S * $g2), ($S * 0.50))),
        (New-Object Drawing.PointF(($S * $g1), ($S * $abajo)))))

    $pluma = New-Pluma $Tinta $trazo
    $g.DrawPath($pluma, $chevron)
    $pluma.Dispose()
    $chevron.Dispose()

    $pluma = New-Pluma $Tinta $trazo
    $g.DrawLine($pluma, ($S * $curDesde), ($S * $abajo), ($S * $curHasta), ($S * $abajo))
    $pluma.Dispose()

    $g.ResetClip()

    # Borde de luz: despega el icono del fondo oscuro de la barra de tareas.
    $luz = New-Object Drawing.Pen(
        [Drawing.Color]::FromArgb(30, 255, 255, 255), [Math]::Max(1, $S / 128))
    $g.DrawPath($luz, $fondoPath)
    $luz.Dispose()

    $fondoPath.Dispose()
    $g.Dispose()
    return $bmp
}

function Get-Png {
    param($bmp)

    $ms = New-Object IO.MemoryStream
    $bmp.Save($ms, [Drawing.Imaging.ImageFormat]::Png)
    $bytes = $ms.ToArray()
    $ms.Dispose()

    # La coma evita que PowerShell desarme el arreglo en la salida: sin ella, quien recibe esto
    # obtiene una lista de objetos y no un byte[], y la sobrecarga que se elija despues al
    # escribirlo puede no ser la que uno cree.
    return , $bytes
}

function Get-Dib {
    # Imagen en formato DIB para el .ico: cabecera BITMAPINFOHEADER, pixeles de abajo hacia
    # arriba y una mascara AND vacia. Las medidas chicas van asi y no en PNG porque NSIS lee el
    # icono del instalador con su propio codigo, no con el de Windows, y no todas las versiones
    # entienden entradas comprimidas.
    param($bmp, [int]$S)

    $rect = New-Object Drawing.Rectangle(0, 0, $S, $S)
    $datos = $bmp.LockBits($rect, [Drawing.Imaging.ImageLockMode]::ReadOnly,
        [Drawing.Imaging.PixelFormat]::Format32bppArgb)

    $buffer = New-Object byte[] ($datos.Stride * $S)
    [Runtime.InteropServices.Marshal]::Copy($datos.Scan0, $buffer, 0, $buffer.Length)
    $bmp.UnlockBits($datos)

    $anchoMascara = [Math]::Floor(($S + 31) / 32) * 4
    $ms = New-Object IO.MemoryStream
    $w = New-Object IO.BinaryWriter($ms)

    $w.Write([int]40)                                   # biSize
    $w.Write([int]$S)                                   # biWidth
    $w.Write([int]($S * 2))                             # biHeight: imagen + mascara
    $w.Write([int16]1)                                  # biPlanes
    $w.Write([int16]32)                                 # biBitCount
    $w.Write([int]0)                                    # biCompression
    $w.Write([int](($S * $S * 4) + ($S * $anchoMascara)))
    $w.Write([int]0); $w.Write([int]0); $w.Write([int]0); $w.Write([int]0)

    for ($y = $S - 1; $y -ge 0; $y--) {
        $w.Write($buffer, ($y * $datos.Stride), ($S * 4))
    }

    # Siempre la sobrecarga de tres argumentos. Con un solo argumento, PowerShell elige la
    # sobrecarga de UN byte y escribe uno: el .ico sale con el indice perfecto y sin imagenes.
    $mascara = New-Object byte[] ($S * $anchoMascara)
    $w.Write($mascara, 0, $mascara.Length)

    $w.Flush()
    $bytes = $ms.ToArray()
    $w.Dispose()
    $ms.Dispose()
    return , $bytes
}

$medidas = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
$imagenes = @()

foreach ($s in $medidas) {
    $bmp = New-Icono -S $s

    # De 128 para arriba, PNG: en DIB, un icono de 256 ocupa 256 KB por si solo.
    $imagenes += [pscustomobject]@{
        Lado  = $s
        Bytes = if ($s -ge 128) { Get-Png $bmp } else { Get-Dib $bmp $s }
    }

    if ($s -eq 256) {
        $bmp.Save((Join-Path $Destino 'icono.png'), [Drawing.Imaging.ImageFormat]::Png)
    }

    $bmp.Dispose()
}

$ms = New-Object IO.MemoryStream
$w = New-Object IO.BinaryWriter($ms)

$w.Write([int16]0)                      # reservado
$w.Write([int16]1)                      # tipo: icono
$w.Write([int16]$imagenes.Count)

$desplazamiento = 6 + (16 * $imagenes.Count)

foreach ($img in $imagenes) {
    # 0 significa 256: el ancho y el alto son de un byte y 256 no entra.
    $lado = if ($img.Lado -ge 256) { 0 } else { $img.Lado }

    $w.Write([byte]$lado)
    $w.Write([byte]$lado)
    $w.Write([byte]0)                   # colores de la paleta
    $w.Write([byte]0)                   # reservado
    $w.Write([int16]1)                  # planos
    $w.Write([int16]32)                 # bits por pixel
    $w.Write([int]$img.Bytes.Length)
    $w.Write([int]$desplazamiento)

    $desplazamiento += $img.Bytes.Length
}

foreach ($img in $imagenes) {
    $bytes = [byte[]]$img.Bytes
    $w.Write($bytes, 0, $bytes.Length)
}

$w.Flush()
[IO.File]::WriteAllBytes((Join-Path $Destino 'cmc.ico'), $ms.ToArray())
$w.Dispose()
$ms.Dispose()

Write-Output "cmc.ico: $($imagenes.Count) medidas ($($medidas -join ', '))"
Write-Output "icono.png: 256x256"
Write-Output "en $Destino"
