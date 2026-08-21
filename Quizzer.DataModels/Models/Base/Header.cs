using Quizzer.DataModels.Enumerations;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Quizzer.DataModels.Models.Base
{
    /// <summary>
    /// Repräsentiert eine Spalten- oder Zeilenüberschrift im Spielfeld-Grid eines Spiels.
    /// Jeder <c>Header</c> gehört genau zu einem <see cref="Game"/> und wird über
    /// <see cref="HeaderType"/> als Spalte (Kategorie) oder Zeile (Schwierigkeitsgrad) klassifiziert.
    /// Gespeichert in der Tabelle <c>base.Header</c>.
    /// </summary>
    [Table(nameof(Header), Schema = "base")]
    public class Header : ModelBase<Header>
    {
        /// <summary>Fremdschlüssel zum zugehörigen Spiel.</summary>
        public Guid GameId { get; set; }

        /// <summary>Gibt an, ob dieser Header eine Zeile oder eine Spalte beschriftet.</summary>
        public HeaderType HeaderType { get; set; }

        /// <summary>Reihenfolge-Index des Headers innerhalb seiner Gruppe (0-basiert).</summary>
        public int Index { get; set; } = 0;

        /// <summary>Navigation-Property zum zugehörigen Spiel.</summary>
        public Game? Game { get; set; }

        /// <inheritdoc/>
        public override Header CloneWithoutReferences(bool copyIdentity = true)
        {
            var clone = new Header
            {
                GameId = GameId,
                HeaderType = HeaderType,
                Index = Index,
                Game = null
            };

            CopyBaseValuesTo(clone, copyIdentity);
            return clone;
        }
    }
}