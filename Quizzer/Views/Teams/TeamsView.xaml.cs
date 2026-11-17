using Quizzer.Base;
using Quizzer.DataModels.Models.Base;
using System.Windows;

namespace Quizzer.Views.Teams
{
    public partial class TeamsView : WindowBase
    {
        public TeamsView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Zeigt den Dialog für ein geladenes Spiel. <c>true</c>, wenn übernommen wurde - das Spiel trägt dann den frischen
        /// Stand (Teams, Zuteilung, RowVersion). <paramref name="guard"/> klammert das Schreiben, etwa mit der Sperre des
        /// Spielaufbaus.
        /// </summary>
        public static bool ShowFor(Game spiel, Window? owner, string titel, Func<Func<Task>, Task>? guard = null)
        {
            var window = new TeamsView { Owner = owner };

            if (window.DataContext is not TeamsViewModel vm)
                return false;

            vm.Lade(spiel, titel);

            if (guard != null)
                vm.Guard = guard;

            window.ShowDialog();
            return vm.Confirmed;
        }
    }
}
