using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.QuestionTypes;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Quizzer.DataModels.Models.Base
{
    /// <summary>
    /// Repräsentiert einen einzelnen Schritt innerhalb einer Frage (z.B. einen Hinweis,
    /// eine Antwort-Option oder den Abschluss-Schritt). Schritte werden von
    /// <c>QuestionBase.CalculateOrderdSteps()</c> in die richtige Reihenfolge gebracht.
    /// Gespeichert in der Tabelle <c>question.QuestionStepResource</c>.
    /// </summary>
    [Table(nameof(QuestionStepResource), Schema = "question")]
    public class QuestionStepResource : ModelBase<QuestionStepResource>
    {
        /// <summary>Fremdschlüssel zur übergeordneten Frage.</summary>
        public Guid QuestionBaseId { get; set; }

        /// <summary>
        /// Optionaler Gruppierungsschlüssel, um mehrere Schritte logisch zusammenzufassen
        /// (z.B. Antwort-Optionen einer Multiple-Choice-Frage).
        /// </summary>
        public string GroupKey { get; set; } = string.Empty;

        /// <summary>Gibt an, ob dieser Schritt das Ergebnis (die Auflösung) der Frage darstellt.</summary>
        public bool IsResult { get; set; } = false;

        /// <summary>
        /// Gibt an, ob dieser Schritt der Abschluss-Schritt ist.
        /// Der <see cref="FinishType"/> steuert, was in diesem Schritt dargestellt wird.
        /// </summary>
        public bool IsFinish { get; set; } = false;

        /// <summary>
        /// Reihenfolge-Nummer innerhalb der geordneten Schritt-Sequenz.
        /// Wird von <c>QuestionBase.CalculateOrderdSteps()</c> in Zehner-Schritten vergeben.
        /// </summary>
        public int SequenceNumber { get; set; }

        /// <summary>Anzeigetext des Schritts (Frage, Hinweis oder Antwort-Option).</summary>
        public string StepText { get; set; } = string.Empty;

        /// <summary>Bestimmt, wie der Abschluss-Schritt angezeigt wird (nur für <c>IsFinish = true</c> relevant).</summary>
        public FinishType FinishType { get; set; } = FinishType.AllPreviousSteps;

        /// <summary>Dateiname der verknüpften Mediendatei (relativ zu <c>Settings.ResourceRootFolder</c>).</summary>
        public string ResourceFileName { get; set; } = string.Empty;

        /// <summary>Typ der verknüpften Ressource; bestimmt den zu verwendenden Media-Player.</summary>
        public ResourceType ResourceTyp { get; set; } = ResourceType.None;

        /// <summary>Schritte, von denen dieser Schritt als Ziel referenziert wird (StepXStep-Verknüpfungen).</summary>
        [InverseProperty(nameof(StepXStep.From))]
        public List<StepXStep> Froms { get; set; } = new();

        /// <summary>Schritte, auf die dieser Schritt als Quelle verweist (StepXStep-Verknüpfungen).</summary>
        [InverseProperty(nameof(StepXStep.To))]
        public List<StepXStep> Tos { get; set; } = new();

        /// <summary>Gibt <c>true</c> zurück, wenn dem Schritt eine Mediendatei beigefügt ist.</summary>
        [NotMapped]
        public bool HasResource => ResourceTyp != ResourceType.None;

        /// <summary>
        /// Anzeigeschlüssel des Schritts (z.B. "A", "B" oder "1", "2"),
        /// der von <c>QuestionBase.CalculateOrderdSteps()</c> gemäß <c>QuestionViewKeyType</c> gesetzt wird.
        /// Nicht in der Datenbank gespeichert.
        /// </summary>
        [NotMapped]
        public string QuestionViewKey { get; set; } = string.Empty;

        /// <summary>
        /// Gibt an, ob dieser Schritt der Start-Schritt ist (Intro der Frage).
        /// Nicht in der Datenbank gespeichert; wird zur Laufzeit von
        /// <c>QuestionBase.CalculateOrderdSteps()</c> gesetzt.
        /// </summary>
        [NotMapped]
        public bool IsStart { get; set; }

        /// <summary>Gibt eine lesbare Darstellung des Schritts zurück: "[SequenceNumber]-[Designation]".</summary>
        public override string ToString()
        {
            return $"{SequenceNumber}-{Designation}";
        }

        public override QuestionStepResource CloneWithoutReferences(bool copyIdentity = true)
        {
            var clone = new QuestionStepResource
            {
                QuestionBaseId = QuestionBaseId,
                GroupKey = GroupKey,
                IsResult = IsResult,
                IsFinish = IsFinish,
                SequenceNumber = SequenceNumber,
                StepText = StepText,
                FinishType = FinishType,
                ResourceFileName = ResourceFileName,
                ResourceTyp = ResourceTyp,
                Froms = new List<StepXStep>(),
                Tos = new List<StepXStep>(),
                QuestionViewKey = QuestionViewKey,
                IsStart = IsStart
            };

            CopyBaseValuesTo(clone, copyIdentity);
            return clone;
        }
    }
}