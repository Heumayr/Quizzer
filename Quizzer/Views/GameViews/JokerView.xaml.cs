using Quizzer.Base;
using System.Windows;

namespace Quizzer.Views.GameViews
{
    public partial class JokerView : WindowBase
    {
        public JokerView()
        {
            InitializeComponent();
        }

        /// <summary>Zeigt die Joker eines Spiels als Dialog über dem Spielleiterfenster.</summary>
        public static void ShowFor(Guid gameId, Window? owner)
        {
            var window = new JokerView { Owner = owner };
            if (window.DataContext is JokerViewModel vm)
                vm.GameId = gameId;

            window.ShowDialog();
        }
    }
}
