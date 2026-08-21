# Quizzer

WPF-Desktop-Anwendung für ein Quizspiel im Jeopardy-Stil; die Mitspieler buzzern über den
Browser ihres Smartphones gegen einen eingebetteten SignalR-Server. Branch `master`.

## Regelwerk

Liegt im Harness-Repo `Heumayr/coding-harness`, lokal unter `e:/Coding_harness/coding-harness`.
Die allgemeinen Regeln gelten für alle Projekte:

@e:/Coding_harness/coding-harness/.harness/coding-standards.md
@e:/Coding_harness/coding-harness/.harness/arbeitsweise.md

`coding-standards.md` sagt, wie der Code aussehen muss; `arbeitsweise.md`, wie eine Sitzung
arbeitet — inklusive Abschnitt 4, **wohin Gedächtnis und Planstand gehören und wann sie
fortgeschrieben werden**.

## Statischer Rahmen und Projektgedächtnis

Stack, Projektstruktur, Architektur (MVVM, Datenzugriff, Spielmodell, Frageschritte,
Buzzer-Dienst), Konfiguration und die projektspezifischen Regeln:

@e:/Coding_harness/coding-harness/.harness/project-harnesses/quizzer/context.md

Projektgedächtnis — Stand, offene Aufgaben, zurückgestellte Entscheidungen, Fallstricke:

@e:/Coding_harness/coding-harness/.harness/project-harnesses/quizzer/memory/MEMORY.md

Ordner: `e:/Coding_harness/coding-harness/.harness/project-harnesses/quizzer/memory/`
Der Index ist zu Beginn leer — die Einträge entstehen während der Arbeit, nicht vorab.

Falls die `@`-Zeilen oben nicht aufgelöst wurden (etwa weil der Freigabedialog für externe
Importe einmal weggeklickt wurde — dann erscheint er nie wieder, siehe `/context` unter
**Memory files**), sind sie **kein Ersatz für das Lesen**: dann `context.md` und den
Memory-Index ausdrücklich selbst öffnen. Liegt das Harness-Repo nicht an diesem Pfad:
`https://github.com/Heumayr/coding-harness`.

## Neues Projektwissen

Gehört ins Harness-Repo, nirgendwo sonst — sonst entstehen zwei Stände, die auseinanderlaufen.
Format siehe `e:/Coding_harness/coding-harness/.harness/memory-convention.md`.

**Diese Datei ist versioniert** — der `.gitignore`-Eintrag dafür wurde am 2026-08-21 entfernt,
damit der Verweis aufs Harness auf jedem Rechner ankommt. Sie bleibt trotzdem reiner Zeiger:
alles, was bleiben soll, gehört ins Harness-Repo, nicht hierher.

## Bevor etwas als fertig gilt

- `dotnet build Quizzer.slnx`
- **Es gibt keine Testprojekte und keine CI.** Der Bau ist die einzige maschinelle Prüfung —
  alles Weitere heißt: `dotnet run --project Quizzer/Quizzer.csproj` und hinsehen
- Die Anwendung startet nur, wenn der Ordner aus `AppSettings.FilePathQuizzer`
  (`Quizzer/appsettings.json`, standardmäßig `E:\QuizzerData`) mit den Bildern existiert
- Beim Bauen darf keine laufende Instanz die DLLs sperren (MSB3021/MSB3027)
