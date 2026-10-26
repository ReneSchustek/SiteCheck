<#
.SYNOPSIS
    Misst die Testabdeckung der Logik-Schichten und meldet die Zeilenrate je Produktiv-Assembly.

.DESCRIPTION
    Führt die Testprojekte in der Debug-Konfiguration aus (Release stellt DebugType=none und
    liefert dann keine PDBs, ohne die keine Instrumentierung möglich ist) und sammelt die
    Abdeckung über die Microsoft Testing Platform im Cobertura-Format. Danach wird je
    Produktiv-Assembly die Zeilenrate ausgelesen und gegen die Schwelle geprüft.

.PARAMETER Threshold
    Mindest-Zeilenrate in Prozent, die Core und Application erreichen müssen (Default: 90).
#>
param(
    [double]$Threshold = 90
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$resultDir = Join-Path $root "TestResults/coverage"
if (Test-Path $resultDir) { Remove-Item -Recurse -Force $resultDir }

$layers = @("Core", "Application", "Data")
$rates = @{}

foreach ($layer in $layers) {
    $proj = Join-Path $root "tests/SiteCheck.$layer.Tests/SiteCheck.$layer.Tests.csproj"
    $out = Join-Path $resultDir $layer
    & dotnet test $proj -c Debug --results-directory $out -- `
        --coverage --coverage-output-format cobertura --coverage-output cov.xml | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Tests fehlgeschlagen: $layer" }

    $file = Get-ChildItem -Recurse -Path $out -Filter cov.xml | Select-Object -First 1
    [xml]$xml = Get-Content -LiteralPath $file.FullName
    $pkg = $xml.coverage.packages.package | Where-Object { $_.name -eq "SiteCheck.$layer" }
    $rates[$layer] = [math]::Round([double]$pkg.'line-rate' * 100, 1)
}

Write-Host ""
Write-Host "Abdeckung je Produktiv-Assembly:" -ForegroundColor Cyan
foreach ($layer in $layers) {
    Write-Host ("  SiteCheck.{0,-12} {1,5} %" -f $layer, $rates[$layer])
}

# Core und Application tragen die prüfbare Logik; Data ist überwiegend echter Netz-/TLS-/DNS-Code,
# der nur über Integrationstests abzudecken ist (siehe docs/abdeckung.md).
$failed = @()
foreach ($layer in @("Core", "Application")) {
    if ($rates[$layer] -lt $Threshold) { $failed += "$layer ($($rates[$layer]) % < $Threshold %)" }
}

if ($failed.Count -gt 0) {
    Write-Host ("Unter der Schwelle: " + ($failed -join ", ")) -ForegroundColor Red
    exit 1
}

Write-Host "Core und Application über der Schwelle." -ForegroundColor Green
