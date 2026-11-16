using Quizzer.Base;
using System.Windows.Input;

namespace Quizzer.Views.GameViews
{
    /// <summary>Joker vergeben und zurücknehmen (M35 C7a) - in einem eigenen Dialog.</summary>
    public partial class GameMasterViewModel
    {
        private RelayCommand? jokerCommand;

        public ICommand JokerCommand => jokerCommand ??= new RelayCommand(_ =>
        {
            if (Game != null)
                JokerView.ShowFor(Game.Id, Window);
        });
    }
}
