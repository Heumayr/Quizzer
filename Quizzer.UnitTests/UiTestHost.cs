using System.Windows;
using System.Windows.Threading;

namespace Quizzer.UnitTests
{
    /// <summary>
    /// Ein einziger Oberflaechen-Thread fuer alle WPF-Tests, der bis zum Prozessende lebt.
    /// <para>
    /// Vorher legte jede Testklasse ihren eigenen STA-Thread an, erzeugte darin eine
    /// <c>Quizzer.App</c> und fuhr den Dispatcher am Ende herunter. <c>Application.Current</c>
    /// ist prozessweit: nach dem ersten Herunterfahren blieb sie gesetzt, ihr Dispatcher aber
    /// tot. Wer danach einen lebenden brauchte, fand keinen mehr.
    /// </para>
    /// <para>
    /// <b>Gemessen am 2026-09-06:</b> das Hinzufuegen einer einzigen Testklasse liess
    /// <c>ExceptionManagerUnitTests</c> zwei Tests mit <c>Assert.Inconclusive</c> ueberspringen -
    /// lautlos, im Testbericht nur als "uebersprungen: 2". Vorher lief es nur deshalb durch, weil
    /// die Reihenfolge zufaellig passte. Ein Test, der sich selbst abmeldet, ist schlimmer als
    /// ein roter: er sieht aus wie keiner.
    /// </para>
    /// <para>
    /// Der Thread wird bewusst nie heruntergefahren. Er laeuft im Hintergrund und endet mit dem
    /// Prozess.
    /// </para>
    /// </summary>
    public static class UiTestHost
    {
        private static readonly object Gate = new();
        private static Dispatcher? dispatcher;

        /// <summary>Der lebende Oberflaechen-Dispatcher. Startet ihn beim ersten Zugriff.</summary>
        public static Dispatcher Dispatcher
        {
            get
            {
                lock (Gate)
                {
                    if (dispatcher is { HasShutdownStarted: false })
                        return dispatcher;

                    dispatcher = Start();

                    return dispatcher;
                }
            }
        }

        private static Dispatcher Start()
        {
            Exception? fehler = null;
            Dispatcher? erzeugt = null;
            var bereit = new ManualResetEventSlim();

            var thread = new Thread(() =>
            {
                try
                {
                    if (Application.Current == null)
                    {
                        var app = new Quizzer.App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                        app.InitializeComponent();
                    }

                    erzeugt = Dispatcher.CurrentDispatcher;
                    bereit.Set();

                    Dispatcher.Run();
                }
                catch (Exception ex)
                {
                    fehler = ex;
                    bereit.Set();
                }
            })
            {
                IsBackground = true,
            };

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();

            if (!bereit.Wait(TimeSpan.FromSeconds(60)))
                throw new InvalidOperationException("Der Oberflaechen-Thread ist nicht hochgekommen.");

            if (fehler != null)
                throw new InvalidOperationException(
                    $"Der Oberflaechen-Thread liess sich nicht starten: {fehler.Message}", fehler);

            return erzeugt!;
        }

        /// <summary>
        /// Fuehrt die Arbeit auf dem Oberflaechen-Thread aus und wartet sie ab. Eine Ausnahme
        /// darin kommt beim Aufrufer an, nicht im Nichts.
        /// </summary>
        public static void Run(Action action)
        {
            var ziel = Dispatcher;

            // Wer schon auf dem Oberflaechen-Thread ist, soll nicht auf sich selbst warten.
            if (ziel.CheckAccess())
            {
                action();
                return;
            }

            ziel.Invoke(action, DispatcherPriority.Normal);
        }
    }
}
