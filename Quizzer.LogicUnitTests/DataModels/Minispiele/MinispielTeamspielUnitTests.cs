using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Questions;
using Quizzer.DataModels.Questions.Minispiele;
using Quizzer.DataModels.Questions.Teams;

namespace Quizzer.LogicUnitTests.DataModels.Minispiele
{
    /// <summary>
    /// Minispiele im Teamspiel (M37 T8, Entwurf Abschnitt 7): jede Art entscheidet ihre Teamwertung - die Arten mit Ziel je
    /// Person, alle übrigen zusammengefasst; der Spielstart meldet eine Art, die im Teamspiel nicht gespielt wird (auch eine
    /// unbekannte Wertung, fail-closed) und eine Art je Person ohne Gegner. Dazu das Glücksrad ohne Robin Hood und der
    /// Bluff, bei dem nur hereingelegte Gegner zählen.
    /// </summary>
    [TestClass]
    public class MinispielTeamspielUnitTests
    {
        /// <summary>Die Arten mit Ziel (Entwurf Abschnitt 7) - je Person gewertet, jedes Ziel nur ein Gegner.</summary>
        private static readonly MinigameKind[] JePerson =
        [
            MinigameKind.Duel,
            MinigameKind.RobberAndGuard,
            MinigameKind.ShareOrTake,
            MinigameKind.WheelOfFortune,
            MinigameKind.Bluff,
            MinigameKind.HotPotato,
        ];

        /// <summary>Die Teamarten (Entwurf Abschnitt 6, T9 ff.) - Team gegen Team, ohne Teamspiel nicht.</summary>
        private static readonly MinigameKind[] NurImTeamspiel = [MinigameKind.TugOfWar, MinigameKind.ProxyDuel, MinigameKind.Relay];

        [TestMethod]
        public void EveryKindDecidesItsTeamScoring()
        {
            foreach (var art in Enum.GetValues<MinigameKind>())
            {
                var wertung = MinigameProfiles.For(art).TeamScoring;
                var erwartet = JePerson.Contains(art) ? MinispielTeamwertung.JePerson
                    : NurImTeamspiel.Contains(art) ? MinispielTeamwertung.NurImTeamspiel
                    : MinispielTeamwertung.Zusammengefasst;

                Assert.IsTrue(Enum.IsDefined(wertung), $"{art} hat keine gültige Teamwertung.");
                Assert.AreEqual(erwartet, wertung, $"{art}: die Teamwertung weicht vom Entwurf ab - neue Art bewusst entscheiden.");
            }
        }

        private static MinigameProfile Profil(MinispielTeamwertung wertung) => new()
        {
            Kind = (MinigameKind)99,
            TeamScoring = wertung,
            DisplayName = "Probe",
            Description = "Probe",
            Variants = [new("Probe", "Probe")],
            MinOptions = 0,
        };

        [TestMethod]
        public void ACombinedKindPlaysInEveryTeamGame()
        {
            foreach (var lager in new[] { 0, 1, 2, 5 })
                Assert.AreEqual(0, MinigameChecks.ForTeams(Profil(MinispielTeamwertung.Zusammengefasst), lager).Count(), $"{lager} Lager");
        }

        [TestMethod]
        public void AKindPerPersonNeedsAnOpponent()
        {
            Assert.AreEqual(0, MinigameChecks.ForTeams(Profil(MinispielTeamwertung.JePerson), 2).Count());

            var befund = MinigameChecks.ForTeams(Profil(MinispielTeamwertung.JePerson), 1).Single();
            Assert.AreEqual((MinigameChecks.NoOpponent, ValidationSeverity.Error), (befund.Code, befund.Severity));
            StringAssert.Contains(befund.Message, "nur gegen Gegner");
        }

        /// <summary>Der Riegel „nicht im Teamspiel“ - und eine Wertung, die diese Fassung nicht kennt, wird ebenso abgewiesen.</summary>
        [TestMethod]
        [DataRow(MinispielTeamwertung.NichtImTeamspiel)]
        [DataRow((MinispielTeamwertung)0)]
        [DataRow((MinispielTeamwertung)42)]
        public void AKindNotForTeamGamesIsAnErrorAtTheStart(MinispielTeamwertung wertung)
        {
            var befund = MinigameChecks.ForTeams(Profil(wertung), 4).Single();

            Assert.AreEqual((MinigameChecks.NotInTeamGame, ValidationSeverity.Error), (befund.Code, befund.Severity), $"Wertung {(int)wertung}");
            StringAssert.Contains(befund.Message, "im Teamspiel nicht gespielt");
        }

        /// <summary>M37 T9: eine Teamart spielt Team gegen Team - im Teamspiel braucht sie ein zweites besetztes Team.</summary>
        [TestMethod]
        public void ATeamKindNeedsASecondTeam()
        {
            Assert.AreEqual(0, MinigameChecks.ForTeams(Profil(MinispielTeamwertung.NurImTeamspiel), 2).Count());
            Assert.AreEqual(0, MinigameChecks.ForTeams(Profil(MinispielTeamwertung.NurImTeamspiel), 5).Count());

            foreach (var lager in new[] { 0, 1 })
            {
                var befund = MinigameChecks.ForTeams(Profil(MinispielTeamwertung.NurImTeamspiel), lager).Single();
                Assert.AreEqual((MinigameChecks.NoOpponent, ValidationSeverity.Error), (befund.Code, befund.Severity), $"{lager} Teams");
                StringAssert.Contains(befund.Message, "Team gegen Team");
            }
        }

        /// <summary>M37 T9: der Riegel „nur im Teamspiel“ - ohne Teamspiel ein Fehler für die Teamart, für alle übrigen nichts.</summary>
        [TestMethod]
        public void ATeamKindIsAnErrorWithoutATeamGame()
        {
            var befund = MinigameChecks.OhneTeamspiel(Profil(MinispielTeamwertung.NurImTeamspiel)).Single();

            Assert.AreEqual((MinigameChecks.OnlyInTeamGame, ValidationSeverity.Error), (befund.Code, befund.Severity));
            StringAssert.Contains(befund.Message, "nur im Teamspiel");

            foreach (var wertung in Enum.GetValues<MinispielTeamwertung>().Where(w => w != MinispielTeamwertung.NurImTeamspiel).Append((MinispielTeamwertung)42))
                Assert.AreEqual(0, MinigameChecks.OhneTeamspiel(Profil(wertung)).Count(), $"Wertung {(int)wertung} ohne Teamspiel abgewiesen.");
        }

        /// <summary>M37 P1, V23: die Frageauswahl bietet genau an, was der Spielstart nicht meldet - mit und ohne Teams (zwei besetzt).</summary>
        [TestMethod]
        public void TheSelectionOffersExactlyWhatTheStartAccepts()
        {
            foreach (var wertung in Enum.GetValues<MinispielTeamwertung>().Append((MinispielTeamwertung)42))
            {
                var profil = Profil(wertung);

                Assert.AreEqual(!MinigameChecks.OhneTeamspiel(profil).Any(), profil.PasstZu(teamspiel: false), $"Wertung {(int)wertung} ohne Teamspiel.");
                Assert.AreEqual(!MinigameChecks.ForTeams(profil, 2).Any(), profil.PasstZu(teamspiel: true), $"Wertung {(int)wertung} im Teamspiel.");
            }
        }

        /// <summary>M37 T9: für eine Teamart zählen nur besetzte Teams - wer ohne Team ist, ist dort kein Gegner, sondern spielt nicht mit.</summary>
        [TestMethod]
        public void OnlyOccupiedTeamsCountForATeamKind()
        {
            var spiel = new Game { Id = Guid.NewGuid(), IsTeamGame = true };
            var rot = new GameTeam { Id = Guid.NewGuid(), GameId = spiel.Id, Name = "Rot" };
            spiel.GameTeams.Add(rot);
            spiel.PlayerXGames.AddRange([new PlayerXGame { PlayerId = Guid.NewGuid(), GameTeamId = rot.Id }, new PlayerXGame { PlayerId = Guid.NewGuid() }]);

            Assert.AreEqual(2, Teamzuordnung.Lager(spiel), "Der Spieler ohne Team zählt als eigenes Lager.");
            Assert.AreEqual(1, Teamzuordnung.BesetzteTeams(spiel), "Ohne Team zählt als besetztes Team.");
        }

        /// <summary>Am Spiel: ohne Teamspiel nichts; alle Mitspieler in einem Team lassen dem Duell keinen Gegner.</summary>
        [TestMethod]
        public void TheGameCountsTheOccupiedTeams()
        {
            var spiel = new Game { Id = Guid.NewGuid(), IsTeamGame = true };
            var rot = new GameTeam { Id = Guid.NewGuid(), GameId = spiel.Id, Name = "Rot" };
            var blau = new GameTeam { Id = Guid.NewGuid(), GameId = spiel.Id, Name = "Blau" };
            spiel.GameTeams.AddRange([rot, blau]);
            spiel.PlayerXGames.AddRange([new PlayerXGame { PlayerId = Guid.NewGuid(), GameTeamId = rot.Id }, new PlayerXGame { PlayerId = Guid.NewGuid(), GameTeamId = rot.Id }]);
            var duell = new MinigameQuestion { Kind = MinigameKind.Duel };

            Assert.AreEqual(MinigameChecks.NoOpponent, MinigameChecks.ForTeams(duell, spiel).Single().Code, "Ein Duell ohne Gegner fällt nicht auf.");
            Assert.AreEqual(0, MinigameChecks.ForTeams(new MinigameQuestion { Kind = MinigameKind.Majority }, spiel).Count());

            spiel.PlayerXGames[1].GameTeamId = blau.Id;
            Assert.AreEqual(0, MinigameChecks.ForTeams(duell, spiel).Count(), "Zwei besetzte Teams haben keinen Gegner.");

            // Wer ohne Team ist, ist niemandes Teamkollege - wie am Hub: zwei ohne Team sind einander Gegner.
            spiel.PlayerXGames.ForEach(x => x.GameTeamId = null);
            Assert.AreEqual(0, MinigameChecks.ForTeams(duell, spiel).Count(), "Zwei ohne Team zählen als ein Lager.");

            spiel.PlayerXGames.ForEach(x => x.GameTeamId = rot.Id);
            spiel.IsTeamGame = false;
            Assert.AreEqual(0, MinigameChecks.ForTeams(duell, spiel).Count(), "Ohne Teamspiel wird nach Teams gefragt.");
        }

        [TestMethod]
        public void TheWheelHasNoRobinHoodInATeamGame()
        {
            CollectionAssert.DoesNotContain(GluecksradEvaluator.FelderFuer(true).ToList(), WheelSegment.RobinHood);
            CollectionAssert.AreEqual(GluecksradEvaluator.Felder.ToList(), GluecksradEvaluator.FelderFuer(false).ToList());

            for (var wurf = -1; wurf <= GluecksradEvaluator.Felder.Count; wurf++)
                Assert.AreNotEqual(WheelSegment.RobinHood, GluecksradEvaluator.Segment(wurf, true), $"Wurf {wurf}");

            Assert.IsFalse(GluecksradTafel.Rad(null, false, true).Any(z => z.Name.Contains("Robin Hood")), "Der Beamer zeigt Robin Hood im Teamspiel.");
            Assert.IsTrue(GluecksradTafel.Rad(null, false).Any(z => z.Name.Contains("Robin Hood")), "Ohne Teamspiel fehlt Robin Hood - die Probe misst nichts.");
        }

        /// <summary>Im Teamspiel sagt jeder Regeltext, wie gewertet wird; ohne Teamspiel bleibt er, wie er war.</summary>
        [TestMethod]
        public void InATeamGameEveryRulesTextSaysHowTheTeamScores()
        {
            var teamspiel = new Game { IsTeamGame = true };

            foreach (var art in Enum.GetValues<MinigameKind>())
            {
                var frage = new MinigameQuestion { Kind = art };
                MinigameProfiles.For(art).ApplyDefaults(frage);

                Assert.AreEqual(MinigameProfiles.RulesFor(frage), MinigameProfiles.RulesFor(frage, new Game()), $"{art}: ohne Teamspiel ein anderer Text.");
                StringAssert.Contains(MinigameProfiles.RulesFor(frage, teamspiel), "Im Teamspiel", $"{art}: der Text sagt nichts zum Teamspiel.");
            }

            var mehrheit = new MinigameQuestion { Kind = MinigameKind.Majority };
            StringAssert.Contains(MinigameProfiles.RulesFor(mehrheit, teamspiel), "Durchschnitt eures Teams");
            StringAssert.Contains(MinigameProfiles.RulesFor(mehrheit, new Game { IsTeamGame = true, TeamMinigameScoring = TeamMinigameScoring.Sum }), "Summe eures Teams");
            StringAssert.Contains(MinigameProfiles.RulesFor(new MinigameQuestion { Kind = MinigameKind.WheelOfFortune }, teamspiel), "ohne Robin Hood");
            StringAssert.EndsWith(MinigameProfiles.RulesFor(new MinigameQuestion { Kind = MinigameKind.RobberAndGuard }, teamspiel), MinigameProfiles.NurGegnerText);
        }

        /// <summary>Annas Bluff legt Bert (ihr Team) und Carl (Gegner) herein - es zählt nur Carl; ohne Teams beide.</summary>
        [TestMethod]
        public void TheBluffCountsOnlyFooledOpponents()
        {
            var anna = Guid.NewGuid();
            var bert = Guid.NewGuid();
            var carl = Guid.NewGuid();
            var runde = new BluffRunde([new(1, "Rom", true, []), new(2, "Paris", false, [anna])], new Dictionary<Guid, int> { [bert] = 2, [carl] = 2 });
            bool Kollegen(Guid a, Guid b) => (a == anna || a == bert) && (b == anna || b == bert);

            var team = BluffEvaluator.Werte(runde, 300, Kontostaende.OhneGrenze, Kollegen);
            var einzeln = BluffEvaluator.Werte(runde, 300, Kontostaende.OhneGrenze);

            Assert.AreEqual((1, 150), (team.Reinfaelle(anna), team.PunkteVon(anna)), "Im Teamspiel zählt Annas Teamkollege Bert.");
            Assert.AreEqual((2, 300), (einzeln.Reinfaelle(anna), einzeln.PunkteVon(anna)));
        }
    }
}
