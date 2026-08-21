using System;
using System.Collections.Generic;
using System.Text;

namespace Quizzer.DataModels.Enumerations
{
    /// <summary>
    /// Definiert, welche Schritte im Abschluss-Schritt einer Frage angezeigt werden.
    /// Steuert das Verhalten des letzten <c>QuestionStepResource</c>-Eintrags
    /// (<c>IsFinish = true</c>) in der geordneten Schritt-Sequenz.
    /// </summary>
    public enum FinishType
    {
        /// <summary>Kein spezieller Abschluss-Typ; der Schritt verhält sich wie ein normaler Schritt.</summary>
        None = 0,

        /// <summary>Nur das Ergebnis (die Auflösung) der Frage wird angezeigt.</summary>
        OnlyResult = 1,

        /// <summary>Alle vorherigen Schritte werden zusammen mit dem Ergebnis nochmals angezeigt.</summary>
        AllPreviousSteps = 2,

        /// <summary>Das Ergebnis verweist explizit auf einen bestimmten Schritt als Referenz-Antwort.</summary>
        ResultReferencer = 3,
    }
}