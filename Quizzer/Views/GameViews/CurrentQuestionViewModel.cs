using LocalBuzzer.Service;
using LocalBuzzer.Service.Base.States;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json.Linq;
using Quizzer.Base;
using Quizzer.DataModels;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.Extentions;
using Quizzer.Logic.Controller.TypedControllers;
using Quizzer.Views.BuzzerViews;
using Quizzer.Views.GameViews.QuestionViews;
using Quizzer.Views.GameViews.QuestionViews.Typed.Media;
using Quizzer.Views.StaticRessources;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Quizzer.Views.GameViews
{
    public partial class CurrentQuestionViewModel : ViewModelBase
    {
        public GamePlayerViewModel? GamePlayerViewModel { get; set; }
        public GameMasterViewModel? GameMasterViewModel { get; set; }

        public PlayersResultViewModel? PlayersResultViewModel { get; set; }

        public BuzzerControlsViewModel? BuzzerControlsViewModel => StaticManager.BuzzerServerViewModel.BuzzerControlsViewModel;

        private string backgroundImagePath = Settings.BackgroundImagePath;
        public Brush HeaderBrush { get; set; } = Brushes.Black;

        public string BackgroundImagePath
        {
            get => backgroundImagePath;
            set
            {
                backgroundImagePath = value;
                OnPropertyChanged();
            }
        }


        public CurrentQuestionViewModel()
        {
            //StaticManager.BuzzerServerViewModel.PlayerConnectionStateChanged += OnPlayerConnectionStateChanged;
        }

        protected override async Task OnloadAsync()
        {
            //OnPlayerConnectionStateChanged(this, StaticManager.BuzzerServerViewModel.ServerState);

            if (Coordinate == null)
                return;

            if (Coordinate.QuestionBaseId == Guid.Empty)
            {
                UserPrompt.Inform("Keine Frage gesetzt.");
                return;
            }

            //reload question
            using var ctrlQ = new QuestionBasesController();
            Coordinate.QuestionBase = (await ctrlQ.GetAsync(Coordinate.QuestionBaseId));

            Coordinate.QuestionBase?.CalculateOrderdSteps();

            NextStep = Coordinate.QuestionBase?.OrderedSteps.FirstOrDefault();
            FinishStep = Coordinate.QuestionBase?.OrderedSteps.FirstOrDefault(s => s.IsFinish);

            PlayersResultViewModel = new PlayersResultViewModel
            {
                GamePlayerViewModel = GamePlayerViewModel,
                CurrentQuestionViewModel = this
            };
            //_resultWindow = new PlayersResultView();
            //_resultWindow.DataContext = PlayersResultViewModel;

            await PlayersResultViewModel.SetCoordinateAsync(Coordinate);

            await OnModeldChanged();

            //Buzzer
            await PrepareBuzzerlayoutAsync();

            (Window as WindowBase)?.SetFullscreen(GameMasterViewModel?.IsFullScreen ?? false);
        }

        protected override Task OnWindow_ContentRenderedAsync()
        {
            return base.OnWindow_ContentRenderedAsync();
        }

        private async Task OnModeldChanged()
        {
            OnPropertyChanged(nameof(QuestionType));
            OnPropertyChanged(nameof(QuestionDesignation));
            OnPropertyChanged(nameof(QuestionDesignationShort));
            OnPropertyChanged(nameof(QuestionCategory));
            OnPropertyChanged(nameof(QuestionNotes));
            OnPropertyChanged(nameof(QuestionDifficulty));
            OnPropertyChanged(nameof(Phase));
            OnPropertyChanged(nameof(CurrentPoints));
            OnPropertyChanged(nameof(CurrentMinusPoints));

            OnPropertyChanged(nameof(QuestionOrderedSteps));

            OnPropertyChanged(nameof(CurrentStep));
            OnPropertyChanged(nameof(FinishStep));
            OnPropertyChanged(nameof(NextStep));
            OnPropertyChanged(nameof(CurrentStepContext));
            OnPropertyChanged(nameof(FinishStepContext));
            OnPropertyChanged(nameof(NextStepContext));

            OnPropertyChanged(nameof(IsDone));
            OnPropertyChanged(nameof(DoneStateText));
            OnPropertyChanged(nameof(WindowTitle));
            OnPropertyChanged(nameof(QuestionSummary));
            OnPropertyChanged(nameof(QuestionNotesLine));
            OnPropertyChanged(nameof(NotesVisibility));
        }

        protected override async Task OnClosed()
        {
            GamePlayerViewModel?.SetView(null);

            await ClearBuzzerLayouts();

            await base.OnClosed();
        }

        //private Brush backgroundBrush = Brushes.DarkGray;

        //public Brush BackgroundBrush
        //{
        //    get => backgroundBrush;
        //    set
        //    {
        //        backgroundBrush = value;
        //        OnPropertyChanged();
        //    }
        //}

        //private void OnPlayerConnectionStateChanged(object? sender, ServerState e)
        //{
        //    BackgroundBrush = e switch
        //    {
        //        ServerState.None => Brushes.DarkGray,
        //        ServerState.Running => Brushes.Red,
        //        ServerState.Stopping => Brushes.Red,
        //        ServerState.Stopped => Brushes.DarkGray,
        //        ServerState.AllConnected => Brushes.Black,
        //        ServerState.ActiveState => Brushes.Black,
        //        _ => Brushes.Red
        //    };

        //    BuzzerState = e.DescriptionOrString();
        //}

        public GameGridCoordinate? Coordinate { get; set; }

        public bool IsDone
        {
            get
            {
                return Coordinate?.IsDone ?? false;
            }
            set
            {
                Coordinate?.IsDone = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsDoneBrush));
                OnPropertyChanged(nameof(DoneStateText));
            }
        }

        public Brush IsDoneBrush
        {
            get
            {
                if (IsDone)
                {
                    return Brushes.DarkRed;
                }

                return Brushes.Black;
            }
        }

        public QuestionBase? Question => Coordinate?.QuestionBase;

        /// <summary>Titel des Fragefensters, mit der Kurzbezeichnung der Frage.</summary>
        public string WindowTitle
        {
            get
            {
                var kurz = Question?.DesignationShort;

                if (string.IsNullOrWhiteSpace(kurz))
                    kurz = Question?.Designation;

                return string.IsNullOrWhiteSpace(kurz) ? "Frage" : $"Frage – {kurz}";
            }
        }

        /// <summary>
        /// Kategorie, Stufe, Punkte und Phase in einer Zeile. Sie stand bisher in neun einzelnen
        /// schreibgeschuetzten Textfeldern, die ein Viertel des Fensters belegten.
        /// </summary>
        public string QuestionSummary
        {
            get
            {
                if (Question == null)
                    return string.Empty;

                var teile = new List<string>();

                if (!string.IsNullOrWhiteSpace(QuestionCategory))
                    teile.Add(QuestionCategory);

                teile.Add(QuestionType);
                teile.Add($"Stufe {(int)Question.Difficulty}");
                teile.Add($"{CurrentPoints} / −{CurrentMinusPoints} Punkte");
                teile.Add($"Phase {Phase}");

                return string.Join(" · ", teile);
            }
        }

        /// <summary>Die Notizen des Spielleiters; die Spieler sehen sie nicht.</summary>
        public string QuestionNotesLine =>
            string.IsNullOrWhiteSpace(QuestionNotes) ? string.Empty : $"Notiz: {QuestionNotes}";

        public Visibility NotesVisibility =>
            string.IsNullOrWhiteSpace(QuestionNotes) ? Visibility.Collapsed : Visibility.Visible;

        /// <summary>
        /// Ob die Zelle abgeschlossen ist, als Satz. Bisher trug das allein ein duenner
        /// dunkelroter Rahmen, den man leicht uebersieht.
        /// </summary>
        public string DoneStateText =>
            IsDone ? "Zelle abgeschlossen" : "Noch nicht abgeschlossen";

        public BuzzerServerViewModel? BuzzerServerViewModel => StaticRessources.StaticManager.BuzzerServerViewModel;

        public override async Task VMSaveAsync()
        {
            if (Coordinate == null)
                return;

            using var ctrl = new GameGridCoordinatesController();

            await ctrl.UpdateAsync(Coordinate);
            await ctrl.SaveChangesAsync();
        }

        public string QuestionType => Question?.TypDisplayName ?? String.Empty;
        public string QuestionDesignation => Question?.Designation ?? String.Empty;
        public string QuestionDesignationShort => Question?.DesignationShort ?? String.Empty;
        public string QuestionCategory => Question?.Category?.Designation ?? String.Empty;
        public string QuestionNotes => Question?.Notes ?? String.Empty;
        public string QuestionDifficulty => Question?.Difficulty.DescriptionOrString().ToString() ?? String.Empty;
        public int Phase => Coordinate?.Phase ?? 0;
        public int CurrentPoints => Coordinate?.CurrentPoints ?? 0;
        public int CurrentMinusPoints => Coordinate?.CurrentMinusPoints ?? 0;

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

        #region Buzzer

        private string buzzerState = string.Empty;

        public string BuzzerState
        {
            get => buzzerState;
            set
            {
                buzzerState = value;
                OnPropertyChanged();
            }
        }

        private AsyncRelayCommand? openResultsCommand;

        public ICommand OpenResultsCommand => openResultsCommand ??= new AsyncRelayCommand(OpenResultsAsync);

        private async Task OpenResultsAsync(object? commandParameter)
        {
            await OpenResultsAsync();
        }

        private async Task OnWinnerDeclared(Player? player, int round)
        {
            MediaPreviewCoordinator.StopRegisterdMedia(); //TODO other events bounded to on buzzer

            PlayersResultViewModel?.CurrentBuzzerWinner = player;

            await OpenResultsAsync();
        }

        /// <summary>
        /// Oeffnet das Ergebnisfenster und uebernimmt die Ergebniszeilen an die Zelle.
        /// <para>
        /// Passt die Zahl der Zeilen nicht zur Mannschaft, wird nichts uebernommen - aber auch
        /// nichts geworfen. Bis 2026-09-06 stand hier ein <c>throw</c>, und weil der Aufruf aus
        /// den Buzzer-Rueckrufen kommt, riss er mitten in der Runde die Anwendung mit.
        /// <c>PlayersResultViewModel.SetCoordinateAsync</c> gleicht die Zeilen inzwischen ab,
        /// sodass der Fall nur noch bei einem Fehler dort auftreten kann.
        /// </para>
        /// </summary>
        private async Task OpenResultsAsync()
        {
            ShowResultWindow();

            var newResults = PlayersResultViewModel?.Results;

            if (newResults == null || Coordinate == null || newResults.Count == 0)
                return;

            if (Coordinate.Game.Players.Count() != newResults.Count)
            {
                UserPrompt.Inform(
                    "Die Ergebniszeilen dieser Zelle passen nicht zur Mannschaft. Das Fenster "
                    + "zeigt trotzdem, was vorhanden ist – bitte die Punkte vor dem Abschließen prüfen.",
                    "Ergebnisse der Zelle");

                return;
            }

            Coordinate.QuestionResults = newResults;
        }

        public List<Player> CoordinateCorrectedAnsweredPlayers => Coordinate?.QuestionResults.Where(r => r.CorrectAnswered).Select(r => r.Player).ToList() ?? new List<Player>();

        public bool SetNextChoosingPlayer { get; private set; } = false;

        private RelayCommand? exitSetNextChoosingPlayerCommand;
        public ICommand ExitSetNextChoosingPlayerCommand => exitSetNextChoosingPlayerCommand ??= new RelayCommand(ExitSetNextChoosingPlayer);

        private void ExitSetNextChoosingPlayer(object? commandParameter)
        {
            SetNextChoosingPlayer = true;
            Exit(null);
        }

        private RelayCommand? exitCommand;
        public ICommand ExitCommand => exitCommand ??= new RelayCommand(Exit);

        private void Exit(object? commandParameter)
        {
            Window?.Close();
        }

        #endregion Buzzer
    }
}