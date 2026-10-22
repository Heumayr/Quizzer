# Richtet die beiden geplanten Aufgaben unter Windows ein (einmal ausfuehren, als der Benutzer,
# unter dem Docker Desktop laeuft):
#   Quizzer Sicherung        taeglich 03:12  -> backup.ps1
#   Quizzer Zertifikat       sonntags 04:17  -> zertifikat-erneuern.ps1
# Docker Desktop laeuft in der Sitzung des Benutzers - die Aufgaben laufen deshalb nur, wenn er
# angemeldet ist ("Interactive"). Der Server-PC sollte angemeldet bleiben (Bildschirm sperren
# genuegt). Ergebnisse stehen in deploy\logs\.
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

$logs = Join-Path $PSScriptRoot 'logs'
New-Item -ItemType Directory -Force $logs | Out-Null

# PowerShell 7 ist Pflicht: nur dort bricht ein fehlgeschlagenes docker/tailscale das Skript ab
# ($PSNativeCommandUseErrorActionPreference). Unter 5.1 meldete eine leere Sicherung Erfolg.
$pwsh = (Get-Command pwsh -ErrorAction SilentlyContinue).Source
if (-not $pwsh) { throw 'PowerShell 7 fehlt (winget install Microsoft.PowerShell).' }

function Register-QuizzerTask([string]$name, [string]$skript, $ausloeser) {
    $befehl = "-NoProfile -ExecutionPolicy Bypass -Command `"& '$(Join-Path $PSScriptRoot $skript)' *>> '$(Join-Path $logs ($skript -replace '\.ps1$', '.log'))'`""
    $aktion = New-ScheduledTaskAction -Execute $pwsh -Argument $befehl -WorkingDirectory $PSScriptRoot
    $einstellung = New-ScheduledTaskSettingsSet -StartWhenAvailable -ExecutionTimeLimit (New-TimeSpan -Hours 1)
    $wer = New-ScheduledTaskPrincipal -UserId $env:USERNAME -LogonType Interactive
    Register-ScheduledTask -TaskName $name -Action $aktion -Trigger $ausloeser -Settings $einstellung -Principal $wer -Force | Out-Null
    Write-Host "Eingerichtet: $name"
}

Register-QuizzerTask 'Quizzer Sicherung' 'backup.ps1' (New-ScheduledTaskTrigger -Daily -At '03:12')
Register-QuizzerTask 'Quizzer Zertifikat' 'zertifikat-erneuern.ps1' (New-ScheduledTaskTrigger -Weekly -DaysOfWeek Sunday -At '04:17')

Write-Host "Pruefen: Aufgabenplanung -> 'Quizzer ...' -> Ausfuehren; Protokolle unter $logs"
