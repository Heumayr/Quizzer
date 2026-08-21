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
    /// <param name="FactorToBase">Faktor auf die Basiseinheit (Meter, Gramm, Liter, Sekunde).</param>
    public sealed record AppreciateUnitInfo(
        AppreciateUnit Unit,
        AppreciateValueKind Kind,
        double FactorToBase)
    {
        /// <summary>Das Einheitenzeichen, wie es der Spieler sieht.</summary>
        public string Symbol => Describe(Unit);

        private static string Describe(AppreciateUnit unit)
            => typeof(AppreciateUnit).GetField(unit.ToString())
                ?.GetCustomAttribute<DescriptionAttribute>()?.Description ?? unit.ToString();
    }

    /// <summary>
    /// Die Einheitentabelle der Schaetzfrage. Einzige Stelle, an der steht, welche Einheit zu
    /// welcher Groesse gehoert und wie sie umgerechnet wird.
    /// </summary>
    public static class AppreciateUnits
    {
        private static readonly AppreciateUnitInfo[] Table =
        [
            new(AppreciateUnit.Stueck,     AppreciateValueKind.Number,   1),
            new(AppreciateUnit.Prozent,    AppreciateValueKind.Percent,  1),

            // Basiseinheit Meter
            new(AppreciateUnit.Kilometer,  AppreciateValueKind.Length,   1000),
            new(AppreciateUnit.Meter,      AppreciateValueKind.Length,   1),
            new(AppreciateUnit.Zentimeter, AppreciateValueKind.Length,   0.01),
            new(AppreciateUnit.Millimeter, AppreciateValueKind.Length,   0.001),

            // Basiseinheit Gramm
            new(AppreciateUnit.Tonne,      AppreciateValueKind.Mass,     1_000_000),
            new(AppreciateUnit.Kilogramm,  AppreciateValueKind.Mass,     1000),
            new(AppreciateUnit.Gramm,      AppreciateValueKind.Mass,     1),

            // Basiseinheit Liter
            new(AppreciateUnit.Liter,      AppreciateValueKind.Volume,   1),
            new(AppreciateUnit.Milliliter, AppreciateValueKind.Volume,   0.001),

            // Basiseinheit Sekunde
            new(AppreciateUnit.Jahre,      AppreciateValueKind.Duration, 365d * 24 * 3600),
            new(AppreciateUnit.Tage,       AppreciateValueKind.Duration, 24d * 3600),
            new(AppreciateUnit.Stunden,    AppreciateValueKind.Duration, 3600),
            new(AppreciateUnit.Minuten,    AppreciateValueKind.Duration, 60),
            new(AppreciateUnit.Sekunden,   AppreciateValueKind.Duration, 1),

            // Basiseinheit Tag
            new(AppreciateUnit.Datum,      AppreciateValueKind.Date,     1),

            new(AppreciateUnit.Euro,       AppreciateValueKind.Money,    1),
        ];

        /// <summary>Alle Einheiten.</summary>
        public static IReadOnlyList<AppreciateUnitInfo> All => Table;

        /// <summary>Die Einheiten, die zu dieser Groesse gehoeren.</summary>
        public static IReadOnlyList<AppreciateUnitInfo> For(AppreciateValueKind kind)
            => Table.Where(u => u.Kind == kind).ToList();

        /// <summary>Die Beschreibung einer einzelnen Einheit.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Bei einer unbekannten Einheit.</exception>
        public static AppreciateUnitInfo Info(AppreciateUnit unit)
            => Table.FirstOrDefault(u => u.Unit == unit)
               ?? throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unbekannte Einheit.");

        /// <summary>Die uebliche Einheit einer Groesse - die Vorauswahl im Editor.</summary>
        public static AppreciateUnit DefaultUnitFor(AppreciateValueKind kind)
            => For(kind).FirstOrDefault()?.Unit ?? AppreciateUnit.Stueck;

        /// <summary>Ob die Einheit zu dieser Groesse passt.</summary>
        public static bool Matches(AppreciateValueKind kind, AppreciateUnit unit)
            => Table.Any(u => u.Kind == kind && u.Unit == unit);

        /// <summary>
        /// Rechnet einen Wert in die Basiseinheit seiner Groesse um, damit sich Tipps in
        /// verschiedenen Einheiten vergleichen lassen.
        /// </summary>
        public static double ToBase(double value, AppreciateUnit unit)
            => value * Info(unit).FactorToBase;
    }
}
