using Quizzer.Views.GameViews.Sub;
using System.Windows;

namespace Quizzer.Views.GameViews
{
    /// <summary>
    /// Die Spielerkarten im Teamspiel, nach Team gruppiert (M37 T3) - in der Reihenfolge des Spielaufbaus, „Ohne Team"
    /// zuletzt. Eine Karte steht beim Team, bei dem ihr Ergebnis zählt (Stempel, sonst das heutige Team), nicht zwingend
    /// beim heutigen: wer nach dem Öffnen der Zelle umgesetzt wurde, wird für das alte Team bewertet (F-M37-07).
    /// </summary>
    public partial class PlayersResultViewModel
    {
        public IReadOnlyList<TeamKartengruppe> TeamGruppen { get; private set; } = [];

        public Visibility EinzelkartenVisibility => TeamGruppen.Count > 0 ? Visibility.Collapsed : Visibility.Visible;

        public Visibility TeamGruppenVisibility => TeamGruppen.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>
        /// Gruppiert die Karten und gibt sie in der Reihenfolge der Gruppen zurück - so zählen Strg+1 … 9 so, wie die Karten
        /// dastehen. Ohne Teamspiel bleibt alles beim Alten.
        /// </summary>
        private List<PlayerResultContext> NachTeams(List<PlayerResultContext> karten)
        {
            var spiel = Coordinate?.Game;
            TeamGruppen = [];

            if (spiel is { IsTeamGame: true } && spiel.GameTeams.Count > 0)
            {
                var spalten = Math.Clamp(karten.Count, 1, 4);
                var gruppen = spiel.GameTeams
                    .OrderBy(t => t.SortOrder)
                    .Select(t => new TeamKartengruppe(t, karten.Where(k => k.ZaehltFuerTeam(spiel) == t.Id).ToList(), spalten))
                    .Where(g => g.Karten.Count > 0)
                    .ToList();

                if (karten.Where(k => k.ZaehltFuerTeam(spiel) == null).ToList() is { Count: > 0 } ohne)
                    gruppen.Add(new TeamKartengruppe(null, ohne, spalten));

                TeamGruppen = gruppen;
            }

            OnPropertyChanged(nameof(TeamGruppen));
            OnPropertyChanged(nameof(EinzelkartenVisibility));
            OnPropertyChanged(nameof(TeamGruppenVisibility));

            return TeamGruppen.Count > 0 ? TeamGruppen.SelectMany(g => g.Karten).ToList() : karten;
        }
    }
}
