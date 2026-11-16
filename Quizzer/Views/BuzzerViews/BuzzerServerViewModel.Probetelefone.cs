using LocalBuzzer.Service.Base;
using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Input;

namespace Quizzer.Views.BuzzerViews
{
    /// <summary>
    /// Probetelefone am PC (M35 B3, V1): je Mitspieler ein Browserfenster mit seinem abgeleiteten
    /// Schluessel (<see cref="ProbePhoneLinks"/>) - Minispiele vorbereiten und ausprobieren ohne Handys.
    /// </summary>
    public partial class BuzzerServerViewModel
    {
        /// <summary>Oeffnet eine Adresse im Browser; ein Test ersetzt es, damit kein Fenster aufgeht.</summary>
        internal Action<Uri> OeffneBrowser { get; set; } = OeffneImBrowser;

        private RelayCommand? openProbePhonesCommand;

        public ICommand OpenProbePhonesCommand => openProbePhonesCommand ??= new RelayCommand(_ => OpenProbePhones());

        internal void OpenProbePhones()
        {
            if (!IsBuzzerServerRunning || _server == null)
            {
                UserPrompt.Inform("Der Buzzer-Server läuft nicht – zuerst „Server starten“.", ServerCaption);
                return;
            }

            var spieler = Game?.Players?.ToList() ?? [];

            if (spieler.Count == 0)
            {
                UserPrompt.Inform("Das Spiel hat noch keine Mitspieler – ohne sie gibt es keine Probetelefone.", ServerCaption);
                return;
            }

            // Der Letzte gewinnt (PlayerConnectionRegistry): ein Probetelefon loest ein echtes ab.
            var verbunden = spieler.Count(p => p.ConnectionState == PlayerConnection.Connected);

            if (verbunden > 0 && !UserPrompt.Confirm(
                    $"{verbunden} Mitspieler {(verbunden == 1 ? "ist" : "sind")} schon mit einem Telefon verbunden. "
                    + "Die Probetelefone lösen diese Verbindungen ab. Trotzdem öffnen?", ServerCaption))
                return;

            // localhost: das Fenster laeuft auf diesem Rechner, die Firewall spielt keine Rolle.
            foreach (var link in ProbePhoneLinks.For(spieler, _server.AccessKeys, $"localhost:{BuzzerPort}"))
                OeffneBrowser(link.Url);
        }

        /// <summary>Edge mit eigenem Fenster, sonst der Standardbrowser - wirft nie.</summary>
        private static void OeffneImBrowser(Uri url)
        {
            try
            {
                Process.Start(new ProcessStartInfo("msedge", $"--new-window \"{url}\"") { UseShellExecute = true });
            }
            catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
            {
                try
                {
                    Process.Start(new ProcessStartInfo(url.ToString()) { UseShellExecute = true });
                }
                catch (Exception inner) when (inner is Win32Exception or InvalidOperationException)
                {
                    UserPrompt.Inform($"Der Browser ließ sich nicht öffnen. Adresse zum Kopieren: {url}", ServerCaption);
                }
            }
        }
    }
}
