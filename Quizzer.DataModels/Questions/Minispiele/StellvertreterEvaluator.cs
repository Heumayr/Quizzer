namespace Quizzer.DataModels.Questions.Minispiele
{
    /// <summary>Wer für ein Team ins Duell geht (M37 T10).</summary>
    /// <param name="TeamId">Das Team, aus der Aufstellung des Hubs beim Start.</param>
    /// <param name="SpielerId">Der Stellvertreter - ein Mitglied eben dieses Teams.</param>
    /// <param name="Gelost">Ob der Server ihn gelost hat (Los am Telefon oder durch den Spielleiter).</param>
    public sealed record Stellvertreter(Guid TeamId, Guid SpielerId, bool Gelost);

    /// <summary>Wie das Stellvertreter-Duell steht oder ausgegangen ist.</summary>
    /// <param name="Disziplin">Reaktion oder Schere-Stein-Papier, gezählt wie beim Duell (<see cref="DuellEvaluator"/>).</param>
    /// <param name="Vertreter">Je Team sein Stellvertreter, in der Reihenfolge der Teamkennungen.</param>
    /// <param name="Sieger">Die Stellvertreter, die gewonnen haben; leer bei Unentschieden, ohne gültige Abgabe oder ohne Gegner.</param>
    /// <param name="Reaktion">Bei der Reaktion die Bestzeiten der Stellvertreter, sonst <c>null</c>.</param>
    /// <param name="Duell">Bei Schere-Stein-Papier der Stand der beiden, sonst <c>null</c>.</param>
    public sealed record StellvertreterErgebnis(
        int Disziplin, IReadOnlyList<Stellvertreter> Vertreter, IReadOnlySet<Guid> Sieger, ReaktionErgebnis? Reaktion, DuellErgebnis? Duell)
    {
        public bool IstSchereSteinPapier => Disziplin == DuellEvaluator.SchereSteinPapierVariant;

        public bool Spielt(Guid spieler) => Vertreter.Any(v => v.SpielerId == spieler);

        public Stellvertreter? VertreterVon(Guid teamId) => Vertreter.FirstOrDefault(v => v.TeamId == teamId);

        public bool IstSiegerteam(Guid teamId) => VertreterVon(teamId) is { } v && Sieger.Contains(v.SpielerId);

        /// <summary>Was ein Stellvertreter über alle Runden erreicht hat: Bestzeit bzw. Siege.</summary>
        public string Leistung(Guid spieler)
        {
            if (Duell != null)
                return Duell.SiegeVon(spieler) == 1 ? "1 Sieg" : $"{Duell.SiegeVon(spieler)} Siege";

            return Reaktion?.BestzeitVon(spieler) is { } ms ? $"{ms} ms"
                : Reaktion?.Abgegeben.Contains(spieler) == true ? "keine gültige Zeit" : "keine Abgabe";
        }
    }

    /// <summary>
    /// Das Stellvertreter-Duell (M37 T10): jedes Team schickt einen, nur die Gewählten spielen. Reaktion für jede Zahl von Teams
    /// (beste Zeit über alle Runden, Gleichstand alle vorn), Schere-Stein-Papier nur bei genau zwei (Best of n). <b>Ohne Gegner
    /// gewinnt niemand</b> - ein einzelnes Team bekäme sonst die Punkte geschenkt. Wer gewinnt, holt die Punkte fürs Team
    /// (<c>Teamwertung.StellvertreterVorschlaege</c>); nie Minus.
    /// </summary>
    public static class StellvertreterEvaluator
    {
        /// <summary>Spielweise 0 der Art - die Vorgabe, sie geht mit jeder Zahl von Teams.</summary>
        public const int ReaktionVariant = 0;

        /// <summary>Spielweise 1 - nur bei genau zwei Teams, sonst spielen die Stellvertreter Reaktion.</summary>
        public const int SchereSteinPapierVariant = 1;

        /// <summary>So viele Wahlversuche nimmt der Server je Telefon und <see cref="FensterSekunden"/> an (Entwurf Abschnitt 4).</summary>
        public const int MaxWahlenJeFenster = 5;

        public const int FensterSekunden = 10;

        /// <summary>Die Disziplin aus Spielweise und Zahl der Teams - als Wert von <see cref="DuellEvaluator"/>.</summary>
        public static int Disziplin(int variant, int teams)
            => variant == SchereSteinPapierVariant && teams == 2 ? DuellEvaluator.SchereSteinPapierVariant : DuellEvaluator.ReaktionVariant;

        /// <summary>
        /// Lost einen aus den Kandidaten - mit der Zufallsquelle des Servers, über eine Reihenfolge, die keine Nachricht
        /// beeinflusst (nach Kennung). <c>null</c> ohne Kandidaten.
        /// </summary>
        public static Guid? Lose(IEnumerable<Guid> kandidaten, Func<int, int, int> zufall)
        {
            ArgumentNullException.ThrowIfNull(kandidaten);
            ArgumentNullException.ThrowIfNull(zufall);

            var liste = kandidaten.Distinct().Order().ToList();

            return liste.Count == 0 ? null : liste[Math.Clamp(zufall(0, liste.Count), 0, liste.Count - 1)];
        }

        /// <summary>Die Reaktion der Stellvertreter (<see cref="ReaktionEvaluator.Werte"/>) - wer kein Stellvertreter ist, zählt nicht.</summary>
        public static StellvertreterErgebnis WerteReaktion(
            IReadOnlyList<Stellvertreter> vertreter, IEnumerable<IReadOnlyDictionary<Guid, ReactionAttempt>> runden)
        {
            ArgumentNullException.ThrowIfNull(vertreter);
            ArgumentNullException.ThrowIfNull(runden);

            var spielen = vertreter.Select(v => v.SpielerId).ToHashSet();
            var reaktion = ReaktionEvaluator.Werte(runden.Select(r => (IReadOnlyDictionary<Guid, ReactionAttempt>)r
                .Where(e => spielen.Contains(e.Key))
                .ToDictionary(e => e.Key, e => e.Value)));

            var sieger = vertreter.Count >= 2 ? reaktion.Gewinner : new HashSet<Guid>();

            return new(DuellEvaluator.ReaktionVariant, vertreter, sieger, reaktion, null);
        }

        /// <summary>Schere-Stein-Papier der beiden (<see cref="DuellEvaluator.WerteSchereSteinPapier"/>); nicht genau zwei: kein Sieger.</summary>
        public static StellvertreterErgebnis WerteSchereSteinPapier(
            IReadOnlyList<Stellvertreter> vertreter, IEnumerable<IReadOnlyDictionary<Guid, SchereSteinPapier>> runden)
        {
            ArgumentNullException.ThrowIfNull(vertreter);
            ArgumentNullException.ThrowIfNull(runden);

            if (vertreter.Count != 2)
                return new(DuellEvaluator.SchereSteinPapierVariant, vertreter, new HashSet<Guid>(), null, null);

            var duell = DuellEvaluator.WerteSchereSteinPapier(vertreter[0].SpielerId, vertreter[1].SpielerId, runden);
            var sieger = duell.Sieger is { } wer ? new HashSet<Guid> { wer } : new HashSet<Guid>();

            return new(DuellEvaluator.SchereSteinPapierVariant, vertreter, sieger, null, duell);
        }

        /// <summary>Best of n bei Schere-Stein-Papier - die Reaktion spielt immer alle Runden.</summary>
        public static bool IstEntschieden(StellvertreterErgebnis ergebnis, int runden)
        {
            ArgumentNullException.ThrowIfNull(ergebnis);
            return ergebnis.Duell is { } duell && DuellEvaluator.IstEntschieden(duell, runden);
        }
    }
}
