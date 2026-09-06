using Quizzer.DataModels.Models.Base;

namespace Quizzer.Logic.Controller.TypedControllers
{
    /// <summary>
    /// Zugriff auf die Designs. Bewusst ohne Include-Kette: die Spiele eines Designs werden
    /// nirgends gebraucht, und sie mitzuladen zoege den ganzen Spielgraphen nach.
    /// </summary>
    public class GameThemesController : GenericController<GameTheme>
    {
        public GameThemesController()
        {
        }

        public GameThemesController(ControllerBase other) : base(other)
        {
        }
    }
}
