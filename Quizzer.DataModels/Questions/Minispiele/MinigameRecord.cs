using System.Globalization;

namespace Quizzer.DataModels.Questions.Minispiele
{
    /// <summary>
    /// Was der Rohwert am Ergebnis (<c>QuestionResult.RecordValue</c>) bei einer Art bedeutet: Titel, Einheit, Richtung
    /// und plausibler Bereich (M35 C9). Steht am Profil (<see cref="MinigameProfile.Record"/>), nicht an der Zeile.
    /// </summary>
    public sealed class MinigameRecord
    {
        /// <summary>Wie die Bestenliste die Art ueberschreibt, etwa „Schnellste Reaktion".</summary>
        public required string Title { get; init; }

        /// <summary>Die Einheit, in der die Liste den Wert zeigt.</summary>
        public required string Unit { get; init; }

        /// <summary>Ob der kleinere Wert besser ist - Reaktionszeit, Abweichung der Stoppuhr.</summary>
        public required bool LowerIsBetter { get; init; }

        /// <summary>Kleinster plausibler Rohwert - darunter weist der Server die Zeile ab.</summary>
        public required int Min { get; init; }

        /// <summary>Groesster plausibler Rohwert - darueber weist der Server die Zeile ab.</summary>
        public required int Max { get; init; }

        /// <summary>Der Rohwert im Klartext mit Einheit - Server und Fenster zeigen dieselben Worte.</summary>
        public required Func<int, string> Format { get; init; }

        /// <summary>
        /// Wo die Werte der Spielweisen nicht vergleichbar sind, je Spielweise eine eigene Liste (M37 E05, Kopfrechnen je
        /// Schwierigkeit): ihr Kurzname fuer den Titel, Index wie <see cref="MinigameProfile.Variants"/>. Leer: eine Liste.
        /// </summary>
        public IReadOnlyList<string> VariantTitles { get; init; } = [];

        public bool IsPlausible(int value) => value >= Min && value <= Max;
    }

    /// <summary>
    /// Die Bestwerte der Arten, die einen Messwert der Runde haben (M35 C9). Eigene Klasse statt Feldern in
    /// <see cref="MinigameProfiles"/>: deren Teildateien legen keine Reihenfolge der Initialisierer fest.
    /// <para>Grenzen kommen aus den Bewertern - ein Wert, den die Runde nie werten wuerde, ist kein Rekord.</para>
    /// </summary>
    public static class MinigameRecords
    {
        private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

        /// <summary>Hundertstel als „7,35".</summary>
        public static string Hundertstel(int wert) => (wert / 100.0).ToString("0.00", Deutsch);

        /// <summary>Die beste gueltige Einzelzeit in ms; unter der Fruehstartgrenze und ueber der Wertungsgrenze nichts.</summary>
        public static MinigameRecord Reaktion { get; } = new()
        {
            Title = "Schnellste Reaktion",
            Unit = "ms",
            LowerIsBetter = true,
            Min = ReaktionEvaluator.FalseStartBelowMs,
            Max = ReaktionEvaluator.NoScoreAboveMs,
            Format = ms => $"{ms} ms",
        };

        /// <summary>Tipps je Sekunde in Hundertsteln - die Dauer ist einstellbar, die Zahl der Tipps allein waere kein Vergleich.</summary>
        public static MinigameRecord Tippsprint { get; } = new()
        {
            Title = "Schnellste Finger",
            Unit = "Tipps je Sekunde",
            LowerIsBetter = false,
            Min = 0,
            Max = TippsprintEvaluator.MaxTapsPerSecond * 100,
            Format = wert => $"{Hundertstel(wert)} Tipps/s",
        };

        /// <summary>Der Abstand zur Zielzeit in ms; gewertet wird bis zur doppelten Zielzeit, der Abstand ist also hoechstens die Zielzeit.</summary>
        public static MinigameRecord Stoppuhr { get; } = new()
        {
            Title = "Genaueste Stoppuhr",
            Unit = "ms daneben",
            LowerIsBetter = true,
            Min = 0,
            Max = MinigameProfiles.StopwatchMaxSeconds * 1000,
            Format = StoppuhrEvaluator.DescribeDistance,
        };

        /// <summary>Richtige Antworten je Minute in Hundertsteln - aus demselben Grund wie beim Tippsprint.</summary>
        public static MinigameRecord Kopfrechnen { get; } = new()
        {
            Title = "Schnellster Rechner",
            Unit = "richtige je Minute",
            LowerIsBetter = false,
            Min = 0,
            Max = KopfrechnenEvaluator.MaxAntwortenJeSekunde * 60 * 100,
            Format = wert => $"{Hundertstel(wert)} richtige/min",

            // Bis 20 rechnet man schneller als bis 1000 - eine gemeinsame Liste vergliche das nicht (F-M35-15).
            VariantTitles = ["leicht", "mittel", "schwer"],
        };

        /// <summary>Der Multiplikator beim Ausstieg in Hundertsteln, gemessen an der Serveruhr.</summary>
        public static MinigameRecord Zocker { get; } = new()
        {
            Title = "Höchster Ausstieg beim Zocker",
            Unit = "Multiplikator",
            LowerIsBetter = false,
            Min = ZockerEvaluator.Startwert,
            Max = ZockerEvaluator.Obergrenze,
            Format = ZockerEvaluator.Text,
        };
    }
}
