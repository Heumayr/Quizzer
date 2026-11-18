using Quizzer.Views.GameViews;
using System.Windows;

namespace Quizzer.UnitTests.Views
{
    /// <summary>
    /// Erzeugt ein Fenster und schließt es wieder - auch wenn dazwischen etwas schiefgeht.
    /// <para>
    /// <b>Warum es das braucht.</b> WPF trägt jedes erzeugte <see cref="Window"/> in
    /// <c>Application.Windows</c> ein, und der Oberflächen-Thread aus <c>UiTestHost</c> lebt bis
    /// zum Prozessende. Ein Test, der zwanzig Fenster baut und keines schließt, hinterlässt sie
    /// also dem gesamten restlichen Testlauf.
    /// </para>
    /// <para>
    /// <b>Gemessen am 2026-09-06:</b> ein <c>LoginView</c> stand während des ganzen Laufs offen.
    /// Aufgefallen ist es erst, als eine neue Zusicherung wissen musste, ob ihr Fenster das
    /// <b>letzte</b> ist - sie konnte diesen Zustand gar nicht mehr herstellen. Ein liegen
    /// gebliebenes Fenster macht keinen Test rot; es nimmt späteren nur die Messbarkeit.
    /// </para>
    /// </summary>
    internal sealed class FensterAufraeumer : IDisposable
    {
        /// <summary>
        /// Fenster, deren XAML kein ViewModel mehr mitbringt (P10-2, P11-2): der Öffner reicht es herein, hier ebenso - sonst
        /// prüften die Fensterlisten ihre Bindungen gegen nichts, und eine tote bliebe grün.
        /// </summary>
        internal static readonly Dictionary<Type, Func<object>> Hereingereicht = new()
        {
            [typeof(PlayersResultView)] = () => new PlayersResultViewModel(),
            [typeof(QuestionMasterView)] = () => new CurrentQuestionViewModel(),
        };

        public FensterAufraeumer(Type fenstertyp)
        {
            Fenster = (Window)Activator.CreateInstance(fenstertyp)!;

            if (!Hereingereicht.TryGetValue(fenstertyp, out var viewModel))
                return;

            Fenster.DataContext = viewModel();

            // Nachträglich gesetzt hängen sich die Bindungen verzögert an. Ohne das legte die Messung eine Fläche aus, die erst
            // danach einklappt, und ihre Kinder behielten die alte Breite (gemessen: die Minispiel-Steuerung im Fragefenster).
            Fenster.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);
        }

        /// <summary>Das erzeugte Fenster.</summary>
        public Window Fenster { get; }

        public void Dispose()
        {
            try
            {
                Fenster.Close();
            }
            catch (InvalidOperationException)
            {
                // Ein Fenster, das nie gezeigt wurde, laesst sich in manchen Zustaenden nicht
                // schliessen. Das ist kein Fehler des Tests - und ein Wurf hier wuerde den
                // eigentlichen Befund verdecken.
            }
        }
    }
}
