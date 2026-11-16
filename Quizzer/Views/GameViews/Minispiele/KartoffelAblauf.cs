using LocalBuzzer.Service.Base.States.Minispiele;
using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Questions.Minispiele;

namespace Quizzer.Views.GameViews.Minispiele
{
    /// <summary>
    /// Heisse Kartoffel im Fragefenster (M35 C6): am Beamer nur, wer sie haelt und wie oft jeder weitergab - nie eine Aussage,
    /// nie eine Zeit; den Zuender kennt auch der Spielleiter nicht. Kein Ton, der zum Ende hin schneller wird - er verriete den
    /// Zuender; nur ein Tick je Weitergabe und der Knall. Danach die Vorschlaege mit Grund (E1) und der Verlauf samt Wahrheit.
    /// </summary>
    internal sealed class KartoffelAblauf : MinispielAblauf
    {
        private readonly Dictionary<Guid, int> weitergaben = new();
        private KartoffelErgebnis? ergebnis;
        private KartoffelRunde? gespielt;

        public KartoffelAblauf(CurrentQuestionViewModel fenster)
            : base(fenster, MinigameKind.HotPotato)
        {
        }

        private Guid? Halter => Zustand?.Lenke(r => (r as HotPotatoRules)?.ChooserId);

        public override void ZeigeLaeuft()
        {
            var halter = Halter;
            Anzeige.Zeige(KartoffelTafel.Kopfzeile(halter is { } h ? Name(h) : null), KartoffelTafel.Laufend(halter, weitergaben, Spieler()));
        }

        /// <summary>Sie ist gewandert: ein Tick und der neue Halter am Beamer. Ein Urteil bleibt beim Halter.</summary>
        public override void Abgabe(MinigameSubmission abgabe)
        {
            if (abgabe.Value is not KartoffelWeitergabe weiter || Zustand?.Phase != MinigamePhase.Running)
                return;

            weitergaben[weiter.Von] = weitergaben.GetValueOrDefault(weiter.Von) + 1;
            Klang.Tick();
            ZeigeLaeuft();
        }

        public override void RundeZu(MinigameRoundClosed runde)
        {
            if (!runde.Aborted && Zustand?.Lenke(r => (r as HotPotatoRules)?.Auswertung) is { } auswertung)
            {
                gespielt = auswertung;
                var wertung = KartoffelEvaluator.Werte(auswertung.Knall, Punkteanteil.Von(Feldpunkte, Frage?.Share ?? 100), Konten(), Name);
                ergebnis = wertung;
                Karten?.WerteVorschlaege(wertung.Vorschlaege, id => KartoffelEvaluator.Karte(wertung, id, Name));
            }

            if (ergebnis == null || gespielt == null)
            {
                Anzeige.Zeige("Minispiel abgebrochen – es zählt nichts");
                return;
            }

            Anzeige.Zeige(runde.Aborted ? "Minispiel abgebrochen – der Stand" : KartoffelEvaluator.Ausgang(gespielt, Name),
                KartoffelTafel.Ende(ergebnis, Spieler()), KartoffelTafel.Aufloesung(gespielt.Verlauf, Name));
        }

        /// <summary>Der Knall klingt wie der Absturz des Zockers (V5); ohne Knall der gewoehnliche Ton.</summary>
        public override void TonZu(MinigameRoundClosed runde)
        {
            if (!runde.Aborted && gespielt?.Knall != null)
                Klang.Absturz();
            else
                base.TonZu(runde);
        }

        /// <summary>Eine Runde gibt es nur einmal - Wiederholen vergisst sie wie das Zuruecksetzen.</summary>
        public override void Wiederhole(int runde) => SetzeZurueck();

        public override void SetzeZurueck()
        {
            weitergaben.Clear();
            gespielt = null;

            if (ergebnis == null)
                return;

            ergebnis = null;
            Karten?.WerteVorschlaege(new Dictionary<Guid, Punktevorschlag>(), _ => string.Empty);
        }
    }
}
