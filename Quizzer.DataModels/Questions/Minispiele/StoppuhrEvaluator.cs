using System.Globalization;

namespace Quizzer.DataModels.Questions.Minispiele
{
    /// <summary>Wo ein Spieler die Stoppuhr angehalten hat.</summary>
    /// <param name="Millisekunden">Gemessen am Telefon vom Start bis zum Stopp.</param>
    /// <param name="Gewertet">Ob die Zeit zaehlt - ueber <see cref="StoppuhrEvaluator.MaxMs"/> nicht.</param>
    public sealed record StoppuhrVersuch(int Millisekunden, bool Gewertet);

    /// <summary>Was die Stoppuhr ergeben hat.</summary>
    /// <param name="Rangfolge">Die Abstandswertung, dieselbe wie bei der Schaetzfrage.</param>
    /// <param name="Abgaben">Je Spieler sein Versuch - wer fehlt, hat nichts abgegeben.</param>
    public sealed record StoppuhrErgebnis(AppreciateOutcome Rangfolge, IReadOnlyDictionary<Guid, StoppuhrVersuch> Abgaben)
    {
        public bool IstGewinner(Guid spieler) => Rangfolge.WinnerIds.Contains(spieler);

        public StoppuhrVersuch? VersuchVon(Guid spieler) => Abgaben.GetValueOrDefault(spieler);

        /// <summary>Der Abstand zur Zielzeit; <c>null</c> ohne gewertete Zeit.</summary>
        public int? AbstandVon(Guid spieler)
            => Rangfolge.Guesses.FirstOrDefault(g => g.PlayerId == spieler)?.Distance is { } abstand
                ? (int)Math.Round(abstand)
                : null;
    }

    /// <summary>
    /// Die Stoppuhr (M35 C1): Grenzen, Einordnung und Wertung. Die Zeit misst das Telefon; gewertet wird
    /// mit der Abstandswertung der Schaetzfrage (<see cref="AppreciateEvaluator.Rank"/>), nicht nachgebaut.
    /// <b>Nur Vorschlaege, nie Minus</b> (E1).
    /// </summary>
    public static class StoppuhrEvaluator
    {
        /// <summary>So lange ist die Uhr am Telefon zu sehen, danach laeuft sie verdeckt.</summary>
        public const int VisibleMs = 3000;

        private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

        /// <summary>Ueber der doppelten Zielzeit keine Wertung - das Telefon gibt dann selbst auf.</summary>
        public static int MaxMs(int zielSekunden) => Math.Max(0, zielSekunden) * 2000;

        /// <summary>Ordnet eine Abgabe ein. <c>null</c> heisst: unbrauchbar (fehlt oder negativ) - verwerfen.</summary>
        public static StoppuhrVersuch? Classify(int? millisekunden, int zielSekunden)
            => millisekunden is not { } ms || ms < 0 ? null : new StoppuhrVersuch(ms, ms <= MaxMs(zielSekunden));

        /// <summary>Kleinster Abstand zur Zielzeit gewinnt, bei Gleichstand alle; zu spaet bleibt ausser Wertung.</summary>
        public static StoppuhrErgebnis Werte(IReadOnlyDictionary<Guid, StoppuhrVersuch> abgaben, int zielSekunden)
        {
            ArgumentNullException.ThrowIfNull(abgaben);

            var werte = abgaben.Select(a => (a.Key, Describe(a.Value), a.Value.Gewertet ? (double?)a.Value.Millisekunden : null));

            return new StoppuhrErgebnis(AppreciateEvaluator.Rank(werte, zielSekunden * 1000.0), abgaben);
        }

        /// <summary>„10,23 s".</summary>
        public static string Sekunden(int millisekunden)
            => (millisekunden / 1000.0).ToString("0.00", Deutsch) + " s";

        /// <summary>Ein Versuch im Klartext - fuer Beamer, Spielerkarte und Uebersicht dieselben Worte.</summary>
        public static string Describe(StoppuhrVersuch? versuch) => versuch switch
        {
            null => "keine Abgabe",
            { Gewertet: true } => Sekunden(versuch.Millisekunden),
            _ => "zu spät",
        };

        /// <summary>„genau getroffen" oder „0,23 s daneben".</summary>
        public static string DescribeDistance(int abstandMs)
            => abstandMs == 0 ? "genau getroffen" : $"{Sekunden(abstandMs)} daneben";
    }
}
