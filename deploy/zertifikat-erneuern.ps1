# Holt das Tailscale-Zertifikat neu und startet Caddy neu, wenn es sich geaendert hat.
# Zum Einplanen (aufgaben-einrichten.ps1, woechentlich) - tailscale cert erneuert selbst nur, wenn
# es noetig ist, ein haeufiger Aufruf schadet also nicht. Das Zertifikat gilt 90 Tage und
# erneuert sich NICHT von allein; vorher stand dafuer nur ein Satz im README.
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
Set-Location $PSScriptRoot

. (Join-Path $PSScriptRoot 'env-lesen.ps1')
$name = (Read-QuizzerEnv)['QUIZZER_TS_HOSTNAME']
if (-not $name) { throw 'QUIZZER_TS_HOSTNAME fehlt in .env (<rechner>.<tailnet>.ts.net).' }

$crt = Join-Path 'certs' 'server.crt'
$key = Join-Path 'certs' 'server.key'
New-Item -ItemType Directory -Force 'certs' | Out-Null

$vorher = if (Test-Path $crt) { (Get-FileHash $crt).Hash } else { '' }

tailscale cert --cert-file $crt --key-file $key $name

if ((Get-FileHash $crt).Hash -ne $vorher) {
    docker compose restart caddy
    Write-Host 'Neues Zertifikat - Caddy neu gestartet.'
}
else {
    Write-Host 'Zertifikat unveraendert.'
}

$ablauf = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new((Resolve-Path $crt).Path).NotAfter
Write-Host "Gueltig bis $ablauf."
if ($ablauf -lt (Get-Date).AddDays(14)) { Write-Warning "Das Zertifikat laeuft in weniger als 14 Tagen ab!"; exit 1 }
