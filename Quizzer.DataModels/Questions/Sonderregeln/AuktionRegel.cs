using Quizzer.DataModels.Questions.Minispiele;

namespace Quizzer.DataModels.Questions.Sonderregeln
{
    /// <summary>Ein Gebot in der Auktion: Betrag und Eingang am Server - bei Gleichstand gewinnt das fruehere.</summary>
    /// <param name="Betrag">0 heisst passen.</param>
    /// <param name="Eingang">Zaehler am Server, vergeben unter seiner Sperre - kleiner ist frueher.</param>
    public readonly record struct Gebot(int Betrag, int Eingang);

    /// <summary>Wer die Auktion gewonnen hat, was er bot und was er zahlt.</summary>
    public sealed record Zuschlag(Guid Gewinner, int Gebot, int Preis);

    /// <summary>
    /// Sonderregel Auktion (M35 C3b): geheime Gebote bis max(Kontostand, Feldpunkte) wie beim Einsatz. Das hoechste
    /// Gebot ueber 0 gewinnt, bei Gleichstand das fruehere; nur der Gewinner antwortet. Er zahlt immer (Minus-Vorschlag,
    /// E1); richtig oder ein Teilwert bringt den doppelten Grundwert, falsch kostet nur den Preis.
    /// </summary>
    public static class AuktionRegel
    {
        /// <summary>Spielweise Zweitpreis: der Gewinner zahlt das zweithoechste Gebot.</summary>
        public const int ZweitpreisSpielweise = 1;

        /// <summary>Hoechstens so viel darf ein Spieler bieten - dieselbe Grenze wie beim Einsatz.</summary>
        public static bool Zulaessig(int gebot, int kontostand, int feldpunkte) => EinsatzRegel.Zulaessig(gebot, kontostand, feldpunkte);

        /// <summary>Der Zuschlag; <c>null</c>, wenn niemand mehr als 0 bietet - dann spielt die Frage normal.</summary>
        /// <param name="gebote">Je Spieler sein Gebot; wer fehlt, hat nicht geboten.</param>
        /// <param name="zweitpreis">Zweitpreis: der Gewinner zahlt das zweithoechste Gebot, bei nur einem Bieter 0.</param>
        public static Zuschlag? Ermittle(IReadOnlyDictionary<Guid, Gebot> gebote, bool zweitpreis)
        {
            ArgumentNullException.ThrowIfNull(gebote);

            var bieter = gebote.Where(e => e.Value.Betrag > 0)
                .OrderByDescending(e => e.Value.Betrag)
                .ThenBy(e => e.Value.Eingang)
                .ToList();

            if (bieter.Count == 0)
                return null;

            var sieger = bieter[0];
            var preis = !zweitpreis ? sieger.Value.Betrag
                : bieter.Count > 1 ? bieter[1].Value.Betrag
                : 0;

            return new(sieger.Key, sieger.Value.Betrag, preis);
        }

        /// <summary>Der Vorschlag fuer den Gewinner - auch unbewertet zahlt er den Preis.</summary>
        public static Punktevorschlag Werte(int preis, SonderregelEingang eingang)
        {
            ArgumentNullException.ThrowIfNull(eingang);

            var gewinn = eingang.Bewertung is RatingKind.Right or RatingKind.Partial ? 2 * eingang.Grundwert : 0;
            var grund = gewinn != 0 ? $"Auktion: doppelt {Punkteanteil.Text(gewinn)}, Preis {preis}" : $"Auktion: Preis {preis}";

            return Sonderregelwertung.Gekappt(gewinn - preis, grund, eingang.Konto);
        }
    }
}
