# Sichert Datenbank und Medien nach deploy\backups\<Zeitstempel>\. Laeuft gegen den laufenden Stapel.
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
Set-Location $PSScriptRoot

$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$target = Join-Path 'backups' $stamp
New-Item -ItemType Directory -Force $target | Out-Null

docker compose exec -T sql bash -c "mkdir -p /var/opt/mssql/backup && /opt/mssql-tools18/bin/sqlcmd -C -b -S localhost -U sa -P `"`$MSSQL_SA_PASSWORD`" -Q `"BACKUP DATABASE [Quizzer] TO DISK = N'/var/opt/mssql/backup/quizzer.bak' WITH INIT, CHECKSUM`""
docker compose cp sql:/var/opt/mssql/backup/quizzer.bak (Join-Path $target 'quizzer.bak')
docker run --rm -v quizzer_quizzer-media:/data:ro -v "$((Resolve-Path $target).Path):/out" mcr.microsoft.com/dotnet/runtime-deps:10.0 tar czf /out/medien.tar.gz -C /data .

Write-Host "Gesichert nach $target"

. (Join-Path $PSScriptRoot 'env-lesen.ps1')
$envWerte = Read-QuizzerEnv

# Eine Kopie auf ein zweites Ziel - eine Sicherung nur auf demselben Rechner ist keine.
$kopieNach = $envWerte['QUIZZER_BACKUP_COPY_TO']
if ($kopieNach) {
    $ziel = Join-Path $kopieNach $stamp
    New-Item -ItemType Directory -Force $ziel | Out-Null
    Copy-Item (Join-Path $target '*') $ziel -Force
    Write-Host "Kopiert nach $ziel"
}
else {
    Write-Warning 'QUIZZER_BACKUP_COPY_TO ist leer - die Sicherung liegt nur auf diesem Rechner.'
}

# Aufbewahrung: nur die neuesten behalten. Die Ordnernamen sind Zeitstempel, also sortierbar.
$behalten = 14
if ($envWerte['QUIZZER_BACKUP_KEEP'] -match '^\d+$' -and [int]$envWerte['QUIZZER_BACKUP_KEEP'] -ge 1) { $behalten = [int]$envWerte['QUIZZER_BACKUP_KEEP'] }

foreach ($ort in @('backups', $kopieNach) | Where-Object { $_ -and (Test-Path $_) }) {
    Get-ChildItem $ort -Directory |
        Where-Object { $_.Name -match '^\d{8}-\d{6}$' } |
        Sort-Object Name -Descending |
        Select-Object -Skip $behalten |
        ForEach-Object { Remove-Item $_.FullName -Recurse -Force; Write-Host "Alte Sicherung entfernt: $($_.FullName)" }
}

Write-Host 'Diese Sicherung ist NICHT verschluesselt - auf einen verschluesselten Datentraeger (BitLocker) oder zweiten Rechner legen.'
