using Quizzer.Base;
using Quizzer.DataModels.Models;

namespace Quizzer.Views.QuestionTypes
{
    /// <summary>Die Vorschau einer Frage - dieselbe Anzeige wie auf dem Beamer.</summary>
    public partial class QuestionPreviewView : WindowBase
    {
        public QuestionPreviewView()
        {
            InitializeComponent();
        }

        /// <summary>Stellt die Vorschau auf eine Frage ein.</summary>
        public void Zeige(QuestionBase frage)
            => (DataContext as QuestionPreviewViewModel)?.SetQuestion(frage);
    }
}
