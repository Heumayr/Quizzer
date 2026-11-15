#Requires -Version 7
# Baut das WPF als eigenstaendigen Ordner (ohne .NET-Installation beim Freund) und packt ihn als
# ZIP nach deploy/pakete/ (M36 A7). Die erste Installation bei einem Freund geht ueber diese ZIP.
#
#   ./wpf-paket.ps1
#
# Die lokale appsettings.json bleibt draussen: sie liegt nicht im Repo, gilt nur fuer diesen
# Rechner und ueberschriebe beim Freund die Vorgaben im Code (Datenordner, Datenbank) - gemessen
# 2026-11-16, ohne diesen Schritt war sie im Paket.
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
Set-Location $PSScriptRoot

$stand = (git rev-parse --short HEAD).Trim()
$name = "Quizzer-$(Get-Date -Format 'yyyyMMdd')-$stand"
$ordner = Join-Path 'pakete' $name
if (Test-Path $ordner) { Remove-Item -Recurse -Force $ordner }

dotnet publish ../Quizzer/Quizzer.csproj -c Release -r win-x64 --self-contained true -o $ordner -v q -nologo

$lokal = @(Get-ChildItem $ordner -Recurse -Filter 'appsettings*.json')
$lokal | Remove-Item
Write-Host "Entfernt: $($lokal.Count) lokale Einstellungsdatei(en)."

$zip = "$ordner.zip"
if (Test-Path $zip) { Remove-Item $zip }
Compress-Archive -Path (Join-Path $ordner '*') -DestinationPath $zip -CompressionLevel Optimal
Remove-Item -Recurse -Force $ordner

$mb = [math]::Round((Get-Item $zip).Length / 1MB, 1)
Write-Host "Paket: $zip ($mb MB). Weitergeben, beim Freund entpacken, Quizzer.exe starten."
