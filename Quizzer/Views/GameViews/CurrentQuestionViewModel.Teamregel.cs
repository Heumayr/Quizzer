namespace Quizzer.Views.GameViews
{
    /// <summary>
    /// Teamregeln im Fragefenster (M37 T5, Entwurf Abschnitte 1, 4 und 7): eine Teamregel ist ein Wert von
    /// <c>CellSpecialRule</c> und läuft über denselben Ablauf wie eine Sonderregel - nur im Teamspiel.
    /// </summary>
    public partial class CurrentQuestionViewModel
    {
        /// <summary>Ob das Spiel dieser Zelle ein Teamspiel ist - danach richtet sich, welche Regeln gespielt werden.</summary>
        private bool Teamspiel => Coordinate?.Game?.IsTeamGame == true;
    }
}
