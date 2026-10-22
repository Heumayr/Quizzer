# Liest deploy\.env in eine Hashtable - ohne die Werte auszugeben. Wird von den anderen Skripten
# mit ". .\env-lesen.ps1" eingebunden.
function Read-QuizzerEnv {
    $werte = @{}
    if (-not (Test-Path (Join-Path $PSScriptRoot '.env'))) { return $werte }
    foreach ($zeile in Get-Content (Join-Path $PSScriptRoot '.env')) {
        if ($zeile -match '^\s*#' -or $zeile -notmatch '=') { continue }
        $name, $wert = $zeile -split '=', 2
        $werte[$name.Trim()] = $wert.Trim()
    }
    return $werte
}
