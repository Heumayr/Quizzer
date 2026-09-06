using Quizzer.Base;

namespace Quizzer.Views
{
    /// <summary>Die Mitspielerauswahl beim Import.</summary>
    public partial class ImportPlayersView : WindowBase
    {
        public ImportPlayersView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Die gewaehlten Mitspieler - <c>null</c>, wenn abgebrochen wurde. Eine leere Liste
        /// heisst ausdruecklich "keine Mitspieler".
        /// </summary>
        public IReadOnlyList<Guid>? Gewaehlt => (DataContext as ImportPlayersViewModel)?.Ergebnis;
    }
}
