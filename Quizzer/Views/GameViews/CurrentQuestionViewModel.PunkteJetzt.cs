using Quizzer.Views.GameViews.Sub;

namespace Quizzer.Views.GameViews
{
    /// <summary>
    /// M11: was „richtig" und „falsch" <b>in diesem Moment</b> bringen - im Fragefenster, nicht
    /// erst im Bewertungsfenster. Bei Hinweisfragen sinkt der Wert mit jedem Schritt; der
    /// Spielleiter sieht so, wann sich Raten noch lohnt. Dieselbe Rechnung wie die Karten
    /// (<see cref="PlayerResultContext.PunkteJetzt"/>).
    /// </summary>
    public partial class CurrentQuestionViewModel
    {
        public int PointsNowRight => PlayerResultContext.PunkteJetzt(Coordinate, this);

        public string PointsNowText
        {
            get
            {
                if (Coordinate == null)
                    return string.Empty;

                var sinkt = Coordinate.QuestionBase?.UseProportionalScoreReductionOnStep == true;
                var text = $"Jetzt: richtig +{PointsNowRight} · falsch −{CurrentMinusPoints}";

                return sinkt ? text + " (sinkt mit jedem Hinweis)" : text;
            }
        }

        private void RaisePointsNow()
        {
            OnPropertyChanged(nameof(PointsNowRight));
            OnPropertyChanged(nameof(PointsNowText));
        }
    }
}
