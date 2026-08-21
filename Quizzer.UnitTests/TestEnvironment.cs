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

            DatabaseInitializer.RecreateForTests();
        }

        [AssemblyCleanup]
        public static void Cleanup()
        {
            ViewCommonBase.UiInvokerOverride = null;
            ExceptionManager.ResetHandler();
            UserPrompt.Reset();
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
