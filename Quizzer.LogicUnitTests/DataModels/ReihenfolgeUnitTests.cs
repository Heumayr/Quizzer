using LocalBuzzer.Service.Base.States;
using LocalBuzzer.Service.Hubs.Accessors;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Helpers;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Questions;
using Quizzer.DataModels.Questions.Schrittbau;
using Quizzer.DataModels.Transfer;

namespace Quizzer.LogicUnitTests.DataModels
{
    /// <summary>
    /// Die Reihenfolgefrage (M12, 2026-10-21: „alle" neuen Fragetypen): Lösung, Wertung je Platz,
    /// der Weg durch Maske, Mischen und Bündel - und die Prüfung der Abgabe am Buzzer-Server.
    /// </summary>
    [TestClass]
    public class ReihenfolgeUnitTests
    {
        private static readonly string[] Epochen = ["Steinzeit", "Antike", "Mittelalter", "Neuzeit"];

        /// <summary>Eine Frage, wie sie die Maske schreibt: Zeilen in Lösungsfolge.</summary>
        private static OrderingQuestion FrageAusDerMaske()
        {
            var frage = (OrderingQuestion)Factory.CreateNewQuestion(QuestionType.Ordering);
            frage.Id = Guid.NewGuid();
            frage.Designation = "Epochen";
            frage.DesignationShort = "E-1";
            frage.CategoryId = Guid.NewGuid();

            var composer = StepComposers.For(QuestionType.Ordering);
            var bild = composer.Lies(frage);

            for (var i = 0; i < Epochen.Length; i++)
                bild.Zeilen[i].Text = Epochen[i];

            composer.Schreib(frage, bild);

            return frage;
        }

        /// <summary>Die Tasten nach dem Mischen, in Lösungsfolge.</summary>
        private static List<string> TastenInLoesungsfolge(QuestionBase frage)
            => ReihenfolgeEvaluator.Loesung(frage)!.Select(s => s.QuestionViewKey).ToList();

        [TestMethod]
        public void TheMaskStampsTheRowOrderAsSolution()
        {
            var frage = FrageAusDerMaske();

            var loesung = ReihenfolgeEvaluator.Loesung(frage);

            Assert.IsNotNull(loesung, "Nach dem Speichern der Maske fehlt die Loesung.");
            CollectionAssert.AreEqual(Epochen, loesung.Select(s => s.StepText).ToArray());
            Assert.AreEqual(4, frage.BuzzerMaxAllowedKeySelect,
                "Gewaehlt werden muessen alle vier Elemente.");
            Assert.AreEqual(0, QuestionValidator.Validate(frage).Count,
                "Eine vollstaendige Reihenfolgefrage traegt Beanstandungen: "
                + string.Join("; ", QuestionValidator.Validate(frage).Select(i => i.Message)));
        }

        /// <summary>
        /// <b>Der Grund für das eigene Feld:</b> das Mischen schreibt die SequenceNumber neu. Die
        /// Lösung muss das überstehen - auch wiederholtes Mischen und erneutes Öffnen der Maske.
        /// </summary>
        [TestMethod]
        public void ShufflingDoesNotChangeTheSolution()
        {
            var frage = FrageAusDerMaske();

            for (var i = 0; i < 20; i++)
                frage.CalculateOrderdSteps();

            CollectionAssert.AreEqual(Epochen,
                ReihenfolgeEvaluator.Loesung(frage)!.Select(s => s.StepText).ToArray());

            // Die Maske liest wieder in Loesungsfolge, nicht in der gemischten.
            var bild = StepComposers.For(QuestionType.Ordering).Lies(frage);

            CollectionAssert.AreEqual(Epochen,
                bild.Zeilen.Where(z => !z.IstLeer).Select(z => z.Text).ToArray());
        }

        [TestMethod]
        public void TheRightOrderGetsFullPoints()
        {
            var frage = FrageAusDerMaske();
            frage.CalculateOrderdSteps();

            var ergebnis = ReihenfolgeEvaluator.Werte(frage, TastenInLoesungsfolge(frage), 100)!;

            Assert.IsTrue(ergebnis.AllesRichtig);
            Assert.AreEqual(100, ergebnis.Punkte);
        }

        /// <summary>Zwei vertauschte Elemente: die anderen beiden stehen richtig - halbe Punkte.</summary>
        [TestMethod]
        public void EachRightPlaceCountsItsShare()
        {
            var frage = FrageAusDerMaske();
            frage.CalculateOrderdSteps();

            var tasten = TastenInLoesungsfolge(frage);
            (tasten[1], tasten[2]) = (tasten[2], tasten[1]);

            var ergebnis = ReihenfolgeEvaluator.Werte(frage, tasten, 100)!;

            Assert.AreEqual(2, ergebnis.RichtigePlaetze);
            Assert.AreEqual(50, ergebnis.Punkte);
            CollectionAssert.AreEqual(new[] { true, false, false, true },
                ergebnis.Plaetze.Select(p => p.Richtig).ToArray());
        }

        [TestMethod]
        public void AMissingOrUnknownKeyIsAWrongPlace()
        {
            var frage = FrageAusDerMaske();
            frage.CalculateOrderdSteps();

            var tasten = TastenInLoesungsfolge(frage).Take(2).Append("Z").ToList();

            var ergebnis = ReihenfolgeEvaluator.Werte(frage, tasten, 100)!;

            Assert.AreEqual(2, ergebnis.RichtigePlaetze);
            Assert.AreEqual(string.Empty, ergebnis.Plaetze[3].Taste);
        }

        [TestMethod]
        public void SharesAreRoundedCommercially()
        {
            Assert.AreEqual(33, ReihenfolgeEvaluator.Anteil(100, 1, 3));
            Assert.AreEqual(67, ReihenfolgeEvaluator.Anteil(100, 2, 3));
            Assert.AreEqual(100, ReihenfolgeEvaluator.Anteil(100, 3, 3));
            Assert.AreEqual(0, ReihenfolgeEvaluator.Anteil(100, 0, 3));
        }

        /// <summary>
        /// Eine umgewandelte Frage trägt überall 0 - dann wertet das Spiel nicht, und die Prüfung
        /// sagt es, ohne das Speichern zu sperren.
        /// </summary>
        [TestMethod]
        public void WithoutAStampedSolutionNothingIsScored()
        {
            var frage = new OrderingQuestion { Id = Guid.NewGuid() };

            foreach (var text in Epochen)
                frage.Steps.Add(new QuestionStepResource { Id = Guid.NewGuid(), StepText = text });

            frage.CalculateOrderdSteps();

            Assert.IsNull(ReihenfolgeEvaluator.Werte(frage, ["A", "B", "C", "D"], 100));

            var funde = QuestionValidator.Validate(frage)
                .Where(i => i.Code == QuestionValidator.OrderingSolutionIncomplete).ToList();

            Assert.AreEqual(1, funde.Count);
            Assert.IsFalse(funde[0].IsError);
        }

        [TestMethod]
        public void TheSolutionTravelsThroughTheExportBundle()
        {
            var frage = FrageAusDerMaske();

            var schritte = frage.Steps.OrderBy(s => s.SequenceNumber).ToList();

            var daten = schritte.Select(s => new GameExportDocument.StepData
            {
                SequenceNumber = s.SequenceNumber,
                SolutionNumber = s.SolutionNumber == 0 ? null : s.SolutionNumber,
                StepText = s.StepText,
            }).ToList();

            Assert.IsTrue(daten.All(d => d.SolutionNumber is >= 1 and <= 4),
                "Das Buendel traegt die Plaetze nicht.");
        }

        // --- Buzzer-Server: was eine Abgabe zu einer Reihenfolgefrage ist ---

        private static (BuzzerKeySelector State, Player Spieler) Tastenwahl(bool reihenfolge)
        {
            var spieler = new Player { Id = Guid.NewGuid(), Designation = "Anna" };
            var spiel = new Game { Id = Guid.NewGuid() };
            spiel.PlayerXGames.Add(new PlayerXGame { Id = Guid.NewGuid(), GameId = spiel.Id, PlayerId = spieler.Id, Player = spieler });

            var state = new BuzzerKeySelector(new GameAccessor { GetGame = () => spiel });
            state.Infos = new BuzzerKeySelector.BuzzerKeySelectorInfo
            {
                KeysAndDesignations = new() { ["A"] = "Antike", ["B"] = "Neuzeit", ["C"] = "Steinzeit" },
                MaxAllowedSelections = 3,
                Ordered = reihenfolge,
            };

            return (state, spieler);
        }

        [TestMethod]
        [DataRow(new[] { "C", "A" }, DisplayName = "unvollstaendig")]
        [DataRow(new[] { "C", "A", "A" }, DisplayName = "doppelt")]
        [DataRow(new[] { "C", "A", "Z" }, DisplayName = "unbekannt")]
        public void TheServerRefusesAnIncompleteOrder(string[] tasten)
        {
            var (state, spieler) = Tastenwahl(reihenfolge: true);

            state.SetSelectedKeys(new() { PlayerId = spieler.Id, Player = spieler, SelectedKeys = [.. tasten] });

            Assert.IsFalse(state.KeyResultsForPlayer[spieler.Id].CommittedResult);
            Assert.IsFalse(state.Locked, "Eine verworfene Abgabe schliesst die Runde.");
        }

        [TestMethod]
        public void TheServerKeepsTheClickOrder()
        {
            var (state, spieler) = Tastenwahl(reihenfolge: true);

            state.SetSelectedKeys(new() { PlayerId = spieler.Id, Player = spieler, SelectedKeys = ["C", "A", "B"] });

            Assert.IsTrue(state.KeyResultsForPlayer[spieler.Id].CommittedResult);
            CollectionAssert.AreEqual(new[] { "C", "A", "B" }, state.KeyResultsForPlayer[spieler.Id].SelectedKeys);
            Assert.IsTrue(((BuzzerKeySelector.BuzzerKeySelectorInfo)state.BuzzerStateInfo).Ordered,
                "Das Telefon erfaehrt nicht, dass die Reihenfolge zaehlt.");
        }

        [TestMethod]
        public void MultipleChoiceStillAcceptsAPartialSelection()
        {
            var (state, spieler) = Tastenwahl(reihenfolge: false);

            state.SetSelectedKeys(new() { PlayerId = spieler.Id, Player = spieler, SelectedKeys = ["A"] });

            Assert.IsTrue(state.KeyResultsForPlayer[spieler.Id].CommittedResult);
        }
    }
}
