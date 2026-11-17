using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Helpers;
using Quizzer.DataModels.Questions.Teams;
using Quizzer.Logic.Spielablauf;
using Quizzer.UnitTests.PlayThrough;
using static Quizzer.Views.GameViews.Sub.PlayerResultContext;

namespace Quizzer.UnitTests.Views.GameViews
{
    /// <summary>
    /// Die Wertung der Teamberatung (M37 T5, F-M37-06, E1, E2): der Feldwert einmal je Team, über die Sprecherfolge verteilt,
    /// das Minus ebenso; jeder Anteil ist ein Vorschlag auf der eigenen Karte, gekappt am eigenen Kontostand; ein Joker wirkt
    /// danach auf den eigenen Anteil, und die Rücknahme nimmt alle Anteile mit.
    /// </summary>
    public partial class TeamberatungUnitTests
    {
        private int Summe(TestableCurrentQuestionViewModel vm, params int[] plaetze) => plaetze.Sum(i => Karte(vm, i).CurrentScoreManipulation);

        [TestMethod]
        public async Task TheTeamGetsTheFieldValueOnceSplitOverItsMembers()
        {
            rotePlaetze = [0, 1, 2];
            var vm = await OeffneTeamberatungAsync();
            var feld = world!.Coordinate.CurrentPoints;
            var minus = world.Coordinate.CurrentMinusPoints;
            var plus = Aufteilung.Verteile(feld, [Id(0), Id(1), Id(2)]);

            // --- Der Sprecher von Rot richtig: jeder aus Rot seinen Anteil, Blau nichts ---
            Karte(vm, 0).Suggestion = ScoreSuggestion.Right;

            CollectionAssert.AreEqual(new[] { plus[Id(0)], plus[Id(1)], plus[Id(2)] }, new[] { 0, 1, 2 }.Select(i => Karte(vm, i).CurrentScoreManipulation).ToArray(),
                "Die Karten von Rot tragen nicht ihren Anteil.");
            Assert.AreEqual(feld, Summe(vm, 0, 1, 2), "Rot bekommt nicht genau den Feldwert.");
            Assert.AreEqual(0, Karte(vm, 3).CurrentScoreManipulation, "Blau bekommt etwas von Rots Antwort.");
            Assert.AreEqual(ScoreSuggestion.Correction, Karte(vm, 1).Suggestion, "Die unbewertete Karte trägt den Anteil nicht als Vorschlag.");
            StringAssert.Contains(Karte(vm, 1).SuggestionText, "Teamberatung Rot");

            // --- Falsch: das Minus einmal, aufgeteilt ---
            Karte(vm, 0).Suggestion = ScoreSuggestion.Wrong;
            Assert.AreEqual(-minus, Summe(vm, 0, 1, 2), "Rot verliert nicht genau einmal die Minuspunkte.");

            // --- Blau allein: der ganze Wert ---
            Karte(vm, 3).Suggestion = ScoreSuggestion.Right;
            Assert.AreEqual(feld, Karte(vm, 3).CurrentScoreManipulation);

            // --- Die Spielleitung bewertet statt des Sprechers einen anderen aus Rot: dessen Wertung gilt fürs Team (E1) ---
            Karte(vm, 0).Suggestion = ScoreSuggestion.None;
            Karte(vm, 2).Suggestion = ScoreSuggestion.Right;
            Assert.AreEqual(feld, Summe(vm, 0, 1, 2), "Ohne Wertung des Sprechers zählt die Wertung eines Mitglieds nicht fürs Team.");
            Assert.AreEqual(plus[Id(2)], Karte(vm, 2).CurrentScoreManipulation, "Das bewertete Mitglied bekommt mehr als seinen Anteil.");
        }

        /// <summary>Der Sprecher zählt vor: richtig er, falsch ein anderes Mitglied - es gilt der Sprecher, und nur einmal.</summary>
        [TestMethod]
        public async Task TheSpeakersRatingCountsForTheTeam()
        {
            var vm = await OeffneTeamberatungAsync();
            var feld = world!.Coordinate.CurrentPoints;

            Karte(vm, 1).Suggestion = ScoreSuggestion.Wrong;
            Karte(vm, 0).Suggestion = ScoreSuggestion.Right;

            Assert.AreEqual(feld, Summe(vm, 0, 1), "Nicht die Wertung des Sprechers gilt.");
        }

        /// <summary>E2 je Mitglied: wer wenig hat, verliert höchstens seinen Kontostand; der gekappte Rest geht an niemanden.</summary>
        [TestMethod]
        public async Task EachMembersMinusIsCappedAtHisOwnBalance()
        {
            unterNullErlaubt = false;
            var vm = await OeffneTeamberatungAsync(kontostaende: [10, 100, 0, 0]);
            var anteile = Aufteilung.Verteile(-world!.Coordinate.CurrentMinusPoints, [Id(0), Id(1)]);
            Assert.IsTrue(anteile[Id(0)] < -10, "Das Minus ist zu klein - die Kappung würde nicht gemessen.");

            Karte(vm, 0).Suggestion = ScoreSuggestion.Wrong;

            Assert.AreEqual(-10, Karte(vm, 0).CurrentScoreManipulation, "Der Sprecher verliert mehr als seinen Kontostand.");
            Assert.AreEqual(anteile[Id(1)], Karte(vm, 1).CurrentScoreManipulation, "Der gekappte Rest wurde umgelegt.");
            Assert.AreEqual(Punktebewegung.Kappe(anteile[Id(1)], 100, false), anteile[Id(1)]);
        }

        /// <summary>Erst die Teamregel, dann der Joker (Entwurf Abschnitt 3): Doppelt verdoppelt den eigenen Anteil, nicht den Feldwert.</summary>
        [TestMethod]
        public async Task AJokerWorksOnTheOwnShareAfterTheTeamRule()
        {
            jokerFuer = (1, JokerKind.Double);
            var vm = await OeffneTeamberatungAsync();
            Assert.IsTrue(vm.VorphaseLaeuft, "Vor der Teamberatung lief keine Joker-Runde.");

            await JokerAsync(1, joker!.Id);
            await NachDerJokerRundeAsync(vm);
            Assert.AreEqual(BuzzerControlsLayout.Input, vm.CurrentBuzzerLayout, "Nach der Joker-Runde kommt keine Teamberatung.");

            var anteile = Aufteilung.Verteile(world!.Coordinate.CurrentPoints, [Id(0), Id(1)]);
            Karte(vm, 0).Suggestion = ScoreSuggestion.Right;

            Assert.AreEqual(2 * anteile[Id(1)], Karte(vm, 1).CurrentScoreManipulation, "Der Joker verdoppelt nicht den eigenen Anteil.");
            Assert.AreEqual(anteile[Id(0)], Karte(vm, 0).CurrentScoreManipulation, "Der Joker eines anderen wirkt auf den Sprecher.");
            StringAssert.Contains(Karte(vm, 1).SuggestionText, "Teamberatung Rot");
            StringAssert.Contains(Karte(vm, 1).SuggestionText, "Joker");
        }

        [TestMethod]
        public async Task AShieldTakesOnlyTheMinusOfTheOwnShare()
        {
            jokerFuer = (1, JokerKind.Shield);
            var vm = await OeffneTeamberatungAsync();
            await JokerAsync(1, joker!.Id);
            await NachDerJokerRundeAsync(vm);

            var anteile = Aufteilung.Verteile(-world!.Coordinate.CurrentMinusPoints, [Id(0), Id(1)]);
            Karte(vm, 0).Suggestion = ScoreSuggestion.Wrong;

            Assert.AreEqual(0, Karte(vm, 1).CurrentScoreManipulation, "Der Schutz nimmt das Minus des eigenen Anteils nicht.");
            Assert.AreEqual(anteile[Id(0)], Karte(vm, 0).CurrentScoreManipulation, "Der Schutz wirkt auf den Sprecher.");
        }

        /// <summary>Die Rücknahme (V5) trägt die Anteile mit: gebucht, dann vollständig zurück - auch auf den unbewerteten Karten.</summary>
        [TestMethod]
        public async Task TheUndoTakesBackEveryShare()
        {
            var vm = await OeffneTeamberatungAsync();
            var anteile = Aufteilung.Verteile(world!.Coordinate.CurrentPoints, [Id(0), Id(1)]);
            var merk = LetzteWertung.Merke(world.Game, world.Coordinate);

            Karte(vm, 0).Suggestion = ScoreSuggestion.Right;
            Assert.IsTrue(await vm.PlayersResultViewModel!.TrySaveAsync(), "Das Speichern scheiterte.");
            world.Coordinate.QuestionResults = vm.PlayersResultViewModel.Results;

            CollectionAssert.AreEquivalent(new[] { (Id(0), anteile[Id(0)]), (Id(1), anteile[Id(1)]) }, merk.Aenderungen().ToList(),
                "Die Rücknahme sieht die Anteile nicht auf allen Karten.");
            await merk.NimmZurueckAsync(world.Game);
            Assert.IsTrue(world.Coordinate.QuestionResults.All(r => r.FinalScore == 0), "Nach der Rücknahme steht noch ein Anteil.");
        }
    }
}
