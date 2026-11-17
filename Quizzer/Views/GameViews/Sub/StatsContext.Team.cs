using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Questions.Teams;
using System.Runtime.CompilerServices;
using System.Windows;

namespace Quizzer.Views.GameViews.Sub
{
    /// <summary>
    /// Die Teams am Beamer und in der Siegerehrung (M37 T3). Im Teamspiel stehen statt der Spielerkacheln Teamkacheln mit
    /// der Summe (<see cref="Game.Teamstand"/>), darunter die Mitglieder klein. Wer keinem Team angehört, steht in einer
    /// eigenen Gruppe „Ohne Team" - seine Punkte zählen für kein Team, bis die Spielleitung zuteilt.
    /// </summary>
    public partial class StatsContext
    {
        private List<TeamKachel> teamKacheln = [];
        private string teamSchluessel = string.Empty;

        /// <summary>Ein Teamspiel mit Teams. Ohne Teams (der Spielstart verhindert das) bleibt die Einzelwertung stehen.</summary>
        public bool IstTeamspiel => Game is { IsTeamGame: true } spiel && spiel.GameTeams.Count > 0;

        /// <summary>Die Teams in der Reihenfolge des Spielaufbaus, zuletzt „Ohne Team", falls jemand ohne ist.</summary>
        public IReadOnlyList<TeamKachel> TeamKacheln => teamKacheln;

        /// <summary>Wie <see cref="TileColumns"/>: bis fünf nebeneinander, darüber zwei Reihen.</summary>
        public int TeamColumns => teamKacheln.Count <= 5 ? Math.Max(teamKacheln.Count, 1) : (int)Math.Ceiling(teamKacheln.Count / 2.0);

        public Visibility EinzelkachelnVisibility => IstTeamspiel ? Visibility.Collapsed : Visibility.Visible;

        public Visibility TeamkachelnVisibility => IstTeamspiel ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>Die Siegerteams - bei Gleichstand alle gleichauf liegenden.</summary>
        public List<TeamKachel> SiegerTeams => teamKacheln.Where(k => !k.IstOhneTeam && k.Platz == 1).ToList();

        public List<TeamKachel> UebrigeTeams => teamKacheln.Where(k => !k.IstOhneTeam && k.Platz > 1).OrderBy(k => k.Platz).ToList();

        public string SiegerTeamsTitel => SiegerTeams.Count > 1 ? "Gleichauf vorn" : "Siegerteam";

        /// <summary>
        /// Nach jedem Zug, nach dem Umsetzen und nach einem Beitritt: die Kacheln neu bauen, wenn sich Teams oder Mitglieder
        /// geändert haben, sonst nur neu lesen - eine neu gebaute Kachel verlöre ihre Plakette mitten im Einblenden.
        /// </summary>
        internal void TeamsAktualisieren(bool siegerehrung)
        {
            if (Schluessel() is var schluessel && schluessel != teamSchluessel)
            {
                teamSchluessel = schluessel;
                teamKacheln = BaueTeamKacheln();
                OnPropertyChanged(nameof(TeamKacheln));
                OnPropertyChanged(nameof(TeamColumns));
            }

            var plaetze = siegerehrung && Game != null
                ? Teamwertung.Rangfolge(Game).ToDictionary(p => p.Team.Id, p => p.Platz)
                : [];

            foreach (var kachel in teamKacheln)
                kachel.Aktualisieren(kachel.Team is { } team ? plaetze.GetValueOrDefault(team.Id) : 0, siegerehrung);

            OnPropertyChanged(nameof(IstTeamspiel));
            OnPropertyChanged(nameof(EinzelkachelnVisibility));
            OnPropertyChanged(nameof(TeamkachelnVisibility));
            OnPropertyChanged(nameof(SiegerTeams));
            OnPropertyChanged(nameof(UebrigeTeams));
            OnPropertyChanged(nameof(SiegerTeamsTitel));
        }

        private List<TeamKachel> BaueTeamKacheln()
        {
            if (!IstTeamspiel)
                return [];

            var kacheln = Game.GameTeams
                .OrderBy(t => t.SortOrder)
                .Select(t => new TeamKachel(Game, t, Mitglieder(Teamzuordnung.Teilnehmer(Game).Where(x => x.GameTeamId == t.Id))))
                .ToList();

            if (Teamzuordnung.OhneTeam(Game) is { Count: > 0 } ohne)
                kacheln.Add(new TeamKachel(Game, null, Mitglieder(ohne)));

            return kacheln;
        }

        /// <summary>Die Kacheln der Spieler, nach Namen - dieselben Objekte wie in der Einzelwertung, mit deren Zählwerk.</summary>
        private List<PlayerStatsContext> Mitglieder(IEnumerable<PlayerXGame> plaetze)
        {
            var ids = plaetze.Select(x => x.PlayerId).ToHashSet();

            return PlayerStatsContextList
                .Where(k => k.Player != null && ids.Contains(k.Player.Id))
                .OrderBy(k => k.PlayerName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        /// <summary>Was die Kacheln trägt: Teams samt Name und Farbe, die Zuordnung und die Spielerkacheln selbst.</summary>
        private string Schluessel()
        {
            if (!IstTeamspiel)
                return string.Empty;

            var teams = Game.GameTeams.Select(t => $"{RuntimeHelpers.GetHashCode(t)}:{t.Id}:{t.Name}:{t.ColorIndex}:{t.SortOrder}");
            var plaetze = Game.PlayerXGames.Select(x => $"{x.PlayerId}:{x.GameTeamId}");
            var kacheln = PlayerStatsContextList.Select(k => RuntimeHelpers.GetHashCode(k).ToString());

            return string.Join("|", teams.Concat(plaetze).Concat(kacheln));
        }
    }
}
