using LocalBuzzer.Service.Base;
using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.Views.StaticRessources;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Quizzer.Views.BuzzerViews
{
    public class BuzzerControlsViewModel : UcViewModelBase, IDisposable
    {
        public Action<Player?, int>? WinnerDeclared { get; set; }

        private BuzzerServerViewModel? BuzzerServerViewModel { get; set; }

        public BuzzerController? BuzzerController => BuzzerServerViewModel?._server?.BuzzerController;
        public Game? Game => BuzzerServerViewModel?.Game;

        public Brush BackgroundBrush => BuzzerServerViewModel?.BackgroundBrush ?? Brushes.DarkGray;

        public void SetBuzzerVerverViewModel(BuzzerServerViewModel viewModel)
        {
            StaticManager.BuzzerServerViewModel.PlayerConnectionStateChanged -= OnPlayerConnectionStateChanged;
            StaticManager.BuzzerServerViewModel.PlayerConnectionStateChanged += OnPlayerConnectionStateChanged;

            var ctrl = viewModel._server?.BuzzerController;
            ctrl?.EventBus.RoundReset -= OnReset;
            ctrl?.EventBus.WinnerDeclared -= OnWinner;
            ctrl?.EventBus.ClientAssigned -= OnAssigned;

            ctrl?.EventBus.RoundReset += OnReset;
            ctrl?.EventBus.WinnerDeclared += OnWinner;
            ctrl?.EventBus.ClientAssigned += OnAssigned;

            BuzzerServerViewModel = viewModel;
        }

        private void OnPlayerConnectionStateChanged(object? sender, ServerState e)
        {
            OnPropertyChanged(nameof(BackgroundBrush));
        }

        private AsyncRelayCommand? resetRoundCommand;

        public ICommand ResetRoundCommand => resetRoundCommand ??= new AsyncRelayCommand(ResetRoundAsync, _ => BuzzerServerViewModel?.IsBuzzerServerRunning ?? false);

        public async Task ResetRoundAsync(object? commandParameter)
        {
            if (BuzzerServerViewModel is null || BuzzerController is null || Game is null)
                throw new Exception("Invalid Server State");

            await BuzzerController.ResetRoundAsync(Game.CurrentRound);
        }

        public void OnAssigned(string name)
        {
        }

        public async void OnWinner(Player? player, int round)
        {
            try
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    WinnerDeclared?.Invoke(player, round);
                });
            }
            catch (Exception ex)
            {
                ExceptionManager.HandleException(ex);
            }
        }

        public void OnReset(int round)
        {
        }

        public void Dispose()
        {
            var ctrl = BuzzerServerViewModel?._server?.BuzzerController;
            ctrl?.EventBus.RoundReset -= OnReset;
            ctrl?.EventBus.WinnerDeclared -= OnWinner;
            ctrl?.EventBus.ClientAssigned -= OnAssigned;

            BuzzerServerViewModel = null;
        }
    }
}