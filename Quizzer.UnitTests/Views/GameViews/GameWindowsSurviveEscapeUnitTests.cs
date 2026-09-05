using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.Views.BuzzerViews;
using Quizzer.Views.GameViews;
using System.Windows;
using System.Windows.Threading;

namespace Quizzer.UnitTests.Views.GameViews
{
    /// <summary>
    /// Die Escape-Taste schliesst jedes Fenster, das von <see cref="WindowBase"/> erbt. Fuer
    /// Dialoge ist das richtig - fuer die drei Fenster des laufenden Spiels waere es das Ende des
    /// Spielzugs: Spielfeld weg, Beamerbild weg, offene Frage weg, und das vor Publikum.
    /// </summary>
    [TestClass]
    public class GameWindowsSurviveEscapeUnitTests
    {
        /// <summary>
        /// Fuehrt die Arbeit auf dem gemeinsamen Oberflaechen-Thread aus.
        /// <para>
        /// Frueher legte diese Klasse einen eigenen STA-Thread an und fuhr dessen Dispatcher
        /// am Ende herunter. <c>Application.Current</c> ist prozessweit - danach fand keine
        /// spaetere Klasse mehr einen lebenden Dispatcher. Einzelheiten in
        /// <see cref="UiTestHost"/>.
        /// </para>
        /// </summary>
        private static void OnUiThread(Action action) => UiTestHost.Run(action);

        /// <summary>Die drei Fenster, die waehrend eines Spielzugs offen sind.</summary>
        [TestMethod]
        public void TheThreeGameWindowsIgnoreEscape()
        {
            OnUiThread(() =>
            {
                Assert.IsFalse(new GameMasterView().CloseOnEscape,
                    "Escape wuerde das Spielfeld samt Spielstand vom Bildschirm nehmen.");

                Assert.IsFalse(new GamePlayerView().CloseOnEscape,
                    "Escape wuerde den Beamerbildschirm der Mitspieler schliessen.");

                Assert.IsFalse(new QuestionMasterView().CloseOnEscape,
                    "Escape wuerde die laufende Frage beenden, ohne den Stand zu schreiben.");
            });
        }

        /// <summary>
        /// Die Gegenrichtung, und sie zaehlt genauso: Dialoge sollen sich mit Escape schliessen
        /// lassen. Ohne diese Probe waere ein pauschales Abschalten unbemerkt geblieben.
        /// </summary>
        [TestMethod]
        public void DialogsStillCloseOnEscape()
        {
            OnUiThread(() =>
            {
                Assert.IsTrue(new BuzzerServerView().CloseOnEscape,
                    "Das Buzzer-Fenster ist ein Nebenfenster und soll sich mit Escape schliessen.");

                Assert.IsTrue(new WindowBase().CloseOnEscape,
                    "Der Standard fuer Dialoge muss bleiben.");
            });
        }
    }
}
