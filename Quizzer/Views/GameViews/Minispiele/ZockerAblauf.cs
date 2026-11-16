using LocalBuzzer.Service.Base.States.Minispiele;
using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Questions.Minispiele;
using System.Windows.Threading;

namespace Quizzer.Views.GameViews.Minispiele
{
    /// <summary>
    /// Zocker im Fragefenster (M35 C4): ein Takt bringt den Multiplikator an den Beamer, gelesen an der
    /// Serveruhr (<see cref="CrashRules.Stand"/>) - der Absturzpunkt steht dort erst, wenn es abgestuerzt
    /// ist. Die Runde schliesst der Server; danach Wertung und Vorschlaege mit Grund auf den Karten (E1).
    /// </summary>
    internal sealed class ZockerAblauf : MinispielAblauf
    {
        /// <summary>So oft steigt die Zahl am Beamer sichtbar.</summary>
        private static readonly TimeSpan TaktDauer = TimeSpan.FromMilliseconds(100);

        /// <summary>Die Ausstiege der laufenden Runde, wie sie angenommen wurden.</summary>
        private readonly Dictionary<Guid, int> ausstiege = new();

        /// <summary>Die Wertung der gezaehlten Runde; ein Abbruch danach behaelt sie.</summary>
        private ZockerErgebnis? ergebnis;

        private DispatcherTimer? takt;

        public ZockerAblauf(CurrentQuestionViewModel fenster)
            : base(fenster, MinigameKind.Crash)
        {
        }

        private CrashRules? Regeln => Zustand?.Rules as CrashRules;

        public override void ZeigeLaeuft()
        {
            Aktualisiere();

            if (takt == null)
            {
                takt = new DispatcherTimer(DispatcherPriority.Render) { Interval = TaktDauer };
                takt.Tick += (_, _) => Takt();
            }

            takt.Start();
        }

        /// <summary>Ein Ausstieg ist angenommen: Ton und die Liste am Beamer.</summary>
        public override void Abgabe(MinigameSubmission abgabe)
        {
            if (abgabe.Value is not ZockerAusstieg ausstieg || Zustand?.Phase != MinigamePhase.Running)
                return;

            ausstiege[abgabe.Player.Id] = ausstieg.Multiplikator;
            Klang.Tick();
            Aktualisiere();
        }

        public override void RundeZu(MinigameRoundClosed runde)
        {
            takt?.Stop();

            if (!runde.Aborted)
            {
                var abgaben = runde.Submissions
                    .Where(a => a.Value is ZockerAusstieg)
                    .ToDictionary(a => a.Key, a => ((ZockerAusstieg)a.Value).Multiplikator);

                ergebnis = Werte(abgaben);
            }

            if (ergebnis == null)
            {
                Anzeige.Zeige("Minispiel abgebrochen – diese Runde zählt nicht");
                return;
            }

            Anzeige.Zeige(runde.Aborted ? "Minispiel abgebrochen – der Stand" : Ausgang(ergebnis), ZockerTafel.Ende(ergebnis, Spieler()));
        }

        /// <summary>Der Absturz klingt anders als ein gewoehnliches Ende (V5).</summary>
        public override void TonZu(MinigameRoundClosed runde)
        {
            if (!runde.Aborted && Regeln?.Abgestuerzt == true)
                Klang.Absturz();
            else
                base.TonZu(runde);
        }

        /// <summary>Eine Runde gibt es nur einmal - Wiederholen vergisst sie wie das Zuruecksetzen.</summary>
        public override void Wiederhole(int runde) => SetzeZurueck();

        /// <summary>Vergisst Ausstiege und Wertung; war schon gewertet, auch die Vorschlaege.</summary>
        public override void SetzeZurueck()
        {
            takt?.Stop();
            ausstiege.Clear();

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

            Aktualisiere();
        }

        /// <summary>Der Stand am Beamer - waehrend der Runde nie mehr als die Serveruhr bis jetzt verraet.</summary>
        private void Aktualisiere()
        {
            if (Regeln is not { } regeln || Zustand?.Phase != MinigamePhase.Running)
                return;

            var stand = regeln.Stand;
            Anzeige.Zeige(ZockerTafel.Kopfzeile(stand.Multiplikator, stand.Gestartet, stand.Abgestuerzt),
                ZockerTafel.Laufend(ausstiege, regeln.Teilnehmer, Spieler()));
        }

        private ZockerErgebnis Werte(IReadOnlyDictionary<Guid, int> abgaben)
        {
            var einsatz = Frage?.Variant == ZockerEvaluator.MitEinsatzVariant
                ? Punkteanteil.Von(Feldpunkte, Frage.Share)
                : (int?)null;

            var wertung = ZockerEvaluator.Werte(Regeln?.Teilnehmer ?? [], abgaben, Regeln?.Aufgedeckt,
                new ZockerWertung(Feldpunkte, einsatz, Konten()));

            Karten?.WerteVorschlaege(wertung.Vorschlaege, id => ZockerEvaluator.Describe(wertung, id));
            return wertung;
        }

        private string Ausgang(ZockerErgebnis wertung)
            => wertung.Absturz is not { } absturz ? "Zocker – vorbei"
                : Regeln?.Abgestuerzt == true ? $"Zocker – abgestürzt bei {ZockerEvaluator.Text(absturz)}!"
                : $"Zocker – alle sind ausgestiegen, abgestürzt wäre es bei {ZockerEvaluator.Text(absturz)}";
    }
}
