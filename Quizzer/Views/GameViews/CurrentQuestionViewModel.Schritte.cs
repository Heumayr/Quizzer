using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.Views.GameViews.QuestionViews;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Quizzer.Views.GameViews
{
    /// <summary>
    /// Der Weg durch die Schritte einer Frage: welcher gerade auf dem Beamer steht, welcher als
    /// nächster kommt, und die Rückfragen davor.
    /// <para>
    /// <b>Eigene Teildatei seit 2026-09-07</b>, weil <c>CurrentQuestionViewModel.cs</c> in der
    /// Nacht über die Größen-Obergrenze gewachsen ist (494 → 522 Codezeilen). Gefunden hat das
    /// nicht der Übersetzer, sondern <c>check-code.ps1</c> — nachdem dessen Größenvergleich
    /// selbst repariert war: er verglich Codezeilen gegen physische Zeilen und war damit
    /// systematisch blind für genau diesen Fall.
    /// </para>
    /// <para>
    /// Es ist dieselbe Teilung nach Thema wie <c>.ResultWindow</c>, <c>.Appreciate</c> und
    /// <c>.Buzzer</c> daneben.
    /// </para>
    /// </summary>
    public partial class CurrentQuestionViewModel
    {
        public QuestionStepResource[] QuestionOrderedSteps => Question?.OrderedSteps ?? [];

        private QuestionStepResource? currentStep;

        public QuestionStepResource? CurrentStep
        {
            get => currentStep;
            set
            {
                currentStep = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentStepContext));
            }
        }

        private QuestionStepResource? finishStep;

        public QuestionStepResource? FinishStep
        {
            get => finishStep;
            set
            {
                finishStep = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FinishStepContext));
            }
        }

        private QuestionStepResource? nextStep;

        public QuestionStepResource? NextStep
        {
            get => nextStep;
            set
            {
                nextStep = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(NextStepContext));
            }
        }

        public QuestionStepViewContext CurrentStepContext
        {
            get
            {
                var current = new QuestionStepViewContext()
                {
                    Owner = this,
                    Step = CurrentStep,
                    IsMasterView = true,
                    DisplayLayoutMode = Question?.StepDisplayLayoutMode ?? StepDisplayLayoutMode.Vertical
                };

                GamePlayerViewModel?.SetView(QuestionStepViewContext.CloneForDifferentView(current, false, Question?.StepDisplayLayoutMode ?? StepDisplayLayoutMode.Vertical));
                return current;
            }
        }

        public QuestionStepViewContext FinishStepContext => new()
        {
            Owner = this,
            Step = FinishStep,
            IsMasterView = true,
            DisplayLayoutMode = Question?.StepDisplayLayoutMode ?? StepDisplayLayoutMode.Vertical,
        };

        public QuestionStepViewContext NextStepContext => new()
        {
            Owner = this,
            Step = NextStep,
            IsMasterView = true,
            DisplayLayoutMode = Question?.StepDisplayLayoutMode ?? StepDisplayLayoutMode.Vertical,
        };

        public ObservableCollection<QuestionStepResource> SelectedSteps { get; set; } = new();

        private AsyncRelayCommand? _startStepCommand;
        public AsyncRelayCommand? StartStepCommnad => _startStepCommand ??= new AsyncRelayCommand(StartStepAsync);

        private async Task StartStepAsync(object? arg)
        {
            CurrentStep = Question?.OrderedSteps.FirstOrDefault();
            NextStep = Question?.GetNextStep(CurrentStep);
        }

        private AsyncRelayCommand? _nextStepCommand;
        public AsyncRelayCommand? NextStepCommnad => _nextStepCommand ??= new AsyncRelayCommand(NextStepAsync);

        private async Task NextStepAsync(object? arg)
        {
            SetStepsView(CurrentStep);
        }

        private void SetStepsView(QuestionStepResource? currentStep, bool up = true)
        {
            if (Question == null)
            {
                UserPrompt.Inform("Keine Frage gesetzt.");
                return;
            }

            // Eine Frage ohne Schritte hat nichts zum Aufdecken. Bis 2026-09-06 lief das in
            // Last() auf einer leeren Folge und riss ein Fehlerfenster samt Stapelspur auf -
            // beim ersten Enter, vor Publikum.
            if (Question.OrderedSteps.Length == 0)
            {
                UserPrompt.Inform(
                    "Diese Frage hat keine Schritte. Im Frage-Editor mindestens einen anlegen, "
                    + "sonst gibt es nichts aufzudecken.",
                    "Frage ohne Schritte");

                return;
            }

            if (up)
            {
                if (CurrentStep == Question.OrderedSteps.Last())
                {
                    //MessageBox.Show("End reached");
                    return;
                }

                bool flowControl = Proceed(NextStep);
                if (!flowControl)
                {
                    return;
                }

                CurrentStep = NextStep;
                NextStep = Question.GetNextStep(CurrentStep);
            }
            else
            {
                if (CurrentStep == Question.OrderedSteps.First())
                {
                    //MessageBox.Show("Start reached");
                    return;
                }

                NextStep = CurrentStep;
                CurrentStep = Question.GetStepBehind(CurrentStep);
            }
        }

        private bool Proceed(QuestionStepResource? next)
        {
            if (Question == null || next == null)
                return true;

            if (Question.WarnOnResultStep
                && ((next?.IsResult ?? false) && (!CurrentStep?.IsResult ?? true))
                && !UserPrompt.Confirm("Der nächste Schritt ist die Lösung. Trotzdem weiter?", "Lösungsschritt voraus"))
            {
                return false;
            }

            if (Question.WarnOnFinishStep
                && ((next?.IsFinish ?? false) && (!CurrentStep?.IsFinish ?? true))
                && !UserPrompt.Confirm("Der nächste Schritt ist der Abschluss. Trotzdem weiter?", "Abschlussschritt voraus"))
            {
                return false;
            }

            return true;
        }

        private AsyncRelayCommand? _backStepCommand;
        public AsyncRelayCommand? BackStepCommnad => _backStepCommand ??= new AsyncRelayCommand(BackStepAsync);

        private async Task BackStepAsync(object? arg)
        {
            SetStepsView(CurrentStep, false);
        }

        private AsyncRelayCommand? _openStepCommand;

        public AsyncRelayCommand? OpenStepCommand => _openStepCommand ??= new AsyncRelayCommand(OpenStepAsync);

        private async Task OpenStepAsync(object? arg)
        {
            if (Question == null)
            {
                UserPrompt.Inform("Keine Frage gesetzt.");
                return;
            }

            var first = SelectedSteps?.FirstOrDefault();

            if (first == currentStep) return;

            bool flowControl = Proceed(first);
            if (!flowControl)
            {
                return;
            }

            CurrentStep = first;
            NextStep = Question.GetNextStep(CurrentStep);
        }

        private AsyncRelayCommand? saveIsDoneFinishStateCommand;
        public ICommand SaveIsDoneFinishStateCommand => saveIsDoneFinishStateCommand ??= new AsyncRelayCommand(SaveIsDoneFinishStateAsync);

        /// <summary>
        /// Schliesst die Zelle ab und stellt den Abschlussschritt auf den Spielerbildschirm.
        /// <para>
        /// Hat die Frage keinen eigenen Abschlussschritt, bleibt der zuletzt gezeigte stehen -
        /// sonst wird der Beamer leer, und die Mitspieler sehen bis zum Schliessen des Fensters
        /// nichts mehr.
        /// </para>
        /// <para>
        /// <c>FinishStep</c> ist fast immer gesetzt: <c>CalculateOrderdSteps</c> ergaenzt einen,
        /// wenn keiner hinterlegt ist. Eine Pruefung allein auf <c>null</c> brauchte es also
        /// nicht - es zaehlt, ob der Schritt etwas zu <i>zeigen</i> hat, und der ergaenzte hat
        /// nichts.
        /// </para>
        /// <para>
        /// <b>Hier stand bis 2026-09-06 „nie <c>null</c>", und das war falsch.</b> Bei einer
        /// Frage ganz ohne Schritte steigt <c>CalculateOrderdSteps</c> vorher aus, und
        /// <c>finishStep</c> bleibt <c>null</c> - in der Spieldatenbank gibt es genau so eine.
        /// <c>HasSomethingToShow</c> faengt den Fall ab; die Begruendung tat es nicht.
        /// </para>
        /// </summary>
        private async Task SaveIsDoneFinishStateAsync(object? commandParameter)
        {
            IsDone = true;

            await VMSaveAsync();

            if (HasSomethingToShow(finishStep))
                CurrentStep = finishStep;

            NextStep = null;
        }

        /// <summary>Ob ein Schritt Text oder ein Medium mitbringt.</summary>
        private static bool HasSomethingToShow(QuestionStepResource? step)
        {
            if (step == null)
                return false;

            return !string.IsNullOrWhiteSpace(step.StepText)
                || !string.IsNullOrWhiteSpace(step.Designation)
                || step.HasResource;
        }
    }
}
