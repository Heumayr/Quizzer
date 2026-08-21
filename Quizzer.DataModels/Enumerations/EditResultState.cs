using System;
using System.Collections.Generic;
using System.Text;

namespace Quizzer.DataModels.Enumerations
{
    /// <summary>
    /// Gibt an, mit welchem Ergebnis ein Bearbeitungs-Dialog geschlossen wurde.
    /// Wird von ViewModels verwendet, um nach dem Schließen eines Fensters
    /// die passende Folgeaktion (Speichern, Verwerfen, Löschen) auszuführen.
    /// </summary>
    public enum EditResultState
    {
        /// <summary>Kein Ergebnis; der Dialog wurde noch nicht geschlossen.</summary>
        None,

        /// <summary>Ein neuer Datensatz wurde angelegt und soll gespeichert werden.</summary>
        New,

        /// <summary>Ein bestehender Datensatz wurde geändert und soll gespeichert werden.</summary>
        Updated,

        /// <summary>Die Bearbeitung wurde abgebrochen; keine Änderungen speichern.</summary>
        Canceled,

        /// <summary>Der Datensatz soll gelöscht werden.</summary>
        Deleted,
    }
}