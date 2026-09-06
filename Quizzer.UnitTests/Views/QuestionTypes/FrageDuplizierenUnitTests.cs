using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Helpers;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;

namespace Quizzer.UnitTests.Views.QuestionTypes
{
    /// <summary>
    /// Eine Frage duplizieren - der Hebel für den Abend mit zehn ähnlichen Fragen.
    /// <para>
    /// <b>Der Klon ist der heikle Teil.</b> Der Schreibweg schreibt ohnehin einen Klon; was der
    /// Klon nicht mitnimmt, wird lautlos nie gespeichert. Beim Duplizieren kommt hinzu, dass
    /// Kennungen <b>nicht</b> mitkommen dürfen - sonst überschriebe die Kopie das Original.
    /// </para>
    /// </summary>
    [TestClass]
    public class FrageDuplizierenUnitTests
    {
        /// <summary>
        /// Der Klon nimmt Inhalt und Schritte mit, aber <b>keine Kennung</b> - weder die der
        /// Frage noch die der Schritte.
        /// </summary>
        [TestMethod]
        public void TheCopyCarriesEverythingButTheIdentity()
        {
            var original = (MultipleChoiceQuestion)Factory.CreateNewQuestion(
                QuestionType.MultipleChoice);

            original.Id = Guid.NewGuid();
            original.Designation = "Hauptstädte 3";
            original.QuestionText = "Welche Stadt ist die Hauptstadt von Australien?";
            original.Points = 250;
            original.CategoryId = Guid.NewGuid();

            original.Steps.Add(new QuestionStepResource
            {
                Id = Guid.NewGuid(),
                QuestionBaseId = original.Id,
                SequenceNumber = 10,
                Designation = "Canberra",
                StepText = "Canberra",
                IsResult = true,
                ResourceFileName = "bild.png",
                ResourceTyp = ResourceType.Image,
            });

            var kopie = original.CloneWithoutReferences(copyIdentity: false);

            kopie.Steps.Clear();

            foreach (var schritt in original.Steps)
            {
                var geklont = schritt.CloneWithoutReferences(copyIdentity: false);

                geklont.QuestionBaseId = Guid.Empty;

                kopie.Steps.Add(geklont);
            }

            Assert.AreEqual(Guid.Empty, kopie.Id,
                "Die Kopie traegt die Kennung des Originals - beim Speichern wuerde sie es "
                + "ueberschreiben statt danebenzuliegen.");

            Assert.AreEqual(Guid.Empty, kopie.Steps[0].Id,
                "Der kopierte Schritt traegt die Kennung des Originalschritts.");

            Assert.AreEqual(QuestionType.MultipleChoice, kopie.Typ);
            Assert.AreEqual(250, kopie.Points, "Die Bewertung kam nicht mit.");
            Assert.AreEqual(original.CategoryId, kopie.CategoryId, "Die Kategorie kam nicht mit.");
            Assert.AreEqual("Canberra", kopie.Steps[0].StepText, "Der Antworttext kam nicht mit.");
            Assert.IsTrue(kopie.Steps[0].IsResult, "Das Haekchen kam nicht mit.");
            Assert.AreEqual("bild.png", kopie.Steps[0].ResourceFileName, "Das Medium kam nicht mit.");
        }

        /// <summary>
        /// Die Kopie heißt anders. Zwei gleichnamige Fragen in der Liste, und niemand weiß, welche
        /// die neue ist.
        /// </summary>
        [TestMethod]
        public void TheCopyIsNamedSoItCanBeToldApart()
        {
            Assert.AreEqual("Hauptstädte 3 (Kopie)",
                Quizzer.Views.QuestionsViewModel.Kopiename("Hauptstädte 3"));

            Assert.AreEqual("Kopie", Quizzer.Views.QuestionsViewModel.Kopiename("   "),
                "Eine Frage ohne Bezeichnung ergibt keinen brauchbaren Kopienamen.");

            Assert.AreEqual("Kopie", Quizzer.Views.QuestionsViewModel.Kopiename(null));
        }
    }
}
