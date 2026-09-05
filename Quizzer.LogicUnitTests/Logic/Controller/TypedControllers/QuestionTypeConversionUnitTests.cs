using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Helpers;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Questions;
using Quizzer.Logic.Controller.TypedControllers;

namespace Quizzer.LogicUnitTests.Logic.Controller.TypedControllers
{
    /// <summary>
    /// Das Umwandeln einer Frage in einen anderen Typ. Der springende Punkt: Schritte und
    /// bisherige Spielergebnisse muessen das ueberstehen - beim naheliegenden Weg ueber
    /// Loeschen und Neuanlegen taeten sie das nicht.
    /// </summary>
    [TestClass]
    public class QuestionTypeConversionUnitTests
    {
        private Category category = null!;
        private readonly List<Guid> createdQuestions = new();

        [TestInitialize]
        public async Task CreateCategory()
        {
            using var ctrl = new CategoriesController();
            category = new Category { Id = Guid.NewGuid(), Designation = $"Kat-{Guid.NewGuid():N}" };
            await ctrl.InsertAsync(category);
            await ctrl.SaveChangesAsync();
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            using (var ctrl = new QuestionBasesController())
            {
                foreach (var id in createdQuestions)
                    await ctrl.DeleteAsync(id);

                await ctrl.SaveChangesAsync();
            }

            using (var ctrl = new CategoriesController())
            {
                await ctrl.DeleteAsync(category.Id);
                await ctrl.SaveChangesAsync();
            }
        }

        private async Task<QuestionBase> CreateAsync(QuestionType typ, int stepCount = 2)
        {
            var question = Factory.CreateNewQuestion(typ);
            question.Id = Guid.NewGuid();
            question.Designation = $"Frage-{Guid.NewGuid():N}";
            question.DesignationShort = "F";
            question.CategoryId = category.Id;
            question.Points = 250;
            question.Notes = "Notiz des Spielleiters";

            for (var i = 0; i < stepCount; i++)
            {
                question.Steps.Add(new QuestionStepResource
                {
                    Id = Guid.NewGuid(),
                    Designation = $"Schritt {i + 1}",
                    StepText = $"Schritt {i + 1}",
                    SequenceNumber = (i + 1) * 10,
                    IsResult = i == 0,
                });
            }

            createdQuestions.Add(question.Id);

            using var ctrl = new QuestionBasesController();
            await ctrl.SaveWithStepsAsync(question);
            await ctrl.SaveChangesAsync();

            return question;
        }

        [TestMethod]
        [DataRow(QuestionType.Default, QuestionType.MultipleChoice)]
        [DataRow(QuestionType.MultipleChoice, QuestionType.Default)]
        [DataRow(QuestionType.Default, QuestionType.Properties)]
        [DataRow(QuestionType.Properties, QuestionType.Appreciate)]
        [DataRow(QuestionType.Appreciate, QuestionType.Default)]
        public async Task ConvertingSwapsTheConcreteType(QuestionType from, QuestionType to)
        {
            var question = await CreateAsync(from);

            using var ctrl = new QuestionBasesController();
            var converted = await ctrl.ConvertTypeAsync(question.Id, to);

            Assert.AreEqual(to, converted.Typ);
            Assert.AreEqual(question.Id, converted.Id, "Die Id bleibt - daran haengt alles andere.");
        }

        [TestMethod]
        public async Task ConvertingKeepsTheStepsAndTheirContent()
        {
            var question = await CreateAsync(QuestionType.Default, stepCount: 3);

            using var ctrl = new QuestionBasesController();
            var converted = await ctrl.ConvertTypeAsync(question.Id, QuestionType.MultipleChoice);

            Assert.AreEqual(3, converted.Steps.Count);
            CollectionAssert.AreEquivalent(
                new[] { "Schritt 1", "Schritt 2", "Schritt 3" },
                converted.Steps.Select(s => s.Designation).ToArray());
            Assert.AreEqual(1, converted.Steps.Count(s => s.IsResult),
                "Die Loesungsmarkierung ueberlebt den Wechsel.");
        }

        [TestMethod]
        public async Task ConvertingKeepsTheHeaderData()
        {
            var question = await CreateAsync(QuestionType.Default);

            using var ctrl = new QuestionBasesController();
            var converted = await ctrl.ConvertTypeAsync(question.Id, QuestionType.Properties);

            Assert.AreEqual(question.Designation, converted.Designation);
            Assert.AreEqual(250, converted.Points);
            Assert.AreEqual("Notiz des Spielleiters", converted.Notes);
            Assert.AreEqual(category.Id, converted.CategoryId);
        }

        [TestMethod]
        public async Task ConvertingAppliesTheTargetTypeDefaults()
        {
            var question = await CreateAsync(QuestionType.Default);

            using var ctrl = new QuestionBasesController();
            var converted = await ctrl.ConvertTypeAsync(question.Id, QuestionType.MultipleChoice);

            var profile = QuestionTypeProfiles.For(QuestionType.MultipleChoice);

            Assert.IsTrue(profile.MatchesOwnedValues(converted),
                "Nach dem Wechsel muessen die typeigenen Werte zum neuen Typ passen.");
            Assert.AreEqual(BuzzerControlsLayout.KeySelect, converted.BuzzerControlsLayout);
            Assert.AreEqual(StepDisplayLayoutMode.Grid, converted.StepDisplayLayoutMode);
        }

        /// <summary>
        /// Der Grund, warum nicht geloescht und neu angelegt wird: QuestionResult haengt mit
        /// Cascade an der Frage.
        /// </summary>
        [TestMethod]
        public async Task ConvertingKeepsThePlayedResults()
        {
            var question = await CreateAsync(QuestionType.Default);

            var player = new Player { Id = Guid.NewGuid(), Designation = $"S-{Guid.NewGuid():N}" };
            using (var ctrl = new PlayersController())
            {
                await ctrl.InsertAsync(player);
                await ctrl.SaveChangesAsync();
            }

            var game = new Game { Id = Guid.NewGuid(), Designation = $"Spiel-{Guid.NewGuid():N}" };
            var coordinate = new GameGridCoordinate
            {
                Id = Guid.NewGuid(),
                GameId = game.Id,
                Game = game,
                QuestionBaseId = question.Id,
            };

            using (var ctrl = new GamesController())
            {
                await ctrl.UpsertAsync(game);
                await ctrl.SaveChangesAsync();
            }

            using (var ctrl = new GameGridCoordinatesController())
            {
                await ctrl.InsertAsync(coordinate);
                await ctrl.SaveChangesAsync();
            }

            var resultId = Guid.NewGuid();
            using (var ctrl = new QuestionResultsController())
            {
                await ctrl.InsertAsync(new QuestionResult
                {
                    Id = resultId,
                    GameId = game.Id,
                    PlayerId = player.Id,
                    QuestionBaseId = question.Id,
                    GameGridCoordinateId = coordinate.Id,
                    Score = 250,
                });

                await ctrl.SaveChangesAsync();
            }

            try
            {
                using var ctrl = new QuestionBasesController();
                await ctrl.ConvertTypeAsync(question.Id, QuestionType.Properties);

                using var resultCtrl = new QuestionResultsController();
                var stillThere = await resultCtrl.GetAsync(resultId);

                Assert.IsNotNull(stillThere, "Die Spielhistorie darf beim Umwandeln nicht fallen.");
                Assert.AreEqual(250, stillThere.Score);
            }
            finally
            {
                using (var ctrl = new GamesController())
                {
                    await ctrl.DeleteAsync(game.Id);
                    await ctrl.SaveChangesAsync();
                }

                using (var ctrl = new PlayersController())
                {
                    await ctrl.DeleteAsync(player.Id);
                    await ctrl.SaveChangesAsync();
                }
            }
        }

        [TestMethod]
        public async Task ConvertingToTheSameTypeChangesNothing()
        {
            var question = await CreateAsync(QuestionType.Properties);

            using var ctrl = new QuestionBasesController();
            var converted = await ctrl.ConvertTypeAsync(question.Id, QuestionType.Properties);

            Assert.AreEqual(QuestionType.Properties, converted.Typ);
            Assert.AreEqual(2, converted.Steps.Count);
        }

        [TestMethod]
        public async Task ConvertingToAppreciateStartsWithAnEmptyExpectedValue()
        {
            var question = await CreateAsync(QuestionType.Default);

            using var ctrl = new QuestionBasesController();
            var converted = await ctrl.ConvertTypeAsync(question.Id, QuestionType.Appreciate);

            var appreciate = converted as AppreciateQestion;

            Assert.IsNotNull(appreciate);
            Assert.AreEqual(0d, appreciate.ExpectedValue);
            Assert.IsNull(appreciate.ExpectedDate);
        }

        [TestMethod]
        public async Task ConvertingAnUnknownQuestionFails()
        {
            using var ctrl = new QuestionBasesController();

            await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                () => ctrl.ConvertTypeAsync(Guid.NewGuid(), QuestionType.Default));
        }

        [TestMethod]
        public async Task ConvertingWithoutAnIdFails()
        {
            using var ctrl = new QuestionBasesController();

            await Assert.ThrowsExactlyAsync<ArgumentException>(
                () => ctrl.ConvertTypeAsync(Guid.Empty, QuestionType.Default));
        }

        [TestMethod]
        public void TheEffectsAreSpelledOutBeforehand()
        {
            var effects = QuestionBasesController.DescribeConversionEffects(
                QuestionType.MultipleChoice, QuestionType.Default);

            Assert.IsTrue(effects.Any(e => e.Contains("Lösungsmarkierungen")),
                "Die stille Verhaltensaenderung muss benannt werden.");
            Assert.IsTrue(effects.Any(e => e.Contains("unberührt")));
        }

        [TestMethod]
        public void ConvertingAwayFromAppreciateWarnsAboutTheExpectedValue()
        {
            var effects = QuestionBasesController.DescribeConversionEffects(
                QuestionType.Appreciate, QuestionType.Default);

            Assert.IsTrue(effects.Any(e => e.Contains("Sollwert")));
        }

        [TestMethod]
        public void ConvertingToTheSameTypeHasNoEffects()
        {
            var effects = QuestionBasesController.DescribeConversionEffects(
                QuestionType.Default, QuestionType.Default);

            Assert.AreEqual(0, effects.Count);
        }
        /// <summary>
        /// Der Tabellenname geht in den SQL-Text, weil er dort kein Parameter sein kann. Eine
        /// Weissliste laesst nur die Untertabellen der Fragetypen durch.
        /// </summary>
        [TestMethod]
        public void EveryProfileTableNameIsAccepted()
        {
            foreach (var profile in QuestionTypeProfiles.All)
            {
                Assert.AreEqual(profile.TableName,
                    QuestionBasesController.EnsureKnownTable(profile.TableName),
                    $"Der Tabellenname des Profils {profile.Typ} kommt nicht durch die Weissliste.");
            }
        }

        /// <summary>
        /// Die Gegenrichtung, und sie ist der Zweck der Weissliste: alles andere wird abgewiesen,
        /// bevor es in eine Anweisung wandert.
        /// </summary>
        [TestMethod]
        [DataRow("QuestionBase")]
        [DataRow("Player")]
        [DataRow("Foo]; DROP TABLE [question].[QuestionBase]--")]
        [DataRow("")]
        public void AnythingElseIsRejected(string tableName)
        {
            var fehler = Assert.ThrowsExactly<InvalidOperationException>(
                () => QuestionBasesController.EnsureKnownTable(tableName));

            StringAssert.Contains(fehler.Message, "Unbekannte Fragetabelle");
        }
    }
}
