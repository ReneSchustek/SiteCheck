<#
.SYNOPSIS
    Erzeugt die verkleinerten Schriften, die der gespeicherte HTML-Bericht in sich trägt.

.DESCRIPTION
    Der Bericht ist eine eigenständige Datei, die weitergegeben wird. Er lädt nichts aus dem Netz,
    also muss die Schrift in ihm stecken. Die vollständigen Dateien wären dafür zu schwer (Roboto
    Flex allein 1,7 MB mit 13 Achsen), deshalb entstehen hier Teilmengen:

      - Roboto Flex: nur die Gewichtsachse bleibt variabel, alle übrigen Achsen stehen auf ihrem
        Grundwert; Gewichte 300 bis 900, wie die Gestaltungslinie sie nutzt.
      - Roboto Mono: Gewichtsachse vollständig.
      - Beide: lateinische Zeichen samt Erweiterung A, die üblichen Satzzeichen (Gedankenstrich,
        Anführungszeichen, Auslassung), Euro, Pfeile. Fehlt ein Zeichen, greift der Browser für
        genau dieses Zeichen auf die nächste Schrift der Kette zurück.
      - Die Namenseinträge bleiben vollständig erhalten. Darin stehen Copyright und Lizenzhinweis
        der SIL Open Font License; so trägt jede Kopie im Bericht ihre Lizenz mit.

    Quelle sind die variablen TTF-Dateien unter src/SiteCheck.App/Assets/Fonts, Ziel ist
    src/SiteCheck.Application/Fonts. Einmalig auszuführen, wenn sich eine Schrift oder der
    Zeichenumfang ändert; die erzeugten Dateien liegen im Repository, der Build braucht das
    Werkzeug nicht.

    Voraussetzung: Python mit fontTools und brotli (pip install fonttools brotli).

.PARAMETER Python
    Pfad zum Python-Interpreter, in dem fontTools installiert ist.
#>
param(
    [string]$Python = "python"
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$sourceDir = Join-Path $root "src/SiteCheck.App/Assets/Fonts"
$targetDir = Join-Path $root "src/SiteCheck.Application/Fonts"
$workDir = Join-Path ([System.IO.Path]::GetTempPath()) ("sitecheck-fonts-" + [guid]::NewGuid().ToString("N"))

# Latein mit Erweiterung A deckt Deutsch, Englisch und die üblichen europäischen Namen ab;
# 2010–2027 bringt Gedankenstriche, deutsche Anführungszeichen und die Auslassung mit.
$unicodes = "U+0020-007E,U+00A0-017F,U+2010-2027,U+2030-203A,U+20AC,U+2122,U+2190-2193,U+2212"

# Alle Achsen außer der Gewichtsachse auf den Grundwert setzen ("drop"), die Gewichtsachse auf
# den Bereich der Gestaltungslinie begrenzen.
$flexAxes = @(
    "wght=300:900", "opsz=drop", "GRAD=drop", "wdth=drop", "slnt=drop",
    "XOPQ=drop", "YOPQ=drop", "XTRA=drop", "YTUC=drop", "YTLC=drop",
    "YTAS=drop", "YTDE=drop", "YTFI=drop"
)

function Invoke-FontTools([string[]]$Arguments) {
    & $Python -I -m fontTools @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "fontTools ist mit Code $LASTEXITCODE gescheitert: $($Arguments -join ' ')"
    }
}

function New-Subset([string]$Source, [string]$Target) {
    Invoke-FontTools @(
        "subset", $Source,
        "--unicodes=$unicodes",
        "--flavor=woff2",
        "--layout-features=*",
        "--name-IDs=*",
        "--name-languages=*",
        "--output-file=$Target"
    )
}

New-Item -ItemType Directory -Path $workDir | Out-Null
try {
    New-Item -ItemType Directory -Force -Path $targetDir | Out-Null

    $flexSource = Join-Path $sourceDir "roboto-flex-variable.ttf"
    $flexReduced = Join-Path $workDir "roboto-flex-wght.ttf"
    Invoke-FontTools (@("varLib.instancer", $flexSource) + $flexAxes + @("--output=$flexReduced"))
    New-Subset $flexReduced (Join-Path $targetDir "roboto-flex-latin.woff2")

    New-Subset (Join-Path $sourceDir "roboto-mono-variable.ttf") (Join-Path $targetDir "roboto-mono-latin.woff2")

    Get-ChildItem $targetDir -Filter *.woff2 | ForEach-Object {
        Write-Host ("{0}: {1:N0} Bytes" -f $_.Name, $_.Length)
    }
}
finally {
    Remove-Item -Recurse -Force -Path $workDir
}
