using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Questions.Teams;
using Quizzer.TestShared;
using Quizzer.UnitTests.PlayThrough;
using Quizzer.Views.GameViews.Teamregeln;
using static Quizzer.Views.GameViews.Sub.PlayerResultContext;

namespace Quizzer.UnitTests.Views.GameViews
{
    /// <summary>
    /// „Alle oder keiner" im Fragefenster (M37 T6), echter Server und Telefone: jedes Mitglied antwortet selbst, die Spielleitung
    /// bewertet jede Karte (E1). Erst wenn alle bewertet sind, die antworten konnten, steht der Teamwert fest - alle richtig der
    /// Feldwert, einer falsch das Minus, beides über das ganze Team aufgeteilt. Wer per Joker gesperrt oder getrennt ist, zählt
    /// nicht mit; konnte niemand antworten, gibt es nichts.
    /// </summary>
    [TestClass]
    public class AlleOderKeinerUnitTests : TeamregelServerTestBasis
    {
        protected override int Port => TestPorts.AlleOderKeiner;

        private Task<TestableCurrentQuestionViewModel> OeffneAsync(QuestionType typ = QuestionType.Default)
            => OeffneTeamregelAsync(CellSpecialRule.AllOrNothing, typ: typ);

        private int Rot(TestableCurrentQuestionViewModel vm) => Karte(vm, 0).CurrentScoreManipulation + Karte(vm, 1).CurrentScoreManipulation;

        [TestMethod]
        public async Task OnlyWhenAllAreRightTheTeamGetsTheFieldValue()
        {
            var vm = await OeffneAsync();
            var feld = world!.Coordinate.CurrentPoints;
            var minus = world.Coordinate.CurrentMinusPoints;
            var plus = Aufteilung.Verteile(feld, [Id(0), Id(1)]);

            Assert.AreEqual(BuzzerControlsLayout.Input, vm.CurrentBuzzerLayout);
            Assert.IsTrue(await WarteAsync(() => Hub.CurrentLayout == BuzzerControlsLayout.Input), "Die Telefone zeigen keine Eingabe.");
            Assert.AreEqual(AlleOderKeinerAblauf.Eingabe, Hub.BuzzerInputState.Infos.Placeholder);
            Assert.AreEqual(4, Hub.AnswerLock.Answerers?.Count, "Nicht jedes Mitglied darf antworten.");
            Assert.AreEqual("Teamregel: Alle oder keiner", vm.Sonderregel.Titel);
            StringAssert.Contains(vm.Sonderregel.Hinweis, "ganze Team");
            Assert.AreEqual("Antwortet für Rot", Karte(vm, 1).SonderregelText);

            await SchreibeAsync(vm, 0, "Paris");
            await SchreibeAsync(vm, 1, "Paris");

            // --- Einer richtig, einer offen: nichts steht fest, auch nicht die vollen Feldpunkte auf der bewerteten Karte ---
            Karte(vm, 0).Suggestion = ScoreSuggestion.Right;
            Assert.AreEqual(0, Rot(vm), "Vor der letzten Bewertung bekommt Rot etwas.");
            StringAssert.Contains(Karte(vm, 0).SuggestionText, "noch 1 unbewertet");

            // --- Beide richtig: der Feldwert einmal, aufgeteilt ---
            Karte(vm, 1).Suggestion = ScoreSuggestion.Right;
            Assert.AreEqual(plus[Id(0)], Karte(vm, 0).CurrentScoreManipulation, "Rot trägt nicht seine Anteile.");
            Assert.AreEqual(feld, Rot(vm), "Rot bekommt nicht genau den Feldwert.");
            StringAssert.Contains(Karte(vm, 1).SuggestionText, "Alle oder keiner Rot: alle richtig");

            // --- Einer falsch: das Minus einmal, aufgeteilt - auch auf der richtigen Karte ---
            Karte(vm, 1).Suggestion = ScoreSuggestion.Wrong;
            Assert.AreEqual(-minus, Rot(vm), "Rot verliert nicht genau einmal die Minuspunkte.");
            Assert.IsTrue(Karte(vm, 0).CurrentScoreManipulation < 0, "Die richtige Karte behält ihr Plus.");
            Assert.AreEqual(0, Karte(vm, 2).CurrentScoreManipulation + Karte(vm, 3).CurrentScoreManipulation, "Blau bekommt etwas von Rot.");
            TestEnvironment.ThrowIfAnythingWasSwallowed();
        }

        /// <summary>A01/E1: der per Joker Gesperrte antwortet umsonst, muss nicht richtig sein und bekommt trotzdem seinen Anteil.</summary>
        [TestMethod]
        public async Task ABlockedMemberNeedNotBeRightButGetsHisShare()
        {
            jokerFuer = (2, JokerKind.Block);
            var vm = await OeffneAsync();

            await JokerAsync(2, joker!.Id, ziel: Id(0));
            await NachDerJokerRundeAsync(vm, antwortende: 3);

            await phones[0].InvokeAsync("SubmitInput", new { value = "Paris" });
            await SchreibeAsync(vm, 1, "Paris");
            Assert.IsNull(Karte(vm, 0).InputResult, "Die Antwort des Gesperrten kam an.");

            Karte(vm, 1).Suggestion = ScoreSuggestion.Right;
            Karte(vm, 0).Suggestion = ScoreSuggestion.Wrong;

            Assert.AreEqual(world!.Coordinate.CurrentPoints, Rot(vm), "Die Bewertung des Gesperrten zählt fürs Team.");
            Assert.AreEqual(Aufteilung.Verteile(world.Coordinate.CurrentPoints, [Id(0), Id(1)])[Id(0)], Karte(vm, 0).CurrentScoreManipulation,
                "Der Gesperrte bekommt nicht seinen Anteil.");
        }

        /// <summary>A06: ist der Einzige eines Teams getrennt, ist „alle richtig" nicht wahr - auch eine bewertete Karte bekommt 0.</summary>
        [TestMethod]
        public async Task WhenNobodyOfATeamCouldAnswerItGetsNothing()
        {
            rotePlaetze = [0];
            FensterEinrichten = async _ =>
            {
                await phones[0].DisposeAsync();
                Assert.IsTrue(await WarteAsync(() => world!.Players[0].ConnectionState == PlayerConnection.Disconnected), "Das Telefon trennt nicht.");
            };

            var vm = await OeffneAsync();

            Assert.IsFalse(Hub.AnswerLock.Allows(Id(0)), "Ein getrenntes Mitglied darf antworten.");
            Karte(vm, 0).Suggestion = ScoreSuggestion.Right;

            Assert.AreEqual(0, Karte(vm, 0).CurrentScoreManipulation, "Ein Team, in dem niemand antworten konnte, bekommt den Feldwert.");
            StringAssert.Contains(Karte(vm, 0).SuggestionText, "niemand konnte antworten");
        }

        /// <summary>Wo die Frage Tasten hat, wählt jeder selbst.</summary>
        [TestMethod]
        public async Task AQuestionWithKeysLetsEveryMemberChoose()
        {
            var vm = await OeffneAsync(typ: QuestionType.MultipleChoice);

            Assert.AreEqual(BuzzerControlsLayout.KeySelect, vm.CurrentBuzzerLayout);
            Assert.IsTrue(await WarteAsync(() => Hub.CurrentLayout == BuzzerControlsLayout.KeySelect));
            Assert.AreEqual(4, Hub.AnswerLock.Answerers?.Count);
        }
    }
}
