#!/usr/bin/env bash
# Spielt eine Sicherung aus deploy/backups/<Ordner>/ zurueck. Die API wird dafuer angehalten
# und in jedem Fall wieder gestartet - auch wenn das Zurueckspielen scheitert.
#   ./restore.sh backups/20261021-193006
set -euo pipefail
cd "$(dirname "$0")"
source="${1:?Ordner der Sicherung angeben, z.B. backups/20261021-193006}"
[ -f "$source/quizzer.bak" ] || { echo "$source/quizzer.bak fehlt"; exit 1; }

sql() { docker compose exec -T sql bash -c "/opt/mssql-tools18/bin/sqlcmd -C -b -S localhost -U sa -P \"\$MSSQL_SA_PASSWORD\" -Q \"$1\""; }

finish() {
  # Die Datenbank darf nie im Einzelbenutzermodus liegen bleiben.
  sql "IF DB_ID('Quizzer') IS NOT NULL ALTER DATABASE [Quizzer] SET MULTI_USER;" || true
  # Der Migrator legt die Betriebsanmeldung neu an - die Datenbank-Benutzer kommen mit der
  # Sicherung zurueck, die Server-Anmeldung lebt aber in master.
  docker compose up -d --no-build migrator api
}
trap finish EXIT

docker compose stop api
docker compose cp "$source/quizzer.bak" sql:/var/opt/mssql/backup/restore.bak
# docker cp legt die Datei als root an; SQL Server laeuft als mssql und muss sie lesen.
docker compose exec -T -u root sql chown mssql /var/opt/mssql/backup/restore.bak

sql "ALTER DATABASE [Quizzer] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;"
sql "RESTORE DATABASE [Quizzer] FROM DISK = N'/var/opt/mssql/backup/restore.bak' WITH REPLACE, CHECKSUM;"

if [ -f "$source/medien.tar.gz" ]; then
  docker run --rm -v quizzer_quizzer-media:/data -v "$PWD/$source":/in:ro mcr.microsoft.com/dotnet/runtime-deps:10.0 \
    sh -c "find /data -mindepth 1 -delete && tar xzf /in/medien.tar.gz -C /data && chown -R 1654:1654 /data"
fi

echo "Zurueckgespielt aus $source"
