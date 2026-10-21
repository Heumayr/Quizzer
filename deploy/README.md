# Quizzer-Server betreiben

SQL Server, Migration, REST-API und davor **Caddy** (TLS) laufen als vier Container. Daten und Medien liegen in zwei
Docker-Volumes (`quizzer-sql`, `quizzer-media`) und überleben jeden Neustart und Neubau.
Plan und Begründung: `.harness/docs/server-trennung.md`.

## Was gebraucht wird

- Ein x86-Rechner mit **Docker Desktop** (Windows) oder Docker Engine (Linux), mindestens 4 GB RAM
  frei — SQL Server allein will 2 GB.
- **Tailscale** auf diesem Rechner, für den Zugang der Freunde (Entscheidung F20).
- Dieses Repo.

## Einrichten (einmal)

```powershell
cd deploy
.\new-secrets.ps1          # legt .env mit Zufallsgeheimnissen an (nie committen)
```

**Zertifikat**: von außen gibt es nur HTTPS, und das macht Caddy mit den Dateien aus `certs\`.
Mit Tailscale:

```powershell
tailscale cert --cert-file certs\server.crt --key-file certs\server.key <rechnername>.<tailnet>.ts.net
```

Das Zertifikat gilt 90 Tage und erneuert sich **nicht von selbst** — den Befehl in die
Aufgabenplanung legen (monatlich), danach `docker compose restart caddy`.

Caddy holt bewusst **kein** eigenes Zertifikat: der Rechner ist aus dem Internet nicht
erreichbar, und Caddys internes Zertifikat wechselt alle 12 Stunden - im WPF ließe es sich nicht
anheften. Ein `tailscale cert`-Zertifikat ist öffentlich vertrauenswürdig, das WPF braucht dafür
keinen Fingerabdruck.

**Adresse**: in `.env` `QUIZZER_BIND_ADDRESS` auf die Tailscale-Adresse setzen
(`tailscale ip -4`). Dann lauscht der Port nur im Tailnet und nicht im Heimnetz.

```powershell
docker compose up -d --build
docker compose ps            # sql: healthy, migrator: exited (0), api + caddy: running
```

Unter Linux den Schlüssel nur für den Besitzer lesbar machen: `chmod 600 certs/server.key`
(unter Windows liegt er im Benutzerprofil bzw. dem Ordner mit dessen Rechten).

**Erste Anmeldung**: Benutzer `admin` (oder `QUIZZER_ADMIN_USER`), Kennwort aus `.env`
(`QUIZZER_ADMIN_PASSWORD`). In der WPF-App unter *Einstellungen → Server* die Adresse
`https://<rechnername>.<tailnet>.ts.net:8443` eintragen. Danach das Kennwort ändern und
Konten für die Freunde anlegen.

## Was wo läuft

| Container | Aufgabe | Rechte |
|---|---|---|
| `sql` | SQL Server 2022 Express | **kein Port nach außen**, nur im internen Netz |
| `migrator` | bringt das Schema auf Stand, legt die Anmeldung `quizzer_app` an, endet dann | kennt als einziger das `sa`-Kennwort |
| `api` | REST-API, intern http auf 8080, **kein Port nach außen** | meldet sich als `quizzer_app` an: nur lesen und schreiben, kein Schema; Dateisystem nur lesbar |
| `caddy` | TLS auf 8443, Größengrenze, Sicherheitsköpfe, leitet an `api` weiter | Dateisystem nur lesbar, alle Linux-Fähigkeiten entzogen bis auf `NET_BIND_SERVICE` |

**Gebaut wird aus diesem Repo**: `docker compose up -d --build` baut das Server-Abbild aus dem
ausgecheckten Stand (`context: ..`, `Quizzer.Server/Dockerfile`). Die Submodule braucht der
Bau nicht. SQL Server und Caddy kommen als fertige Abbilder (`mssql/server:2022-latest`,
`caddy:2.10.0-alpine`).

**Absenderadresse hinter Caddy:** die Anmeldebremse zählt je Adresse. Die API glaubt
`X-Forwarded-For` nur von Caddys fester Adresse `172.30.77.10` (`Quizzer__TrustedProxyNetworks`,
`/32` - nicht das ganze Netz `edge`, in dem auch das Gateway des Hosts liegt). Wer das Netz in
`docker-compose.yml` ändert, muss beide Stellen ändern. **Grenze:** Docker Desktop reicht veröffentlichte Ports unter Windows meist mit einer
einzigen internen Absenderadresse durch; dann teilen sich alle Freunde eine Bremse
(10 Anmeldeversuche je Minute). Die Kontosperre nach 5 Fehlversuchen wirkt davon unabhängig je
Konto.

Die API startet nicht, solange Migrationen offen sind.

**Bewusste Ausnahme:** zwischen API und SQL Server ist die Verbindung verschlüsselt, das
Zertifikat des SQL Servers wird aber nicht geprüft (`TrustServerCertificate=True`) — es ist
selbst erzeugt, und die Strecke verlässt das interne Docker-Netz nicht. Dasselbe gilt für
Caddy → API (http im Netz `edge`).

## Rauchtest gegen den laufenden Stapel

Ein Test schickt das Demospiel „Alle Fragetypen" von einem „PC" über Caddy zum Server und holt
es auf einem „Laptop" wieder ab - samt Bildern. **Er schreibt in den Stapel**, also nicht gegen den
Server laufen lassen, auf dem die Freunde ihre Spiele haben.

```powershell
$env:QUIZZER_E2E_URL      = "https://<rechnername>.<tailnet>.ts.net:8443"
$env:QUIZZER_E2E_PASSWORD = "<Admin-Kennwort>"
# nur bei selbst erzeugtem Zertifikat: $env:QUIZZER_E2E_PIN = "<SHA-256 des Zertifikats>"
dotnet test Quizzer.Server.Tests --filter StackSmoke
```

Gemessen 2026-10-21 gegen den Stapel mit Caddy: bestanden (alle 8 Fragetypen, Lösungen, Bilder).

## Sichern und zurückspielen

```powershell
.\backup.ps1                              # nach backups\<Zeitstempel>\
.\restore.ps1 backups\20261021-193006     # hält die API an und startet sie danach wieder
```

Beides ist am 2026-10-21 gegen den laufenden Stapel durchgespielt worden (ein nach der Sicherung
angelegtes Konto war nach dem Zurückspielen wieder weg). **Die Sicherungen sind nicht
verschlüsselt** — auf ein BitLocker-Laufwerk oder einen zweiten Rechner legen.

## Aktualisieren

```powershell
git pull
docker compose up -d --build    # der Migrator bringt das Schema mit
```

## Unter Linux

Statt der `.ps1`-Skripte `new-secrets.sh`, `backup.sh`, `restore.sh`. Für ein Testzertifikat ohne
Tailscale:

```bash
openssl req -x509 -newkey rsa:2048 -nodes -days 30 -subj "/CN=localhost" \
  -addext "subjectAltName=DNS:localhost,IP:127.0.0.1" -keyout certs/server.key -out certs/server.crt
```

Die WPF-App prüft das Zertifikat vollständig. Ein selbst erzeugtes lässt sie nur zu, wenn sein
Fingerabdruck in den Einstellungen hinterlegt ist.
