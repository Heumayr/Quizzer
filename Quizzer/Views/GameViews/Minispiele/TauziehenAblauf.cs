using LocalBuzzer.Service.Base.States.Minispiele;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Questions.Minispiele;
using Quizzer.DataModels.Questions.Teams;

namespace Quizzer.Views.GameViews.Minispiele
{
    /// <summary>
    /// Tauziehen im Fragefenster (M37 T9): jeder angenommene Stand bewegt das Seil am Beamer - gelesen am Server unter der
    /// Sperre des Rahmens, damit Beamer und Telefone dieselben Teilnehmer und denselben Zug sehen. Die Runde schließt die
    /// Serveruhr; danach bekommt das stärkere Team die Feldpunkte aufgeteilt als Vorschlag (E1), die anderen nichts.
    /// </summary>
    internal sealed class TauziehenAblauf : MinispielAblauf
    {
        /// <summary>Die Wertung der gezählten Runde; ein Abbruch danach behält sie.</summary>
        private TauziehenErgebnis? ergebnis;

        /// <summary>Was zuletzt am Beamer stand - ein Stand ohne neuen Tipp zeichnet nichts neu.</summary>
        private TauziehenErgebnis? gezeigt;

        public TauziehenAblauf(CurrentQuestionViewModel fenster)
            : base(fenster, MinigameKind.TugOfWar)
        {
        }

        private TeamMinigameScoring Wertung => Fenster.Coordinate?.Game?.TeamMinigameScoring ?? TeamMinigameScoring.Average;

        /// <summary>Der laufende Stand am Server; vor dem Start leer.</summary>
        private TauziehenErgebnis Laufend => Zustand?.Lenke(r => (r as TugOfWarRules)?.Stand) ?? TauziehenErgebnis.Leer;

        public override void ZeigeLaeuft()
        {
            gezeigt = null;
            ZeigeRennen();
        }

        /// <summary>Ein Stand ist angenommen: das Seil wandert mit, solange die Runde läuft.</summary>
        public override void Abgabe(MinigameSubmission abgabe)
        {
            // Nach dem Schließen steht das Ergebnis am Beamer - ein spät verteilter Stand darf es nicht überschreiben.
            if (abgabe.Value is not TauziehenZug || Zustand?.Phase != MinigamePhase.Running)
                return;

            ZeigeRennen();
        }

        public override void RundeZu(MinigameRoundClosed runde)
        {
            if (!runde.Aborted)
            {
                ergebnis = Werte(runde.Submissions
                    .Where(a => a.Value is TauziehenZug)
                    .ToDictionary(a => a.Key, a => (TauziehenZug)a.Value));
            }

            if (ergebnis == null)
            {
                Anzeige.Zeige("Minispiel abgebrochen – diese Runde zählt nicht");
                return;
            }

            Zeige(runde.Aborted ? "Minispiel abgebrochen – der Stand" : Ausgang(ergebnis), ergebnis, ende: true);
        }

        /// <summary>Eine Runde gibt es nur einmal - Wiederholen vergisst sie wie das Zurücksetzen.</summary>
        public override void Wiederhole(int runde) => SetzeZurueck();

        /// <summary>Vergisst die Wertung; war schon gewertet, auch die Vorschläge.</summary>
        public override void SetzeZurueck()
        {
            var gewertet = ergebnis != null;

            ergebnis = null;
            gezeigt = null;

            if (gewertet)
                Karten?.WerteTauziehen(TauziehenErgebnis.Leer, new Dictionary<Guid, TauziehenZug>());
        }

        private void ZeigeRennen()
        {
            var stand = Laufend;

            if (gezeigt != null && gezeigt.Teams.SequenceEqual(stand.Teams))
                return;

            gezeigt = stand;
            Zeige($"Tauziehen läuft – {Frage?.DurationSeconds ?? 0} Sekunden, zieht!", stand, ende: false);
        }

        private TauziehenErgebnis Werte(IReadOnlyDictionary<Guid, TauziehenZug> zuege)
        {
            var werte = TauziehenEvaluator.Werte(zuege, Wertung);
            Karten?.WerteTauziehen(werte, zuege);
            return werte;
        }

        private void Zeige(string kopfzeile, TauziehenErgebnis stand, bool ende)
        {
            Anzeige.Zeige(kopfzeile, TauziehenTafel.Zeilen(stand, Teamname, ende));
            Anzeige.ZeigeSeil(TauziehenTafel.Seil(stand, Teamname));
        }

        private string Ausgang(TauziehenErgebnis werte) => werte.Gewinner.Count switch
        {
            0 => "Ergebnis – kein Team hat gezogen",
            1 => $"Ergebnis – {Teamname(werte.Gewinner.First())} gewinnt das Tauziehen",
            _ => "Ergebnis – Gleichstand, die Teams vorn gewinnen",
        };

        /// <summary>Symbol und Name aus dem Spielaufbau, wie Beamer und Karten das Team kennen.</summary>
        private string Teamname(Guid teamId) => TeamTitel(Fenster.Coordinate?.Game?.GameTeams.FirstOrDefault(t => t.Id == teamId));

        internal static string TeamTitel(DataModels.Models.Base.GameTeam? team)
        {
            if (team == null)
                return "?";

            return TeamFarben.IsValid(team.ColorIndex) ? $"{TeamFarben.Alle[team.ColorIndex].Symbol} {team.Name}" : team.Name;
        }
    }
}
