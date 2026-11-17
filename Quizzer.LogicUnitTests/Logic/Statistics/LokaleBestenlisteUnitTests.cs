using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Questions.Minispiele;
using Quizzer.Logic.Controller.TypedControllers;

namespace Quizzer.LogicUnitTests.Logic.Statistics
{
    /// <summary>
    /// Die Bestenliste ohne Server (M37 E03): im Einzelbetrieb ueber alle Spiele der lokalen Datenbank, egal wer sie
    /// leitet - es gibt kein Konto, das filtern koennte.
    /// </summary>
    [TestClass]
    public class LokaleBestenlisteUnitTests
    {
        private static readonly DateTimeOffset Frueh = new(2001, 1, 1, 20, 0, 0, TimeSpan.Zero);

        private static async Task<MinigameQuestion> MinispielAsync(MinigameKind art, Guid kategorie)
        {
            var frage = new MinigameQuestion { Id = Guid.NewGuid(), Designation = "Lokal " + art, DesignationShort = "L", CategoryId = kategorie, Points = 100 };
            MinigameProfiles.For(art).ApplyDefaults(frage);

            using var ctrl = new QuestionBasesController();
            await ctrl.UpsertAsync(frage);
            await ctrl.SaveChangesAsync();
            return frage;
        }

        /// <summary>Ein Spiel mit einer Zelle zu dieser Frage und je Spieler einem Ergebnis.</summary>
        private static async Task<Game> SpielAsync(string name, Guid moderator, QuestionBase frage, params (Player Spieler, int? Rohwert, int Klaus)[] ergebnisse)
        {
            var spiel = new Game { Id = Guid.NewGuid(), Designation = name, Phase = 1, SuggestedPhases = 1, ModeratorPlayerId = moderator };
            using (var ctrl = new GamesController())
            {
                await ctrl.UpsertAsync(spiel);
                await ctrl.SaveChangesAsync();
            }

            var zelle = new GameGridCoordinate { Id = Guid.NewGuid(), GameId = spiel.Id, X = 0, Y = 0, Phase = 1, QuestionBaseId = frage.Id, IsDone = true };
            using var ctrlZellen = new GameGridCoordinatesController();
            using var ctrlErgebnis = new QuestionResultsController(ctrlZellen);
            await ctrlZellen.InsertAsync(zelle);

            foreach (var (spieler, rohwert, klaus) in ergebnisse)
            {
                await ctrlErgebnis.InsertAsync(new QuestionResult
                {
                    Id = Guid.NewGuid(), GameId = spiel.Id, PlayerId = spieler.Id, GameGridCoordinateId = zelle.Id, QuestionBaseId = frage.Id,
                    Score = 100, RecordValue = rohwert, RecordedAt = rohwert == null ? null : Frueh, Steals = klaus,
                });
            }

            await ctrlZellen.SaveChangesAsync();
            return spiel;
        }

        [TestMethod]
        public async Task InSingleModeTheRecordsOfEveryLocalGameAppear()
        {
            var kategorie = new Category { Id = Guid.NewGuid(), Designation = "Lokale Bestenliste" };
            using (var ctrl = new CategoriesController())
            {
                await ctrl.InsertAsync(kategorie);
                await ctrl.SaveChangesAsync();
            }

            var spieler = new[] { "Leiterin", "Anna", "Bert", "Carl" }
                .Select(n => new Player { Id = Guid.NewGuid(), Designation = n + " " + Guid.NewGuid().ToString("N")[..6] }).ToList();
            using (var ctrl = new PlayersController())
            {
                foreach (var p in spieler)
                    await ctrl.InsertAsync(p);
                await ctrl.SaveChangesAsync();
            }

            var reaktion = await MinispielAsync(MinigameKind.Reaction, kategorie.Id);
            var tipps = await MinispielAsync(MinigameKind.TapSprint, kategorie.Id);
            var record = MinigameProfiles.For(MinigameKind.Reaction).Record!;

            // Zwei Leiter: im Einzelbetrieb filtert niemand nach dem angemeldeten Spielleiter.
            var spielA = await SpielAsync("Abend A " + Guid.NewGuid().ToString("N")[..6], spieler[0].Id, reaktion, (spieler[1], record.Min, 0));
            var spielB = await SpielAsync("Abend B " + Guid.NewGuid().ToString("N")[..6], spieler[1].Id, tipps, (spieler[2], 900, 0), (spieler[3], null, 2));

            Quizzer.DataModels.Transfer.Server.Leaderboard liste;
            using (var ctrl = new QuestionResultsController())
                liste = await ctrl.BestenlisteAsync();

            var reaktionen = liste.Categories.Single(c => c.Kind == MinigameKind.Reaction).Entries;
            var schnellste = liste.Categories.Single(c => c.Kind == MinigameKind.TapSprint).Entries;

            Assert.IsTrue(reaktionen.Any(e => e.PlayerId == spieler[1].Id && e.GameName == spielA.Designation), "Der Rekord aus Abend A fehlt.");
            Assert.IsTrue(schnellste.Any(e => e.PlayerId == spieler[2].Id && e.GameName == spielB.Designation), "Der Rekord aus Abend B fehlt.");
            Assert.IsTrue(liste.MostSteals.Any(s => s.PlayerId == spieler[3].Id && s.Steals == 2), "Die Klaus aus Abend B fehlen.");
        }
    }
}
