using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.DataModels.Models.Base;
using Quizzer.UnitTests.PlayThrough;
using Quizzer.Views.GameViews;
using Quizzer.Views.GameViews.Sub;
using System.Windows;
using System.Windows.Controls;

namespace Quizzer.UnitTests.Views.GameViews
{
    /// <summary>
    /// Die Siegerehrung im Teamspiel (M37 T3): der Sieger ist das Team, bei Gleichstand alle gleichauf liegenden; danach
    /// kommt die Einzelwertung, Rückblick und Verlauf bleiben je Person. Ohne Teamspiel bleibt alles, wie E3 es gebaut hat.
    /// </summary>
    [TestClass]
    public class TeamSiegerehrungUnitTests
    {
        private RecordingUserPrompt prompt = null!;

        [TestInitialize]
        public void SetUp()
        {
            prompt = new RecordingUserPrompt(answer: true);
            UserPrompt.Current = prompt;
        }

        [TestCleanup]
        public void TearDown() => UserPrompt.Reset();

        /// <summary>Ein Teamspiel, dessen einzige Zelle gespielt ist: Rot 300, Blau 300, Gelb 100; auf Wunsch dazu Grün ohne Mitglieder.</summary>
        private static (Game Spiel, GameMasterViewModel Leiter) Abend(int rot = 300, int blau = 300, int gelb = 100, bool gruenLeer = false)
        {
            var spiel = new Game { Id = Guid.NewGuid(), Designation = "Teamabend", IsTeamGame = true, Phase = 1, SuggestedPhases = 1 };
            var zelle = new GameGridCoordinate { Id = Guid.NewGuid(), GameId = spiel.Id, QuestionBaseId = Guid.NewGuid(), IsDone = true };
            spiel.GameGridCoordinates.Add(zelle);

            foreach (var (name, farbe, punkte) in new[] { ("Rot", 5, rot), ("Blau", 4, blau), ("Gelb", 3, gelb) })
            {
                var team = new GameTeam { Id = Guid.NewGuid(), GameId = spiel.Id, Name = name, ColorIndex = farbe, SortOrder = spiel.GameTeams.Count };
                var spieler = new Player { Id = Guid.NewGuid(), Designation = $"Spieler {name}", DisplayName = $"Spieler {name}" };
                spiel.GameTeams.Add(team);
                spiel.PlayerXGames.Add(new PlayerXGame { Id = Guid.NewGuid(), GameId = spiel.Id, PlayerId = spieler.Id, Player = spieler, GameTeamId = team.Id });
                zelle.QuestionResults.Add(new QuestionResult
                {
                    Id = Guid.NewGuid(), GameId = spiel.Id, PlayerId = spieler.Id, Score = Math.Max(punkte, 0), MinusScore = Math.Max(-punkte, 0), GameTeamId = team.Id,
                });
            }

            if (gruenLeer)
                spiel.GameTeams.Add(new GameTeam { Id = Guid.NewGuid(), GameId = spiel.Id, Name = "Grün", ColorIndex = 2, SortOrder = spiel.GameTeams.Count });

            var leiter = new GameMasterViewModel { Game = spiel };
            var stats = leiter.StatsContext;
            stats.GameMasterViewModel = leiter;
            stats.Game = spiel;
            stats.PlayerStatsContextList = spiel.Players
                .Select(p => new PlayerStatsContext { Player = p, Game = spiel, StatsContext = stats, GameMasterViewModel = leiter })
                .ToList();
            stats.UpdateScores(leiter.IsGameFinished);
            return (spiel, leiter);
        }

        private static UcStatsView Beamer(StatsContext stats)
        {
            var ansicht = new UcStatsView { DataContext = stats };
            ansicht.Measure(new Size(1920, 1080));
            ansicht.Arrange(new Rect(0, 0, 1920, 1080));
            ansicht.UpdateLayout();
            return ansicht;
        }

        private static bool Spielerkarten(UcStatsView ansicht) =>
            TeamBeamerUnitTests.Descendants<UcPlayerStatsView>(ansicht).Any(TeamBeamerUnitTests.Sichtbar);

        [TestMethod]
        public void TheWinningTeamsComeFirstAndATieSharesThePlace()
        {
            UiTestHost.Run(() =>
            {
                var (_, leiter) = Abend();
                var stats = leiter.StatsContext;
                using var wache = new BindingErrorWatch();
                var ansicht = Beamer(stats);

                Assert.IsTrue(leiter.IsGameFinished, "Das Spiel ist nicht zu Ende - dann misst der Test nichts.");
                CollectionAssert.AreEqual(new[] { "Rot", "Blau" }, stats.SiegerTeams.Select(k => k.Name).ToArray(), "Gleichauf liegende Teams fehlen unter den Siegern.");
                Assert.AreEqual("Gleichauf vorn", stats.SiegerTeamsTitel);
                CollectionAssert.AreEqual(new[] { 3 }, stats.UebrigeTeams.Select(k => k.Platz).ToArray(), "Gelb steht nicht auf Platz 3 - der Gleichstand teilt den Platz nicht.");

                var teamkarten = TeamBeamerUnitTests.Descendants<UcTeamScoreTile>(ansicht).Where(TeamBeamerUnitTests.Sichtbar).ToList();
                Assert.AreEqual(3, teamkarten.Count, "Nicht alle Teams stehen in der Siegerehrung.");
                Assert.IsFalse(Spielerkarten(ansicht), "Siegerteam und Spielerkarten stehen übereinander.");
                Assert.AreEqual(0, wache.Errors.Count, string.Join(" || ", wache.Errors));
            });
        }

        /// <summary>R3-1: alle Teams im Minus, Grün ohne Mitglieder - Grün stand mit 0 als Siegerteam da.</summary>
        [TestMethod]
        public void AnEmptyTeamIsNoWinnerTeam()
        {
            UiTestHost.Run(() =>
            {
                var (_, leiter) = Abend(rot: -200, blau: -100, gelb: -300, gruenLeer: true);
                var stats = leiter.StatsContext;
                var ansicht = Beamer(stats);

                CollectionAssert.AreEqual(new[] { "Blau" }, stats.SiegerTeams.Select(k => k.Name).ToArray(), "Das leere Team Grün ist Siegerteam.");
                CollectionAssert.AreEqual(new[] { ("Rot", 2), ("Gelb", 3) }, stats.UebrigeTeams.Select(k => (k.Name, k.Platz)).ToArray());
                Assert.AreEqual(3, TeamBeamerUnitTests.Descendants<UcTeamScoreTile>(ansicht).Count(TeamBeamerUnitTests.Sichtbar),
                    "Das leere Team steht in der Siegerehrung.");
            });
        }

        /// <summary>Nach dem Siegerteam die Einzelwertung - die Punkte bleiben am Spieler (F-M37-01) - und wieder zurück.</summary>
        [TestMethod]
        public void AfterTheTeamsComesTheSinglePlayerRanking()
        {
            UiTestHost.Run(() =>
            {
                var (_, leiter) = Abend();
                var stats = leiter.StatsContext;

                CollectionAssert.AreEqual(new[] { SiegerehrungTafel.Sieger, SiegerehrungTafel.Einzelwertung }, stats.Tafeln.ToArray());
                Assert.AreEqual("Einzelwertung zeigen", stats.NaechsteTafelText, "Vom Siegerteam aus nennt der Knopf nicht die Einzelwertung.");

                stats.ZeigeTafel(SiegerehrungTafel.Einzelwertung);
                var ansicht = Beamer(stats);

                Assert.IsTrue(Spielerkarten(ansicht), "Die Einzelwertung zeigt keine Spielerkarten.");
                Assert.IsFalse(TeamBeamerUnitTests.Descendants<UcTeamScoreTile>(ansicht).Any(TeamBeamerUnitTests.Sichtbar), "In der Einzelwertung stehen noch die Teams.");
                Assert.AreEqual("Sieger zeigen", stats.NaechsteTafelText);
            });
        }

        /// <summary>Der Spielleiter blättert mit seinem Knopf: ohne Rückblick bleibt er nicht beim Siegerteam stehen.</summary>
        [TestMethod]
        public async Task TheGameMasterTurnsFromTheTeamsToTheSinglePlayersAndBack()
        {
            await using var world = await TestGameBuilder.CreateAsync(normalStepCount: 1, playerCount: 2);
            var rot = new GameTeam { Id = Guid.NewGuid(), GameId = world.Game.Id, Name = "Rot", ColorIndex = 5 };
            var blau = new GameTeam { Id = Guid.NewGuid(), GameId = world.Game.Id, Name = "Blau", ColorIndex = 4, SortOrder = 1 };
            world.Game.IsTeamGame = true;
            world.Game.GameTeams.AddRange([rot, blau]);
            world.Game.PlayerXGames[0].GameTeamId = rot.Id;
            world.Game.PlayerXGames[1].GameTeamId = blau.Id;
            world.Coordinate.IsDone = true;

            var leiter = new GameMasterViewModel { Game = world.Game };
            leiter.StatsContext.GameMasterViewModel = leiter;
            leiter.StatsContext.Game = world.Game;

            await ((AsyncRelayCommand)leiter.NaechsteTafelCommand).ExecuteAsync(null);
            Assert.AreEqual(SiegerehrungTafel.Einzelwertung, leiter.StatsContext.Tafel);
            Assert.AreEqual(0, prompt.Informs.Count, "Im Teamspiel meldet der Knopf „kein Rückblick“, obwohl die Einzelwertung folgt.");

            await ((AsyncRelayCommand)leiter.NaechsteTafelCommand).ExecuteAsync(null);
            Assert.AreEqual(SiegerehrungTafel.Sieger, leiter.StatsContext.Tafel, "Nach der Einzelwertung kommt ohne Rückblick wieder das Siegerteam.");
        }

        /// <summary>Gegenrichtung: ohne Teamspiel gibt es keine Einzelwertung als eigene Tafel und kein Siegerteam.</summary>
        [TestMethod]
        public void WithoutTeamGameTheWinnerIsAPlayer()
        {
            UiTestHost.Run(() =>
            {
                var (spiel, leiter) = Abend();
                spiel.IsTeamGame = false;
                var stats = leiter.StatsContext;
                stats.UpdateScores(leiter.IsGameFinished);
                var ansicht = Beamer(stats);

                CollectionAssert.AreEqual(new[] { SiegerehrungTafel.Sieger }, stats.Tafeln.ToArray());
                Assert.AreEqual("Rückblick zeigen", stats.NaechsteTafelText);
                Assert.IsTrue(Spielerkarten(ansicht));
                Assert.IsFalse(TeamBeamerUnitTests.Descendants<UcTeamScoreTile>(ansicht).Any(TeamBeamerUnitTests.Sichtbar));
            });
        }
    }
}
