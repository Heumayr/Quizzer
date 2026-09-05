using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.Logic.Controller.TypedControllers;
using Quizzer.Views.QuestionTypes;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Data;
using Quizzer.DataModels.Helpers;
using System.Windows;
using System.Windows.Input;

namespace Quizzer.Views.HelperViewModels
{
    public class QuestionSelectorViewModel : ViewModelBase
    {
        public Game? Game { get; set; }

        public QuestionBase[] AllQuestion { get; set; } = [];

        public GameGridCoordinate? Coordinate { get; set; }

        public QuestionBase? SelectedQuestion
        {
            get => Coordinate?.QuestionBase;
            set
            {
                Coordinate?.QuestionBase = value;
                Coordinate?.QuestionBaseId = value != null ? value.Id : default;
                OnPropertyChanged(nameof(SelectedQuestion));
                OnPropertyChanged(nameof(CurrentSelectedQuestionDisplay));
            }
        }

        protected override async Task OnloadAsync()
        {
            using var qCtrl = new QuestionBasesController();
            AllQuestion = await qCtrl.GetAllAsync();
            CalculateAvailableQuestions();
        }

        public string CurrentSelectedQuestionDisplay => Coordinate?.QuestionBase != null ? $"{Coordinate.QuestionBase.Category?.Designation} {Coordinate.QuestionBase.Designation} {Coordinate.QuestionBase.Difficulty} {Coordinate.QuestionBase.Points}" : "No question selected";

        public void SetDependencys(Game game, GameGridCoordinate coordinate)
        {
            Game = game;
            Coordinate = coordinate;
            OnModelChanged();
            CalculateAvailableQuestions();
        }

        public void OnModelChanged()
        {
            OnPropertyChanged(nameof(AvailableQuestions));
            OnPropertyChanged(nameof(Game));
            OnPropertyChanged(nameof(Coordinate));
            OnPropertyChanged(nameof(SelectedQuestion));
            OnPropertyChanged(nameof(CurrentSelectedQuestionDisplay));
        }

        public override async Task VMSaveAsync()
        {
            if (Coordinate == null) return;

            // Die Punkte einer Zelle sind gespeicherte Spalten, keine Rechnung beim Anzeigen.
            // Ohne diesen Aufruf schreibt der Dialog den Stand VOR der Zuweisung - meist eine
            // Null - und die Oberflaeche zeigt danach den richtigen Wert, den niemand mehr
            // speichert. Die Frage wird dann fuer null Punkte gespielt.
            if (Game != null)
                Coordinate.Game = Game;

            Coordinate.CalculateAndSetCurrentPoints();

            using var ctrlCoords = new GameGridCoordinatesController();
            await ctrlCoords.UpsertAsync(Coordinate);
            await ctrlCoords.SaveChangesAsync();
        }

        public List<QuestionBase> AvailableQuestions
        {
            get => availableQuestions;
            set
            {
                availableQuestions = value;
                OnPropertyChanged();
            }
        }

        public void CalculateAvailableQuestions()
        {
            var used = Game?.GameGridCoordinates
                .Where(c => c.QuestionBaseId != null && c.QuestionBaseId.Value != Guid.Empty)
                .Select(c => c.QuestionBaseId!.Value)
                .ToHashSet() ?? new HashSet<Guid>();

            var choices = AllQuestion
                .Where(q => q.Id != Guid.Empty && !used.Contains(q.Id))
                .ToList();

            //if (SelectedQuestion != null && !choices.Contains(SelectedQuestion))
            //{
            //    choices.Prepend(SelectedQuestion);
            //}
            AvailableQuestions = choices;
        }

        private AsyncRelayCommand? openQuestionCommand;
        public ICommand OpenQuestionCommand => openQuestionCommand ??= new AsyncRelayCommand(OpenQuestionAsync);

        private async Task OpenQuestionAsync(object? model)
        {
            if (model is QuestionBase questionBase)
            {
                await EditQuestionAsync(questionBase);
            }
        }

        private AsyncRelayCommand? addQuestionCommand;
        public ICommand AddQuestionCommand => addQuestionCommand ??= new AsyncRelayCommand(AddQuestionAsync);

        private Task AddQuestionAsync(object? commandParameter)
        {
            var question = AskForNewQuestion();

            return question == null ? Task.CompletedTask : EditQuestionAsync(question);
        }

        /// <summary>
        /// Fragt den Fragetyp ab und liefert eine neue Frage dieses Typs - oder <c>null</c>,
        /// wenn abgebrochen wurde. Gemeinsamer Einstieg beider Anlegen-Strecken.
        /// </summary>
        private static QuestionBase? AskForNewQuestion()
        {
            var window = new NewQuestionView { Owner = Application.Current?.MainWindow };
            window.ShowDialog();

            var chosen = window.ViewModel?.ChosenType;

            return chosen == null ? null : Factory.CreateNewQuestion(chosen.Value);
        }


        private async Task EditQuestionAsync(QuestionBase questionBase)
        {
            var window = new EditQuestionsView();

            if (window.DataContext is EditQuestionViewModel vm)
            {
                await vm.SetModel(questionBase);
                window.ShowDialog();

                await OnloadAsync();
            }
            else
            {
                throw new InvalidOperationException("DataContext is not of type EditQuestionViewModel");
            }
        }

        private RelayCommand? closeCommand;
        public ICommand CloseCommand => closeCommand ??= new RelayCommand(Close);

        private void Close(object? commandParameter)
        {
            Window?.Close();
        }

        private AsyncRelayCommand? editSelectedQuestionCommand;
        public ICommand EditSelectedQuestionCommand => editSelectedQuestionCommand ??= new AsyncRelayCommand(EditSelectedQuestion);

        private Task EditSelectedQuestion(object? commandParameter)
        {
            if (SelectedQuestion is null)
            {
                return Task.CompletedTask;
            }

            return EditQuestionAsync(SelectedQuestion);
        }

        private RelayCommand? deselectCommand;
        public ICommand DeselectCommand => deselectCommand ??= new RelayCommand(Deselect);

        private void Deselect(object? commandParameter)
        {
            SelectedQuestion = null;
            CalculateAvailableQuestions();
        }

        private AsyncRelayCommand? selectAndCloseCommand;
        private List<QuestionBase> availableQuestions = new();

        public ICommand SelectAndCloseCommand => selectAndCloseCommand ??= new AsyncRelayCommand(SelectAndCloseAsync);

        private async Task SelectAndCloseAsync(object? commandParameter)
        {
            if (commandParameter is QuestionBase questionBase)
            {
                if (SelectedQuestion?.Id != questionBase.Id)
                {
                    SelectedQuestion = questionBase;
                }

                Window?.Close();
            }
        }
    }
}