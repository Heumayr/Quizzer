# Spielt eine Sicherung aus deploy\backups\<Ordner>\ zurueck. Die API wird dafuer angehalten
# und in jedem Fall wieder gestartet - auch wenn das Zurueckspielen scheitert.
#   .\restore.ps1 backups\20261021-193006
param([Parameter(Mandatory)][string]$Source)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
Set-Location $PSScriptRoot

$bak = Join-Path $Source 'quizzer.bak'
if (-not (Test-Path $bak)) { throw "$bak fehlt" }

function Invoke-Sql([string]$query) {
    docker compose exec -T sql bash -c "/opt/mssql-tools18/bin/sqlcmd -C -b -S localhost -U sa -P `"`$MSSQL_SA_PASSWORD`" -Q `"$query`""
}

try {
    docker compose stop api
    docker compose cp $bak sql:/var/opt/mssql/backup/restore.bak
    # docker cp legt die Datei als root an; SQL Server laeuft als mssql und muss sie lesen.
    docker compose exec -T -u root sql chown mssql /var/opt/mssql/backup/restore.bak

    Invoke-Sql 'ALTER DATABASE [Quizzer] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;'
    Invoke-Sql "RESTORE DATABASE [Quizzer] FROM DISK = N'/var/opt/mssql/backup/restore.bak' WITH REPLACE, CHECKSUM;"

    $media = Join-Path $Source 'medien.tar.gz'
    if (Test-Path $media) {
        docker run --rm -v quizzer_quizzer-media:/data -v "$((Resolve-Path $Source).Path):/in:ro" mcr.microsoft.com/dotnet/runtime-deps:10.0 `
            sh -c 'find /data -mindepth 1 -delete && tar xzf /in/medien.tar.gz -C /data && chown -R 1654:1654 /data'
    }

    Write-Host "Zurueckgespielt aus $Source"
}
finally {
    # Die Datenbank darf nie im Einzelbenutzermodus liegen bleiben.
    try { Invoke-Sql "IF DB_ID('Quizzer') IS NOT NULL ALTER DATABASE [Quizzer] SET MULTI_USER;" } catch { }
    # Der Migrator legt die Betriebsanmeldung neu an (sie lebt in master, nicht in der Sicherung).
    docker compose up -d --no-build migrator api
}
