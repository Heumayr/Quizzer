using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Quizzer.DataModels.Models.Base
{
    /// <summary>
    /// Verknüpfungstabelle zwischen zwei <see cref="QuestionStepResource"/>-Einträgen.
    /// Ermöglicht es, Abhängigkeiten oder Übergänge zwischen Schritten zu modellieren
    /// (z.B. "Schritt A verweist auf Schritt B als Ergebnis-Referenz").
    /// Der Unique-Index verhindert doppelte From-To-Paare.
    /// Gespeichert in der Tabelle <c>question.StepXStep</c>.
    /// </summary>
    [Table(nameof(StepXStep), Schema = "question")]
    [Index(nameof(FromId), nameof(ToId), IsUnique = true)]
    public class StepXStep : ModelBase<StepXStep>
    {
        /// <summary>Fremdschlüssel des Quell-Schritts (nullable für optionale Verknüpfungen).</summary>
        public Guid? FromId { get; set; }

        /// <summary>Fremdschlüssel des Ziel-Schritts (nullable für optionale Verknüpfungen).</summary>
        public Guid? ToId { get; set; }

        /// <summary>Navigation-Property zum Quell-Schritt.</summary>
        [ForeignKey(nameof(FromId))]
        public QuestionStepResource? From { get; set; }

        /// <summary>Navigation-Property zum Ziel-Schritt.</summary>
        [ForeignKey(nameof(ToId))]
        public QuestionStepResource? To { get; set; }

        public override StepXStep CloneWithoutReferences(bool copyIdentity = true)
        {
            var clone = new StepXStep
            {
                FromId = FromId,
                ToId = ToId,
                From = null,
                To = null
            };

            CopyBaseValuesTo(clone, copyIdentity);
            return clone;
        }
    }
}