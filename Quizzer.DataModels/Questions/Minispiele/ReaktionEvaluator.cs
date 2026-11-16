namespace Quizzer.DataModels.Questions.Minispiele
{
    /// <summary>Wie ein Versuch der Reaktion zaehlt. Die Zahl geht ans Telefon - nur anhaengen.</summary>
    public enum ReactionOutcome
    {
        /// <summary>Gueltige Zeit, sie zaehlt.</summary>
        Valid = 1,

        /// <summary>Vor Gruen getippt oder unter der Grenze - die Runde ist verloren.</summary>
        FalseStart = 2,

        /// <summary>Zu langsam - keine Wertung.</summary>
        TooSlow = 3,
    }

    /// <summary>Ein Versuch eines Spielers in einer Runde.</summary>
    /// <param name="Outcome">Wie er zaehlt.</param>
    /// <param name="Milliseconds">Die gemessene Zeit am Telefon; <c>null</c> beim Tippen vor Gruen.</param>
    public sealed record ReactionAttempt(ReactionOutcome Outcome, int? Milliseconds);

    /// <summary>Was die Reaktion ueber alle Runden ergeben hat.</summary>
    /// <param name="Bestzeiten">Je Spieler seine beste gueltige Zeit - wer fehlt, hat keine.</param>
    /// <param name="Gewinner">Die Spieler mit der besten Zeit; leer, wenn niemand eine hat.</param>
    /// <param name="Abgegeben">Wer in mindestens einer Runde etwas abgegeben hat.</param>
    public sealed record ReaktionErgebnis(
        IReadOnlyDictionary<Guid, int> Bestzeiten,
        IReadOnlySet<Guid> Gewinner,
        IReadOnlySet<Guid> Abgegeben)
    {
        public bool IstGewinner(Guid spieler) => Gewinner.Contains(spieler);

        public int? BestzeitVon(Guid spieler) => Bestzeiten.TryGetValue(spieler, out var ms) ? ms : null;
    }

    /// <summary>
    /// Die Reaktion (M35 B3): Grenzen, Einordnung einer Abgabe und die Wertung ueber alle Runden.
    /// <para>
    /// <b>Die Zeit misst das Telefon</b> ab seinem eigenen Umschalten auf Gruen - Ankunftszeiten am
    /// Server sind wegen WLAN-Jitter unbrauchbar. Der Server prueft nur die Plausibilitaet.
    /// <b>Nur Vorschlaege, nie Minus</b> (E1): Frueh- und Fehlstarts kosten die Runde, keine Punkte.
    /// </para>
    /// </summary>
    public static class ReaktionEvaluator
    {
        /// <summary>Darunter ist es kein Reagieren mehr, sondern Raten - Fruehstart.</summary>
        public const int FalseStartBelowMs = 100;

        /// <summary>Darueber gibt es keine Wertung; das Telefon gibt dann selbst auf.</summary>
        public const int NoScoreAboveMs = 5000;

        /// <summary>Die kuerzeste und die laengste Wartezeit bis Gruen, gewuerfelt am Server.</summary>
        public const int MinWaitMs = 2000;

        public const int MaxWaitMs = 6000;

        /// <summary>
        /// Ordnet eine Abgabe ein. <c>null</c> heisst: unbrauchbar (weder Fruehstart noch Zeit) - verwerfen.
        /// </summary>
        public static ReactionAttempt? Classify(bool falseStart, int? milliseconds)
        {
            if (falseStart)
                return new(ReactionOutcome.FalseStart, null);

            if (milliseconds is not { } ms || ms < 0)
                return null;

            if (ms < FalseStartBelowMs)
                return new(ReactionOutcome.FalseStart, ms);

            return ms > NoScoreAboveMs
                ? new(ReactionOutcome.TooSlow, ms)
                : new(ReactionOutcome.Valid, ms);
        }

        /// <summary>Wertet alle gespielten Runden aus: die beste Einzelzeit gewinnt, bei Gleichstand alle.</summary>
        /// <param name="runden">Je Runde die Versuche je Spieler; wer fehlt, hat nichts abgegeben.</param>
        public static ReaktionErgebnis Werte(IEnumerable<IReadOnlyDictionary<Guid, ReactionAttempt>> runden)
        {
            ArgumentNullException.ThrowIfNull(runden);

            var bestzeiten = new Dictionary<Guid, int>();
            var abgegeben = new HashSet<Guid>();

            foreach (var runde in runden)
            {
                foreach (var (spieler, versuch) in runde)
                {
                    abgegeben.Add(spieler);

                    if (versuch.Outcome != ReactionOutcome.Valid || versuch.Milliseconds is not { } ms)
                        continue;

                    if (!bestzeiten.TryGetValue(spieler, out var bisher) || ms < bisher)
                        bestzeiten[spieler] = ms;
                }
            }

            var gewinner = new HashSet<Guid>();

            if (bestzeiten.Count > 0)
            {
                var beste = bestzeiten.Values.Min();
                gewinner.UnionWith(bestzeiten.Where(b => b.Value == beste).Select(b => b.Key));
            }

            return new ReaktionErgebnis(bestzeiten, gewinner, abgegeben);
        }

        /// <summary>Ein Versuch im Klartext - fuer Beamer, Spielerkarte und Telefon dieselben Worte.</summary>
        public static string Describe(ReactionAttempt? versuch) => versuch switch
        {
            null => "keine Abgabe",
            { Outcome: ReactionOutcome.Valid, Milliseconds: { } ms } => $"{ms} ms",
            { Outcome: ReactionOutcome.FalseStart } => "Frühstart",
            { Outcome: ReactionOutcome.TooSlow } => "zu langsam",
            _ => "keine Abgabe",
        };
    }
}
