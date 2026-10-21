#!/usr/bin/env bash
# Sichert Datenbank und Medien nach deploy/backups/<Zeitstempel>/. Laeuft gegen den laufenden Stapel.
set -euo pipefail
cd "$(dirname "$0")"
source .env
stamp=$(date +%Y%m%d-%H%M%S)
target="backups/$stamp"
mkdir -p "$target"

docker compose exec -T sql bash -c "mkdir -p /var/opt/mssql/backup && /opt/mssql-tools18/bin/sqlcmd -C -b -S localhost -U sa -P \"\$MSSQL_SA_PASSWORD\" -Q \"BACKUP DATABASE [Quizzer] TO DISK = N'/var/opt/mssql/backup/quizzer.bak' WITH INIT, CHECKSUM\""
docker compose cp sql:/var/opt/mssql/backup/quizzer.bak "$target/quizzer.bak"
docker run --rm -v quizzer_quizzer-media:/data:ro -v "$PWD/$target":/out mcr.microsoft.com/dotnet/runtime-deps:10.0 tar czf /out/medien.tar.gz -C /data .

echo "Gesichert nach $target"
echo "Diese Sicherung ist NICHT verschluesselt - auf einen verschluesselten Datentraeger oder zweiten Rechner legen."
