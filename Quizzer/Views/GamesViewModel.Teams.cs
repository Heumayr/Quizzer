using Quizzer.DataModels.Questions.Teams;
using Quizzer.Logic.Controller.TypedControllers;
using Quizzer.Views.Teams;

namespace Quizzer.Views
{
    /// <summary>Teamspiel beim Start (M37 T2).</summary>
    public partial class GamesViewModel
    {
        /// <summary>
        /// Die Mitspieler kommen erst mit „Wer spielt mit?“ dazu (M9) - wer dort neu dabei ist, hat im Teamspiel noch kein
        /// Team, und der Spielstart hielte gleich danach an. Deshalb hier die Zuteilung anbieten, solange etwas fehlt.
        /// Abbrechen ist erlaubt: dann sagt der Spielstart, was fehlt.
        /// </summary>
        private async Task TeamsZuteilenWennNoetigAsync(Guid gameId)
        {
            using var ctrl = new GamesController();
            var spiel = await ctrl.GetAsync(gameId);

            if (spiel != null && Teamzuordnung.Startfehler(spiel).Count > 0)
                TeamsView.ShowFor(spiel, Window, "Teams zuteilen");
        }
    }
}
