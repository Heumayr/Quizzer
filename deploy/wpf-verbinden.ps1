# Stellt das WPF auf DIESEM Rechner auf den lokalen Docker-Stapel um (M24): Servermodus an,
# Adresse https://localhost:<QUIZZER_PORT>, Fingerabdruck aus certs\server.sha256. Ordner,
# Datenbank und Anzeigeschalter in %APPDATA%\Quizzer\settings.json bleiben, wie sie sind.
#   .\wpf-verbinden.ps1            einschalten
#   .\wpf-verbinden.ps1 -Aus       zurueck in den Einzelbetrieb (die Adresse bleibt gemerkt)
# Dasselbe geht von Hand im WPF unter Einstellungen -> Server.
param([switch]$Aus)

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'Bitte mit PowerShell 7 starten (pwsh).' }

. (Join-Path $PSScriptRoot 'env-lesen.ps1')
$envWerte = Read-QuizzerEnv
$port = if ($envWerte['QUIZZER_PORT']) { $envWerte['QUIZZER_PORT'] } else { '8443' }
if (-not (Test-Path certs/server.sha256)) { throw 'certs\server.sha256 fehlt - zuerst .\zertifikat-lokal.ps1' }
$fingerabdruck = (Get-Content certs/server.sha256 -Raw).Trim()

$datei = Join-Path $env:APPDATA 'Quizzer\settings.json'
# Dieselben Felder und Vorgaben wie UserSettings.Werte - der Leser unterscheidet Gross- und Kleinschreibung.
$werte = [ordered]@{
    FilePathQuizzer = $null; MsSqlConString = $null; UseServer = $false; ServerUrl = $null
    ServerCertificateSha256 = $null; LastUserName = $null; ScoresLeft = $false; Animationen = $true; Toene = $true
}
if (Test-Path $datei) {
    Copy-Item $datei "$datei.vor-wpf-verbinden-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
    $vorhanden = Get-Content $datei -Raw | ConvertFrom-Json
    foreach ($feld in $vorhanden.PSObject.Properties) { $werte[$feld.Name] = $feld.Value }
}

$werte.UseServer = -not $Aus
if (-not $Aus) {
    $werte.ServerUrl = "https://localhost:$port"
    $werte.ServerCertificateSha256 = $fingerabdruck
    if (-not $werte.LastUserName) { $werte.LastUserName = $envWerte['QUIZZER_ADMIN_USER'] }
}

New-Item -ItemType Directory -Force (Split-Path $datei) | Out-Null
$werte | ConvertTo-Json | Set-Content $datei -Encoding utf8NoBOM
if ($Aus) { Write-Host "Einzelbetrieb eingestellt ($datei)." }
else { Write-Host "WPF zeigt auf $($werte.ServerUrl), Fingerabdruck $fingerabdruck ($datei). Beim naechsten Start fragt es nach der Anmeldung." }
