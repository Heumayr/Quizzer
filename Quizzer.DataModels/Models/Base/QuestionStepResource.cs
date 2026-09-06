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

        /// <summary>Gibt an, ob dieser Schritt das Ergebnis (die Auflösung) der Frage darstellt.</summary>
        public bool IsResult { get; set; } = false;

        /// <summary>
        /// Gibt an, ob dieser Schritt der Abschluss-Schritt ist. Er laeuft immer zuletzt und
        /// zeigt die Aufloesung.
        /// </summary>
        public bool IsFinish { get; set; } = false;

        /// <summary>
        /// Reihenfolge-Nummer innerhalb der geordneten Schritt-Sequenz.
        /// Wird von <c>QuestionBase.CalculateOrderdSteps()</c> in Zehner-Schritten vergeben.
        /// </summary>
        public int SequenceNumber { get; set; }

        /// <summary>Anzeigetext des Schritts (Frage, Hinweis oder Antwort-Option).</summary>
        public string StepText { get; set; } = string.Empty;

        /// <summary>Dateiname der verknüpften Mediendatei (relativ zu <c>Settings.ResourceRootFolder</c>).</summary>
        public string ResourceFileName { get; set; } = string.Empty;

        /// <summary>Typ der verknüpften Ressource; bestimmt den zu verwendenden Media-Player.</summary>
        public ResourceType ResourceTyp { get; set; } = ResourceType.None;

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
        /// Gibt an, ob dieser Schritt das Intro der Frage ist. Startschritte tragen keinen
        /// Anzeigeschluessel und laufen als erstes.
        /// <para>
        /// Bis zum 21.08.2026 war dies <c>[NotMapped]</c> und wurde an genau einer Stelle
        /// gesetzt: auf einem Schritt, den <c>CalculateOrderdSteps</c> selbst erfunden hat.
        /// Ein Startschritt liess sich also gar nicht <i>anlegen</i>.
        /// </para>
        /// <para>
        /// <b>Seit dem 2026-09-06 gilt beides zugleich.</b> Die Spalte ist echt und im
        /// Schritt-Dialog setzbar; liegt aber keiner vor, ergaenzt <c>CalculateOrderdSteps</c>
        /// wieder einen leeren. Der leere erste Bildschirm ist die <b>Spielregel</b>: der
        /// Spielleiter liest die Frage vor, und wer buzzert, bevor sie zu Ende gelesen ist, darf
        /// sie nicht lesen koennen.
        /// </para>
        /// <para>
        /// Der Unterschied, auf den es ankommt, ist damit „vom Spielleiter angelegt" gegen „vom
        /// Modell ergaenzt" - erkennbar daran, dass der ergaenzte Schritt <b>nicht</b> in
        /// <c>Steps</c> liegt und <c>QuestionBaseId</c> leer traegt.
        /// </para>
        /// </summary>
        public bool IsStart { get; set; }

        /// <summary>
        /// Der Bildschirm, auf dem nur die Frage steht - und sonst nichts.
        /// <para>
        /// <b>Nutzerentscheidung vom 2026-09-06:</b> „dann sollte aber immer danach ein schritt
        /// folgen der ... die frage einblendet und unten noch nichts steht ... erst dann der
        /// nächste". Der Anlass war die Schätzfrage: sie hat zwischen Start und Auflösung keinen
        /// Schritt, also standen Frage und Antwort auf demselben Bildschirm.
        /// </para>
        /// <para>
        /// <b>Der Schritt wird immer ergänzt</b> und liegt wie der Startschritt nur in
        /// <see cref="QuestionBase.OrderedSteps"/>, nie in <c>Steps</c>. Deshalb
        /// <c>[NotMapped]</c>: es gibt ihn nur zur Anzeige.
        /// </para>
        /// <para>
        /// <b>Er zählt nirgends als normaler Schritt.</b> Er bekommt keine Antworttaste - sonst
        /// verschöben sich die Tasten auf den Telefonen um eine - und er geht nicht in die Zahl
        /// der Hinweise ein, aus der die Eigenschaftsfrage ihren Punkteabzug rechnet.
        /// </para>
        /// </summary>
        [NotMapped]
        public bool IsQuestionOnly { get; set; }

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
                IsResult = IsResult,
                IsFinish = IsFinish,
                SequenceNumber = SequenceNumber,
                StepText = StepText,
                ResourceFileName = ResourceFileName,
                ResourceTyp = ResourceTyp,
                QuestionViewKey = QuestionViewKey,
                IsStart = IsStart
            };

            CopyBaseValuesTo(clone, copyIdentity);
            return clone;
        }
    }
}