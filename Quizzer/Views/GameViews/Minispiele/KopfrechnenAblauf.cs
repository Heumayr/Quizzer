using LocalBuzzer.Service.Base.States.Minispiele;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Questions.Minispiele;

namespace Quizzer.Views.GameViews.Minispiele
{
    /// <summary>
    /// Kopfrechnen im Fragefenster (M35 C5): jede angenommene Antwort treibt das Rennen am Beamer, der
    /// Endstand der Runde die Wertung - die meisten richtigen bekommen die Feldpunkte als Vorschlag mit Grund (E1).
    /// </summary>
    internal sealed class KopfrechnenAblauf : MinispielAblauf
    {
        /// <summary>Der letzte Stand je Spieler - das Rennen.</summary>
        private readonly Dictionary<Guid, KopfrechnenStand> staende = new();

        /// <summary>Die Wertung der gezaehlten Runde; ein Abbruch danach behaelt sie.</summary>
        private KopfrechnenErgebnis? ergebnis;

        public KopfrechnenAblauf(CurrentQuestionViewModel fenster)
            : base(fenster, MinigameKind.MentalArithmetic)
        {
        }

        public override void ZeigeLaeuft()
            => Anzeige.Zeige($"Kopfrechnen läuft – {Frage?.DurationSeconds ?? 0} Sekunden, rechnet!",
                KopfrechnenTafel.Rennen(staende, Spieler()));

        /// <summary>Eine Antwort ist angenommen: das Rennen laeuft mit, solange die Runde laeuft.</summary>
        public override void Abgabe(MinigameSubmission abgabe)
        {
            // Nach dem Schliessen steht das Ergebnis am Beamer - eine spaet verteilte Antwort darf es nicht ueberschreiben.
            if (abgabe.Value is not KopfrechnenAntwort antwort || Zustand?.Phase != MinigamePhase.Running)
                return;

            staende[abgabe.Player.Id] = antwort.Stand;
            ZeigeLaeuft();
        }

        public override void RundeZu(MinigameRoundClosed runde)
        {
            if (!runde.Aborted)
            {
                var endstaende = runde.Submissions
                    .Where(a => a.Value is KopfrechnenStand)
                    .ToDictionary(a => a.Key, a => (KopfrechnenStand)a.Value);

                ergebnis = KopfrechnenEvaluator.Werte(endstaende, Feldpunkte, Konten());
                Karten?.WerteVorschlaege(ergebnis.Vorschlaege, id => KopfrechnenEvaluator.Describe(ergebnis, id));
                Karten?.MerkeMesswerte(Messwerte.Kopfrechnen(ergebnis, Frage?.DurationSeconds ?? 0));
            }

            if (ergebnis == null)
            {
                Anzeige.Zeige("Minispiel abgebrochen – diese Runde zählt nicht");
                return;
            }

            Anzeige.Zeige(runde.Aborted ? "Minispiel abgebrochen – der Stand" : "Ergebnis – die meisten richtigen gewinnen",
                KopfrechnenTafel.Ende(ergebnis, Spieler()));
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
            Karten?.MerkeMesswerte(new Dictionary<Guid, int>());
        }
    }
}
