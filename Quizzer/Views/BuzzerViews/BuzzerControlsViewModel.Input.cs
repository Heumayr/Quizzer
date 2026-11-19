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
        /// „Runde zuruecksetzen" bei Eingabe oder Tastenwahl (V22): der Hub hat die Abgaben verworfen - wer sie im Fenster
        /// weiterverarbeitet hat, raeumt sie hier weg. Gegenstueck zu <see cref="MinigameReset"/>.
        /// </summary>
        public Action? AnswerRoundReset;

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

        /// <summary>Freie Texteingabe (M37 T5): der Sprecher einer Teamregel tippt die Antwort seines Teams.</summary>
        public void SetTextInputInfo(string placeholder, Guid? questionId)
        {
            if (BuzzerController?.StateManager.BuzzerInputState is { } state)
                state.Infos = new BuzzerInputInfo { InputType = "text", Placeholder = placeholder, QuestionId = questionId };
        }

        /// <summary>Finale (M37 E4): jeder gibt genau einmal ab - bis das nächste Layout kommt.</summary>
        public void SetzeEinmalAbgabe()
        {
            if (BuzzerController?.StateManager.BuzzerInputState is { } state)
                state.EinmalAbgabe = true;
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

        public async void OnPlayerSubmittedInput(InputResult result, int runde)
        {
            try
            {
                // P12-1: der Tipp wartet wie die Auflösung auf den Oberflächen-Thread - kam „Runde zurücksetzen“ davor, gehört er
                // weder auf die Karte noch in die Liveübersicht der neuen Runde. Beide in einem Auftrag, mit einer Prüfung.
                await RunOnUiAsync(async () =>
                {
                    if (SeitherZurueckgesetzt(runde))
                        return;

                    TrackInput(result.PlayerId, result.Value);

                    if (PlayerSubmittedInput != null)
                        await PlayerSubmittedInput.Invoke(result);
                });
            }
            catch (Exception ex)
            {
                ExceptionManager.HandleException(ex);
            }
        }

        public async void OnAllPlayersSubmittedInput(ConcurrentDictionary<Guid, InputResult> inputs, int runde)
        {
            try
            {
                await RunOnUiAsync(async () =>
                {
                    if (AllPlayersSubmittedInput != null && !SeitherZurueckgesetzt(runde))
                        await AllPlayersSubmittedInput.Invoke(inputs);
                });
            }
            catch (Exception ex)
            {
                ExceptionManager.HandleException(ex);
            }
        }

        /// <summary>
        /// P11-3, P12-1: ob die Runde mit der Marke <paramref name="runde"/> zurückgesetzt wurde, seit der Hub sie aufgelöst oder eine
        /// Abgabe darin angenommen hat. Beides wartet auf den Oberflächen-Thread; kam „Runde zurücksetzen“ davor, ist die neue Runde
        /// nicht ihre. Dann gilt es nicht, weder leer noch alt.
        /// </summary>
        private bool SeitherZurueckgesetzt(int runde) => BuzzerController is { } steuerung && steuerung.StateManager.ResetCount != runde;
    }
}
