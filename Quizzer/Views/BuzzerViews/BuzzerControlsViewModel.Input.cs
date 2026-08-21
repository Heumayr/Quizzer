using LocalBuzzer.Service.Base.States;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Questions;
using Quizzer.Views.StaticRessources;
using System.Collections.Concurrent;
using static LocalBuzzer.Service.Base.States.BuzzerInputState;

namespace Quizzer.Views.BuzzerViews
{
    /// <summary>
    /// Eingabeteil der Buzzer-Steuerung: die Schaetzfrage. Genaues Gegenstueck zu den
    /// KeySelect-Haken, nur mit einem Textwert statt einer Tastenauswahl.
    /// </summary>
    public partial class BuzzerControlsViewModel
    {
        /// <summary>Ein Spieler hat seinen Schaetzwert abgegeben.</summary>
        public Func<InputResult, Task>? PlayerSubmittedInput;

        /// <summary>Alle Mitspieler haben abgegeben.</summary>
        public Func<ConcurrentDictionary<Guid, InputResult>, Task>? AllPlayersSubmittedInput;

        /// <summary>
        /// Stellt das Eingabefeld der Spieler auf die Frage ein: Zahl oder Datum, und ein
        /// Platzhalter, der die Einheit nennt. Die Feldnamen liest <c>inputLayout.js</c> aus.
        /// </summary>
        public Task SetInputInfo(AppreciateValueKind valueKind, AppreciateUnit unit, Guid? questionId)
        {
            var info = new BuzzerInputInfo
            {
                InputType = AppreciateEvaluator.InputTypeFor(valueKind),
                Placeholder = AppreciateEvaluator.PlaceholderFor(valueKind, unit),
                QuestionId = questionId,
            };

            if (BuzzerController?.StateManager.BuzzerInputState is { } state)
                state.Infos = info;

            return Task.CompletedTask;
        }

        internal void SubscribeInputEvents(LocalBuzzer.Service.Base.BuzzerController? ctrl)
        {
            if (ctrl == null)
                return;

            ctrl.EventBus.PlayerSubmittedInput -= OnPlayerSubmittedInput;
            ctrl.EventBus.AllPlayersSubmittedInput -= OnAllPlayersSubmittedInput;

            ctrl.EventBus.PlayerSubmittedInput += OnPlayerSubmittedInput;
            ctrl.EventBus.AllPlayersSubmittedInput += OnAllPlayersSubmittedInput;
        }

        internal void UnsubscribeInputEvents(LocalBuzzer.Service.Base.BuzzerController? ctrl)
        {
            if (ctrl == null)
                return;

            ctrl.EventBus.PlayerSubmittedInput -= OnPlayerSubmittedInput;
            ctrl.EventBus.AllPlayersSubmittedInput -= OnAllPlayersSubmittedInput;
        }

        public async void OnPlayerSubmittedInput(InputResult result)
        {
            try
            {
                await RunOnUiAsync(async () =>
                {
                    if (PlayerSubmittedInput != null)
                        await PlayerSubmittedInput.Invoke(result);
                });
            }
            catch (Exception ex)
            {
                ExceptionManager.HandleException(ex);
            }
        }

        public async void OnAllPlayersSubmittedInput(ConcurrentDictionary<Guid, InputResult> inputs)
        {
            try
            {
                await RunOnUiAsync(async () =>
                {
                    if (AllPlayersSubmittedInput != null)
                        await AllPlayersSubmittedInput.Invoke(inputs);
                });
            }
            catch (Exception ex)
            {
                ExceptionManager.HandleException(ex);
            }
        }
    }
}
