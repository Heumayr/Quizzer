using Quizzer.DataModels.Anzeige;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Questions.Sonderregeln;
using Quizzer.DataModels.Questions.Teams;

namespace Quizzer.Views.GameViews.Teamregeln
{
    /// <summary>
    /// Teamberatung (M37 T5, F-M37-02): jedes Team berät, sein Sprecher antwortet für alle. Die Wertung des Teams ist die der
    /// Karte des Sprechers - bewertet der Spielleiter stattdessen eine andere Karte des Teams, gilt die erste bewertete in
    /// Sprecherfolge (E1). Richtig verteilt die Feldpunkte, falsch die Minuspunkte, einmal je Team (F-M37-06).
    /// </summary>
    internal sealed class TeamberatungAblauf : TeamregelAblauf
    {
        public TeamberatungAblauf()
            : base(CellSpecialRule.TeamCouncil)
        {
        }

        protected override IReadOnlyDictionary<Guid, Teamanteil> Anteile(IReadOnlyDictionary<Guid, SonderregelEingang> eingaenge)
        {
            var anteile = new Dictionary<Guid, Teamanteil>();
            var sprecher = Sprecher;

            foreach (var team in Teams)
            {
                if (WertungDes(team, sprecher, eingaenge) is not { } wertung)
                    continue;

                var grund = $"Teamberatung {team.Name}: Anteil an {Punktewechsel.Text(wertung.Grundwert)}";

                foreach (var (mitglied, punkte) in Aufteilung.Verteile(wertung.Grundwert, team.Folge.Where(eingaenge.ContainsKey)))
                    anteile[mitglied] = new Teamanteil(wertung.Bewertung, punkte, grund);
            }

            return anteile;
        }
    }
}
