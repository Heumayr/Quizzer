using Quizzer.Base;
using Quizzer.Views.BuzzerViews;
using Quizzer.Views.StaticRessources;
using System.Windows.Input;

namespace Quizzer.Views.GameViews
{
    /// <summary>Das Fenster des Buzzer-Servers.</summary>
    public partial class GameMasterViewModel
    {
        private RelayCommand? buzzerServerCommand;
        public ICommand BuzzerServerCommand => buzzerServerCommand ??= new RelayCommand(BuzzerServer);

        private void BuzzerServer(object? commandParameter)
        {
            if (buzzerServerView != null)
            {
                buzzerServerView.Close();
                buzzerServerView = null;
            }

            buzzerServerView = new BuzzerServerView();

            buzzerServerView.DataContext = StaticManager.BuzzerServerViewModel;
            buzzerServerView.Show();
        }
    }
}
