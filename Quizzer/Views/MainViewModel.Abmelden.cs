using Quizzer.Base;
using System.Windows.Input;

namespace Quizzer.Views
{
    /// <summary>Abmelden vom Quizzer-Server (M28).</summary>
    public partial class MainViewModel
    {
        private AsyncRelayCommand? logoutCommand;

        /// <summary>
        /// Abmelden und zurück ins Anmeldefenster - dort geht auch ein anderes Konto.
        /// <para>
        /// <b>Nutzerwort vom 2026-10-23 (M28):</b> „fix an und Abmeldung sinnvolles handling im der
        /// wpf". Bis dahin gab es keinen Weg vom einmal angemeldeten Konto weg.
        /// </para>
        /// <para>
        /// Vorher geht Offenes zum Server; was danach noch liegt, nennt die Rückfrage. Verloren
        /// geht es nicht - die Zwischendatenbank bleibt, die nächste Anmeldung desselben Kontos
        /// schickt es.
        /// </para>
        /// </summary>
        public ICommand LogoutCommand => logoutCommand ??= new AsyncRelayCommand(async _ =>
        {
            var offen = await Server.ServerSession.FlushAsync();

            var frage = "Abmelden und neu starten? Offene Fenster werden dabei geschlossen.";
            if (Server.ServerSession.IsOffline)
            {
                frage += Environment.NewLine + Environment.NewLine
                    + "Du arbeitest ohne Server: was seit der letzten Verbindung gespeichert wurde, bleibt auf "
                    + "diesem Rechner und geht erst bei der nächsten Anmeldung mit diesem Konto hinaus.";
            }
            else if (offen > 0)
            {
                frage += Environment.NewLine + Environment.NewLine
                    + $"{offen} Änderung(en) sind noch nicht beim Server. Sie bleiben auf diesem Rechner und gehen "
                    + "bei der nächsten Anmeldung mit diesem Konto hinaus.";
            }

            if (!UserPrompt.Confirm(frage, "Abmelden"))
                return;

            await Server.ServerSession.SignOutAsync();
            AppRestart.Restart();
        }, _ => Server.ServerSession.IsActive);
    }
}
