using Quizzer.DataModels.Attributes;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace Quizzer.DataModels.Models
{
    /// <summary>
    /// Abstrakte Basisklasse für alle Fragetypen. Implementiert das Table-per-Type (TPT)-
    /// Vererbungsmuster: Die gemeinsamen Felder werden in <c>question.QuestionBase</c>
    /// gespeichert, jeder konkrete Typ (z.B. <c>DefaultQuestion</c>) erhält eine eigene Tabelle.
    /// <para>
    /// Eine Frage besteht aus einer geordneten Sequenz von <see cref="QuestionStepResource"/>-Schritten,
    /// die über <see cref="CalculateOrderdSteps"/> in die richtige Reihenfolge gebracht werden:
    /// Start-Schritte → Normal-Schritte (optional randomisiert) → Finish-Schritte.
    /// </para>
    /// </summary>
    [Table(nameof(QuestionBase), Schema = "question")]
    public class QuestionBase : ModelBase<QuestionBase>
    {
        /// <summary>Kurz-Bezeichnung der Frage, die im Spielfeld-Grid angezeigt wird (z.B. "ML-1").</summary>
        public string DesignationShort { get; set; } = string.Empty;

        /// <summary>Vollständiger Fragetext (kann leer sein, wenn der Text in den Schritten steckt).</summary>
        public string QuestionText { get; set; } = string.Empty;

        /// <summary>Fremdschlüssel zur zugehörigen Kategorie.</summary>
        public Guid CategoryId { get; set; }

        /// <summary>Basis-Punktewert der Frage. Wird durch Schwierigkeit und Phasen-Multiplikatoren skaliert.</summary>
        public int Points { get; set; }

        /// <summary>Basis-Minuspunkte bei falscher Antwort. Wird ebenfalls skaliert.</summary>
        public int MinusPoints { get; set; }

        /// <summary>
        /// Wenn <c>true</c>, werden Punkte bei jeder aufgedeckten Einheit proportional reduziert
        /// (relevant für <c>PropertiesQuestion</c>).
        /// </summary>
        public bool UseProportionalScoreReductionOnStep { get; set; } = false;

        /// <summary>Interne Notizen des Spielleiters zur Frage (nicht für Spieler sichtbar).</summary>
        public string Notes { get; set; } = string.Empty;

        /// <summary>
        /// Konkreter Fragetyp. Wird von Unterklassen im Konstruktor gesetzt
        /// und ist danach schreibgeschützt.
        /// </summary>
        public QuestionType Typ { get; protected set; }

        /// <summary>
        /// Standard-Abschluss-Typ für den automatisch erzeugten Finish-Schritt,
        /// falls kein expliziter Finish-Schritt in <see cref="Steps"/> vorhanden ist.
        /// Wird von Unterklassen im Konstruktor gesetzt.
        /// </summary>
        public FinishType DefaultFinishType { get; protected set; } = FinishType.None;

        /// <summary>Schwierigkeitsgrad der Frage; beeinflusst den berechneten Punktewert.</summary>
        public Difficulty Difficulty { get; set; } = Difficulty.Level1;

        /// <summary>Wenn <c>true</c>, warnt der Spielleiter beim Erreichen des Ergebnis-Schritts.</summary>
        public bool WarnOnResultStep { get; set; } = true;

        /// <summary>Wenn <c>true</c>, warnt der Spielleiter beim Erreichen des Finish-Schritts.</summary>
        public bool WarnOnFinishStep { get; set; } = true;

        /// <summary>
        /// Wenn <c>true</c>, werden die normalen Schritte vor der Anzeige zufällig gemischt
        /// (z.B. für Multiple-Choice-Antworten).
        /// </summary>
        public bool UseRandomSequenceOnNoneFinishSteps { get; set; } = false;

        /// <summary>Schema für die Beschriftung der Antwort-Tasten (alphabetisch oder numerisch).</summary>
        public QuestionViewKeyType QuestionViewKeyType { get; set; } = QuestionViewKeyType.Alphabetical;

        #region Buzzer

        /// <summary>Eingabe-Layout, das dem Spieler im Browser angezeigt wird, wenn diese Frage aktiv ist.</summary>
        public BuzzerControlsLayout BuzzerControlsLayout { get; set; } = BuzzerControlsLayout.Buzzer;

        /// <summary>Maximale Anzahl auswählbarer Antwort-Tasten im KeySelect-Layout.</summary>
        public int BuzzerMaxAllowedKeySelect { get; set; } = 1;

        /// <summary>Wenn <c>true</c>, wird der Antworttext im KeySelect-Layout im Browser angezeigt.</summary>
        public bool ShowTextOnKeySelect { get; set; } = true;

        #endregion Buzzer

        #region View

        /// <summary>Anordnung der Schritte auf dem Spieler-Bildschirm (vertikal, horizontal oder Grid).</summary>
        public StepDisplayLayoutMode StepDisplayLayoutMode { get; set; } = StepDisplayLayoutMode.Vertical;

        #endregion View

        /// <summary>Alle Schritte dieser Frage, unsortiert aus der Datenbank geladen.</summary>
        public List<QuestionStepResource> Steps { get; set; } = new();

        /// <summary>Navigation-Property zur zugehörigen Kategorie.</summary>
        public Category? Category { get; set; }

        /// <summary>
        /// Geordnete Schritt-Sequenz, berechnet von <see cref="CalculateOrderdSteps"/>.
        /// Nicht in der Datenbank gespeichert; muss nach jedem Laden neu berechnet werden.
        /// </summary>
        [NotMapped]
        public QuestionStepResource[] OrderedSteps { get; set; } = [];

        /// <summary>
        /// Gibt den nächsten Schritt nach <paramref name="currentStep"/> zurück.
        /// Gibt den ersten Schritt zurück, wenn <paramref name="currentStep"/> <c>null</c> ist.
        /// Gibt <c>null</c> zurück, wenn kein weiterer Schritt vorhanden ist.
        /// </summary>
        /// <param name="currentStep">Der aktuell angezeigte Schritt, oder <c>null</c> für den Anfang.</param>
        public QuestionStepResource? GetNextStep(QuestionStepResource? currentStep = null)
        {
            if (!OrderedSteps.Any())
                return null;

            if (currentStep == null)
                return OrderedSteps.First();

            var seq = currentStep.SequenceNumber;
            return OrderedSteps.FirstOrDefault(s => s.SequenceNumber > seq);
        }

        /// <summary>
        /// Gibt den Schritt vor <paramref name="currentStep"/> zurück.
        /// Gibt <c>null</c> zurück, wenn kein vorheriger Schritt vorhanden ist.
        /// </summary>
        /// <param name="currentStep">Der aktuell angezeigte Schritt.</param>
        public QuestionStepResource? GetStepBehind(QuestionStepResource? currentStep = null)
        {
            if (!OrderedSteps.Any() || currentStep == null)
                return null;

            var seq = currentStep.SequenceNumber;
            return OrderedSteps.Reverse().FirstOrDefault(s => s.SequenceNumber < seq);
        }

        /// <summary>
        /// Berechnet die geordnete Schritt-Sequenz und schreibt sie in <see cref="OrderedSteps"/>.
        /// Reihenfolge: Start-Schritte → normale Schritte (sortiert oder zufällig) → Finish-Schritte.
        /// Fehlende Start- oder Finish-Schritte werden automatisch ergänzt.
        /// Jedem normalen Schritt wird ein <c>QuestionViewKey</c> zugewiesen.
        /// </summary>
        /// <summary>
        /// Zufallsquelle fuer das Mischen der normalen Schritte. Im laufenden Programm
        /// <see cref="Random.Shared"/>; Tests setzen eine Quelle mit festem Startwert ein, sonst
        /// laesst sich die gemischte Reihenfolge nicht pruefen.
        /// </summary>
        public static Random Randomizer { get; set; } = Random.Shared;

        public void CalculateOrderdSteps()
        {
            if (Steps == null || !Steps.Any())
            {
                OrderedSteps = [];
                return;
            }

            var result = new List<QuestionStepResource>();

            var normalSteps = Steps.Where(s => !s.IsFinish && !s.IsStart).ToList();
            var startSteps = Steps.Where(s => s.IsStart).ToList();
            var finishSteps = Steps.Where(s => s.IsFinish).ToList();

            if (UseRandomSequenceOnNoneFinishSteps)
            {
                normalSteps = normalSteps
                    .OrderBy(_ => Randomizer.Next())
                    .ToList();
            }
            else
            {
                normalSteps = normalSteps
                    .OrderBy(s => s.SequenceNumber)
                    .ToList();
            }

            if (finishSteps.Count == 0)
            {
                finishSteps.Add(new QuestionStepResource
                {
                    FinishType = DefaultFinishType,
                    IsFinish = true,
                    Id = Guid.NewGuid()
                });
            }

            // Frueher wurde hier ein leerer Startschritt erfunden, wenn keiner vorlag - und weil
            // IsStart nicht gespeichert wurde, lag nie einer vor. Der erste Druck auf "Weiter"
            // zeigte dadurch immer einen leeren Bildschirm. Wer ein Intro will, legt es jetzt an.

            var ordered = startSteps.Concat(normalSteps).Concat(finishSteps).ToList();

            var nextSequenceNumber = 0;
            var currentKey = Helpers.Helper.GetNextViewKey(string.Empty, QuestionViewKeyType);

            foreach (var step in ordered)
            {
                step.SequenceNumber = nextSequenceNumber;

                if (!step.IsStart && !step.IsFinish)
                {
                    step.QuestionViewKey = currentKey;
                    currentKey = Helpers.Helper.GetNextViewKey(currentKey, QuestionViewKeyType);
                }

                result.Add(step);
                nextSequenceNumber += 10;
            }

            OrderedSteps = result.ToArray();
        }

        /// <summary>
        /// Factory-Methode, die von <see cref="CloneWithoutReferences"/> aufgerufen wird,
        /// um eine Instanz des richtigen konkreten Typs zu erzeugen.
        /// Unterklassen überschreiben diese Methode, um den korrekten Typ zurückzugeben.
        /// </summary>
        protected virtual QuestionBase CreateCloneInstance()
        {
            return new QuestionBase();
        }

        /// <inheritdoc/>
        public override QuestionBase CloneWithoutReferences(bool copyIdentity = true)
        {
            var clone = CreateCloneInstance();
            CopyQuestionBaseValuesTo(clone, copyIdentity);
            return clone;
        }

        /// <summary>
        /// Kopiert alle Felder von <c>QuestionBase</c> in die Zielinstanz.
        /// Navigation-Properties (<see cref="Steps"/>, <see cref="Category"/>, <see cref="OrderedSteps"/>)
        /// werden dabei nicht übertragen. Ruft <c>CopyBaseValuesTo</c> für die Basisfelder auf.
        /// </summary>
        protected void CopyQuestionBaseValuesTo(QuestionBase target, bool copyIdentity = true)
        {
            CopyBaseValuesTo(target, copyIdentity);

            target.DesignationShort = DesignationShort;
            target.QuestionText = QuestionText;
            target.CategoryId = CategoryId;
            target.Points = Points;
            target.MinusPoints = MinusPoints;
            target.UseProportionalScoreReductionOnStep = UseProportionalScoreReductionOnStep;
            target.Notes = Notes;
            target.Typ = Typ;
            target.DefaultFinishType = DefaultFinishType;
            target.Difficulty = Difficulty;
            target.WarnOnResultStep = WarnOnResultStep;
            target.WarnOnFinishStep = WarnOnFinishStep;
            target.UseRandomSequenceOnNoneFinishSteps = UseRandomSequenceOnNoneFinishSteps;
            target.QuestionViewKeyType = QuestionViewKeyType;

            target.BuzzerControlsLayout = BuzzerControlsLayout;
            target.BuzzerMaxAllowedKeySelect = BuzzerMaxAllowedKeySelect;
            target.ShowTextOnKeySelect = ShowTextOnKeySelect;

            target.StepDisplayLayoutMode = StepDisplayLayoutMode;

            target.Steps = new List<QuestionStepResource>();
            target.Category = null;
            target.OrderedSteps = [];
        }
    }
}