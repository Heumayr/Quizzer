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

        protected override void ShowResultWindow() => ShowResultWindowCalls++;
    }
}
