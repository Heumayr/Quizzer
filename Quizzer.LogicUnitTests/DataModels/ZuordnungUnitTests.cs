using LocalBuzzer.Service.Base.States;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Helpers;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Questions;
using Quizzer.DataModels.Questions.Schrittbau;
using Quizzer.DataModels.Transfer;

namespace Quizzer.LogicUnitTests.DataModels
{
    /// <summary>
    /// Die Zuordnungsfrage (M12): Paare aus der Maske, Wertung je Paar über denselben Weg wie die
    /// Reihenfolge, Prüfung beider Seiten, Weg durch das Bündel.
    /// </summary>
    [TestClass]
    public class ZuordnungUnitTests
    {
        private static readonly (string Land, string Stadt)[] Paare =
            [("Österreich", "Wien"), ("Schweiz", "Bern"), ("Deutschland", "Berlin")];

        private static MatchingQuestion FrageAusDerMaske()
        {
            var frage = (MatchingQuestion)Factory.CreateNewQuestion(QuestionType.Matching);
            frage.Id = Guid.NewGuid();
            frage.Designation = "Hauptstaedte";
            frage.DesignationShort = "H-1";
            frage.CategoryId = Guid.NewGuid();

            var composer = StepComposers.For(QuestionType.Matching);
            var bild = composer.Lies(frage);

            for (var i = 0; i < Paare.Length; i++)
            {
                bild.Zeilen[i].Partner = Paare[i].Land;
                bild.Zeilen[i].Text = Paare[i].Stadt;
            }

            composer.Schreib(frage, bild);

            return frage;
        }

        [TestMethod]
        public void TheMaskWritesOnePairPerStep()
        {
            var frage = FrageAusDerMaske();

            var loesung = ReihenfolgeEvaluator.Loesung(frage)!;

            CollectionAssert.AreEqual(Paare.Select(p => p.Land).ToArray(), loesung.Select(s => s.PartnerText).ToArray());
            CollectionAssert.AreEqual(Paare.Select(p => p.Stadt).ToArray(), loesung.Select(s => s.StepText).ToArray());
            Assert.AreEqual(0, QuestionValidator.Validate(frage).Count,
                string.Join("; ", QuestionValidator.Validate(frage).Select(i => i.Message)));
        }

        /// <summary>Eine Zeile, in der erst der linke Begriff steht, darf beim Speichern nicht verschwinden.</summary>
        [TestMethod]
        public void ARowWithOnlyThePartnerIsKeptAndReported()
        {
            var frage = FrageAusDerMaske();
            var composer = StepComposers.For(QuestionType.Matching);

            var bild = composer.Lies(frage);
            bild.Zeilen.Add(composer is StepComposerBase b ? b.NeueZeile() : throw new InvalidOperationException());
            bild.Zeilen[^1].Partner = "Italien";

            composer.Schreib(frage, bild);

            Assert.AreEqual(4, frage.Steps.Count, "Die halbe Zeile wurde weggeraeumt.");
            Assert.IsTrue(QuestionValidator.Validate(frage).Any(i => i.Code == QuestionValidator.MatchingCounterpartMissing && i.IsError),
                "Das fehlende Gegenstueck wird nicht als Fehler gemeldet.");
        }

        [TestMethod]
        public void EachRightPairCountsItsShare()
        {
            var frage = FrageAusDerMaske();
            frage.CalculateOrderdSteps();

            var tasten = ReihenfolgeEvaluator.Loesung(frage)!.Select(s => s.QuestionViewKey).ToList();

            Assert.AreEqual(90, ReihenfolgeEvaluator.Werte(frage, tasten, 90)!.Punkte);

            (tasten[0], tasten[1]) = (tasten[1], tasten[0]);

            var ergebnis = ReihenfolgeEvaluator.Werte(frage, tasten, 90)!;

            Assert.AreEqual(1, ergebnis.RichtigePlaetze, "Nur Deutschland-Berlin ist richtig.");
            Assert.AreEqual(30, ergebnis.Punkte);
        }

        [TestMethod]
        public void ThePartnerTravelsThroughTheBundle()
        {
            var frage = FrageAusDerMaske();

            var schritt = frage.Steps.First();
            var klon = schritt.CloneWithoutReferences();

            Assert.AreEqual(schritt.PartnerText, klon.PartnerText, "Der Klon verliert den Begriff.");

            var daten = new GameExportDocument.StepData { PartnerText = schritt.PartnerText, SolutionNumber = schritt.SolutionNumber };
            var json = System.Text.Json.JsonSerializer.Serialize(daten);
            var zurueck = System.Text.Json.JsonSerializer.Deserialize<GameExportDocument.StepData>(json)!;

            Assert.AreEqual(schritt.PartnerText, zurueck.PartnerText);
        }

        [TestMethod]
        public void ThePhoneLearnsThePrompts()
        {
            var state = new BuzzerKeySelector(new global::LocalBuzzer.Service.Hubs.Accessors.GameAccessor());
            state.Infos = new BuzzerKeySelector.BuzzerKeySelectorInfo
            {
                Ordered = true,
                Prompts = ["Österreich", "Schweiz"],
            };

            var info = (BuzzerKeySelector.BuzzerKeySelectorInfo)state.BuzzerStateInfo;

            CollectionAssert.AreEqual(new[] { "Österreich", "Schweiz" }, info.Prompts);

            state.ClearAndLock();

            Assert.AreEqual(0, state.Infos.Prompts.Count, "Nach der Frage bleiben die Begriffe stehen.");
        }
    }
}
