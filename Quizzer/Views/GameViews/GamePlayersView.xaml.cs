using Quizzer.Base;

namespace Quizzer.Views.GameViews
{
    public partial class GamePlayersView : WindowBase
    {
        public GamePlayersView()
        {
            InitializeComponent();
        }

        public bool Confirmed => (DataContext as GamePlayersViewModel)?.Confirmed == true;

        /// <summary>Zeigt den Dialog fuer ein Spiel. <c>true</c>, wenn gestartet werden soll.</summary>
        public static bool Ask(Guid gameId)
        {
            var window = new GamePlayersView();
            if (window.DataContext is GamePlayersViewModel vm)
                vm.GameId = gameId;

            window.ShowDialog();
            return window.Confirmed;
        }
    }
}
