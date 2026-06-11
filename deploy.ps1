#requires -Version 5.1
<#
.SYNOPSIS
    Veröffentlicht die NOOSE-Website und rollt sie auf den Produktiv-Server aus.

.DESCRIPTION
    Ein-Befehl-Deploy: Release veröffentlichen -> mit tar packen (NICHT Compress-Archive,
    das zerschießt Dateien) -> per scp hochladen -> auf dem Server Dienst stoppen, Dateien
    tauschen (App_Data bleibt erhalten), Dienst starten, Health-Check.

.EXAMPLE
    .\deploy.ps1
        Standard-Deploy auf root@195.20.225.12.

.EXAMPLE
    .\deploy.ps1 -SkipPublish
        Nutzt den vorhandenen .\publish-Ordner (kein erneutes dotnet publish).

.NOTES
    Voraussetzung: .NET SDK + ssh/scp/tar (in Windows 11 enthalten).
    Ohne SSH-Key fragt scp/ssh je einmal nach dem Server-Passwort.
    Passwortlosen Deploy einrichten: siehe DEPLOYMENT.md ("SSH-Key").
#>

[CmdletBinding()]
param(
    [string]$Server  = "root@195.20.225.12",
    [string]$AppDir  = "/var/www/noose",
    [string]$Service = "noose",
    [switch]$SkipPublish
)

$ErrorActionPreference = "Stop"

$project = Join-Path $PSScriptRoot "NOOSE-Website\NOOSE-Website.csproj"
$publish = Join-Path $PSScriptRoot "publish"
$tarball = Join-Path $PSScriptRoot "noose-publish.tgz"

function Invoke-Step {
    param([string]$Label, [scriptblock]$Action)
    Write-Host "==> $Label" -ForegroundColor Cyan
    & $Action
    if ($LASTEXITCODE -ne 0) { throw "Schritt fehlgeschlagen: $Label (Exit $LASTEXITCODE)" }
}

# 1) Release veröffentlichen
if (-not $SkipPublish) {
    Invoke-Step "Veröffentliche Release" { dotnet publish $project -c Release -o $publish --nologo }
} else {
    Write-Host "==> Überspringe Publish (-SkipPublish)" -ForegroundColor DarkYellow
}
if (-not (Test-Path (Join-Path $publish "NOOSE-Website.dll"))) {
    throw "publish-Ordner unvollständig: $publish (NOOSE-Website.dll fehlt)."
}

# 2) Mit tar packen — zuverlässig; Compress-Archive hat schon 0-Byte-Dateien erzeugt.
if (Test-Path $tarball) { Remove-Item $tarball -Force }
Invoke-Step "Packe Artefakt (tar)" { tar -czf $tarball -C $publish . }

# 3) Auf den Server kopieren
Invoke-Step "Lade auf Server hoch" { scp $tarball "${Server}:/tmp/noose-publish.tgz" }

# 4) Auf dem Server ausrollen: Dienst stoppen, Dateien tauschen (App_Data behalten),
#    Rechte setzen, Dienst starten, kurz warten, Health prüfen. Alles per && -> fail-fast.
$remote = "systemctl stop $Service" +
          " && find $AppDir -mindepth 1 -maxdepth 1 ! -name App_Data -exec rm -rf {} +" +
          " && tar -xzf /tmp/noose-publish.tgz -C $AppDir" +
          " && chown -R www-data:www-data $AppDir" +
          " && systemctl start $Service" +
          " && rm -f /tmp/noose-publish.tgz" +
          " && sleep 6" +
          " && curl -s -o /dev/null -w 'Health-Check: HTTP %{http_code}\n' http://127.0.0.1:5000/health"
Invoke-Step "Rolle auf dem Server aus" { ssh $Server $remote }

# 5) Lokales Artefakt aufräumen
Remove-Item $tarball -Force -ErrorAction SilentlyContinue

Write-Host ""
Write-Host "Fertig. https://noose.info ist aktualisiert." -ForegroundColor Green
Write-Host "Im Browser ggf. mit Strg+F5 hart neu laden (Asset-Cache)." -ForegroundColor Green
