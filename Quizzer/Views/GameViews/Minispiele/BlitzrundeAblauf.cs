using LocalBuzzer.Service.Base.States.Minispiele;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Questions.Minispiele;

namespace Quizzer.Views.GameViews.Minispiele
{
    /// <summary>
    /// Blitzrunde im Fragefenster (M35 C5): waehrend der Runde zeigt der Beamer nur, wie weit jeder ist - ob eine
    /// Antwort stimmte, verriete die Aussage den anderen. Der Endstand bringt je Spieler den Anteil der Feldpunkte
    /// als Vorschlag mit Grund (E1), ein Minus gekappt (E2).
    /// </summary>
    internal sealed class BlitzrundeAblauf : MinispielAblauf
    {
        /// <summary>So viele Aussagen hat jeder beantwortet - das Rennen.</summary>
        private readonly Dictionary<Guid, int> beantwortet = new();

        /// <summary>Die Wertung der gezaehlten Runde; ein Abbruch danach behaelt sie.</summary>
        private BlitzrundeErgebnis? ergebnis;

        public BlitzrundeAblauf(CurrentQuestionViewModel fenster)
            : base(fenster, MinigameKind.LightningRound)
        {
        }

        /// <summary>Gespielt wird, was der Server bekommen hat - nicht, was die Frage inzwischen traegt.</summary>
        private int Aussagen => Zustand?.Setup.Content?.Count ?? 0;

        public override void ZeigeLaeuft()
            => Anzeige.Zeige($"Blitzrunde läuft – {Aussagen} Aussagen: wahr oder falsch?",
                BlitzrundeTafel.Rennen(beantwortet, Aussagen, Spieler()));

        public override void Abgabe(MinigameSubmission abgabe)
        {
            // Nach dem Schliessen steht das Ergebnis am Beamer - eine spaet verteilte Antwort darf es nicht ueberschreiben.
            if (abgabe.Value is not BlitzrundeFortschritt fortschritt || Zustand?.Phase != MinigamePhase.Running)
                return;

            beantwortet[abgabe.Player.Id] = fortschritt.Beantwortet;
            ZeigeLaeuft();
        }

        public override void RundeZu(MinigameRoundClosed runde)
        {
            if (!runde.Aborted)
            {
                var endstaende = runde.Submissions
                    .Where(a => a.Value is BlitzrundeStand)
                    .ToDictionary(a => a.Key, a => (BlitzrundeStand)a.Value);

                ergebnis = BlitzrundeEvaluator.Werte(endstaende, Aussagen, Feldpunkte, Konten());
                Karten?.WerteVorschlaege(ergebnis.Vorschlaege, id => BlitzrundeEvaluator.Describe(ergebnis, id));
            }

            if (ergebnis == null)
            {
                Anzeige.Zeige("Minispiel abgebrochen – diese Runde zählt nicht");
                return;
            }

            Anzeige.Zeige(runde.Aborted ? "Minispiel abgebrochen – der Stand" : "Ergebnis – richtig +1, falsch −1",
                BlitzrundeTafel.Ende(ergebnis, Spieler()));
        }

        /// <summary>Eine Runde gibt es nur einmal - Wiederholen vergisst sie wie das Zuruecksetzen.</summary>
        public override void Wiederhole(int runde) => SetzeZurueck();

        /// <summary>Vergisst Rennen und Wertung; war schon gewertet, auch die Vorschlaege.</summary>
        public override void SetzeZurueck()
        {
            beantwortet.Clear();

            if (ergebnis == null)
                return;

            ergebnis = null;
            Karten?.WerteVorschlaege(new Dictionary<Guid, Punktevorschlag>(), _ => string.Empty);
        }
    }
}
