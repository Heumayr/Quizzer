using LocalBuzzer.Service.Base.States;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Questions.Minispiele;
using Quizzer.Logic.Controller.TypedControllers;
using Quizzer.Views.GameViews;
using Quizzer.Views.GameViews.Sub;
using System.Collections.Concurrent;
using static Quizzer.Views.GameViews.Sub.PlayerResultContext;

namespace Quizzer.UnitTests.PlayThrough
{
    /// <summary>
    /// Minispiele je Team auf den Spielerkarten (M37 T8): im Teamspiel fasst eine zusammengefasste Art die Einzelwerte eines
    /// Teams als Durchschnitt oder Summe (F-M37-11) und legt je Karte den Anteil als Vorschlag ab (F-M37-06, E1) - auch dem
    /// Mitglied, das selbst nichts gewann. Eine Art je Person behält den Einzelvorschlag; der Spielstart fragt bei einem Duell
    /// ohne Gegner. Rot sind Spieler 1 und 2, Blau Spieler 3; die Zelle ist <see cref="feld"/> wert.
    /// </summary>
    [TestClass]
    public class MinispielTeamwertungUnitTests
    {
        private TestGameBuilder world = null!;

        /// <summary>Was „richtig“ in der Zelle bringt - gerade, damit die Anteile ohne Rest aufgehen.</summary>
        private int feld;

        [TestInitialize]
        public async Task SetUp()
        {
            UserPrompt.Current = new RecordingUserPrompt(answer: true);
            TestEnvironment.ClearSwallowedExceptions();

            world = await TestGameBuilder.CreateAsync(QuestionType.Minigame, normalStepCount: 3, playerCount: 3);
            var rot = new GameTeam { Id = Guid.NewGuid(), GameId = world.Game.Id, Name = "Rot", ColorIndex = 5, SortOrder = 0 };
            var blau = new GameTeam { Id = Guid.NewGuid(), GameId = world.Game.Id, Name = "Blau", ColorIndex = 4, SortOrder = 1 };

            using (var ctrl = new GameTeamsController())
            {
                await ctrl.InsertAsync(rot);
                await ctrl.InsertAsync(blau);
                await ctrl.SaveChangesAsync();
            }

            world.Game.IsTeamGame = true;
            world.Game.GameTeams.AddRange([rot, blau]);

            await using var context = new Quizzer.Logic.Context.DataContext();
            foreach (var (spieler, team) in new[] { (world.Players[0], rot), (world.Players[1], rot), (world.Players[2], blau) })
            {
                world.Game.PlayerXGames.Single(x => x.PlayerId == spieler.Id).GameTeamId = team.Id;
                context.PlayerXGames.Single(x => x.GameId == world.Game.Id && x.PlayerId == spieler.Id).GameTeamId = team.Id;
            }

            await context.SaveChangesAsync();
        }

        [TestCleanup]
        public async Task TearDown()
        {
            await world.DisposeAsync();
            UserPrompt.Reset();
        }

        private async Task<PlayersResultViewModel> OeffneAsync(TeamMinigameScoring wertung, MinigameKind art = MinigameKind.Majority)
        {
            world.Game.TeamMinigameScoring = wertung;

            var vm = new TestableCurrentQuestionViewModel { Coordinate = world.Coordinate };
            await vm.LoadForTestAsync();
            TestEnvironment.ThrowIfAnythingWasSwallowed();

            var fenster = vm.PlayersResultViewModel!;
            ((MinigameQuestion)fenster.Coordinate!.QuestionBase!).Kind = art;
            feld = PlayerResultContext.PunkteJetzt(fenster.Coordinate, vm);
            Assert.IsTrue(feld > 0 && feld % 4 == 0, $"Die Zelle ist {feld} wert - die Anteile unten gehen nicht ohne Rest auf.");

            return fenster;
        }

        private PlayerResultContext Karte(PlayersResultViewModel fenster, int spieler)
            => fenster.PlayerResultContextList.Single(k => k.Player.Id == world.Players[spieler].Id);

        private (ScoreSuggestion, int) Stand(PlayersResultViewModel fenster, int spieler)
            => (Karte(fenster, spieler).Suggestion, Karte(fenster, spieler).CurrentScoreManipulation);

        /// <summary>Spieler 1 und 3 wählen dasselbe, Spieler 2 etwas anderes - Spieler 1 und 3 sind die größte Gruppe.</summary>
        private void Mehrheit(PlayersResultViewModel fenster)
        {
            var tasten = ((MinigameQuestion)fenster.Coordinate!.QuestionBase!).Steps
                .Where(s => !s.IsStart && !s.IsFinish && !s.IsQuestionOnly && !string.IsNullOrEmpty(s.QuestionViewKey))
                .Select(s => s.QuestionViewKey)
                .OrderBy(t => t, StringComparer.Ordinal)
                .ToList();

            KeyValuePair<Guid, BuzzerKeySelector.SelectionResult> Wahl(int spieler, string taste)
                => new(world.Players[spieler].Id, new BuzzerKeySelector.SelectionResult
                {
                    PlayerId = world.Players[spieler].Id,
                    SelectedKeys = [taste],
                    CommittedResult = true,
                });

            fenster.SelectionResultsDic = new ConcurrentDictionary<Guid, BuzzerKeySelector.SelectionResult>(
                [Wahl(0, tasten[0]), Wahl(1, tasten[1]), Wahl(2, tasten[0])]);
        }

        /// <summary>Durchschnitt: Rot hat einen Sieger von zweien - das halbe Feld, je ein Viertel, auch für den Verlierer; Blau allein - das Feld.</summary>
        [TestMethod]
        public async Task TheMajorityIsScoredPerTeamOnAverage()
        {
            var fenster = await OeffneAsync(TeamMinigameScoring.Average);

            Mehrheit(fenster);

            Assert.AreEqual((ScoreSuggestion.Correction, feld / 4), Stand(fenster, 0), "Spieler 1 bekommt nicht seinen Anteil am Durchschnitt.");
            Assert.AreEqual((ScoreSuggestion.Correction, feld / 4), Stand(fenster, 1), "Spieler 2 bekommt keinen Anteil am Teamwert.");
            Assert.AreEqual((ScoreSuggestion.Correction, feld), Stand(fenster, 2), "Blau allein bekommt nicht den Feldwert.");
            Assert.AreEqual($"Rot – Durchschnitt +{feld / 2}: Anteil +{feld / 4}", Karte(fenster, 1).Grund);
            StringAssert.Contains(Karte(fenster, 1).ReihenfolgeText, "nicht in der größten Gruppe", "Der eigene Satz fehlt auf der Karte.");
        }

        [TestMethod]
        public async Task TheMajorityIsScoredPerTeamAsSum()
        {
            var fenster = await OeffneAsync(TeamMinigameScoring.Sum);

            Mehrheit(fenster);

            Assert.AreEqual((ScoreSuggestion.Correction, feld / 2), Stand(fenster, 0));
            Assert.AreEqual((ScoreSuggestion.Correction, feld / 2), Stand(fenster, 1));
            Assert.AreEqual((ScoreSuggestion.Correction, feld), Stand(fenster, 2));
            StringAssert.Contains(Karte(fenster, 0).Grund, $"Summe +{feld}");
        }

        /// <summary>Eine Art mit Punkten: der Durchschnitt deckelt am Feldwert, ein leerer Durchgang räumt alle Anteile weg.</summary>
        [TestMethod]
        public async Task ACombinedKindWithPointsSplitsTheTeamValueAndClearsAgain()
        {
            var fenster = await OeffneAsync(TeamMinigameScoring.Average, MinigameKind.MentalArithmetic);

            fenster.WerteVorschlaege(new Dictionary<Guid, Punktevorschlag> { [world.Players[0].Id] = new(3 * feld, "Kopfrechnen") }, _ => "Satz");

            Assert.AreEqual((ScoreSuggestion.Correction, feld / 2), Stand(fenster, 0), "Der Durchschnitt wird nicht am Feldwert gedeckelt und aufgeteilt.");
            Assert.AreEqual((ScoreSuggestion.Correction, feld / 2), Stand(fenster, 1));
            Assert.AreEqual((ScoreSuggestion.None, 0), Stand(fenster, 2), "Blau ohne Wert bekommt einen Vorschlag.");

            fenster.WerteVorschlaege(new Dictionary<Guid, Punktevorschlag>(), _ => string.Empty);

            for (var i = 0; i < 3; i++)
                Assert.AreEqual(ScoreSuggestion.None, Karte(fenster, i).Suggestion, $"Spieler {i + 1} behält den Anteil nach dem Wiederholen.");
        }

        /// <summary>E1: was der Spielleiter von Hand setzt, bleibt, solange sich der Anteil nicht ändert.</summary>
        [TestMethod]
        public async Task AValueSetByHandStaysWhileTheShareIsUnchanged()
        {
            var fenster = await OeffneAsync(TeamMinigameScoring.Sum, MinigameKind.MentalArithmetic);
            var vorschlaege = new Dictionary<Guid, Punktevorschlag> { [world.Players[0].Id] = new(80, "Kopfrechnen +80") };

            fenster.WerteVorschlaege(vorschlaege, _ => "Satz");
            Karte(fenster, 1).CurrentScoreManipulation = 7;
            fenster.WerteVorschlaege(vorschlaege, _ => "Satz");

            Assert.AreEqual(7, Karte(fenster, 1).CurrentScoreManipulation, "Der Anteil überschreibt den Wert des Spielleiters.");
            Assert.AreEqual(40, Karte(fenster, 0).CurrentScoreManipulation);
        }

        /// <summary>
        /// Der Verlauf: der Sieg wandert innerhalb von Rot (erst Spieler 2, dann Spieler 1), der Teamwert bleibt - und damit der
        /// Anteil. Ein eigenes „richtig“ darf dabei nicht durchschlagen.
        /// </summary>
        [TestMethod]
        public async Task AWinMovingWithinTheTeamLeavesTheShares()
        {
            var fenster = await OeffneAsync(TeamMinigameScoring.Average);
            var tasten = ((MinigameQuestion)fenster.Coordinate!.QuestionBase!).Steps
                .Where(s => !s.IsStart && !s.IsFinish && !s.IsQuestionOnly && !string.IsNullOrEmpty(s.QuestionViewKey))
                .Select(s => s.QuestionViewKey).OrderBy(t => t, StringComparer.Ordinal).ToList();

            void Waehle(int spieler, string taste) => fenster.SetSelectionResult(new BuzzerKeySelector.SelectionResult
            {
                PlayerId = world.Players[spieler].Id,
                SelectedKeys = [taste],
                CommittedResult = true,
            });

            Waehle(0, tasten[0]);
            Waehle(1, tasten[1]);
            Waehle(2, tasten[1]);
            Assert.AreEqual((ScoreSuggestion.Correction, feld / 4), Stand(fenster, 1), "Spieler 2 in der größten Gruppe: nicht sein Anteil.");

            Waehle(2, tasten[0]);

            Assert.AreEqual((ScoreSuggestion.Correction, feld / 4), Stand(fenster, 0), "Spieler 1 bekommt sein eigenes „richtig“ statt des Anteils.");
            Assert.AreEqual((ScoreSuggestion.Correction, feld / 4), Stand(fenster, 1), "Spieler 2 verliert den Anteil, weil er selbst nicht mehr gewinnt.");
        }

        /// <summary>Der Verlauf bei einer Art mit Punkten: die Einzelwerte ändern sich, die Summe nicht - die Anteile bleiben.</summary>
        [TestMethod]
        public async Task TheOwnPointsNeverReachTheCardWhileTheShareStays()
        {
            var fenster = await OeffneAsync(TeamMinigameScoring.Sum, MinigameKind.MentalArithmetic);
            var anna = world.Players[0].Id;
            var bert = world.Players[1].Id;

            fenster.WerteVorschlaege(new Dictionary<Guid, Punktevorschlag> { [anna] = new(80, "Kopfrechnen +80") }, _ => "Satz");
            fenster.WerteVorschlaege(new Dictionary<Guid, Punktevorschlag> { [anna] = new(60, "Kopfrechnen +60"), [bert] = new(20, "Kopfrechnen +20") }, _ => "Satz");

            Assert.AreEqual(40, Karte(fenster, 0).CurrentScoreManipulation, "Spieler 1 bekommt seine eigenen Punkte statt des Anteils.");
            Assert.AreEqual(40, Karte(fenster, 1).CurrentScoreManipulation, "Spieler 2 bekommt seine eigenen Punkte statt des Anteils.");
        }

        /// <summary>Tippsprint, Stoppuhr und Reaktion schlagen „richtig“ vor - im Teamspiel kommt auch dort der Anteil.</summary>
        [TestMethod]
        [DataRow(MinigameKind.TapSprint)]
        [DataRow(MinigameKind.Stopwatch)]
        [DataRow(MinigameKind.Reaction)]
        public async Task TheTimeGamesAreScoredPerTeam(MinigameKind art)
        {
            var fenster = await OeffneAsync(TeamMinigameScoring.Average, art);
            var anna = world.Players[0].Id;
            var carl = world.Players[2].Id;

            if (art == MinigameKind.TapSprint)
                fenster.WerteTippsprint(TippsprintEvaluator.Werte(new Dictionary<Guid, int> { [anna] = 30, [carl] = 30 }));
            else if (art == MinigameKind.Stopwatch)
                fenster.WerteStoppuhr(StoppuhrEvaluator.Werte(new Dictionary<Guid, StoppuhrVersuch> { [anna] = new(5000, true), [carl] = new(5000, true) }, 5));
            else
                fenster.WerteReaktion(ReaktionEvaluator.Werte([new Dictionary<Guid, ReactionAttempt> { [anna] = new(ReactionOutcome.Valid, 300), [carl] = new(ReactionOutcome.Valid, 300) }]));

            Assert.AreEqual((ScoreSuggestion.Correction, feld / 4), Stand(fenster, 0), $"{art}: Spieler 1 bekommt nicht den Anteil.");
            Assert.AreEqual((ScoreSuggestion.Correction, feld / 4), Stand(fenster, 1), $"{art}: Spieler 2 bekommt keinen Anteil.");
            Assert.AreEqual((ScoreSuggestion.Correction, feld), Stand(fenster, 2), $"{art}: Blau allein bekommt nicht das Feld.");
        }

        /// <summary>Eine Art je Person (Räuber) bleibt beim Einzelvorschlag - auch im Teamspiel.</summary>
        [TestMethod]
        public async Task AKindPerPersonKeepsTheOwnSuggestion()
        {
            var fenster = await OeffneAsync(TeamMinigameScoring.Average, MinigameKind.RobberAndGuard);

            fenster.WerteVorschlaege(new Dictionary<Guid, Punktevorschlag>
            {
                [world.Players[0].Id] = new(50, "Raub bei Spieler 3 +50"),
                [world.Players[2].Id] = new(30, "Schild hielt +30"),
            }, _ => "Satz");

            Assert.AreEqual((ScoreSuggestion.Correction, 50), Stand(fenster, 0), "Der Räuber bekommt nicht seinen eigenen Vorschlag.");
            Assert.AreEqual((ScoreSuggestion.None, 0), Stand(fenster, 1), "Der Teamkollege bekommt einen Anteil.");
            Assert.AreEqual("Raub bei Spieler 3 +50", Karte(fenster, 0).Grund);
        }

        /// <summary>
        /// Der Spielstart fragt bei einem Duell ohne Gegner (MinigameChecks.ForTeams) - über den Weg, den der Start wirklich
        /// nimmt; mit Spieler 3 in Blau fragt er nicht.
        /// </summary>
        [TestMethod]
        public async Task ADuelWithoutAnOpponentIsAskedAtTheGameStart()
        {
            await using (var context = new Quizzer.Logic.Context.DataContext())
            {
                context.Set<MinigameQuestion>().Single(q => q.Id == world.Question.Id).Kind = MinigameKind.Duel;
                await context.SaveChangesAsync();
            }

            var prompt = new RecordingUserPrompt(answer: true);
            UserPrompt.Current = prompt;
            var leiter = new Player { Id = Guid.NewGuid(), Designation = "Leo" };
            world.Game.Moderator = leiter;
            world.Game.ModeratorPlayerId = leiter.Id;

            Assert.IsTrue(await GameMasterViewModel.StartbarAsync(world.Game));
            Assert.IsFalse(prompt.Confirms.Any(c => c.Message.Contains("nur gegen Gegner")), "Mit Blau als Gegner meldet der Start ein Duell ohne Gegner.");

            world.Game.PlayerXGames.Single(x => x.PlayerId == world.Players[2].Id).GameTeamId = world.Game.GameTeams[0].Id;

            Assert.IsTrue(await GameMasterViewModel.StartbarAsync(world.Game), "Der Start wurde abgewiesen statt gefragt (E1).");
            StringAssert.Contains(prompt.Confirms[^1].Message, "nur gegen Gegner", "Das Duell ohne Gegner fällt beim Start nicht auf.");
            StringAssert.Contains(prompt.Confirms[^1].Message, "nicht ordentlich spielen");
        }
    }
}
