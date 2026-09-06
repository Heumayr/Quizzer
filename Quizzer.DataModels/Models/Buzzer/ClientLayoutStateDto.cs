using Quizzer.DataModels.Enumerations;
using System;
using System.Collections.Generic;
using System.Text;

namespace Quizzer.DataModels.Models.Buzzer
{
    /// <summary>
    /// Datenübertragungsobjekt (DTO), das der SignalR-Hub an den Browser-Client sendet,
    /// wenn sich der Zustand des Buzzer-Layouts ändert. Der Client verwendet diese Daten,
    /// um das angezeigte Eingabe-Layout (Buzzer, KeySelect, Input) zu aktualisieren.
    /// Wird über das SignalR-Ereignis <c>StateChanged</c> übertragen.
    /// </summary>
    public sealed class ClientLayoutStateDto
    {
        /// <summary>Anzeigename des verbundenen Spielers (zur Personalisierung der Buzzer-Oberfläche).</summary>
        public string? PlayerName { get; set; }

        /// <summary>Aktuelle Runde des Spiels (wird im Browser angezeigt).</summary>
        public int Round { get; set; }

        /// <summary>
        /// Wie oft die Runde zurückgesetzt wurde. Die Telefonseite nimmt die Zahl in ihre
        /// Layout-Kennung auf und baut dadurch beim Zurücksetzen wirklich neu auf, statt nur zu
        /// entsperren. <c>Round</c> taugt dafür nicht - die ändert sich dabei nicht.
        /// </summary>
        public int ResetCount { get; set; }

        /// <summary>Das derzeit aktive Eingabe-Layout im Browser.</summary>
        public BuzzerControlsLayout Layout { get; set; }

        /// <summary>Gibt an, ob das Layout des aktuellen Spielers gesperrt ist (z.B. nach dem Buzzen).</summary>
        public bool CurrentLayoutLocked { get; set; }

        /// <summary>Gibt an, ob alle Spieler gesperrt sind (z.B. nach Abgabe aller Antworten).</summary>
        public bool AllLocked { get; set; }

        /// <summary>
        /// Layout-spezifische Zusatzinformationen (z.B. Antwort-Optionen bei KeySelect).
        /// Der Typ hängt vom aktiven <see cref="Layout"/> ab und wird client-seitig deserialisiert.
        /// </summary>
        public object? LayoutInfo { get; set; }

        /// <summary>Name des Gewinners der aktuellen Runde (nach erfolgreichem Buzz); <c>null</c> wenn noch kein Gewinner.</summary>
        public string? Winner { get; set; }
    }
}