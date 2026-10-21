#!/usr/bin/env bash
# Legt deploy/.env mit Zufallsgeheimnissen an. Ueberschreibt nie eine bestehende .env.
set -euo pipefail
cd "$(dirname "$0")"
if [ -f .env ]; then echo ".env gibt es schon - nichts geaendert."; exit 0; fi
pw() { echo "Q$(openssl rand -base64 33 | tr -d '/+=' | cut -c1-30)a1!"; }
sed -e "s|^QUIZZER_SA_PASSWORD=.*|QUIZZER_SA_PASSWORD=$(pw)|" \
    -e "s|^QUIZZER_APP_PASSWORD=.*|QUIZZER_APP_PASSWORD=$(pw)|" \
    -e "s|^QUIZZER_JWT_KEY=.*|QUIZZER_JWT_KEY=$(openssl rand -base64 48)|" \
    -e "s|^QUIZZER_ADMIN_PASSWORD=.*|QUIZZER_ADMIN_PASSWORD=$(pw)|" \
    .env.example > .env
chmod 600 .env
echo ".env angelegt. Admin-Kennwort steht darin (QUIZZER_ADMIN_PASSWORD) - nach der ersten Anmeldung aendern."
