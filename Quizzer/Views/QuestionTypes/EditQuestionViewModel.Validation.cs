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

        /// <summary>Ob gespeichert werden darf.</summary>
        public bool CanSave => Question != null && !Issues.Any(i => i.IsError);

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
        /// </summary>
        public void Revalidate()
        {
            Issues.Clear();

            if (Question != null)
            {
                foreach (var issue in QuestionValidator.Validate(Question))
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
