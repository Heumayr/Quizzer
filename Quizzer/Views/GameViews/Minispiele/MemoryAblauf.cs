using LocalBuzzer.Service.Base.States.Minispiele;
using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Questions.Minispiele;

namespace Quizzer.Views.GameViews.Minispiele
{
    /// <summary>
    /// Memory im Fragefenster (M35 C5): jedes Umdrehen treibt die Paare je Spieler am Beamer - nie die
    /// Belegung, dort schauen alle mit. Am Ende Rang nach Zeit, sonst nach Paaren; die Vorderen bekommen
    /// die Feldpunkte als Vorschlag mit Grund (E1).
    /// </summary>
    internal sealed class MemoryAblauf : MinispielAblauf
    {
        /// <summary>Der letzte Stand je Spieler - das Rennen.</summary>
        private readonly Dictionary<Guid, MemoryStand> staende = new();

        /// <summary>Die Wertung der gezaehlten Runde; ein Abbruch danach behaelt sie.</summary>
        private MemoryErgebnis? ergebnis;

        public MemoryAblauf(CurrentQuestionViewModel fenster)
            : base(fenster, MinigameKind.Memory)
        {
        }

        private int PaareGesamt => (Zustand?.Rules as MemoryRules)?.PaareGesamt ?? MemoryBelegung.PaareFuer(Frage?.Variant ?? 0);

        public override void ZeigeLaeuft()
            => Anzeige.Zeige($"Memory läuft – {PaareGesamt} Paare, höchstens {Frage?.DurationSeconds ?? 0} Sekunden!",
                MemoryTafel.Rennen(staende, PaareGesamt, Spieler()));

        /// <summary>Ein Umdrehen ist angenommen: die Paare laufen mit; wer fertig ist, bekommt einen Ton.</summary>
        public override void Abgabe(MinigameSubmission abgabe)
        {
            // Nach dem Schliessen steht das Ergebnis am Beamer - ein spaet verteilter Zug darf es nicht ueberschreiben.
            if (abgabe.Value is not MemoryZug zug || Zustand?.Phase != MinigamePhase.Running)
                return;

            staende[abgabe.Player.Id] = zug.Stand;

            if (zug.Stand.Fertig != null)
                Klang.Tick();

            ZeigeLaeuft();
        }

        public override void RundeZu(MinigameRoundClosed runde)
        {
            if (!runde.Aborted)
            {
                var endstaende = runde.Submissions
                    .Where(a => a.Value is MemoryStand)
                    .ToDictionary(a => a.Key, a => (MemoryStand)a.Value);

                ergebnis = MemoryEvaluator.Werte(endstaende, PaareGesamt, Feldpunkte, Konten());
                Karten?.WerteVorschlaege(ergebnis.Vorschlaege, id => MemoryEvaluator.Describe(ergebnis, id));
            }

            if (ergebnis == null)
            {
                Anzeige.Zeige("Minispiel abgebrochen – diese Runde zählt nicht");
                return;
            }

            Anzeige.Zeige(runde.Aborted ? "Minispiel abgebrochen – der Stand" : "Ergebnis – wer zuerst fertig ist, gewinnt",
                MemoryTafel.Ende(ergebnis, Spieler()));
        }

        /// <summary>Eine Runde gibt es nur einmal - Wiederholen vergisst sie wie das Zuruecksetzen.</summary>
        public override void Wiederhole(int runde) => SetzeZurueck();

        /// <summary>Vergisst Rennen und Wertung; war schon gewertet, auch die Vorschlaege.</summary>
        public override void SetzeZurueck()
        {
            staende.Clear();

            if (ergebnis == null)
                return;

            ergebnis = null;
            Karten?.WerteVorschlaege(new Dictionary<Guid, Punktevorschlag>(), _ => string.Empty);
        }
    }
}
