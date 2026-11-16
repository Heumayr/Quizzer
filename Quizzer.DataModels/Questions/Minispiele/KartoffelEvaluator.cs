namespace Quizzer.DataModels.Questions.Minispiele
{
    /// <summary>Eine angenommene Antwort des Halters, gemeldet an den Spielleiter - der kennt die Wahrheit ohnehin.</summary>
    /// <param name="Nummer">Die wievielte Aussage der Runde.</param>
    /// <param name="Richtig">Ob die Antwort stimmte - dann darf der Halter weitergeben.</param>
    public sealed record KartoffelAntwort(int Nummer, bool Richtig);

    /// <summary>Die Kartoffel ist gewandert - das sehen alle.</summary>
    public sealed record KartoffelWeitergabe(Guid Von, Guid An);

    /// <summary>Wer beim Knall hielt.</summary>
    /// <param name="Halter">Er verliert.</param>
    /// <param name="Zuschieber">Wer sie ihm gegeben hat; <c>null</c> beim Starthalter und wenn der Spielleiter sie hingelegt hat.</param>
    public sealed record KartoffelKnall(Guid Halter, Guid? Zuschieber);

    /// <summary>Eine gezeigte Aussage im Verlauf der Runde.</summary>
    /// <param name="Halter">Wer sie beantworten sollte.</param>
    /// <param name="Wahr">Der Haken der Zeile.</param>
    /// <param name="Richtig">Ob die Antwort stimmte; <c>null</c>: beim Knall noch offen.</param>
    /// <param name="WeiterAn">Wem der Halter sie danach gab - nur nach einer richtigen Antwort.</param>
    public sealed record KartoffelZug(Guid Halter, string Aussage, bool Wahr, bool? Richtig = null, Guid? WeiterAn = null);

    /// <summary>Eine gespielte Runde - fuer den Spielleiter erst, wenn sie zu ist.</summary>
    /// <param name="Knall">Wer beim Knall hielt; <c>null</c>, wenn die Runde ohne Knall zu ging.</param>
    /// <param name="Verlauf">Die gezeigten Aussagen in ihrer Reihenfolge.</param>
    /// <param name="ZuenderSekunden">Nach so vielen Sekunden knallte es - erst danach kein Geheimnis mehr.</param>
    public sealed record KartoffelRunde(KartoffelKnall? Knall, IReadOnlyList<KartoffelZug> Verlauf, int? ZuenderSekunden);

    /// <summary>Was die Heisse Kartoffel ergeben hat.</summary>
    /// <param name="Betrag">Was der Knall bewegt: der Anteil der Feldpunkte.</param>
    /// <param name="Vorschlaege">Je Spieler sein Vorschlag mit Grund (E1); das Minus schon gekappt (E2).</param>
    public sealed record KartoffelErgebnis(KartoffelKnall? Knall, int Betrag, IReadOnlyDictionary<Guid, Punktevorschlag> Vorschlaege)
    {
        public int PunkteVon(Guid spieler) => Vorschlaege.GetValueOrDefault(spieler)?.Punkte ?? 0;
    }

    /// <summary>
    /// Die Heisse Kartoffel (M35 C6): wer beim Knall haelt, bekommt −B, wer sie ihm zugeschoben hat, +B. Beim Starthalter
    /// (und wenn der Spielleiter sie hingelegt hat) gibt es keinen Zuschieber - dann nur das Minus (Annahme C6b: auch er
    /// hatte jede Gelegenheit, richtig zu antworten). Das Minus wird nach E2 gekappt, das Plus nie - wie beim Raeuber.
    /// </summary>
    public static class KartoffelEvaluator
    {
        /// <summary>
        /// Plausibilitaetsgrenze am Server, je Spieler: wer eine Aussage liest, schafft kaum mehr als drei Antworten in zwei
        /// Sekunden. Ein Skript, das blind raet, kaeme sonst nach einem Augenblick an eine richtige und gaebe weiter.
        /// </summary>
        public const int MaxAntwortenJeFenster = 3;

        /// <summary>Das Fenster zu <see cref="MaxAntwortenJeFenster"/>.</summary>
        public const int FensterSekunden = 2;

        /// <summary>
        /// Wann der Zuender hochgehen kann: zwischen einem Drittel und der ganzen eingestellten Zeit, gezaehlt ab dem Ende des
        /// Countdowns. Mit einer Untergrenze kommt jeder ein paar Mal dran; ohne sie koennte es schon nach einer Sekunde knallen.
        /// </summary>
        public static (TimeSpan Kuerzeste, TimeSpan Laengste) Zuendbereich(int laengsteSekunden)
        {
            var laengste = TimeSpan.FromSeconds(Math.Max(1, laengsteSekunden));
            return (laengste / 3, laengste);
        }

        /// <summary>Der Vorschlag fuer den Knall; ohne Knall (Runde von Hand geschlossen) bewegt sich nichts.</summary>
        /// <param name="betrag">Der Anteil der Feldpunkte, <see cref="Punkteanteil.Von"/>.</param>
        public static KartoffelErgebnis Werte(KartoffelKnall? knall, int betrag, Kontostaende konten, Func<Guid, string> name)
        {
            ArgumentNullException.ThrowIfNull(konten);
            ArgumentNullException.ThrowIfNull(name);

            var vorschlaege = new Dictionary<Guid, Punktevorschlag>();

            if (knall == null || betrag <= 0)
                return new(knall, betrag, vorschlaege);

            var minus = konten.Kappe(knall.Halter, -betrag);
            vorschlaege[knall.Halter] = new(minus, $"Heiße Kartoffel: {Describe(knall, knall.Halter, name)} {Punkteanteil.Text(minus)}"
                                                   + (minus != -betrag ? Punkteanteil.GekapptText : string.Empty));

            // Sich selbst zuschieben prueft schon der Server - hier faellt es trotzdem heraus.
            if (knall.Zuschieber is { } von && von != knall.Halter)
                vorschlaege[von] = new(betrag, $"Heiße Kartoffel: {Describe(knall, von, name)} {Punkteanteil.Text(betrag)}");

            return new(knall, betrag, vorschlaege);
        }

        /// <summary>Was der Knall fuer diesen Spieler bedeutet - fuer Beamer, Karte und Grund dieselben Worte.</summary>
        public static string Describe(KartoffelKnall? knall, Guid spieler, Func<Guid, string> name)
        {
            ArgumentNullException.ThrowIfNull(name);

            if (knall == null)
                return "kein Knall";

            if (spieler == knall.Halter)
                return knall.Zuschieber is { } von && von != spieler ? $"beim Knall gehalten – von {name(von)} zugeschoben" : "beim Knall gehalten";

            return spieler == knall.Zuschieber ? $"rechtzeitig an {name(knall.Halter)} weitergegeben" : "nicht betroffen";
        }

        /// <summary>Der Satz auf der Spielerkarte.</summary>
        public static string Karte(KartoffelErgebnis ergebnis, Guid spieler, Func<Guid, string> name)
        {
            ArgumentNullException.ThrowIfNull(ergebnis);

            return $"{Describe(ergebnis.Knall, spieler, name)} – {Punkteanteil.Text(ergebnis.PunkteVon(spieler))}";
        }

        /// <summary>Die Kopfzeile am Beamer nach der Runde.</summary>
        public static string Ausgang(KartoffelRunde runde, Func<Guid, string> name)
        {
            ArgumentNullException.ThrowIfNull(runde);
            ArgumentNullException.ThrowIfNull(name);

            if (runde.Knall is not { } knall)
                return "Heiße Kartoffel – vorbei, ohne Knall";

            var wann = runde.ZuenderSekunden is { } sekunden ? $" nach {sekunden} s" : string.Empty;
            return $"BUMM{wann}! Die Kartoffel ist bei {name(knall.Halter)} geplatzt";
        }
    }
}
