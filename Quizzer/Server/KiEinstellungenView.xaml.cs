using Quizzer.Base;
using System.Windows;

namespace Quizzer.Server
{
    public partial class KiEinstellungenView : WindowBase
    {
        public KiEinstellungenView()
        {
            InitializeComponent();
        }

        private void SchluesselGeaendert(object sender, RoutedEventArgs e)
        {
            if (DataContext is KiEinstellungenViewModel vm)
                vm.NeuerSchluessel = Schluessel.Password;
        }
    }
}
