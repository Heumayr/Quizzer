#!/usr/bin/env bash
# Holt das Tailscale-Zertifikat neu und startet Caddy neu, wenn es sich geaendert hat.
# Woechentlich per cron einplanen (README.md); tailscale cert erneuert nur, wenn es noetig ist.
set -euo pipefail
cd "$(dirname "$0")"
source .env
: "${QUIZZER_TS_HOSTNAME:?QUIZZER_TS_HOSTNAME fehlt in .env}"
mkdir -p certs
vorher=$(sha256sum certs/server.crt 2>/dev/null | cut -d' ' -f1 || true)
tailscale cert --cert-file certs/server.crt --key-file certs/server.key "$QUIZZER_TS_HOSTNAME"
chmod 600 certs/server.key
if [ "$(sha256sum certs/server.crt | cut -d' ' -f1)" != "$vorher" ]; then
  docker compose restart caddy; echo "Neues Zertifikat - Caddy neu gestartet."
else
  echo "Zertifikat unveraendert."
fi
openssl x509 -in certs/server.crt -noout -enddate
openssl x509 -in certs/server.crt -noout -checkend $((14*24*3600)) >/dev/null || { echo "WARNUNG: laeuft in weniger als 14 Tagen ab!" >&2; exit 1; }
