using System;
using System.Collections.Generic;
using System.Text;

namespace Quizzer.DataModels.Enumerations
{
    /// <summary>
    /// Beschreibt den aktuellen Lebenszyklus-Zustand eines Spiels.
    /// </summary>
    public enum GameState
    {
        /// <summary>Das Spiel wird noch konfiguriert (Fragen zuweisen, Spieler hinzufügen).</summary>
        Building = 0,

        /// <summary>Das Spiel läuft aktiv; Fragen werden gespielt und Punkte vergeben.</summary>
        InProgress = 1,

        /// <summary>Alle Felder wurden gespielt; das Spiel ist abgeschlossen.</summary>
        Finished = 2,
    }
}