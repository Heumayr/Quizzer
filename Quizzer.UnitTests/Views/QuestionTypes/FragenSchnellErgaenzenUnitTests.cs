using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Helpers;
using Quizzer.DataModels.Questions;
using Quizzer.Views.QuestionTypes;

namespace Quizzer.UnitTests.Views.QuestionTypes
{
    /// <summary>
    /// E1 (M31, 2026-10-23): „ziel ist es leicht fragen zu ergänzen" - Typwahl im Editor,
    /// Vorbelegung aus der letzten Frage, Bezeichnung aus dem Fragetext.
    /// </summary>
    [TestClass]
    public class FragenSchnellErgaenzenUnitTests
    {
        private static EditQuestionViewModel Neu(QuestionType typ)
        {
            var frage = Factory.CreateNewQuestion(typ);
            frage.Points = 100;
            return new EditQuestionViewModel { Question = frage };
        }

        [TestMethod]
        public void ANewQuestionStartsWithTheLastOnesTypeCategoryAndPoints()
        {
            var vorige = Factory.CreateNewQuestion(QuestionType.MultipleChoice);
            vorige.CategoryId = Guid.NewGuid();
            vorige.Points = 300;
            vorige.MinusPoints = 150;
            vorige.Difficulty = Difficulty.Level3;

            Vorbelegung.Merke(vorige);
            var neu = Vorbelegung.NeueFrage();

            Assert.AreEqual(QuestionType.MultipleChoice, neu.Typ);
            Assert.AreEqual(vorige.CategoryId, neu.CategoryId);
            Assert.AreEqual(300, neu.Points);
            Assert.AreEqual(150, neu.MinusPoints);
            Assert.AreEqual(Difficulty.Level3, neu.Difficulty);
            Assert.AreEqual(Guid.Empty, neu.Id, "Die Vorbelegung darf nichts speichern.");

            // Eine Reihenfolgefrage kennt keine Minuspunkte - die Vorbelegung bringt sie nicht mit.
            Assert.AreEqual(0, Vorbelegung.NeueFrage(QuestionType.Ordering).MinusPoints);
        }

        [TestMethod]
        public void TheDesignationFollowsTheQuestionTextUntilSomeoneChangesIt()
        {
            var vm = Neu(QuestionType.Default);

            vm.Fragetext = "Wie hoch ist der Großglockner?";
            Assert.AreEqual("Wie hoch ist der Großglockner", vm.Question!.Designation);
            Assert.IsFalse(string.IsNullOrWhiteSpace(vm.Question.DesignationShort));

            vm.Fragetext = "Wie hoch ist der Ortler?";
            Assert.AreEqual("Wie hoch ist der Ortler", vm.Question.Designation, "Die Bezeichnung lief nicht mit.");

            vm.Bezeichnung = "Berge: Ortler";
            vm.Fragetext = "Wie hoch ist der Ortler genau?";
            Assert.AreEqual("Berge: Ortler", vm.Question.Designation, "Eine von Hand geänderte Bezeichnung wurde überschrieben.");
        }

        [TestMethod]
        public void SwitchingTheTypeOfANewQuestionKeepsTextAndRows()
        {
            var vm = Neu(QuestionType.MultipleChoice);
            vm.Fragetext = "Ordne die Städte von Nord nach Süd";

            var editor = vm.Zeileneditor!;
            while (editor.Zeilen.Count < 3)
                editor.AddRowCommand.Execute(null);

            editor.Zeilen[0].Text = "Hamburg";
            editor.Zeilen[1].Text = "Wien";
            editor.Zeilen[2].Text = "Rom";

            vm.GewaehlterTyp = QuestionTypeProfiles.For(QuestionType.Ordering);

            Assert.AreEqual(QuestionType.Ordering, vm.Question!.Typ);
            Assert.AreEqual("Ordne die Städte von Nord nach Süd", vm.Question.QuestionText);
            CollectionAssert.AreEqual(new[] { "Hamburg", "Wien", "Rom" },
                vm.Zeileneditor!.Zeilen.Select(z => z.Text).Where(t => t.Length > 0).ToArray(),
                "Die Zeilen gingen beim Typwechsel verloren.");

            // Die Bezeichnung läuft weiter mit - der Wechsel macht sie nicht zur Handeingabe.
            vm.Fragetext = "Ordne die Städte";
            Assert.AreEqual("Ordne die Städte", vm.Question.Designation);
        }

        [TestMethod]
        public void ASavedQuestionDoesNotChangeItsTypeHere()
        {
            var vm = ZeileneditorUnitTests.Mit(QuestionType.Default);

            Assert.IsFalse(vm.IstNeu);
            vm.GewaehlterTyp = QuestionTypeProfiles.For(QuestionType.MultipleChoice);

            Assert.AreEqual(QuestionType.Default, vm.Question!.Typ, "Eine gespeicherte Frage wird umgewandelt, nicht neu aufgebaut.");
        }

        /// <summary>M32: von der KI kommen nur neue falsche Antworten - keine richtige, keine doppelte.</summary>
        [TestMethod]
        public void OnlyNewWrongAnswersAreTakenFromTheDraft()
        {
            var entwurf = new DataModels.Transfer.Ki.KiFrageEntwurf
            {
                Zeilen =
                [
                    new() { Text = "Lima", Richtig = true },
                    new() { Text = "quito" },
                    new() { Text = "Bogotá" },
                    new() { Text = "Caracas" },
                    new() { Text = "Santiago" },
                ],
            };

            var neu = EditQuestionViewModel.NeueAblenker([entwurf], ["Lima", "Quito"], anzahl: 2);

            CollectionAssert.AreEqual(new[] { "Bogotá", "Caracas" }, neu);

            var anfrage = EditQuestionViewModel.AblenkerAnfrage("Hauptstadt von Peru?", ["Lima"], ["Quito"], 2);
            Assert.AreEqual(QuestionType.MultipleChoice, anfrage.Typ);
            StringAssert.Contains(anfrage.Text, "Lima");
            StringAssert.Contains(anfrage.Text, "Quito");
            StringAssert.Contains(anfrage.Wuensche, "2");
        }

        [TestMethod]
        public void FromATileThereIsNoSaveAndNext()
        {
            var vm = Neu(QuestionType.Default);
            vm.EineFrage = true;

            Assert.IsFalse(vm.SaveAndNextCommand.CanExecute(null));
            Assert.AreEqual(System.Windows.Visibility.Collapsed, vm.NaechsteVisibility);
        }
    }
}
