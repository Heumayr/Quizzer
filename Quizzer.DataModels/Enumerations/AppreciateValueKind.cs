using System.ComponentModel;

namespace Quizzer.DataModels.Enumerations
{
    /// <summary>
    /// Was bei einer Schaetzfrage geschaetzt wird. Bestimmt, welche Einheiten zur Wahl stehen,
    /// welche Eingabeart die Spieler am Telefon bekommen und wie der Abstand zum Sollwert
    /// gerechnet wird.
    /// <para>
    /// Die Zahlenwerte sind in der Datenbank gespeichert und duerfen sich nicht aendern.
    /// Neue Arten bekommen den naechsten freien Wert.
    /// </para>
    /// </summary>
    public enum AppreciateValueKind
    {
        /// <summary>Eine blosse Anzahl ohne Einheit (Einwohner, Stueck, Punkte).</summary>
        [Description("Zahl")]
        Number = 0,

        /// <summary>Ein Anteil in Prozent.</summary>
        [Description("Prozent")]
        Percent = 1,

        /// <summary>Eine Laenge, Hoehe oder Entfernung.</summary>
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

        /// <summary>Eine Flaeche (Seen, Laender, Wohnungen).</summary>
        [Description("Flaeche")]
        Area = 8,

        /// <summary>Eine Temperatur.</summary>
        [Description("Temperatur")]
        Temperature = 9,

        /// <summary>Eine Geschwindigkeit.</summary>
        [Description("Geschwindigkeit")]
        Speed = 10,

        /// <summary>Eine Leistung (Motoren, Kraftwerke).</summary>
        [Description("Leistung")]
        Power = 11,

        /// <summary>Eine Energiemenge (Stromverbrauch, Naehrwert).</summary>
        [Description("Energie")]
        Energy = 12,

        /// <summary>Eine Datenmenge.</summary>
        [Description("Datenmenge")]
        DataVolume = 13,
    }
}
