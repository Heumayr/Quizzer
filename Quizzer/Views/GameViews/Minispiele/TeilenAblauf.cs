using LocalBuzzer.Service.Base.States.Minispiele;
using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Questions.Minispiele;

namespace Quizzer.Views.GameViews.Minispiele
{
    /// <summary>
    /// Teilen oder Nehmen im Fragefenster (M35 C2): Partnerwahl am Telefon, eine Runde, dann der Ausgang am
    /// Beamer und nur Plus-Vorschlaege mit Grund auf den Karten (E1).
    /// </summary>
    internal sealed class TeilenAblauf : MinispielAblauf
    {
        /// <summary>Die Entscheidungen der gezaehlten Runde (<c>true</c> = nimmt).</summary>
        private Dictionary<Guid, bool> entscheidungen = new();

        public TeilenAblauf(CurrentQuestionViewModel fenster)
            : base(fenster, MinigameKind.ShareOrTake)
        {
        }

        public override void ZeigeLaeuft()
        {
            if (Paar is { } paar)
            {
                Anzeige.Zeige($"Teilen oder Nehmen – {Name(paar.A)} und {Name(paar.B)} entscheiden geheim!");
                return;
            }

            var waehler = Zustand?.Rules is IMinigamePairRules { ChooserId: { } id } ? Name(id) : "Der Spielleiter";
            Anzeige.Zeige($"Teilen oder Nehmen – {waehler} wählt einen Partner …");
        }

        /// <summary>Die Partnerwahl ist angenommen: Ton und das Paar an den Beamer (V5).</summary>
        public override void Abgabe(MinigameSubmission abgabe)
        {
            if (abgabe.Value is not PlayerChosen)
                return;

            Klang.Tick();
            ZeigeLaeuft();
        }

        public override void RundeZu(MinigameRoundClosed runde)
        {
            if (!runde.Aborted)
            {
                entscheidungen = runde.Submissions
                    .Where(a => a.Value is bool)
                    .ToDictionary(a => a.Key, a => (bool)a.Value);
            }

            if (Werte() is not { } aufloesung)
            {
                Anzeige.Zeige("Teilen oder Nehmen abgebrochen – es wurde kein Partner gewählt");
                return;
            }

            Anzeige.Zeige((runde.Aborted ? "Minispiel abgebrochen – " : "Teilen oder Nehmen – ") + aufloesung.Ergebnis.Ausgang,
                SpielerwahlTafel.TeilenNehmen(aufloesung.Paar, aufloesung.Ergebnis, Name));
        }

        /// <summary>Eine Runde gibt es nur einmal - Wiederholen vergisst sie wie das Zuruecksetzen.</summary>
        public override void Wiederhole(int runde) => SetzeZurueck();

        public override void SetzeZurueck()
        {
            var gewertet = entscheidungen.Count > 0;
            entscheidungen = new();

            if (gewertet)
                Werte();
        }

        private (TeilenNehmenPaar Paar, TeilenNehmenErgebnis Ergebnis)? Werte()
        {
            if (Paar is not { } paar)
            {
                Karten?.WerteVorschlaege(new Dictionary<Guid, Punktevorschlag>(), _ => string.Empty);
                return null;
            }

            var wahl = new TeilenNehmenPaar(
                paar.A, entscheidungen.TryGetValue(paar.A, out var a) ? a : null,
                paar.B, entscheidungen.TryGetValue(paar.B, out var b) ? b : null);

            var ergebnis = TeilenNehmenEvaluator.Werte(wahl, Feldpunkte, Name);

            Karten?.WerteVorschlaege(ergebnis.Vorschlaege, id => id == paar.A || id == paar.B
                ? $"{TeilenNehmenEvaluator.Describe(id == paar.A ? wahl.NimmtA : wahl.NimmtB)} – {ergebnis.Ausgang}"
                : "Schaut bei Teilen oder Nehmen zu");

            return (wahl, ergebnis);
        }
    }
}
