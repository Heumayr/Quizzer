using LocalBuzzer.Service.Base.States;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.Extentions;
using System.Collections.Concurrent;
using System.Windows;

namespace Quizzer.Views.GameViews
{
    /// <summary>
    /// Buzzer-Teil der Frageansicht: welches Layout die Frage auf die Telefone bringt und was
    /// mit den Rueckmeldungen geschieht.
    /// </summary>
    public partial class CurrentQuestionViewModel
    {
        /// <summary>
        /// Ob diese Frage die Telefone braucht, der Buzzer-Server aber nicht laeuft.
        /// <para>
        /// <b>Gemessen 2026-09-07:</b> wurde eine Frage geoeffnet, ohne dass der Server lief,
        /// stieg die Vorbereitung <b>stumm</b> aus. Kein Layout ging auf die Telefone, die
        /// Buzzer-Zeile blieb leer, und "Runde zuruecksetzen" war tot. Das Fragefenster ist
        /// modal - der Spielleiter kam an das Buzzer-Fenster gar nicht mehr heran und musste
        /// erst die Frage schliessen. Auf dem Bildschirm stand kein Grund.
        /// </para>
        /// </summary>
        public bool BuzzerFehlt =>
            BuzzerControlsViewModel == null
            && (Question?.BuzzerControlsLayout ?? BuzzerControlsLayout.None) != BuzzerControlsLayout.None;

        /// <summary>Der Satz dazu - leer, solange nichts fehlt.</summary>
        public string BuzzerFehltText => BuzzerFehlt
            ? "Der Buzzer-Server läuft nicht. Diese Frage geht nicht auf die Telefone - "
              + "Frage schließen, im Spielfeld „Buzzer-Server öffnen“ drücken, dann erneut öffnen."
            : string.Empty;

        public Visibility BuzzerFehltVisibility =>
            BuzzerFehlt ? Visibility.Visible : Visibility.Collapsed;

        private async Task PrepareBuzzerlayoutAsync()
        {
            if (BuzzerControlsViewModel == null || Coordinate == null)
            {
                // Nicht mehr stumm: die drei Eigenschaften oben tragen den Grund ins Fenster.
                OnPropertyChanged(nameof(BuzzerFehlt));
                OnPropertyChanged(nameof(BuzzerFehltText));
                OnPropertyChanged(nameof(BuzzerFehltVisibility));

                return;
            }

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
