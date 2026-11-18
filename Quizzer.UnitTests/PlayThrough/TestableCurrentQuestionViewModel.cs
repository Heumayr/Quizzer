using Quizzer.Views.GameViews;

namespace Quizzer.UnitTests.PlayThrough
{
    /// <summary>
    /// Spielt eine Frage durch, ohne das Ergebnisfenster zu oeffnen. Das ist die einzige
    /// Abweichung vom laufenden Programm - der Ablauf selbst bleibt derselbe.
    /// </summary>
    public sealed class TestableCurrentQuestionViewModel : CurrentQuestionViewModel
    {
        public int ShowResultWindowCalls { get; private set; }

        /// <summary>Wie oft das Ergebnisfenster geschlossen wurde.</summary>
        public int CloseResultWindowCalls { get; private set; }

        /// <summary>
        /// Wie das echte Ergebnisfenster: sein Schließen bucht, was auf den Karten steht, über <c>ViewModelBase.NotifyClosed</c>
        /// (R5-1). Ohne das legt der Test es still, und eine Zusicherung über „bewertet" sieht nie eine Buchung.
        /// </summary>
        public bool SchliessenBucht { get; set; }

        protected override void ShowResultWindow() => ShowResultWindowCalls++;

        protected override void CloseResultWindow()
        {
            CloseResultWindowCalls++;

            if (SchliessenBucht)
                _ = PlayersResultViewModel?.NotifyClosed();
        }
    }
}
