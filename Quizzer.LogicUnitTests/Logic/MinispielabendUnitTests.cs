using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Questions;
using Quizzer.DataModels.Questions.Minispiele;
using Quizzer.DataModels.Questions.Sonderregeln;
using Quizzer.Logic.Controller.TypedControllers;
using Quizzer.Logic.Demo;

namespace Quizzer.LogicUnitTests.Logic
{
    /// <summary>
    /// Der Minispielabend (M35 C8): jede Art und jede Sonderregel, spielbar nach denselben Prüfungen wie Editor und
    /// Spielstart, zwei offene Joker - und <c>demo-entfernen</c> lässt nichts davon zurück.
    /// </summary>
    [TestClass]
    public class MinispielabendUnitTests
    {
        [TestInitialize]
        public void SetUp() => TestDatabase.ClearDiscardedChanges();

        [TestCleanup]
        public async Task TearDown()
        {
            await DemoDataRemover.RemoveAsync();
            TestDatabase.ClearDiscardedChanges();
        }

        /// <summary>Das Spiel samt Zellen und je Zelle die Frage mit Schritten, wie Editor und Spielstart sie laden.</summary>
        private static async Task<(Game Spiel, List<(GameGridCoordinate Zelle, QuestionBase Frage)> Zellen)> LadeAsync(Guid spielId)
        {
            using var spiele = new GamesController();
            using var fragen = new QuestionBasesController();

            var spiel = await spiele.GetAsync(spielId);
            Assert.IsNotNull(spiel, "Der Minispielabend wurde nicht geschrieben.");

            var zellen = new List<(GameGridCoordinate, QuestionBase)>();

            foreach (var zelle in spiel!.GameGridCoordinates.OrderBy(z => z.X).ThenBy(z => z.Y))
            {
                Assert.IsTrue(zelle.HatFrage, $"Zelle {zelle.X + 1}/{zelle.Y + 1} ist leer.");
                zellen.Add((zelle, (await fragen.GetAsync(zelle.QuestionBaseId!.Value))!));
            }

            return (spiel, zellen);
        }

        /// <summary>
        /// Jede Art (ausser 0) und jede Sonderregel (ausser „Keine") steht im Abend - über die Aufzählungen, nicht über
        /// eine abgeschriebene Liste. Eine Regel zählt nur, wo sie auch gespielt wird: auf einer gewöhnlichen Frage.
        /// </summary>
        [TestMethod]
        public async Task EveryMinigameKindAndEverySpecialRuleIsPlayedInTheEvening()
        {
            var abend = await DemoDataSeeder.CreateMinispielabendAsync();
            var (_, zellen) = await LadeAsync(abend.Spiel.Id);

            var arten = zellen.Select(z => z.Frage).OfType<MinigameQuestion>().Select(m => m.Kind).ToHashSet();
            var fehlendeArten = Enum.GetValues<MinigameKind>().Where(k => (int)k != 0 && !arten.Contains(k)).ToList();

            var regeln = zellen.Where(z => SpecialRuleProfiles.Applies(z.Zelle, z.Frage)).Select(z => z.Zelle.SpecialRule).ToHashSet();
            var fehlendeRegeln = Enum.GetValues<CellSpecialRule>().Where(r => r != CellSpecialRule.None && !regeln.Contains(r)).ToList();

            Assert.AreEqual(0, fehlendeArten.Count, "Ohne Demozelle: " + string.Join(", ", fehlendeArten));
            Assert.AreEqual(0, fehlendeRegeln.Count, "Ohne gespielte Demozelle: " + string.Join(", ", fehlendeRegeln));
        }

        /// <summary>
        /// Jede Zelle besteht, was Editor und Spielstart prüfen: <see cref="QuestionValidator"/> (darin
        /// <see cref="MinigameChecks.Content"/>), die Mitspielerzahl je Art und die Sonderregel an der Zelle - ohne
        /// Fehler <b>und</b> ohne Warnung, denn eine Warnung hielte den Spielstart mit einer Rückfrage an.
        /// </summary>
        [TestMethod]
        public async Task EveryCellPassesTheChecksOfEditorAndGameStart()
        {
            var abend = await DemoDataSeeder.CreateMinispielabendAsync();
            var (spiel, zellen) = await LadeAsync(abend.Spiel.Id);
            var spielerzahl = spiel.Players.Count();
            var maengel = new List<string>();

            foreach (var (zelle, frage) in zellen)
            {
                var befunde = QuestionValidator.Validate(frage).ToList();

                if (frage is MinigameQuestion minispiel)
                    befunde.AddRange(MinigameChecks.ForPlayers(minispiel, spielerzahl));

                if (Sonderregelraster.Startbefund(zelle, frage) is { } regel)
                    befunde.Add(regel);

                maengel.AddRange(befunde.Select(b => $"{frage.Designation}: {b.Severity} {b.Message}"));
            }

            Assert.AreEqual(0, maengel.Count, string.Join(Environment.NewLine, maengel));
            Assert.AreEqual(abend.Fragen, zellen.Count, "Nicht jede Frage hat eine Zelle.");
            Assert.IsTrue(zellen.All(z => z.Zelle.CurrentPoints > 0), "Eine Zelle ist null Punkte wert.");
        }

        /// <summary>
        /// Die Mitspieler reichen für die anspruchsvollste Art - aus den Profilen gelesen. Und es gibt gewöhnliche
        /// Fragen ohne Sonderregel: nur dort öffnet die Joker-Runde (<c>CurrentQuestionViewModel.StarteJokerRundeAsync</c>).
        /// </summary>
        [TestMethod]
        public async Task ThePlayersSufficeAndThereIsRoomForTheJokerRound()
        {
            var abend = await DemoDataSeeder.CreateMinispielabendAsync();
            var (spiel, zellen) = await LadeAsync(abend.Spiel.Id);

            Assert.IsTrue(spiel.Players.Count() >= MinigameProfiles.All.Max(p => p.MinPlayers),
                $"{spiel.Players.Count()} Mitspieler - die anspruchsvollste Art braucht {MinigameProfiles.All.Max(p => p.MinPlayers)}.");

            var jokerZellen = zellen.Count(z => z.Frage.Typ != QuestionType.Minigame && z.Zelle.SpecialRule == CellSpecialRule.None);
            Assert.IsTrue(jokerZellen >= 2, $"Nur {jokerZellen} Zelle(n), an denen die Joker-Runde aufgeht.");

            var themen = spiel.Columns.Select(c => c.Designation).ToList();
            Assert.AreEqual(spiel.Width, themen.Distinct().Count(t => !string.IsNullOrWhiteSpace(t)), "Jede Spalte braucht ihr eigenes Thema.");
        }

        /// <summary>Zwei Mitspieler des Abends halten je einen offenen Joker, einer davon die Sperre, Grund „Demo".</summary>
        [TestMethod]
        public async Task TwoPlayersHoldAnOpenJokerOneOfThemABlock()
        {
            var abend = await DemoDataSeeder.CreateMinispielabendAsync();
            var (spiel, _) = await LadeAsync(abend.Spiel.Id);

            using var ctrl = new PlayerJokersController();
            var joker = await ctrl.GetForGameAsync(spiel.Id);
            var mitspieler = spiel.Players.Select(p => p.Id).ToHashSet();

            Assert.AreEqual(2, joker.Count, "Es sind nicht genau zwei Joker vergeben.");
            Assert.AreEqual(2, joker.Select(j => j.PlayerId).Distinct().Count(), "Die Joker liegen nicht bei zwei Spielern.");
            Assert.IsTrue(joker.All(j => mitspieler.Contains(j.PlayerId)), "Ein Joker liegt bei jemandem, der nicht mitspielt.");
            Assert.IsTrue(joker.All(j => !j.IsRedeemed), "Ein Joker ist schon eingelöst.");
            Assert.AreEqual(1, joker.Count(j => j.Kind == JokerKind.Block), "Es gibt nicht genau eine Sperre.");
            Assert.IsTrue(joker.All(j => j.Reason == DemoDataSeeder.DemoJokerGrund), "Ein Joker trägt nicht den Grund „Demo“.");
        }

        /// <summary>
        /// <c>demo-entfernen</c> lässt nichts zurück: kein Spiel, keine Joker (sie hängen mit NO ACTION am Spiel), keine
        /// Demo-Spieler und keine Fragen oder Kategorien mit der Marke.
        /// </summary>
        [TestMethod]
        public async Task RemovingTheDemoLeavesNoRemains()
        {
            var abend = await DemoDataSeeder.CreateMinispielabendAsync();

            var entfernt = await DemoDataRemover.RemoveAsync();

            Assert.AreEqual(1, entfernt.Spiele);
            Assert.AreEqual(abend.Fragen, entfernt.Fragen);

            using var spiele = new GamesController();
            using var joker = new PlayerJokersController(spiele);
            using var fragen = new QuestionBasesController(spiele);
            using var spieler = new PlayersController(spiele);
            using var kategorien = new CategoriesController(spiele);

            Assert.IsNull(await spiele.GetAsync(abend.Spiel.Id), "Das Spiel steht noch.");
            Assert.AreEqual(0, (await joker.GetForGameAsync(abend.Spiel.Id)).Count, "Joker des Spiels sind übrig.");

            foreach (var j in abend.Joker)
                Assert.IsNull(await joker.GetAsync(j.Id), "Ein Demo-Joker ist übrig.");

            static bool Demo(string? d) => d?.StartsWith(DemoDataSeeder.Marke, StringComparison.Ordinal) == true;

            Assert.AreEqual(0, (await spieler.GetAllAsync()).Count(p => Demo(p.Designation)), "Demo-Spieler sind übrig.");
            Assert.AreEqual(0, (await fragen.GetAllAsync()).Count(q => Demo(q.Designation)), "Demo-Fragen sind übrig.");
            Assert.AreEqual(0, (await kategorien.GetAllAsync()).Count(c => Demo(c.Designation)), "Demo-Kategorien sind übrig.");
        }
    }
}
