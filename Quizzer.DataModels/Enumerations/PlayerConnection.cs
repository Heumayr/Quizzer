using System;
using System.Collections.Generic;
using System.Text;

namespace Quizzer.DataModels.Enumerations
{
    /// <summary>
    /// Gibt den aktuellen Verbindungsstatus eines Spielers zum Buzzer-Server an.
    /// Wird in <c>Player.ConnectionState</c> gesetzt und löst das
    /// <c>PlayerConnectionChanged</c>-Ereignis aus.
    /// </summary>
    public enum PlayerConnection
    {
        /// <summary>Verbindungsstatus ist unbekannt (Initialzustand).</summary>
        Unknown = 0,

        /// <summary>Spieler ist mit dem Buzzer-Server verbunden.</summary>
        Connected = 1,

        /// <summary>Spieler hat die Verbindung getrennt oder wurde getrennt.</summary>
        Disconnected = 2,
    }
}