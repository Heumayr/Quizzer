using LocalBuzzer.Service.Base;
using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.Views.StaticRessources;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using static LocalBuzzer.Service.Base.States.BuzzerKeySelector;

namespace Quizzer.Views.BuzzerViews
{
    public partial class BuzzerControlsViewModel : UcViewModelBase, IDisposable
    {
        public Func<Player?, int, Task>? WinnerDeclared { get; set; }

        public Func<SelectionResult, Task>? PlayerSelectedKeys;

        public Func<ConcurrentDictionary<Guid, SelectionResult>, Task>? AllPlayersSelectedKeys;

        private BuzzerServerViewModel? BuzzerServerViewModel { get; set; }

        public BuzzerController? BuzzerController => BuzzerServerViewModel?._server?.BuzzerController;

        public Game? Game => BuzzerServerViewModel?.Game;

        public Brush BackgroundBrush => BuzzerServerViewModel?.BackgroundBrush ?? Brushes.DarkGray;

        public void SetBuzzerVerverViewModel(BuzzerServerViewModel viewModel)
        {
            StaticManager.BuzzerServerViewModel.PlayerConnectionStateChanged -= OnPlayerConnectionStateChanged;
            StaticManager.BuzzerServerViewModel.PlayerConnectionStateChanged += OnPlayerConnectionStateChanged;

            var ctrl = viewModel._server?.BuzzerController;

            //guard
            ctrl?.EventBus.RoundReset -= OnReset;
            ctrl?.EventBus.WinnerDeclared -= OnBuzzerWinner;
            ctrl?.EventBus.ClientAssigned -= OnAssigned;

            ctrl?.EventBus.PlayerSelectedKeys -= OnPlayerSelectedKeys;
            ctrl?.EventBus.AllPlayersSelectedKeys -= OnAllPlayerSelectedKeys;

            //add
            ctrl?.EventBus.RoundReset += OnReset;
            ctrl?.EventBus.WinnerDeclared += OnBuzzerWinner;
            ctrl?.EventBus.ClientAssigned += OnAssigned;

            ctrl?.EventBus.PlayerSelectedKeys += OnPlayerSelectedKeys;
            ctrl?.EventBus.AllPlayersSelectedKeys += OnAllPlayerSelectedKeys;

            SubscribeInputEvents(ctrl);

            BuzzerServerViewModel = viewModel;
        }

        private void OnPlayerConnectionStateChanged(object? sender, ServerState e)
        {
            OnPropertyChanged(nameof(BackgroundBrush));
            RefreshConnections();
        }

        private AsyncRelayCommand? closeRoundCommand;

        public ICommand CloseRoundCommand => closeRoundCommand ??= new AsyncRelayCommand(
            CloseRoundAsync, _ => BuzzerServerViewModel?.IsBuzzerServerRunning ?? false);

        /// <summary>
        /// Schließt die Runde, ohne auf die fehlenden Abgaben zu warten.
        /// <para>
        /// <b>Der Notausgang für den Abend.</b> Eine Schätzfragen- oder Multiple-Choice-Runde
        /// schließt sonst nur, wenn <b>restlos jeder</b> Mitspieler abgegeben hat. Ein leerer
        /// Akku, ein iPhone mit gesperrtem Bildschirm oder jemand ganz ohne Telefon genügt, und
        /// die Frage bekommt nie ihre Auswertung.
        /// </para>
        /// <para>
        /// <b>Nicht dasselbe wie „Runde zurücksetzen".</b> Das wirft die Tipps weg; das hier
        /// wertet mit dem aus, was da ist.
        /// </para>
        /// </summary>
        public async Task CloseRoundAsync(object? commandParameter)
        {
            try
            {
                if (BuzzerController is null)
                    return;

                await BuzzerController.LockAllAsync();
            }
            catch (Exception ex)
            {
                ExceptionManager.HandleException(ex);
            }
        }

        private AsyncRelayCommand? resetRoundCommand;

        public ICommand ResetRoundCommand => resetRoundCommand ??= new AsyncRelayCommand(ResetRoundAsync, _ => BuzzerServerViewModel?.IsBuzzerServerRunning ?? false);

        /// <summary>
        /// Spielt eine Runde auf die Telefone aus.
        /// <para>
        /// Ohne laufenden Server geschieht nichts. Das ist ein Normalzustand, kein Fehler: bis
        /// 2026-09-05 warf diese Stelle, und nach einem "Server beenden" brachte jede geoeffnete
        /// und jede geschlossene Frage ein Fehlerfenster mit sich.
        /// </para>
        /// </summary>
        public async Task ResetRoundAsync(object? commandParameter)
        {
            try
            {
                if (BuzzerServerViewModel is null || BuzzerController is null || Game is null)
                    return;

                var buzzerLayout = BuzzerControlsLayout.None;

                if (commandParameter is BuzzerControlsLayout bcl)
                    buzzerLayout = bcl;

                await BuzzerController.ResetRoundAsync(Game.CurrentRound, buzzerLayout);
            }
            catch (Exception ex)
            {
                ExceptionManager.HandleException(ex);
            }
        }

        /// <summary>
        /// Ein Telefon hat sich angemeldet. Die Spielerliste haengt am Verbindungszustand und
        /// zieht ueber <see cref="RefreshConnections"/> nach; hier ist nur der Sprung auf den
        /// Oberflaechen-Thread noetig.
        /// <para>
        /// Bis 2026-09-05 stand hier ein leerer <c>RunOnUi</c>-Rumpf. Der blockierte den
        /// Hub-Thread mitten im Handshake, solange die Oberflaeche beschaeftigt war - fuer nichts.
        /// </para>
        /// </summary>
        public void OnAssigned(string displayName, Guid guid)
        {
            _ = RunOnUiAsync(RefreshConnections);
        }

        public async void OnPlayerSelectedKeys(SelectionResult selectionResult)
        {
            try
            {
                TrackSelection(selectionResult.PlayerId, selectionResult.SelectedKeys);

                await RunOnUiAsync(async () =>
                {
                    if (PlayerSelectedKeys != null)
                        await PlayerSelectedKeys.Invoke(selectionResult);
                });
            }
            catch (Exception ex)
            {
                ExceptionManager.HandleException(ex);
            }
        }

        public async void OnAllPlayerSelectedKeys(ConcurrentDictionary<Guid, SelectionResult> selectionResultsDic)
        {
            try
            {
                await RunOnUiAsync(async () =>
                {
                    if (AllPlayersSelectedKeys != null)
                        await AllPlayersSelectedKeys.Invoke(selectionResultsDic);
                });
            }
            catch (Exception ex)
            {
                ExceptionManager.HandleException(ex);
            }
        }

        public async void OnBuzzerWinner(Player? player, int round)
        {
            try
            {
                TrackWinner(player?.Id);

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

        /// <summary>
        /// Stellt die Tastenwahl auf die Frage ein. <paramref name="showDesignations"/> wurde bis
        /// 2026-09-05 durch ein festes <c>true</c> ersetzt - der Schalter des Editors blieb wirkungslos.
        /// </summary>
        public async Task SetKeySelectorDictionary(Dictionary<string, string>? keySelectorDic, int maxAllowedSelections = 1, bool showDesignations = true, Guid? questionId = null)
        {
            var info = new BuzzerKeySelectorInfo()
            {
                MaxAllowedSelections = maxAllowedSelections,
                KeysAndDesignations = keySelectorDic ?? new(),
                ShowDesignations = showDesignations,
                QuestionId = questionId
            };

            BuzzerController?.StateManager.BuzzerKeySelector.Infos = info;
        }

        /// <summary>
        /// Eine neue Runde wurde ausgespielt: die Abgaben der letzten gelten nicht mehr, und die
        /// Rundennummer in der Kopfzeile stimmt nicht mehr.
        /// </summary>
        public void OnReset(int round)
        {
            ClearRound();
        }

        public void Dispose()
        {
            var ctrl = BuzzerServerViewModel?._server?.BuzzerController;
            ctrl?.EventBus.RoundReset -= OnReset;
            ctrl?.EventBus.WinnerDeclared -= OnBuzzerWinner;
            ctrl?.EventBus.ClientAssigned -= OnAssigned;

            ctrl?.EventBus.PlayerSelectedKeys -= OnPlayerSelectedKeys;
            ctrl?.EventBus.AllPlayersSelectedKeys -= OnAllPlayerSelectedKeys;

            UnsubscribeInputEvents(ctrl);

            // Das Abo haengt am prozessweiten BuzzerServerViewModel und ueberlebt sonst jedes
            // Entsorgen: nach mehrmaligem Start liefen tote ViewModels bei jedem Verbindungs-
            // wechsel ihre Spielerlisten durch.
            StaticManager.BuzzerServerViewModel.PlayerConnectionStateChanged -= OnPlayerConnectionStateChanged;

            BuzzerServerViewModel = null;
        }
    }
}