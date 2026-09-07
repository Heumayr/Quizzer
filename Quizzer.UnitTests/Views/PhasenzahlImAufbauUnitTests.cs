using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.Logic.Controller.TypedControllers;
using Quizzer.UnitTests.PlayThrough;
using Quizzer.Views;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace Quizzer.UnitTests.Views
{
    /// <summary>
    /// Wie viele Phasen ein Abend hat — einstellbar im Spielaufbau.
    /// <para>
    /// <b>Gemessen 2026-09-07: dafür gab es kein Feld.</b> Jedes neue Spiel bekam drei Phasen und
    /// behielt sie für immer; wer zwei wollte oder gar keine Steigerung, kam nur über die
    /// Datenbank heran. Dabei entscheidet die Zahl, <b>wie oft</b> das Spiel mitten im Abend nach
    /// dem Phasenwechsel fragt und damit die Punkte hochsetzt.
    /// </para>
    /// </summary>
    [TestClass]
    public class PhasenzahlImAufbauUnitTests
    {
        private TestGameBuilder world = null!;

        [TestInitialize]
        public async Task SetUp()
        {
            UserPrompt.Current = new RecordingUserPrompt(answer: true);

            world = await TestGameBuilder.CreateAsync(QuestionType.Default, normalStepCount: 1);
        }

        [TestCleanup]
        public async Task TearDown()
        {
            await world.DisposeAsync();
            UserPrompt.Reset();
        }

        private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
        {
            var count = VisualTreeHelper.GetChildrenCount(root);

            for (var i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);

                if (child is T hit)
                    yield return hit;

                foreach (var deeper in Descendants<T>(child))
                    yield return deeper;
            }
        }

        /// <summary>
        /// <b>Die Maske hat ein Feld dafür.</b> Genau das fehlte — die Eigenschaft allein nützt
        /// nichts, wenn sie in keiner Maske steht.
        /// </summary>
        [TestMethod]
        public void TheMaskOffersAFieldForTheNumberOfPhases()
        {
            var pfade = new List<string>();

            UiTestHost.Run(() =>
            {
                var fenster = new EditGameView();
                var inhalt = (FrameworkElement)fenster.Content;

                inhalt.Measure(new Size(1200, 800));
                inhalt.Arrange(new Rect(0, 0, 1200, 800));
                inhalt.UpdateLayout();

                foreach (var feld in Descendants<TextBox>(inhalt))
                {
                    var bindung = BindingOperations.GetBinding(feld, TextBox.TextProperty);

                    if (bindung?.Path?.Path is { Length: > 0 } pfad)
                        pfade.Add(pfad);
                }

                fenster.Close();
            });

            // Die Zaehlschranke misst mit: findet der Baumlauf gar keine Felder, waere die
            // Zusicherung sonst aus dem falschen Grund rot - und die Meldung fuehrte in die Irre.
            Assert.IsTrue(pfade.Count >= 8,
                "Der Baumlauf hat kaum Eingabefelder gefunden - dann misst diese Zusicherung "
                + "nicht die Maske, sondern sich selbst. Gefunden: " + string.Join(", ", pfade));

            CollectionAssert.Contains(pfade, nameof(EditGameViewModel.SuggestedPhases),
                "Im Spielaufbau gibt es kein Feld fuer die Zahl der Phasen. Der Spielleiter kann "
                + "sie damit nur ueber die Datenbank aendern. Gefunden wurde: "
                + string.Join(", ", pfade));
        }

        /// <summary>
        /// <b>Die eingestellte Zahl landet in der Datenbank.</b> Ein Feld, das nur die Maske
        /// ändert, wäre schlimmer als keines.
        /// </summary>
        [TestMethod]
        public async Task TheNumberOfPhasesReachesTheDatabase()
        {
            var vm = new EditGameViewModel();

            await vm.LoadModel(world.Game.Id);

            Assert.AreNotEqual(2, vm.SuggestedPhases,
                "Das Testspiel steht schon auf dem Wert, der gleich gesetzt wird - dann misst "
                + "die Zusicherung nichts.");

            vm.SuggestedPhases = 2;

            await vm.VMSaveAsync();

            using var ctrl = new GamesController();

            var nachher = await ctrl.GetAsync(world.Game.Id);

            Assert.AreEqual(2, nachher!.SuggestedPhases,
                "Die Zahl der Phasen steht nur in der Maske, nicht in der Datenbank.");
        }

        /// <summary>
        /// <b>Unter 1 wird auf 1 gehoben.</b> Eine 0 hieße „keine Schwellen" — das sieht harmlos
        /// aus, ergibt in der Kopfzeile des Spielleiters aber „Phase 1 von 0".
        /// </summary>
        [TestMethod]
        public async Task TheNumberOfPhasesNeverDropsBelowOne()
        {
            var vm = new EditGameViewModel();

            await vm.LoadModel(world.Game.Id);

            // Die Gegenrichtung ZUERST, und das ist hier wesentlich: das Testspiel steht schon
            // auf 1. Wer mit "= 0" anfängt, misst nur, dass sich nichts geaendert hat - die
            // Zusicherung waere auch gruen, wenn der Setzer gar nichts taete. Gemessen
            // 2026-09-07 an genau diesem Aufbau.
            vm.SuggestedPhases = 4;

            Assert.AreEqual(4, vm.SuggestedPhases, "Eine echte Phasenzahl kommt nicht an.");

            vm.SuggestedPhases = 0;

            Assert.AreEqual(1, vm.SuggestedPhases, "Eine 0 kommt durch.");

            vm.SuggestedPhases = 4;
            vm.SuggestedPhases = -3;

            Assert.AreEqual(1, vm.SuggestedPhases, "Ein negativer Wert kommt durch.");
        }
    }
}
