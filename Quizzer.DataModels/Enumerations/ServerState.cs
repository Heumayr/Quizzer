using System;
using System.Collections.Generic;
using System.Text;

namespace Quizzer.DataModels.Enumerations
{
    /// <summary>
    /// Beschreibt den Betriebszustand des eingebetteten Kestrel/SignalR-Buzzer-Servers
    /// (<c>BuzzerServer</c>). Als Flags-Enum können mehrere Zustände kombiniert werden,
    /// um zusammengesetzte Bedingungen wie <see cref="ActiveState"/> auszudrücken.
    /// </summary>
    [Flags]
    public enum ServerState
    {
        /// <summary>Kein Zustand; Server wurde noch nicht gestartet.</summary>
        None = 0,

        /// <summary>Server wurde angehalten.</summary>
        Stopped = 1 << 0,

        /// <summary>Server startet gerade (Kestrel wird hochgefahren).</summary>
        Starting = 1 << 1,

        /// <summary>Server läuft und nimmt Verbindungen entgegen.</summary>
        Running = 1 << 2,

        /// <summary>Alle erwarteten Spieler sind verbunden.</summary>
        AllConnected = 1 << 3,

        /// <summary>Server wird gerade angehalten.</summary>
        Stopping = 1 << 4,

        /// <summary>Ein Fehler ist beim Starten oder Betrieb aufgetreten.</summary>
        Error = 1 << 5,

        /// <summary>Kombinationszustand: Server läuft (<see cref="Running"/>) oder alle Spieler sind verbunden (<see cref="AllConnected"/>).</summary>
        ActiveState = Running | AllConnected
    }
}