using Quizzer.Base;
using System.Windows;
using System.Windows.Controls;

namespace Quizzer.Views
{
    /// <summary>
    /// Die Anmeldung. Wer leitet heute das Quiz - danach richtet sich, welche Fragen zur
    /// Verfuegung stehen.
    /// </summary>
    public partial class LoginView : WindowBase
    {
        public LoginView()
        {
            InitializeComponent();
        }

        /// <summary>Ob die Anmeldung gelungen ist.</summary>
        public bool SignedIn => (DataContext as LoginViewModel)?.SignedIn == true;

        /// <summary>
        /// Das Kennwort von Hand weiterreichen. <c>PasswordBox.Password</c> ist bewusst keine
        /// Abhaengigkeitseigenschaft und laesst sich deshalb nicht binden - ein gebundenes
        /// Kennwort staende im Arbeitsspeicher als Zeichenkette und im Bindungsprotokoll.
        /// </summary>
        private void Kennwortfeld_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is LoginViewModel vm && sender is PasswordBox feld)
                vm.Password = feld.Password;
        }
    }
}
