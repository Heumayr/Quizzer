using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Questions;
using Quizzer.DataModels.Questions.Minispiele;
using Quizzer.DataModels.Questions.Sonderregeln;
using Quizzer.DataModels.Questions.Teams;
using Quizzer.Logic.Controller.TypedControllers;
using Quizzer.Logic.Demo;

namespace Quizzer.LogicUnitTests.Logic
{
    /// <summary>
    /// Der Teamabend (M37 T12, <c>demo-teams</c>): jede Regel, die das Teamspiel anbietet, und jede Art nur im Teamspiel genau
    /// einmal, spielbar nach denselben Prüfungen wie Editor und Spielstart eines Teamspiels; sechs Mitspieler in drei ungleich
    /// großen Teams - und <c>demo-entfernen</c> lässt nichts davon zurück.
    /// </summary>
    [TestClass]
    public class TeamabendUnitTests
    {
        [TestInitialize]
        public void SetUp() => TestDatabase.ClearDiscardedChanges();

        [TestCleanup]
        public async Task TearDown()
        {
            await DemoDataRemover.RemoveAsync();
            TestDatabase.ClearDiscardedChanges();
        }

        private static bool IstTeamart(MinigameKind art)
            => MinigameProfiles.TryFor(art, out var profil) && profil.TeamScoring == MinispielTeamwertung.NurImTeamspiel;

        /// <summary>Das Spiel samt Zellen, Teams und Plätzen, je Zelle die Frage mit Schritten - wie Editor und Spielstart sie laden.</summary>
        private static async Task<(Game Spiel, List<(GameGridCoordinate Zelle, QuestionBase Frage)> Zellen)> LadeAsync(Guid spielId)
        {
            using var spiele = new GamesController();
            using var fragen = new QuestionBasesController();

            var spiel = await spiele.GetAsync(spielId);
            Assert.IsNotNull(spiel, "Der Teamabend wurde nicht geschrieben.");

            var zellen = new List<(GameGridCoordinate, QuestionBase)>();

            foreach (var zelle in spiel!.GameGridCoordinates.OrderBy(z => z.X).ThenBy(z => z.Y))
            {
                Assert.IsTrue(zelle.HatFrage, $"Zelle {zelle.X + 1}/{zelle.Y + 1} ist leer.");
                zellen.Add((zelle, (await fragen.GetAsync(zelle.QuestionBaseId!.Value))!));
            }

            return (spiel, zellen);
        }

        /// <summary>
        /// Jede Regel, die das Teamspiel anbietet - die Teamregeln, das Finale, Einsatz und Risiko -, und jede Art nur im Teamspiel
        /// steht genau einmal im Abend; über die Aufzählungen, nicht über eine abgeschriebene Liste. Eine Regel zählt nur, wo sie
        /// im Teamspiel auch gespielt wird. Dazu Minispiele beider Teamwertungen (zusammengefasst und je Person, M37 T8).
        /// </summary>
        [TestMethod]
        public async Task EveryRuleOfTheTeamGameAndEveryTeamKindIsPlayedExactlyOnce()
        {
            var abend = await DemoDataSeeder.CreateTeamabendAsync();
            var (spiel, zellen) = await LadeAsync(abend.Spiel.Id);

            Assert.IsTrue(spiel.IsTeamGame, "Der Teamabend ist kein Teamspiel.");

            var gespielt = zellen.Where(z => SpecialRuleProfiles.Applies(z.Zelle, z.Frage, true)).Select(z => z.Zelle.SpecialRule).ToList();
            var regeln = Enum.GetValues<CellSpecialRule>()
                .Where(r => r != CellSpecialRule.None && SpecialRuleProfiles.TryFor(r, out var profil) && profil.PasstZu(true));
            var falscheRegeln = regeln.Where(r => gespielt.Count(g => g == r) != 1).Select(r => $"{r} {gespielt.Count(g => g == r)}x").ToList();

            var arten = zellen.Select(z => z.Frage).OfType<MinigameQuestion>().Select(m => m.Kind).ToList();
            var falscheArten = Enum.GetValues<MinigameKind>().Where(IstTeamart)
                .Where(k => arten.Count(a => a == k) != 1).Select(k => $"{k} {arten.Count(a => a == k)}x").ToList();

            Assert.AreEqual(0, falscheRegeln.Count, "Nicht genau einmal gespielt: " + string.Join(", ", falscheRegeln));
            Assert.AreEqual(0, falscheArten.Count, "Nicht genau einmal im Abend: " + string.Join(", ", falscheArten));

            var wertungen = arten.Select(a => MinigameProfiles.For(a).TeamScoring).ToHashSet();
            Assert.IsTrue(wertungen.Contains(MinispielTeamwertung.Zusammengefasst) && wertungen.Contains(MinispielTeamwertung.JePerson),
                "Es fehlt ein Minispiel, das je Team zusammenfasst, oder eins, das je Person wertet.");
        }

        /// <summary>
        /// Jede Zelle besteht, was Editor und Spielstart eines Teamspiels prüfen (<c>GameMasterViewModel.Spielstart</c>): die
        /// Frage selbst, Mitspielerzahl und Teams je Art, die Regel an der Zelle im Teamspiel, die Aufstellung und das eine Finale
        /// - ohne Fehler <b>und</b> ohne Warnung, denn eine Warnung hielte den Start mit einer Rückfrage an.
        /// </summary>
        [TestMethod]
        public async Task EveryCellPassesTheChecksOfEditorAndGameStartInATeamGame()
        {
            var abend = await DemoDataSeeder.CreateTeamabendAsync();
            var (spiel, zellen) = await LadeAsync(abend.Spiel.Id);
            var spielerzahl = spiel.Players.Count();
            var maengel = new List<string>();

            foreach (var (zelle, frage) in zellen)
            {
                var befunde = QuestionValidator.Validate(frage).ToList();

                if (frage is MinigameQuestion minispiel)
                    befunde.AddRange(MinigameChecks.ForPlayers(minispiel, spielerzahl).Concat(MinigameChecks.ForTeams(minispiel, spiel)));

                if (Sonderregelraster.Startbefund(zelle, frage, spiel.IsTeamGame) is { } regel)
                    befunde.Add(regel);

                maengel.AddRange(befunde.Select(b => $"{frage.Designation}: {b.Severity} {b.Message}"));
            }

            maengel.AddRange(Teamzuordnung.Startfehler(spiel).Concat(Teamzuordnung.Startwarnungen(spiel)));
            maengel.AddRange(Sonderregelraster.Spielbefunde(spiel.GameGridCoordinates).Select(b => b.Message));

            Assert.AreEqual(0, maengel.Count, string.Join(Environment.NewLine, maengel));
            Assert.AreEqual(abend.Fragen, zellen.Count, "Nicht jede Frage hat eine Zelle.");
            Assert.IsTrue(zellen.All(z => z.Zelle.CurrentPoints > 0), "Eine Zelle ist null Punkte wert.");
        }

        /// <summary>
        /// Sechs Mitspieler in drei Teams zu 3, 2 und 1 - ungleich groß, damit das Aufteilen sichtbar wird (F-M37-06), eins mit
        /// Rest beim Teilen. Der Spielleiter sitzt nicht am Spiel und braucht kein Team.
        /// </summary>
        [TestMethod]
        public async Task SixPlayersPlayInThreeTeamsOfDifferentSizes()
        {
            var abend = await DemoDataSeeder.CreateTeamabendAsync();
            var (spiel, _) = await LadeAsync(abend.Spiel.Id);

            var teilnehmer = Teamzuordnung.Teilnehmer(spiel);
            var groessen = spiel.GameTeams.Select(t => teilnehmer.Count(x => x.GameTeamId == t.Id)).OrderDescending().ToList();

            Assert.AreEqual(6, teilnehmer.Count, "Nicht sechs Mitspieler.");
            CollectionAssert.AreEqual(new[] { 3, 2, 1 }, groessen, "Die Teams sind nicht 3, 2 und 1 groß.");
            Assert.AreEqual(0, Teamzuordnung.OhneTeam(spiel).Count, "Ein Mitspieler ist ohne Team.");
            Assert.IsNull(TeamGrenzen.AufstellungProblem(spiel.GameTeams), "Die Aufstellung verletzt eine Grenze.");
            Assert.IsFalse(spiel.PlayerXGames.Any(x => x.PlayerId == spiel.ModeratorPlayerId), "Der Spielleiter sitzt am Spiel.");
        }

        /// <summary>Zwei Mitspieler aus verschiedenen Teams halten je einen offenen Joker, einer davon die Sperre - sie trifft nur Gegner.</summary>
        [TestMethod]
        public async Task TwoPlayersOfDifferentTeamsHoldAnOpenJoker()
        {
            var abend = await DemoDataSeeder.CreateTeamabendAsync();
            var (spiel, _) = await LadeAsync(abend.Spiel.Id);

            using var ctrl = new PlayerJokersController();
            var joker = await ctrl.GetForGameAsync(spiel.Id);

            Assert.AreEqual(2, joker.Count, "Es sind nicht genau zwei Joker vergeben.");
            Assert.AreEqual(1, joker.Count(j => j.Kind == JokerKind.Block), "Es gibt nicht genau eine Sperre.");
            Assert.AreEqual(2, joker.Select(j => Teamzuordnung.TeamVon(spiel, j.PlayerId)).OfType<Guid>().Distinct().Count(),
                "Die Joker liegen nicht in zwei Teams.");
        }

        /// <summary>
        /// <c>demo-entfernen</c> lässt nichts zurück - auch keine Teams (NO ACTION am Spiel) und nicht die beiden zusätzlichen
        /// Mitspieler. Ein zweiter Lauf legt sie nicht noch einmal an (V13).
        /// </summary>
        [TestMethod]
        public async Task ASecondRunReusesThePlayersAndRemovingLeavesNoTeams()
        {
            var erster = await DemoDataSeeder.CreateTeamabendAsync();
            var zweiter = await DemoDataSeeder.CreateTeamabendAsync();

            using (var spieler = new PlayersController())
            {
                var demo = (await spieler.GetAllAsync()).Where(p => p.Designation.StartsWith(DemoDataSeeder.Marke, StringComparison.Ordinal)).ToList();
                Assert.AreEqual(1, demo.Count(p => p.DisplayName == "Emil"), "Emil steht doppelt da.");
                Assert.AreEqual(1, demo.Count(p => p.DisplayName == "Frieda"), "Frieda steht doppelt da.");
            }

            StringAssert.EndsWith(zweiter.Spiel.Designation, $"{DemoDataSeeder.TeamabendName} 2");

            var entfernt = await DemoDataRemover.RemoveAsync();
            Assert.AreEqual(2, entfernt.Spiele);

            using var teams = new GameTeamsController();
            using var uebrig = new PlayersController(teams);

            foreach (var abend in new[] { erster, zweiter })
                Assert.AreEqual(0, (await teams.GetForGameAsync(abend.Spiel.Id)).Count, "Teams eines entfernten Teamabends sind übrig.");

            Assert.AreEqual(0, (await uebrig.GetAllAsync()).Count(p => p.Designation.StartsWith(DemoDataSeeder.Marke, StringComparison.Ordinal)),
                "Demo-Spieler sind übrig.");
        }
    }
}
