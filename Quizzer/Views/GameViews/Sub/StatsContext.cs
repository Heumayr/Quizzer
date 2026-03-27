using Quizzer.Base;
using Quizzer.DataModels.Models.Base;
using Quizzer.Logic.Controller.TypedControllers;
using Quizzer.Logic.Migrations;
using Quizzer.Views.StaticRessources;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Input;

namespace Quizzer.Views.GameViews.Sub
{
    public class StatsContext : UcViewModelBase
    {
        private List<PlayerStatsContext> playerStatsContextList = new();
        private Game game = null!;

        public List<Player> Winners { get; set; } = new List<Player>();

        public GameMasterViewModel? GameMasterViewModel { get; set; }

        public bool IsGameFinished => GameMasterViewModel?.IsGameFinished ?? false;

        public Visibility ShowNormalStatsView =>
            IsGameFinished ? Visibility.Collapsed : Visibility.Visible;

        public Visibility ShowFinishedStatsView =>
            IsGameFinished ? Visibility.Visible : Visibility.Collapsed;

        public Game Game
        {
            get => game;
            set
            {
                game = value;
                OnPropertyChanged();
            }
        }

        public int Columns => PlayerStatsContextList.Count();

        public List<PlayerStatsContext> PlayerStatsContextList
        {
            get => playerStatsContextList;
            set
            {
                playerStatsContextList = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Columns));
                OnPropertyChanged(nameof(FirstPlacePlayers));
                OnPropertyChanged(nameof(OtherPlayers));
            }
        }

        public void UpdateScore(bool calulateAndSetWinners)
        {
            Winners.Clear();

            if (calulateAndSetWinners)
            {
                CalculateAndSetWinners();
            }

            foreach (var item in PlayerStatsContextList)
            {
                if (!calulateAndSetWinners)
                    item.Placement = 0;

                item.UpdateScore();
            }

            OnPropertyChanged(nameof(FirstPlacePlayers));
            OnPropertyChanged(nameof(OtherPlayers));
            OnPropertyChanged(nameof(Columns));
            OnPropertyChanged(nameof(IsGameFinished));
            OnPropertyChanged(nameof(ShowNormalStatsView));
            OnPropertyChanged(nameof(ShowFinishedStatsView));
        }

        private void CalculateAndSetWinners()
        {
            if (PlayerStatsContextList == null || PlayerStatsContextList.Count == 0)
            {
                Winners = new List<Player>();
                OnPropertyChanged(nameof(Winners));
                return;
            }

            var orderedStats = PlayerStatsContextList
                .OrderByDescending(x => x.ActualScore)
                .ToList();

            int? lastScore = null;

            for (int i = 0; i < orderedStats.Count; i++)
            {
                var context = orderedStats[i];

                if (lastScore == null || context.ActualScore != lastScore.Value)
                {
                    context.Placement = i + 1;
                    lastScore = context.ActualScore;
                }
                else
                {
                    context.Placement = orderedStats[i - 1].Placement;
                }
            }

            int bestScore = orderedStats.First().ActualScore;

            Winners = orderedStats
                .Where(x => x.ActualScore == bestScore)
                .Select(x => x.Player)
                .ToList();

            OnPropertyChanged(nameof(Winners));
            OnPropertyChanged(nameof(IsGameFinished));
            OnPropertyChanged(nameof(ShowNormalStatsView));
            OnPropertyChanged(nameof(ShowFinishedStatsView));
        }

        public void RefreshFinishState()
        {
            OnPropertyChanged(nameof(IsGameFinished));
            OnPropertyChanged(nameof(ShowNormalStatsView));
            OnPropertyChanged(nameof(ShowFinishedStatsView));
        }

        public List<PlayerStatsContext> FirstPlacePlayers =>
             PlayerStatsContextList
                .Where(x => x.Placement == 1)
                .OrderByDescending(x => x.ActualScore)
                .ThenBy(x => x.Player.DisplayName)
                .ToList();

        public List<PlayerStatsContext> OtherPlayers =>
            PlayerStatsContextList
                .Where(x => x.Placement > 1)
                .OrderBy(x => x.Placement)
                .ThenByDescending(x => x.ActualScore)
                .ThenBy(x => x.Player.DisplayName)
                .ToList();
    }
}