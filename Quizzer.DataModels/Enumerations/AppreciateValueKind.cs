using System.ComponentModel;

namespace Quizzer.DataModels.Enumerations
{
    /// <summary>
    /// Was bei einer Schaetzfrage geschaetzt wird. Bestimmt, welche Einheiten zur Wahl stehen,
    /// welche Eingabeart die Spieler am Telefon bekommen und wie der Abstand zum Sollwert
    /// gerechnet wird.
    /// </summary>
    public enum AppreciateValueKind
    {
        /// <summary>Eine blosse Anzahl ohne Einheit (Einwohner, Stueck, Punkte).</summary>
        [Description("Zahl")]
        Number = 0,

        /// <summary>Ein Anteil in Prozent.</summary>
        [Description("Prozent")]
        Percent = 1,

        /// <summary>Eine Laenge oder Entfernung.</summary>
        [Description("Laenge")]
        Length = 2,

        /// <summary>Eine Masse.</summary>
        [Description("Masse")]
        Mass = 3,

        /// <summary>Ein Volumen.</summary>
        [Description("Volumen")]
        Volume = 4,

        /// <summary>Eine Zeitspanne.</summary>
        [Description("Dauer")]
        Duration = 5,

        /// <summary>Ein Kalenderdatum. Der Abstand wird in Tagen gerechnet.</summary>
        [Description("Datum")]
        Date = 6,

        /// <summary>Ein Geldbetrag.</summary>
        [Description("Geldbetrag")]
        Money = 7,
    }
}
