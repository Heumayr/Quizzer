using Quizzer.DataModels.Helpers;
using Quizzer.DataModels.Questions.Minispiele;

namespace Quizzer.DataModels.Questions.Sonderregeln
{
    /// <summary>Wie der Spielleiter die Antwort bewertet hat - die Eingabe jeder Sonderregel.</summary>
    public enum RatingKind
    {
        /// <summary>Nicht bewertet - bei Buzzerfragen jeder ausser dem, der dran war.</summary>
        None = 0,

        Right = 1,

        Wrong = 2,

        /// <summary>Teilwert („eigener Wert") der Typen mit Anteil: Reihenfolge, Zuordnung, Wahr oder falsch.</summary>
        Partial = 3,
    }

    /// <summary>Eine Bewertung, wie sie die Spielerkarte ohne Sonderregel vorschlagen wuerde.</summary>
    /// <param name="Bewertung">Richtig, falsch, Teilwert oder nichts.</param>
    /// <param name="Grundwert">Der Wert ohne Regel, noch ungekappt: richtig +Punkte, falsch −Minuspunkte, sonst der Teilwert.</param>
    /// <param name="Feldpunkte">Was „richtig" in dieser Zelle bringt - Grundlage des Anteils.</param>
    /// <param name="Konto">Kontostand und Spieleinstellung fuer die Kappung (E2).</param>
    public sealed record SonderregelEingang(RatingKind Bewertung, int Grundwert, int Feldpunkte, Kappung Konto);

    /// <summary>Was die Kappung (E2) braucht: der Kontostand eines Spielers und ob das Spiel ein Minus erlaubt.</summary>
    public sealed record Kappung(int Kontostand, bool UnterNullErlaubt)
    {
        public static Kappung OhneGrenze { get; } = new(0, true);
    }

    /// <summary>Gemeinsame Rechnungen der Sonderregeln (M35 C3).</summary>
    public static class Sonderregelwertung
    {
        /// <summary>Kappt ein Minus am Kontostand (E2) und sagt es im Grund.</summary>
        public static Punktevorschlag Gekappt(int punkte, string grund, Kappung konto)
        {
            var gekappt = Punktebewegung.Kappe(punkte, konto.Kontostand, konto.UnterNullErlaubt);

            return new(gekappt, gekappt != punkte ? grund + Punkteanteil.GekapptText : grund);
        }
    }

    /// <summary>
    /// Sonderregel Einsatz (M35 C3): der Einsatz tritt an die Stelle der Feldpunkte. Richtig +Einsatz,
    /// falsch −Einsatz (gekappt nach E2), ein Teilwert anteilig. Nicht bewertet: keine Bewegung.
    /// </summary>
    public static class EinsatzRegel
    {
        /// <summary>Hoechstens so viel darf ein Spieler setzen - am Server geprueft.</summary>
        public static int Obergrenze(int kontostand, int feldpunkte) => Math.Max(0, Math.Max(kontostand, feldpunkte));

        /// <summary>Ob ein Einsatz zulaessig ist: 0 bis zur Obergrenze.</summary>
        public static bool Zulaessig(int einsatz, int kontostand, int feldpunkte)
            => einsatz >= 0 && einsatz <= Obergrenze(kontostand, feldpunkte);

        /// <summary>Der Vorschlag fuer diesen Einsatz; <c>null</c>, wenn nicht bewertet.</summary>
        public static Punktevorschlag? Werte(int einsatz, SonderregelEingang eingang)
        {
            ArgumentNullException.ThrowIfNull(eingang);

            int? punkte = eingang.Bewertung switch
            {
                RatingKind.Right => einsatz,
                RatingKind.Wrong => -einsatz,
                RatingKind.Partial => Anteil(einsatz, eingang.Grundwert, eingang.Feldpunkte),
                _ => null,
            };

            return punkte is { } wert ? Sonderregelwertung.Gekappt(wert, $"Einsatz {einsatz}", eingang.Konto) : null;
        }

        /// <summary>Der Teilwert als Anteil der Feldpunkte, auf den Einsatz uebertragen - kaufmaennisch, hoechstens der Einsatz.</summary>
        public static int Anteil(int einsatz, int teilwert, int feldpunkte)
        {
            if (feldpunkte <= 0)
                return 0;

            var wert = (int)Math.Round((double)einsatz * teilwert / feldpunkte, MidpointRounding.AwayFromZero);

            return Math.Clamp(wert, -einsatz, einsatz);
        }
    }

    /// <summary>
    /// Sonderregel Risiko (M35 C3): wer „doppelt spielen" gewaehlt hat, bekommt Plus und Minus doppelt,
    /// auch einen Teilwert. Wer nein oder nichts gewaehlt hat, spielt wie immer.
    /// </summary>
    public static class RisikoRegel
    {
        public const string DoppeltGrund = "Risiko: doppelt";

        /// <summary>Der Vorschlag; <c>null</c>, wenn die Regel nicht greift (nein, nichts, nicht bewertet).</summary>
        public static Punktevorschlag? Werte(bool doppelt, SonderregelEingang eingang)
        {
            ArgumentNullException.ThrowIfNull(eingang);

            if (!doppelt || eingang.Bewertung == RatingKind.None)
                return null;

            return Sonderregelwertung.Gekappt(2 * eingang.Grundwert, DoppeltGrund, eingang.Konto);
        }
    }
}
