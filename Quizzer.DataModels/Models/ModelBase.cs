using System;
using System.ComponentModel.DataAnnotations;

namespace Quizzer.DataModels.Models
{
    /// <summary>
    /// Nicht-generische Basisklasse für alle Datenbankentitäten.
    /// Stellt den Primärschlüssel (<see cref="Id"/>), eine lesbare Bezeichnung
    /// (<see cref="Designation"/>) und einen optimistischen Sperrmechanismus
    /// (<see cref="RowVersion"/>) bereit.
    /// </summary>
    public abstract class ModelBase
    {
        /// <summary>Eindeutiger Primärschlüssel der Entität (GUID).</summary>
        [Key]
        public Guid Id { get; set; }

        /// <summary>Lesbare Bezeichnung des Datensatzes (z.B. Name einer Kategorie oder eines Spielers).</summary>
        public string Designation { get; set; } = string.Empty;

        /// <summary>
        /// Optimistisches Sperr-Token, das EF Core bei jedem UPDATE automatisch aktualisiert.
        /// Verhindert konkurrierende Schreibzugriffe (Concurrency-Konflikt).
        /// </summary>
        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }

    /// <summary>
    /// Generisches Klonierungsinterface für Entitäten, das tiefenfreie Kopien
    /// (ohne Navigation-Properties) liefert.
    /// </summary>
    /// <typeparam name="TEntity">Der konkrete Entitätstyp.</typeparam>
    public interface ICloneWithoutReferences<out TEntity>
    {
        /// <summary>
        /// Erstellt eine flache Kopie der Entität ohne Navigation-Properties.
        /// </summary>
        /// <param name="copyIdentity">
        /// Wenn <c>true</c>, werden <c>Id</c> und <c>RowVersion</c> übernommen;
        /// wenn <c>false</c>, erhält der Klon neue/leere Identitätswerte.
        /// </param>
        TEntity CloneWithoutReferences(bool copyIdentity = false);
    }

    /// <summary>
    /// Generische Basisklasse für typisierte Entitäten. Implementiert
    /// <see cref="ICloneWithoutReferences{TEntity}"/> und stellt die
    /// Hilfsmethode <see cref="CopyBaseValuesTo"/> für Unterklassen bereit.
    /// </summary>
    /// <typeparam name="TEntity">Der konkrete Entitätstyp, der von dieser Klasse erbt.</typeparam>
    public abstract class ModelBase<TEntity> : ModelBase, ICloneWithoutReferences<TEntity>
    where TEntity : ModelBase
    {
        /// <inheritdoc/>
        public abstract TEntity CloneWithoutReferences(bool copyIdentity = true);

        /// <summary>
        /// Kopiert die Basisfelder (<c>Id</c>, <c>RowVersion</c>, <c>Designation</c>)
        /// in die Zielentität. Wird von <c>CloneWithoutReferences</c>-Implementierungen
        /// in Unterklassen aufgerufen.
        /// </summary>
        /// <param name="target">Zielentität, in die die Werte kopiert werden.</param>
        /// <param name="copyIdentity">Wenn <c>true</c>, werden Id und RowVersion übertragen.</param>
        protected void CopyBaseValuesTo(TEntity target, bool copyIdentity = true)
        {
            if (copyIdentity)
            {
                target.Id = Id;
                target.RowVersion = RowVersion?.ToArray();
            }

            target.Designation = Designation;
        }
    }
}