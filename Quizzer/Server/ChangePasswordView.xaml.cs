using Quizzer.Base;
using System.Windows;

namespace Quizzer.Server
{
    public partial class ChangePasswordView : WindowBase
    {
        public ChangePasswordView()
        {
            InitializeComponent();
        }

        private void Changed(object sender, RoutedEventArgs e)
        {
            if (DataContext is not ChangePasswordViewModel vm)
                return;

            vm.Current = Bisher.Password;
            vm.Next = Neu.Password;
            vm.Repeat = Wiederholt.Password;
        }
    }
}
