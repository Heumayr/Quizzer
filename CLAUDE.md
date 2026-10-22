# Quizzer

WPF-Desktop-Anwendung für ein Quizspiel im Jeopardy-Stil; die Mitspieler buzzern über den
Browser ihres Smartphones gegen einen eingebetteten SignalR-Server. Branch `master`.

Regelwerk und Gedächtnis kommen aus den beiden Submodulen:

@.harness-core/regeln/arbeitsweise.md
@.harness-core/regeln/standards-allgemein.md
@.harness/context.md
@.harness/memory/MEMORY.md

Die sprachspezifischen Standards (.harness-core/regeln/bei-bedarf/) laden automatisch über
die .claude/rules-Verknüpfung, sobald passende Dateien berührt werden — legt einrichten.ps1 an.

**Pflichtlektüre vor der ersten Änderung: `.harness/memory/was-gilt.md`.** Neues Projektwissen
gehört ins `.harness`-Submodul (dort committen und pushen), nirgendwo sonst — Format:
`.harness-core/regeln/memory-convention.md`.

## Bevor etwas als fertig gilt

- `dotnet build Quizzer.slnx`
- `dotnet test` je Testprojekt (`Quizzer.LogicUnitTests`, `Quizzer.UnitTests`) — **zwei
  Testprojekte mit je eigener LocalDB**; `DoNotParallelize` wirkt nur innerhalb einer Assembly
  (Einzelheiten: `was-gilt.md` und `quizzer-testaufbau.md`)
- `dotnet test Quizzer.Server.Tests` — Server und Abgleich Ende zu Ende; Datenbank aus
  `QUIZZER_SERVER_TEST_SQL`, sonst LocalDB `Quizzer_Server_Tests` (`.harness/memory/quizzer-server.md`)
- Optional, schreibt in den Stapel: `dotnet test Quizzer.Server.Tests --filter StackSmoke` mit
  `QUIZZER_E2E_URL`/`QUIZZER_E2E_PASSWORD` - Rundlauf PC → Caddy → Laptop (`deploy/README.md`)
- **CI seit 2026-10-22** (`.github/workflows/ci.yml`): Windows baut alles und fährt alle drei
  Testprojekte gegen LocalDB, Linux Logik- und Servertests gegen SQL Server im Container. Rot in
  der CI ist rot - lokal grün ersetzt sie nicht. Lokal ohne LocalDB: `QUIZZER_LOGIC_TEST_SQL`
- Fehlt der Ordner aus `AppSettings.FilePathQuizzer` (`Quizzer.DataModels/appsettings.json`,
  standardmäßig `E:\QuizzerData`), **startet die Anwendung trotzdem** — sie sagt es seit
  2026-09-07 beim Start und bleibt sonst schwarz (`StaticResources` fällt je Bild auf Schwarz
  zurück, es wirft nichts). Zum Arbeiten braucht es ihn trotzdem
- Beim Bauen darf keine laufende Instanz die DLLs sperren (MSB3021/MSB3027)

## Nach einem Neuklon

```powershell
git submodule update --init
```

<!-- Wartungsnotiz: Diese Datei traegt nur Zeiger und Build-Kommandos. Fallstricke gehoeren in
     .harness/memory/was-gilt.md - nicht hierher. -->
