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
Write-Host 'Diese Sicherung ist NICHT verschluesselt - auf einen verschluesselten Datentraeger (BitLocker) oder zweiten Rechner legen.'
