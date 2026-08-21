using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Questions;
using System.Collections.Concurrent;
using static LocalBuzzer.Service.Base.States.BuzzerInputState;

namespace Quizzer.Views.GameViews
{
    /// <summary>
    /// Schaetzfrage-Teil des Spielablaufs: nimmt die Tipps der Spieler entgegen und ermittelt,
    /// wer am naechsten dran liegt.
    /// </summary>
    public partial class CurrentQuestionViewModel
    {
        /// <summary>Die aktuelle Frage als Schaetzfrage, sofern es eine ist.</summary>
        public AppreciateQestion? AppreciateQuestion => Question as AppreciateQestion;

        /// <summary>
        /// Das Ergebnis der Schaetzrunde: alle Tipps mit Abstand, Gewinner markiert.
        /// Liegt erst vor, wenn alle abgegeben haben.
        /// </summary>
        public AppreciateOutcome? AppreciateOutcome { get; private set; }

        /// <summary>Ein einzelner Spieler hat abgegeben - Ergebnisfenster nachziehen.</summary>
        private async Task OnPlayerSubmittedInput(InputResult result)
        {
            PlayersResultViewModel?.SetInputResult(result);
            await OpenResultsAsync();
        }

        /// <summary>
        /// Alle haben abgegeben. Jetzt steht fest, wer am naechsten dran liegt - der Vorschlag
        /// geht an das Ergebnisfenster, wo der Spielleiter ihn uebernehmen oder aendern kann.
        /// </summary>
        private async Task OnAllPlayersSubmittedInput(ConcurrentDictionary<Guid, InputResult> inputs)
        {
            AppreciateOutcome = EvaluateGuesses(inputs);

            PlayersResultViewModel?.ApplyAppreciateOutcome(AppreciateOutcome, inputs);

            OnPropertyChanged(nameof(AppreciateOutcome));
            OnPropertyChanged(nameof(AppreciateSummary));

            await OpenResultsAsync();
        }

        /// <summary>
        /// Wertet die Tipps aus. Ohne Schaetzfrage gibt es nichts zu rechnen - dann bleibt es
        /// bei einer leeren Auswertung, statt irgendetwas zu erfinden.
        /// </summary>
        internal AppreciateOutcome? EvaluateGuesses(ConcurrentDictionary<Guid, InputResult> inputs)
        {
            if (AppreciateQuestion == null)
                return null;

            var guesses = inputs.ToDictionary(pair => pair.Key, pair => (string?)pair.Value.Value);

            return AppreciateEvaluator.Evaluate(AppreciateQuestion, guesses);
        }

        /// <summary>Kurzfassung fuer den Spielleiter: Sollwert und wer ihn am besten getroffen hat.</summary>
        public string AppreciateSummary
        {
            get
            {
                if (AppreciateQuestion == null)
                    return string.Empty;

                var expected = AppreciateEvaluator.DescribeExpected(AppreciateQuestion);

                if (AppreciateOutcome is not { HasWinner: true })
                    return $"Richtig waere: {expected}";

                var winners = AppreciateOutcome.Guesses
                    .Where(g => g.IsWinner)
                    .Select(g => PlayersResultViewModel?.DisplayNameOf(g.PlayerId) ?? "?")
                    .ToList();

                return winners.Count == 1
                    ? $"Richtig waere: {expected}. Am naechsten dran: {winners[0]}."
                    : $"Richtig waere: {expected}. Gleichauf: {string.Join(", ", winners)}.";
            }
        }

        /// <summary>
        /// Nur fuer Tests: speist die Tipps ein, als kaemen sie ueber den Buzzer herein.
        /// Im laufenden Programm ruft das der BuzzerEventBus.
        /// </summary>
        internal Task SubmitAllForTestAsync(ConcurrentDictionary<Guid, InputResult> inputs)
            => OnAllPlayersSubmittedInput(inputs);

        /// <summary>Setzt die Auswertung zurueck, wenn die Frage geschlossen wird.</summary>
        internal void ClearAppreciateOutcome()
        {
            AppreciateOutcome = null;
            OnPropertyChanged(nameof(AppreciateOutcome));
            OnPropertyChanged(nameof(AppreciateSummary));
        }
    }
}
