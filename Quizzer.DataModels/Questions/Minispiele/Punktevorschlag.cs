using Quizzer.DataModels.Anzeige;
using Quizzer.DataModels.Helpers;

namespace Quizzer.DataModels.Questions.Minispiele
{
    /// <summary>Eine Punktebewegung fuer die Spielerkarte (E1): schon gekappt (E2), samt Grund im Klartext.</summary>
    /// <param name="Punkte">Plus oder Minus; gebucht wird erst mit der Bestaetigung des Spielleiters.</param>
    /// <param name="Grund">Steht auf der Karte hinter dem Wert, etwa „Duell gegen Bert gewonnen".</param>
    /// <param name="Klaus">
    /// Wie viele Klaus mit Gewinn fuer diesen Spieler darin stecken (M35 C9: Raub, Klauen am Gluecksrad, Kopfgeld) -
    /// gezaehlt erst, wenn der Spielleiter den Vorschlag speichert.
    /// </param>
    public sealed record Punktevorschlag(int Punkte, string Grund, int Klaus = 0);

    /// <summary>Was die Kappung (E2) braucht: die Kontostaende und ob das Spiel ein Minus erlaubt.</summary>
    /// <param name="Stand">Je Spieler sein Kontostand; wer fehlt, steht bei 0.</param>
    /// <param name="UnterNullErlaubt"><see cref="Models.Base.Game.AllowNegativeScore"/>.</param>
    public sealed record Kontostaende(IReadOnlyDictionary<Guid, int> Stand, bool UnterNullErlaubt)
    {
        /// <summary>Ohne Grenze - das Spiel erlaubt ein Minus.</summary>
        public static Kontostaende OhneGrenze { get; } = new(new Dictionary<Guid, int>(), true);

        public int Von(Guid spieler) => Stand.GetValueOrDefault(spieler);

        /// <summary>Kappt ein Minus am Kontostand dieses Spielers, wenn das Spiel kein Minus erlaubt.</summary>
        public int Kappe(Guid spieler, int punkte) => Punktebewegung.Kappe(punkte, Von(spieler), UnterNullErlaubt);
    }

    /// <summary>Gemeinsame Rechnungen der Spiele um Punkte (M35 C2).</summary>
    public static class Punkteanteil
    {
        /// <summary>Hinter einem gekappten Minus - der Spielleiter sieht, warum es weniger ist.</summary>
        public const string GekapptText = " – gekappt am Kontostand";

        /// <summary><paramref name="prozent"/> Prozent der Feldpunkte, kaufmaennisch gerundet, nie unter 0.</summary>
        public static int Von(int feldpunkte, int prozent)
            => Math.Max(0, (int)Math.Round(feldpunkte * prozent / 100.0, MidpointRounding.AwayFromZero));

        /// <summary>„+50" bzw. „−50", ohne Bewegung „±0" - fuer Gruende und Tafel.</summary>
        public static string Text(int punkte) => punkte == 0 ? "±0" : Punktewechsel.Text(punkte);
    }
}
