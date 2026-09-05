using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.DataModels;
using Quizzer.Logic.Context;
using Quizzer.Views.StaticRessources;

// Settings, UserPrompt, ExceptionManager.Handler, die Zufallsquelle und die Testdatenbank
// sind allesamt prozessweit. Parallele Testklassen wuerden einander den Boden wegziehen.
[assembly: DoNotParallelize]

namespace Quizzer.UnitTests
{
    /// <summary>
    /// Richtet den Testlauf so ein, dass ViewModels ohne laufende WPF-Anwendung arbeiten:
    /// Oberflaechenaufrufe laufen an Ort und Stelle, Rueckfragen werden beantwortet statt
    /// angezeigt, und Ausnahmen fallen durch, statt in einem Fenster zu landen.
    /// </summary>
    [TestClass]
    public static class TestEnvironment
    {
        public const string ConnectionString =
            @"Data Source=(localdb)\MSSQLLocalDB;Database=Quizzer_App_Tests;Integrated Security=True";

        /// <summary>Ausnahmen, die der ExceptionManager waehrend eines Tests aufgefangen hat.</summary>
        public static List<Exception> SwallowedExceptions { get; } = new();

        /// <summary>
        /// Controller, die mit offenen Aenderungen entsorgt wurden - also ohne
        /// <c>SaveChangesAsync</c>. Ein vergessener Aufruf verliert die Aenderung lautlos: es
        /// gibt keine Ausnahme, keine Meldung, und der naechste Bildschirmaufbau zeigt einfach
        /// wieder den alten Stand. Genau der Fall, den der Nutzer am 05.09. gemeldet hat.
        /// </summary>
        public static List<string> DiscardedChanges { get; } = new();

        [AssemblyInitialize]
        public static void Initialize(TestContext _)
        {
            Settings.ConnectionString = ConnectionString;
            Settings.FilePathQuizzer = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "QuizzerTestAssets");
            System.IO.Directory.CreateDirectory(Settings.FilePathQuizzer);

            ViewCommonBase.UiInvokerOverride = () => new InlineUiInvoker();

            ExceptionManager.Handler = ex =>
            {
                lock (SwallowedExceptions)
                {
                    SwallowedExceptions.Add(ex);
                }
            };

            Quizzer.Logic.Controller.UnsavedChangesWatch.Handler = (controller, anzahl) =>
            {
                lock (DiscardedChanges)
                {
                    DiscardedChanges.Add($"{controller}: {anzahl} Aenderungen");
                }
            };

            DatabaseInitializer.RecreateForTests();
        }

        [AssemblyCleanup]
        public static void Cleanup()
        {
            ViewCommonBase.UiInvokerOverride = null;
            ExceptionManager.ResetHandler();
            Quizzer.Logic.Controller.UnsavedChangesWatch.ResetHandler();
            UserPrompt.Reset();
        }

        /// <summary>
        /// Scheitert, wenn ein Controller mit offenen Aenderungen entsorgt wurde.
        /// </summary>
        public static void ThrowIfAnythingWasDiscarded()
        {
            lock (DiscardedChanges)
            {
                if (DiscardedChanges.Count == 0)
                    return;

                var verloren = string.Join(", ", DiscardedChanges);
                DiscardedChanges.Clear();

                throw new AssertFailedException(
                    "Ein Controller wurde ohne SaveChangesAsync entsorgt - die Aenderungen sind "
                    + $"verloren: {verloren}");
            }
        }

        /// <summary>
        /// Fuehrt einen Async-Befehl aus und wartet ihn ab.
        /// <para>
        /// <c>Execute</c> eines <c>AsyncRelayCommand</c> ist <c>async void</c> - ein Test, der
        /// danach zusichert, misst den Stand davor. Der Umweg ueber diesen Helfer haelt zugleich
        /// die Nullpruefung an einer Stelle; ein direkter Cast erzeugte CS8600/CS8602.
        /// </para>
        /// </summary>
        public static Task RunCommandAsync(System.Windows.Input.ICommand? command)
        {
            if (command is not AsyncRelayCommand asyncCommand)
                throw new AssertFailedException("Der erwartete Async-Befehl fehlt.");

            return asyncCommand.ExecuteAsync(null);
        }

        /// <summary>Vergisst gemeldete Verluste, wenn ein Test sie erwartet hat.</summary>
        public static void ClearDiscardedChanges()
        {
            lock (DiscardedChanges)
            {
                DiscardedChanges.Clear();
            }
        }

        /// <summary>
        /// Wirft die erste aufgefangene Ausnahme weiter. Ohne diesen Aufruf sieht ein Test
        /// gruen aus, obwohl im ViewModel etwas geworfen hat.
        /// </summary>
        public static void ThrowIfAnythingWasSwallowed()
        {
            lock (SwallowedExceptions)
            {
                if (SwallowedExceptions.Count == 0)
                    return;

                var first = SwallowedExceptions[0];
                SwallowedExceptions.Clear();

                throw new AssertFailedException(
                    $"Im ViewModel wurde eine Ausnahme aufgefangen: {first.Message}", first);
            }
        }

        /// <summary>Verwirft aufgefangene Ausnahmen, wenn ein Test sie erwartet hat.</summary>
        public static void ClearSwallowedExceptions()
        {
            lock (SwallowedExceptions)
            {
                SwallowedExceptions.Clear();
            }
        }
    }
}
