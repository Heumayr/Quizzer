using System.Windows.Controls;

namespace Quizzer.Views.QuestionTypes.Typed
{
    /// <summary>
    /// Die Maske eines Fragetyps - eine Ansicht für alle fünf, weil sich die Typen nicht in der
    /// <i>Form</i> unterscheiden, sondern darin, welche Teile davon sie zeigen und wie sie sie
    /// nennen.
    /// <para>
    /// Was der Typ bestimmt, sagt sein <c>ZeileneditorViewModel</c>: Überschriften, ob es ein
    /// Häkchen „richtig" gibt, ob sortiert und ob überhaupt eine Zeile angelegt werden darf.
    /// </para>
    /// </summary>
    public partial class ZeileneditorView : UserControl
    {
        public ZeileneditorView()
        {
            InitializeComponent();
        }
    }
}
