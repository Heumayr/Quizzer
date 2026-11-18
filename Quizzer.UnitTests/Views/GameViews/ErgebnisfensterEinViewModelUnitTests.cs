using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.UnitTests.PlayThrough;
using Quizzer.Views.GameViews;
using System.Windows.Threading;

namespace Quizzer.UnitTests.Views.GameViews
{
    /// <summary>
    /// P10-2 (Durchsicht M37, P11): das Bewertungsfenster legt kein eigenes <see cref="PlayersResultViewModel"/> an. Vorher stand
    /// eines als DataContext im XAML; das übergebene ersetzte es für die Bindungen, aber das erste hing weiter an <c>Loaded</c>
    /// und <c>Closed</c> - <c>OnClosed</c> lief zweimal, das zweite Mal auf einer leeren Kartenliste.
    /// </summary>
    [TestClass]
    public class ErgebnisfensterEinViewModelUnitTests
    {
        private TestGameBuilder world = null!;

        [TestInitialize]
        public async Task SetUp()
        {
            UserPrompt.Current = new RecordingUserPrompt(answer: true);
            TestEnvironment.ClearSwallowedExceptions();

            world = await TestGameBuilder.CreateAsync(QuestionType.Default, normalStepCount: 1, playerCount: 2);
        }

        [TestCleanup]
        public async Task TearDown()
        {
            await world.DisposeAsync();
            UserPrompt.Reset();
        }

        [TestMethod]
        public async Task OnlyTheGivenViewModelServesAndHearsTheWindow()
        {
            var frage = new TestableCurrentQuestionViewModel { Coordinate = world.Coordinate };
            await frage.LoadForTestAsync();
            var vm = frage.PlayersResultViewModel!;

            object? vorher = null;
            var zweitesHaengtAmFenster = false;
            string? titel = null;

            UiTestHost.Run(() =>
            {
                var fenster = new PlayersResultView();
                fenster.DataContextChanged += (_, e) => vorher = e.OldValue;
                fenster.DataContext = vm;
                fenster.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle); // die Bindungen lösen sich verzögert auf

                titel = fenster.Title;
                zweitesHaengtAmFenster = vorher is ViewModelBase zweites && ReferenceEquals(zweites.Window, fenster);
            });

            Assert.AreNotEqual("Bewerten", vm.WindowTitle, "Ohne Frage im Titel unterscheiden sich die beiden nicht - dann misst der Test nichts.");
            Assert.AreEqual(vm.WindowTitle, titel, "Die Bindungen bedient nicht das übergebene ViewModel.");
            Assert.IsFalse(zweitesHaengtAmFenster, "Ein zweites ViewModel hängt an Loaded und Closed des Fensters.");
            Assert.IsNull(vorher, $"Das Fenster legt selbst ein {vorher?.GetType().Name} an - es bleibt nicht genau eines.");
        }
    }
}
