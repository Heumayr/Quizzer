#!/usr/bin/env bash
# Sichert Datenbank und Medien nach deploy/backups/<Zeitstempel>/. Laeuft gegen den laufenden Stapel.
set -euo pipefail

# Sicherungen enthalten die ganze Datenbank samt Kennwort-Hashes: nur fuer den Besitzer lesbar
# (Sicherheitspruefung 2026-10-21).
umask 077
cd "$(dirname "$0")"
source .env
stamp=$(date +%Y%m%d-%H%M%S)
target="backups/$stamp"
mkdir -p "$target"

docker compose exec -T sql bash -c "mkdir -p /var/opt/mssql/backup && /opt/mssql-tools18/bin/sqlcmd -C -b -S localhost -U sa -P \"\$MSSQL_SA_PASSWORD\" -Q \"BACKUP DATABASE [Quizzer] TO DISK = N'/var/opt/mssql/backup/quizzer.bak' WITH INIT, CHECKSUM\""
docker compose cp sql:/var/opt/mssql/backup/quizzer.bak "$target/quizzer.bak"
docker run --rm -v quizzer_quizzer-media:/data:ro -v "$PWD/$target":/out mcr.microsoft.com/dotnet/runtime-deps:10.0 tar czf /out/medien.tar.gz -C /data .

echo "Gesichert nach $target"

# Eine Kopie auf ein zweites Ziel - eine Sicherung nur auf demselben Rechner ist keine.
if [ -n "${QUIZZER_BACKUP_COPY_TO:-}" ]; then
  mkdir -p "$QUIZZER_BACKUP_COPY_TO/$stamp"
  cp "$target"/* "$QUIZZER_BACKUP_COPY_TO/$stamp/"
  echo "Kopiert nach $QUIZZER_BACKUP_COPY_TO/$stamp"
else
  echo "WARNUNG: QUIZZER_BACKUP_COPY_TO ist leer - die Sicherung liegt nur auf diesem Rechner." >&2
fi

# Aufbewahrung: nur die neuesten behalten (Ordnernamen sind Zeitstempel, also sortierbar).
keep="${QUIZZER_BACKUP_KEEP:-14}"
for ort in backups "${QUIZZER_BACKUP_COPY_TO:-}"; do
  [ -n "$ort" ] && [ -d "$ort" ] || continue
  ls -1d "$ort"/*/ 2>/dev/null | grep -E '/[0-9]{8}-[0-9]{6}/$' | sort -r | tail -n +"$((keep + 1))" | while read -r alt; do
    rm -rf "$alt"; echo "Alte Sicherung entfernt: $alt"
  done
done
echo "Diese Sicherung ist NICHT verschluesselt - auf einen verschluesselten Datentraeger oder zweiten Rechner legen."
