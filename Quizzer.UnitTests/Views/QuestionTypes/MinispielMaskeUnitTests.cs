using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Questions.Minispiele;
using Quizzer.Views.QuestionTypes.Typed;
using System.Windows;

namespace Quizzer.UnitTests.Views.QuestionTypes
{
    /// <summary>
    /// Die Maske des Minispiels (M35 B2): Art und Parameter ueber den Optionen, und eine frisch
    /// ausgefuellte Mehrheit laesst sich speichern.
    /// </summary>
    [TestClass]
    public class MinispielMaskeUnitTests
    {
        private static (Quizzer.Views.QuestionTypes.EditQuestionViewModel Vm, MinispielViewModel Maske) Neu()
        {
            var vm = ZeileneditorUnitTests.Mit(QuestionType.Minigame, f =>
            {
                f.DesignationShort = "M";
                f.CategoryId = Guid.NewGuid();
            });

            Assert.IsInstanceOfType<MinispielViewModel>(vm.Zeileneditor, "Das Minispiel bekommt nicht seine Maske.");

            return (vm, (MinispielViewModel)vm.Zeileneditor);
        }

        /// <summary>Die Maske zeigt Art und Spielweise - und schreibt sie in die Frage.</summary>
        [TestMethod]
        public void TheMaskShowsKindAndVariantAndWritesThem()
        {
            var (vm, maske) = Neu();

            Assert.AreSame(maske, maske.Zusatzmaske, "Ohne Zusatzmaske bleibt der Block für Art und Parameter leer.");
            Assert.AreEqual(MinigameKind.Majority, maske.Art?.Kind);
            Assert.AreEqual(Visibility.Visible, maske.VarianteVisibility, "Die Spielweise „Minderheit“ ist nicht wählbar.");

            maske.Variante = MehrheitEvaluator.MinorityVariant;

            Assert.AreEqual(MehrheitEvaluator.MinorityVariant, ((MinigameQuestion)vm.Question!).Variant);
            StringAssert.Contains(maske.Regelzeile, "kleinsten");
        }

        /// <summary>
        /// Die Auswahlliste der Spielweisen bleibt dieselbe Liste, und -1 wird keine Spielweise.
        /// <para>
        /// Eine neue Liste je Meldung setzte in WPF die Auswahl zurueck, und deren Index -1 kaeme ueber
        /// die TwoWay-Bindung als <c>Variant = -1</c> in die Frage - beim ersten Wechsel der Spielweise.
        /// </para>
        /// </summary>
        [TestMethod]
        public void TheVariantListStaysPutAndMinusOneIsNoVariant()
        {
            var (vm, maske) = Neu();
            var vorher = maske.Varianten;

            maske.Variante = MehrheitEvaluator.MinorityVariant;
            Assert.AreSame(vorher, maske.Varianten, "Die Spielweisen kommen als neue Liste - die Auswahl springt.");

            maske.Variante = -1;
            Assert.AreEqual(MehrheitEvaluator.MinorityVariant, ((MinigameQuestion)vm.Question!).Variant);
        }

        /// <summary>
        /// Eine frisch ausgefuellte Mehrheit ist speicherbar - mit einer Option nicht. Gemessen an
        /// <c>CanSave</c>, denn die Pruefung laeuft auf der Vorschau der Maske, nicht auf der Frage.
        /// </summary>
        [TestMethod]
        public void AFreshlyFilledMajorityCanBeSavedAndOneOptionCannot()
        {
            var (vm, maske) = Neu();

            maske.Zeilen[0].Text = "Pizza";

            Assert.IsFalse(vm.CanSave, "Eine Mehrheit mit einer einzigen Option ist speicherbar.");
            Assert.IsTrue(vm.Issues.Any(i => i.Code == MinigameChecks.TooFewOptions),
                "Die Maske nennt nicht, was fehlt: " + string.Join(" | ", vm.Issues.Select(i => i.Code)));

            maske.Zeilen[1].Text = "Pasta";

            Assert.IsTrue(vm.CanSave,
                "Mit zwei Optionen nicht speicherbar. Offen: "
                + string.Join(" | ", vm.Issues.Where(i => i.IsError).Select(i => i.Code)));
        }
    }
}
