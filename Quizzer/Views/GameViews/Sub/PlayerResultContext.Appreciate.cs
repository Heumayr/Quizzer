using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Questions;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using static LocalBuzzer.Service.Base.States.BuzzerInputState;

namespace Quizzer.Views.GameViews.Sub
{
    /// <summary>
    /// Schaetzfrage-Teil der Spielerkachel: der abgegebene Tipp, sein Abstand zum Sollwert und
    /// ob dieser Spieler am naechsten dran liegt.
    /// </summary>
    public partial class PlayerResultContext
    {
        private InputResult? inputResult;
        private AppreciateGuess? appreciateGuess;

        /// <summary>Was der Spieler eingetippt hat.</summary>
        public InputResult? InputResult
        {
            get => inputResult;
            set
            {
                if (value != null && value.PlayerId != Player.Id)
                    throw new Exception("Invalid player for input result");

                inputResult = value;
                OnPropertyChanged();
                RaiseAppreciateChanged();
            }
        }

        /// <summary>Die Auswertung dieses Tipps - Abstand und ob er gewonnen hat.</summary>
        public AppreciateGuess? AppreciateGuess
        {
            get => appreciateGuess;
            set
            {
                appreciateGuess = value;
                OnPropertyChanged();
                RaiseAppreciateChanged();
            }
        }

        /// <summary>Ob dieser Spieler ueberhaupt etwas eingetippt hat.</summary>
        public Visibility ShowAppreciateGuess
            => InputResult != null ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>Der Tipp, wie der Spieler ihn eingegeben hat.</summary>
        public string AppreciateGuessText => InputResult?.Value ?? string.Empty;

        /// <summary>Der Abstand zum Sollwert im Klartext, oder ein Hinweis auf Unlesbares.</summary>
        public string AppreciateDistanceText
        {
            get
            {
                if (AppreciateGuess == null)
                    return string.Empty;

                // Der Abstand steht in der Basiseinheit; der Evaluator rechnet ihn in die
                // Einheit der Frage zurueck. Ungerechnet hiesse "100" bei einer km-Frage
                // in Wahrheit 100 Meter.
                if (Coordinate?.QuestionBase is AppreciateQestion question)
                    return AppreciateEvaluator.DescribeDistance(question, AppreciateGuess);

                return AppreciateGuess.IsValid ? string.Empty : "nicht lesbar";
            }
        }

        /// <summary>Ob dieser Spieler am naechsten dran liegt.</summary>
        public bool IsAppreciateWinner => AppreciateGuess?.IsWinner ?? false;

        /// <summary>Hebt den naechstliegenden Tipp hervor.</summary>
        public Brush AppreciateGuessBrush
            => IsAppreciateWinner ? Brushes.DarkGreen : Brushes.Transparent;

        private void RaiseAppreciateChanged()
        {
            OnPropertyChanged(nameof(ShowAppreciateGuess));
            OnPropertyChanged(nameof(AppreciateGuessText));
            OnPropertyChanged(nameof(AppreciateDistanceText));
            OnPropertyChanged(nameof(IsAppreciateWinner));
            OnPropertyChanged(nameof(AppreciateGuessBrush));
        }
    }
}
