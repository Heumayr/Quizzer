using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Questions;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Quizzer.Views.QuestionTypes
{
    /// <summary>
    /// Die Typwahl vor dem Anlegen einer Frage.
    /// <para>
    /// Bis hierher gab es dafuer zwei verschiedene Wege: die Fragenliste hatte eine
    /// unbeschriftete Auswahlliste ohne Benachrichtigung (Vorgabe Standardfrage), und die
    /// Fragenauswahl beim Spielfeld erzeugte fest eine Standardfrage - ohne jede Wahl.
    /// Jetzt fuehren beide hierher.
    /// </para>
    /// </summary>
    public class NewQuestionViewModel : ViewModelBase
    {
        /// <summary>Die vier Fragetypen samt Name und Erklaerung.</summary>
        public ObservableCollection<QuestionTypeProfile> Profiles { get; } =
            new(QuestionTypeProfiles.All);

        private QuestionTypeProfile? selectedProfile;

        /// <summary>Der gewaehlte Typ.</summary>
        public QuestionTypeProfile? SelectedProfile
        {
            get => selectedProfile;
            set
            {
                selectedProfile = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanCreate));
                CommandManager.InvalidateRequerySuggested();
            }
        }

        /// <summary>Ob ein Typ gewaehlt wurde.</summary>
        public bool CanCreate => SelectedProfile != null;

        /// <summary>Der gewaehlte Typ, oder <c>null</c> wenn abgebrochen wurde.</summary>
        public QuestionType? ChosenType { get; private set; }

        private RelayCommand? createCommand;

        public ICommand CreateCommand => createCommand ??= new RelayCommand(_ =>
        {
            if (SelectedProfile == null)
                return;

            ChosenType = SelectedProfile.Typ;
            Window?.Close();
        }, _ => CanCreate);

        private RelayCommand? cancelCommand;

        public ICommand CancelCommand => cancelCommand ??= new RelayCommand(_ =>
        {
            ChosenType = null;
            Window?.Close();
        });

        protected override Task OnloadAsync() => Task.CompletedTask;

        public override Task VMSaveAsync() => Task.CompletedTask;
    }
}
