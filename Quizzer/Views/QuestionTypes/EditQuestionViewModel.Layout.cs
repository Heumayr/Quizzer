using Quizzer.DataModels.Enumerations;
using System.Windows;

namespace Quizzer.Views.QuestionTypes
{
    /// <summary>
    /// Sichtbarkeiten des Frage-Editors, abgeleitet aus dem Profil des Fragetyps.
    /// <para>
    /// Damit zeigt die Maske nur noch, was der jeweilige Typ wirklich braucht. Bis hierher
    /// bekam jeder Typ dieselben zwoelf Zeilen zu sehen - auch die, die fuer ihn wirkungslos
    /// waren, und ohne die, die er zwingend gebraucht haette.
    /// </para>
    /// <para>
    /// Die Sichtbarkeit wird bewusst hier berechnet und nicht ueber Konverter im XAML gebildet -
    /// so halten es die Spielansichten (<c>QuestionStepViewContext</c>) bereits.
    /// </para>
    /// </summary>
    public partial class EditQuestionViewModel
    {
        private static Visibility When(bool visible) => visible ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>Hoechstzahl waehlbarer Antworten - nur bei Multiple Choice.</summary>
        public Visibility MaxAllowedKeySelectVisibility => When(Profile?.ShowMaxAllowedKeySelect ?? false);

        /// <summary>Antworttexte auf den Tasten anzeigen - nur bei Multiple Choice.</summary>
        public Visibility ShowTextOnKeySelectVisibility => When(Profile?.ShowShowTextOnKeySelect ?? false);

        /// <summary>Spalte "Ist Loesung" in der Schrittliste.</summary>
        public Visibility IsResultColumnVisibility => When(Profile?.ShowIsResultPerStep ?? false);

        /// <summary>Sollwert und Einheit - nur bei der Schaetzfrage.</summary>
        public Visibility ExpectedValueVisibility => When(Profile?.ShowExpectedValue ?? false);

        /// <summary>Die Liste der Beanstandungen.</summary>
        public Visibility IssuesVisibility => When(HasIssues);

        /// <summary>
        /// Hinweis, dass die proportionale Punktereduktion greift - nur bei der
        /// Eigenschaftsfrage, wo sie tatsaechlich ausgewertet wird.
        /// </summary>
        public Visibility ProportionalScoreHintVisibility
            => When(Profile?.UseProportionalScoreReductionOnStep ?? false);

        /// <summary>
        /// Hinweis, dass die Schritte bei jedem Spielen gemischt werden - nur dort, wo das
        /// wirklich geschieht.
        /// </summary>
        public Visibility RandomSequenceHintVisibility
            => When(Profile?.UseRandomSequenceOnNoneFinishSteps ?? false);

        /// <summary>Das Buzzer-Layout im Klartext, wie der Typ es vorgibt.</summary>
        public string BuzzerLayoutText => Profile?.BuzzerControlsLayout switch
        {
            BuzzerControlsLayout.Buzzer => "Buzzer - wer zuerst drueckt, darf antworten",
            BuzzerControlsLayout.KeySelect => "Tastenwahl - die Spieler waehlen eine Antwort",
            BuzzerControlsLayout.Input => "Texteingabe - die Spieler tippen einen Wert",
            _ => string.Empty,
        };

        /// <summary>Der Anzeigemodus im Klartext, wie der Typ ihn vorgibt.</summary>
        public string StepLayoutText => Profile?.StepDisplayLayoutMode switch
        {
            StepDisplayLayoutMode.Vertical => "untereinander",
            StepDisplayLayoutMode.Horizontal => "nebeneinander",
            StepDisplayLayoutMode.Grid => "als Raster",
            _ => string.Empty,
        };

        /// <summary>Die Beschriftung der Antworttasten im Klartext.</summary>
        public string ViewKeyText => Profile?.QuestionViewKeyType switch
        {
            QuestionViewKeyType.Alphabetical => "A, B, C ...",
            QuestionViewKeyType.Numerical => "1, 2, 3 ...",
            _ => string.Empty,
        };

        /// <summary>Meldet alle abgeleiteten Anzeigewerte als geaendert.</summary>
        private void RaiseLayoutChanged()
        {
            OnPropertyChanged(nameof(MaxAllowedKeySelectVisibility));
            OnPropertyChanged(nameof(ShowTextOnKeySelectVisibility));
            OnPropertyChanged(nameof(IsResultColumnVisibility));
            OnPropertyChanged(nameof(ExpectedValueVisibility));
            OnPropertyChanged(nameof(IssuesVisibility));
            OnPropertyChanged(nameof(ProportionalScoreHintVisibility));
            OnPropertyChanged(nameof(RandomSequenceHintVisibility));
            OnPropertyChanged(nameof(BuzzerLayoutText));
            OnPropertyChanged(nameof(StepLayoutText));
            OnPropertyChanged(nameof(ViewKeyText));
        }
    }
}
