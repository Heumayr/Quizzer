namespace Quizzer.DataModels.Questions.Minispiele
{
    /// <summary>Eine Antwort, die in Phase 2 des Bluffs zur Wahl stand.</summary>
    /// <param name="Nummer">Ihre Nummer am Telefon - die Stelle in der gemischten Liste, ab 1.</param>
    /// <param name="Text">Wie sie am Telefon steht.</param>
    /// <param name="Echt">Die richtige Antwort aus den Zeilen.</param>
    /// <param name="Urheber">Wer diesen Bluff geschrieben hat - gleiche Bluffs sind zusammengefasst, alle zaehlen.</param>
    /// <param name="Gestrichen">Der Bluff glich der echten Antwort - er stand nicht zur Wahl und bringt nichts.</param>
    public sealed record BluffOption(int Nummer, string Text, bool Echt, IReadOnlyList<Guid> Urheber, bool Gestrichen = false);

    /// <summary>Was die Wahl ergeben hat: alle Antworten (auch die gestrichenen) und die Wahl je Spieler.</summary>
    public sealed record BluffRunde(IReadOnlyList<BluffOption> Optionen, IReadOnlyDictionary<Guid, int> Wahl);

    /// <summary>Eine angenommene Abgabe, gemeldet an den Spielleiter - ohne Text und ohne Wahl, die bleiben am Server.</summary>
    /// <param name="Phase">1 = Bluff geschrieben, 2 = gewaehlt.</param>
    public sealed record BluffAbgabe(int Phase);

    /// <summary>Was der Bluff ergeben hat.</summary>
    public sealed record BluffErgebnis(
        BluffRunde Runde, int PunkteEcht, int PunkteReinfall, IReadOnlyDictionary<Guid, Punktevorschlag> Vorschlaege)
    {
        public int PunkteVon(Guid spieler) => Vorschlaege.GetValueOrDefault(spieler)?.Punkte ?? 0;

        /// <summary>Ob dieser Spieler die echte Antwort gewaehlt hat.</summary>
        public bool Gefunden(Guid spieler)
            => Runde.Wahl.TryGetValue(spieler, out var nummer) && Option(nummer) is { Echt: true };

        /// <summary>So viele sind auf einen Bluff dieses Spielers hereingefallen.</summary>
        public int Reinfaelle(Guid spieler)
            => Runde.Wahl.Count(w => w.Key != spieler && Option(w.Value) is { Echt: false, Gestrichen: false } o && o.Urheber.Contains(spieler));

        /// <summary>Die meisten Punkte; leer, wenn keiner welche hat.</summary>
        public IReadOnlySet<Guid> Gewinner
        {
            get
            {
                var beste = Vorschlaege.Values.Select(v => v.Punkte).DefaultIfEmpty(0).Max();
                return beste > 0 ? Vorschlaege.Where(v => v.Value.Punkte == beste).Select(v => v.Key).ToHashSet() : new HashSet<Guid>();
            }
        }

        public BluffOption? Option(int nummer) => Runde.Optionen.FirstOrDefault(o => o.Nummer == nummer);
    }

    /// <summary>
    /// Der Bluff (M35 C6): in Phase 1 schreibt jeder eine falsche Antwort, in Phase 2 waehlt jeder unter allen Bluffs und
    /// der echten. Wer die echte findet, bekommt die Feldpunkte; je Mitspieler, der auf den eigenen Bluff hereinfaellt,
    /// die Haelfte davon (Annahme C6a). Gestrichene Bluffs bringen nichts, zusammengefasste zaehlen fuer jeden Urheber.
    /// Ein Minus gibt es nicht.
    /// </summary>
    public static class BluffEvaluator
    {
        /// <summary>Hoechstlaenge eines Bluffs - wie am Telefon.</summary>
        public const int MaxZeichen = 60;

        public static int PunkteEcht(int feldpunkte) => Math.Max(0, feldpunkte);

        public static int PunkteReinfall(int feldpunkte) => (int)Math.Round(Math.Max(0, feldpunkte) / 2.0, MidpointRounding.AwayFromZero);

        /// <summary>Die Wertung als Vorschlag mit Grund je Spieler (E1); gekappt wird nur ein Minus (E2).</summary>
        public static BluffErgebnis Werte(BluffRunde runde, int feldpunkte, Kontostaende konten)
        {
            ArgumentNullException.ThrowIfNull(runde);
            ArgumentNullException.ThrowIfNull(konten);

            var roh = new BluffErgebnis(runde, PunkteEcht(feldpunkte), PunkteReinfall(feldpunkte), new Dictionary<Guid, Punktevorschlag>());
            var vorschlaege = new Dictionary<Guid, Punktevorschlag>();
            var spieler = runde.Wahl.Keys.Concat(runde.Optionen.SelectMany(o => o.Urheber)).Distinct();

            foreach (var id in spieler)
            {
                var punkte = konten.Kappe(id, (roh.Gefunden(id) ? roh.PunkteEcht : 0) + roh.Reinfaelle(id) * roh.PunkteReinfall);

                if (punkte != 0)
                    vorschlaege[id] = new(punkte, $"Bluff: {Describe(roh, id)} {Punkteanteil.Text(punkte)}");
            }

            return roh with { Vorschlaege = vorschlaege };
        }

        /// <summary>„echte gefunden, 2 hereingelegt" - fuer Beamer, Spielerkarte und Grund dieselben Worte.</summary>
        public static string Describe(BluffErgebnis ergebnis, Guid spieler)
        {
            ArgumentNullException.ThrowIfNull(ergebnis);

            var gefunden = ergebnis.Gefunden(spieler) ? "echte gefunden" : "echte verfehlt";
            return $"{gefunden}, {ergebnis.Reinfaelle(spieler)} hereingelegt";
        }

        public static string Karte(BluffErgebnis ergebnis, Guid spieler)
            => $"{Describe(ergebnis, spieler)} – {Punkteanteil.Text(ergebnis.PunkteVon(spieler))}";

        /// <summary>Nach der Runde am Beamer: jede Antwort, wer sie schrieb und wer darauf hereinfiel - erst jetzt.</summary>
        public static IReadOnlyList<string> Aufloesung(BluffErgebnis ergebnis, Func<Guid, string> name)
        {
            ArgumentNullException.ThrowIfNull(ergebnis);
            ArgumentNullException.ThrowIfNull(name);

            string Waehler(BluffOption o)
            {
                var wer = ergebnis.Runde.Wahl.Where(w => w.Value == o.Nummer).Select(w => name(w.Key)).OrderBy(n => n, StringComparer.CurrentCulture).ToList();
                return wer.Count == 0 ? "niemand" : string.Join(", ", wer);
            }

            string Urheber(BluffOption o) => string.Join(", ", o.Urheber.Select(name).OrderBy(n => n, StringComparer.CurrentCulture));

            return ergebnis.Runde.Optionen
                .OrderByDescending(o => o.Echt)
                .ThenBy(o => o.Gestrichen)
                .ThenBy(o => o.Nummer)
                .Select(o => o.Echt ? $"✓ echt – {o.Text} – gefunden: {Waehler(o)}"
                    : o.Gestrichen ? $"gestrichen – {o.Text} (von {Urheber(o)})"
                    : $"Bluff von {Urheber(o)} – {o.Text} – hereingefallen: {Waehler(o)}")
                .ToList();
        }
    }
}
