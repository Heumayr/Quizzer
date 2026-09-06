using Quizzer.Base;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;

using Quizzer.Views.GameViews;
using Quizzer.Views.GameViews.QuestionViews;
using System.Windows.Input;

namespace Quizzer.Views.QuestionTypes
{
    /// <summary>
    /// Die Frage durchspielen, ohne ein Spiel dafür zu bauen.
    /// <para>
    /// <b>Nutzerwunsch vom 2026-09-06:</b> „ein vorschaumodus im frageneditor ... dass man direkt
    /// dort die fragen ausprobieren kann".
    /// </para>
    /// <para>
    /// <b>Sie benutzt dieselbe Anzeige wie der Beamer</b> - <c>QuestionScreenView</c> mit einem
    /// <c>GamePlayerViewModel</c> dahinter. Eine nachgebaute Vorschau zeigt früher oder später
    /// etwas anderes als der Abend; genau das ist am selben Tag zweimal passiert (die doppelte
    /// Frage und „Frage und Antwort zugleich").
    /// </para>
    /// <para>
    /// <b>Sie schreibt nichts.</b> Die Frage wird geklont, bevor die Schritte geordnet werden -
    /// sonst trüge der Editor danach die vom Modell ergänzten Bildschirme in seiner Liste.
    /// </para>
    /// </summary>
    public class QuestionPreviewViewModel : ViewModelBase
    {
        private int index;

        /// <summary>Die Anzeige - dieselbe, die auf dem Beamer läuft.</summary>
        public GamePlayerViewModel Beamer { get; } = new();

        /// <summary>Die Frage, die vorgeführt wird.</summary>
        public QuestionBase? Question { get; private set; }

        private CurrentQuestionViewModel? traeger;

        /// <summary>Wo man gerade steht - „Bildschirm 2 von 5".</summary>
        public string Standanzeige =>
            Question == null || Question.OrderedSteps.Length == 0
                ? "Diese Frage hat noch keine Schritte."
                : $"Bildschirm {index + 1} von {Question.OrderedSteps.Length}";

        /// <summary>Was auf diesem Bildschirm zu sehen ist - als Wort, für den Anlegenden.</summary>
        public string Bildschirmart
        {
            get
            {
                var schritt = Question?.OrderedSteps.ElementAtOrDefault(index);

                if (schritt == null)
                    return string.Empty;

                if (schritt.IsStart) return "Startbildschirm: nur die Fragenart";
                if (schritt.IsQuestionOnly) return "Fragebildschirm: nur die Frage";
                if (schritt.IsFinish) return "Abschluss";

                return "Inhaltsschritt";
            }
        }

        public bool CanNext => Question != null && index < Question.OrderedSteps.Length - 1;

        public bool CanBack => index > 0;

        /// <summary>
        /// Stellt die Vorschau auf eine Frage ein. Die Frage wird <b>geklont</b> - was hier
        /// geschieht, darf den Editor nicht verändern.
        /// </summary>
        public void SetQuestion(QuestionBase original)
        {
            var klon = original.CloneWithoutReferences();

            klon.Steps.Clear();

            foreach (var schritt in original.Steps)
                klon.Steps.Add(schritt.CloneWithoutReferences());

            klon.CalculateOrderdSteps();

            Question = klon;

            traeger = new CurrentQuestionViewModel
            {
                Coordinate = new GameGridCoordinate
                {
                    QuestionBase = klon,
                    QuestionBaseId = klon.Id,
                },
            };

            index = 0;

            Zeige();
        }

        protected override Task OnloadAsync() => Task.CompletedTask;

        public override Task VMSaveAsync() => Task.CompletedTask;

        private void Zeige()
        {
            var schritt = Question?.OrderedSteps.ElementAtOrDefault(index);

            if (schritt == null || traeger == null)
            {
                Beamer.QuestionStepViewContext = null;
                Melde();
                return;
            }

            Beamer.QuestionStepViewContext = new QuestionStepViewContext
            {
                Owner = traeger,
                Step = schritt,
                IsMasterView = false,
                DisplayLayoutMode = Question!.StepDisplayLayoutMode,
            };

            // Der Setter von QuestionStepResource holt sich Text und Fragenart aus dem Kontext -
            // hier ist er gesetzt, also stimmt beides von selbst.
            Melde();
        }

        private void Melde()
        {
            OnPropertyChanged(nameof(Standanzeige));
            OnPropertyChanged(nameof(Bildschirmart));
            OnPropertyChanged(nameof(CanNext));
            OnPropertyChanged(nameof(CanBack));

            nextCommand?.RaiseCanExecuteChanged();
            backCommand?.RaiseCanExecuteChanged();
        }

        private RelayCommand? nextCommand;

        public ICommand NextCommand => nextCommand ??= new RelayCommand(_ =>
        {
            if (!CanNext)
                return;

            index++;
            Zeige();
        }, _ => CanNext);

        private RelayCommand? backCommand;

        public ICommand BackCommand => backCommand ??= new RelayCommand(_ =>
        {
            if (!CanBack)
                return;

            index--;
            Zeige();
        }, _ => CanBack);

        private RelayCommand? restartCommand;

        public ICommand RestartCommand => restartCommand ??= new RelayCommand(_ =>
        {
            index = 0;
            Zeige();
        });

        private RelayCommand? closeCommand;

        public ICommand CloseCommand => closeCommand ??= new RelayCommand(_ => Window?.Close());
    }
}
