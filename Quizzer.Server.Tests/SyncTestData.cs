using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Transfer.Sync;
using Quizzer.Logic.Sync;

namespace Quizzer.Server.Tests
{
    /// <summary>Baut Zeilen, wie ein Client sie schicken wuerde.</summary>
    internal static class SyncTestData
    {
        public static SyncRow Row(object entity)
        {
            using var store = new SyncStore();
            return store.ToRow(entity);
        }

        public static SyncUpsert New(object entity) => new() { Row = Row(entity), BaseHash = null };

        public static SyncUpsert Changed(object entity, string baseHash) => new() { Row = Row(entity), BaseHash = baseHash };

        public sealed record GameSet(Category Category, DefaultQuestion Question, Game Game, GameGridCoordinate Cell)
        {
            public SyncPushRequest AsNewPush() => new()
            {
                Upserts = [New(Category), New(Question), New(Game), New(Cell)],
            };
        }

        public static GameSet NewGame(string name, Guid? claimedGameMaster = null)
        {
            var category = new Category { Id = Guid.NewGuid(), Designation = "Kat " + name };
            var question = new DefaultQuestion
            {
                Id = Guid.NewGuid(),
                Designation = "Frage " + name,
                QuestionText = "Wie heisst " + name + "?",
                CategoryId = category.Id,
                Points = 100,
            };
            var game = new Game { Id = Guid.NewGuid(), Designation = "Spiel " + name, ModeratorPlayerId = claimedGameMaster };
            var cell = new GameGridCoordinate { Id = Guid.NewGuid(), GameId = game.Id, QuestionBaseId = question.Id, X = 0, Y = 0 };

            return new GameSet(category, question, game, cell);
        }
    }
}
