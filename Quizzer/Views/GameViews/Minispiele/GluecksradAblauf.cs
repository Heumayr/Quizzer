using LocalBuzzer.Service.Base.States.Minispiele;
using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Questions.Minispiele;
using System.Windows.Threading;

namespace Quizzer.Views.GameViews.Minispiele
{
    /// <summary>
    /// Gluecksrad im Fragefenster (M35 C4): „Runde starten" dreht. Waehrend des Countdowns der Telefone
    /// laeuft ein Zeiger ueber die Felder, dann steht das Rad auf dem Feld, das der Server gewuerfelt hat.
    /// Die Runde schliesst der Server (Landung oder Zielwahl); danach Vorschlaege mit Grund (E1).
    /// </summary>
    internal sealed class GluecksradAblauf : MinispielAblauf
    {
        /// <summary>So schnell wandert der Zeiger von Feld zu Feld.</summary>
        private static readonly TimeSpan TaktDauer = TimeSpan.FromMilliseconds(150);

        /// <summary>Die Wertung der gezaehlten Runde; ein Abbruch danach behaelt sie.</summary>
        private GluecksradErgebnis? ergebnis;

        private DispatcherTimer? takt;
        private int schritt;
        private bool gelandet;

        public GluecksradAblauf(CurrentQuestionViewModel fenster)
            : base(fenster, MinigameKind.WheelOfFortune)
        {
        }

        private WheelOfFortuneRules? Regeln => Zustand?.Rules as WheelOfFortuneRules;

        public override void ZeigeLaeuft()
        {
            // Nach dem Landen (der Spielleiter legt das Ziel fest) dreht es nicht noch einmal.
            if (Regeln?.Gelandet == true && Zustand?.Phase == MinigamePhase.Running)
            {
                Lande();
                return;
            }

            gelandet = false;
            schritt = 0;
            Drehe();

            if (takt == null)
            {
                takt = new DispatcherTimer(DispatcherPriority.Render) { Interval = TaktDauer };
                takt.Tick += (_, _) => Takt();
            }

            takt.Start();
        }

        /// <summary>Das Ziel ist gewaehlt - die Runde schliesst gleich, der Ton sagt es schon.</summary>
        public override void Abgabe(MinigameSubmission abgabe)
        {
            if (abgabe.Value is WheelTargetChosen)
                Klang.Tick();
        }

        public override void RundeZu(MinigameRoundClosed runde)
        {
            takt?.Stop();

            if (!runde.Aborted)
                ergebnis = Werte();

            if (ergebnis == null)
            {
                Anzeige.Zeige("Minispiel abgebrochen – das Rad zählt nicht");
                return;
            }

            Anzeige.Zeige((runde.Aborted ? "Minispiel abgebrochen – der Stand: " : "Glücksrad: ") + Ausgang(ergebnis),
                GluecksradTafel.Ende(ergebnis, Spieler()));
        }

        /// <summary>Neu drehen - die alte Wertung zaehlt nicht mehr.</summary>
        public override void Wiederhole(int runde) => SetzeZurueck();

        public override void SetzeZurueck()
        {
            takt?.Stop();
            gelandet = false;

            if (ergebnis == null)
                return;

            ergebnis = null;
            Karten?.WerteVorschlaege(new Dictionary<Guid, Punktevorschlag>(), _ => string.Empty);
        }

        public override void Beende() => takt?.Stop();

        private void Takt()
        {
            if (Zustand?.Phase != MinigamePhase.Running)
            {
                takt?.Stop();
                return;
            }

            if (Regeln?.Gelandet == true)
            {
                takt?.Stop();
                Lande();
                return;
            }

            schritt++;
            Klang.Tick();
            Drehe();
        }

        private void Drehe()
        {
            var felder = GluecksradEvaluator.FelderFuer(Teamspiel);
            Anzeige.Zeige("Das Glücksrad dreht sich …", GluecksradTafel.Rad(felder[schritt % felder.Count], false, Teamspiel));
        }

        /// <summary>Das Rad steht: Feld an den Beamer; braucht es ein Ziel, wer es waehlt.</summary>
        private void Lande()
        {
            if (Regeln?.Segment is not { } feld)
                return;

            var titel = $"Glücksrad: {GluecksradEvaluator.Name(feld)}!";
            var kopf = !GluecksradEvaluator.BrauchtZiel(feld) ? titel
                : Paar is { } paar ? $"{titel} {Name(paar.A)} trifft {Name(paar.B)}."
                : $"{titel} {(Regeln.ChooserId is { } d ? Name(d) : "Der Dreher")} wählt am Telefon, wen es trifft …";

            Anzeige.Zeige(kopf, GluecksradTafel.Rad(feld, true, Teamspiel));

            // V5: das Ergebnis beim Rad - ohne Ziel folgt gleich der Gong des Rundenendes.
            if (!gelandet && GluecksradEvaluator.BrauchtZiel(feld))
                Klang.Buzzer();

            gelandet = true;
        }

        private GluecksradErgebnis? Werte()
        {
            if (Regeln is not { Segment: { } feld } regeln)
                return null;

            var wurf = new GluecksradWurf(feld, regeln.ChooserId, regeln.PartnerId);
            var wertung = GluecksradEvaluator.Werte(wurf, Punkteanteil.Von(Feldpunkte, Frage?.Share ?? 0), Konten(), Spieler());

            Karten?.WerteVorschlaege(wertung.Vorschlaege, wertung.Describe);
            return wertung;
        }

        private string Ausgang(GluecksradErgebnis wertung)
            => wertung.Wurf.Segment == WheelSegment.RobinHood && wertung.Vorschlaege.Count == 0
                ? "Robin Hood – alle stehen gleich, es passiert nichts."
                : GluecksradEvaluator.Ausgang(wertung.Wurf, Name);
    }
}
