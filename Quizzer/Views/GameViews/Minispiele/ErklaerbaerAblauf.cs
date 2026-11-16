using LocalBuzzer.Service.Base.States.Minispiele;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Questions.Minispiele;

namespace Quizzer.Views.GameViews.Minispiele
{
    /// <summary>
    /// Erklaerbaer im Fragefenster (M35 C6): waehrend der Runde steht am Beamer nur, wer erklaert - den Begriff sieht
    /// allein der Spielleiter (<see cref="Begriff"/>). Nach jeder Runde der Begriff und wer ihn erraten hat; die
    /// Summe ueber alle Runden geht als Vorschlag mit Grund auf die Karten (E1). „Verraten" wertet der Spielleiter
    /// hier: laeuft die Runde, endet sie; ist sie zu, zaehlt sie nachtraeglich nicht.
    /// </summary>
    internal sealed class ErklaerbaerAblauf : MinispielAblauf
    {
        /// <summary>Die gespielten Runden mit ihrem Begriff - eine wiederholte ueberschreibt ihre alte.</summary>
        private readonly SortedDictionary<int, (ErklaerbaerRunde Runde, string Begriff)> runden = new();

        /// <summary>Runden, die der Spielleiter als verraten gewertet hat.</summary>
        private readonly HashSet<int> verraten = new();

        private ErklaerbaerErgebnis? ergebnis;

        public ErklaerbaerAblauf(CurrentQuestionViewModel fenster)
            : base(fenster, MinigameKind.ExplainIt)
        {
        }

        private ExplainItRules? Regeln => Zustand?.Rules as ExplainItRules;

        /// <summary>Fuer den Spielleiter allein: der Begriff der laufenden oder letzten Runde.</summary>
        public string? Begriff => Regeln?.CurrentTerm;

        public override void ZeigeLaeuft()
        {
            var erklaerer = Regeln?.ChooserId is { } id ? Name(id) : "?";
            Anzeige.Zeige($"Erklärbär – Runde {Zustand?.Round ?? 0} von {Zustand?.Setup.TotalRounds ?? 0}: {erklaerer} erklärt, "
                          + "alle anderen raten am Telefon!", ergebnis != null ? ErklaerbaerTafel.Stand(ergebnis, Spieler(), false) : null);
            Fenster.PaarwahlGeaendert();
        }

        public override void RundeZu(MinigameRoundClosed runde)
        {
            if (!runde.Aborted && Regeln is { ChooserId: { } erklaerer } regeln)
            {
                var rater = runde.Submissions.Where(a => a.Value is ErklaerbaerVersuch { Richtig: true }).Select(a => (Guid?)a.Key).FirstOrDefault();
                runden[runde.Round] = (new ErklaerbaerRunde(erklaerer, rater), regeln.CurrentTerm ?? "?");
                Werte();
            }

            Zeige(runde.Aborted, runde.Aborted ? null : runde.Round, runde.Finished);
        }

        /// <summary>Der Erklaerer hat das Wort gesagt: laeuft die Runde, endet sie; sonst zaehlt die letzte nicht.</summary>
        public async Task VerratenAsync()
        {
            if (Zustand is not { Round: > 0 } zustand || zustand.Aborted)
                return;

            verraten.Add(zustand.Round);

            if (zustand.Phase == MinigamePhase.Running)
            {
                await (Fenster.BuzzerControlsViewModel?.CloseMinigameRoundAsync() ?? Task.CompletedTask);
                return;
            }

            if (!runden.ContainsKey(zustand.Round))
                return;

            Werte();
            Zeige(false, zustand.Round, zustand.Phase == MinigamePhase.Finished);
        }

        /// <summary>Eine wiederholte Runde bekommt einen neuen Begriff - Ergebnis und Wertung der alten zaehlen nicht.</summary>
        public override void Wiederhole(int runde)
        {
            runden.Remove(runde);
            verraten.Remove(runde);
            Werte();
            Fenster.PaarwahlGeaendert();
        }

        public override void SetzeZurueck()
        {
            runden.Clear();
            verraten.Clear();
            Fenster.PaarwahlGeaendert();

            if (ergebnis == null)
                return;

            ergebnis = null;
            Karten?.WerteVorschlaege(new Dictionary<Guid, Punktevorschlag>(), _ => string.Empty);
        }

        /// <summary>Rechnet alle gespielten Runden neu - „Verraten" kann eine schon gewertete aendern.</summary>
        private void Werte()
        {
            var liste = runden.Select(r => r.Value.Runde with { Verraten = verraten.Contains(r.Key) }).ToList();
            ergebnis = ErklaerbaerEvaluator.Werte(liste, Zustand?.Setup.TotalRounds ?? 1, Feldpunkte, Konten());
            var wertung = ergebnis;
            Karten?.WerteVorschlaege(wertung.Vorschlaege, id => ErklaerbaerEvaluator.Karte(wertung, id));
        }

        private void Zeige(bool abgebrochen, int? runde, bool ende)
        {
            Fenster.PaarwahlGeaendert();

            if (ergebnis == null)
            {
                Anzeige.Zeige("Minispiel abgebrochen – diese Runde zählt nicht");
                return;
            }

            var kopf = abgebrochen ? "Minispiel abgebrochen – der Stand"
                : runde is { } r && runden.TryGetValue(r, out var gespielt)
                    ? $"Runde {r}: " + ErklaerbaerEvaluator.Ausgang(gespielt.Runde with { Verraten = verraten.Contains(r) }, gespielt.Begriff, Name)
                    : "Erklärbär";

            Anzeige.Zeige(kopf, ErklaerbaerTafel.Stand(ergebnis, Spieler(), ende || abgebrochen));
        }
    }
}
