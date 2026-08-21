using System;
using System.Collections.Generic;
using System.Text;

namespace Quizzer.DataModels.Attributes
{
    /// <summary>
    /// Markiert eine Eigenschaft, deren Wert vor dem Speichern in die Datenbank
    /// automatisch zurückgesetzt werden soll (z.B. Laufzeit-Cache-Felder).
    /// Wird ausschließlich auf Properties angewendet (<see cref="AttributeTargets.Property"/>).
    /// Die Auswertung erfolgt durch den jeweiligen Controller oder Speicher-Mechanismus.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class ClearOnSaveAttribute : Attribute
    {
    }
}