<#
.SYNOPSIS
    Baut die SiteCheck-Setup.exe: self-contained Publish + Inno-Setup-Kompilierung.

.DESCRIPTION
    1. Publiziert SiteCheck.App self-contained (.NET + Windows App SDK) nach dist\setup-publish.
    2. Kompiliert installer\SiteCheck.iss mit Inno Setup zur Setup.exe.

    Ergebnis: <OutputDir>\SiteCheck-Setup-v<Version>.exe

.PARAMETER Version
    Versionsnummer (Default: aus der Datei VERSION).

.PARAMETER OutputDir
    Zielordner für die fertige Setup.exe (Default: dist/ im Repo-Root).

.PARAMETER Runtime
    Ziel-RID (Default: win-x64).
#>
param(
    [string]$Version = "",
    [string]$OutputDir = "",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = (Get-Content -LiteralPath (Join-Path $root "VERSION") -Raw).Trim()
}
if ([string]::IsNullOrWhiteSpace($OutputDir)) { $OutputDir = Join-Path $root "dist" }

$appProject = Join-Path $root "src/SiteCheck.App/SiteCheck.App.csproj"
$publishDir = Join-Path $root "dist/setup-publish"
$iss = Join-Path $root "installer/SiteCheck.iss"

# Inno-Setup-Compiler finden: Standardpfade zuerst, dann PATH.
$iscc = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { $iscc = (Get-Command "ISCC.exe" -ErrorAction SilentlyContinue | Select-Object -First 1).Source }
if (-not $iscc) { throw "Inno Setup (ISCC.exe) nicht gefunden. Installation: winget install JRSoftware.InnoSetup" }
Write-Host "Inno Setup: $iscc" -ForegroundColor DarkGray

New-Item -ItemType Directory -Force $OutputDir | Out-Null

Write-Host "Publish (self-contained, $Runtime) ..." -ForegroundColor Cyan
if (Test-Path $publishDir) { Remove-Item -Recurse -Force $publishDir }
& dotnet publish $appProject `
    -c Release `
    -r $Runtime `
    --self-contained `
    -p:Platform=x64 `
    -p:WindowsPackageType=None `
    -p:Version=$Version `
    -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish ist fehlgeschlagen (Exit $LASTEXITCODE)." }

Write-Host "Kompiliere Setup.exe ..." -ForegroundColor Cyan
& $iscc "/DMyAppVersion=$Version" "/DPublishDir=$publishDir" "/O$OutputDir" $iss
if ($LASTEXITCODE -ne 0) { throw "Inno Setup ist fehlgeschlagen (Exit $LASTEXITCODE)." }

Write-Host "Fertig: $(Join-Path $OutputDir "SiteCheck-Setup-v$Version.exe")" -ForegroundColor Green
