#Requires -Version 7
# Baut das WPF als eigenstaendigen Ordner (ohne .NET-Installation beim Freund) und packt ihn als
# ZIP nach deploy/pakete/ (M36 A7). Die erste Installation bei einem Freund geht ueber diese ZIP,
# jede weitere ueber den Server: das Anmeldefenster bietet ein neueres Paket von selbst an.
#
#   ./wpf-paket.ps1                                        nur bauen
#   ./wpf-paket.ps1 -Bereitstellen [-Projekt quizzer-pruef] bauen und in den laufenden Stapel legen
#
# Die Paketversion (yyyyMMdd-HHmm-<Commit>, UTC) steht in der Assembly; nur damit kennt das WPF
# seine eigene. Ein Bau ohne sie ist ein Entwicklungsbau und bietet nie eine Aktualisierung an.
#
# Die lokale appsettings.json bleibt draussen: sie liegt nicht im Repo, gilt nur fuer diesen
# Rechner und ueberschriebe beim Freund die Vorgaben im Code (Datenordner, Datenbank) - gemessen
# 2026-11-16, ohne diesen Schritt war sie im Paket.
param(
    [switch]$Bereitstellen,
    [ValidateSet('quizzer', 'quizzer-pruef')][string]$Projekt = 'quizzer'
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
Set-Location $PSScriptRoot

$stand = (git rev-parse --short=7 HEAD).Trim()
# UTC: eine Zeitumstellung darf ein spaeteres Paket nicht aelter aussehen lassen.
$version = "$([DateTime]::UtcNow.ToString('yyyyMMdd-HHmm'))-$stand"
$name = "Quizzer-$version"
$ordner = Join-Path 'pakete' $name
if (Test-Path $ordner) { Remove-Item -Recurse -Force $ordner }

# Ohne IncludeSourceRevision haengte das SDK "+<Commit>" an - das WPF liest es mit, der Abgleich nicht.
dotnet publish ../Quizzer/Quizzer.csproj -c Release -r win-x64 --self-contained true -o $ordner -v q -nologo `
    "-p:InformationalVersion=$version" -p:IncludeSourceRevisionInInformationalVersion=false

$gelesen = [System.Diagnostics.FileVersionInfo]::GetVersionInfo((Resolve-Path (Join-Path $ordner 'Quizzer.dll'))).ProductVersion
if ($gelesen -ne $version) { throw "Paketversion nicht in Quizzer.dll: erwartet $version, gelesen $gelesen" }

$lokal = @(Get-ChildItem $ordner -Recurse -Filter 'appsettings*.json')
$lokal | Remove-Item
Write-Host "Entfernt: $($lokal.Count) lokale Einstellungsdatei(en)."

$zip = "$ordner.zip"
if (Test-Path $zip) { Remove-Item $zip }
Compress-Archive -Path (Join-Path $ordner '*') -DestinationPath $zip -CompressionLevel Optimal
Remove-Item -Recurse -Force $ordner

$mb = [math]::Round((Get-Item $zip).Length / 1MB, 1)
Write-Host "Paket: $zip ($mb MB, Version $version). Weitergeben, beim Freund entpacken, Quizzer.exe starten."

if (-not $Bereitstellen) { return }

# In das Volume des Dienstes api, Ordner client/ unter der Medienwurzel. Erst unter einem Namen,
# den der Server nicht als Paket liest - eine halb kopierte Datei bietet er so nie an.
function Compose([string[]]$weitere) {
    if ($Projekt -eq 'quizzer-pruef') {
        docker compose -p quizzer-pruef --env-file .env.pruef -f docker-compose.yml -f docker-compose.pruef.yml @weitere
    } else {
        docker compose @weitere
    }
}

$ziel = '/data/media/client'
$datei = Split-Path $zip -Leaf

Compose @('exec', '-T', 'api', 'mkdir', '-p', $ziel)
Compose @('cp', $zip, "api:$ziel/$datei.neu")
Compose @('exec', '-T', 'api', 'sh', '-c',
    "mv -f '$ziel/$datei.neu' '$ziel/$datei' && find '$ziel' -maxdepth 1 -name 'Quizzer-*.zip' ! -name '$datei' -delete && ls -l '$ziel'")
Write-Host "Bereitgestellt in ${Projekt}: $datei"
