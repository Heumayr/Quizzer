using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Helpers;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Questions;

namespace Quizzer.LogicUnitTests.DataModels
{
    /// <summary>
    /// Die beiden Prüfregeln, die mit den Typmasken dazugekommen sind.
    /// <para>
    /// Beide stammen aus dem Entwurf zum Editor-Umbau: eine Aufdeckfrage ohne Bild war
    /// speicherbar, und dass am Ende einer Frage nichts steht, hat nie jemand gemeldet.
    /// </para>
    /// </summary>
    [TestClass]
    public class QuestionValidatorNeueRegelnUnitTests
    {
        private static QuestionBase Brauchbar(QuestionType typ)
        {
            var frage = Factory.CreateNewQuestion(typ);

            frage.Id = Guid.NewGuid();
            frage.Designation = "Probe";
            frage.DesignationShort = "P";
            frage.CategoryId = Guid.NewGuid();

            return frage;
        }

        private static bool Hat(QuestionBase frage, string code)
            => QuestionValidator.Validate(frage).Any(i => i.Code == code);

        /// <summary>
        /// Eine Aufdeckfrage ohne Bild ist nicht spielbar - und war bis zum Umbau speicherbar.
        /// </summary>
        [TestMethod]
        public void ARevealQuestionWithoutAnImageIsNotSavable()
        {
            var frage = (RevealQuestion)Brauchbar(QuestionType.Reveal);

            Assert.IsTrue(Hat(frage, QuestionValidator.RevealImageMissing),
                "Ohne Bild meldet die Pruefung nichts - es gibt nichts aufzudecken.");

            Assert.IsFalse(QuestionValidator.IsSavable(frage),
                "Eine Aufdeckfrage ohne Bild laesst sich speichern.");

            frage.ImageFileName = "portraet.png";

            Assert.IsFalse(Hat(frage, QuestionValidator.RevealImageMissing),
                "Mit Bild meldet sie immer noch - dann prueft sie das Falsche.");
        }

        /// <summary>
        /// Steht am Ende nichts, ist das eine <b>Warnung</b>.
        /// <para>
        /// Als Fehler sperrte sie das Speichern und machte den gesamten Bestand an
        /// Standardfragen beim nächsten Öffnen unspeicherbar - der Typ hatte bis zum Umbau gar
        /// kein Feld für die Antwort.
        /// </para>
        /// </summary>
        [TestMethod]
        public void AMissingClosingTextWarnsButNeverBlocks()
        {
            var frage = Brauchbar(QuestionType.Default);

            var befund = QuestionValidator.Validate(frage)
                .FirstOrDefault(i => i.Code == QuestionValidator.FinishTextMissing);

            Assert.IsNotNull(befund,
                "Ohne Abschlusstext meldet die Pruefung nichts - am Beamer bliebe der letzte "
                + "Bildschirm leer.");

            Assert.AreEqual(ValidationSeverity.Warning, befund!.Severity,
                "Der fehlende Abschluss ist ein Fehler - damit waere der gesamte Bestand an "
                + "Standardfragen unspeicherbar.");

            Assert.IsTrue(QuestionValidator.IsSavable(frage),
                "Die Frage laesst sich nicht mehr speichern.");

            frage.Steps.Add(new QuestionStepResource
            {
                Id = Guid.NewGuid(),
                SequenceNumber = 10,
                IsFinish = true,
                StepText = "3798 Meter",
                Designation = "3798 Meter",
            });

            Assert.IsFalse(Hat(frage, QuestionValidator.FinishTextMissing),
                "Mit Abschlusstext meldet sie weiter - dann prueft sie das Falsche.");
        }

        /// <summary>
        /// Wo der Typ ohnehin einen Lösungsschritt verlangt, schweigt die neue Regel - sonst
        /// stünden zwei Zeilen zur selben Sache.
        /// </summary>
        [TestMethod]
        public void TypesThatAlreadyDemandAResultStepStaySilent()
        {
            var frage = Brauchbar(QuestionType.MultipleChoice);

            Assert.IsTrue(Hat(frage, QuestionValidator.ResultStepMissing),
                "Multiple Choice meldet den fehlenden Loesungsschritt nicht mehr.");

            Assert.IsFalse(Hat(frage, QuestionValidator.FinishTextMissing),
                "Multiple Choice meldet zusaetzlich den fehlenden Abschluss - zwei Zeilen zur "
                + "selben Sache.");
        }
    }
}
