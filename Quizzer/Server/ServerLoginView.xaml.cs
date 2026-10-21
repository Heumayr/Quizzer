using Quizzer.Base;
using System.Windows;
using System.Windows.Controls;

namespace Quizzer.Server
{
    public partial class ServerLoginView : WindowBase
    {
        public ServerLoginView()
        {
            InitializeComponent();
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
