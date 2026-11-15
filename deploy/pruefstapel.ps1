#Requires -Version 7
# Pruefstapel (M36 A1): eigener Stapel mit Wegwerfdaten, damit Bausitzung und StackSmoke nie den
# Betriebsstapel treffen. StackSmoke SCHREIBT in den Stapel, und "up --build" startet die API neu.
#
#   ./pruefstapel.ps1 up       bauen und starten (legt .env.pruef beim ersten Mal an)
#   ./pruefstapel.ps1 smoke    StackSmoke gegen den Pruefstapel
#   ./pruefstapel.ps1 down     anhalten, Daten bleiben
#   ./pruefstapel.ps1 weg      anhalten und Daten loeschen
#   ./pruefstapel.ps1 status   Container und /version
param([Parameter(Mandatory)][ValidateSet('up', 'smoke', 'down', 'weg', 'status')][string]$Befehl)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
Set-Location $PSScriptRoot

$projekt = 'quizzer-pruef'
$envDatei = '.env.pruef'
$port = 8544

function Compose([string[]]$weitere) {
    docker compose -p $projekt --env-file $envDatei -f docker-compose.yml -f docker-compose.pruef.yml @weitere
}

function Wert([string]$schluessel) {
    $zeile = Get-Content $envDatei | Where-Object { $_ -match "^$schluessel=" } | Select-Object -First 1
    if (-not $zeile) { throw "$schluessel fehlt in $envDatei" }
    return $zeile.Substring($schluessel.Length + 1)
}

if (-not (Test-Path $envDatei)) {
    ./new-secrets.ps1 -Datei $envDatei
    # Nur lokal und auf einem eigenen Port - 8543 gehoert dem Betriebsstapel.
    Add-Content $envDatei "`nQUIZZER_BIND_ADDRESS=127.0.0.1`nQUIZZER_PORT=$port"
}

$adresse = "https://127.0.0.1:$port"
switch ($Befehl) {
    'up' { Compose @('up', '-d', '--build') }
    'down' { Compose @('down') }
    'weg' { Compose @('down', '-v') }
    'status' {
        Compose @('ps')
        curl.exe -sk "$adresse/api/v1/version"
        Write-Host ''
    }
    'smoke' {
        $env:QUIZZER_E2E_URL = $adresse
        $env:QUIZZER_E2E_USER = Wert 'QUIZZER_ADMIN_USER'
        $env:QUIZZER_E2E_PASSWORD = Wert 'QUIZZER_ADMIN_PASSWORD'
        $env:QUIZZER_E2E_PIN = (Get-Content certs/server.sha256 -Raw).Trim()
        dotnet test ../Quizzer.Server.Tests --filter StackSmoke
    }
}
