using Quizzer.Base;
using Quizzer.DataModels;
using Quizzer.DataModels.Models.Base;
using Quizzer.Logic.Controller.TypedControllers;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Quizzer.Views.GameViews
{
    /// <summary>
    /// „Wer spielt mit?" - direkt vor dem Start (M9: „spieler werden beim start dazu geholt bzw
    /// gesetzt"). Vorbelegt mit den Mitspielern des letzten Starts; Gaeste ohne Konto lassen sich
    /// hier anlegen. Der Gamemaster steht nicht zur Wahl - er leitet.
    /// </summary>
    public sealed class GamePlayersViewModel : ViewModelBase
    {
        public sealed class Choice : ViewCommonBase, System.ComponentModel.INotifyPropertyChanged
        {
            private bool isChecked;

            public Choice(Player player, bool isChecked, bool hasResults)
            {
                Player = player;
                this.isChecked = isChecked;
                HasResults = hasResults;
            }

            public Player Player { get; }

            public string Name => Player.CalculatedDisplayName;

            public bool HasResults { get; }

            public bool IsChecked
            {
                get => isChecked;
                set { isChecked = value; OnPropertyChanged(); }
            }
        }

        private Game? game;
        private string newGuestName = string.Empty;
        private string message = string.Empty;

        public Guid GameId { get; set; }

        public bool Confirmed { get; private set; }

        public ObservableCollection<Choice> Choices { get; } = new();

        public string GameName => game?.Designation ?? string.Empty;

        public string GameMasterName { get; private set; } = string.Empty;

        public string NewGuestName
        {
            get => newGuestName;
            set { newGuestName = value; OnPropertyChanged(); }
        }

        public string Message
        {
            get => message;
            private set { message = value; OnPropertyChanged(); }
        }

        protected override async Task OnloadAsync()
        {
            using var games = new GamesController();
            game = await games.GetAsync(GameId);

            if (game == null)
            {
                Message = "Das Spiel wurde nicht gefunden.";
                return;
            }

            // Ein Spiel aus der Zeit vor M9 hat womoeglich keinen Gamemaster - dann leitet, wer es startet.
            if ((game.ModeratorPlayerId == null || game.ModeratorPlayerId == Guid.Empty) && Session.CurrentModeratorId is { } me)
            {
                game.ModeratorPlayerId = me;
                await games.UpdateAsync(game);
                await games.SaveChangesAsync();
            }

            using var players = new PlayersController();
            var all = await players.GetAllAsync();

            var seated = game.PlayerXGames.Select(x => x.PlayerId).ToHashSet();
            var withResults = game.QuestionResults.Select(r => r.PlayerId).ToHashSet();

            GameMasterName = all.FirstOrDefault(p => p.Id == game.ModeratorPlayerId)?.CalculatedDisplayName ?? "(niemand)";

            Choices.Clear();
            foreach (var player in all.Where(p => p.Id != game.ModeratorPlayerId).OrderBy(p => p.CalculatedDisplayName, StringComparer.CurrentCultureIgnoreCase))
                Choices.Add(new Choice(player, seated.Contains(player.Id), withResults.Contains(player.Id)));

            OnPropertyChanged(nameof(GameName));
            OnPropertyChanged(nameof(GameMasterName));
        }

        public override Task VMSaveAsync() => Task.CompletedTask;

        private AsyncRelayCommand? addGuestCommand;

        /// <summary>Ein Gast ohne Konto - er bekommt eines, wenn der Admin es spaeter fuer ihn anlegt.</summary>
        public ICommand AddGuestCommand => addGuestCommand ??= new AsyncRelayCommand(async _ =>
        {
            var name = NewGuestName.Trim();
            if (name.Length == 0)
            {
                Message = "Erst einen Namen für den Gast eingeben.";
                return;
            }

            if (Choices.Any(c => string.Equals(c.Name, name, StringComparison.CurrentCultureIgnoreCase)))
            {
                Message = $"„{name}“ gibt es schon - einfach den Haken setzen.";
                return;
            }

            using var players = new PlayersController();
            var inserted = await players.InsertAsync(new Player { Designation = name, DisplayName = name, IsModerator = false });
            await players.SaveChangesAsync();

            Choices.Add(new Choice(inserted.Entity, true, false));
            NewGuestName = string.Empty;
            Message = $"Gast „{name}“ angelegt und dabei.";
        });

        private AsyncRelayCommand? confirmCommand;

        public ICommand ConfirmCommand => confirmCommand ??= new AsyncRelayCommand(async _ =>
        {
            if (game == null)
                return;

            var chosen = Choices.Where(c => c.IsChecked).Select(c => c.Player.Id).ToHashSet();

            if (chosen.Count == 0)
            {
                Message = "Mindestens ein Mitspieler muss dabei sein.";
                return;
            }

            var leaving = game.PlayerXGames.Where(x => !chosen.Contains(x.PlayerId)).ToList();
            var leavingWithPoints = Choices.Where(c => c.HasResults && leaving.Any(l => l.PlayerId == c.Player.Id)).Select(c => c.Name).ToList();

            if (leavingWithPoints.Count > 0
                && !UserPrompt.Confirm(
                    $"{string.Join(", ", leavingWithPoints)} hat in diesem Spiel schon Punkte. "
                    + "Ohne Haken fällt das aus der Wertung heraus."
                    + Environment.NewLine + Environment.NewLine + "Trotzdem ohne sie spielen?",
                    "Mitspieler"))
                return;

            using var seats = new PlayerXGamesController();

            foreach (var seat in leaving)
                await seats.DeleteAsync(seat.Id);

            var seated = game.PlayerXGames.Select(x => x.PlayerId).ToHashSet();
            foreach (var playerId in chosen.Where(id => !seated.Contains(id)))
                await seats.InsertAsync(new PlayerXGame { GameId = game.Id, PlayerId = playerId });

            await seats.SaveChangesAsync();

            Confirmed = true;
            Window?.Close();
        });

        private RelayCommand? cancelCommand;

        public ICommand CancelCommand => cancelCommand ??= new RelayCommand(_ =>
        {
            Confirmed = false;
            Window?.Close();
        });
    }
}
