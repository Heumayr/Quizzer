using Quizzer.Base;
using System.Windows.Input;

namespace Quizzer.Views.QuestionTypes
{
    /// <summary>
    /// Die Frage ausprobieren, ohne ein Spiel dafuer zu bauen.
    /// <para>
    /// <b>Nutzerwunsch vom 2026-09-06:</b> „ein vorschaumodus im frageneditor ... dass man direkt
    /// dort die fragen ausprobieren kann".
    /// </para>
    /// <para>
    /// Die Vorschau arbeitet auf einem <b>Klon</b> - sie speichert nichts und veraendert die
    /// Frage im Editor nicht. Das ist keine Vorsicht, sondern noetig:
    /// <c>CalculateOrderdSteps</c> ergaenzt Bildschirme, und die duerfen nicht in der
    /// Schrittliste des Editors landen.
    /// </para>
    /// </summary>
    public partial class EditQuestionViewModel
    {
        private RelayCommand? previewCommand;

        public ICommand PreviewCommand => previewCommand ??= new RelayCommand(
            _ => PreviewHandler(Question!),
            _ => Question != null);

        /// <summary>
        /// Wie die Vorschau geoeffnet wird. Gekapselt aus demselben Grund wie
        /// <c>IUserPrompt</c>: ein Fenster bliebe im Testlauf stehen.
        /// </summary>
        public static Action<DataModels.Models.QuestionBase> PreviewHandler { get; set; } = Standard;

        /// <summary>Setzt auf das echte Fenster zurueck.</summary>
        public static void ResetPreviewHandler() => PreviewHandler = Standard;

        private static void Standard(DataModels.Models.QuestionBase frage)
        {
            var fenster = new QuestionPreviewView();

            fenster.Zeige(frage);
            fenster.ShowDialog();
        }
    }
}
