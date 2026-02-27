using LocalBuzzer.Service;
using LocalBuzzer.Service.Base;
using QRCoder;
using Quizzer.Base;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Enumerations;
using System.Collections.ObjectModel;
using System.IO;
using System.Security.Cryptography.X509Certificates;
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
        private BuzzerServer? _server;

        public override Task VMSaveAsync()
        {
            return Task.CompletedTask;
        }

        public string State { get; set; } = "Stopped";

        private readonly ObservableCollection<Player> _players = new();
        public ObservableCollection<Player> Players => _players;

        public Game? Game
        {
            get => game;
            set
            {
                if (game != null)
                    foreach (var p in game.Players)
                        p.PlayerConnectionChanged -= OnConnectionChanged;

                game = value;

                _players.Clear();
                if (game != null)
                {
                    foreach (var p in game.Players)
                    {
                        _players.Add(p);
                        p.PlayerConnectionChanged += OnConnectionChanged;
                    }
                }

                OnPropertyChanged(nameof(Game));
            }
        }

        private void OnConnectionChanged(object? sender, PlayerConnection e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                // If Player implements INotifyPropertyChanged for its connection props,
                // this is NOT needed. But if it doesn't, refresh the view:
                CollectionViewSource.GetDefaultView(_players).Refresh();
            });
        }

        private AsyncRelayCommand? startServerCommand;
        public ICommand StartServerCommand => startServerCommand ??= new AsyncRelayCommand(StartServerAsync);

        private async Task StartServerAsync(object? commandParameter)
        {
            if (_server != null)
            {
                MessageBox.Show("Server is already running");
                return;
            }

            State = "Starting";
            OnPropertyChanged(nameof(State));

            _server = new BuzzerServer();
            _server.GetGame = () => Game;
            _server.WinnerDeclared += (name, round) =>
            {
                MessageBox.Show($"Winner: {name} (Runde {round})");
            };

            await _server.StartAsync(new BuzzerServerOptions
            {
                Port = 5000,
                WebRootPath = System.IO.Path.Combine(AppContext.BaseDirectory, "wwwroot")
            });

            State = "Running";
            OnPropertyChanged(nameof(State));
        }

        private AsyncRelayCommand? stopServerCommand;
        public ICommand StopServerCommand => stopServerCommand ??= new AsyncRelayCommand(StopServerAsync);

        private async Task StopServerAsync(object? commandParameter)
        {
            if (_server is not null) await _server.StopAsync();

            _server = null;
            State = "Stopped";
            OnPropertyChanged(nameof(State));
        }

        private AsyncRelayCommand? resetRoundCommand;
        private Game? game;

        public ICommand ResetRoundCommand => resetRoundCommand ??= new AsyncRelayCommand(ResetRoundAsync);

        private async Task ResetRoundAsync(object? commandParameter)
        {
            if (_server == null) return;

            await _server.ResetRoundAsync();
        }

        private RelayCommand? openPlayerQRCommand;
        public ICommand OpenPlayerQRCommand => openPlayerQRCommand ??= new RelayCommand(OpenPlayerQR);

        public object ConnectionChanged { get; private set; }

        private void OpenPlayerQR(object? commandParameter)
        {
            if (commandParameter is not Player player)
                return;

            if (_server is null || !_server.IsRunning)
            {
                MessageBox.Show("Server is not running.");
                return;
            }

            // pick first LAN endpoint (e.g. 192.168.0.23:5000)
            var endpoint = _server.GetBestListeningIpPort();

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

            win.ShowDialog();
        }

        private static BitmapImage CreateQrBitmap(string payload)
        {
            using var gen = new QRCodeGenerator();
            using var data = gen.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);

            // PNG bytes
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