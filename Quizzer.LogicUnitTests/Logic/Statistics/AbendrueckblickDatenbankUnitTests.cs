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
    /// Der Abendrückblick aus der Datenbank (M37 E04): dieselbe Zeilenabfrage wie die Bestenliste, nur für dieses Spiel,
    /// dazu Ergebnisse, Joker und Namen. Ein besserer Wert eines anderen Spiels in derselben Datenbank bleibt draußen.
    /// </summary>
    [TestClass]
    public class AbendrueckblickDatenbankUnitTests
    {
        private static readonly DateTimeOffset Abend = new(2026, 11, 16, 20, 0, 0, TimeSpan.FromHours(1));

        private static async Task<Player> SpielerAsync(string name)
        {
            var spieler = new Player { Id = Guid.NewGuid(), Designation = name + " " + Guid.NewGuid().ToString("N")[..6], DisplayName = name };
            using var ctrl = new PlayersController();
            await ctrl.InsertAsync(spieler);
            await ctrl.SaveChangesAsync();
            return spieler;
        }

        /// <summary>Ein Spiel mit einer Reaktionszelle; je Spieler Rohwert, Klaus und richtige Antworten.</summary>
        private static async Task<(Game Spiel, GameGridCoordinate Zelle)> SpielAsync(QuestionBase frage, params (Player Spieler, int Rohwert, int Klaus, int Richtige)[] ergebnisse)
        {
            var spiel = new Game { Id = Guid.NewGuid(), Designation = "Rückblick " + Guid.NewGuid().ToString("N")[..6], Phase = 1, SuggestedPhases = 1 };
            using (var ctrl = new GamesController())
            {
                await ctrl.UpsertAsync(spiel);
                await ctrl.SaveChangesAsync();
            }

            var zelle = new GameGridCoordinate { Id = Guid.NewGuid(), GameId = spiel.Id, QuestionBaseId = frage.Id, IsDone = true, PlayedAt = Abend };
            using var ctrlZellen = new GameGridCoordinatesController();
            using var ctrlErgebnis = new QuestionResultsController(ctrlZellen);
            await ctrlZellen.InsertAsync(zelle);

            foreach (var (spieler, rohwert, klaus, richtige) in ergebnisse)
            {
                await ctrlErgebnis.InsertAsync(new QuestionResult
                {
                    Id = Guid.NewGuid(), GameId = spiel.Id, PlayerId = spieler.Id, GameGridCoordinateId = zelle.Id, QuestionBaseId = frage.Id,
                    RecordValue = rohwert, RecordedAt = Abend, Steals = klaus, RightCount = richtige, Score = 100 * richtige,
                });
            }

            await ctrlZellen.SaveChangesAsync();
            return (spiel, zelle);
        }

        [TestMethod]
        public async Task TheReviewComesFromThisGameOnly()
        {
            var kategorie = new Category { Id = Guid.NewGuid(), Designation = "Rückblick" };
            using (var ctrl = new CategoriesController())
            {
                await ctrl.InsertAsync(kategorie);
                await ctrl.SaveChangesAsync();
            }

            var frage = new MinigameQuestion { Id = Guid.NewGuid(), Designation = "Reaktion Rückblick", DesignationShort = "R", CategoryId = kategorie.Id, Points = 100 };
            MinigameProfiles.For(MinigameKind.Reaction).ApplyDefaults(frage);
            using (var ctrl = new QuestionBasesController())
            {
                await ctrl.UpsertAsync(frage);
                await ctrl.SaveChangesAsync();
            }

            var anna = await SpielerAsync("Anna");
            var bert = await SpielerAsync("Bert");
            var carl = await SpielerAsync("Carl");

            var (spiel, zelle) = await SpielAsync(frage, (anna, 240, 1, 2), (bert, 199, 0, 1));
            await SpielAsync(frage, (carl, 150, 5, 9));

            using (var ctrl = new PlayerJokersController())
            {
                await ctrl.InsertAsync(new PlayerJoker
                {
                    Id = Guid.NewGuid(), GameId = spiel.Id, PlayerId = bert.Id, Kind = JokerKind.Block, Reason = "Probe",
                    GrantedAt = Abend, GameGridCoordinateId = zelle.Id, RedeemedAt = Abend, TargetPlayerId = anna.Id,
                });
                await ctrl.SaveChangesAsync();
            }

            Quizzer.Logic.Statistics.Rueckblick r;
            using (var ctrl = new QuestionResultsController())
                r = await ctrl.AbendrueckblickAsync(spiel.Id);

            Assert.AreEqual("Schnellste Reaktion: Bert – 199 ms", r.Bestwerte.Single().Zeile, "Carls 150 ms aus dem anderen Spiel zaehlen mit.");
            Assert.AreEqual("Anna – 1 Klau", r.MeisteKlaus?.Zeile);
            Assert.AreEqual("Bert: Sperre gegen Anna", r.Joker.Single().Zeile);
            Assert.AreEqual("Anna – 2 richtige Antworten", r.MeisteRichtige?.Zeile);
        }
    }
}
