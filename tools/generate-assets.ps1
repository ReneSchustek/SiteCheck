<#
.SYNOPSIS
    Erzeugt aus dem quadratischen Logo die Fenster-/EXE-Icon-Datei und die MSIX-Kachelbilder.

.DESCRIPTION
    Liest src/SiteCheck.App/Assets/logo.png und erzeugt:
      - AppIcon.ico (mehrere Größen 16–256, als PNG eingebettet),
      - die Kachel-PNGs, die das Package.appxmanifest referenziert.
    Nicht-quadratische Kacheln (Wide, Splash) bekommen das Logo zentriert auf transparenter
    Fläche. Ohne ImageMagick, nur mit System.Drawing (Windows).

    Einmalig auszuführen, wenn sich das Logo ändert. Die erzeugten Dateien liegen im Repo, damit
    der Build sie nicht jedes Mal neu braucht.
#>
param()

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$root = Split-Path -Parent $PSScriptRoot
$assets = Join-Path $root "src/SiteCheck.App/Assets"
$logoPath = Join-Path $assets "logo.png"
$logo = [System.Drawing.Image]::FromFile($logoPath)

function New-Canvas([int]$w, [int]$h) {
    $bmp = New-Object System.Drawing.Bitmap($w, $h, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    return @($bmp, $g)
}

# Zeichnet das Logo zentriert und auf die Zielfläche eingepasst (mit Rand), Seitenverhältnis gewahrt.
function Save-Tile([int]$w, [int]$h, [double]$fill, [string]$name) {
    $pair = New-Canvas $w $h
    $bmp = $pair[0]; $g = $pair[1]
    $side = [Math]::Min($w, $h) * $fill
    $x = ($w - $side) / 2
    $y = ($h - $side) / 2
    $g.DrawImage($logo, [System.Drawing.RectangleF]::new($x, $y, $side, $side))
    $g.Dispose()
    $out = Join-Path $assets $name
    $bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Host "  $name ($w x $h)"
}

Write-Host "Kacheln:" -ForegroundColor Cyan
Save-Tile 44 44 0.9 "Square44x44Logo.scale-100.png"
Save-Tile 88 88 0.9 "Square44x44Logo.scale-200.png"
Save-Tile 24 24 1.0 "Square44x44Logo.targetsize-24_altform-unplated.png"
Save-Tile 150 150 0.75 "Square150x150Logo.scale-100.png"
Save-Tile 300 300 0.75 "Square150x150Logo.scale-200.png"
Save-Tile 310 150 0.8 "Wide310x150Logo.scale-100.png"
Save-Tile 620 300 0.8 "Wide310x150Logo.scale-200.png"
Save-Tile 50 50 0.9 "StoreLogo.png"
Save-Tile 620 300 0.5 "SplashScreen.scale-100.png"
Save-Tile 1240 600 0.5 "SplashScreen.scale-200.png"

# AppIcon.ico: mehrere Größen, jede als PNG eingebettet (Windows Vista+ liest PNG-Icons).
Write-Host "Icon:" -ForegroundColor Cyan
$sizes = @(16, 32, 48, 64, 128, 256)
$pngs = @()
foreach ($s in $sizes) {
    $pair = New-Canvas $s $s
    $bmp = $pair[0]; $g = $pair[1]
    $g.DrawImage($logo, [System.Drawing.RectangleF]::new(0, 0, $s, $s))
    $g.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    $pngs += , $ms.ToArray()
}

$icoPath = Join-Path $assets "AppIcon.ico"
$fs = [System.IO.File]::Create($icoPath)
$bw = New-Object System.IO.BinaryWriter($fs)
$bw.Write([UInt16]0)            # reserviert
$bw.Write([UInt16]1)            # Typ 1 = Icon
$bw.Write([UInt16]$sizes.Count) # Anzahl Bilder
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]
    $bw.Write([Byte]($(if ($s -ge 256) { 0 } else { $s })))  # Breite (0 = 256)
    $bw.Write([Byte]($(if ($s -ge 256) { 0 } else { $s })))  # Höhe
    $bw.Write([Byte]0)           # Farben
    $bw.Write([Byte]0)           # reserviert
    $bw.Write([UInt16]1)         # Ebenen
    $bw.Write([UInt16]32)        # Bits pro Pixel
    $bw.Write([UInt32]$pngs[$i].Length)
    $bw.Write([UInt32]$offset)
    $offset += $pngs[$i].Length
}
foreach ($png in $pngs) { $bw.Write($png) }
$bw.Flush(); $bw.Dispose(); $fs.Dispose()
Write-Host "  AppIcon.ico ($($sizes -join ', '))"

$logo.Dispose()
Write-Host "Fertig." -ForegroundColor Green
