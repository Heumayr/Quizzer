using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.QuestionTypes;

namespace Quizzer.DataModels.Questions
{
    /// <summary>
    /// Die Fragetyp-Profile. Einzige Stelle, an der steht, was ein Typ mitbringt.
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
            HelpText = "Die Spieler wählen am Telefon eine Antworttaste. Die Optionen werden "
                     + "gemischt und im Raster angezeigt. Mindestens eine Option muss als "
                     + "Lösung markiert sein.",
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
                     + "erreichbaren Punkte. Die Spieler buzzern und antworten mündlich.",
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
            DisplayName = "Schätzfrage",
            HelpText = "Die Spieler tippen einen Wert ein. Wer am nächsten dran liegt, gewinnt - "
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

        private static readonly QuestionTypeProfile Reveal = new()
        {
            Typ = QuestionType.Reveal,
            DisplayName = "Aufdeckfrage",
            HelpText = "Ein Bild wird schrittweise sichtbar - entweder fallen nacheinander "
                     + "verdeckende Flächen weg, oder das Bild wird von Schritt zu Schritt "
                     + "schärfer. Wer zuerst buzzert, darf raten.",
            TableName = nameof(RevealQuestion),
            BuzzerControlsLayout = BuzzerControlsLayout.Buzzer,
            StepDisplayLayoutMode = StepDisplayLayoutMode.Vertical,
            QuestionViewKeyType = QuestionViewKeyType.Numerical,
            UseRandomSequenceOnNoneFinishSteps = false,

            // Wer frueh raet, sieht weniger - der Punkteabzug je Schritt ist hier der Sinn der
            // Sache und keine Einstellung.
            UseProportionalScoreReductionOnStep = true,

            ShowRevealImage = true,
            MinNormalSteps = 1,
            RequiresResultStep = false,
            AllowsMultipleResultSteps = false,
        };

        private static readonly QuestionTypeProfile Ordering = new()
        {
            Typ = QuestionType.Ordering,
            DisplayName = "Reihenfolge",
            HelpText = "Die Spieler tippen die Elemente am Telefon in der richtigen Reihenfolge an. "
                     + "Die Elemente werden gemischt angezeigt; jedes Element am richtigen Platz "
                     + "bringt seinen Anteil der Punkte.",
            TableName = nameof(OrderingQuestion),

            // Dieselbe Tastenwahl wie Multiple Choice - nur zaehlt hier die Reihenfolge der
            // Klicks, und gewaehlt werden muessen alle (BuzzerKeySelectorInfo.Ordered).
            BuzzerControlsLayout = BuzzerControlsLayout.KeySelect,
            StepDisplayLayoutMode = StepDisplayLayoutMode.Grid,
            QuestionViewKeyType = QuestionViewKeyType.Alphabetical,
            UseRandomSequenceOnNoneFinishSteps = true,
            UseProportionalScoreReductionOnStep = false,
            ShowShowTextOnKeySelect = true,
            ShowIsResultPerStep = false,
            MinNormalSteps = 3,
            RequiresResultStep = false,

            // Haekchen bedeuten hier nichts. Erlaubt bleiben sie trotzdem, damit eine
            // umgewandelte Multiple-Choice-Frage sie beim Zurueckwandeln noch hat.
            AllowsMultipleResultSteps = true,
        };

        private static readonly QuestionTypeProfile Matching = new()
        {
            Typ = QuestionType.Matching,
            DisplayName = "Zuordnung",
            HelpText = "Links stehen feste Begriffe, rechts gemischt ihre Gegenstücke. Die Spieler "
                     + "tippen am Telefon zu jedem Begriff der Reihe nach das passende Gegenstück "
                     + "an; jedes richtige Paar bringt seinen Anteil der Punkte.",
            TableName = nameof(MatchingQuestion),

            // Dieselbe Mechanik wie die Reihenfolge: Platz i der Abgabe ist das Gegenstueck zu
            // Begriff i. Wertung und Telefon teilen sich deshalb den Weg (ReihenfolgeEvaluator).
            BuzzerControlsLayout = BuzzerControlsLayout.KeySelect,
            StepDisplayLayoutMode = StepDisplayLayoutMode.Grid,
            QuestionViewKeyType = QuestionViewKeyType.Alphabetical,
            UseRandomSequenceOnNoneFinishSteps = true,
            UseProportionalScoreReductionOnStep = false,
            ShowShowTextOnKeySelect = true,
            ShowIsResultPerStep = false,
            MinNormalSteps = 3,
            RequiresResultStep = false,
            AllowsMultipleResultSteps = true,
        };

        /// <summary>Alle Profile in der Reihenfolge, in der sie angeboten werden.</summary>
        public static IReadOnlyList<QuestionTypeProfile> All { get; } =
            [Default, MultipleChoice, Properties, Appreciate, Reveal, Ordering, Matching];

        /// <summary>Liefert das Profil zu einem Fragetyp.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Bei einem unbekannten Typ.</exception>
        public static QuestionTypeProfile For(QuestionType typ)
            => All.FirstOrDefault(p => p.Typ == typ)
               ?? throw new ArgumentOutOfRangeException(nameof(typ), typ, "Unbekannter Fragetyp.");
    }
}
