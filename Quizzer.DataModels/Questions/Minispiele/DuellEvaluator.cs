namespace Quizzer.DataModels.Questions.Minispiele
{
    /// <summary>Ein Zug bei Schere-Stein-Papier. Die Zahl geht ans Telefon (<c>constants.js</c>) - nur anhaengen.</summary>
    public enum SchereSteinPapier
    {
        Schere = 1,
        Stein = 2,
        Papier = 3,
    }

    /// <summary>Wie ein Duell steht oder ausgegangen ist.</summary>
    /// <param name="Herausforderer">Wer fordert - der Ausloeser.</param>
    /// <param name="Gegner">Wen er gewaehlt hat.</param>
    /// <param name="SiegeHerausforderer">Gewonnene Runden; bei der Reaktion 1 fuer den Sieger.</param>
    /// <param name="SiegeGegner">Ebenso fuer den Gegner.</param>
    /// <param name="Sieger"><c>null</c> heisst Unentschieden.</param>
    public sealed record DuellErgebnis(Guid Herausforderer, Guid Gegner, int SiegeHerausforderer, int SiegeGegner, Guid? Sieger)
    {
        public Guid? Verlierer => Sieger == null ? null : Sieger == Herausforderer ? Gegner : Herausforderer;

        public bool Spielt(Guid spieler) => spieler == Herausforderer || spieler == Gegner;

        public int SiegeVon(Guid spieler)
            => spieler == Herausforderer ? SiegeHerausforderer : spieler == Gegner ? SiegeGegner : 0;

        /// <summary>„2:1" aus Sicht des Herausforderers.</summary>
        public string Stand => $"{SiegeHerausforderer}:{SiegeGegner}";
    }

    /// <summary>
    /// Das Duell (M35 C2): zwei spielen, die anderen schauen zu. Schere-Stein-Papier ist Best of n und
    /// endet, sobald einer mehr als die Haelfte der Runden hat; die Reaktion wertet wie B3 die beste
    /// Einzelzeit der beiden. <b>Wer nichts abgibt, verliert gegen einen, der abgibt</b> - wie bei der
    /// Reaktion; der Spielleiter kann die Runde wiederholen (E1).
    /// </summary>
    public static class DuellEvaluator
    {
        /// <summary>Spielweise 0 der Art - <see cref="MinigameProfiles"/> fuehrt sie in dieser Reihenfolge.</summary>
        public const int SchereSteinPapierVariant = 0;

        public const int ReaktionVariant = 1;

        /// <summary>1: <paramref name="a"/> schlaegt <paramref name="b"/>, -1: umgekehrt, 0: gleich.</summary>
        public static int Vergleiche(SchereSteinPapier a, SchereSteinPapier b)
        {
            if (a == b)
                return 0;

            // Schere schneidet Papier, Stein schleift Schere, Papier wickelt Stein.
            return (a, b) is (SchereSteinPapier.Schere, SchereSteinPapier.Papier)
                or (SchereSteinPapier.Stein, SchereSteinPapier.Schere)
                or (SchereSteinPapier.Papier, SchereSteinPapier.Stein)
                ? 1
                : -1;
        }

        /// <summary>Wer eine Runde gewinnt; <c>null</c> bei gleichen Zuegen oder wenn keiner gezogen hat.</summary>
        public static Guid? RundenSieger(Guid a, Guid b, IReadOnlyDictionary<Guid, SchereSteinPapier> zuege)
        {
            ArgumentNullException.ThrowIfNull(zuege);

            var hatA = zuege.TryGetValue(a, out var zugA);
            var hatB = zuege.TryGetValue(b, out var zugB);

            if (hatA != hatB)
                return hatA ? a : b;

            if (!hatA)
                return null;

            return Vergleiche(zugA, zugB) switch
            {
                > 0 => a,
                < 0 => b,
                _ => null,
            };
        }

        /// <summary>Zaehlt die Rundensiege; wer mehr hat, gewinnt, gleich viele heisst Unentschieden.</summary>
        public static DuellErgebnis WerteSchereSteinPapier(
            Guid a, Guid b, IEnumerable<IReadOnlyDictionary<Guid, SchereSteinPapier>> runden)
        {
            ArgumentNullException.ThrowIfNull(runden);

            int siegeA = 0, siegeB = 0;

            foreach (var runde in runden)
            {
                var sieger = RundenSieger(a, b, runde);

                if (sieger == a)
                    siegeA++;
                else if (sieger == b)
                    siegeB++;
            }

            return new(a, b, siegeA, siegeB, siegeA > siegeB ? a : siegeB > siegeA ? b : null);
        }

        /// <summary>Die Reaktion der beiden ueber alle Runden (<see cref="ReaktionEvaluator.Werte"/>); gleiche Bestzeit: Unentschieden.</summary>
        public static DuellErgebnis WerteReaktion(Guid a, Guid b, IEnumerable<IReadOnlyDictionary<Guid, ReactionAttempt>> runden)
        {
            ArgumentNullException.ThrowIfNull(runden);

            // Nur die beiden: ein Zuschauer, der doch etwas geschickt haette, gewinnt kein Duell.
            var ergebnis = ReaktionEvaluator.Werte(runden.Select(r => (IReadOnlyDictionary<Guid, ReactionAttempt>)r
                .Where(e => e.Key == a || e.Key == b)
                .ToDictionary(e => e.Key, e => e.Value)));

            Guid? sieger = ergebnis.Gewinner.Count == 1 ? ergebnis.Gewinner.Single() : null;

            return new(a, b, sieger == a ? 1 : 0, sieger == b ? 1 : 0, sieger);
        }

        /// <summary>Best of <paramref name="runden"/>: entschieden, sobald einer mehr als die Haelfte hat.</summary>
        public static bool IstEntschieden(DuellErgebnis ergebnis, int runden)
        {
            ArgumentNullException.ThrowIfNull(ergebnis);
            return Math.Max(ergebnis.SiegeHerausforderer, ergebnis.SiegeGegner) * 2 > runden;
        }

        /// <summary>Sieger +Einsatz, Verlierer −Einsatz (gekappt nach E2); Unentschieden: keine Bewegung.</summary>
        public static IReadOnlyDictionary<Guid, Punktevorschlag> Vorschlaege(
            DuellErgebnis ergebnis, int einsatz, Kontostaende konten, Func<Guid, string> name)
        {
            ArgumentNullException.ThrowIfNull(ergebnis);
            ArgumentNullException.ThrowIfNull(konten);
            ArgumentNullException.ThrowIfNull(name);

            if (ergebnis is not { Sieger: { } sieger, Verlierer: { } verlierer })
                return new Dictionary<Guid, Punktevorschlag>();

            var minus = konten.Kappe(verlierer, -einsatz);

            return new Dictionary<Guid, Punktevorschlag>
            {
                [sieger] = new(einsatz, $"Duell gegen {name(verlierer)} gewonnen"),
                [verlierer] = new(minus, $"Duell gegen {name(sieger)} verloren" + (minus != -einsatz ? Punkteanteil.GekapptText : string.Empty)),
            };
        }

        /// <summary>Ein Zug im Klartext - fuer Beamer, Spielerkarte und Telefon dieselben Worte.</summary>
        public static string Describe(SchereSteinPapier? zug) => zug switch
        {
            SchereSteinPapier.Schere => "Schere",
            SchereSteinPapier.Stein => "Stein",
            SchereSteinPapier.Papier => "Papier",
            _ => "keine Abgabe",
        };
    }
}
