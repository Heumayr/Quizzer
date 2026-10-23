using Quizzer.Base;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Quizzer.Server
{
    public partial class ServerLoginView : WindowBase
    {
        public ServerLoginView()
        {
            InitializeComponent();

            // M28: angemeldet wird jedes Mal, und der Name ist meist vorbelegt - dann gleich ins
            // Kennwortfeld. Mit Input-Vorrang: erst muss die Bindung den Namen eingetragen haben.
            Loaded += (_, _) => Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
            {
                if (string.IsNullOrEmpty(Namensfeld.Text))
                    Namensfeld.Focus();
                else
                    Kennwortfeld.Focus();
            });
        }

        public bool SignedIn => (DataContext as ServerLoginViewModel)?.SignedIn == true;

        // Ein PasswordBox laesst sich aus Sicherheitsgruenden nicht binden; das Kennwort geht
        // nur in das ViewModel und von dort einmal zum Server.
        private void Kennwortfeld_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is ServerLoginViewModel vm && sender is PasswordBox feld)
                vm.Password = feld.Password;
        }
    }
}
