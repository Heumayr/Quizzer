using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Views.StaticRessources;
using System.Windows;
using System.Windows.Threading;

namespace Quizzer.UnitTests.Views.StaticRessources
{
    /// <summary>
    /// Wohin eine gefangene Ausnahme laeuft. Im laufenden Programm baut die Behandlung ein
    /// Fenster - das geht nur auf dem Oberflaechen-Thread.
    /// <para>
    /// Die Buzzer-Rueckrufe kommen aus dem Kestrel-Thread des Hubs und sind <c>async void</c>.
    /// Wirft dort etwas, war der Weg ins Fehlerfenster bis 2026-09-05 der zweite Fehler nach dem
    /// ersten und riss die ganze Anwendung ab.
    /// </para>
    /// </summary>
    [TestClass]
    public class ExceptionManagerUnitTests
    {
        private static void WithRunningApplication(Action<Dispatcher> body)
        {
            body(EnsureApplication());
        }

        /// <summary>
        /// Liefert den lebenden Oberflaechen-Dispatcher aus <see cref="UiTestHost"/>.
        /// <para>
        /// Bis 2026-09-06 baute diese Klasse ihn selbst und stieg mit
        /// <c>Assert.Inconclusive</c> aus, wenn eine andere Testklasse den prozessweiten
        /// <c>Application.Current</c> schon heruntergefahren hatte. Das trat wirklich ein:
        /// eine einzige neue Testklasse liess diese beiden Tests lautlos ueberspringen.
        /// </para>
        /// </summary>
        private static Dispatcher EnsureApplication() => UiTestHost.Dispatcher;

        /// <summary>
        /// Der Kern: eine Ausnahme aus einem fremden Thread muss auf dem Oberflaechen-Thread
        /// behandelt werden. Sonst scheitert der Fensterbau dort, wo schon etwas schiefging.
        /// </summary>
        [TestMethod]
        public void AnExceptionFromAnotherThreadIsHandledOnTheUiThread()
        {
            WithRunningApplication(dispatcher =>
            {
                var vorheriger = ExceptionManager.Handler;

                try
                {
                    int? behandeltAuf = null;
                    var behandelt = new ManualResetEventSlim();

                    ExceptionManager.Handler = _ =>
                    {
                        behandeltAuf = Environment.CurrentManagedThreadId;
                        behandelt.Set();
                    };

                    var uiThreadId = dispatcher.Invoke(() => Environment.CurrentManagedThreadId);

                    // So kommt der Fehler im Programm an: aus einem fremden Thread, wie aus dem
                    // Kestrel-Thread des Buzzer-Hubs.
                    Task.Run(() => ExceptionManager.HandleException(new InvalidOperationException("Probe")));

                    Assert.IsTrue(behandelt.Wait(TimeSpan.FromSeconds(10)),
                        "Die Ausnahme wurde gar nicht behandelt.");

                    Assert.AreEqual(uiThreadId, behandeltAuf,
                        "Die Behandlung lief auf einem fremden Thread - dort scheitert der Fensterbau.");
                }
                finally
                {
                    ExceptionManager.Handler = vorheriger;
                }
            });
        }

        /// <summary>
        /// Die Gegenrichtung: wer schon auf dem Oberflaechen-Thread ist, soll nicht ueber den
        /// Dispatcher umgeleitet werden - das wuerde die Behandlung verzoegern.
        /// </summary>
        [TestMethod]
        public void AnExceptionOnTheUiThreadIsHandledRightAway()
        {
            WithRunningApplication(dispatcher =>
            {
                var vorheriger = ExceptionManager.Handler;

                try
                {
                    var sofortBehandelt = false;

                    ExceptionManager.Handler = _ => sofortBehandelt = true;

                    dispatcher.Invoke(() =>
                    {
                        ExceptionManager.HandleException(new InvalidOperationException("Probe"));

                        Assert.IsTrue(sofortBehandelt,
                            "Auf dem Oberflaechen-Thread muss die Behandlung ohne Umweg laufen.");
                    });
                }
                finally
                {
                    ExceptionManager.Handler = vorheriger;
                }
            });
        }
    }
}
