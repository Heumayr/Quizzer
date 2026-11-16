namespace Quizzer.Views.GameViews.QuestionViews
{
    /// <summary>Sonderregel (M35 C3): die Vorphase am Beamer - dasselbe Objekt wie beim Spielleiter.</summary>
    public partial class QuestionStepViewContext
    {
        public Sub.SonderregelAnzeige? Sonderregel => Owner?.Sonderregel;
    }
}
