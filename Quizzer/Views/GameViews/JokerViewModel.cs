using Quizzer.Base;
using Quizzer.DataModels.Anzeige;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.Logic.Controller.TypedControllers;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Quizzer.Views.GameViews
{
    /// <summary>
    /// Joker aus dem Spielleiterfenster (M35 C7a, Entwurf F-M35-12): einem Mitspieler einen Joker geben
    /// (Art, Grund), je Mitspieler seine Joker sehen und einen noch offenen zurücknehmen. Eingelöst wird
    /// erst in C7b - ein eingelöster hat schon gewirkt und bleibt stehen.
    /// </summary>
    public sealed class JokerViewModel : ViewModelBase
    {
        public sealed record KindChoice(JokerKind Kind)
        {
            public string Name => JokerTexte.Name(Kind);

            public string Wirkung => JokerTexte.Wirkung(Kind);
        }

        public sealed class JokerEntry(PlayerJoker joker)
        {
            public PlayerJoker Joker { get; } = joker;

            public string Name => JokerTexte.Name(Joker.Kind);

            public string Reason => Joker.Reason;

            public bool IsRedeemed => Joker.IsRedeemed;

            public string Status => Joker.IsRedeemed ? "eingelöst" : "offen";
        }

        public sealed class PlayerEntry(Player player, IEnumerable<PlayerJoker> jokers)
        {
            public Player Player { get; } = player;

            public string Name => Player.CalculatedDisplayName;

            public List<JokerEntry> Jokers { get; } = jokers.Select(j => new JokerEntry(j)).ToList();

            public string Summary
            {
                get
                {
                    var offen = Jokers.Count(j => !j.IsRedeemed);
                    var eingeloest = Jokers.Count - offen;

                    return Jokers.Count == 0 ? "keine Joker" : $"{offen} offen, {eingeloest} eingelöst";
                }
            }
        }

        private PlayerEntry? selectedPlayer;
        private KindChoice selectedKind;
        private string reason = string.Empty;
        private string message = string.Empty;
        private string gameName = string.Empty;

        public JokerViewModel()
        {
            selectedKind = Kinds[0];
        }

        public Guid GameId { get; set; }

        public string GameName
        {
            get => gameName;
            private set { gameName = value; OnPropertyChanged(); }
        }

        public ObservableCollection<PlayerEntry> Players { get; } = new();

        public IReadOnlyList<KindChoice> Kinds { get; } = Enum.GetValues<JokerKind>().Select(k => new KindChoice(k)).ToList();

        public int MaxReasonLength => PlayerJoker.MaxReasonLength;

        public PlayerEntry? SelectedPlayer
        {
            get => selectedPlayer;
            set { selectedPlayer = value; OnPropertyChanged(); }
        }

        public KindChoice SelectedKind
        {
            get => selectedKind;
            set { selectedKind = value ?? Kinds[0]; OnPropertyChanged(); }
        }

        public string Reason
        {
            get => reason;
            set { reason = value ?? string.Empty; OnPropertyChanged(); }
        }

        public string Message
        {
            get => message;
            private set { message = value; OnPropertyChanged(); }
        }

        protected override async Task OnloadAsync() => await ReloadAsync();

        public override Task VMSaveAsync() => Task.CompletedTask;

        private async Task ReloadAsync()
        {
            using var games = new GamesController();
            var game = await games.GetAsync(GameId);

            if (game == null)
            {
                Message = "Das Spiel wurde nicht gefunden.";
                return;
            }

            GameName = game.Designation;

            using var ctrl = new PlayerJokersController();
            var jokers = await ctrl.GetForGameAsync(GameId);
            var selectedId = SelectedPlayer?.Player.Id;

            Players.Clear();
            foreach (var player in game.Players.Where(p => p != null).OrderBy(p => p.CalculatedDisplayName, StringComparer.CurrentCultureIgnoreCase))
                Players.Add(new PlayerEntry(player, jokers.Where(j => j.PlayerId == player.Id)));

            SelectedPlayer = Players.FirstOrDefault(p => p.Player.Id == selectedId);
        }

        private AsyncRelayCommand? grantCommand;

        /// <summary>Gibt dem gewählten Mitspieler einen Joker der gewählten Art, mit Grund.</summary>
        public ICommand GrantCommand => grantCommand ??= new AsyncRelayCommand(async _ =>
        {
            if (SelectedPlayer is not { } spieler)
            {
                Message = "Erst einen Mitspieler wählen.";
                return;
            }

            var grund = Reason.Trim();
            if (grund.Length == 0 || grund.Length > PlayerJoker.MaxReasonLength)
            {
                Message = $"Der Grund fehlt oder ist länger als {PlayerJoker.MaxReasonLength} Zeichen.";
                return;
            }

            using (var ctrl = new PlayerJokersController())
            {
                await ctrl.GrantAsync(GameId, spieler.Player.Id, SelectedKind.Kind, grund, DateTimeOffset.Now);
                await ctrl.SaveChangesAsync();
            }

            Reason = string.Empty;
            await ReloadAsync();
            Message = $"{spieler.Name} hat einen Joker „{SelectedKind.Name}“ bekommen.";
        });

        private AsyncRelayCommand? withdrawCommand;

        /// <summary>Nimmt einen noch offenen Joker zurück, nach Rückfrage. Ein eingelöster bleibt.</summary>
        public ICommand WithdrawCommand => withdrawCommand ??= new AsyncRelayCommand(async p =>
        {
            if (p is not JokerEntry eintrag)
                return;

            var besitzer = Players.FirstOrDefault(x => x.Player.Id == eintrag.Joker.PlayerId)?.Name ?? "dem Mitspieler";

            if (!UserPrompt.Confirm(
                    $"Den Joker „{eintrag.Name}“ von {besitzer} zurücknehmen?" + Environment.NewLine
                    + $"Grund war: {eintrag.Reason}",
                    "Joker"))
                return;

            bool weg;
            using (var ctrl = new PlayerJokersController())
            {
                weg = await ctrl.WithdrawAsync(eintrag.Joker.Id);
                await ctrl.SaveChangesAsync();
            }

            await ReloadAsync();
            Message = weg
                ? $"Der Joker „{eintrag.Name}“ von {besitzer} ist zurückgenommen."
                : "Der Joker ist schon eingelöst und bleibt stehen.";
        }, p => p is JokerEntry { IsRedeemed: false });

        private RelayCommand? closeCommand;

        public ICommand CloseCommand => closeCommand ??= new RelayCommand(_ => Window?.Close());
    }
}
