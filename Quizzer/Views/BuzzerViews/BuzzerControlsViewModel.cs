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
using static LocalBuzzer.Service.Base.States.BuzzerKeySelector;

namespace Quizzer.Views.BuzzerViews
{
    public class BuzzerControlsViewModel : UcViewModelBase, IDisposable
    {
        public Func<Player?, int, Task>? WinnerDeclared { get; set; }

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
            ctrl?.EventBus.WinnerDeclared -= OnBuzzerWinner;
            ctrl?.EventBus.ClientAssigned -= OnAssigned;

            ctrl?.EventBus.RoundReset += OnReset;
            ctrl?.EventBus.WinnerDeclared += OnBuzzerWinner;
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

            var buzzerLayout = BuzzerControlsLayout.None;

            if (commandParameter is BuzzerControlsLayout bcl)
                buzzerLayout = bcl;

            await BuzzerController.ResetRoundAsync(Game.CurrentRound, buzzerLayout);
        }

        public void OnAssigned(string displayName, Guid guid)
        {
            RunOnUi(() =>
            {
                // UI updates here
            });
        }

        public async void OnBuzzerWinner(Player? player, int round)
        {
            try
            {
                await RunOnUiAsync(async () =>
                {
                    if (WinnerDeclared != null)
                        await WinnerDeclared.Invoke(player, round);
                });

                //await Application.Current.Dispatcher.InvokeAsync(async () =>
                //{
                //    if (WinnerDeclared != null)
                //        await WinnerDeclared.Invoke(player, round);
                //});
            }
            catch (Exception ex)
            {
                ExceptionManager.HandleException(ex);
            }
        }

        public async Task SetKeySelectorDictionary(Dictionary<string, string>? keySelectorDic, int maxAllowedSelections = 1, bool ShowDesignations = true)
        {
            var info = new BuzzerKeySelectorInfo()
            {
                MaxAllowedSelections = maxAllowedSelections,
                KeysAndDesignations = keySelectorDic ?? new(),
                ShowDesignations = true
            };

            BuzzerController?.StateManager.BuzzerKeySelector.Infos = info;
        }

        public void OnReset(int round)
        {
        }

        public void Dispose()
        {
            var ctrl = BuzzerServerViewModel?._server?.BuzzerController;
            ctrl?.EventBus.RoundReset -= OnReset;
            ctrl?.EventBus.WinnerDeclared -= OnBuzzerWinner;
            ctrl?.EventBus.ClientAssigned -= OnAssigned;

            BuzzerServerViewModel = null;
        }
    }
}