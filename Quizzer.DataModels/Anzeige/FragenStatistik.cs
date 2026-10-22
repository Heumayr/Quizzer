using System.Globalization;

namespace Quizzer.DataModels.Anzeige
{
    /// <summary>
    /// Wie sich eine Frage in gespielten Spielen geschlagen hat (V5, 2026-10-22) - damit man beim
    /// Zusammenstellen sieht, was zu leicht oder zu schwer war.
    /// <para>
    /// <b>Gezählt wird nur, was abgeschlossen ist</b> (Zelle erledigt). Eine Zelle legt beim
    /// Öffnen für jeden Mitspieler eine Ergebniszeile an; eine nur geöffnete Frage hätte sonst
    /// lauter „falsch".
    /// </para>
    /// </summary>
    /// <param name="Gespielt">In wie vielen Zellen die Frage abgeschlossen wurde.</param>
    /// <param name="Antworten">Ergebniszeilen dieser Zellen - je Mitspieler eine.</param>
    /// <param name="Richtig">Davon richtig beantwortet.</param>
    /// <param name="Punkte">Alle dort vergebenen Punkte, Abzüge und Korrekturen eingerechnet.</param>
    public sealed record FragenStatistik(int Gespielt, int Antworten, int Richtig, int Punkte)
    {
        public static readonly FragenStatistik Nie = new(0, 0, 0, 0);

        /// <summary>Unter so vielen Antworten sagt eine Quote nichts.</summary>
        public const int MindestAntworten = 3;

        public double? Quote => Antworten == 0 ? null : Richtig / (double)Antworten;

        /// <summary>Zum Sortieren: nie gespielte Fragen ans Ende.</summary>
        public double Sortierwert => Quote ?? -1;

        public string QuoteText => Quote is { } q
            ? $"{Math.Round(q * 100).ToString(CultureInfo.InvariantCulture)} % ({Richtig}/{Antworten})"
            : "–";

        public string GespieltText => Gespielt == 0 ? "nie" : Gespielt == 1 ? "1-mal" : $"{Gespielt}-mal";

        /// <summary>„leicht" ab 80 % richtig, „schwer" bis 20 % - erst ab <see cref="MindestAntworten"/>.</summary>
        public string Einschaetzung => Antworten < MindestAntworten || Quote is not { } q
            ? string.Empty
            : q >= 0.8 ? "leicht" : q <= 0.2 ? "schwer" : string.Empty;
    }
}
