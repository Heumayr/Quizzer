using System.ComponentModel;

namespace Quizzer.DataModels.Enumerations
{
    /// <summary>
    /// Die Einheit, in der ein Schaetzwert angegeben wird. Jede Einheit gehoert zu genau einem
    /// <see cref="AppreciateValueKind"/>; welche zu welchem, steht in
    /// <c>Quizzer.DataModels.Questions.AppreciateUnits</c>.
    /// </summary>
    public enum AppreciateUnit
    {
        /// <summary>Blosse Anzahl, ohne Einheitenzeichen.</summary>
        [Description("")]
        Stueck = 0,

        [Description("%")]
        Prozent = 10,

        [Description("km")]
        Kilometer = 20,

        [Description("m")]
        Meter = 21,

        [Description("cm")]
        Zentimeter = 22,

        [Description("mm")]
        Millimeter = 23,

        [Description("t")]
        Tonne = 30,

        [Description("kg")]
        Kilogramm = 31,

        [Description("g")]
        Gramm = 32,

        [Description("l")]
        Liter = 40,

        [Description("ml")]
        Milliliter = 41,

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

        /// <summary>Kalenderdatum. Der Abstand wird in Tagen gerechnet.</summary>
        [Description("Datum")]
        Datum = 60,

        [Description("Euro")]
        Euro = 70,
    }
}
