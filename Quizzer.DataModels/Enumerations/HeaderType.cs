using System;
using System.Collections.Generic;
using System.Text;

namespace Quizzer.DataModels.Enumerations
{
    /// <summary>
    /// Gibt an, ob ein <c>Header</c>-Eintrag eine Zeilen- oder Spaltenüberschrift
    /// im Spielfeld-Grid darstellt.
    /// </summary>
    public enum HeaderType
    {
        /// <summary>Zeilenüberschrift (vertikale Achse, typischerweise Schwierigkeitsgrade).</summary>
        Row = 0,

        /// <summary>Spaltenüberschrift (horizontale Achse, typischerweise Kategorien).</summary>
        Column = 1
    }
}