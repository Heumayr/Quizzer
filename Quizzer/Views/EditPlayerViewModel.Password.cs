using Quizzer.Base;
using Quizzer.DataModels;
using System.Windows.Controls;
using System.Windows.Input;

namespace Quizzer.Views
{
    /// <summary>
    /// Das Kennwort eines Spielleiters.
    /// <para>
    /// <b>Vorgesehen, nicht verlangt.</b> Wo keines gesetzt ist, genügt bei der Anmeldung die
    /// Auswahl. Wer eines setzt, wird ab dann danach gefragt.
    /// </para>
    /// <para>
    /// Gespeichert wird nie das Kennwort selbst, sondern eine PBKDF2-Ableitung mit eigenem Salz
    /// (<see cref="Session.HashPassword"/>). Ein Blick in die Datenbank gibt damit kein Kennwort
    /// preis.
    /// </para>
    /// </summary>
    public partial class EditPlayerViewModel
    {
        /// <summary>Im Klartext, ob ein Kennwort gesetzt ist - ohne es zu zeigen.</summary>
        public string PasswordState =>
            Session.RequiresPassword(Player) ? "Kennwort gesetzt" : "Kein Kennwort";

        private RelayCommand? setPasswordCommand;

        /// <summary>
        /// Übernimmt das Kennwort aus dem Feld. Der Parameter ist die <c>PasswordBox</c> selbst:
        /// <c>PasswordBox.Password</c> ist bewusst keine Abhängigkeitseigenschaft und lässt sich
        /// nicht binden.
        /// </summary>
        public ICommand SetPasswordCommand => setPasswordCommand ??= new RelayCommand(Setzen);

        private void Setzen(object? parameter)
        {
            if (Player == null || parameter is not PasswordBox feld)
                return;

            if (string.IsNullOrEmpty(feld.Password))
            {
                UserPrompt.Inform("Es steht kein Kennwort im Feld.", "Kennwort setzen");
                return;
            }

            Player.PasswordHash = Session.HashPassword(feld.Password);

            feld.Clear();

            OnPropertyChanged(nameof(PasswordState));

            UserPrompt.Inform(
                "Das Kennwort ist gesetzt. Es gilt, sobald der Mitspieler gespeichert ist.",
                "Kennwort setzen");
        }

        private RelayCommand? clearPasswordCommand;

        public ICommand ClearPasswordCommand => clearPasswordCommand ??= new RelayCommand(Entfernen);

        private void Entfernen(object? _)
        {
            if (Player == null || !Session.RequiresPassword(Player))
                return;

            if (!UserPrompt.Confirm(
                    "Das Kennwort entfernen? Danach genügt bei der Anmeldung die Auswahl.",
                    "Kennwort entfernen"))
                return;

            Player.PasswordHash = string.Empty;

            OnPropertyChanged(nameof(PasswordState));
        }
    }
}
