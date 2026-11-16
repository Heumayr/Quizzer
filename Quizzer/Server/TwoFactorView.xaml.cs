using Quizzer.Base;
using System.Windows;

namespace Quizzer.Server
{
    public partial class TwoFactorView : WindowBase
    {
        public TwoFactorView()
        {
            InitializeComponent();
        }

        public bool Activated => (DataContext as TwoFactorViewModel)?.Activated == true;

        // Ein PasswordBox laesst sich nicht binden; das Kennwort geht nur ins ViewModel.
        private void Kennwort_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is TwoFactorViewModel vm)
                vm.Password = Kennwort.Password;
        }
    }
}
