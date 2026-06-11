#requires -Version 5.1
<#
.SYNOPSIS
    Veroeffentlicht die NOOSE-Website und rollt sie auf den Produktiv-Server aus.

.DESCRIPTION
    Ein-Befehl-Deploy: Release veroeffentlichen -> mit tar packen (NICHT Compress-Archive,
    das zerschießt Dateien) -> per scp hochladen -> auf dem Server Dienst stoppen, Dateien
    tauschen (App_Data bleibt erhalten), Dienst starten, Health-Check.

.EXAMPLE
    .\deploy.ps1
        Standard-Deploy auf root@195.20.225.12.

.EXAMPLE
    .\deploy.ps1 -SkipPublish
        Nutzt den vorhandenen .\publish-Ordner (kein erneutes dotnet publish).

.EXAMPLE
    .\deploy.ps1 -NoPause
        Ohne "Enter zum Schließen" am Ende (fuer Terminal-/CI-Nutzung).

.NOTES
    Am besten aus einer bereits offenen PowerShell starten. Bei "Run with PowerShell" /
    Doppelklick haelt das Skript das Fenster am Ende offen, damit Ausgabe & Fehler lesbar bleiben.
    Ohne SSH-Key fragt scp/ssh je einmal nach dem Server-Passwort (siehe DEPLOYMENT.md -> SSH-Key).
#>

[CmdletBinding()]
param(
    [string]$Server  = "root@195.20.225.12",
    [string]$AppDir  = "/var/www/noose",
    [string]$Service = "noose",
    [switch]$SkipPublish,
    [switch]$NoPause
)

$ErrorActionPreference = "Stop"
$exitCode = 0

function Invoke-Step {
    param([string]$Label, [scriptblock]$Action)
    Write-Host "==> $Label" -ForegroundColor Cyan
    & $Action
    if ($LASTEXITCODE -ne 0) { throw "Schritt fehlgeschlagen: $Label (Exit $LASTEXITCODE)" }
}

# Findet ssh/scp robust – PATH-unabhaengig und auch aus einem 32-bit-PowerShell heraus, wo
# C:\Windows\System32 per WOW64 auf SysWOW64 umgeleitet wird und die 64-bit-OpenSSH-Exe dort fehlt.
function Resolve-Exe {
    param([string]$Name)
    $cmd = Get-Command $Name -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    $candidates = @(
        (Join-Path $env:WINDIR "System32\OpenSSH\$Name.exe"),   # 64-bit-Prozess
        (Join-Path $env:WINDIR "Sysnative\OpenSSH\$Name.exe"),  # aus 32-bit-Prozess -> echtes System32
        (Join-Path $env:ProgramFiles "Git\usr\bin\$Name.exe")   # Git for Windows als Fallback
    )
    foreach ($p in $candidates) {
        if ($p -and (Test-Path $p)) { return $p }
    }
    throw "$Name nicht gefunden. Tipp: deploy.ps1 in der normalen (64-bit) Windows PowerShell starten, oder OpenSSH-Client installieren (Einstellungen > Apps > Optionale Features > 'OpenSSH-Client')."
}

try {
    $project = Join-Path $PSScriptRoot "NOOSE-Website\NOOSE-Website.csproj"
    $publish = Join-Path $PSScriptRoot "publish"
    $tarball = Join-Path $PSScriptRoot "noose-publish.tgz"

    if (-not (Test-Path $project)) {
        throw "Projekt nicht gefunden: $project. Liegt deploy.ps1 wirklich im Repo-Root?"
    }

    # ssh/scp vorab auf vollen Pfad aufloesen (PATH-unabhaengig).
    $scp = Resolve-Exe 'scp'
    $ssh = Resolve-Exe 'ssh'

    # 1) Release veroeffentlichen
    if (-not $SkipPublish) {
        Invoke-Step "Veroeffentliche Release" { dotnet publish $project -c Release -o $publish --nologo }
    } else {
        Write-Host "==> ueberspringe Publish (-SkipPublish)" -ForegroundColor DarkYellow
    }
    if (-not (Test-Path (Join-Path $publish "NOOSE-Website.dll"))) {
        throw "publish-Ordner unvollstaendig: $publish (NOOSE-Website.dll fehlt). Laeuft evtl. noch eine Dev-Instanz und sperrt bin/?"
    }

    # 2) Mit tar packen — zuverlaessig; Compress-Archive hat schon 0-Byte-Dateien erzeugt.
    if (Test-Path $tarball) { Remove-Item $tarball -Force }
    Invoke-Step "Packe Artefakt (tar)" { tar -czf $tarball -C $publish . }

    # 3) Auf den Server kopieren
    Invoke-Step "Lade auf Server hoch" { & $scp $tarball "${Server}:/tmp/noose-publish.tgz" }

    # 4) Auf dem Server ausrollen: Dienst stoppen, Dateien tauschen (App_Data behalten),
    #    Rechte setzen, Dienst starten, kurz warten, Health pruefen. Alles per && -> fail-fast.
    $remote = "systemctl stop $Service" +
              " && find $AppDir -mindepth 1 -maxdepth 1 ! -name App_Data -exec rm -rf {} +" +
              " && tar -xzf /tmp/noose-publish.tgz -C $AppDir" +
              " && chown -R www-data:www-data $AppDir" +
              " && systemctl start $Service" +
              " && rm -f /tmp/noose-publish.tgz" +
              " && sleep 6" +
              " && curl -s -o /dev/null -w 'Health-Check: HTTP %{http_code}\n' http://127.0.0.1:5000/health"
    Invoke-Step "Rolle auf dem Server aus" { & $ssh $Server $remote }

    # 5) Lokales Artefakt aufraeumen
    Remove-Item $tarball -Force -ErrorAction SilentlyContinue

    Write-Host ""
    Write-Host "Fertig. https://noose.info ist aktualisiert." -ForegroundColor Green
    Write-Host "Im Browser ggf. mit Strg+F5 hart neu laden (Asset-Cache)." -ForegroundColor Green
}
catch {
    $exitCode = 1
    Write-Host ""
    Write-Host "============================================" -ForegroundColor Red
    Write-Host "  DEPLOY FEHLGESCHLAGEN" -ForegroundColor Red
    Write-Host "============================================" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    if ($_.ScriptStackTrace) {
        Write-Host ""
        Write-Host $_.ScriptStackTrace -ForegroundColor DarkGray
    }
}
finally {
    if (-not $NoPause) {
        Write-Host ""
        $null = Read-Host "Enter druecken zum Schließen"
    }
}

exit $exitCode
