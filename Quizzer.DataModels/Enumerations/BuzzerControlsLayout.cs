using System;
using System.Collections.Generic;
using System.Text;

namespace Quizzer.DataModels.Enumerations
{
    /// <summary>
    /// Gibt an, welches Eingabe-Layout dem Spieler im Browser angezeigt wird,
    /// wenn eine Frage aktiv ist. Der <c>LayoutStateManager</c> im
    /// <c>LocalBuzzer.Service</c> schaltet zwischen diesen Layouts um.
    /// </summary>
    public enum BuzzerControlsLayout
    {
        /// <summary>Kein aktives Layout; der Buzzer ist deaktiviert.</summary>
        None = 0,

        /// <summary>Klassisches Buzzer-Layout: erster Spieler, der drückt, gewinnt den Zug.</summary>
        Buzzer = 1,

        /// <summary>Spieler wählen eine oder mehrere vorgegebene Antwort-Tasten aus (z.B. Multiple-Choice).</summary>
        KeySelect = 2,

        /// <summary>Spieler geben einen freien Text ein (z.B. für Schätzfragen).</summary>
        Input = 3,
    }
}