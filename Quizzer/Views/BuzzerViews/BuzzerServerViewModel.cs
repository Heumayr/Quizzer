using LocalBuzzer.Service;
using LocalBuzzer.Service.Base;
using QRCoder;
using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.Views.StaticRessources;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Quizzer.Views.BuzzerViews
{
    public partial class BuzzerServerViewModel : ViewModelBase
    {
        internal BuzzerServer? _server;
        internal Game? _game;

        public BuzzerControlsViewModel? BuzzerControlsViewModel { get; set; }

        private ServerState _serverState = ServerState.None;

        public event EventHandler<ServerState>? PlayerConnectionStateChanged;

        public override Task VMSaveAsync() => Task.CompletedTask;

        protected override Task OnloadAsync() => Task.CompletedTask;

        public string State => ServerState.ToString();

        /// <summary>Ueberschrift der Meldungsfenster dieses ViewModels.</summary>
        internal const string ServerCaption = "Buzzer-Server";

        /// <summary>Wie viele Telefone bereits verbunden sind.</summary>
        public int ConnectedCount => _players.Count(p => p.ConnectionState == PlayerConnection.Connected);

        /// <summary>Wie viele Mitspieler das Spiel hat.</summary>
        public int PlayerCount => _players.Count;

        /// <summary>
        /// Der Zustand als Satz, den der Spielleiter lesen kann. Bis 2026-09-05 stand hier der
        /// rohe Name des Enums - „None“, „Running“, „ActiveState“ - und nirgends, auf wie viele
        /// Telefone noch gewartet wird.
        /// </summary>
        public string StateText
        {
            get
            {
                if (ServerState.HasFlag(ServerState.Error))
                    return "Fehler – der Buzzer-Server läuft nicht";

                if (ServerState.HasFlag(ServerState.Stopping))
                    return "Server wird beendet …";

                if (ServerState.HasFlag(ServerState.Starting) && !IsBuzzerServerRunning)
                    return "Server startet …";

                if (!IsBuzzerServerRunning)
                    return "Server nicht gestartet";

                if (PlayerCount == 0)
                    return "Läuft – kein Mitspieler im Spiel";

                return ConnectedCount >= PlayerCount
                    ? $"Alle {PlayerCount} Telefone verbunden"
                    : $"Läuft – {ConnectedCount} von {PlayerCount} Telefonen verbunden";
            }
        }

        /// <summary>
        /// ViewModel state = server state + computed flags (e.g. AllConnected).
        /// </summary>
        public ServerState ServerState
        {
            get => _serverState;
            private set
            {
                if (_serverState == value) return;
                _serverState = value;

                // Gedeckte Toene statt Rot fuer den Normalfall: Rot war bisher der Zustand
                // "laeuft, wartet noch auf Telefone" und faerbte im Fragefenster den Grund, auf
                // dem die Uebersicht des Spielleiters steht. Das Wort daneben traegt die
                // Bedeutung, nicht die Farbe.
                BackgroundBrush = _serverState switch
                {
                    _ when _serverState.HasFlag(ServerState.Error) => ErrorBrush,
                    _ when _serverState.HasFlag(ServerState.AllConnected) => AllConnectedBrush,
                    _ when _serverState.HasFlag(ServerState.Running) => WaitingBrush,
                    _ => IdleBrush
                };

                NotifyServerStateChanged();
                OnPropertyChanged(nameof(BackgroundBrush));
            }
        }

        private static readonly Brush IdleBrush = Freeze("#2A2A2A");
        private static readonly Brush WaitingBrush = Freeze("#3A2F10");
        private static readonly Brush AllConnectedBrush = Freeze("#10321A");
        private static readonly Brush ErrorBrush = Freeze("#4A1010");

        private static Brush Freeze(string hex)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            brush.Freeze();
            return brush;
        }

        private Brush backgroundBrush = Brushes.DarkGray;

        public Brush BackgroundBrush
        {
            get => backgroundBrush;
            set
            {
                backgroundBrush = value;
                OnPropertyChanged();
            }
        }

        private readonly ObservableCollection<Player> _players = new();
        public ObservableCollection<Player> Players => _players;

        public Game? Game
        {
            get => _game;
            set
            {
                if (_game != null)
                    foreach (var p in _game.Players)
                        p.PlayerConnectionChanged -= OnConnectionChanged;

                _game = value;

                _players.Clear();
                if (_game != null)
                {
                    foreach (var p in _game.Players)
                    {
                        _players.Add(p);
                        p.PlayerConnectionChanged += OnConnectionChanged;
                    }
                }

                OnPropertyChanged(nameof(Game));
                OnPropertyChanged(nameof(Players));

                // Re-evaluate state when game/players change
                RecalcServerState();
            }
        }

        /// <summary>
        /// True if underlying server is running (independent from AllConnected flag).
        /// </summary>
        public bool IsBuzzerServerRunning => _server != null && _server.ServerState.HasFlag(ServerState.Running);

        /// <summary>
        /// Die Adresse, unter der ein Telefon die Buzzer-Seite oeffnet. Sie stand bisher nur im
        /// QR-Fenster - der Spielleiter konnte sie also niemandem ansagen und bei Stoerungen auch
        /// nicht selbst ausprobieren.
        /// </summary>
        public string EndpointUrl
        {
            get
            {
                var endpoint = IsBuzzerServerRunning ? _server?.GetBestListeningIpPort() : null;

                return string.IsNullOrWhiteSpace(endpoint) ? string.Empty : $"http://{endpoint}";
            }
        }

        public Visibility EndpointVisibility =>
            string.IsNullOrEmpty(EndpointUrl) ? Visibility.Collapsed : Visibility.Visible;

        private RelayCommand? copyEndpointCommand;

        public ICommand CopyEndpointCommand => copyEndpointCommand ??= new RelayCommand(CopyEndpoint);

        private void CopyEndpoint(object? commandParameter)
        {
            if (!string.IsNullOrEmpty(EndpointUrl))
                Clipboard.SetText(EndpointUrl);
        }

        private void NotifyServerStateChanged()
        {
            OnPropertyChanged(nameof(ServerState));
            OnPropertyChanged(nameof(State));
            OnPropertyChanged(nameof(StateText));
            OnPropertyChanged(nameof(ConnectedCount));
            OnPropertyChanged(nameof(PlayerCount));
            OnPropertyChanged(nameof(EndpointUrl));
            OnPropertyChanged(nameof(EndpointVisibility));
            OnPropertyChanged(nameof(IsBuzzerServerRunning));

            startServerCommand?.RaiseCanExecuteChanged();
            stopServerCommand?.RaiseCanExecuteChanged();
            openPlayerQRCommand?.RaiseCanExecuteChanged();
            //resetRoundCommand?.RaiseCanExecuteChanged();

            PlayerConnectionStateChanged?.Invoke(this, ServerState);
        }

        /// <summary>
        /// Ein Telefon hat sich an- oder abgemeldet. Der Aufruf kommt aus dem Kestrel-Thread.
        /// <para>
        /// Faehrt die Anwendung gerade herunter, nimmt der Dispatcher nichts mehr an; ohne den
        /// Ausstieg entkaeme aus dieser <c>async void</c>-Methode eine Ausnahme.
        /// </para>
        /// </summary>
        private async void OnConnectionChanged(object? sender, PlayerConnection e)
        {
            if (Application.Current?.Dispatcher.HasShutdownStarted == true)
                return;

            try
            {
                await RunOnUiAsync(() =>
                {
                    CollectionViewSource.GetDefaultView(_players).Refresh();
                    RecalcServerState();

                    // Der Zaehler im Zustandssatz haengt an den Spielern, nicht am Serverzustand:
                    // ohne diesen Anstoss bliebe "0 von 3" stehen, waehrend alle verbunden sind.
                    OnPropertyChanged(nameof(StateText));
                    OnPropertyChanged(nameof(ConnectedCount));
                });
            }
            catch (TaskCanceledException)
            {
                // Die Anwendung macht zu, waehrend der Server noch Telefone abmeldet.
            }
            catch (Exception ex)
            {
                ExceptionManager.HandleException(ex);
            }
        }

        /// <summary>
        /// Rebuild VM ServerState from:
        /// 1) _server.ServerState (authoritative server state)
        /// 2) computed AllConnected flag (players)
        /// </summary>
        private void RecalcServerState()
        {
            var s = _server?.ServerState ?? ServerState.None;

            // Only consider AllConnected if there are players
            bool allConnected =
                _players.Count > 0 &&
                _players.All(p => p.ConnectionState == PlayerConnection.Connected);

            if (allConnected)
                s |= ServerState.AllConnected;
            else
                s &= ~ServerState.AllConnected;

            ServerState = s;
        }

        // ---------------- Commands ----------------

        private AsyncRelayCommand? startServerCommand;

        public ICommand StartServerCommand =>
            startServerCommand ??= new AsyncRelayCommand(StartServerAsync, _ => !IsBuzzerServerRunning && !ServerState.HasFlag(ServerState.Starting));

        private async Task StartServerAsync(object? commandParameter)
        {
            try
            {
                if (_server == null)
                {
                    _server = new BuzzerServer
                    {
                        GetGame = () => Game
                    };
                }

                ServerState = (_server.ServerState | ServerState.Starting) & ~ServerState.Error;

                await _server.StartAsync(new BuzzerServerOptions
                {
                    Port = BuzzerPort,
                    WebRootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot")
                });

                BuzzerControlsViewModel?.Dispose();
                BuzzerControlsViewModel = new();
                BuzzerControlsViewModel.SetBuzzerVerverViewModel(this);

                RecalcServerState();
                RefreshFirewallHint();
            }
            catch (Exception ex)
            {
                var s = _server?.ServerState ?? ServerState.None;
                ServerState = s | ServerState.Error;

                // Nicht weiterwerfen: sonst zeigt der ExceptionManager dieselbe Sache ein zweites
                // Mal, mit Stapelabbild, mitten im Spielaufbau.
                UserPrompt.Inform(
                    "Der Buzzer-Server konnte nicht starten. Meist ist Port 5000 noch belegt – "
                    + "läuft noch eine zweite Quizzer-Instanz oder ein Testlauf?"
                    + Environment.NewLine + Environment.NewLine + ex.Message,
                    ServerCaption);
            }
        }

        private AsyncRelayCommand? stopServerCommand;

        public ICommand StopServerCommand =>
            stopServerCommand ??= new AsyncRelayCommand(StopServerAsync, _ => IsBuzzerServerRunning);

        private async Task StopServerAsync(object? commandParameter)
        {
            if (_server == null) return;
            if (!_server.ServerState.HasFlag(ServerState.Running)) return;

            try
            {
                ServerState = (ServerState | ServerState.Stopping) & ~ServerState.Starting;

                // Auch leeren, nicht nur entsorgen: das Fragefenster bindet es als
                // DataContext (QuestionMasterView.xaml), und ein entsorgter Stand haengt sonst
                // weiter in der Oberflaeche. Gemessen 2026-09-07 - ein "= null" gab es in
                // dieser Datei an keiner Stelle.
                BuzzerControlsViewModel?.Dispose();
                BuzzerControlsViewModel = null;

                await _server.StopAsync();

                RecalcServerState();
                RefreshFirewallHint();
            }
            catch (Exception ex)
            {
                var s = _server.ServerState;
                ServerState = s | ServerState.Error;

                UserPrompt.Inform(
                    "Der Buzzer-Server ließ sich nicht sauber beenden."
                    + Environment.NewLine + Environment.NewLine + ex.Message,
                    ServerCaption);
            }
        }

        //private AsyncRelayCommand? resetRoundCommand;

        //public ICommand ResetRoundCommand => resetRoundCommand ??= new AsyncRelayCommand(ResetRoundAsync, _ => IsBuzzerServerRunning);

        //private async Task ResetRoundAsync(object? commandParameter)
        //{
        //    if (_server == null) return;
        //    await _server.ResetRoundAsync();
        //}

    }
}