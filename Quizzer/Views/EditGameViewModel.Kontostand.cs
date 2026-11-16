namespace Quizzer.Views
{
    public partial class EditGameViewModel
    {
        /// <summary>Schalter „Kontostand darf unter 0 fallen" im Spielaufbau (M35 E2).</summary>
        public bool AllowNegativeScore
        {
            get => Game?.AllowNegativeScore ?? true;
            set
            {
                if (Game == null || Game.AllowNegativeScore == value)
                    return;

                Game.AllowNegativeScore = value;
                OnPropertyChanged();
            }
        }
    }
}
