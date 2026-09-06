using Quizzer.Base;
using Quizzer.Views.BuzzerViews.Firewall;
using Quizzer.Views.StaticRessources;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;

namespace Quizzer.Views.BuzzerViews
{
    /// <summary>
    /// Der haeufigste Grund dafuer, dass kein Telefon durchkommt, obwohl der Server laeuft: die
    /// Windows-Firewall gibt den Port im aktiven Netz nicht frei.
    /// <para>
    /// Zweimal gemessen (2026-08-24 und 2026-09-05): die Regeln fuer die Anwendung galten nur
    /// fuer das Profil Oeffentlich, das Netz war Privat. Das Programm sagte „Running“, und die
    /// Suche nach der Ursache ging in den Quelltext statt in die Firewall.
    /// </para>
    /// </summary>
    public partial class BuzzerServerViewModel
    {
        /// <summary>
        /// Der Port, auf dem der Buzzer-Server lauscht.
        /// <para>
        /// <b>Im Betrieb immer 5000</b> - die Firewallregel heißt „Quizzer Buzzer (TCP 5000)",
        /// und die QR-Codes tragen ihn. Veränderbar ist er ausschließlich als <b>Testnaht</b>:
        /// <c>BuzzerHandshakeUnitTests</c> band vorher auf denselben Port wie eine laufende
        /// Anwendung und meldete deshalb die ganze Klasse mit <c>Assert.Inconclusive</c> ab,
        /// sobald ein Quizzer-Fenster offen war - elf Testfälle, die aussahen wie keine.
        /// </para>
        /// </summary>
        internal int BuzzerPort { get; set; } = 5000;

        private const string RuleName = "Quizzer Buzzer (TCP 5000)";

        private IFirewallReader? firewallReader;

        /// <summary>Woher die Firewall-Lage kommt. Tests setzen hier eine eigene Quelle ein.</summary>
        public IFirewallReader FirewallReader
        {
            get => firewallReader ??= new WindowsFirewallReader();
            set
            {
                firewallReader = value;
                RefreshFirewallHint();
            }
        }

        private FirewallVerdict firewallVerdict = FirewallVerdict.Ok();

        /// <summary>Was der Spielleiter zur Erreichbarkeit lesen soll; leer, wenn alles passt.</summary>
        public string FirewallHint => firewallVerdict.Hint;

        public Visibility FirewallHintVisibility =>
            firewallVerdict.NeedsAttention ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>
        /// Fragt die Firewall neu ab. Wird nach jedem Start und Stopp des Servers gerufen; ohne
        /// laufenden Server gibt es nichts zu warnen.
        /// </summary>
        public void RefreshFirewallHint()
        {
            firewallVerdict = IsBuzzerServerRunning ? AssessFirewall() : FirewallVerdict.Ok();
            NotifyFirewallChanged();
        }

        /// <summary>
        /// Nur fuer Tests: bewertet die Lage auch ohne laufenden Server. Im Programm gibt es
        /// nichts zu warnen, solange niemand verbinden will.
        /// </summary>
        internal void ForceFirewallCheckForTest()
        {
            firewallVerdict = AssessFirewall();
            NotifyFirewallChanged();
        }

        private void NotifyFirewallChanged()
        {
            OnPropertyChanged(nameof(FirewallHint));
            OnPropertyChanged(nameof(FirewallHintVisibility));
            allowFirewallCommand?.RaiseCanExecuteChanged();
        }

        private FirewallVerdict AssessFirewall()
        {
            var reader = FirewallReader;
            var rules = reader.ReadRules();

            if (!reader.IsAvailable)
            {
                return FirewallVerdict.Unknown(
                    "Die Windows-Firewall ließ sich nicht abfragen. Kommt kein Telefon durch, "
                    + $"muss Port {BuzzerPort} von Hand freigegeben werden.");
            }

            return FirewallAssessment.Assess(rules, reader.ActiveProfiles, Environment.ProcessPath, BuzzerPort);
        }

        private RelayCommand? allowFirewallCommand;

        public ICommand AllowFirewallCommand =>
            allowFirewallCommand ??= new RelayCommand(AllowFirewall, _ => firewallVerdict.NeedsAttention);

        /// <summary>
        /// Legt eine Freigabe fuer den Port an - ueber <c>netsh</c> mit Rechteabfrage, weil das
        /// Aendern der Firewall Verwalterrechte braucht.
        /// <para>
        /// Bewusst eine Portregel und keine Programmregel: der Programmpfad wechselt zwischen
        /// Debug und Release und mit jedem Ausgabeordner, und dann fehlt die Freigabe erneut.
        /// </para>
        /// </summary>
        private void AllowFirewall(object? commandParameter)
        {
            var argumente =
                $"advfirewall firewall add rule name=\"{RuleName}\" dir=in action=allow "
                + $"protocol=TCP localport={BuzzerPort} profile=any";

            try
            {
                var start = new ProcessStartInfo("netsh.exe", argumente)
                {
                    UseShellExecute = true,
                    Verb = "runas",
                };

                using var prozess = Process.Start(start);

                prozess?.WaitForExit();

                RefreshFirewallHint();

                if (firewallVerdict.NeedsAttention)
                {
                    UserPrompt.Inform(
                        "Die Freigabe ist nicht angekommen. Der Befehl lässt sich in einer "
                        + "Eingabeaufforderung als Administrator ausführen:"
                        + Environment.NewLine + Environment.NewLine + "netsh " + argumente,
                        ServerCaption);
                }
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                // 1223 = der Nutzer hat die Rechteabfrage abgebrochen. Das ist kein Fehler.
                UserPrompt.Inform(
                    "Die Freigabe wurde abgebrochen. Ohne sie erreichen die Telefone den "
                    + "Buzzer-Server nicht.", ServerCaption);
            }
            catch (Exception ex)
            {
                ExceptionManager.HandleException(ex);
            }
        }
    }
}
