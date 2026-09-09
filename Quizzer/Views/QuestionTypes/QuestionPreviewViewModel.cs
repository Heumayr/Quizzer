using Quizzer.Base;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Questions;
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

        /// <summary>
        /// Was auf diesem Bildschirm zu holen ist - für „richtig" und für „falsch".
        /// <para>
        /// <b>Nutzermeldung vom 2026-09-09 (M1):</b> „ein punkt den du aufnehmen kannst ist dass
        /// in der vorschau auch der punkte lauf ... richtig bzw. falsch angezeigt wird". Die
        /// Vorschau zeigte die Bildschirme, ohne zu sagen, was auf ihnen zu holen ist - gerade
        /// dort sieht man die Frage aber so, wie sie am Beamer steht.
        /// </para>
        /// <para>
        /// <b>Gerechnet aus <see cref="Punkteabzug"/></b>, derselben Stelle, aus der das Spiel
        /// liest - eine zweite Abschrift liefe der ersten davon.
        /// </para>
        /// <para>
        /// <b>Die Minuspunkte sinken nicht mit</b>, und das ist kein Versehen der Anzeige:
        /// <c>PlayerResultContext</c> nimmt bei „falsch" immer den vollen Wert. Wer früh rät,
        /// verliert also gleich viel wie einer, der bis zum Schluss wartet.
        /// </para>
        /// </summary>
        public string Punktestand
        {
            get
            {
                var frage = Question;

                if (frage == null || frage.OrderedSteps.Length == 0)
                    return string.Empty;

                var schritt = frage.OrderedSteps.ElementAtOrDefault(index);

                if (schritt == null)
                    return string.Empty;

                var minus = frage.MinusPoints > 0 ? $"falsch −{frage.MinusPoints}" : "falsch 0";

                return $"richtig +{PlusAufDiesemBildschirm(frage, schritt)}, {minus}";
            }
        }

        /// <summary>
        /// Der Pluswert dieses Bildschirms. Ohne Punkteabzug je Hinweis ist es schlicht die
        /// Punktzahl der Frage; mit Abzug die Kurve, und im Auflösungsschritt 0.
        /// </summary>
        private int PlusAufDiesemBildschirm(QuestionBase frage, QuestionStepResource schritt)
        {
            if (!frage.UseProportionalScoreReductionOnStep)
                return frage.Points;

            // Erst die Aufloesung nimmt alles - solange nur Hinweise stehen, kann noch geraten
            // werden (Nutzerentscheidung F16).
            if (schritt.IsFinish)
                return 0;

            return Punkteabzug.Verbleibend(
                frage.Points,
                frage.HinweiseGesamt,
                frage.GezeigteHinweiseBis(index),
                frage.ScoreReductionMode,
                frage.ScoreReductionFactor);
        }

        /// <summary>
        /// Der Hinweis, dass die Punkte hier <b>ohne</b> Schwierigkeit und Phase stehen - die
        /// greifen erst, wenn die Frage in einem Raster liegt.
        /// </summary>
        public string PunktestandHinweis
            => Question == null || Question.OrderedSteps.Length == 0
                ? string.Empty
                : "Grundwerte der Frage - Schwierigkeit und Phase kommen erst im Spielfeld dazu.";

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
            OnPropertyChanged(nameof(Punktestand));
            OnPropertyChanged(nameof(PunktestandHinweis));
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
