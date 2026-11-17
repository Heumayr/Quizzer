using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Questions.Minispiele;
using Quizzer.DataModels.Questions.Teams;

namespace Quizzer.LogicUnitTests.DataModels.Teams
{
    /// <summary>
    /// Minispiele je Team (M37 T8, F-M37-11 und F-M37-06) als reine Funktion: Summe oder Durchschnitt der Einzelwerte, wer
    /// keinen hat, zählt mit 0; beim Durchschnitt deckelt der Feldwert; danach aufgeteilt - die Summe der Anteile ist der
    /// Teamwert; ein Minus wird je Mitglied gekappt (E2).
    /// </summary>
    [TestClass]
    public class TeamwertungMinispielUnitTests
    {
        private const int Feld = 300;

        private static readonly Guid Anna = Guid.NewGuid();
        private static readonly Guid Bert = Guid.NewGuid();
        private static readonly Guid Carl = Guid.NewGuid();

        private static MinispielTeam Rot(params Guid[] folge) => new(Guid.NewGuid(), "Rot", folge);

        private static MinispielTeamwert Fasse(TeamMinigameScoring wertung, Dictionary<Guid, int> einzeln, params Guid[] folge)
            => Teamwertung.FasseMinispiel(Rot(folge.Length > 0 ? folge : [Anna, Bert, Carl]), einzeln, wertung, Feld);

        /// <summary>Ein Sieger unter dreien: der Durchschnitt gibt dem Team ein Drittel, die Summe alles - je aufgeteilt.</summary>
        [TestMethod]
        public void OneWinnerOfThreeGivesTheTeamAThirdOnAverageAndAllAsSum()
        {
            var schnitt = Fasse(TeamMinigameScoring.Average, new() { [Anna] = Feld });
            var summe = Fasse(TeamMinigameScoring.Sum, new() { [Anna] = Feld });

            Assert.AreEqual(100, schnitt.Wert, "Der Durchschnitt zählt die Mitglieder ohne Wert nicht mit 0.");
            CollectionAssert.AreEqual(new[] { 34, 33, 33 }, new[] { Anna, Bert, Carl }.Select(id => schnitt.Anteile[id]).ToArray(),
                "Der Durchschnitt wird nicht in der Sprecherfolge aufgeteilt.");
            Assert.AreEqual(Feld, summe.Wert);
            CollectionAssert.AreEqual(new[] { 100, 100, 100 }, new[] { Anna, Bert, Carl }.Select(id => summe.Anteile[id]).ToArray());
        }

        /// <summary>Ein großes Team gewinnt im Durchschnitt nicht durch seine Größe: alle gewinnen - ein Feldwert, wie groß es auch ist.</summary>
        [TestMethod]
        public void OnAverageTheTeamSizeDoesNotMatter()
        {
            for (var groesse = 1; groesse <= TeamGrenzen.MaxTeams; groesse++)
            {
                var folge = Enumerable.Range(0, groesse).Select(_ => Guid.NewGuid()).ToArray();
                var alle = folge.ToDictionary(id => id, _ => Feld);

                Assert.AreEqual(Feld, Fasse(TeamMinigameScoring.Average, alle, folge).Wert, $"{groesse} Sieger: nicht ein Feldwert.");
                Assert.AreEqual(Feld * groesse, Fasse(TeamMinigameScoring.Sum, alle, folge).Wert, $"{groesse} Sieger: die Summe ist nicht die Summe.");
            }
        }

        /// <summary>Beim Durchschnitt deckelt der Feldwert (Zocker: das Vielfache), die Summe nicht; der Grund sagt es.</summary>
        [TestMethod]
        public void TheAverageIsCappedAtTheFieldValueTheSumIsNot()
        {
            var einzeln = new Dictionary<Guid, int> { [Anna] = 750, [Bert] = 450 };

            var schnitt = Fasse(TeamMinigameScoring.Average, einzeln, Anna, Bert);
            var summe = Fasse(TeamMinigameScoring.Sum, einzeln, Anna, Bert);

            Assert.AreEqual((Feld, true), (schnitt.Wert, schnitt.Gedeckelt), "Der Durchschnitt 600 wird nicht am Feldwert gedeckelt.");
            Assert.AreEqual((1200, false), (summe.Wert, summe.Gedeckelt));
            StringAssert.Contains(Teamwertung.Teamsatz(schnitt), "höchstens der Feldwert");
            Assert.AreEqual("Rot – Summe +1200", Teamwertung.Teamsatz(summe));
        }

        /// <summary>Kaufmännisch gerundet, ein Minus bleibt ungedeckelt - die Obergrenze gilt nur nach oben.</summary>
        [TestMethod]
        [DataRow(new[] { 100, 0, 0 }, 33)]
        [DataRow(new[] { 1, 0 }, 1)]
        [DataRow(new[] { -1, 0 }, -1)]
        [DataRow(new[] { -900, 0, 0 }, -300)]
        [DataRow(new[] { -900, -900, -900 }, -900)]
        public void TheAverageIsRoundedAwayFromZeroAndAMinusIsNotCapped(int[] werte, int erwartet)
        {
            var folge = werte.Select(_ => Guid.NewGuid()).ToArray();
            var einzeln = folge.Select((id, i) => (id, werte[i])).ToDictionary(x => x.id, x => x.Item2);

            Assert.AreEqual(erwartet, Fasse(TeamMinigameScoring.Average, einzeln, folge).Wert);
        }

        /// <summary>Über viele Muster: die Anteile ergeben den Teamwert, der Durchschnitt liegt nie über dem Feldwert.</summary>
        [TestMethod]
        public void TheSharesAlwaysAddUpAndTheAverageNeverExceedsTheFieldValue()
        {
            var zufall = new Random(37);
            var geprueft = 0;

            for (var runde = 0; runde < 500; runde++)
            {
                var folge = Enumerable.Range(0, zufall.Next(1, 9)).Select(_ => Guid.NewGuid()).ToArray();
                var einzeln = folge.Where(_ => zufall.Next(3) > 0).ToDictionary(id => id, _ => zufall.Next(-600, 1500));

                foreach (var wertung in new[] { TeamMinigameScoring.Average, TeamMinigameScoring.Sum })
                {
                    var wert = Fasse(wertung, einzeln, folge);
                    Assert.AreEqual(wert.Wert, wert.Anteile.Values.Sum(), "Die Anteile ergeben nicht den Teamwert.");
                    Assert.IsTrue(wertung == TeamMinigameScoring.Sum || wert.Wert <= Feld, $"Durchschnitt {wert.Wert} über dem Feldwert.");
                    geprueft++;
                }
            }

            Assert.AreEqual(1000, geprueft);
        }

        [TestMethod]
        public void AnUnknownScoringMovesNothing()
        {
            var wert = Fasse((TeamMinigameScoring)7, new() { [Anna] = Feld });

            Assert.AreEqual(0, wert.Wert, "Eine unbekannte Wertung bewegt Punkte.");
            Assert.AreEqual(0, Teamwertung.MinispielVorschlaege(wert, Kontostaende.OhneGrenze).Count);
        }

        /// <summary>Je Mitglied gekappt (E2), der Rest wird nicht umgelegt; ein Anteil von 0 bringt keinen Vorschlag.</summary>
        [TestMethod]
        public void EachShareIsCappedAtTheOwnBalanceAndZeroBringsNoSuggestion()
        {
            var wert = Fasse(TeamMinigameScoring.Sum, new() { [Anna] = -300 });
            var konten = new Kontostaende(new Dictionary<Guid, int> { [Anna] = 500, [Bert] = 40, [Carl] = 0 }, false);

            var vorschlaege = Teamwertung.MinispielVorschlaege(wert, konten);

            Assert.AreEqual(-100, vorschlaege[Anna].Punkte);
            Assert.AreEqual(-40, vorschlaege[Bert].Punkte, "Berts Anteil wird nicht an seinem Kontostand gekappt.");
            Assert.IsFalse(vorschlaege.ContainsKey(Carl), "Carl bei 0 bekommt einen Vorschlag von 0.");
            Assert.AreEqual("Rot – Summe −300: Anteil −40" + Punkteanteil.GekapptText, vorschlaege[Bert].Grund);
        }

        /// <summary>Die Teams der Zelle nach dem Stempel; wer ohne Team ist, bildet sein eigenes; je Team die Sprecherfolge.</summary>
        [TestMethod]
        public void TheTeamsOfTheCellFollowTheStampAndAPlayerWithoutTeamIsHisOwn()
        {
            var spiel = new Game { Id = Guid.NewGuid(), IsTeamGame = true };
            var rot = new GameTeam { Id = Guid.NewGuid(), GameId = spiel.Id, Name = "Rot", SpeakerTurn = 1 };
            var blau = new GameTeam { Id = Guid.NewGuid(), GameId = spiel.Id, Name = "Blau" };
            spiel.GameTeams.AddRange([rot, blau]);
            var dora = Guid.NewGuid();

            // Bert sitzt heute bei Blau, sein Ergebnis trägt aber den Stempel Rot (F-M37-07).
            spiel.PlayerXGames.AddRange([
                new PlayerXGame { PlayerId = Anna, GameTeamId = rot.Id },
                new PlayerXGame { PlayerId = Bert, GameTeamId = blau.Id },
                new PlayerXGame { PlayerId = Carl, GameTeamId = blau.Id },
                new PlayerXGame { PlayerId = dora }]);

            var teams = Teamwertung.MinispielTeams(spiel, [
                (new QuestionResult { PlayerId = Anna }, "Anna"),
                (new QuestionResult { PlayerId = Bert, GameTeamId = rot.Id }, "Bert"),
                (new QuestionResult { PlayerId = Carl }, "Carl"),
                (new QuestionResult { PlayerId = dora }, "Dora")]);

            CollectionAssert.AreEqual(new[] { Bert, Anna }, teams.Single(t => t.Schluessel == rot.Id).Folge.ToArray(),
                "Rot folgt nicht dem Stempel oder nicht der Sprecherfolge ab Stand 1.");
            CollectionAssert.AreEqual(new[] { Carl }, teams.Single(t => t.Schluessel == blau.Id).Folge.ToArray());
            Assert.AreEqual(Teamwertung.OhneTeamName, teams.Single(t => t.Schluessel == dora).Name, "Dora ohne Team ist nicht ihr eigenes Team.");
        }

        [TestMethod]
        public void OnlyACombinedKindInATeamGameIsScoredAsTeam()
        {
            var teamspiel = new Game { IsTeamGame = true };
            MinigameQuestion Art(MinigameKind art) => new() { Kind = art };

            Assert.IsTrue(Teamwertung.FasstMinispielZusammen(teamspiel, Art(MinigameKind.Majority)));
            Assert.IsFalse(Teamwertung.FasstMinispielZusammen(new Game(), Art(MinigameKind.Majority)), "Ohne Teamspiel wird zusammengefasst.");
            Assert.IsFalse(Teamwertung.FasstMinispielZusammen(teamspiel, Art(MinigameKind.RobberAndGuard)), "Eine Art je Person wird zusammengefasst.");
            Assert.IsFalse(Teamwertung.FasstMinispielZusammen(teamspiel, Art((MinigameKind)99)), "Eine unbekannte Art wird zusammengefasst.");
            Assert.IsFalse(Teamwertung.FasstMinispielZusammen(teamspiel, new DefaultQuestion()), "Eine gewöhnliche Frage wird zusammengefasst.");
            Assert.IsFalse(Teamwertung.FasstMinispielZusammen(null, Art(MinigameKind.Majority)));
        }
    }
}
