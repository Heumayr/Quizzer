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
        }

        private void CalculateAndSetWinners()
        {
            if (PlayerStatsContextList == null || PlayerStatsContextList.Count == 0)
            {
                Winners = new List<Player>();
                return;
            }

            var orderedStats = PlayerStatsContextList
                .OrderByDescending(x => x.ActualScore)
                .ToList();

            int currentPlacement = 0;
            int? lastScore = null;

            foreach (var context in orderedStats)
            {
                if (lastScore == null || context.ActualScore != lastScore.Value)
                {
                    currentPlacement++;
                    lastScore = context.ActualScore;
                }

                context.Placement = currentPlacement;
            }

            int bestScore = orderedStats.First().ActualScore;

            Winners = orderedStats
                .Where(x => x.ActualScore == bestScore)
                .Select(x => x.Player)
                .ToList();
        }
    }
}