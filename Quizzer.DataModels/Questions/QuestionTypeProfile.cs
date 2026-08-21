using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;

namespace Quizzer.DataModels.Questions
{
    /// <summary>
    /// Beschreibt einen Fragetyp: wie er heisst, was er selbst festlegt, welche Felder der
    /// Spielleiter dafuer ausfuellen muss und welche gar nicht erst gezeigt werden.
    /// <para>
    /// Bis hierher stand dieses Wissen nur verstreut in den Konstruktoren der Unterklassen,
    /// und die Eingabemaske kannte es gar nicht - sie bot jedem Typ dieselben Felder an.
    /// Maske und Pruefung lesen jetzt beide von hier.
    /// </para>
    /// </summary>
    public sealed class QuestionTypeProfile
    {
        /// <summary>Der Fragetyp, den dieses Profil beschreibt.</summary>
        public required QuestionType Typ { get; init; }

        /// <summary>Name des Typs, wie er dem Spielleiter angezeigt wird.</summary>
        public required string DisplayName { get; init; }

        /// <summary>Ein Satz dazu, wofuer dieser Typ gedacht ist.</summary>
        public required string HelpText { get; init; }

        /// <summary>Name der Untertabelle. Wird beim Umwandeln in einen anderen Typ gebraucht.</summary>
        public required string TableName { get; init; }

        // --- Typeigene Werte: setzt der Typ, im Editor nur lesbar ---

        /// <summary>Welche Bedienung die Spieler im Browser bekommen.</summary>
        public required BuzzerControlsLayout BuzzerControlsLayout { get; init; }

        /// <summary>Wie die Schritte auf dem Spielerbildschirm angeordnet werden.</summary>
        public required StepDisplayLayoutMode StepDisplayLayoutMode { get; init; }

        /// <summary>Ob die Antworttasten A, B, C oder 1, 2, 3 heissen.</summary>
        public required QuestionViewKeyType QuestionViewKeyType { get; init; }

        /// <summary>Ob die normalen Schritte bei jedem Spielen neu gemischt werden.</summary>
        public required bool UseRandomSequenceOnNoneFinishSteps { get; init; }

        /// <summary>Ob die Punkte mit jedem aufgedeckten Schritt sinken.</summary>
        public required bool UseProportionalScoreReductionOnStep { get; init; }

        // --- Typbedingte Felder: sichtbar nur bei diesem Typ, aber editierbar ---

        /// <summary>
        /// Ob der Spielleiter einstellen darf, wie viele Antworttasten gewaehlt werden duerfen.
        /// Bewusst editierbar und nicht vom Profil vorgegeben - sonst waere eine
        /// Multiple-Choice-Frage mit mehreren richtigen Antworten wieder unmoeglich.
        /// </summary>
        public bool ShowMaxAllowedKeySelect { get; init; }

        /// <summary>Ob die Antworttexte auf den Tasten im Browser erscheinen sollen.</summary>
        public bool ShowShowTextOnKeySelect { get; init; }

        /// <summary>Ob einzelne Schritte als Loesung markiert werden koennen.</summary>
        public bool ShowIsResultPerStep { get; init; }

        /// <summary>Ob der Typ einen Schaetzwert samt Einheit braucht.</summary>
        public bool ShowExpectedValue { get; init; }

        // --- Regeln fuer das Schrittmodell, gelesen von der Pruefung ---

        /// <summary>Wie viele normale Schritte dieser Typ mindestens braucht.</summary>
        public required int MinNormalSteps { get; init; }

        /// <summary>Ob mindestens ein Schritt als Loesung markiert sein muss.</summary>
        public required bool RequiresResultStep { get; init; }

        /// <summary>Ob mehrere Loesungsschritte erlaubt sind.</summary>
        public required bool AllowsMultipleResultSteps { get; init; }

        /// <summary>
        /// Setzt die typeigenen Werte auf die Frage. Wird von den Konstruktoren der
        /// Unterklassen aufgerufen und beim Umwandeln in einen anderen Typ.
        /// </summary>
        public void ApplyTo(QuestionBase question)
        {
            ArgumentNullException.ThrowIfNull(question);

            question.BuzzerControlsLayout = BuzzerControlsLayout;
            question.StepDisplayLayoutMode = StepDisplayLayoutMode;
            question.QuestionViewKeyType = QuestionViewKeyType;
            question.UseRandomSequenceOnNoneFinishSteps = UseRandomSequenceOnNoneFinishSteps;
            question.UseProportionalScoreReductionOnStep = UseProportionalScoreReductionOnStep;
        }

        /// <summary>
        /// Prueft, ob die typeigenen Werte der Frage noch mit dem Profil uebereinstimmen.
        /// Weichen sie ab, wurde die Frage an der Maske vorbei veraendert - im Spiel fuehrt
        /// das etwa zu der Anzeige "Not Supported" auf dem Spielerbildschirm.
        /// </summary>
        public bool MatchesOwnedValues(QuestionBase question)
        {
            ArgumentNullException.ThrowIfNull(question);

            return question.BuzzerControlsLayout == BuzzerControlsLayout
                && question.StepDisplayLayoutMode == StepDisplayLayoutMode
                && question.QuestionViewKeyType == QuestionViewKeyType
                && question.UseRandomSequenceOnNoneFinishSteps == UseRandomSequenceOnNoneFinishSteps
                && question.UseProportionalScoreReductionOnStep == UseProportionalScoreReductionOnStep;
        }
    }
}
