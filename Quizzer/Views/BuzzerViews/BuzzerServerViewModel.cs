using LocalBuzzer.Service;
using LocalBuzzer.Service.Base;
using QRCoder;
using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
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
    public class BuzzerServerViewModel : ViewModelBase
    {
        internal BuzzerServer? _server;
        internal Game? _game;

        public BuzzerControlsViewModel? BuzzerControlsViewModel { get; set; }

        private ServerState _serverState = ServerState.None;

        public event EventHandler<ServerState>? PlayerConnectionStateChanged;

        public override Task VMSaveAsync() => Task.CompletedTask;

        protected override Task OnloadAsync() => Task.CompletedTask;

        public string State => ServerState.ToString();

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

                BackgroundBrush = _serverState switch
                {
                    ServerState.None => Brushes.DarkGray,
                    ServerState.Running => Brushes.Red,
                    ServerState.Stopping => Brushes.Red,
                    ServerState.Stopped => Brushes.DarkGray,
                    ServerState.AllConnected => Brushes.Black,
                    ServerState.ActiveState => Brushes.Black,
                    _ => Brushes.Red
                };

                NotifyServerStateChanged();
                OnPropertyChanged(nameof(BackgroundBrush));
            }
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

        private void NotifyServerStateChanged()
        {
            OnPropertyChanged(nameof(ServerState));
            OnPropertyChanged(nameof(State));
            OnPropertyChanged(nameof(IsBuzzerServerRunning));

            startServerCommand?.RaiseCanExecuteChanged();
            stopServerCommand?.RaiseCanExecuteChanged();
            openPlayerQRCommand?.RaiseCanExecuteChanged();
            //resetRoundCommand?.RaiseCanExecuteChanged();

            PlayerConnectionStateChanged?.Invoke(this, ServerState);
        }

        private async void OnConnectionChanged(object? sender, PlayerConnection e)
        {
            // Avoid deadlocks: prefer InvokeAsync
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                CollectionViewSource.GetDefaultView(_players).Refresh();
                RecalcServerState();
            });
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
                    Port = 5000,
                    WebRootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot")
                });

                BuzzerControlsViewModel?.Dispose();
                BuzzerControlsViewModel = new();
                BuzzerControlsViewModel.SetBuzzerVerverViewModel(this);

                RecalcServerState();
            }
            catch (Exception ex)
            {
                var s = _server?.ServerState ?? ServerState.None;
                ServerState = s | ServerState.Error;

                MessageBox.Show(ex.Message, "Start Server failed", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
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

                BuzzerControlsViewModel?.Dispose();

                await _server.StopAsync();

                RecalcServerState();
            }
            catch (Exception ex)
            {
                var s = _server.ServerState;
                ServerState = s | ServerState.Error;

                MessageBox.Show(ex.Message, "Stop Server failed", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
        }

        //private AsyncRelayCommand? resetRoundCommand;

        //public ICommand ResetRoundCommand => resetRoundCommand ??= new AsyncRelayCommand(ResetRoundAsync, _ => IsBuzzerServerRunning);

        //private async Task ResetRoundAsync(object? commandParameter)
        //{
        //    if (_server == null) return;
        //    await _server.ResetRoundAsync();
        //}

        private RelayCommand? openPlayerQRCommand;

        public ICommand OpenPlayerQRCommand => openPlayerQRCommand ??= new RelayCommand(OpenPlayerQR);

        private void OpenPlayerQR(object? commandParameter)
        {
            if (commandParameter is not Player player)
                return;

            if (!IsBuzzerServerRunning)
            {
                MessageBox.Show("Server is not running.");
                return;
            }

            var endpoint = _server?.GetBestListeningIpPort();
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                MessageBox.Show("No LAN endpoint found (is the server started and bound to a LAN interface?).");
                return;
            }

            if (player.ConnectionState == PlayerConnection.Connected)
            {
                MessageBox.Show($"Player {player.DisplayName} is already connected.");
                return;
            }

            var url = $"http://{endpoint}?id={Uri.EscapeDataString(player.Id.ToString())}";
            var qrImage = CreateQrBitmap(url);

            var urlBox = new TextBox
            {
                Text = url,
                IsReadOnly = true,
                Margin = new Thickness(12, 8, 12, 0)
            };

            var copyBtn = new Button
            {
                Content = "Copy URL",
                Margin = new Thickness(12, 8, 12, 12),
                Padding = new Thickness(10, 6, 10, 6)
            };
            copyBtn.Click += (_, __) => Clipboard.SetText(url);

            var img = new Image
            {
                Source = qrImage,
                Width = 360,
                Height = 360,
                Margin = new Thickness(12),
                Stretch = Stretch.Uniform
            };

            var panel = new StackPanel();
            panel.Children.Add(new TextBlock
            {
                Text = $"QR für {player.CalculatedDisplayName}",
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(12, 12, 12, 0)
            });
            panel.Children.Add(img);
            panel.Children.Add(urlBox);
            panel.Children.Add(copyBtn);

            var win = new Window
            {
                Title = "Player QR",
                Content = panel,
                SizeToContent = SizeToContent.WidthAndHeight,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Application.Current?.MainWindow
            };

            win.Loaded += (o, e) =>
            {
                _server?.BuzzerController?.EventBus.ClientAssigned += (name, playerId) => PlayerAssigend(player, win, name, playerId);
            };

            win.Closed += (o, e) =>
            {
                _server?.BuzzerController?.EventBus.ClientAssigned -= (name, playerId) => PlayerAssigend(player, win, name, playerId);
            };

            win.Show();
        }

        public void PlayerAssigend(Player player, Window win, string displayname, Guid playerId)
        {
            RunOnUi(() =>
            {
                if (playerId == player.Id && player.ConnectionState == PlayerConnection.Connected)
                {
                    win.Close();
                }
            });
        }

        private static BitmapImage CreateQrBitmap(string payload)
        {
            using var gen = new QRCodeGenerator();
            using var data = gen.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);

            var png = new PngByteQRCode(data);
            byte[] bytes = png.GetGraphic(20);

            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.StreamSource = new MemoryStream(bytes);
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
    }
}