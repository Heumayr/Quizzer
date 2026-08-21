using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.QuestionTypes;

namespace Quizzer.DataModels.Questions
{
    /// <summary>
    /// Die vier Fragetyp-Profile. Einzige Stelle, an der steht, was ein Typ mitbringt.
    /// </summary>
    public static class QuestionTypeProfiles
    {
        private static readonly QuestionTypeProfile Default = new()
        {
            Typ = QuestionType.Default,
            DisplayName = "Standardfrage",
            HelpText = "Der Spielleiter liest die Frage vor, der erste Buzzer darf antworten. "
                     + "Hinweise werden in fester Reihenfolge aufgedeckt.",
            TableName = nameof(DefaultQuestion),
            BuzzerControlsLayout = BuzzerControlsLayout.Buzzer,
            StepDisplayLayoutMode = StepDisplayLayoutMode.Vertical,
            QuestionViewKeyType = QuestionViewKeyType.Alphabetical,
            UseRandomSequenceOnNoneFinishSteps = false,
            UseProportionalScoreReductionOnStep = false,
            ShowIsResultPerStep = true,
            MinNormalSteps = 0,
            RequiresResultStep = false,
            AllowsMultipleResultSteps = true,
        };

        private static readonly QuestionTypeProfile MultipleChoice = new()
        {
            Typ = QuestionType.MultipleChoice,
            DisplayName = "Multiple Choice",
            HelpText = "Die Spieler waehlen am Telefon eine Antworttaste. Die Optionen werden "
                     + "gemischt und im Raster angezeigt. Mindestens eine Option muss als "
                     + "Loesung markiert sein.",
            TableName = nameof(MultipleChoiceQuestion),
            BuzzerControlsLayout = BuzzerControlsLayout.KeySelect,
            StepDisplayLayoutMode = StepDisplayLayoutMode.Grid,
            QuestionViewKeyType = QuestionViewKeyType.Alphabetical,
            UseRandomSequenceOnNoneFinishSteps = true,
            UseProportionalScoreReductionOnStep = false,
            ShowMaxAllowedKeySelect = true,
            ShowShowTextOnKeySelect = true,
            ShowIsResultPerStep = true,
            MinNormalSteps = 2,
            RequiresResultStep = true,
            AllowsMultipleResultSteps = true,
        };

        private static readonly QuestionTypeProfile Properties = new()
        {
            Typ = QuestionType.Properties,
            DisplayName = "Eigenschaftsfrage",
            HelpText = "Hinweise werden nacheinander aufgedeckt; mit jedem Hinweis sinken die "
                     + "erreichbaren Punkte. Die Spieler buzzern und antworten muendlich.",
            TableName = nameof(PropertiesQuestion),
            BuzzerControlsLayout = BuzzerControlsLayout.Buzzer,
            StepDisplayLayoutMode = StepDisplayLayoutMode.Vertical,
            QuestionViewKeyType = QuestionViewKeyType.Numerical,
            UseRandomSequenceOnNoneFinishSteps = false,
            UseProportionalScoreReductionOnStep = true,
            ShowIsResultPerStep = true,
            MinNormalSteps = 1,
            RequiresResultStep = false,
            AllowsMultipleResultSteps = true,
        };

        private static readonly QuestionTypeProfile Appreciate = new()
        {
            Typ = QuestionType.Appreciate,
            DisplayName = "Schaetzfrage",
            HelpText = "Die Spieler tippen einen Wert ein. Wer am naechsten dran liegt, gewinnt - "
                     + "das ermittelt das Spiel selbst aus Sollwert und Einheit.",
            TableName = nameof(AppreciateQestion),
            BuzzerControlsLayout = BuzzerControlsLayout.Input,
            StepDisplayLayoutMode = StepDisplayLayoutMode.Vertical,
            QuestionViewKeyType = QuestionViewKeyType.Numerical,
            UseRandomSequenceOnNoneFinishSteps = false,
            UseProportionalScoreReductionOnStep = false,
            ShowExpectedValue = true,
            MinNormalSteps = 0,
            RequiresResultStep = false,
            AllowsMultipleResultSteps = false,
        };

        /// <summary>Alle Profile in der Reihenfolge, in der sie angeboten werden.</summary>
        public static IReadOnlyList<QuestionTypeProfile> All { get; } =
            [Default, MultipleChoice, Properties, Appreciate];

        /// <summary>Liefert das Profil zu einem Fragetyp.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Bei einem unbekannten Typ.</exception>
        public static QuestionTypeProfile For(QuestionType typ)
            => All.FirstOrDefault(p => p.Typ == typ)
               ?? throw new ArgumentOutOfRangeException(nameof(typ), typ, "Unbekannter Fragetyp.");
    }
}
