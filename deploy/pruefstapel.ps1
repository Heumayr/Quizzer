#Requires -Version 7
# Pruefstapel (M36 A1): eigener Stapel mit Wegwerfdaten, damit Bausitzung und StackSmoke nie den
# Betriebsstapel treffen. StackSmoke SCHREIBT in den Stapel, und "up --build" startet die API neu.
#
#   ./pruefstapel.ps1 up       bauen und starten (legt .env.pruef beim ersten Mal an)
#   ./pruefstapel.ps1 smoke    StackSmoke gegen den Pruefstapel
#   ./pruefstapel.ps1 down     anhalten, Daten bleiben
#   ./pruefstapel.ps1 weg      anhalten und Daten loeschen
#   ./pruefstapel.ps1 status   Container und /version
#   ./pruefstapel.ps1 absender echte Absenderadresse hinter dem Tunnel pruefen (M36 A3)
param([Parameter(Mandatory)][ValidateSet('up', 'smoke', 'down', 'weg', 'status', 'absender')][string]$Befehl)

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

# Fehlanmeldungen mit einem erfundenen Namen (keine Kontosperre), gesendet aus dem Netz edge des
# Pruefstapels von Adresse $von, mit Cf-Connecting-Ip $cfIp. Liefert die Statuscodes.
function Anmeldeversuche([string]$von, [string]$cfIp, [int]$anzahl) {
    $rumpf = '{\"userName\":\"probe-gibt-es-nicht\",\"password\":\"falsch-falsch-falsch\"}'
    $skript = "for i in `$(seq 1 $anzahl); do wget --no-check-certificate -qO /dev/null -S " +
        "--header 'Cf-Connecting-Ip: $cfIp' --header 'Content-Type: application/json' " +
        "--post-data `"$rumpf`" https://caddy:8443/api/v1/auth/login 2>&1 | grep -m1 'HTTP/' | awk '{print `$2}'; done"
    return @(docker run --rm --network "$($projekt)_edge" --ip $von --entrypoint sh caddy:2.10.0-alpine -c $skript)
}
switch ($Befehl) {
    'up' { Compose @('up', '-d', '--build') }
    'down' { Compose @('down') }
    'weg' { Compose @('down', '-v') }
    'status' {
        Compose @('ps')
        curl.exe -sk "$adresse/api/v1/version"
        Write-Host ''
    }
    'absender' {
        # Gemessen wird am Verhalten der Anmeldebremse (10 je Adresse und Minute), nicht am Log:
        # Nur wenn die API die weitergereichte Adresse sieht, trennt sie A von B.
        $a = "203.0.113.$(Get-Random -Minimum 1 -Maximum 127)"
        $b = "203.0.113.$(Get-Random -Minimum 128 -Maximum 254)"
        $tunnelA = @(Anmeldeversuche '172.30.78.11' $a 11)
        $tunnelB = @(Anmeldeversuche '172.30.78.11' $b 1)
        $fremd = @(Anmeldeversuche '172.30.78.12' $a 1)
        $lokal = curl.exe -sk -o NUL -w '%{http_code}' -H "Cf-Connecting-Ip: $a" -H 'Content-Type: application/json' `
            -d '{"userName":"probe-gibt-es-nicht","password":"x"}' "$adresse/api/v1/auth/login"
        Write-Host "Tunnel, Absender $a (11x): $($tunnelA -join ' ')"
        Write-Host "Tunnel, Absender $b (1x):  $($tunnelB -join ' ')"
        Write-Host "andere Adresse im Netz mit Kopf ${a}: $($fremd -join ' ')"
        Write-Host "lokale Tuer mit Kopf ${a}: $lokal"
        $gut = $tunnelA.Count -eq 11 -and $tunnelA[10] -eq '429' -and -not ($tunnelA[0..9] -ne '401') -and
            $tunnelB[0] -eq '401' -and $fremd[0] -eq '401' -and $lokal -eq '401'
        if (-not $gut) { Write-Host 'ROT: Die API trennt die Absender nicht wie erwartet.'; exit 1 }
        Write-Host 'GRUEN: echte Absenderadresse nur vom Tunnel, je Absender eine eigene Bremse.'
    }
    'smoke' {
        $env:QUIZZER_E2E_URL = $adresse
        $env:QUIZZER_E2E_USER = Wert 'QUIZZER_ADMIN_USER'
        $env:QUIZZER_E2E_PASSWORD = Wert 'QUIZZER_ADMIN_PASSWORD'
        $env:QUIZZER_E2E_PIN = (Get-Content certs/server.sha256 -Raw).Trim()
        dotnet test ../Quizzer.Server.Tests --filter StackSmoke
    }
}
