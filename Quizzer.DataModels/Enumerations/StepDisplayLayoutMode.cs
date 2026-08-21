using System;
using System.Collections.Generic;
using System.Text;

namespace Quizzer.DataModels.Enumerations
{
    /// <summary>
    /// Bestimmt, wie die Schritte einer Frage auf dem Spieler-Bildschirm angeordnet werden.
    /// Wird in <c>QuestionBase.StepDisplayLayoutMode</c> gespeichert und von den
    /// Schritt-Ansichten unter <c>Quizzer/Views/GameViews/QuestionViews/Typed</c> ausgewertet.
    /// Achtung: nicht jeder Fragetyp unterstuetzt jeden Modus - die Multiple-Choice-Ansicht
    /// verlangt <see cref="Grid"/>, die Eigenschaften-Ansicht <see cref="Vertical"/>; jeder
    /// andere Wert erzeugt dort die Anzeige "Not Supported".
    /// </summary>
    public enum StepDisplayLayoutMode
    {
        /// <summary>Schritte werden von oben nach unten untereinander dargestellt.</summary>
        Vertical = 0,

        /// <summary>Schritte werden nebeneinander in einer Zeile dargestellt.</summary>
        Horizontal = 1,

        /// <summary>Schritte werden in einem mehrspaltigem Grid dargestellt (z.B. bei Multiple-Choice).</summary>
        Grid = 2
    }
}