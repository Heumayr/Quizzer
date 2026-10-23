# Uebernimmt den Bestand des Einzelbetriebs in den Docker-Stapel (M24): die LocalDB des WPF samt
# Datenordner. Danach traegt der Server Spiele, Fragen, Mitspieler, Ergebnisse, Designs und Medien.
#
#   .\bestand-uebernehmen.ps1                       Vorgaben aus Quizzer.DataModels\appsettings.json
#   .\bestand-uebernehmen.ps1 -Design ''            ohne neues Design aus den Grundtexturen
#   .\bestand-uebernehmen.ps1 -Ersetzen             auch wenn der Server schon Daten hat - sie gehen verloren
#
# Was geschieht:
#   1. Sicherung der lokalen Datenbank (COPY_ONLY - eine bestehende Sicherungskette bleibt heil)
#   2. API anhalten, Sicherung im Container zurueckspielen, Medienablage ersetzen
#   3. Migrator: Schema auf Stand, Betriebsanmeldung neu (sie lebt in master, nicht in der Sicherung)
#   4. Design <Design> aus den Grundtexturen anlegen und jedem Spiel ohne Design zuweisen - die
#      Wurzeltexturen laedt der Client nie hoch, nur Themes\<Ordner> (ClientSyncService). Dazu
#      liegen sie oben in der Ablage; von dort gibt sie der Server heraus (MediaService.SkinFiles)
#   5. API und Caddy starten; beim ersten Start entsteht das Admin-Konto aus .env neu
#   6. Zaehlen: Server gegen lokal
#
# Die lokale Datenbank und der Datenordner werden nur gelesen. Braucht PowerShell 7 und sqlcmd.
param(
    [string]$Server = '(localdb)\MSSQLLocalDB',
    [string]$Datenbank,
    [string]$Datenordner,
    [string]$Design = 'Hausdesign',
    [switch]$Ersetzen
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
Set-Location $PSScriptRoot
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'Bitte mit PowerShell 7 starten (pwsh).' }
if (-not (Test-Path .env)) { throw '.env fehlt - zuerst .\new-secrets.ps1' }
if (-not (Get-Command sqlcmd -ErrorAction SilentlyContinue)) { throw 'sqlcmd fehlt (kommt mit SQL Server LocalDB bzw. den SQL-Befehlszeilentools).' }

# --- Vorgaben wie im WPF: appsettings.json der DataModels, darueber %APPDATA%\Quizzer\settings.json
$vorgaben = (Get-Content (Join-Path $PSScriptRoot '..\Quizzer.DataModels\appsettings.json') -Raw | ConvertFrom-Json).AppSettings
$eigene = Join-Path $env:APPDATA 'Quizzer\settings.json'
$eigeneWerte = if (Test-Path $eigene) { Get-Content $eigene -Raw | ConvertFrom-Json } else { $null }
if (-not $Datenordner) { $Datenordner = if ($eigeneWerte.FilePathQuizzer) { $eigeneWerte.FilePathQuizzer } else { $vorgaben.FilePathQuizzer } }
if (-not $Datenbank) {
    $verbindung = if ($eigeneWerte.MsSqlConString) { $eigeneWerte.MsSqlConString } else { $vorgaben.MsSqlConString }
    $Datenbank = if ($verbindung -match '(?i)(Database|Initial Catalog)\s*=\s*([^;]+)') { $Matches[2].Trim() } else { 'Quizzer' }
}
if (-not (Test-Path $Datenordner)) { throw "Datenordner $Datenordner fehlt." }
# Der Name wird Ordner und landet in SQL - nur Buchstaben, Ziffern, Leerzeichen, _ und -.
if ($Design -and $Design -notmatch '^[\p{L}\p{N}][\p{L}\p{N} _\-]{0,63}$') { throw "Ungueltiger Designname: $Design" }

function Invoke-Lokal([string]$query) {
    sqlcmd -S $Server -E -b -h -1 -W -d $Datenbank -Q "SET NOCOUNT ON; $query"
}
function Invoke-Container([string]$query, [string]$db = 'master') {
    docker compose exec -T sql bash -c "/opt/mssql-tools18/bin/sqlcmd -C -b -h -1 -W -S localhost -U sa -P `"`$MSSQL_SA_PASSWORD`" -d $db -Q `"SET NOCOUNT ON; $query`""
}
function Get-Zahlen([scriptblock]$abfrage, [string]$mitspieler = 'base.Player') {
    $zeilen = & $abfrage "SELECT 'Spiele', COUNT(*) FROM base.Game UNION ALL SELECT 'Fragen', COUNT(*) FROM question.QuestionBase UNION ALL SELECT 'Schritte', COUNT(*) FROM question.QuestionStepResource UNION ALL SELECT 'Mitspieler', COUNT(*) FROM $mitspieler UNION ALL SELECT 'Kategorien', COUNT(*) FROM base.Category UNION ALL SELECT 'Ergebnisse', COUNT(*) FROM question.QuestionResult UNION ALL SELECT 'Designs', COUNT(*) FROM base.GameTheme"
    $zahlen = [ordered]@{}
    foreach ($zeile in $zeilen) { if ($zeile -match '^(\S+)\s+(\d+)$') { $zahlen[$Matches[1]] = [int]$Matches[2] } }
    return $zahlen
}

# --- 0. Riegel: ein Server mit eigenen Daten wird nicht stillschweigend ueberschrieben
if ((docker compose ps --status running --services) -notcontains 'sql') { throw 'Der Dienst sql laeuft nicht - zuerst: docker compose up -d --build' }
# EXEC: ohne die Datenbank scheiterte der Verweis auf ihre Tabellen schon beim Uebersetzen.
$vorhanden = Invoke-Container "IF DB_ID('Quizzer') IS NULL SELECT 0 ELSE EXEC(N'SELECT (SELECT COUNT(*) FROM Quizzer.base.Game) + (SELECT COUNT(*) FROM Quizzer.question.QuestionBase) + CASE WHEN (SELECT COUNT(*) FROM Quizzer.auth.Account) > 1 THEN 1 ELSE 0 END')"
if ([int]($vorhanden | Select-Object -Last 1) -gt 0 -and -not $Ersetzen) {
    throw 'Der Server hat schon Spiele, Fragen oder mehr als das erste Konto. Nichts geaendert. Wer sie ersetzen will: -Ersetzen (vorher .\backup.ps1).'
}

$arbeit = Join-Path ([IO.Path]::GetTempPath()) "quizzer-uebernahme-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
$medien = Join-Path $arbeit 'medien'
New-Item -ItemType Directory -Force $medien | Out-Null

try {
    # --- 1. Lokal lesen: Sicherung, Dateinamen der Datenbank, Spielerbilder, Zahlen
    $lokal = Get-Zahlen { param($q) Invoke-Lokal $q }
    $bak = Join-Path $arbeit 'quizzer.bak'
    Invoke-Lokal "BACKUP DATABASE [$Datenbank] TO DISK = N'$bak' WITH COPY_ONLY, INIT, CHECKSUM" | Out-Null
    $dateien = Invoke-Lokal "SELECT type, name FROM sys.database_files" | Where-Object { $_ -match '^\d\s' }
    $datenName = ($dateien | Where-Object { $_ -match '^0\s' } | Select-Object -First 1) -replace '^0\s+', ''
    $logName = ($dateien | Where-Object { $_ -match '^1\s' } | Select-Object -First 1) -replace '^1\s+', ''
    $bilder = Invoke-Lokal "SELECT DISTINCT UserPictureFileName FROM base.Player WHERE UserPictureFileName IS NOT NULL AND UserPictureFileName <> ''" | Where-Object { $_ }

    # --- Medien so ablegen, wie der Server sie fuehrt (MediaPaths): Resources\, Themes\<Ordner>\, Spielerbilder oben
    foreach ($ordner in 'Resources', 'Themes') {
        $quelle = Join-Path $Datenordner $ordner
        if (Test-Path $quelle) { Copy-Item $quelle (Join-Path $medien $ordner) -Recurse }
    }
    foreach ($bild in $bilder) {
        $datei = Join-Path $Datenordner $bild
        if (Test-Path $datei) { Copy-Item $datei $medien } else { Write-Warning "Spielerbild fehlt im Datenordner: $bild" }
    }

    # Die Grundtexturen oben in der Ablage gibt der Server jedem Konto heraus - die Namen stehen in
    # MediaService.SkinFiles, der Liste, nach der er ausliefert. So sieht auch ein Rechner, dessen
    # Medienordner unter ProgramData leer beginnt (M26), die Fenster ausserhalb eines Spiels wie hier.
    $skinBlock = [regex]::Match((Get-Content (Join-Path $PSScriptRoot '..\Quizzer.Server\Media\MediaService.cs') -Raw),
        'SkinFiles\s*=\s*new[^{]*\{(?<namen>[^}]*)\}').Groups['namen'].Value
    $grundtexturen = @([regex]::Matches($skinBlock, '"([^"]+\.png)"') | ForEach-Object { $_.Groups[1].Value })
    if (-not $grundtexturen) { throw 'MediaService.SkinFiles nicht gefunden - das Skript passt nicht mehr zum Server.' }
    $grundtexturen = @($grundtexturen | Where-Object { Test-Path (Join-Path $Datenordner $_) })
    foreach ($datei in $grundtexturen) { Copy-Item (Join-Path $Datenordner $datei) $medien }

    $texturen = @()
    if ($Design) {
        $designOrdner = Join-Path $medien "Themes\$Design"
        if (Test-Path $designOrdner) { throw "Unter Themes gibt es schon einen Ordner $Design - anderen Namen waehlen (-Design)." }
        # Die Dateinamen kommen aus ThemeAssets.Texturen, der einzigen Quelle dafuer - eine neue
        # Textur dort wandert damit ohne Zutun mit.
        $texturen = Select-String -Path (Join-Path $PSScriptRoot '..\Quizzer.DataModels\Themes\ThemeAssets.cs') -Pattern 'new\("[^"]+",\s*"([^"]+\.png)"' |
            ForEach-Object { $_.Matches[0].Groups[1].Value } |
            Where-Object { Test-Path (Join-Path $Datenordner $_) }
        if (-not $texturen) { throw "Im Datenordner liegt keine der Grundtexturen aus ThemeAssets - ohne Design weiter: -Design ''" }
        New-Item -ItemType Directory -Force $designOrdner | Out-Null
        foreach ($textur in $texturen) { Copy-Item (Join-Path $Datenordner $textur) $designOrdner }
    }
    $medienZahl = (Get-ChildItem $medien -Recurse -File).Count

    # --- 2. Zurueckspielen
    docker compose stop api
    # Im frischen Container gibt es den Ordner noch nicht - backup.ps1 legt ihn erst an.
    docker compose exec -T sql mkdir -p /var/opt/mssql/backup
    docker compose cp $bak sql:/var/opt/mssql/backup/uebernahme.bak
    # docker cp legt die Datei als root an; SQL Server laeuft als mssql und muss sie lesen.
    docker compose exec -T -u root sql chown mssql /var/opt/mssql/backup/uebernahme.bak
    Invoke-Container "IF DB_ID('Quizzer') IS NOT NULL ALTER DATABASE [Quizzer] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;" | Out-Null
    Invoke-Container "RESTORE DATABASE [Quizzer] FROM DISK = N'/var/opt/mssql/backup/uebernahme.bak' WITH REPLACE, CHECKSUM, MOVE N'$datenName' TO N'/var/opt/mssql/data/Quizzer.mdf', MOVE N'$logName' TO N'/var/opt/mssql/data/Quizzer_log.ldf';" | Out-Null
    # Besitzer war das Windows-Konto der LocalDB - im Container gibt es das nicht.
    Invoke-Container "ALTER AUTHORIZATION ON DATABASE::[Quizzer] TO [sa]; ALTER DATABASE [Quizzer] SET MULTI_USER;" | Out-Null
    docker compose exec -T -u root sql rm -f /var/opt/mssql/backup/uebernahme.bak

    docker run --rm -v quizzer_quizzer-media:/data -v "$((Resolve-Path $medien).Path):/in:ro" mcr.microsoft.com/dotnet/runtime-deps:10.0 `
        sh -c 'find /data -mindepth 1 -delete && cp -R /in/. /data/ && chown -R 1654:1654 /data'

    # --- 3. Schema auf Stand bringen, bevor jemand die Daten sieht
    docker compose up -d --no-build migrator
    docker compose wait migrator | Out-Null

    # --- 4. Das Design aus den Grundtexturen - als Datei, damit die Umlaute heil ankommen
    if ($Design) {
        $sql = Join-Path $arbeit 'design.sql'
        @"
SET NOCOUNT ON;
DECLARE @ordner nvarchar(200) = N'$Design';
IF NOT EXISTS (SELECT 1 FROM base.GameTheme WHERE FolderName = @ordner)
    INSERT INTO base.GameTheme (Id, FolderName, BackgroundColor, ForegroundColor, CellTextColor, HeaderTextColor, Notes, Designation)
    VALUES (NEWID(), @ordner, N'', N'', N'', N'', N'Übernommen am $(Get-Date -Format 'yyyy-MM-dd') aus den Grundtexturen des Einzelbetriebs ($($texturen.Count) Texturen).', @ordner);
UPDATE base.Game SET GameThemeId = (SELECT Id FROM base.GameTheme WHERE FolderName = @ordner) WHERE GameThemeId IS NULL;
"@ | Set-Content $sql -Encoding utf8NoBOM
        docker compose cp $sql sql:/tmp/design.sql
        docker compose exec -T sql bash -c "/opt/mssql-tools18/bin/sqlcmd -C -b -f 65001 -S localhost -U sa -P `"`$MSSQL_SA_PASSWORD`" -d Quizzer -i /tmp/design.sql"
    }
}
finally {
    # Die Datenbank darf nie im Einzelbenutzermodus liegen bleiben, und die API nie angehalten.
    try { Invoke-Container "IF DB_ID('Quizzer') IS NOT NULL ALTER DATABASE [Quizzer] SET MULTI_USER;" | Out-Null } catch { Write-Warning "MULTI_USER nicht gesetzt: $_" }
    docker compose up -d --no-build migrator api caddy
    # Die Sicherung ist unverschluesselt - sie bleibt nicht im Temp-Ordner liegen.
    Remove-Item $arbeit -Recurse -Force -ErrorAction SilentlyContinue
}

# --- 6. Nachzaehlen
# Mitspieler am Server nur ohne Konto: das Admin-Konto bringt beim API-Start einen eigenen mit,
# und wann der Start so weit ist, haengt nicht von diesem Skript ab.
$amServer = Get-Zahlen { param($q) Invoke-Container $q 'Quizzer' } '(SELECT Id FROM base.Player WHERE Id NOT IN (SELECT PlayerId FROM auth.Account)) p'
$ohneDesign = [int](Invoke-Container 'SELECT COUNT(*) FROM base.Game WHERE GameThemeId IS NULL' 'Quizzer' | Select-Object -Last 1)
$imVolume = [int](docker run --rm -v quizzer_quizzer-media:/data:ro mcr.microsoft.com/dotnet/runtime-deps:10.0 sh -c 'find /data -type f | wc -l')

$erwartet = [ordered]@{}
foreach ($name in $lokal.Keys) { $erwartet[$name] = $lokal[$name] }
if ($Design) { $erwartet['Designs'] += 1 }
$abweichung = $false
Write-Host ''
Write-Host ('{0,-13} {1,8} {2,7}' -f '', 'erwartet', 'Server')
foreach ($name in $erwartet.Keys) {
    $marke = if ($amServer[$name] -ne $erwartet[$name]) { $abweichung = $true; '  <- ABWEICHUNG' } else { '' }
    Write-Host ('{0,-13} {1,8} {2,7}{3}' -f $name, $erwartet[$name], $amServer[$name], $marke)
}
Write-Host ('{0,-13} {1,8} {2,7}' -f 'Mediendateien', $medienZahl, $imVolume)
Write-Host "Grundtexturen oben in der Ablage: $($grundtexturen.Count)"
if ($Design) { Write-Host "Design $Design mit $($texturen.Count) Texturen; Spiele ohne Design: $ohneDesign" }
if ($abweichung -or $imVolume -ne $medienZahl -or ($Design -and $ohneDesign -gt 0)) { throw 'Die Zahlen stimmen nicht - siehe oben.' }
Write-Host 'Uebernommen. Anmeldung im WPF mit QUIZZER_ADMIN_USER aus .env.'
