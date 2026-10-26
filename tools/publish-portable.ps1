<#
.SYNOPSIS
    Erzeugt die portable Verteil-Variante von SiteCheck: ein Self-Contained-Publish
    der WinUI-App und eine ZIP-Datei.

.DESCRIPTION
    Führt 'dotnet publish' für SiteCheck.App aus (Self-Contained, win-x64, das Windows
    App SDK mitgeliefert) und packt das Ergebnis als 'SiteCheck-Portable-v<Version>.zip'
    nach dist/. Ein MSIX-Paket und eine Signatur entstehen hier bewusst nicht — das ist
    ein eigener, noch offener Schritt (braucht ein Code-Signing-Zertifikat).

.PARAMETER Version
    Versionsnummer der Artefakte (Default: aus der Datei VERSION).

.PARAMETER Runtime
    Ziel-RID (Default: win-x64).
#>
param(
    [string]$Version = "",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = (Get-Content -LiteralPath (Join-Path $root "VERSION") -Raw).Trim()
}

$appProject = Join-Path $root "src/SiteCheck.App/SiteCheck.App.csproj"
$distDir = Join-Path $root "dist"
$publishDir = Join-Path $distDir "publish-$Runtime"

New-Item -ItemType Directory -Force $distDir | Out-Null
if (Test-Path $publishDir) { Remove-Item -Recurse -Force $publishDir }

Write-Host "Publish (Self-Contained, $Runtime) ..." -ForegroundColor Cyan
& dotnet publish $appProject `
    -c Release `
    -r $Runtime `
    --self-contained `
    -p:Platform=x64 `
    -p:WindowsPackageType=None `
    -p:Version=$Version `
    -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish ist fehlgeschlagen (Exit $LASTEXITCODE)." }

$zipPath = Join-Path $distDir "SiteCheck-Portable-v$Version.zip"
if (Test-Path $zipPath) { Remove-Item -Force $zipPath }

Write-Host "Packe portable ZIP ..." -ForegroundColor Cyan
Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath

Write-Host "Fertig: $zipPath" -ForegroundColor Green
