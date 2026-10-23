using Quizzer.Base;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace Quizzer.Views.QuestionTypes;

/// <summary>
/// Der Frageneditor. Hier nur, was Tastatur und Fokus betrifft (E1, M31): Strg+Enter heißt
/// „Speichern und nächste", und danach steht der Cursor wieder im Fragetext.
/// </summary>
public partial class EditQuestionsView : WindowBase
{
    public EditQuestionsView()
    {
        InitializeComponent();

        // Vorschau statt Tastenbindung: ein mehrzeiliges Textfeld nimmt Strg+Enter sonst als
        // Zeilenumbruch, bevor das Fenster es sieht.
        PreviewKeyDown += StrgEnter;

        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is EditQuestionViewModel alt)
                alt.NaechsteGeladen -= FokusAufFragetext;

            if (e.NewValue is EditQuestionViewModel neu)
                neu.NaechsteGeladen += FokusAufFragetext;
        };

        if (DataContext is EditQuestionViewModel vm)
            vm.NaechsteGeladen += FokusAufFragetext;

        Loaded += (_, _) =>
        {
            if (DataContext is EditQuestionViewModel { IstNeu: true })
                FokusAufFragetext(this, EventArgs.Empty);
        };
    }

    private void StrgEnter(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Return || Keyboard.Modifiers != ModifierKeys.Control || DataContext is not EditQuestionViewModel vm)
            return;

        // Erst die Bindung des Feldes unter dem Cursor abschließen - LostFocus-Felder wie der
        // Sollwert hätten den getippten Wert sonst noch nicht übergeben.
        if (Keyboard.FocusedElement is System.Windows.Controls.TextBox feld)
            feld.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty)?.UpdateSource();

        if (vm.SaveAndNextCommand.CanExecute(null))
            vm.SaveAndNextCommand.Execute(null);

        e.Handled = true;
    }

    private void FokusAufFragetext(object? sender, EventArgs e)
        => Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            FragetextFeld.Focus();
            FragetextFeld.SelectAll();
        });
}
