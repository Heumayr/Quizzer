# Legt deploy\.env (oder -Datei, etwa .env.pruef) mit Zufallsgeheimnissen an. Ueberschreibt nie eine bestehende.
param([string]$Datei = '.env')
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
if (Test-Path $Datei) { Write-Host "$Datei gibt es schon - nichts geaendert."; exit 0 }

function New-Secret([int]$bytes) {
    $buffer = [byte[]]::new($bytes)
    [System.Security.Cryptography.RandomNumberGenerator]::Fill($buffer)
    return $buffer
}
function New-Password { 'Q' + (([Convert]::ToBase64String((New-Secret 33)) -replace '[/+=]', '').Substring(0, 30)) + 'a1!' }

$text = Get-Content .env.example -Raw
$text = $text -replace '(?m)^QUIZZER_SA_PASSWORD=.*$', "QUIZZER_SA_PASSWORD=$(New-Password)"
$text = $text -replace '(?m)^QUIZZER_APP_PASSWORD=.*$', "QUIZZER_APP_PASSWORD=$(New-Password)"
$text = $text -replace '(?m)^QUIZZER_JWT_KEY=.*$', "QUIZZER_JWT_KEY=$([Convert]::ToBase64String((New-Secret 48)))"
$text = $text -replace '(?m)^QUIZZER_ADMIN_PASSWORD=.*$', "QUIZZER_ADMIN_PASSWORD=$(New-Password)"
# Ein zufaelliger Admin-Name statt "admin" - den bekannten Namen koennte jeder sperren.
$adminName = 'leitung-' + [Convert]::ToHexString((New-Secret 3)).ToLowerInvariant()
$text = $text -replace '(?m)^QUIZZER_ADMIN_USER=.*$', "QUIZZER_ADMIN_USER=$adminName"
Set-Content $Datei $text -NoNewline -Encoding utf8

# Nur der eigene Benutzer darf die Datei lesen.
if ($IsWindows -or $PSVersionTable.PSEdition -eq 'Desktop') {
    $acl = Get-Acl $Datei
    $acl.SetAccessRuleProtection($true, $false)
    $acl.AddAccessRule([System.Security.AccessControl.FileSystemAccessRule]::new($env:USERNAME, 'FullControl', 'Allow'))
    Set-Acl $Datei $acl
}
else {
    chmod 600 $Datei
}

Write-Host "$Datei angelegt. Erste Anmeldung: Benutzer $adminName, Kennwort in QUIZZER_ADMIN_PASSWORD - danach aendern."
