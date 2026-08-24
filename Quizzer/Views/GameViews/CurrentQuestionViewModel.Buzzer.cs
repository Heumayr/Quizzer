using LocalBuzzer.Service.Base.States;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.Extentions;
using System.Collections.Concurrent;

namespace Quizzer.Views.GameViews
{
    /// <summary>
    /// Buzzer-Teil der Frageansicht: welches Layout die Frage auf die Telefone bringt und was
    /// mit den Rueckmeldungen geschieht.
    /// </summary>
    public partial class CurrentQuestionViewModel
    {
        private async Task PrepareBuzzerlayoutAsync()
        {
            if (BuzzerControlsViewModel == null || Coordinate == null)
                return;

            var layout = Question?.BuzzerControlsLayout ?? BuzzerControlsLayout.None;

            // Der Spielleiter soll waehrend der Runde sehen, worauf er wartet und was richtig
            // waere - bis hierher stand beides erst im Ergebnisfenster, also erst danach.
            BuzzerControlsViewModel.SetRoundInfo(Question);

            switch (layout)
            {
                case BuzzerControlsLayout.None:
                    break;

                case BuzzerControlsLayout.Buzzer:
                    BuzzerControlsViewModel.WinnerDeclared = OnWinnerDeclared;
                    break;

                case BuzzerControlsLayout.KeySelect:
                    await BuzzerControlsViewModel.SetKeySelectorDictionary(
                        Question?.Steps.GetKeyDictionary(),
                        Question?.BuzzerMaxAllowedKeySelect ?? 1,
                        Question?.ShowTextOnKeySelect ?? true,
                        Question?.Id);

                    BuzzerControlsViewModel.PlayerSelectedKeys = OnPlayerSelectedKeys;
                    BuzzerControlsViewModel.AllPlayersSelectedKeys = OnAllPlayersSelectedKeys;

                    break;

                case BuzzerControlsLayout.Input:
                    if (Question is AppreciateQestion appreciate)
                    {
                        await BuzzerControlsViewModel.SetInputInfo(
                            appreciate.ValueKind, appreciate.Unit, appreciate.Id);
                    }

                    BuzzerControlsViewModel.PlayerSubmittedInput = OnPlayerSubmittedInput;
                    BuzzerControlsViewModel.AllPlayersSubmittedInput = OnAllPlayersSubmittedInput;

                    break;
            }

            // Erst das Ausspielen bringt das Layout auf die Telefone: nur ResetRoundAsync ruft
            // StateManager.ResetLayouts und schickt StateChanged an alle Clients.
            //
            // Frueher stand das allein im Buzzer-Zweig. Bei Multiple Choice und bei der
            // Schaetzfrage wurden die Angaben zwar gesetzt, aber nie verschickt - CurrentLayout
            // blieb None, CurrentState null, und damit blieb jeder Spieler gesperrt. Am Telefon
            // sah das aus, als bestuende keine Verbindung.
            if (layout != BuzzerControlsLayout.None && !Coordinate.IsDone)
            {
                await BuzzerControlsViewModel.ResetRoundAsync(CurrentBuzzerLayout);
            }
        }

        private async Task OnAllPlayersSelectedKeys(ConcurrentDictionary<Guid, BuzzerKeySelector.SelectionResult> dictionary)
        {
            PlayersResultViewModel?.SelectionResultsDic = dictionary;
            await OpenResultsAsync();
        }

        private async Task OnPlayerSelectedKeys(BuzzerKeySelector.SelectionResult result)
        {
            PlayersResultViewModel?.SetSelectionResult(result);
            await OpenResultsAsync();
        }

        private async Task ClearBuzzerLayouts()
        {
            if (BuzzerControlsViewModel != null)
            {
                //buzzer
                BuzzerControlsViewModel.WinnerDeclared = null;

                //kex select
                await BuzzerControlsViewModel.SetKeySelectorDictionary(null);
                BuzzerControlsViewModel.PlayerSelectedKeys = null;
                BuzzerControlsViewModel.AllPlayersSelectedKeys = null;

                //schaetzfrage
                BuzzerControlsViewModel.PlayerSubmittedInput = null;
                BuzzerControlsViewModel.AllPlayersSubmittedInput = null;
                ClearAppreciateOutcome();
                BuzzerControlsViewModel.SetRoundInfo(null);

                await BuzzerControlsViewModel.ResetRoundAsync(null);
            }
        }

        public BuzzerControlsLayout CurrentBuzzerLayout => Question?.BuzzerControlsLayout ?? BuzzerControlsLayout.None;
    }
}
