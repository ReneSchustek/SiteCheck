<#
.SYNOPSIS
    Erzeugt ein ECDSA-P-256-Schlüsselpaar für die Signierung der Release-Pakete.

.DESCRIPTION
    SiteCheck prüft die Echtheit heruntergeladener Update-Pakete über eine
    losgelöste Signatur (siehe ReleaseSignatureVerifier). Dieses Skript erzeugt
    einmalig das zugehörige Schlüsselpaar:

      * Der PRIVATE Schlüssel (PKCS#8-PEM) wird unter -KeyPath gespeichert und MUSS
        offline und geheim gehalten werden. Er darf niemals ins Repository gelangen.
      * Der ÖFFENTLICHE Schlüssel (SubjectPublicKeyInfo, Base64) wird ausgegeben und
        muss in ReleaseSignatureVerifier.DefaultPublicKeySpkiBase64 eingetragen werden.

    Ein bestehender Schlüssel wird nicht überschrieben (Schutz vor versehentlichem
    Verlust der signierten Vertrauenskette). Zum bewussten Rotieren -Force angeben.

.PARAMETER KeyPath
    Zielpfad des privaten Schlüssels (PEM). Sollte außerhalb des Repos liegen.

.PARAMETER Force
    Überschreibt einen bestehenden Schlüssel. Achtung: ältere Clients vertrauen dann
    nur noch dem alten öffentlichen Schlüssel — Rotation gut überlegen.

.EXAMPLE
    ./tools/new-signing-key.ps1 -KeyPath F:\Entwicklung\SiteCheck-signing\private-key.pem
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$KeyPath,
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ((Test-Path -LiteralPath $KeyPath) -and -not $Force) {
    throw "Schlüssel existiert bereits: $KeyPath. Zum bewussten Rotieren -Force angeben."
}

# Verzeichnis sicherstellen.
$dir = Split-Path -Parent $KeyPath
if ($dir -and -not (Test-Path -LiteralPath $dir)) {
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
}

# ECDSA P-256: modern, kurze Signaturen, vollständig in der .NET-BCL (keine Fremd-Lib).
$ecdsa = [System.Security.Cryptography.ECDsa]::Create(
    [System.Security.Cryptography.ECCurve+NamedCurves]::nistP256)
try {
    Set-Content -LiteralPath $KeyPath -Value $ecdsa.ExportPkcs8PrivateKeyPem() -NoNewline
    $publicSpki = [Convert]::ToBase64String($ecdsa.ExportSubjectPublicKeyInfo())
}
finally {
    $ecdsa.Dispose()
}

Write-Host "Privater Schlüssel geschrieben (GEHEIM HALTEN, NICHT committen):" -ForegroundColor Yellow
Write-Host "  $KeyPath"
Write-Host ''
Write-Host "Öffentlichen Schlüssel in ReleaseSignatureVerifier.DefaultPublicKeySpkiBase64 eintragen:" -ForegroundColor Cyan
Write-Host $publicSpki
