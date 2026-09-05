using Newtonsoft.Json;
using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Helpers;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.Logic.Controller.TypedControllers;
using Quizzer.Views.QuestionTypes;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Data;
using System.Windows;
using System.Windows.Input;

namespace Quizzer.Views
{
    internal class QuestionsViewModel : ViewModelBase
    {
        private ObservableCollection<QuestionBase> _questions = new();

        public ObservableCollection<QuestionBase> Questions
        {
            get => _questions;
            private set
            {
                _questions = value;
                OnPropertyChanged();
                QuestionsView = CollectionViewSource.GetDefaultView(_questions);
                OnPropertyChanged(nameof(QuestionsView));
            }
        }

        public ICollectionView? QuestionsView { get; private set; }

        public override async Task VMSaveAsync()
        {
            using var ctrl = new QuestionBasesController();

            foreach (var q in Questions)
            {
                await ctrl.UpsertAsync(q);
            }

            await ctrl.SaveChangesAsync();
        }

        private AsyncRelayCommand? saveCommand;
        public ICommand SaveCommand => saveCommand ??= new AsyncRelayCommand(SaveCommandAsync);

        private async Task SaveCommandAsync(object? param)
        {
            await VMSaveAsync();
        }

        protected override async Task OnloadAsync()
        {
            using var ctrl = new QuestionBasesController();
            var questions = await ctrl.GetAllAsync();

            Questions = new ObservableCollection<QuestionBase>(questions);
        }

        public ObservableCollection<QuestionBase> SelectedQuestions { get; set; } = new();

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

        private AsyncRelayCommand? removeQuestionCommand;
        public ICommand RemoveQuestionCommand => removeQuestionCommand ??= new AsyncRelayCommand(RemoveQuestionAsync);

        /// <summary>
        /// Entfernt die ausgewählten Fragen - nach Rückfrage, und nicht, wenn sie in einem
        /// Spielfeld liegen.
        /// <para>
        /// Bis 2026-09-06 löschte dieser Weg <b>ohne jede Rückfrage</b>. Zwei Folgen hingen
        /// daran: <c>QuestionResult.QuestionBaseId</c> steht auf CASCADE, mit der Frage
        /// verschwand also ihre gesamte Spielhistorie; und liegt die Frage in einem Raster,
        /// scheitert das Löschen am NO ACTION von <c>GameGridCoordinate</c> - der Spielleiter
        /// bekam einen rohen Datenbankfehler zu sehen.
        /// </para>
        /// </summary>
        private async Task RemoveQuestionAsync(object? commandParameter)
        {
            if (SelectedQuestions == null || SelectedQuestions.Count == 0)
            {
                return;
            }

            var toRemove = new List<QuestionBase>(SelectedQuestions);

            if (!await ConfirmRemovalAsync(toRemove))
                return;

            using var ctrl = new QuestionBasesController();

            foreach (var question in toRemove)
            {
                await ctrl.DeleteAsync(question.Id);
            }

            await ctrl.SaveChangesAsync();
            await OnloadAsync();
        }

        /// <summary>
        /// Prüft, ob die Fragen überhaupt löschbar sind, und fragt sonst nach - mit dem, was
        /// dabei verloren geht.
        /// </summary>
        private static async Task<bool> ConfirmRemovalAsync(List<QuestionBase> toRemove)
        {
            using var ctrlZellen = new GameGridCoordinatesController();

            var belegt = new List<string>();

            foreach (var frage in toRemove)
            {
                var spiele = await ctrlZellen.GameNamesUsingQuestionAsync(frage.Id);

                if (spiele.Count > 0)
                    belegt.Add($"{frage.Designation} - liegt in: {string.Join(", ", spiele)}");
            }

            if (belegt.Count > 0)
            {
                UserPrompt.Inform(
                    "Diese Fragen liegen in einem Spielfeld und lassen sich nicht löschen:"
                    + Environment.NewLine + Environment.NewLine
                    + string.Join(Environment.NewLine, belegt)
                    + Environment.NewLine + Environment.NewLine
                    + "Zuerst im Spielaufbau die Zuweisung entfernen.",
                    "Frage entfernen");

                return false;
            }

            using var ctrlErgebnisse = new QuestionResultsController();

            var ergebnisse = await ctrlErgebnisse.CountResultsOfQuestionsAsync(toRemove.Select(q => q.Id));

            var namen = string.Join(", ", toRemove.Select(q => q.Designation));
            var zeilen = ergebnisse == 1 ? "1 Ergebniszeile" : $"{ergebnisse} Ergebniszeilen";

            var frageText = ergebnisse == 0
                ? $"{namen} entfernen?"
                : $"{namen} entfernen?" + Environment.NewLine + Environment.NewLine
                  + $"Dabei werden {zeilen} aus gespielten Runden mitgelöscht. Das lässt sich "
                  + "nicht rückgängig machen.";

            return UserPrompt.Confirm(frageText, "Frage entfernen");
        }
    }
}