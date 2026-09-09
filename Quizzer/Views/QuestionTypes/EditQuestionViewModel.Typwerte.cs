using Quizzer.Base;
using Quizzer.DataModels.Questions;
using System.Windows;
using System.Windows.Input;

namespace Quizzer.Views.QuestionTypes
{
    /// <summary>
    /// Der Weg zurück, wenn die typeigenen Werte einer Frage von ihrem Profil abweichen.
    /// <para>
    /// <b>Gemessen am 2026-09-09 an der Spieldatenbank, und es war eine Sackgasse:</b> die Frage
    /// „Appre Frage" trug <c>QuestionViewKeyType = Alphabetical</c>, während das Profil der
    /// Schätzfrage <c>Numerical</c> verlangt. Der Prüfer meldet das als <b>Fehler</b>, damit
    /// sperrt <see cref="EditQuestionViewModel.CanSave"/> das Speichern - und der abweichende
    /// Wert ist in der Maske <b>nirgends</b> änderbar, weil ihn der Fragetyp setzt. Die Frage
    /// war damit nicht spielbar, nicht speicherbar und nicht reparierbar; es blieb nur, sie zu
    /// löschen.
    /// </para>
    /// <para>
    /// <b>Warum nicht still beim Laden richten:</b> dann verschwände der Hinweis, dass etwas an
    /// der Maske vorbei geschrieben hat - und genau das ist der Befund. Der Nutzer bekommt den
    /// Fehler zu sehen <b>und</b> einen Knopf, der ihn behebt.
    /// </para>
    /// </summary>
    public partial class EditQuestionViewModel
    {
        /// <summary>Ob die Frage von den typeigenen Vorgaben abweicht.</summary>
        public bool HasTypeOwnedDrift => Question != null
                                      && Profile != null
                                      && !Profile.MatchesOwnedValues(Question);

        /// <summary>Der Knopf erscheint nur, wenn es etwas zu richten gibt.</summary>
        public Visibility TypeOwnedRepairVisibility
            => HasTypeOwnedDrift ? Visibility.Visible : Visibility.Collapsed;

        private RelayCommand? repairTypeOwnedValuesCommand;

        /// <summary>
        /// Setzt die typeigenen Werte auf das, was das Profil vorgibt - dieselbe Rechnung, die
        /// auch der Konstruktor und das Umwandeln benutzen.
        /// </summary>
        public ICommand RepairTypeOwnedValuesCommand
            => repairTypeOwnedValuesCommand ??= new RelayCommand(
                _ =>
                {
                    if (Question == null || Profile == null)
                        return;

                    Profile.ApplyTo(Question);

                    OnPropertyChanged(nameof(HasTypeOwnedDrift));
                    OnPropertyChanged(nameof(TypeOwnedRepairVisibility));
                    OnPropertyChanged(nameof(BuzzerLayoutText));
                    Revalidate();
                },
                _ => HasTypeOwnedDrift);
    }
}
