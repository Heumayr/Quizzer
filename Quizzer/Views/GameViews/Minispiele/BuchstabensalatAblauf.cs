using LocalBuzzer.Service.Base.States.Minispiele;
using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Questions.Minispiele;

namespace Quizzer.Views.GameViews.Minispiele
{
    /// <summary>
    /// Buchstabensalat im Fragefenster (M35 C5): am Beamer das Wirrwarr des offenen Begriffs und je Spieler die
    /// geloesten; jeder geloeste Begriff steht mit Namen in der Kopfzeile. Der Endstand bringt je Spieler den Anteil
    /// der Feldpunkte als Vorschlag mit Grund (E1).
    /// </summary>
    internal sealed class BuchstabensalatAblauf : MinispielAblauf
    {
        /// <summary>So viele Begriffe hat jeder geloest - das Rennen.</summary>
        private readonly Dictionary<Guid, int> geloest = new();

        /// <summary>„Anna hat „KÄSE" gelöst" - steht, bis der naechste geloest ist.</summary>
        private string zuletzt = string.Empty;

        /// <summary>Die Wertung der gezaehlten Runde; ein Abbruch danach behaelt sie.</summary>
        private BuchstabensalatErgebnis? ergebnis;

        public BuchstabensalatAblauf(CurrentQuestionViewModel fenster)
            : base(fenster, MinigameKind.WordScramble)
        {
        }

        /// <summary>Der Blick von aussen - Wirrwarr und Zahl der gespielten Begriffe, wie der Server sie fuehrt.</summary>
        private WordScrambleClientInfo? Stand => (Zustand?.InfoFor(null) as MinigameClientInfo)?.KindInfo as WordScrambleClientInfo;

        public override void ZeigeLaeuft()
        {
            var stand = Stand;
            var kopf = stand is { TermNumber: > 0 }
                ? $"Buchstabensalat – Begriff {stand.TermNumber} von {stand.TotalTerms}: {stand.Scramble}"
                : "Buchstabensalat läuft";

            Anzeige.Zeige(zuletzt.Length > 0 ? $"{kopf} · {zuletzt}" : kopf,
                BuchstabensalatTafel.Rennen(geloest, stand?.TotalTerms ?? 0, Spieler()));
        }

        /// <summary>Ein Begriff ist geloest: Rennen und Kopfzeile ziehen mit, ein Ton fuer den Saal; daneben bewegt nichts.</summary>
        public override void Abgabe(MinigameSubmission abgabe)
        {
            if (abgabe.Value is not BuchstabensalatVersuch { Richtig: true } versuch || Zustand?.Phase != MinigamePhase.Running)
                return;

            geloest[abgabe.Player.Id] = versuch.Stand.Geloest;
            zuletzt = $"{Name(abgabe.Player.Id)} hat „{versuch.Loesung}“ gelöst";
            Klang.Tick();
            ZeigeLaeuft();
        }

        public override void RundeZu(MinigameRoundClosed runde)
        {
            if (!runde.Aborted)
            {
                var endstaende = runde.Submissions
                    .Where(a => a.Value is BuchstabensalatStand)
                    .ToDictionary(a => a.Key, a => (BuchstabensalatStand)a.Value);

                ergebnis = BuchstabensalatEvaluator.Werte(endstaende, Stand?.TotalTerms ?? 0, Feldpunkte, Konten());
                Karten?.WerteVorschlaege(ergebnis.Vorschlaege, id => BuchstabensalatEvaluator.Describe(ergebnis, id));
            }

            if (ergebnis == null)
            {
                Anzeige.Zeige("Minispiel abgebrochen – diese Runde zählt nicht");
                return;
            }

            var geloesteGesamt = ergebnis.Staende.Values.Sum(s => s.Geloest);
            Anzeige.Zeige(runde.Aborted
                    ? "Minispiel abgebrochen – der Stand"
                    : $"Ergebnis – {geloesteGesamt} von {ergebnis.Begriffe} Begriffen gelöst",
                BuchstabensalatTafel.Ende(ergebnis, Spieler()));
        }

        /// <summary>Eine Runde gibt es nur einmal - Wiederholen vergisst sie wie das Zuruecksetzen.</summary>
        public override void Wiederhole(int runde) => SetzeZurueck();

        /// <summary>Vergisst Rennen und Wertung; war schon gewertet, auch die Vorschlaege.</summary>
        public override void SetzeZurueck()
        {
            geloest.Clear();
            zuletzt = string.Empty;

            if (ergebnis == null)
                return;

            ergebnis = null;
            Karten?.WerteVorschlaege(new Dictionary<Guid, Punktevorschlag>(), _ => string.Empty);
        }
    }
}
