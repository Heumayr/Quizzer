using Quizzer.Base;
using Quizzer.Views.GameViews.Teamregeln;
using System.Windows;
using System.Windows.Input;
using static LocalBuzzer.Service.Base.States.BuzzerKeySelector;

namespace Quizzer.Views.GameViews
{
    /// <summary>
    /// Team-Abstimmung im Fragefenster (M37 T7): die Telefone zeigen beim Abstimmen nur die eigene Stimme und den Stand des
    /// eigenen Teams. Ist die Abstimmung zu - alle haben gewählt oder „Runde schließen" -, zählt die Regel je Team, die Karten
    /// tragen die Anteile (E1), der Beamer zeigt alle Teams, jedes Telefon die Antwort seines Teams. Einen Gleichstand ohne
    /// Sprecherstimme entscheidet die Spielleitung hier; sie kann auch einen Sprecherentscheid ändern.
    /// </summary>
    public partial class CurrentQuestionViewModel
    {
        private TeamAbstimmungAblauf? TeamAbstimmung => sonderregel as TeamAbstimmungAblauf;

        /// <summary>Je Team mit Gleichstand eine Zeile je Antwort zum Wählen - erst nach dem Schluss, sonst leer.</summary>
        public IReadOnlyList<TeamAbstimmungEintrag> TeamAbstimmungGleichstaende { get; private set; } = [];

        public Visibility TeamAbstimmungVisibility => TeamAbstimmungGleichstaende.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        private AsyncRelayCommand? teamAntwortWaehlenCommand;

        /// <summary>„Diese Antwort gilt" für ein Team mit Gleichstand (E1).</summary>
        public ICommand TeamAntwortWaehlenCommand => teamAntwortWaehlenCommand ??= new AsyncRelayCommand(
            p => p is TeamAbstimmungEintrag eintrag ? EntscheideTeamAbstimmungAsync(eintrag) : Task.CompletedTask);

        /// <summary>Vor dem Ausspielen der Tastenwahl: ab jetzt zeigen die Telefone Stimme und Stand des eigenen Teams.</summary>
        private void StarteTeamAbstimmung()
        {
            if (TeamAbstimmung != null)
                BuzzerControlsViewModel?.StarteTeamAbstimmung();
        }

        /// <summary>Die Abstimmung ist zu: die Regel zählt je Team; Karten, Beamer, Telefone und die Liste folgen.</summary>
        private async Task TeamAbstimmungZuAsync(IReadOnlyDictionary<Guid, SelectionResult> wahl)
        {
            if (TeamAbstimmung is not { } regel)
                return;

            regel.NimmStimmen(wahl.Where(e => e.Value != null).ToDictionary(e => e.Key, e => (IReadOnlyList<string>)(e.Value.SelectedKeys ?? []).ToList()));
            await ZeigeTeamAbstimmungAsync(regel);
        }

        /// <summary>Die Spielleitung wählt bei Gleichstand die Antwort eines Teams (E1); <c>false</c>, wenn sie nicht unter den Gleichauf ist.</summary>
        internal async Task<bool> EntscheideTeamAbstimmungAsync(TeamAbstimmungEintrag eintrag)
        {
            if (TeamAbstimmung is not { } regel || !regel.Entscheide(eintrag.TeamId, eintrag.Antwort))
                return false;

            await ZeigeTeamAbstimmungAsync(regel);
            return true;
        }

        private async Task ZeigeTeamAbstimmungAsync(TeamAbstimmungAblauf regel)
        {
            var teams = regel.Ergebnisse;
            var mitAntwort = teams.Count(t => t.Ergebnis?.Antwort != null);

            TeamAbstimmungGleichstaende = teams
                .Where(t => t.Ergebnis is { Gleichstand: true })
                .SelectMany(t => t.Ergebnis!.Gleichauf.Select(a => new TeamAbstimmungEintrag(t.Team.TeamId, a, regel.Beschriftung(t, a))))
                .ToList();

            ZeigeTeamregel(regel, $"Abgestimmt: {mitAntwort} von {teams.Count} Teams haben eine Antwort");
            Sonderregel.ZeigeAufloesung(regel.Aufloesung());

            foreach (var karte in Karten)
                karte.SonderregelGeaendert();

            WerteSonderregel();

            OnPropertyChanged(nameof(TeamAbstimmungGleichstaende));
            OnPropertyChanged(nameof(TeamAbstimmungVisibility));

            if (BuzzerControlsViewModel != null)
                await BuzzerControlsViewModel.SetzeTeamAbstimmungAsync(regel.ErgebnisseFuerTelefone());
        }

        private void LeereTeamAbstimmung()
        {
            TeamAbstimmungGleichstaende = [];
            OnPropertyChanged(nameof(TeamAbstimmungGleichstaende));
            OnPropertyChanged(nameof(TeamAbstimmungVisibility));
        }
    }

    /// <summary>Eine Antwort eines Teams mit Gleichstand in der Liste des Spielleiters - ein Knopf macht sie zur geltenden.</summary>
    public sealed class TeamAbstimmungEintrag
    {
        internal TeamAbstimmungEintrag(Guid teamId, string antwort, string beschriftung)
        {
            TeamId = teamId;
            Antwort = antwort;
            Beschriftung = beschriftung;
        }

        internal Guid TeamId { get; }

        internal string Antwort { get; }

        public string Beschriftung { get; }
    }
}
