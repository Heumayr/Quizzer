using Quizzer.Base;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Questions.Teams;
using Quizzer.Logic.Controller.TypedControllers;
using Quizzer.Logic.Spielablauf;
using Quizzer.Views.StaticRessources;
using Quizzer.Views.Teams;
using System.Windows;
using System.Windows.Input;

namespace Quizzer.Views.GameViews
{
    /// <summary>
    /// „Teams umsetzen“ zwischen zwei Zellen (M37 T2): derselbe Dialog wie im Spielaufbau, auf dem geladenen Spiel - das
    /// Fragefenster ist modal, während einer Zelle ist der Knopf nicht erreichbar. Alte Stempel bleiben (F-M37-07), die
    /// nächste Zelle stempelt das neue Team; die Telefone bekommen ihr neues Team sofort (T4).
    /// </summary>
    public partial class GameMasterViewModel
    {
        public Visibility TeamsVisibility => Game?.IsTeamGame == true ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>Der Knopf sagt, wenn jemand ohne Team ist - etwa wer sich am Telefon dazugemeldet hat (M10).</summary>
        public string TeamsUmsetzenText => Game is { IsTeamGame: true } spiel && Teamzuordnung.OhneTeam(spiel).Count is > 0 and var ohne
            ? $"Teams umsetzen … ({ohne} ohne Team)"
            : "Teams umsetzen …";

        private AsyncRelayCommand? teamsUmsetzenCommand;

        public ICommand TeamsUmsetzenCommand => teamsUmsetzenCommand ??= new AsyncRelayCommand(async _ =>
        {
            if (Game != null && TeamsView.ShowFor(Game, Window, "Teams umsetzen"))
                await TeamsUmgesetztAsync();
        });

        /// <summary>Nach dem Umsetzen: Knopf und Beamer neu lesen, und die Aufstellung an den Hub (T4).</summary>
        internal async Task TeamsUmgesetztAsync()
        {
            TeamsAnzeigeAktualisieren();
            await StaticManager.BuzzerServerViewModel.SendTeamsAsync();
        }

        /// <summary>Nach dem Umsetzen und nach einem Beitritt am Telefon: den Knopf und die Teamkacheln am Beamer (T3) neu lesen.</summary>
        internal void TeamsAnzeigeAktualisieren()
        {
            OnPropertyChanged(nameof(TeamsUmsetzenText));
            OnPropertyChanged(nameof(SprecherText));
            StatsContext.TeamsAktualisieren(IsGameFinished);
        }

        // --- Sprecher (M37 T5) ---

        /// <summary>„Sprecher: ✚ Rot – Anna · ★ Blau – Carl" - wer bei der nächsten Teamregel antwortet.</summary>
        public string SprecherText => Game is { IsTeamGame: true } spiel && spiel.GameTeams.Count > 0
            ? "Sprecher: " + string.Join(" · ", spiel.GameTeams.OrderBy(t => t.SortOrder).Select(t => $"{(TeamFarben.IsValid(t.ColorIndex) ? TeamFarben.Alle[t.ColorIndex].Symbol : "–")} {t.Name} – {SprecherName(spiel, t)}"))
            : string.Empty;

        private static string SprecherName(Game spiel, GameTeam team)
            => Sprecherfolge.Sprecher(Sprecherfolge.Folge(spiel, team)) is { } id
                ? spiel.Players.FirstOrDefault(p => p.Id == id)?.CalculatedDisplayName ?? "?"
                : "niemand";

        private AsyncRelayCommand? sprecherWeiterCommand;

        public ICommand SprecherWeiterCommand => sprecherWeiterCommand ??= new AsyncRelayCommand(_ => SprecherBewegenAsync(vor: true));

        private AsyncRelayCommand? sprecherZurueckCommand;

        public ICommand SprecherZurueckCommand => sprecherZurueckCommand ??= new AsyncRelayCommand(_ => SprecherBewegenAsync(vor: false));

        /// <summary>Nach einer bewerteten Teamregel-Zelle spricht in jedem Team der Nächste; ansehen und schließen bewegt nichts.</summary>
        internal async Task NachTeamregelAsync(CurrentQuestionViewModel frage, LetzteWertung merk)
        {
            if (Game != null && frage.TeamregelGespielt && merk.HatSichGeaendert(Game))
                await SprecherBewegenAsync(vor: true);
        }

        /// <summary>
        /// Weiter oder zurück, in jedem Team - geschrieben und an die Telefone. Die Rücknahme einer Wertung bewegt den Sprecher
        /// nicht (Entwurf Abschnitt 7); dafür gibt es „Sprecher zurück“. Scheitert das Schreiben, bleibt der alte Stand.
        /// </summary>
        internal async Task SprecherBewegenAsync(bool vor)
        {
            if (Game is not { IsTeamGame: true } spiel || spiel.GameTeams.Count == 0)
                return;

            var vorher = spiel.GameTeams.ToDictionary(t => t, t => t.SpeakerTurn);

            foreach (var team in spiel.GameTeams)
                team.SpeakerTurn = vor ? Sprecherfolge.Weiter(team.SpeakerTurn) : Sprecherfolge.Zurueck(team.SpeakerTurn, Sprecherfolge.Folge(spiel, team).Count);

            try
            {
                using var ctrl = new GameTeamsController();

                foreach (var team in spiel.GameTeams)
                    await ctrl.UpdateAsync(team);

                await ctrl.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                foreach (var (team, stand) in vorher)
                    team.SpeakerTurn = stand;

                ExceptionManager.HandleException(ex);
            }

            OnPropertyChanged(nameof(SprecherText));
            await StaticManager.BuzzerServerViewModel.SendTeamsAsync();
        }
    }
}
