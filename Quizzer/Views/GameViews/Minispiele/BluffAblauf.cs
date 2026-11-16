using LocalBuzzer.Service.Base.States.Minispiele;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Questions.Minispiele;

namespace Quizzer.Views.GameViews.Minispiele
{
    /// <summary>
    /// Bluff im Fragefenster (M35 C6): die zwei Phasen sind die zwei Runden des Rahmens. Am Beamer waehrend einer Phase
    /// nur, wer abgegeben hat. Zwischen den Phasen streicht der Spielleiter (<c>CurrentQuestionViewModel.Bluff</c>). Nach
    /// der Wahl die Aufloesung - erst jetzt mit Urhebern - und die Vorschlaege mit Grund (E1).
    /// </summary>
    internal sealed class BluffAblauf : MinispielAblauf
    {
        private readonly HashSet<Guid> abgegeben = new();
        private BluffErgebnis? ergebnis;

        public BluffAblauf(CurrentQuestionViewModel fenster)
            : base(fenster, MinigameKind.Bluff)
        {
        }

        private int Phase => Zustand?.Round ?? 0;

        public override void ZeigeLaeuft()
        {
            var kopf = Phase >= 2
                ? "Bluff – Phase 2: Welche Antwort ist die echte? Wählt am Telefon!"
                : "Bluff – Phase 1: Denkt euch eine falsche Antwort aus, die echt klingt!";

            Anzeige.Zeige(kopf, BluffTafel.Eingang(abgegeben, Phase, Spieler()));
            Fenster.BluffGeaendert();
        }

        public override void Abgabe(MinigameSubmission abgabe)
        {
            if (abgabe.Value is not BluffAbgabe eingang || eingang.Phase != Phase || Zustand?.Phase != MinigamePhase.Running)
                return;

            abgegeben.Add(abgabe.Player.Id);
            ZeigeLaeuft();
        }

        public override void RundeZu(MinigameRoundClosed runde)
        {
            abgegeben.Clear();

            if (!runde.Aborted && runde.Round == 1)
            {
                Anzeige.Zeige("Bluff – Phase 1 vorbei: gleich wird gewählt");
                Fenster.BluffGeaendert();
                return;
            }

            if (!runde.Aborted && Zustand?.Lenke(r => (r as BluffRules)?.Auswertung) is { } auswertung)
            {
                var wertung = BluffEvaluator.Werte(auswertung, Feldpunkte, Konten());
                ergebnis = wertung;
                Karten?.WerteVorschlaege(wertung.Vorschlaege, id => BluffEvaluator.Karte(wertung, id));
            }

            Fenster.BluffGeaendert();

            if (ergebnis == null)
            {
                Anzeige.Zeige("Minispiel abgebrochen – es zählt nichts");
                return;
            }

            var echt = ergebnis.Runde.Optionen.FirstOrDefault(o => o.Echt)?.Text ?? "?";
            Anzeige.Zeige(runde.Aborted ? "Minispiel abgebrochen – der Stand" : $"Ergebnis – echt war „{echt}“",
                BluffTafel.Ende(ergebnis, Spieler()), BluffEvaluator.Aufloesung(ergebnis, Name));
        }

        /// <summary>Eine Phase neu - die Abgaben und eine schon gezaehlte Wahl gelten nicht mehr.</summary>
        public override void Wiederhole(int runde) => SetzeZurueck();

        public override void SetzeZurueck()
        {
            abgegeben.Clear();
            Fenster.BluffGeaendert();

            if (ergebnis == null)
                return;

            ergebnis = null;
            Karten?.WerteVorschlaege(new Dictionary<Guid, Punktevorschlag>(), _ => string.Empty);
        }
    }
}
