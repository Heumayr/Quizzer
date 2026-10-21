using LocalBuzzer.Service.Base;
using Quizzer.Base;
using Quizzer.DataModels.Models.Base;
using Quizzer.Logic.Controller.TypedControllers;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace Quizzer.Views.BuzzerViews
{
    /// <summary>
    /// M10: Mitspieler melden sich am Telefon selbst an - mit ihrem Namen oder als Gast - und der
    /// Gamemaster bestaetigt hier. Ein Kennwort geht dabei nie ueber das WLAN (Rueckfrage M10).
    /// </summary>
    public partial class BuzzerServerViewModel
    {
        public sealed record JoinItem(JoinRequest Request)
        {
            public string Text => Request.IsGuest
                ? $"{Request.DisplayName} (Gast) möchte mitspielen"
                : $"{Request.DisplayName} möchte mitspielen";
        }

        private List<JoinCandidate> joinCandidates = new();

        public ObservableCollection<JoinItem> PendingJoins { get; } = new();

        public Visibility PendingJoinsVisibility => PendingJoins.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>Ein neuer Mitspieler ist im laufenden Spiel - das Spielleiter-Fenster zieht nach.</summary>
        public event EventHandler<Player>? PlayerJoined;

        /// <summary>Nach dem Start des Servers: Namen bereitstellen, auf Anfragen hoeren.</summary>
        private async Task EnableJoiningAsync()
        {
            if (_server == null)
                return;

            await RefreshJoinCandidatesAsync();
            _server.GetJoinCandidates = () => joinCandidates;
            _server.JoinRequests.Requested += request => RunOnUi(() =>
            {
                PendingJoins.Add(new JoinItem(request));
                OnPropertyChanged(nameof(PendingJoinsVisibility));
            });
        }

        /// <summary>Alle Mitspieler ausser dem Gamemaster - auch die schon eingetragenen (neues Telefon).</summary>
        private async Task RefreshJoinCandidatesAsync()
        {
            using var players = new PlayersController();
            var all = await players.GetAllAsync();

            joinCandidates = all
                .Where(p => p.Id != Game?.ModeratorPlayerId)
                .Select(p => new JoinCandidate(p.Id, p.CalculatedDisplayName))
                .ToList();
        }

        private AsyncRelayCommand? acceptJoinCommand;

        public ICommand AcceptJoinCommand => acceptJoinCommand ??= new AsyncRelayCommand(async p =>
        {
            if (p is not JoinItem item || _server == null || Game == null)
                return;

            var player = await ResolveJoinPlayerAsync(item.Request);
            if (player == null)
            {
                UserPrompt.Inform($"{item.Request.DisplayName} wurde nicht gefunden.", ServerCaption);
                return;
            }

            if (Game.PlayerXGames.All(x => x.PlayerId != player.Id))
            {
                using var seats = new PlayerXGamesController();
                var inserted = await seats.InsertAsync(new PlayerXGame { GameId = Game.Id, PlayerId = player.Id });
                await seats.SaveChangesAsync();

                inserted.Entity.Player = player;
                Game.PlayerXGames.Add(inserted.Entity);

                // Den Setter neu durchlaufen: Liste und Verbindungsbeobachtung kennen den Neuen sonst nicht.
                Game = Game;
                PlayerJoined?.Invoke(this, player);
            }

            _server.JoinRequests.Accept(item.Request.Id, player.Id);
            RemoveJoin(item);
        });

        private RelayCommand? declineJoinCommand;

        public ICommand DeclineJoinCommand => declineJoinCommand ??= new RelayCommand(p =>
        {
            if (p is not JoinItem item || _server == null)
                return;

            _server.JoinRequests.Decline(item.Request.Id);
            RemoveJoin(item);
        });

        private RelayCommand? openJoinQrCommand;

        /// <summary>Ein QR-Code fuer alle: wer ihn scannt, waehlt seinen Namen selbst.</summary>
        public ICommand OpenJoinQrCommand => openJoinQrCommand ??= new RelayCommand(_ =>
        {
            var endpoint = _server?.GetBestListeningIpPort();

            if (!IsBuzzerServerRunning || string.IsNullOrWhiteSpace(endpoint))
            {
                UserPrompt.Inform("Der Buzzer-Server läuft nicht oder hat keine Adresse im Netzwerk.", ServerCaption);
                return;
            }

            ShowQrWindow(new Player { DisplayName = "alle (selbst anmelden)" }, $"http://{endpoint}/?join=1");
        });

        private async Task<Player?> ResolveJoinPlayerAsync(JoinRequest request)
        {
            using var players = new PlayersController();

            if (request.PlayerId is { } id)
                return Game?.Players.FirstOrDefault(p => p.Id == id) ?? await players.GetAsync(id);

            // Gast: ein neuer Mitspieler ohne Konto.
            var inserted = await players.InsertAsync(new Player
            {
                Designation = request.DisplayName,
                DisplayName = request.DisplayName,
                IsModerator = false,
            });
            await players.SaveChangesAsync();
            await RefreshJoinCandidatesAsync();

            return inserted.Entity;
        }

        private void RemoveJoin(JoinItem item)
        {
            PendingJoins.Remove(item);
            OnPropertyChanged(nameof(PendingJoinsVisibility));
        }
    }
}
