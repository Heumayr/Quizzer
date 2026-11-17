using Quizzer.Base;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Questions.Teams;
using Quizzer.Logic.Controller.TypedControllers;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Quizzer.Views.Teams
{
    /// <summary>
    /// „Teams bearbeiten“ im Spielaufbau und „Teams umsetzen“ im Spielfenster (M37 T2, F-M37-05): Teams anlegen,
    /// umbenennen, färben und entfernen, je Mitspieler zuteilen, auslosen. Alles ist Entwurf, bis „Übernehmen“ es in
    /// einem Zug schreibt (<see cref="GameTeamsController.SaveLineupAsync"/>) und den frischen Stand ins geladene Spiel
    /// übernimmt. Der Spielleiter steht nicht zur Wahl (<see cref="Teamzuordnung.Teilnehmer"/>).
    /// </summary>
    public sealed class TeamsViewModel : ViewModelBase
    {
        private Game? spiel;
        private int auslosAnzahl = TeamGrenzen.MinTeams;
        private string message = string.Empty;
        private string titel = "Teams bearbeiten";

        public string Titel
        {
            get => titel;
            private set { titel = value; OnPropertyChanged(); }
        }

        public string GameName => spiel?.Designation ?? string.Empty;

        /// <summary>Die Teams des Entwurfs in ihrer Reihenfolge.</summary>
        public ObservableCollection<TeamZeile> Teams { get; } = new();

        /// <summary>„ohne Team“ und die Teams - die Auswahl je Mitspieler. Nur einzeln geändert, nie neu gebaut.</summary>
        public ObservableCollection<TeamZeile> TeamAuswahl { get; } = [TeamZeile.Ohne];

        public ObservableCollection<MitgliedZeile> Mitglieder { get; } = new();

        public IReadOnlyList<FarbWahl> Farben { get; } = TeamFarben.Alle.Select(f => new FarbWahl(f)).ToList();

        public int AuslosAnzahl
        {
            get => auslosAnzahl;
            set { auslosAnzahl = value; OnPropertyChanged(); }
        }

        /// <summary>Hinweis im Dialog - was das Auslosen getan hat oder warum etwas nicht geht.</summary>
        public string Message
        {
            get => message;
            private set { message = value; OnPropertyChanged(); }
        }

        public bool Confirmed { get; private set; }

        /// <summary>Die Sperre des Aufrufers um das Schreiben - im Spielaufbau die des Rasteraufbaus (B26).</summary>
        internal Func<Func<Task>, Task> Guard { get; set; } = arbeit => arbeit();

        /// <summary>Die Zufallsquelle des Auslosens; Tests reichen eine feste herein.</summary>
        internal Random Zufall { get; set; } = Random.Shared;

        /// <summary>Baut den Entwurf aus dem geladenen Spiel - es wird erst beim Übernehmen verändert.</summary>
        internal void Lade(Game game, string dialogTitel)
        {
            spiel = game;
            Titel = dialogTitel;

            foreach (var team in game.GameTeams.OrderBy(t => t.SortOrder))
            {
                var farbe = TeamFarben.IsValid(team.ColorIndex) ? TeamFarben.Alle[team.ColorIndex] : TeamFarben.Alle[0];
                var zeile = new TeamZeile(team.Id, team.Name, farbe);
                Teams.Add(zeile);
                TeamAuswahl.Add(zeile);
            }

            foreach (var platz in Teamzuordnung.Teilnehmer(game).OrderBy(x => x.Player?.CalculatedDisplayName, StringComparer.CurrentCultureIgnoreCase))
            {
                var team = Teams.FirstOrDefault(t => t.Id == platz.GameTeamId) ?? TeamZeile.Ohne;
                Mitglieder.Add(new MitgliedZeile(platz.PlayerId, platz.Player?.CalculatedDisplayName ?? "(unbekannt)", team, Zaehle));
            }

            AuslosAnzahl = Math.Max(TeamGrenzen.MinTeams, Teams.Count);
            Zaehle();
            OnPropertyChanged(nameof(GameName));
        }

        private void Zaehle()
        {
            foreach (var team in Teams)
                team.Mitglieder = Mitglieder.Count(m => m.Auswahl == team);
        }

        public override Task VMSaveAsync() => Task.CompletedTask;

        /// <summary>Nichts zu laden: der Entwurf entsteht in <see cref="Lade"/> aus dem Spiel des Aufrufers.</summary>
        protected override Task OnloadAsync() => Task.CompletedTask;

        private RelayCommand? addTeamCommand;

        public ICommand AddTeamCommand => addTeamCommand ??= new RelayCommand(_ => NeuesTeam());

        private TeamZeile? NeuesTeam()
        {
            // Die Palette ist die Grenze (TeamGrenzen.MaxTeams): ohne freie Farbe kein neues Team.
            if (TeamFarben.ErsteFreie(Teams.Select(t => t.Farbe.Index)) is not { } farbe)
            {
                Message = $"Höchstens {TeamGrenzen.MaxTeams} Teams – so viele Farben hat die Palette.";
                return null;
            }

            var zeile = new TeamZeile(Guid.NewGuid(), $"Team {farbe.Name}", farbe);
            Teams.Add(zeile);
            TeamAuswahl.Add(zeile);
            Message = string.Empty;
            return zeile;
        }

        private AsyncRelayCommand? removeTeamCommand;

        public ICommand RemoveTeamCommand => removeTeamCommand ??= new AsyncRelayCommand(p => EntferneAsync(p as TeamZeile));

        /// <summary>
        /// Nimmt ein Team aus dem Entwurf; seine Mitglieder sind danach ohne Team. Ein gespeichertes Team mit Ergebnissen
        /// bleibt - es lässt sich nur umbenennen (F-M37-07: die Punkte bleiben beim alten Team).
        /// </summary>
        private async Task EntferneAsync(TeamZeile? zeile)
        {
            if (zeile == null || !Teams.Contains(zeile))
                return;

            using (var ctrl = new GameTeamsController())
            {
                if (await ctrl.CountResultsAsync(zeile.Id) is > 0 and var ergebnisse)
                {
                    Message = new TeamHasResultsException(zeile.Name, ergebnisse).Message;
                    return;
                }
            }

            foreach (var mitglied in Mitglieder.Where(m => m.Auswahl == zeile))
                mitglied.Auswahl = TeamZeile.Ohne;

            Teams.Remove(zeile);
            TeamAuswahl.Remove(zeile);
            Message = string.Empty;
        }

        private RelayCommand? drawCommand;

        public ICommand DrawCommand => drawCommand ??= new RelayCommand(_ => Lose());

        /// <summary>
        /// Lost die Mitspieler auf die ersten <see cref="AuslosAnzahl"/> Teams aus und legt fehlende an. Weitere Teams
        /// bleiben stehen, aber leer - entfernen ist ein eigener Handgriff.
        /// </summary>
        private void Lose()
        {
            if (Teamauslosung.Problem(Mitglieder.Count, AuslosAnzahl) is { } problem)
            {
                Message = problem;
                return;
            }

            // Problem() hält die Anzahl unter der Palette - eine freie Farbe gibt es für jedes fehlende Team.
            for (var fehlt = AuslosAnzahl - Teams.Count; fehlt > 0; fehlt--)
                NeuesTeam();

            var lostTeams = Teams.Take(AuslosAnzahl).ToList();
            var los = Teamauslosung.Lose(Mitglieder.Select(m => m.PlayerId), lostTeams.Select(t => t.Id), Zufall);

            foreach (var mitglied in Mitglieder)
                mitglied.Auswahl = lostTeams.First(t => t.Id == los[mitglied.PlayerId]);

            var leer = Teams.Skip(AuslosAnzahl).Select(t => $"„{t.Name}“").ToList();
            Message = $"Ausgelost auf {AuslosAnzahl} Teams."
                + (leer.Count > 0 ? $" Leer geblieben: {string.Join(", ", leer)}." : string.Empty)
                + " Erst „Übernehmen“ speichert.";
        }

        private AsyncRelayCommand? saveCommand;

        public ICommand SaveCommand => saveCommand ??= new AsyncRelayCommand(_ => UebernehmenAsync());

        /// <summary>Schreibt den Entwurf in einem Zug und übernimmt den frischen Stand ins geladene Spiel.</summary>
        internal async Task UebernehmenAsync()
        {
            if (spiel == null)
                return;

            var teams = Teams.Select((t, i) => new GameTeam { Id = t.Id, GameId = spiel.Id, Name = t.Name.Trim(), ColorIndex = t.Farbe.Index, SortOrder = i }).ToList();
            var zuteilung = Mitglieder.ToDictionary(m => m.PlayerId, m => m.TeamId);
            TeamAufstellung? frisch = null;

            try
            {
                await Guard(async () =>
                {
                    using var ctrl = new GameTeamsController();
                    frisch = await ctrl.SaveLineupAsync(spiel.Id, teams, zuteilung);
                });
            }
            catch (Exception ex) when (ex is ArgumentException or TeamHasResultsException)
            {
                UserPrompt.Inform(ex.Message, Titel);
                return;
            }

            Uebernimm(spiel, frisch!);
            Confirmed = true;
            Window?.Close();
        }

        /// <summary>Der frische Stand ins geladene Spiel: Teams ersetzen, je Platz Team und RowVersion.</summary>
        private static void Uebernimm(Game game, TeamAufstellung frisch)
        {
            game.GameTeams.Clear();
            game.GameTeams.AddRange(frisch.Teams);

            foreach (var platz in game.PlayerXGames)
            {
                if (frisch.Plaetze.FirstOrDefault(x => x.Id == platz.Id) is not { } neu)
                    continue;

                platz.GameTeamId = neu.GameTeamId;
                platz.RowVersion = neu.RowVersion;
            }
        }

        private RelayCommand? cancelCommand;

        public ICommand CancelCommand => cancelCommand ??= new RelayCommand(_ =>
        {
            Confirmed = false;
            Window?.Close();
        });
    }
}
