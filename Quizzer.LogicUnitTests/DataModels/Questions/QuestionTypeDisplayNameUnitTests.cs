using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Helpers;
using Quizzer.DataModels.Questions;

namespace Quizzer.LogicUnitTests.DataModels.Questions
{
    /// <summary>
    /// Der Fragetyp erscheint auf dem Bildschirm mit seinem deutschen Namen.
    /// <para>
    /// Bis 2026-09-06 stand im Kopf des Fragefensters und in der Spalte "Typ" der Fragenliste
    /// der interne Bezeichner: "Appreciate" statt "Schaetzfrage", "MultipleChoice" statt
    /// "Multiple Choice". Der Grund war ein <c>ToString()</c> auf dem Enum - das Enum traegt
    /// bewusst keine <c>[Description]</c>, weil der Anzeigename schon in
    /// <c>QuestionTypeProfiles</c> steht und nicht zweimal gepflegt werden soll.
    /// </para>
    /// </summary>
    [TestClass]
    public class QuestionTypeDisplayNameUnitTests
    {
        /// <summary>Jeder Typ nennt sich so, wie sein Profil ihn nennt.</summary>
        [TestMethod]
        [DataRow(QuestionType.Default)]
        [DataRow(QuestionType.MultipleChoice)]
        [DataRow(QuestionType.Properties)]
        [DataRow(QuestionType.Appreciate)]
        public void EveryTypeShowsItsProfileName(QuestionType typ)
        {
            var frage = Factory.CreateNewQuestion(typ);

            Assert.AreEqual(QuestionTypeProfiles.For(typ).DisplayName, frage.TypDisplayName,
                "Der angezeigte Name weicht vom Profil ab - dann gibt es zwei Quellen.");
        }

        /// <summary>
        /// Und der Name ist nicht der interne Bezeichner. Ohne diese Zusicherung waere der Test
        /// oben auch dann gruen, wenn beide Seiten "Appreciate" saegten.
        /// </summary>
        [TestMethod]
        [DataRow(QuestionType.Default)]
        [DataRow(QuestionType.MultipleChoice)]
        [DataRow(QuestionType.Properties)]
        [DataRow(QuestionType.Appreciate)]
        public void NoTypeShowsItsInternalIdentifier(QuestionType typ)
        {
            var frage = Factory.CreateNewQuestion(typ);

            // MultipleChoice heisst mit Leerzeichen "Multiple Choice" - das ist der Anzeigename,
            // nicht der Bezeichner. Verglichen wird deshalb genau.
            Assert.AreNotEqual(typ.ToString(), frage.TypDisplayName, StringComparer.Ordinal,
                $"Der Typ zeigt seinen internen Bezeichner \"{typ}\" auf dem Bildschirm.");
        }
    }
}
