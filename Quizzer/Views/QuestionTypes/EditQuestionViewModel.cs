using Quizzer.Base;

using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.Logic.Controller.TypedControllers;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using System.Windows.Data;
using System.Windows.Input;

namespace Quizzer.Views.QuestionTypes
{
    public partial class EditQuestionViewModel : ViewModelBase
    {
        public Array Difficulties { get; } = Enum.GetValues(typeof(Difficulty));

        public ObservableCollection<QuestionStepResource> Steps
        {
            get => steps;
            set
            {
                steps = value;
                OnModelChanged();
                StepsView = CollectionViewSource.GetDefaultView(steps);
                OnPropertyChanged(nameof(StepsView));
            }
        }

        public ICollectionView? StepsView { get; private set; }

        public ObservableCollection<Category> Categories
        {
            get => categories;
            set
            {
                categories = value;
                OnPropertyChanged();
            }
        }

        public Category? SelectedCategory
        {
            get
            {
                return _question?.Category;
            }

            set
            {
                _question?.Category = value;

                if (value != null)
                {
                    _question?.CategoryId = value.Id;
                }

                OnPropertyChanged();
                Revalidate();
            }
        }

        protected override async Task OnloadAsync()
        {
            using var catCtrl = new CategoriesController();

            var categories = await catCtrl.GetAllAsync();

            Categories = new ObservableCollection<Category>(categories);

            if (Question != null && Question.CategoryId != Guid.Empty)
                SelectedCategory = categories.FirstOrDefault(c => c.Id == Question.CategoryId);

            OnPropertyChanged(nameof(SelectedCategory));
            Revalidate();
        }

        public List<QuestionStepResource> SelectedSteps { get; set; } = new List<QuestionStepResource>();

        private QuestionBase? _question;

        public QuestionBase? Question
        {
            get => _question;
            set
            {
                if (value == null)
                    throw new Exception("Can't set null model");

                _question = value;

                // Frueher stand hier ein Zugriff auf Categories - die Liste ist zu diesem
                // Zeitpunkt aber noch leer (SetModel laeuft vor OnloadAsync), und der Setter
                // von SelectedCategory hat dabei Question.Category auf null gesetzt.
                // Aufgeloest wird die Kategorie jetzt ausschliesslich in OnloadAsync.
                OnModelChanged();
                Steps = new ObservableCollection<QuestionStepResource>(_question.Steps.OrderBy(s => s.SequenceNumber));
                Revalidate();
            }
        }

        public void OnModelChanged()
        {
            OnPropertyChanged(nameof(SelectedCategory));
            OnPropertyChanged(nameof(Question));
            //OnDatagridSourceChanged();
        }

        public async Task SetModel(QuestionBase questionBase)
        {
            ResultState = EditResultState.Canceled;
            await LoadModel(questionBase);
        }

        internal async Task LoadModel(QuestionBase questionBase)
        {
            if (questionBase.Id == Guid.Empty)
            {
                Question = questionBase;
                return;
            }

            using var ctrl = new QuestionBasesController();
            var q = await ctrl.GetAsync(questionBase.Id);

            if (q == null)
                throw new Exception("Model could not be loaded");

            Question = q;
        }

        private AsyncRelayCommand? saveCommand;

        public ICommand SaveCommand => saveCommand ??= new AsyncRelayCommand(SaveAsync, _ => CanSave);

        private async Task SaveAsync(object? commandParameter)
        {
            if (Question == null) return;

            await VMSaveAsync();
            await LoadModel(Question);
            Revalidate();
        }

        public override async Task VMSaveAsync()
        {
            if (Question == null) return;

            var wasNew = Question.Id == Guid.Empty;

            // Nur eine neue Frage bekommt einen Besitzer. Eine bestehende ohne Besitzer gehoert
            // allen - sie beim blossen Oeffnen zu vereinnahmen wuerde sie den anderen
            // Spielleitern wegnehmen.
            if (wasNew)
                DataModels.QuestionOwnership.ClaimIfUnowned(Question);

            using var ctrl = new QuestionBasesController();
            await ctrl.SaveWithStepsAsync(Question);
            await ctrl.SaveChangesAsync();
            ResultState = wasNew ? EditResultState.New : EditResultState.Updated;
        }

        private AsyncRelayCommand? saveAndCloseCommand;
        public ICommand SaveAndCloseCommand => saveAndCloseCommand ??= new AsyncRelayCommand(SaveAndCloseAsync, _ => CanSave);

        private async Task SaveAndCloseAsync(object? param)
        {
            await SaveAsync(param);
            Window?.Close();
        }

        private AsyncRelayCommand? addStepCommnad;
        public ICommand AddStepCommnad => addStepCommnad ??= new AsyncRelayCommand(AddStepCommnadAsync);

        private Task AddStepCommnadAsync(object? commandParameter)
        {
            if (Question == null)
            {
                return Task.CompletedTask;
            }

            var step = new QuestionStepResource
            {
                Id = Guid.NewGuid(),
                QuestionBaseId = Question.Id,
                SequenceNumber = Question.Steps.Any() ? Question.Steps.Max(q => q.SequenceNumber) + 10 : 0,
            };

            Question.Steps.Add(step);

            return EditStepAsync(step);
        }

        private AsyncRelayCommand? removeStepCommnad;
        public ICommand RemoveStepCommnad => removeStepCommnad ??= new AsyncRelayCommand(RemoveStepCommnadAsync);

        private Task RemoveStepCommnadAsync(object? commandParameter)
        {
            if (Question == null)
            {
                throw new InvalidOperationException("Question is null");
            }

            // Nur aus der Frage nehmen - geschrieben wird beim Speichern, ueber
            // SaveWithStepsAsync. Frueher wurde hier sofort und ohne Rueckfrage geloescht,
            // auch wenn der Spielleiter danach abgebrochen hat.
            foreach (var step in SelectedSteps.ToList())
            {
                Question.Steps.Remove(step);
            }

            RefreshSteps();

            return Task.CompletedTask;
        }

        /// <summary>Uebernimmt den Schrittstand der Frage in die angezeigte Liste.</summary>
        private void RefreshSteps()
        {
            if (Question == null)
                return;

            Steps = new ObservableCollection<QuestionStepResource>(
                Question.Steps.OrderBy(s => s.SequenceNumber));

            Revalidate();
        }

        private async Task EditStepAsync(QuestionStepResource step)
        {
            if (Question == null) return;

            var window = new EditStepView();

            if (window.DataContext is EditStepViewModel vm)
            {
                step.QuestionBaseId = Question.Id;

                // Der Schritt gehoert der Frage im Speicher; geschrieben wird beides zusammen
                // beim Speichern. Deshalb kein Vorab-Speichern der halbfertigen Frage mehr.
                vm.PersistDirectly = false;

                await vm.SetModel(step);
                window.ShowDialog();

                RefreshSteps();
            }
            else
            {
                throw new InvalidOperationException("DataContext is not of type EditQuestionViewModel");
            }
        }

        //private void OnDatagridSourceChanged()
        //{
        //    if (Question == null)
        //    {
        //        return;
        //    }

        //    var view = CollectionViewSource.GetDefaultView(Question.Steps);
        //    view.SortDescriptions.Clear();
        //    view.SortDescriptions.Add(
        //        new System.ComponentModel.SortDescription(
        //            nameof(QuestionStepResource.SequenceNumber),
        //            System.ComponentModel.ListSortDirection.Ascending));

        //    view.Refresh();
        //}

        private AsyncRelayCommand? openStepCommand;
        private ObservableCollection<Category> categories = new();

        //private bool warnOnResultStep;
        private ObservableCollection<QuestionStepResource> steps = new();

        public ICommand OpenStepCommand => openStepCommand ??= new AsyncRelayCommand(OpenStepAsync);

        private Task OpenStepAsync(object? commandParameter)
        {
            if (commandParameter is QuestionStepResource step)
            {
                return EditStepAsync(step);
            }

            return Task.CompletedTask;
        }
    }
}