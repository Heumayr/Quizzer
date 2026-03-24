using Quizzer.Base;
using Quizzer.DataModels.Models.Base;
using Quizzer.Logic.Controller.TypedControllers;
using Quizzer.Views.GameViews.Sub;
using Quizzer.Views.StaticRessources;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Controls;
using System.Windows.Input;

namespace Quizzer.Views.GameViews
{
    public class PlayersResultViewModel : ViewModelBase
    {
        private List<PlayerResultContext> playerResultContextList = new();
        private Player? currentBuzzerWinner;

        public List<QuestionResult> Results => PlayerResultContextList.Select(x => x.Result).ToList();

        public GameGridCoordinate? Coordinate { get; private set; }

        public int Columns => PlayerResultContextList.Count();

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
                        QuestionBaseId = Coordinate.QuestionBaseId.Value,
                    };

                    await ctrlResults.InsertAsync(result);
                    await ctrlResults.SaveChangesAsync();
                    results.Add(result);
                }

                result.Player = player;
                result.Game = Coordinate.Game;
                result.QuestionBase = Coordinate.QuestionBase!;
                result.GameGridCoordinate = Coordinate;

                var newContext = new PlayerResultContext()
                {
                    PlayersResultViewModel = this,
                    Player = player,
                    Result = result
                };

                contextList.Add(newContext);
            }

            PlayerResultContextList = contextList;
            OnPropertyChanged(nameof(Columns));
        }

        public override async Task VMSaveAsync()
        {
            try
            {
                using var ctrl = new QuestionResultsController();
                foreach (var ctx in PlayerResultContextList)
                {
                    ctx.SetManipulationToResult();

                    await ctrl.UpsertAsync(ctx.Result);
                }

                await ctrl.SaveChangesAsync();

                foreach (var ctx in PlayerResultContextList)
                {
                    ctx.RefreshUIOnModelSave();
                }
            }
            catch (Exception ex)
            {
                ExceptionManager.HandleException(ex);
            }
        }

        protected override async Task OnloadAsync()
        {
        }

        private AsyncRelayCommand? saveCommand;
        public ICommand SaveCommand => saveCommand ??= new AsyncRelayCommand(SaveAsync);

        private async Task SaveAsync(object? commandParameter)
        {
            await VMSaveAsync();
        }

        private AsyncRelayCommand? saveAndCloseCommand;
        public ICommand SaveAndCloseCommand => saveAndCloseCommand ??= new AsyncRelayCommand(SaveAndCloseAsync);

        private async Task SaveAndCloseAsync(object? commandParameter)
        {
            await VMSaveAsync();
            Window?.Close();
        }

        public bool IsDoneAndShowFinishState { get; private set; }

        private AsyncRelayCommand? saveIsDoneFinishStateCommand;
        public ICommand SaveIsDoneFinishStateCommand => saveIsDoneFinishStateCommand ??= new AsyncRelayCommand(SaveIsDoneFinishStateAsync);

        private async Task SaveIsDoneFinishStateAsync(object? commandParameter)
        {
            await VMSaveAsync();

            IsDoneAndShowFinishState = true;

            Window?.Close();
        }
    }
}