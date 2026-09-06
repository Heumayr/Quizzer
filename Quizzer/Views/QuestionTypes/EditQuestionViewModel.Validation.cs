using Quizzer.Base;
using Quizzer.DataModels.Questions;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Quizzer.Views.QuestionTypes
{
    /// <summary>
    /// Pruefteil des Frage-Editors: sammelt die Beanstandungen und haelt das Speichern an,
    /// solange ein Fehler offen ist. Bis hierher liess sich jede Frage speichern, auch eine
    /// unspielbare - aufgefallen ist das erst mitten im Spiel.
    /// </summary>
    public partial class EditQuestionViewModel
    {
        /// <summary>Die offenen Beanstandungen zur aktuellen Frage.</summary>
        public ObservableCollection<ValidationIssue> Issues { get; } = new();

        /// <summary>Ob es mindestens eine Beanstandung gibt - steuert die Sichtbarkeit der Liste.</summary>
        public bool HasIssues => Issues.Count > 0;

        /// <summary>
        /// Ob gespeichert werden darf.
        /// <para>
        /// <b>Frisch gerechnet, nicht aus der Liste gelesen.</b> Bezeichnung, Kurzform, Punkte
        /// und Fragetext binden unmittelbar ans Modell, und <c>QuestionBase</c> meldet keine
        /// Änderungen - es gibt also keinen Haken, an dem eine Neuprüfung hinge. Gemessen
        /// 2026-09-07 in <b>beide</b> Richtungen: eine geleerte Bezeichnung liess sich speichern
        /// (die Liste blieb leer), und eine erst nach der Kategorie eingetippte Bezeichnung
        /// liess den Knopf grau, obwohl sie dastand - bei der Schätzfrage ohne jeden Ausweg,
        /// weil es dort keine Zeilenliste gibt, über die man versehentlich eine Neuprüfung
        /// auslöst.
        /// </para>
        /// <para>
        /// <c>CommandManager.RequerySuggested</c> feuert bei jedem Tastendruck; der Knopf folgt
        /// damit sofort. <see cref="Issues"/> bleibt an <see cref="Revalidate"/> gebunden - eine
        /// Fehlerliste, die sich beim Tippen laufend umbaut, ist unlesbar.
        /// </para>
        /// </summary>
        public bool CanSave => Question != null
                            && QuestionValidator.IsSavable(Zeileneditor?.Vorschau(Question) ?? Question);

        /// <summary>Das Profil des aktuellen Fragetyps - die Maske richtet sich danach.</summary>
        public QuestionTypeProfile? Profile
            => Question == null ? null : QuestionTypeProfiles.For(Question.Typ);

        /// <summary>Name des Fragetyps fuer die Anzeige.</summary>
        public string TypeDisplayName => Profile?.DisplayName ?? string.Empty;

        /// <summary>Erklaerung zum Fragetyp fuer die Anzeige.</summary>
        public string TypeHelpText => Profile?.HelpText ?? string.Empty;

        /// <summary>
        /// Rechnet die Beanstandungen neu. Wird nach jeder Aenderung an der Frage oder an den
        /// Schritten aufgerufen.
        /// <para>
        /// <b>Geprueft wird der Stand der MASKE, nicht der der Frage.</b> Die Typmaske schreibt
        /// erst beim Speichern zurueck; gegen <c>Question.Steps</c> geprueft trug eine frische
        /// Multiple-Choice-Frage <c>TooFewSteps</c>, obwohl vier Antworten dastanden - und weil
        /// <c>CanSave</c> das Speichern sperrt, kam sie nie dazu, ihre Schritte zu schreiben.
        /// Gemessen am 2026-09-06: sie war ueberhaupt nicht speicherbar.
        /// </para>
        /// </summary>
        public void Revalidate()
        {
            Issues.Clear();

            var geprueft = Zeileneditor != null && Question != null
                ? Zeileneditor.Vorschau(Question)
                : Question;

            if (geprueft != null)
            {
                foreach (var issue in QuestionValidator.Validate(geprueft))
                    Issues.Add(issue);
            }

            OnPropertyChanged(nameof(HasIssues));
            OnPropertyChanged(nameof(CanSave));
            OnPropertyChanged(nameof(CanConvert));
            OnPropertyChanged(nameof(Profile));
            OnPropertyChanged(nameof(TypeDisplayName));
            OnPropertyChanged(nameof(TypeHelpText));
            RaiseLayoutChanged();
            RaiseAppreciateChanged();
            CommandManager.InvalidateRequerySuggested();
        }

        private RelayCommand? cancelCommand;

        /// <summary>
        /// Schliesst den Editor, ohne weitere Aenderungen zu schreiben. Bis hierher gab es
        /// ueberhaupt keinen Weg zurueck - nur das Fensterkreuz.
        /// </summary>
        public ICommand CancelCommand => cancelCommand ??= new RelayCommand(_ =>
        {
            ResultState = DataModels.Enumerations.EditResultState.Canceled;
            Window?.Close();
        });
    }
}
