using QRCoder;
using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Quizzer.Views.BuzzerViews
{
    /// <summary>
    /// Der QR-Code, mit dem ein Telefon dem Spiel beitritt. Eigene Teildatei, damit die
    /// Hauptdatei unter der Groessengrenze bleibt.
    /// </summary>
    public partial class BuzzerServerViewModel
    {
        private RelayCommand? openPlayerQRCommand;

        public ICommand OpenPlayerQRCommand => openPlayerQRCommand ??= new RelayCommand(OpenPlayerQR);

        private void OpenPlayerQR(object? commandParameter)
        {
            if (commandParameter is not Player player)
                return;

            if (!IsBuzzerServerRunning)
            {
                UserPrompt.Inform("Der Buzzer-Server läuft nicht – zuerst „Server starten“.", ServerCaption);
                return;
            }

            var endpoint = _server?.GetBestListeningIpPort();

            if (string.IsNullOrWhiteSpace(endpoint))
            {
                UserPrompt.Inform(
                    "Es wurde keine Adresse im Netzwerk gefunden. Hängt der Rechner am WLAN "
                    + "oder am Kabel?", ServerCaption);
                return;
            }

            ShowQrWindow(player, $"http://{endpoint}?id={Uri.EscapeDataString(player.Id.ToString())}");
        }

        /// <summary>
        /// Zeigt den Code. Auch fuer einen Spieler, der als verbunden gilt: sein Zustand kann aus
        /// einer abgerissenen Sitzung stammen, und ein neuer Scan loest die alte Verbindung ab.
        /// Bis 2026-09-05 verweigerte diese Stelle den Code mit einer Meldung ohne Ausweg.
        /// </summary>
        private void ShowQrWindow(Player player, string url)
        {
            var panel = new StackPanel();

            panel.Children.Add(new TextBlock
            {
                Text = $"QR-Code für {player.CalculatedDisplayName}",
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(12, 12, 12, 0),
            });

            if (player.ConnectionState == PlayerConnection.Connected)
            {
                panel.Children.Add(new TextBlock
                {
                    Text = $"{player.CalculatedDisplayName} gilt als verbunden – "
                         + "ein neuer Scan ersetzt die bisherige Verbindung.",
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 360,
                    Margin = new Thickness(12, 6, 12, 0),
                    Foreground = Brushes.Wheat,
                });
            }

            panel.Children.Add(new Image
            {
                Source = CreateQrBitmap(url),
                Width = 360,
                Height = 360,
                Margin = new Thickness(12),
                Stretch = Stretch.Uniform,
            });

            panel.Children.Add(new TextBox
            {
                Text = url,
                IsReadOnly = true,
                Margin = new Thickness(12, 8, 12, 0),
            });

            var copyBtn = new Button
            {
                Content = "Adresse kopieren",
                Margin = new Thickness(12, 8, 12, 12),
                Padding = new Thickness(10, 6, 10, 6),
            };

            copyBtn.Click += (_, _) => Clipboard.SetText(url);
            panel.Children.Add(copyBtn);

            var win = new WindowBase
            {
                Title = $"QR-Code für {player.CalculatedDisplayName}",
                Content = panel,
                SizeToContent = SizeToContent.WidthAndHeight,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window ?? Application.Current?.MainWindow,
            };

            AttachAssignedHandler(player, win);
            win.Show();
        }

        /// <summary>
        /// Schliesst das Fenster, sobald genau dieser Spieler verbunden ist - und meldet sich
        /// danach wieder ab.
        /// <para>
        /// Bis 2026-09-05 wurden zum An- und Abmelden zwei verschiedene Lambdas benutzt. Die
        /// Abmeldung griff deshalb nie: jedes je geoeffnete QR-Fenster blieb am Ereignis haengen
        /// und rief spaeter <c>Close()</c> auf einem laengst geschlossenen Fenster.
        /// </para>
        /// </summary>
        internal void AttachAssignedHandler(Player player, Window win)
        {
            var bus = _server?.BuzzerController?.EventBus;

            if (bus == null)
                return;

            void OnAssigned(string displayName, Guid playerId)
                => PlayerAssigend(player, win, displayName, playerId);

            bus.ClientAssigned += OnAssigned;

            win.Closed += (_, _) => bus.ClientAssigned -= OnAssigned;
        }

        public void PlayerAssigend(Player player, Window win, string displayname, Guid playerId)
        {
            // Nicht blockierend: der Aufruf kommt aus dem Kestrel-Thread mitten im Handshake.
            _ = RunOnUiAsync(() =>
            {
                if (playerId == player.Id && player.ConnectionState == PlayerConnection.Connected)
                    win.Close();
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
