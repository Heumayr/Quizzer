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
                        ErzeugeAnwendungOhneStart();

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
        /// Erzeugt die WPF-Anwendung fuer den Testlauf - <b>ohne</b> die Startlogik der echten
        /// Anwendung.
        /// <para>
        /// <b>Gemessen am 2026-09-06, und es war eine Ueberraschung.</b> Der Konstruktor von
        /// <see cref="Application"/> stellt die <c>Startup</c>-Nachricht selbst in die
        /// Warteschlange des Dispatchers; das <c>Dispatcher.Run()</c> weiter oben hat sie dann
        /// abgearbeitet. Wer hier ein <c>Quizzer.App</c> erzeugte, liess also
        /// <c>App.OnStartup</c> laufen - mit allem, was daranhaengt:
        /// </para>
        /// <list type="bullet">
        ///   <item><description><c>Settings.LoadSettings()</c> ueberschrieb mitten im Lauf die
        ///   Verbindungszeichenfolge, die eine Zusicherung gerade gesetzt hatte</description></item>
        ///   <item><description>die Datenbank wurde auf den Migrationsstand gezogen</description></item>
        ///   <item><description>und das <b>Anmeldefenster stand modal offen</b>, den ganzen
        ///   Testlauf lang - alle Zusicherungen liefen in dessen verschachtelter
        ///   Dispatcher-Schleife</description></item>
        /// </list>
        /// <para>
        /// Aufgefallen ist es erst, als eine Zusicherung wissen musste, ob ihr Fenster das
        /// <b>letzte</b> ist. Nichts davon hat je einen Test rot gemacht.
        /// </para>
        /// <para>
        /// Deshalb eine schlichte <see cref="Application"/> - deren <c>OnStartup</c> tut nichts -
        /// und die Gestaltung aus <c>App.xaml</c> von Hand hineingeladen. Genau das macht das
        /// erzeugte <c>InitializeComponent</c> auch, nur eben ohne die Startlogik.
        /// </para>
        /// </summary>
        private static void ErzeugeAnwendungOhneStart()
        {
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };

            // Dieselbe Gestaltung, die App.xaml einbindet. Ein LoadComponent auf App.xaml selbst
            // ginge nicht: die Datei traegt x:Class="Quizzer.App", und dann verlangt WPF genau
            // diesen Typ - womit die Startlogik wieder mitkaeme.
            app.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/Quizzer;component/Styles/AppResources.xaml"),
            });
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
