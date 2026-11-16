using LocalBuzzer.Service.Base.States.Minispiele;
using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Questions.Minispiele;

namespace Quizzer.Views.GameViews.Minispiele
{
    /// <summary>
    /// Duell im Fragefenster (M35 C2): der Ausloeser waehlt am Telefon, dann zaehlen die Runden der beiden.
    /// Sieger +Anteil, Verlierer −Anteil (gekappt nach E2), beides als Vorschlag mit Grund (E1).
    /// </summary>
    internal sealed class DuellAblauf : MinispielAblauf
    {
        /// <summary>Die gezaehlten Runden; eine Wiederholung ersetzt ihre Runde.</summary>
        private readonly SortedDictionary<int, IReadOnlyDictionary<Guid, object>> runden = new();

        public DuellAblauf(CurrentQuestionViewModel fenster)
            : base(fenster, MinigameKind.Duel)
        {
        }

        private bool IstReaktion => Frage?.Variant == DuellEvaluator.ReaktionVariant;

        public override void ZeigeLaeuft()
        {
            var zustand = Zustand;

            if (Paar is not { } paar)
            {
                var waehler = zustand?.Rules is IMinigamePairRules { ChooserId: { } id } ? Name(id) : "Der Spielleiter";
                Anzeige.Zeige($"Duell – {waehler} wählt einen Gegner …");
                return;
            }

            Anzeige.Zeige($"Runde {zustand?.Round ?? 0} von {zustand?.Setup.TotalRounds ?? 0} – {Name(paar.A)} gegen {Name(paar.B)}: "
                          + (IstReaktion ? "wartet auf Grün!" : "Schere, Stein oder Papier – geheim!"));
        }

        /// <summary>Die Gegnerwahl ist angenommen: Ton und das Paar an den Beamer (V5).</summary>
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
                runden[runde.Round] = runde.Submissions;

            var ergebnis = Werte();

            if (ergebnis == null)
            {
                Anzeige.Zeige("Duell abgebrochen – es wurde kein Gegner gewählt");
                return;
            }

            if (runde.Aborted || runde.Finished)
                ZeigeEnde(ergebnis, runde.Aborted);
            else
                ZeigeRunde(ergebnis, runde);
        }

        private void ZeigeRunde(DuellErgebnis ergebnis, MinigameRoundClosed runde)
        {
            var kopf = $"Runde {runde.Round} von {runde.TotalRounds}";

            if (IstReaktion)
            {
                Anzeige.Zeige($"{kopf} – die Zeiten", ReaktionTafel.Runde(Reaktionen(runden[runde.Round]), Duellanten(ergebnis)));
                return;
            }

            var zuege = Zuege(runden[runde.Round]);
            var sieger = DuellEvaluator.RundenSieger(ergebnis.Herausforderer, ergebnis.Gegner, zuege);

            Anzeige.Zeige($"{kopf} – " + (sieger is { } s ? $"{Name(s)} gewinnt die Runde" : "Gleichstand") + $" · Stand {ergebnis.Stand}",
                SpielerwahlTafel.DuellRunde(ergebnis.Herausforderer, ergebnis.Gegner, zuege, Name));
        }

        private void ZeigeEnde(DuellErgebnis ergebnis, bool abgebrochen)
        {
            var ausgang = ergebnis.Sieger is { } sieger
                ? $"{Name(sieger)} gewinnt das Duell" + (IstReaktion ? string.Empty : $" {ergebnis.Stand}")
                : "Unentschieden – keine Punkte";

            var kopf = (abgebrochen ? "Duell abgebrochen – " : "Ergebnis – ") + ausgang;

            Anzeige.Zeige(kopf, IstReaktion
                ? ReaktionTafel.Ende(ReaktionEvaluator.Werte(runden.Values.Select(Reaktionen)), Duellanten(ergebnis))
                : SpielerwahlTafel.DuellStand(ergebnis, Name));
        }

        public override void Wiederhole(int runde)
        {
            runden.Remove(runde);
            Werte();
        }

        public override void SetzeZurueck()
        {
            runden.Clear();
            Werte();
        }

        /// <summary>Rechnet den Stand und legt die Vorschlaege auf die Karten; <c>null</c> ohne Paar.</summary>
        private DuellErgebnis? Werte()
        {
            if (Paar is not { } paar)
            {
                Karten?.WerteVorschlaege(new Dictionary<Guid, Punktevorschlag>(), _ => string.Empty);
                return null;
            }

            var ergebnis = IstReaktion
                ? DuellEvaluator.WerteReaktion(paar.A, paar.B, runden.Values.Select(Reaktionen))
                : DuellEvaluator.WerteSchereSteinPapier(paar.A, paar.B, runden.Values.Select(Zuege));

            var einsatz = Punkteanteil.Von(Feldpunkte, Frage?.Share ?? 0);
            var vorschlaege = runden.Count == 0
                ? new Dictionary<Guid, Punktevorschlag>()
                : DuellEvaluator.Vorschlaege(ergebnis, einsatz, Konten(), Name);

            Karten?.WerteVorschlaege(vorschlaege, id => Satz(ergebnis, id));
            return ergebnis;
        }

        private string Satz(DuellErgebnis ergebnis, Guid spieler)
        {
            if (!ergebnis.Spielt(spieler))
                return "Schaut beim Duell zu";

            var anderer = spieler == ergebnis.Herausforderer ? ergebnis.Gegner : ergebnis.Herausforderer;
            var stand = IstReaktion ? string.Empty : $" {ergebnis.SiegeVon(spieler)}:{ergebnis.SiegeVon(anderer)}";
            var lage = ergebnis.Sieger == null ? "unentschieden" : ergebnis.Sieger == spieler ? "vorn" : "hinten";

            return $"Duell gegen {Name(anderer)}: {lage}{stand}";
        }

        private List<(Guid Id, string Name)> Duellanten(DuellErgebnis ergebnis)
            => Spieler().Where(s => ergebnis.Spielt(s.Id)).ToList();

        private static IReadOnlyDictionary<Guid, SchereSteinPapier> Zuege(IReadOnlyDictionary<Guid, object> abgaben)
            => abgaben.Where(a => a.Value is SchereSteinPapier).ToDictionary(a => a.Key, a => (SchereSteinPapier)a.Value);

        private static IReadOnlyDictionary<Guid, ReactionAttempt> Reaktionen(IReadOnlyDictionary<Guid, object> abgaben)
            => abgaben.Where(a => a.Value is ReactionAttempt).ToDictionary(a => a.Key, a => (ReactionAttempt)a.Value);
    }
}
