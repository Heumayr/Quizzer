using System.ComponentModel;

namespace Quizzer.DataModels.Enumerations
{
    /// <summary>
    /// Die Einheit, in der ein Schaetzwert angegeben wird. Jede Einheit gehoert zu genau einem
    /// <see cref="AppreciateValueKind"/>; welche zu welcher und wie sie umgerechnet wird, steht
    /// in <c>Quizzer.DataModels.Questions.AppreciateUnits</c>.
    /// <para>
    /// Die Zahlenwerte sind in der Datenbank gespeichert und duerfen sich nicht aendern. Je
    /// Groesse ist ein Zehnerblock reserviert; neue Einheiten bekommen die naechste freie
    /// Nummer im passenden Block.
    /// </para>
    /// </summary>
    public enum AppreciateUnit
    {
        // ── Zahl ─────────────────────────────────────────────────────────────
        /// <summary>Blosse Anzahl, ohne Einheitenzeichen.</summary>
        [Description("")]
        Stueck = 0,

        // ── Prozent ──────────────────────────────────────────────────────────
        [Description("%")]
        Prozent = 10,

        // ── Laenge (Basis: Meter) ────────────────────────────────────────────
        [Description("km")]
        Kilometer = 20,

        [Description("m")]
        Meter = 21,

        [Description("cm")]
        Zentimeter = 22,

        [Description("mm")]
        Millimeter = 23,

        [Description("Meilen")]
        Meile = 24,

        [Description("Seemeilen")]
        Seemeile = 25,

        [Description("Fuss")]
        Fuss = 26,

        [Description("Zoll")]
        Zoll = 27,

        [Description("Lichtjahre")]
        Lichtjahr = 28,

        // ── Masse (Basis: Gramm) ─────────────────────────────────────────────
        [Description("t")]
        Tonne = 30,

        [Description("kg")]
        Kilogramm = 31,

        [Description("g")]
        Gramm = 32,

        [Description("mg")]
        Milligramm = 33,

        [Description("Karat")]
        Karat = 34,

        // ── Volumen (Basis: Liter) ───────────────────────────────────────────
        [Description("l")]
        Liter = 40,

        [Description("ml")]
        Milliliter = 41,

        [Description("m3")]
        Kubikmeter = 42,

        [Description("hl")]
        Hektoliter = 43,

        [Description("cl")]
        Zentiliter = 44,

        // ── Dauer (Basis: Sekunde) ───────────────────────────────────────────
        [Description("Jahre")]
        Jahre = 50,

        [Description("Tage")]
        Tage = 51,

        [Description("Stunden")]
        Stunden = 52,

        [Description("Minuten")]
        Minuten = 53,

        [Description("Sekunden")]
        Sekunden = 54,

        [Description("Wochen")]
        Wochen = 55,

        [Description("Monate")]
        Monate = 56,

        [Description("ms")]
        Millisekunden = 57,

        // ── Datum (Basis: Tag) ───────────────────────────────────────────────
        /// <summary>Kalenderdatum. Der Abstand wird in Tagen gerechnet.</summary>
        [Description("Datum")]
        Datum = 60,

        // ── Geldbetrag (nicht umrechenbar, siehe AppreciateUnits) ────────────
        [Description("Euro")]
        Euro = 70,

        [Description("Dollar")]
        Dollar = 71,

        [Description("Franken")]
        Franken = 72,

        [Description("Schilling")]
        Schilling = 73,

        // ── Flaeche (Basis: Quadratmeter) ────────────────────────────────────
        [Description("m2")]
        Quadratmeter = 80,

        [Description("km2")]
        Quadratkilometer = 81,

        [Description("ha")]
        Hektar = 82,

        [Description("cm2")]
        Quadratzentimeter = 83,

        // ── Temperatur (Basis: Grad Celsius) ─────────────────────────────────
        [Description("Grad C")]
        GradCelsius = 90,

        [Description("Kelvin")]
        Kelvin = 91,

        [Description("Grad F")]
        GradFahrenheit = 92,

        // ── Geschwindigkeit (Basis: Meter pro Sekunde) ───────────────────────
        [Description("km/h")]
        KilometerProStunde = 100,

        [Description("m/s")]
        MeterProSekunde = 101,

        [Description("Knoten")]
        Knoten = 102,

        [Description("mph")]
        MeilenProStunde = 103,

        // ── Leistung (Basis: Watt) ───────────────────────────────────────────
        [Description("PS")]
        Pferdestaerken = 110,

        [Description("kW")]
        Kilowatt = 111,

        [Description("W")]
        Watt = 112,

        [Description("MW")]
        Megawatt = 113,

        // ── Energie (Basis: Kilojoule) ───────────────────────────────────────
        [Description("kcal")]
        Kilokalorien = 120,

        [Description("kJ")]
        Kilojoule = 121,

        [Description("kWh")]
        Kilowattstunden = 122,

        [Description("MJ")]
        Megajoule = 123,

        // ── Datenmenge (Basis: Megabyte, dezimal) ────────────────────────────
        [Description("GB")]
        Gigabyte = 130,

        [Description("MB")]
        Megabyte = 131,

        [Description("TB")]
        Terabyte = 132,

        [Description("KB")]
        Kilobyte = 133,
    }
}
