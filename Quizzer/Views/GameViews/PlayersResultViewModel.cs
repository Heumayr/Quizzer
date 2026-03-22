using Quizzer.Base;
using Quizzer.DataModels.Models.Base;
using Quizzer.Logic.Controller.TypedControllers;
using Quizzer.Views.GameViews.Sub;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Controls;

namespace Quizzer.Views.GameViews
{
    public class PlayersResultViewModel : ViewModelBase
    {
        private List<PlayerResultContext> playerResultContextList = new();
        private Player? currentBuzzerWinner;

        private GameGridCoordinate? Coordinate { get; set; }

        public Player? CurrentBuzzerWinner
        {
            get => currentBuzzerWinner;
            set
            {
                currentBuzzerWinner = value;
                OnPropertyChanged();

                foreach (var context in PlayerResultContextList)
                {
                    context.CurrentBuzzerWinner = value;
                }
            }
        }

        public List<PlayerResultContext> PlayerResultContextList
        {
            get => playerResultContextList;
            set
            {
                playerResultContextList = value;
                OnPropertyChanged();
            }
        }

        public async Task SetCoordinateAsync(GameGridCoordinate coordinate)
        {
            if (coordinate == null) throw new ArgumentNullException(nameof(coordinate));
            if (coordinate.QuestionBaseId == null || coordinate.QuestionBaseId == Guid.Empty) throw new Exception("No question set to coordinate");

            var playersCount = coordinate.Game.Players.Count();

            if (playersCount == 0)
            {
                throw new Exception("No players loaded");
            }

            Coordinate = coordinate;

            using var ctrlResults = new QuestionResultsController();

            var results = await ctrlResults.GetAllResultsForCoordinate(Coordinate.Id) ?? new();

            if (results.Count > 0 && results.Count != playersCount)
            {
                throw new Exception("Invalid results count");
            }

            var contextList = new List<PlayerResultContext>();
            foreach (var player in Coordinate.Game.Players)
            {
                var result = results.FirstOrDefault(r => r.PlayerId == player.Id);

                if (result == null)
                {
                    result = new()
                    {
                        PlayerId = player.Id,
                        GameId = Coordinate.GameId,
                        GameGridCoordinateId = Coordinate.Id,
                        QuestionBaseId = Coordinate.QuestionBaseId.Value
                    };

                    await ctrlResults.InsertAsync(result);
                    await ctrlResults.SaveChangesAsync();
                    results.Add(result);
                }

                var newContext = new PlayerResultContext()
                {
                    Coordinate = Coordinate,
                    Player = player,
                    Result = result
                };

                contextList.Add(newContext);
            }

            PlayerResultContextList = contextList;
        }

        public override async Task VMSaveAsync()
        {
        }

        protected override async Task OnloadAsync()
        {
        }
    }
}