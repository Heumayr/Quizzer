using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.QuestionTypes;
using System.Globalization;

namespace Quizzer.DataModels.Questions
{
    /// <summary>Ein einzelner Tipp samt seinem Abstand zum Sollwert.</summary>
    /// <param name="PlayerId">Der Spieler, von dem der Tipp stammt.</param>
    /// <param name="RawInput">Was der Spieler eingetippt hat, unveraendert.</param>
    /// <param name="Value">Der gelesene Wert in der Basiseinheit, oder <c>null</c> wenn unlesbar.</param>
    /// <param name="Distance">Betrag des Abstands zum Sollwert, oder <c>null</c> wenn unlesbar.</param>
    /// <param name="IsWinner">Ob dieser Tipp am naechsten dran liegt.</param>
    public sealed record AppreciateGuess(
        Guid PlayerId,
        string RawInput,
        double? Value,
        double? Distance,
        bool IsWinner)
    {
        /// <summary>Ob sich der Tipp ueberhaupt als Zahl beziehungsweise Datum lesen liess.</summary>
        public bool IsValid => Value.HasValue;
    }

    /// <summary>Das Ergebnis einer Schaetzrunde.</summary>
    /// <param name="Guesses">Alle Tipps, nach Abstand aufsteigend; unlesbare zuletzt.</param>
    /// <param name="WinnerIds">Wer gewonnen hat - bei Gleichstand mehrere.</param>
    public sealed record AppreciateOutcome(
        IReadOnlyList<AppreciateGuess> Guesses,
        IReadOnlyList<Guid> WinnerIds)
    {
        /// <summary>Ob ueberhaupt jemand einen lesbaren Tipp abgegeben hat.</summary>
        public bool HasWinner => WinnerIds.Count > 0;
    }

    /// <summary>
    /// Wertet eine Schaetzfrage aus: liest die Tipps, rechnet sie auf die Basiseinheit um und
    /// bestimmt, wer am naechsten dran liegt.
    /// <para>
    /// Rein rechnend - keine Oberflaeche, keine Datenbank, kein Zufall. Der Spielleiter kann
    /// jede Bewertung nachtraeglich von Hand aendern; dies hier ist ein Vorschlag, keine Sperre.
    /// </para>
    /// </summary>
    public static class AppreciateEvaluator
    {
        /// <summary>
        /// Liest einen eingetippten Wert und rechnet ihn in die Basiseinheit der Groesse um.
        /// Komma und Punkt gelten beide als Dezimaltrenner, Tausenderpunkte werden nicht
        /// erwartet. Bei <see cref="AppreciateValueKind.Date"/> wird ein Datum gelesen und in
        /// Tagen ausgedrueckt.
        /// </summary>
        /// <returns>Der Wert in der Basiseinheit, oder <c>null</c> wenn nichts Lesbares dastand.</returns>
        public static double? ParseGuess(string? text, AppreciateValueKind kind, AppreciateUnit unit)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            text = text.Trim();

            if (kind == AppreciateValueKind.Date)
                return ParseDate(text) is { } date ? date.Date.ToOADate() : null;

            if (!TryParseNumber(text, out var number))
                return null;

            return AppreciateUnits.ToBase(number, unit);
        }

        /// <summary>
        /// Bestimmt die Gewinner: kleinster Abstand zum Sollwert. Bei Gleichstand gewinnen alle
        /// Gleichauf-Spieler. Unlesbare Eingaben bleiben ausser Wertung - sie zaehlen nicht als
        /// Abstand null.
        /// </summary>
        /// <param name="question">Die Schaetzfrage mit Sollwert und Einheit.</param>
        /// <param name="guesses">Die Tipps je Spieler, so wie eingetippt.</param>
        public static AppreciateOutcome Evaluate(
            AppreciateQestion question, IReadOnlyDictionary<Guid, string?> guesses)
        {
            ArgumentNullException.ThrowIfNull(question);
            ArgumentNullException.ThrowIfNull(guesses);

            var expected = ExpectedInBaseUnit(question);

            var evaluated = guesses.Select(pair =>
            {
                var value = ParseGuess(pair.Value, question.ValueKind, question.Unit);
                var distance = value.HasValue && expected.HasValue
                    ? Math.Abs(value.Value - expected.Value)
                    : (double?)null;

                return new AppreciateGuess(pair.Key, pair.Value ?? string.Empty, value, distance, false);
            }).ToList();

            var best = evaluated
                .Where(g => g.Distance.HasValue)
                .Select(g => g.Distance!.Value)
                .DefaultIfEmpty(double.NaN)
                .Min();

            var winnerIds = double.IsNaN(best)
                ? new List<Guid>()
                : evaluated.Where(g => g.Distance == best).Select(g => g.PlayerId).ToList();

            var withWinners = evaluated
                .Select(g => g with { IsWinner = winnerIds.Contains(g.PlayerId) })
                .OrderBy(g => g.Distance.HasValue ? 0 : 1)
                .ThenBy(g => g.Distance ?? double.MaxValue)
                .ToList();

            return new AppreciateOutcome(withWinners, winnerIds);
        }

        /// <summary>Der Sollwert der Frage, in der Basiseinheit ihrer Groesse.</summary>
        public static double? ExpectedInBaseUnit(AppreciateQestion question)
        {
            ArgumentNullException.ThrowIfNull(question);

            if (question.ValueKind == AppreciateValueKind.Date)
                return question.ExpectedDate?.Date.ToOADate();

            return AppreciateUnits.ToBase(question.ExpectedValue, question.Unit);
        }

        /// <summary>
        /// Wie der Sollwert dem Spielleiter angezeigt wird - mit Einheit beziehungsweise als Datum.
        /// </summary>
        public static string DescribeExpected(AppreciateQestion question)
        {
            ArgumentNullException.ThrowIfNull(question);

            if (question.ValueKind == AppreciateValueKind.Date)
                return question.ExpectedDate?.ToString("d", CultureInfo.CurrentCulture) ?? "-";

            var symbol = AppreciateUnits.Info(question.Unit).Symbol;

            return string.IsNullOrEmpty(symbol)
                ? question.ExpectedValue.ToString("0.####", CultureInfo.CurrentCulture)
                : $"{question.ExpectedValue.ToString("0.####", CultureInfo.CurrentCulture)} {symbol}";
        }

        /// <summary>
        /// Beschreibt den Abstand eines Tipps zum Sollwert - in der Einheit der Frage, nicht in
        /// der Basiseinheit.
        /// <para>
        /// <see cref="AppreciateGuess.Distance"/> steht in der Basiseinheit, weil danach
        /// sortiert wird. Ungerechnet angezeigt waere das irrefuehrend: bei einer Frage in
        /// Kilometern hiesse "Abstand 100" in Wahrheit 100 Meter.
        /// </para>
        /// </summary>
        public static string DescribeDistance(AppreciateQestion question, AppreciateGuess guess)
        {
            ArgumentNullException.ThrowIfNull(question);
            ArgumentNullException.ThrowIfNull(guess);

            if (!guess.IsValid || guess.Distance == null)
                return "nicht lesbar";

            if (guess.Distance.Value == 0)
                return "genau richtig";

            if (question.ValueKind == AppreciateValueKind.Date)
            {
                var days = Math.Round(guess.Distance.Value);
                return days == 1 ? "1 Tag daneben" : $"{days:0} Tage daneben";
            }

            var inUnit = AppreciateUnits.DifferenceFromBase(guess.Distance.Value, question.Unit);
            var symbol = AppreciateUnits.Info(question.Unit).Symbol;
            var text = inUnit.ToString("0.####", CultureInfo.CurrentCulture);

            return string.IsNullOrEmpty(symbol) ? $"{text} daneben" : $"{text} {symbol} daneben";
        }

        /// <summary>
        /// Die Eingabeart, die der Browser fuer diese Frage anbieten soll. Wird als
        /// <c>inputType</c> an das Eingabe-Layout geschickt.
        /// </summary>
        public static string InputTypeFor(AppreciateValueKind kind)
            => kind == AppreciateValueKind.Date ? "date" : "number";

        /// <summary>Der Platzhaltertext im Eingabefeld des Spielers.</summary>
        public static string PlaceholderFor(AppreciateValueKind kind, AppreciateUnit unit)
        {
            if (kind == AppreciateValueKind.Date)
                return "Datum";

            var symbol = AppreciateUnits.Info(unit).Symbol;

            return string.IsNullOrEmpty(symbol) ? "Zahl" : $"Wert in {symbol}";
        }

        private static bool TryParseNumber(string text, out double number)
        {
            // Der Spieler tippt am Telefon; Komma wie Punkt muessen beide gelten.
            return double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out number)
                || double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number)
                || double.TryParse(text.Replace(',', '.'), NumberStyles.Float,
                       CultureInfo.InvariantCulture, out number);
        }

        private static DateTime? ParseDate(string text)
        {
            // Das Eingabe-Layout schickt ISO (yyyy-MM-dd); von Hand getippt kommt Ortsformat.
            if (DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var iso))
                return iso;

            if (DateTime.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.None, out var local))
                return local;

            if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var inv))
                return inv;

            // Eine blosse Jahreszahl ist bei Schaetzfragen der Regelfall.
            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var year)
                && year is >= 1 and <= 9999)
                return new DateTime(year, 1, 1);

            return null;
        }
    }
}
