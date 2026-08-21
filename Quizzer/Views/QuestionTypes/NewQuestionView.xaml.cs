using Quizzer.Base;

namespace Quizzer.Views.QuestionTypes
{
    /// <summary>
    /// Interaction logic for NewQuestionView.xaml
    /// </summary>
    public partial class NewQuestionView : WindowBase
    {
        public NewQuestionView()
        {
            InitializeComponent();
        }

        /// <summary>Das ViewModel des Fensters, um den gewaehlten Typ auszulesen.</summary>
        public NewQuestionViewModel? ViewModel => DataContext as NewQuestionViewModel;
    }
}
