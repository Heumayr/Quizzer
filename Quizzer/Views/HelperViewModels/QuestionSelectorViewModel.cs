using Quizzer.Base;
using Quizzer.DataModels;
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

                // Bewusst (Guid?)null und nicht default: der Ausdruck haette sonst den Typ Guid,
                // und "keine Frage" waere Guid.Empty. Der Aufrufer prueft aber auf HasValue -
                // nach einem "Auswahl entfernen" sprang er deshalb an seinem Nachladezweig
                // vorbei, und die geleerte Zelle zeigte weiter ihre alten Punkte.
                Coordinate?.QuestionBaseId = value != null ? value.Id : (Guid?)null;

                OnPropertyChanged(nameof(SelectedQuestion));
                OnPropertyChanged(nameof(CurrentSelectedQuestionDisplay));
            }
        }

        protected override async Task OnloadAsync()
        {
            using var qCtrl = new QuestionBasesController();

            // Derselbe Filter wie in der Fragenliste: was der angemeldete Spielleiter nicht
            // sieht, darf er auch nicht auf eine Zelle legen.
            AllQuestion = QuestionOwnership.VisibleTo(await qCtrl.GetAllAsync()).ToArray();
            CalculateAvailableQuestions();
        }

        /// <summary>
        /// Was in der Maske neben „Ausgewählt" steht.
        /// <para>
        /// <b>Deutsch seit 2026-09-07</b> - hier stand „No question selected", und zwar sichtbar
        /// in einer sonst durchgehend deutschen Maske (<c>QuestionSelectorView.xaml</c>, Label
        /// neben der Auswahl).
        /// </para>
        /// </summary>
        public string CurrentSelectedQuestionDisplay => Coordinate?.QuestionBase != null
            ? $"{Coordinate.QuestionBase.Category?.Designation} {Coordinate.QuestionBase.Designation} {Coordinate.QuestionBase.Difficulty} {Coordinate.QuestionBase.Points}"
            : "Keine Frage ausgewählt";

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

            // Hinweis: diese Methode hat heute keinen Aufrufer - der Auswahldialog wird ueber
            // ShowDialog geoeffnet und geschlossen, ohne dass jemand VMSaveAsync ruft.
            // Geschrieben wird die Zelle in EditGameViewModel.SaveCoordinateAsync. Die Punkte
            // werden hier trotzdem gerechnet, damit die Methode richtig bleibt, falls sie je
            // wieder angeschlossen wird.
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

        /// <summary>
        /// Legt eine neue Frage an - und legt sie danach auf die Kachel.
        /// <para>
        /// <b>Nutzerwunsch vom 2026-09-06:</b> „wenn man ein spiel editiert sollte man wenn man
        /// eine kachel wählt dort nicht nur bestehende fragen auswählen können sonder ggf. auch
        /// neue anlegen über den fragen editor".
        /// </para>
        /// <para>
        /// <b>Den Knopf gab es schon</b> - was fehlte, war der letzte Schritt: die neu angelegte
        /// Frage landete in der Liste, und man musste sie dort noch einmal suchen und anklicken.
        /// Wer aus einer Kachel heraus anlegt, meint diese Kachel.
        /// </para>
        /// </summary>
        private async Task AddQuestionAsync(object? commandParameter)
        {
            var question = AskForNewQuestion();

            if (question == null)
                return;

            var gespeichert = await EditQuestionAsync(question);

            if (!gespeichert || question.Id == Guid.Empty)
                return;

            SelectedQuestion = question;

            Window?.Close();
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


        /// <summary>
        /// Öffnet den Frageneditor. Meldet zurück, ob wirklich gespeichert wurde - ein Abbruch
        /// darf nichts auf die Kachel legen.
        /// </summary>
        private async Task<bool> EditQuestionAsync(QuestionBase questionBase)
        {
            var window = new EditQuestionsView();

            if (window.DataContext is not EditQuestionViewModel vm)
                throw new InvalidOperationException("DataContext is not of type EditQuestionViewModel");

            await vm.SetModel(questionBase);
            window.ShowDialog();

            await OnloadAsync();

            return vm.ResultState is EditResultState.New or EditResultState.Updated;
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