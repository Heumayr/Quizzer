namespace Quizzer.DataModels.Questions.Minispiele
{
    /// <summary>
    /// Die Rohwerte je Spieler aus der Wertung einer Runde (M35 C9), in der Einheit von <see cref="MinigameRecord"/>.
    /// Wer fehlt, hat keinen Messwert - nichts abgegeben, ungueltig oder (bei „mehr ist besser") null erreicht.
    /// </summary>
    public static class Messwerte
    {
        /// <summary>Die beste gueltige Einzelzeit je Spieler in ms.</summary>
        public static IReadOnlyDictionary<Guid, int> Reaktion(ReaktionErgebnis ergebnis)
        {
            ArgumentNullException.ThrowIfNull(ergebnis);
            return new Dictionary<Guid, int>(ergebnis.Bestzeiten);
        }

        /// <summary>Tipps je Sekunde in Hundertsteln, bezogen auf die eingestellte Dauer.</summary>
        public static IReadOnlyDictionary<Guid, int> Tippsprint(TippsprintErgebnis ergebnis, int dauerSekunden)
        {
            ArgumentNullException.ThrowIfNull(ergebnis);
            return ergebnis.Endstaende.Where(e => e.Value > 0)
                .ToDictionary(e => e.Key, e => Hundertstel(e.Value, dauerSekunden));
        }

        /// <summary>Der Abstand zur Zielzeit in ms - nur fuer gewertete Zeiten.</summary>
        public static IReadOnlyDictionary<Guid, int> Stoppuhr(StoppuhrErgebnis ergebnis)
        {
            ArgumentNullException.ThrowIfNull(ergebnis);

            var werte = new Dictionary<Guid, int>();
            foreach (var spieler in ergebnis.Abgaben.Keys)
            {
                if (ergebnis.AbstandVon(spieler) is { } abstand)
                    werte[spieler] = abstand;
            }

            return werte;
        }

        /// <summary>Richtige Antworten je Minute in Hundertsteln, bezogen auf die eingestellte Dauer.</summary>
        public static IReadOnlyDictionary<Guid, int> Kopfrechnen(KopfrechnenErgebnis ergebnis, int dauerSekunden)
        {
            ArgumentNullException.ThrowIfNull(ergebnis);
            return ergebnis.Staende.Where(s => s.Value.Richtig > 0)
                .ToDictionary(s => s.Key, s => Hundertstel(s.Value.Richtig * 60L, dauerSekunden));
        }

        /// <summary>Der Multiplikator beim Ausstieg in Hundertsteln; wer nicht ausstieg, hat keinen.</summary>
        public static IReadOnlyDictionary<Guid, int> Zocker(ZockerErgebnis ergebnis)
        {
            ArgumentNullException.ThrowIfNull(ergebnis);
            return new Dictionary<Guid, int>(ergebnis.Ausstiege);
        }

        /// <summary><paramref name="anzahl"/> je Sekunde in Hundertsteln, kaufmaennisch gerundet; ohne Dauer 0.</summary>
        public static int Hundertstel(long anzahl, int sekunden)
            => sekunden <= 0 ? 0 : (int)Math.Round(anzahl * 100.0 / sekunden, MidpointRounding.AwayFromZero);
    }
}
