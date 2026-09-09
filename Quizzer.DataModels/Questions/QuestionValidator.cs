using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;

namespace Quizzer.DataModels.Questions
{
    /// <summary>
    /// Prueft, ob eine Frage so, wie sie dasteht, wirklich spielbar ist.
    /// <para>
    /// Bewusst ohne WPF und ohne Datenbank, damit jede Regel einzeln pruefbar bleibt.
    /// Die WPF-Regeln in <c>Quizzer/Validators</c> pruefen weiterhin die Eingabeform
    /// (ist das ueberhaupt eine Zahl), hier geht es um die Stimmigkeit.
    /// </para>
    /// </summary>
    public static class QuestionValidator
    {
        public const string DesignationMissing = "designation-missing";
        public const string DesignationShortMissing = "designation-short-missing";
        public const string CategoryMissing = "category-missing";
        public const string PointsNegative = "points-negative";
        public const string MinusPointsNegative = "minus-points-negative";
        public const string TooFewSteps = "too-few-steps";
        public const string ResultStepMissing = "result-step-missing";
        public const string MultipleResultStepsNotAllowed = "multiple-result-steps-not-allowed";
        public const string KeySelectCountMismatch = "key-select-count-mismatch";
        public const string KeySelectOutOfRange = "key-select-out-of-range";
        public const string ResourceWithoutType = "resource-without-type";
        public const string ResourceTypeWithoutFile = "resource-type-without-file";
        public const string MultipleStartSteps = "multiple-start-steps";
        public const string MultipleFinishSteps = "multiple-finish-steps";

        /// <summary>
        /// Eine Hinweisfrage ohne gefüllten Auflösungsschritt (F16).
        /// </summary>
        public const string FinishStepMissing = "finish-step-missing";
        public const string StepTextMissing = "step-text-missing";
        public const string TypeOwnedValuesChanged = "type-owned-values-changed";
        public const string ExpectedDateMissing = "expected-date-missing";
        public const string ExpectedValueMissing = "expected-value-missing";
        public const string UnitDoesNotMatchKind = "unit-does-not-match-kind";
        public const string RevealImageMissing = "reveal-image-missing";
        public const string FinishTextMissing = "finish-text-missing";
        public const string KeySelectWillBeAdjusted = "key-select-will-be-adjusted";

        /// <summary>Prueft die Frage und liefert alle Beanstandungen.</summary>
        public static IReadOnlyList<ValidationIssue> Validate(QuestionBase question)
        {
            ArgumentNullException.ThrowIfNull(question);

            var profile = QuestionTypeProfiles.For(question.Typ);
            var issues = new List<ValidationIssue>();

            ValidateHeader(question, issues);
            ValidateSteps(question, profile, issues);
            ValidateKeySelect(question, profile, issues);
            ValidateTypeOwnedValues(question, profile, issues);
            ValidateExpectedValue(question, issues);
            ValidateRevealImage(question, issues);
            ValidateFinishText(question, profile, issues);

            return issues;
        }

        /// <summary>Ob die Frage gespeichert werden darf.</summary>
        public static bool IsSavable(QuestionBase question)
            => !Validate(question).Any(i => i.IsError);

        private static void ValidateHeader(QuestionBase question, List<ValidationIssue> issues)
        {
            if (string.IsNullOrWhiteSpace(question.Designation))
                issues.Add(new(DesignationMissing, ValidationSeverity.Error,
                    "Die Frage braucht eine Bezeichnung.", nameof(question.Designation)));

            if (string.IsNullOrWhiteSpace(question.DesignationShort))
                issues.Add(new(DesignationShortMissing, ValidationSeverity.Warning,
                    "Ohne Kurzbezeichnung bleibt die Zelle im Spielfeld leer.",
                    nameof(question.DesignationShort)));

            if (question.CategoryId == Guid.Empty)
                issues.Add(new(CategoryMissing, ValidationSeverity.Error,
                    "Die Frage braucht eine Kategorie.", nameof(question.CategoryId)));

            if (question.Points < 0)
                issues.Add(new(PointsNegative, ValidationSeverity.Error,
                    "Die Punkte dürfen nicht negativ sein.", nameof(question.Points)));

            if (question.MinusPoints < 0)
                issues.Add(new(MinusPointsNegative, ValidationSeverity.Error,
                    "Die Minuspunkte dürfen nicht negativ sein.", nameof(question.MinusPoints)));
        }

        private static void ValidateSteps(
            QuestionBase question, QuestionTypeProfile profile, List<ValidationIssue> issues)
        {
            var steps = question.Steps ?? new List<QuestionStepResource>();
            var normalSteps = steps.Where(s => !s.IsStart && !s.IsFinish).ToList();

            if (normalSteps.Count < profile.MinNormalSteps)
                issues.Add(new(TooFewSteps, ValidationSeverity.Error,
                    $"{profile.DisplayName}: mindestens {profile.MinNormalSteps} Schritte nötig, "
                    + $"vorhanden sind {normalSteps.Count}.", nameof(question.Steps)));

            var resultSteps = steps.Where(s => s.IsResult).ToList();

            if (profile.RequiresResultStep && resultSteps.Count == 0)
                issues.Add(new(ResultStepMissing, ValidationSeverity.Error,
                    "Kein Schritt ist als Lösung markiert - die Antwort kann nie als richtig "
                    + "gewertet werden.", nameof(question.Steps)));

            if (!profile.AllowsMultipleResultSteps && resultSteps.Count > 1)
                issues.Add(new(MultipleResultStepsNotAllowed, ValidationSeverity.Error,
                    $"{profile.DisplayName} verträgt nur einen Lösungsschritt, "
                    + $"markiert sind {resultSteps.Count}.", nameof(question.Steps)));

            if (steps.Count(s => s.IsStart) > 1)
                issues.Add(new(MultipleStartSteps, ValidationSeverity.Error,
                    "Es darf höchstens einen Startschritt geben.", nameof(question.Steps)));

            if (steps.Count(s => s.IsFinish) > 1)
                issues.Add(new(MultipleFinishSteps, ValidationSeverity.Error,
                    "Es darf höchstens einen Abschlussschritt geben.", nameof(question.Steps)));

            ValidateAufloesung(question, steps, issues);

            foreach (var step in steps)
            {
                var hasFile = !string.IsNullOrWhiteSpace(step.ResourceFileName);
                var hasType = step.ResourceTyp != ResourceType.None;

                if (hasFile && !hasType)
                    issues.Add(new(ResourceWithoutType, ValidationSeverity.Error,
                        $"Schritt {Describe(step)}: eine Datei ist hinterlegt, aber kein "
                        + "Medientyp - im Spiel bleibt sie unsichtbar.", nameof(step.ResourceTyp)));

                if (hasType && !hasFile)
                    issues.Add(new(ResourceTypeWithoutFile, ValidationSeverity.Error,
                        $"Schritt {Describe(step)}: ein Medientyp ist gesetzt, aber keine Datei.",
                        nameof(step.ResourceFileName)));

                if (!step.IsStart && !step.IsFinish
                    && string.IsNullOrWhiteSpace(step.StepText)
                    && string.IsNullOrWhiteSpace(step.Designation)
                    && !hasFile)
                    issues.Add(new(StepTextMissing, ValidationSeverity.Warning,
                        "Ein Schritt ohne Text und ohne Medium bleibt im Spiel leer.",
                        nameof(step.StepText)));
            }
        }

        /// <summary>
        /// <b>F16.</b> Eine Frage, deren Punkte je Hinweis sinken, braucht einen gefüllten
        /// Auflösungsschritt.
        /// <para>
        /// <b>Warum das eine Beanstandung ist und keine Warnung</b> (Nutzerentscheidung
        /// 2026-09-09): der Auflösungsschritt ist die einzige Stelle, an der die Punkte auf 0
        /// gehen - „auch wenn alles erkennbar ist kann noch geraten werden". Fehlt er, erfindet
        /// <see cref="Models.QuestionBase.CalculateOrderdSteps"/> einen leeren; der nennt die
        /// Lösung nicht, und der letzte Bildschirm der Frage bliebe stumm.
        /// </para>
        /// <para>
        /// <b>Vor der Entscheidung gemessen:</b> von den fünf bestehenden Hinweisfragen trifft
        /// es genau eine, <c>Aufdeckfrage</c> - die vier Eigenschaftsfragen haben ihren
        /// gefüllten Abschlussschritt bereits.
        /// </para>
        /// </summary>
        private static void ValidateAufloesung(
            QuestionBase question, List<QuestionStepResource> steps, List<ValidationIssue> issues)
        {
            if (!question.UseProportionalScoreReductionOnStep)
                return;

            var gefuellt = steps.Any(s => s.IsFinish
                && (!string.IsNullOrWhiteSpace(s.StepText)
                    || !string.IsNullOrWhiteSpace(s.Designation)
                    || !string.IsNullOrWhiteSpace(s.ResourceFileName)));

            if (!gefuellt)
                issues.Add(new(FinishStepMissing, ValidationSeverity.Error,
                    "Es fehlt ein gefüllter Auflösungsschritt - erst dort stehen 0 Punkte, "
                    + "und erst dort steht die Lösung.", nameof(question.Steps)));
        }

        private static void ValidateKeySelect(
            QuestionBase question, QuestionTypeProfile profile, List<ValidationIssue> issues)
        {
            if (!profile.ShowMaxAllowedKeySelect)
                return;

            var steps = question.Steps ?? new List<QuestionStepResource>();
            var normalCount = steps.Count(s => !s.IsStart && !s.IsFinish);
            var resultCount = steps.Count(s => s.IsResult);

            if (question.BuzzerMaxAllowedKeySelect < 1
                || (normalCount > 0 && question.BuzzerMaxAllowedKeySelect > normalCount))
                issues.Add(new(KeySelectOutOfRange, ValidationSeverity.Error,
                    $"Es dürfen zwischen 1 und {Math.Max(normalCount, 1)} Antworten gewählt "
                    + $"werden, eingestellt sind {question.BuzzerMaxAllowedKeySelect}.",
                    nameof(question.BuzzerMaxAllowedKeySelect)));

            // PlayerResultContext wertet nur dann als richtig, wenn die Anzahl der gewaehlten
            // Tasten der eingestellten Hoechstzahl entspricht. Stimmen die beiden nicht ueberein,
            // kann die Frage nie richtig beantwortet werden.
            if (resultCount > 0 && question.BuzzerMaxAllowedKeySelect != resultCount)
                issues.Add(new(KeySelectCountMismatch, ValidationSeverity.Error,
                    $"{resultCount} Lösungen markiert, aber {question.BuzzerMaxAllowedKeySelect} "
                    + "wählbare Antworten eingestellt. So kann die Frage nie richtig beantwortet "
                    + "werden.", nameof(question.BuzzerMaxAllowedKeySelect)));
        }

        private static void ValidateExpectedValue(QuestionBase question, List<ValidationIssue> issues)
        {
            if (question is not AppreciateQestion appreciate)
                return;

            if (!AppreciateUnits.Matches(appreciate.ValueKind, appreciate.Unit))
            {
                issues.Add(new(UnitDoesNotMatchKind, ValidationSeverity.Error,
                    "Die gewählte Einheit passt nicht zur Art des Schätzwerts.",
                    nameof(appreciate.Unit)));
                return;
            }

            if (appreciate.ValueKind == AppreciateValueKind.Date)
            {
                if (appreciate.ExpectedDate == null)
                    issues.Add(new(ExpectedDateMissing, ValidationSeverity.Error,
                        "Ohne Solldatum kann der nächste Tipp nicht ermittelt werden.",
                        nameof(appreciate.ExpectedDate)));

                return;
            }

            // Null ist ein zulaessiger Sollwert, aber fast immer bedeutet er, dass niemand
            // ihn eingetragen hat - deshalb eine Warnung, keine Sperre.
            if (appreciate.ExpectedValue == 0)
                issues.Add(new(ExpectedValueMissing, ValidationSeverity.Warning,
                    "Der Sollwert steht auf 0. Ist das wirklich gemeint?",
                    nameof(appreciate.ExpectedValue)));
        }

        /// <summary>
        /// Eine Aufdeckfrage ohne Bild ist nicht spielbar - es gibt nichts aufzudecken.
        /// <para>
        /// <b>Bis zum 2026-09-06 hat die Pruefung dazu geschwiegen</b>; die Frage liess sich
        /// speichern und stand am Abend als leerer Bildschirm da.
        /// </para>
        /// </summary>
        private static void ValidateRevealImage(QuestionBase question, List<ValidationIssue> issues)
        {
            if (question is not RevealQuestion aufdeck)
                return;

            if (string.IsNullOrWhiteSpace(aufdeck.ImageFileName))
                issues.Add(new(RevealImageMissing, ValidationSeverity.Error,
                    "Für die Aufdeckfrage ist kein Bild hinterlegt. Es gibt nichts aufzudecken.",
                    nameof(aufdeck.ImageFileName)));
        }

        /// <summary>
        /// Am Ende der Frage sollte etwas stehen.
        /// <para>
        /// <b>Ausdruecklich eine Warnung, kein Fehler.</b> Als Fehler wuerde sie das Speichern
        /// sperren und den gesamten Bestand an Standardfragen beim naechsten Oeffnen
        /// unspeicherbar machen - der Typ hatte bis zum Umbau gar kein Feld dafuer.
        /// </para>
        /// </summary>
        private static void ValidateFinishText(
            QuestionBase question, QuestionTypeProfile profile, List<ValidationIssue> issues)
        {
            // Wo der Typ ohnehin einen Loesungsschritt verlangt, meldet ResultStepMissing schon;
            // eine zweite Zeile dazu waere nur Laerm.
            if (profile.RequiresResultStep)
                return;

            var steps = question.Steps ?? new List<QuestionStepResource>();
            var abschluss = steps.FirstOrDefault(s => s.IsFinish);

            if (abschluss == null
                || (string.IsNullOrWhiteSpace(abschluss.StepText)
                    && string.IsNullOrWhiteSpace(abschluss.Designation)
                    && abschluss.ResourceTyp == ResourceType.None))
                issues.Add(new(FinishTextMissing, ValidationSeverity.Warning,
                    "Am Ende der Frage steht nichts. Die Mitspieler sehen dann einen leeren "
                    + "Bildschirm statt der Auflösung."));
        }

        private static void ValidateTypeOwnedValues(
            QuestionBase question, QuestionTypeProfile profile, List<ValidationIssue> issues)
        {
            if (profile.MatchesOwnedValues(question))
                return;

            issues.Add(new(TypeOwnedValuesChanged, ValidationSeverity.Error,
                $"Die Frage weicht von den Vorgaben für {profile.DisplayName} ab. Im Spiel "
                + "steht auf dem Spielerbildschirm statt der Frage ein Hinweis, dass sie für die "
                + "gewählte Anzeigeart nicht eingerichtet ist.",
                nameof(question.Typ)));
        }

        private static string Describe(QuestionStepResource step)
            => !string.IsNullOrWhiteSpace(step.Designation) ? step.Designation
             : !string.IsNullOrWhiteSpace(step.StepText) ? step.StepText
             : $"Nr. {step.SequenceNumber}";
    }
}
