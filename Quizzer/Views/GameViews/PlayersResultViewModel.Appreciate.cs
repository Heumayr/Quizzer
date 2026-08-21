using Quizzer.DataModels.Questions;
using Quizzer.Views.GameViews.Sub;
using System.Collections.Concurrent;
using static LocalBuzzer.Service.Base.States.BuzzerInputState;

namespace Quizzer.Views.GameViews
{
    /// <summary>
    /// Schaetzfrage-Teil des Ergebnisfensters: verteilt die Tipps auf die Spielerkacheln und
    /// setzt den Bewertungsvorschlag fuer den naechstliegenden Tipp.
    /// </summary>
    public partial class PlayersResultViewModel
    {
        /// <summary>Der Anzeigename eines Spielers, sofern er im Spiel ist.</summary>
        public string? DisplayNameOf(Guid playerId)
            => PlayerResultContextList
                .FirstOrDefault(ctx => ctx.Player.Id == playerId)?.Player.CalculatedDisplayName;

        /// <summary>Zeigt den Tipp eines einzelnen Spielers an, sobald er abgegeben hat.</summary>
        public void SetInputResult(InputResult? result)
        {
            if (result == null)
                return;

            var ctx = PlayerResultContextList.FirstOrDefault(c => c.Player.Id == result.PlayerId);

            if (ctx == null)
                return;

            ctx.InputResult = result;
        }

        /// <summary>
        /// Uebernimmt die fertige Auswertung: jeder Spieler bekommt seinen Abstand, und wer am
        /// naechsten dran liegt, bekommt den Vorschlag "richtig".
        /// <para>
        /// Bewusst nur ein Vorschlag: die uebrigen Spieler bleiben auf <c>None</c> statt auf
        /// <c>Wrong</c>, damit ein danebenliegender Tipp nicht ungefragt Minuspunkte kostet.
        /// Der Spielleiter kann jede Bewertung von Hand aendern.
        /// </para>
        /// </summary>
        public void ApplyAppreciateOutcome(
            AppreciateOutcome? outcome, ConcurrentDictionary<Guid, InputResult> inputs)
        {
            foreach (var ctx in PlayerResultContextList)
            {
                inputs.TryGetValue(ctx.Player.Id, out var input);
                ctx.InputResult = input;

                var guess = outcome?.Guesses.FirstOrDefault(g => g.PlayerId == ctx.Player.Id);
                ctx.AppreciateGuess = guess;

                if (guess?.IsWinner == true)
                    ctx.Suggestion = PlayerResultContext.ScoreSuggestion.Right;
            }

            OnPropertyChanged(nameof(PlayerResultContextList));
        }
    }
}
