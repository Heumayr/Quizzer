namespace Quizzer.DataModels.Questions.Minispiele
{
    /// <summary>Wie viele Begriffe ein Spieler im Buchstabensalat geloest hat.</summary>
    public sealed record BuchstabensalatStand(int Geloest);

    /// <summary>Ein angenommener Versuch, gemeldet an den Spielleiter.</summary>
    /// <param name="Begriff">Die Nummer des Begriffs, ab 1.</param>
    /// <param name="Loesung">Der Begriff, wenn dieser Versuch ihn geloest hat - dann ist er nicht mehr offen; sonst <c>null</c>.</param>
    /// <param name="Stand">Der Stand des Spielers danach.</param>
    public sealed record BuchstabensalatVersuch(int Begriff, string? Loesung, BuchstabensalatStand Stand)
    {
        public bool Richtig => Loesung != null;
    }

    /// <summary>Was der Buchstabensalat ergeben hat.</summary>
    /// <param name="Staende">Je Spieler mit mindestens einem Versuch sein Stand.</param>
    /// <param name="Begriffe">So viele Begriffe waren im Spiel.</param>
    /// <param name="Vorschlaege">Die Punkte je Spieler mit Grund (E1).</param>
    public sealed record BuchstabensalatErgebnis(
        IReadOnlyDictionary<Guid, BuchstabensalatStand> Staende, int Begriffe, IReadOnlyDictionary<Guid, Punktevorschlag> Vorschlaege)
    {
        public BuchstabensalatStand? StandVon(Guid spieler) => Staende.GetValueOrDefault(spieler);

        public int PunkteVon(Guid spieler) => Vorschlaege.GetValueOrDefault(spieler)?.Punkte ?? 0;

        /// <summary>Die meisten geloesten; leer, wenn keiner einen hat.</summary>
        public IReadOnlySet<Guid> Gewinner
        {
            get
            {
                var beste = Staende.Values.Select(s => s.Geloest).DefaultIfEmpty(0).Max();
                return beste > 0 ? Staende.Where(s => s.Value.Geloest == beste).Select(s => s.Key).ToHashSet() : new HashSet<Guid>();
            }
        }
    }

    /// <summary>
    /// Der Buchstabensalat (M35 C5): jeder Begriff gehoert dem, der ihn zuerst richtig eintippt (Serveruhr beim
    /// Eintreffen). Der Vorschlag ist der Anteil der Feldpunkte: <c>Feldpunkte × geloest / Begriffe</c>,
    /// kaufmaennisch gerundet - wer alle loest, bekommt sie ganz. Falsch geraten kostet nichts; ein Minus gibt es
    /// nicht, gekappt wird darum nie (E2 kappt nur ein Minus).
    /// </summary>
    public static class BuchstabensalatEvaluator
    {
        /// <summary>
        /// Plausibilitaetsgrenze am Server, je Spieler: hoechstens so viele Versuche im gleitenden Fenster. Raten ist
        /// hier der Missbrauch - ein Skript koennte alle Anordnungen durchprobieren. Ein Mensch tippt ein Wort nicht
        /// schneller als etwa eines je Sekunde und braucht fuer drei Versuche samt Korrektur kaum weniger als fuenf
        /// Sekunden; ein Skript schafft so 0,6 Versuche je Sekunde, fuer sechs Buchstaben (720 Anordnungen) also
        /// zwanzig Minuten - weit ueber jeder Runde. Kurze Begriffe (vier Buchstaben, 24 Anordnungen) bleiben in
        /// vierzig Sekunden ratbar; das ist der Preis dafuer, keinen ehrlichen Spieler auszubremsen (Annahme C5b).
        /// </summary>
        public const int MaxVersucheJeFenster = 3;

        /// <summary>Das Fenster zu <see cref="MaxVersucheJeFenster"/>.</summary>
        public const int FensterSekunden = 5;

        /// <summary>Die Punkte fuer so viele geloeste; ohne Begriffe 0.</summary>
        public static int Punkte(int geloest, int begriffe, int feldpunkte)
            => begriffe <= 0 ? 0 : (int)Math.Round((double)feldpunkte * geloest / begriffe, MidpointRounding.AwayFromZero);

        /// <summary>Die Wertung: jeder mit einem geloesten Begriff bekommt seinen Anteil als Vorschlag mit Grund.</summary>
        public static BuchstabensalatErgebnis Werte(
            IReadOnlyDictionary<Guid, BuchstabensalatStand> staende, int begriffe, int feldpunkte, Kontostaende konten)
        {
            ArgumentNullException.ThrowIfNull(staende);
            ArgumentNullException.ThrowIfNull(konten);

            var vorschlaege = new Dictionary<Guid, Punktevorschlag>();

            foreach (var (id, stand) in staende)
            {
                var punkte = konten.Kappe(id, Punkte(stand.Geloest, begriffe, feldpunkte));

                if (punkte != 0)
                    vorschlaege[id] = new(punkte, $"Buchstabensalat: {Describe(stand)} von {begriffe} {Punkteanteil.Text(punkte)}");
            }

            return new(staende, begriffe, vorschlaege);
        }

        /// <summary>„2 gelöst" - fuer Beamer, Spielerkarte und Buzzer-Fenster dieselben Worte.</summary>
        public static string Describe(BuchstabensalatStand stand)
        {
            ArgumentNullException.ThrowIfNull(stand);

            return $"{stand.Geloest} gelöst";
        }

        /// <summary>Der Satz auf der Spielerkarte.</summary>
        public static string Describe(BuchstabensalatErgebnis ergebnis, Guid spieler)
        {
            ArgumentNullException.ThrowIfNull(ergebnis);

            return ergebnis.StandVon(spieler) is { } stand
                ? $"{Describe(stand)} von {ergebnis.Begriffe} – {Punkteanteil.Text(ergebnis.PunkteVon(spieler))}"
                : "Kein Versuch – von Hand werten";
        }
    }
}
