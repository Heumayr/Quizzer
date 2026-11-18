using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Questions.Teams;
using Quizzer.Logic.Controller.TypedControllers;

namespace Quizzer.Views.GameViews
{
    /// <summary>
    /// Der Stempel beim Öffnen der Zelle (M37 T2, R4-3): eine neue Zeile bekommt das heutige Team des Spielers, eine bestehende
    /// ohne jede Buchung ebenso - sonst folgte die Zelle nach „Ansehen und schließen" und „Teams umsetzen" zwei Teams: Hub und
    /// Telefone der Zuordnung, Sprecher, Siegerteam und Teamstand dem alten Stempel. Eine gebuchte Zeile behält ihren (F-M37-07).
    /// </summary>
    public partial class PlayersResultViewModel
    {
        /// <summary>
        /// R4-4: gebuchte Zeilen abgewählter Spieler im Teamspiel. Sie bleiben an der Zelle und zählen beim Team ihres Stempels
        /// (F-M37-07) - vorher nahm das Aufräumen beim Wiederöffnen dem Team diese Punkte. Ohne Teamspiel wird weiter geräumt.
        /// </summary>
        private List<QuestionResult> ehemalige = [];

        /// <summary>Was an der Zelle liegt: die Zeilen der Karten und die behaltenen Zeilen Abgewählter.</summary>
        internal List<QuestionResult> ZeilenDerZelle => [.. Results, .. ehemalige];

        private List<QuestionResult> GebuchteImTeamspiel(IEnumerable<QuestionResult> verwaist) =>
            Coordinate?.Game is { IsTeamGame: true } ? verwaist.Where(z => !Teamwertung.OhneBuchung(z)).ToList() : [];

        private static async Task StempleUngebuchteNeuAsync(QuestionResultsController ctrl, Game spiel, IEnumerable<QuestionResult> zeilen)
        {
            var neu = zeilen
                .Where(z => Teamwertung.OhneBuchung(z) && z.GameTeamId != Teamzuordnung.TeamVon(spiel, z.PlayerId))
                .ToList();

            if (neu.Count == 0)
                return;

            foreach (var zeile in neu)
            {
                zeile.GameTeamId = Teamzuordnung.TeamVon(spiel, zeile.PlayerId);
                await ctrl.UpdateAsync(zeile);
            }

            await ctrl.SaveChangesAsync();
        }
    }
}
