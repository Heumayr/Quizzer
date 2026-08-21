using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Helpers;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.Logic.Controller.TypedControllers;

namespace Quizzer.UnitTests.PlayThrough
{
    /// <summary>
    /// Legt ein vollstaendiges, spielbares Spiel in der Testdatenbank an: Kategorie, Frage samt
    /// Schritten, Spieler, Spielfeldzelle. Jeder Aufruf erzeugt eigene Bezeichnungen, damit sich
    /// zwei Tests nicht in die Quere kommen.
    /// </summary>
    public sealed class TestGameBuilder : IAsyncDisposable
    {
        private readonly List<Func<Task>> cleanups = new();

        public Category Category { get; private set; } = null!;

        public Game Game { get; private set; } = null!;

        public QuestionBase Question { get; private set; } = null!;

        public List<Player> Players { get; } = new();

        public GameGridCoordinate Coordinate { get; private set; } = null!;

        public static async Task<TestGameBuilder> CreateAsync(
            QuestionType questionType = QuestionType.Default,
            int normalStepCount = 2,
            int playerCount = 2)
        {
            var builder = new TestGameBuilder();
            await builder.BuildAsync(questionType, normalStepCount, playerCount);
            return builder;
        }

        private async Task BuildAsync(QuestionType questionType, int normalStepCount, int playerCount)
        {
            var tag = Guid.NewGuid().ToString("N")[..8];

            using (var ctrl = new CategoriesController())
            {
                Category = new Category { Id = Guid.NewGuid(), Designation = $"Kat-{tag}" };
                await ctrl.InsertAsync(Category);
                await ctrl.SaveChangesAsync();
            }
            cleanups.Add(async () =>
            {
                using var ctrl = new CategoriesController();
                await ctrl.DeleteAsync(Category.Id);
                await ctrl.SaveChangesAsync();
            });

            Question = Factory.CreateNewQuestion(questionType);
            Question.Id = Guid.NewGuid();
            Question.Designation = $"Frage-{tag}";
            Question.DesignationShort = "F";
            Question.QuestionText = "Wie hoch ist der Grossglockner?";
            Question.CategoryId = Category.Id;
            Question.Points = 100;
            Question.MinusPoints = 50;

            using (var ctrl = new QuestionBasesController())
            {
                await ctrl.UpsertAsync(Question);
                await ctrl.SaveChangesAsync();
            }
            cleanups.Insert(0, async () =>
            {
                using var ctrl = new QuestionBasesController();
                await ctrl.DeleteAsync(Question.Id);
                await ctrl.SaveChangesAsync();
            });

            // Schritte einzeln - UpsertAsync der Frage speichert sie heute nicht mit.
            using (var ctrl = new QuestionStepResourcesController())
            {
                for (var i = 0; i < normalStepCount; i++)
                {
                    await ctrl.InsertAsync(new QuestionStepResource
                    {
                        Id = Guid.NewGuid(),
                        QuestionBaseId = Question.Id,
                        Designation = $"Hinweis {i + 1}",
                        StepText = $"Hinweis {i + 1}",
                        SequenceNumber = (i + 1) * 10,
                    });
                }

                await ctrl.InsertAsync(new QuestionStepResource
                {
                    Id = Guid.NewGuid(),
                    QuestionBaseId = Question.Id,
                    Designation = "Aufloesung",
                    StepText = "3798 Meter",
                    SequenceNumber = 900,
                    IsResult = true,
                    IsFinish = true,
                });

                await ctrl.SaveChangesAsync();
            }

            using (var ctrl = new PlayersController())
            {
                for (var i = 0; i < playerCount; i++)
                {
                    var player = new Player
                    {
                        Id = Guid.NewGuid(),
                        Designation = $"Spieler{i + 1}-{tag}",
                        DisplayName = $"Spieler {i + 1}",
                    };

                    await ctrl.InsertAsync(player);
                    Players.Add(player);
                }

                await ctrl.SaveChangesAsync();
            }
            cleanups.Insert(0, async () =>
            {
                using var ctrl = new PlayersController();
                foreach (var player in Players)
                    await ctrl.DeleteAsync(player.Id);
                await ctrl.SaveChangesAsync();
            });

            Game = new Game
            {
                Id = Guid.NewGuid(),
                Designation = $"Spiel-{tag}",
                SuggestedPhases = 1,
                Phase = 1,
            };

            Coordinate = new GameGridCoordinate
            {
                Id = Guid.NewGuid(),
                GameId = Game.Id,
                Game = Game,
                X = 0,
                Y = 0,
                Phase = 1,
                QuestionBaseId = Question.Id,
            };

            Game.GameGridCoordinates.Add(Coordinate);

            foreach (var player in Players)
            {
                Game.PlayerXGames.Add(new PlayerXGame
                {
                    Id = Guid.NewGuid(),
                    GameId = Game.Id,
                    PlayerId = player.Id,
                    Player = player,
                });
            }

            using (var ctrl = new GamesController())
            {
                await ctrl.UpsertAsync(Game);
                await ctrl.SaveChangesAsync();
            }
            cleanups.Insert(0, async () =>
            {
                using var ctrl = new GamesController();
                await ctrl.DeleteAsync(Game.Id);
                await ctrl.SaveChangesAsync();
            });

            // Kinder einzeln nachziehen: UpsertAsync des Spiels schreibt sie nicht mit,
            // weil CloneWithoutReferences die Sammlungen leert - dieselbe Eigenheit wie
            // bei den Frageschritten.
            using (var ctrl = new GameGridCoordinatesController())
            {
                await ctrl.InsertAsync(Coordinate);
                await ctrl.SaveChangesAsync();
            }

            using (var ctrl = new PlayerXGamesController())
            {
                foreach (var link in Game.PlayerXGames)
                    await ctrl.InsertAsync(link);

                await ctrl.SaveChangesAsync();
            }
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var cleanup in cleanups)
            {
                try
                {
                    await cleanup();
                }
                catch
                {
                    // Aufraeumen darf einen bereits gescheiterten Test nicht ueberdecken.
                }
            }
        }
    }
}
