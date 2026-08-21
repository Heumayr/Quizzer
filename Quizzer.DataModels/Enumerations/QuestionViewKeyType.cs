using System;
using System.Collections.Generic;
using System.Text;

namespace Quizzer.DataModels.Enumerations
{
    /// <summary>
    /// Bestimmt, nach welchem Schema die Antwort-Tasten-Bezeichnungen für die
    /// Schritte einer Frage generiert werden (z.B. A, B, C … oder 1, 2, 3 …).
    /// Wird von <c>Helper.GetNextViewKey</c> ausgewertet und in
    /// <c>QuestionBase.CalculateOrderdSteps</c> jedem Schritt zugewiesen.
    /// </summary>
    public enum QuestionViewKeyType
    {
        /// <summary>Buchstaben-Sequenz: A, B, C, …, Z, AA, AB, …</summary>
        Alphabetical = 0,

        /// <summary>Zahlen-Sequenz: 1, 2, 3, …</summary>
        Numerical = 1,
    }
}