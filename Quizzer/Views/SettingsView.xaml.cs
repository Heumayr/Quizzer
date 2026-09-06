using Quizzer.Base;
using System.Windows;
using System.Windows.Controls;

namespace Quizzer.Views
{
    /// <summary>
    /// Die Einstellungsmaske. Auch vor dem Hauptfenster erreichbar - siehe
    /// <see cref="SettingsViewModel"/>.
    /// </summary>
    public partial class SettingsView : WindowBase
    {
        public SettingsView()
        {
            InitializeComponent();
        }

        /// <summary>Ob gespeichert wurde.</summary>
        public bool Saved => (DataContext as SettingsViewModel)?.Saved == true;

        /// <summary>
        /// Das Kennwort von Hand weiterreichen - derselbe Weg wie in <see cref="LoginView"/>.
        /// <c>PasswordBox.Password</c> ist bewusst keine Abhaengigkeitseigenschaft und laesst sich
        /// deshalb nicht binden.
        /// </summary>
        private void Kennwortfeld_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is SettingsViewModel vm && sender is PasswordBox feld)
                vm.Kennwort = feld.Password;
        }
    }
}
