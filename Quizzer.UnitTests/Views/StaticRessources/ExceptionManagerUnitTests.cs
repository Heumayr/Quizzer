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
        /// <summary>
        /// Faehrt eine echte WPF-Anwendung auf einem eigenen STA-Thread hoch und laesst die
        /// Nachrichtenschleife laufen, damit ein Sprung auf den Dispatcher auch ankommt.
        /// <para>
        /// <see cref="Application.Current"/> ist prozessweit und laesst sich nur einmal je Prozess
        /// setzen. Steht schon eine (etwa aus einem Aufbau-Test derselben Assembly), wird deren
        /// Dispatcher benutzt statt einer zweiten Anwendung.
        /// </para>
        /// </summary>
        private static Dispatcher? laufenderDispatcher;

        private static void WithRunningApplication(Action<Dispatcher> body)
        {
            body(EnsureApplication());
        }

        /// <summary>
        /// Liefert einen lebenden Oberflaechen-Dispatcher. Die Anwendung wird bewusst nie
        /// heruntergefahren: <see cref="Application.Current"/> bliebe danach gesetzt, ihr
        /// Dispatcher aber tot, und jeder folgende Test liefe in eine abgebrochene Aufgabe.
        /// Der Thread laeuft im Hintergrund und endet mit dem Prozess.
        /// </summary>
        private static Dispatcher EnsureApplication()
        {
            if (laufenderDispatcher is { HasShutdownStarted: false })
                return laufenderDispatcher;

            var vorhanden = Application.Current;

            if (vorhanden != null)
            {
                if (vorhanden.Dispatcher.HasShutdownStarted)
                {
                    Assert.Inconclusive(
                        "Eine fremde WPF-Anwendung im selben Prozess ist schon heruntergefahren - "
                        + "ihr Dispatcher nimmt nichts mehr an. Diese Klasse einzeln ausfuehren.");
                }

                laufenderDispatcher = vorhanden.Dispatcher;
                return laufenderDispatcher;
            }

            Exception? fehler = null;
            Dispatcher? dispatcher = null;
            var bereit = new ManualResetEventSlim();

            var thread = new Thread(() =>
            {
                try
                {
                    var app = new Quizzer.App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                    app.InitializeComponent();

                    dispatcher = Dispatcher.CurrentDispatcher;
                    bereit.Set();

                    Dispatcher.Run();
                }
                catch (Exception ex)
                {
                    fehler = ex;
                    bereit.Set();
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();

            Assert.IsTrue(bereit.Wait(TimeSpan.FromSeconds(30)),
                "Die Anwendung ist nicht hochgekommen.");

            if (fehler != null)
                throw new AssertFailedException($"Die Anwendung liess sich nicht starten: {fehler.Message}", fehler);

            laufenderDispatcher = dispatcher;
            return dispatcher!;
        }

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
