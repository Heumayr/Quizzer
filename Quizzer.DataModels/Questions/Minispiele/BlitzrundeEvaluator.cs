namespace Quizzer.DataModels.Questions.Minispiele
{
    /// <summary>Wie ein Spieler in der Blitzrunde steht - erst nach der Runde bekannt, waehrend ihr verriete es die Wahrheit.</summary>
    /// <param name="Richtig">So viele Aussagen hat er richtig beurteilt.</param>
    /// <param name="Falsch">So viele falsch.</param>
    public sealed record BlitzrundeStand(int Richtig, int Falsch)
    {
        public int Beantwortet => Richtig + Falsch;

        /// <summary>Richtig +1, falsch −1.</summary>
        public int Netto => Richtig - Falsch;
    }

    /// <summary>Eine angenommene Antwort, gemeldet an den Spielleiter - nur der Fortschritt, nie ob sie stimmte.</summary>
    /// <param name="Aussage">Die Nummer der beantworteten Aussage, ab 1.</param>
    /// <param name="Beantwortet">So viele hat der Spieler jetzt beantwortet.</param>
    public sealed record BlitzrundeFortschritt(int Aussage, int Beantwortet);

    /// <summary>Was die Blitzrunde ergeben hat.</summary>
    /// <param name="Staende">Je Spieler mit mindestens einer Antwort sein Stand.</param>
    /// <param name="Aussagen">So viele Aussagen waren im Spiel.</param>
    /// <param name="Vorschlaege">Die Punkte je Spieler, schon gekappt, mit Grund (E1, E2).</param>
    public sealed record BlitzrundeErgebnis(
        IReadOnlyDictionary<Guid, BlitzrundeStand> Staende, int Aussagen, IReadOnlyDictionary<Guid, Punktevorschlag> Vorschlaege)
    {
        public BlitzrundeStand? StandVon(Guid spieler) => Staende.GetValueOrDefault(spieler);

        public int PunkteVon(Guid spieler) => Vorschlaege.GetValueOrDefault(spieler)?.Punkte ?? 0;
    }

    /// <summary>
    /// Die Blitzrunde (M35 C5): je Aussage richtig +1, falsch −1, keine Antwort 0. Der Vorschlag ist der
    /// Anteil der Feldpunkte: <c>Feldpunkte × (richtig − falsch) / Aussagen</c>, kaufmaennisch gerundet - alle
    /// richtig bringt die Feldpunkte, alle falsch ihr Minus. Ein Minus kappt der Kontostand (E2), ein Plus nie.
    /// </summary>
    public static class BlitzrundeEvaluator
    {
        /// <summary>
        /// Plausibilitaetsgrenze am Server: eine Aussage lesen und beurteilen dauert laenger als eine
        /// Drittelsekunde. Raten bringt hier nichts (−1 je Fehler) - die Grenze haelt nur Fluten ab.
        /// </summary>
        public const int MaxAntwortenJeSekunde = 3;

        /// <summary>Die Punkte eines Stands, noch ungekappt; ohne Aussagen 0.</summary>
        public static int Punkte(BlitzrundeStand stand, int aussagen, int feldpunkte)
        {
            ArgumentNullException.ThrowIfNull(stand);

            return aussagen <= 0
                ? 0
                : (int)Math.Round((double)feldpunkte * stand.Netto / aussagen, MidpointRounding.AwayFromZero);
        }

        /// <summary>Die Wertung: jeder mit Antworten bekommt seinen Anteil als Vorschlag mit Grund; null Punkte bringen keinen.</summary>
        public static BlitzrundeErgebnis Werte(
            IReadOnlyDictionary<Guid, BlitzrundeStand> staende, int aussagen, int feldpunkte, Kontostaende konten)
        {
            ArgumentNullException.ThrowIfNull(staende);
            ArgumentNullException.ThrowIfNull(konten);

            var vorschlaege = new Dictionary<Guid, Punktevorschlag>();

            foreach (var (id, stand) in staende)
            {
                var punkte = Punkte(stand, aussagen, feldpunkte);
                var gekappt = konten.Kappe(id, punkte);

                if (gekappt != 0)
                    vorschlaege[id] = new(gekappt, $"Blitzrunde: {Describe(stand)} {Punkteanteil.Text(gekappt)}"
                                                   + (gekappt != punkte ? Punkteanteil.GekapptText : string.Empty));
            }

            return new(staende, aussagen, vorschlaege);
        }

        /// <summary>„5 richtig, 2 falsch" - fuer Beamer, Spielerkarte und Telefon dieselben Worte.</summary>
        public static string Describe(BlitzrundeStand stand)
        {
            ArgumentNullException.ThrowIfNull(stand);

            return $"{stand.Richtig} richtig, {stand.Falsch} falsch";
        }

        /// <summary>Der Satz auf der Spielerkarte.</summary>
        public static string Describe(BlitzrundeErgebnis ergebnis, Guid spieler)
        {
            ArgumentNullException.ThrowIfNull(ergebnis);

            return ergebnis.StandVon(spieler) is { } stand
                ? $"{Describe(stand)} von {ergebnis.Aussagen} – {Punkteanteil.Text(ergebnis.PunkteVon(spieler))}"
                : "Keine Antwort – von Hand werten";
        }
    }
}
