using Quizzer.Base;
using System.Windows;

namespace Quizzer.Server
{
    public partial class AccountsView : WindowBase
    {
        public AccountsView()
        {
            InitializeComponent();
        }

        private void NewChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is AccountsViewModel vm)
                vm.NewPassword = NeuesKennwort.Password;
        }

        private void ResetChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is AccountsViewModel vm)
                vm.ResetPassword = Zuruecksetzen.Password;
        }
    }
}
