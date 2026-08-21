using System;
using System.Collections.Generic;
using System.Text;

namespace Quizzer.DataModels.Enumerations
{
    /// <summary>
    /// Bestimmt, in welchem Kontext eine Spielfeld-Zelle dargestellt wird.
    /// Wird vom <c>GridBuilder</c> verwendet, um den richtigen Anzeigemodus
    /// für die jeweilige Ansicht zu erzeugen.
    /// </summary>
    public enum CellView
    {
        /// <summary>Bearbeitungs-Ansicht: Zelle zeigt Informationen zum Zuweisen von Fragen.</summary>
        Build,

        /// <summary>Spielleiter-Ansicht: Zelle zeigt verkürzte Fragen-Bezeichnung und Punkte.</summary>
        Master,

        /// <summary>Publikums-Ansicht (Spieler-Bildschirm): Zelle zeigt das Spielfeld für alle Teilnehmer.</summary>
        PlayField
    }
}