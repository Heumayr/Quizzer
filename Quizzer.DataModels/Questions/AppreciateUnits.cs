using Quizzer.DataModels.Enumerations;
using System.ComponentModel;
using System.Reflection;

namespace Quizzer.DataModels.Questions
{
    /// <summary>
    /// Beschreibt eine Einheit: zu welcher Art von Groesse sie gehoert und wie sie sich in die
    /// Basiseinheit dieser Art umrechnet.
    /// </summary>
    /// <param name="Unit">Die Einheit.</param>
    /// <param name="Kind">Die Groesse, zu der sie gehoert.</param>
    /// <param name="FactorToBase">Faktor auf die Basiseinheit.</param>
    /// <param name="OffsetToBase">
    /// Versatz, der <em>vor</em> dem Faktor addiert wird: <c>basis = (wert + versatz) * faktor</c>.
    /// Nur die Temperaturen brauchen ihn - Kelvin und Grad Fahrenheit haben einen anderen
    /// Nullpunkt als Grad Celsius, ein blosser Faktor waere dort schlicht falsch.
    /// </param>
    /// <param name="Convertible">
    /// Ob sich die Einheit sinnvoll in andere Einheiten derselben Groesse umrechnen laesst.
    /// Fuer Waehrungen <c>false</c>: zwischen Euro und Dollar gibt es keinen festen Kurs.
    /// </param>
    public sealed record AppreciateUnitInfo(
        AppreciateUnit Unit,
        AppreciateValueKind Kind,
        double FactorToBase,
        double OffsetToBase = 0,
        bool Convertible = true)
    {
        /// <summary>Das Einheitenzeichen, wie es der Spielleiter und die Spieler sehen.</summary>
        public string Symbol => Describe(Unit);

        private static string Describe(AppreciateUnit unit)
            => typeof(AppreciateUnit).GetField(unit.ToString())
                ?.GetCustomAttribute<DescriptionAttribute>()?.Description ?? unit.ToString();
    }

    /// <summary>
    /// Die Einheitentabelle der Schaetzfrage. Einzige Stelle, an der steht, welche Einheit zu
    /// welcher Groesse gehoert und wie sie umgerechnet wird.
    /// <para>
    /// <b>Die Reihenfolge je Groesse ist bedeutsam:</b> die erste Einheit ist die Vorauswahl,
    /// die der Editor beim Wechsel der Art einsetzt. Deshalb steht je Groesse die im Quiz
    /// gebraeuchlichste Einheit vorn, nicht die kleinste oder die SI-Basiseinheit.
    /// </para>
    /// </summary>
    public static class AppreciateUnits
    {
        private static readonly AppreciateUnitInfo[] Table =
        [
            new(AppreciateUnit.Stueck,     AppreciateValueKind.Number,   1),
            new(AppreciateUnit.Prozent,    AppreciateValueKind.Percent,  1),

            // Laenge - Basiseinheit Meter
            new(AppreciateUnit.Meter,      AppreciateValueKind.Length,   1),
            new(AppreciateUnit.Kilometer,  AppreciateValueKind.Length,   1000),
            new(AppreciateUnit.Zentimeter, AppreciateValueKind.Length,   0.01),
            new(AppreciateUnit.Millimeter, AppreciateValueKind.Length,   0.001),
            new(AppreciateUnit.Meile,      AppreciateValueKind.Length,   1609.344),
            new(AppreciateUnit.Seemeile,   AppreciateValueKind.Length,   1852),
            new(AppreciateUnit.Fuss,       AppreciateValueKind.Length,   0.3048),
            new(AppreciateUnit.Zoll,       AppreciateValueKind.Length,   0.0254),
            new(AppreciateUnit.Lichtjahr,  AppreciateValueKind.Length,   9_460_730_472_580_800d),

            // Flaeche - Basiseinheit Quadratmeter
            new(AppreciateUnit.Quadratmeter,      AppreciateValueKind.Area, 1),
            new(AppreciateUnit.Quadratkilometer,  AppreciateValueKind.Area, 1_000_000),
            new(AppreciateUnit.Hektar,            AppreciateValueKind.Area, 10_000),
            new(AppreciateUnit.Quadratzentimeter, AppreciateValueKind.Area, 0.0001),

            // Masse - Basiseinheit Gramm
            new(AppreciateUnit.Kilogramm,  AppreciateValueKind.Mass,     1000),
            new(AppreciateUnit.Gramm,      AppreciateValueKind.Mass,     1),
            new(AppreciateUnit.Tonne,      AppreciateValueKind.Mass,     1_000_000),
            new(AppreciateUnit.Milligramm, AppreciateValueKind.Mass,     0.001),
            new(AppreciateUnit.Karat,      AppreciateValueKind.Mass,     0.2),

            // Volumen - Basiseinheit Liter
            new(AppreciateUnit.Liter,      AppreciateValueKind.Volume,   1),
            new(AppreciateUnit.Milliliter, AppreciateValueKind.Volume,   0.001),
            new(AppreciateUnit.Kubikmeter, AppreciateValueKind.Volume,   1000),
            new(AppreciateUnit.Hektoliter, AppreciateValueKind.Volume,   100),
            new(AppreciateUnit.Zentiliter, AppreciateValueKind.Volume,   0.01),

            // Dauer - Basiseinheit Sekunde. Jahr und Monat sind Mittelwerte
            // (365 Tage, 30 Tage); fuer eine Schaetzfrage genau genug.
            new(AppreciateUnit.Jahre,         AppreciateValueKind.Duration, 365d * 24 * 3600),
            new(AppreciateUnit.Monate,        AppreciateValueKind.Duration, 30d * 24 * 3600),
            new(AppreciateUnit.Wochen,        AppreciateValueKind.Duration, 7d * 24 * 3600),
            new(AppreciateUnit.Tage,          AppreciateValueKind.Duration, 24d * 3600),
            new(AppreciateUnit.Stunden,       AppreciateValueKind.Duration, 3600),
            new(AppreciateUnit.Minuten,       AppreciateValueKind.Duration, 60),
            new(AppreciateUnit.Sekunden,      AppreciateValueKind.Duration, 1),
            new(AppreciateUnit.Millisekunden, AppreciateValueKind.Duration, 0.001),

            // Datum - Basiseinheit Tag
            new(AppreciateUnit.Datum,      AppreciateValueKind.Date,     1),

            // Geldbetrag - kein fester Kurs zwischen den Waehrungen, daher nicht umrechenbar.
            // Der Faktor 1 gilt nur innerhalb derselben Waehrung.
            new(AppreciateUnit.Euro,      AppreciateValueKind.Money, 1, 0, Convertible: false),
            new(AppreciateUnit.Dollar,    AppreciateValueKind.Money, 1, 0, Convertible: false),
            new(AppreciateUnit.Franken,   AppreciateValueKind.Money, 1, 0, Convertible: false),
            new(AppreciateUnit.Schilling, AppreciateValueKind.Money, 1, 0, Convertible: false),

            // Temperatur - Basiseinheit Grad Celsius, mit Versatz statt blossem Faktor
            new(AppreciateUnit.GradCelsius,    AppreciateValueKind.Temperature, 1),
            new(AppreciateUnit.Kelvin,         AppreciateValueKind.Temperature, 1, -273.15),
            new(AppreciateUnit.GradFahrenheit, AppreciateValueKind.Temperature, 5d / 9d, -32),

            // Geschwindigkeit - Basiseinheit Meter pro Sekunde
            new(AppreciateUnit.KilometerProStunde, AppreciateValueKind.Speed, 1d / 3.6),
            new(AppreciateUnit.MeterProSekunde,    AppreciateValueKind.Speed, 1),
            new(AppreciateUnit.Knoten,             AppreciateValueKind.Speed, 1852d / 3600),
            new(AppreciateUnit.MeilenProStunde,    AppreciateValueKind.Speed, 1609.344 / 3600),

            // Leistung - Basiseinheit Watt
            new(AppreciateUnit.Pferdestaerken, AppreciateValueKind.Power, 735.49875),
            new(AppreciateUnit.Kilowatt,       AppreciateValueKind.Power, 1000),
            new(AppreciateUnit.Watt,           AppreciateValueKind.Power, 1),
            new(AppreciateUnit.Megawatt,       AppreciateValueKind.Power, 1_000_000),

            // Energie - Basiseinheit Kilojoule
            new(AppreciateUnit.Kilokalorien,     AppreciateValueKind.Energy, 4.184),
            new(AppreciateUnit.Kilojoule,        AppreciateValueKind.Energy, 1),
            new(AppreciateUnit.Kilowattstunden,  AppreciateValueKind.Energy, 3600),
            new(AppreciateUnit.Megajoule,        AppreciateValueKind.Energy, 1000),

            // Datenmenge - Basiseinheit Megabyte, dezimal gerechnet (1000, nicht 1024),
            // so wie Hersteller und Datentarife es angeben.
            new(AppreciateUnit.Gigabyte, AppreciateValueKind.DataVolume, 1000),
            new(AppreciateUnit.Megabyte, AppreciateValueKind.DataVolume, 1),
            new(AppreciateUnit.Terabyte, AppreciateValueKind.DataVolume, 1_000_000),
            new(AppreciateUnit.Kilobyte, AppreciateValueKind.DataVolume, 0.001),
        ];

        /// <summary>Alle Einheiten, in der Reihenfolge der Tabelle.</summary>
        public static IReadOnlyList<AppreciateUnitInfo> All => Table;

        /// <summary>
        /// Die Einheiten dieser Groesse. Die erste ist die Vorauswahl beim Wechsel der Art.
        /// </summary>
        public static IReadOnlyList<AppreciateUnitInfo> For(AppreciateValueKind kind)
            => Table.Where(u => u.Kind == kind).ToList();

        /// <summary>Die Beschreibung einer einzelnen Einheit.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Bei einer unbekannten Einheit.</exception>
        public static AppreciateUnitInfo Info(AppreciateUnit unit)
            => Table.FirstOrDefault(u => u.Unit == unit)
               ?? throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unbekannte Einheit.");

        /// <summary>Die Vorauswahl einer Groesse - die erste Einheit ihrer Liste.</summary>
        public static AppreciateUnit DefaultUnitFor(AppreciateValueKind kind)
            => For(kind).FirstOrDefault()?.Unit ?? AppreciateUnit.Stueck;

        /// <summary>Ob die Einheit zu dieser Groesse passt.</summary>
        public static bool Matches(AppreciateValueKind kind, AppreciateUnit unit)
            => Table.Any(u => u.Kind == kind && u.Unit == unit);

        /// <summary>
        /// Rechnet einen Wert in die Basiseinheit seiner Groesse um, damit sich Werte
        /// vergleichen lassen.
        /// </summary>
        public static double ToBase(double value, AppreciateUnit unit)
        {
            var info = Info(unit);
            return (value + info.OffsetToBase) * info.FactorToBase;
        }

        /// <summary>
        /// Rechnet einen Wert aus der Basiseinheit zurueck in die angegebene Einheit.
        /// Gegenstueck zu <see cref="ToBase"/>.
        /// </summary>
        public static double FromBase(double baseValue, AppreciateUnit unit)
        {
            var info = Info(unit);
            return baseValue / info.FactorToBase - info.OffsetToBase;
        }

        /// <summary>
        /// Rechnet eine <em>Differenz</em> aus der Basiseinheit in die angegebene Einheit.
        /// Anders als bei einem Messwert faellt der Versatz dabei weg: der Abstand zwischen
        /// zwei Temperaturen ist derselbe, egal ob von Celsius oder Kelvin aus gemessen.
        /// Wird gebraucht, um dem Spielleiter den Abstand in <em>seiner</em> Einheit zu zeigen
        /// statt in der Basiseinheit.
        /// </summary>
        public static double DifferenceFromBase(double baseDifference, AppreciateUnit unit)
            => baseDifference / Info(unit).FactorToBase;
    }
}
