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
- **Keine CI** — der Bau und die Tests auf diesem Rechner sind die einzige maschinelle Prüfung
- Die Anwendung startet nur, wenn der Ordner aus `AppSettings.FilePathQuizzer`
  (`Quizzer/appsettings.json`, standardmäßig `E:\QuizzerData`) mit den Bildern existiert
- Beim Bauen darf keine laufende Instanz die DLLs sperren (MSB3021/MSB3027)

## Nach einem Neuklon

```powershell
git submodule update --init
```

<!-- Wartungsnotiz: Diese Datei traegt nur Zeiger und Build-Kommandos. Fallstricke gehoeren in
     .harness/memory/was-gilt.md - nicht hierher. -->
