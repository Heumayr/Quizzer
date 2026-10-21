# Quizzer-Server betreiben

SQL Server, Migration und REST-API laufen als drei Container. Daten und Medien liegen in zwei
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

**Zertifikat**: die API spricht nur HTTPS. Mit Tailscale:

```powershell
tailscale cert --cert-file certs\server.crt --key-file certs\server.key <rechnername>.<tailnet>.ts.net
```

Das Zertifikat gilt 90 Tage und erneuert sich **nicht von selbst** — den Befehl in die
Aufgabenplanung legen (monatlich), danach `docker compose restart api`.

**Adresse**: in `.env` `QUIZZER_BIND_ADDRESS` auf die Tailscale-Adresse setzen
(`tailscale ip -4`). Dann lauscht der Port nur im Tailnet und nicht im Heimnetz.

```powershell
docker compose up -d --build
docker compose ps            # sql: healthy, migrator: exited (0), api: running
```

**Erste Anmeldung**: Benutzer `admin` (oder `QUIZZER_ADMIN_USER`), Kennwort aus `.env`
(`QUIZZER_ADMIN_PASSWORD`). In der WPF-App unter *Einstellungen → Server* die Adresse
`https://<rechnername>.<tailnet>.ts.net:8443` eintragen. Danach das Kennwort ändern und
Konten für die Freunde anlegen.

## Was wo läuft

| Container | Aufgabe | Rechte |
|---|---|---|
| `sql` | SQL Server 2022 Express | **kein Port nach außen**, nur im internen Netz |
| `migrator` | bringt das Schema auf Stand, legt die Anmeldung `quizzer_app` an, endet dann | kennt als einziger das `sa`-Kennwort |
| `api` | REST-API auf 8443, nur HTTPS | meldet sich als `quizzer_app` an: nur lesen und schreiben, kein Schema |

Die API startet nicht, solange Migrationen offen sind.

**Bewusste Ausnahme:** zwischen API und SQL Server ist die Verbindung verschlüsselt, das
Zertifikat des SQL Servers wird aber nicht geprüft (`TrustServerCertificate=True`) — es ist
selbst erzeugt, und die Strecke verlässt das interne Docker-Netz nicht.

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
