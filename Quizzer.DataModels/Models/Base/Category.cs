using Newtonsoft.Json;
using Quizzer.DataModels.Attributes;
using Quizzer.DataModels.Models.QuestionTypes;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Quizzer.DataModels.Models.Base
{
    /// <summary>
    /// Repräsentiert eine Fragekategorie (z.B. "Geschichte", "Sport").
    /// Kategorien werden Fragen zugewiesen und dienen als Spalten-Bezeichnungen
    /// im Spielfeld-Grid. Gespeichert in der Tabelle <c>base.Category</c>.
    /// </summary>
    [Table(nameof(Category), Schema = "base")]
    public class Category : ModelBase<Category>
    {
        /// <summary>
        /// Alle Fragen, die dieser Kategorie zugeordnet sind.
        /// Nicht in der Datenbank gespeichert (<c>[NotMapped]</c>);
        /// wird bei Bedarf aus <c>QuestionBase</c> geladen.
        /// </summary>
        [NotMapped]
        public List<QuestionBase> Questions { get; set; } = new List<QuestionBase>();

        /// <inheritdoc/>
        public override Category CloneWithoutReferences(bool copyIdentity = true)
        {
            var clone = new Category
            {
                Questions = new List<QuestionBase>()
            };

            CopyBaseValuesTo(clone, copyIdentity);
            return clone;
        }
    }
}