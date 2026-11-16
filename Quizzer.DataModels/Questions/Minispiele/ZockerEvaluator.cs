using System.Globalization;

namespace Quizzer.DataModels.Questions.Minispiele
{
    /// <summary>Ein Ausstieg beim Zocker: der Multiplikator in Hundertsteln (185 = ×1,85), gemessen an der Serveruhr.</summary>
    public sealed record ZockerAusstieg(int Multiplikator);

    /// <summary>Was der Zocker fuer die Wertung braucht.</summary>
    /// <param name="Feldpunkte">Was „richtig" in der Zelle bringt.</param>
    /// <param name="Einsatz">Nur in der Spielweise mit Einsatz: was jeder riskiert; sonst <c>null</c>.</param>
    /// <param name="Konten">Fuer die Kappung (E2).</param>
    public sealed record ZockerWertung(int Feldpunkte, int? Einsatz, Kontostaende Konten);

    /// <summary>Was der Zocker ergeben hat.</summary>
    /// <param name="Teilnehmer">Wer beim Start verbunden war und damit drin ist.</param>
    /// <param name="Ausstiege">Je Ausgestiegenem sein Multiplikator in Hundertsteln.</param>
    /// <param name="Absturz">Der Absturzpunkt in Hundertsteln - erst nach der Runde bekannt.</param>
    /// <param name="Vorschlaege">Je Spieler mit Bewegung der Vorschlag, schon gekappt.</param>
    public sealed record ZockerErgebnis(
        IReadOnlyCollection<Guid> Teilnehmer, IReadOnlyDictionary<Guid, int> Ausstiege, int? Absturz,
        IReadOnlyDictionary<Guid, Punktevorschlag> Vorschlaege)
    {
        /// <summary>Wer am hoechsten ausgestiegen ist - bei Gleichstand alle.</summary>
        public bool IstGewinner(Guid spieler)
            => Ausstiege.TryGetValue(spieler, out var m) && m == Ausstiege.Values.Max();
    }

    /// <summary>
    /// Der Zocker (M35 C4): der Multiplikator steigt ab ×1,00 nach <see cref="Multiplikator"/>, bis er am
    /// geheimen Absturzpunkt stehen bleibt. Gerechnet wird in Hundertsteln, damit Server, Beamer und
    /// Karte dieselbe Zahl zeigen.
    /// <para>
    /// <b>Die Verteilung ist fair:</b> P(Absturz ≥ m) = 1/m bis zur Obergrenze ×10. Damit bringt jede
    /// Ausstiegsstrategie im Mittel dasselbe - es gibt keinen sicheren Trick, nur Nerven.
    /// </para>
    /// </summary>
    public static class ZockerEvaluator
    {
        /// <summary>Der Index der Spielweise „Mit Einsatz" in <see cref="MinigameProfiles"/>.</summary>
        public const int MitEinsatzVariant = 1;

        /// <summary>×1,00 - dort beginnt jede Runde.</summary>
        public const int Startwert = 100;

        /// <summary>×10 - spaetestens dort stuerzt es ab, die Runde dauert also hoechstens ~18 s.</summary>
        public const int Obergrenze = 1000;

        /// <summary>Wie schnell es steigt: e^(t / 8 s) - ×2 nach 5,5 s, ×10 nach 18,4 s.</summary>
        public static readonly TimeSpan Zeitkonstante = TimeSpan.FromSeconds(8);

        /// <summary>Der Bereich des Wurfs fuer <see cref="Absturz"/>: 0 bis ausschliesslich 10 000.</summary>
        public const int Wurfbereich = 10_000;

        private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

        /// <summary>Der Multiplikator nach dieser Laufzeit in Hundertsteln, abgerundet; vor dem Start ×1,00.</summary>
        public static int Multiplikator(TimeSpan laufzeit)
        {
            if (laufzeit <= TimeSpan.Zero)
                return Startwert;

            var wert = Math.Floor(Startwert * Math.Exp(laufzeit / Zeitkonstante));
            return (int)Math.Min(Obergrenze, wert);
        }

        /// <summary>
        /// Der Absturzpunkt aus einem Wurf in [0, <see cref="Wurfbereich"/>): 1/(1−U), oberhalb von ×10
        /// genau ×10. Ein Wurf ausserhalb wird an den Rand gelegt.
        /// </summary>
        public static int Absturz(int wurf)
        {
            var w = Math.Clamp(wurf, 0, Wurfbereich - 1);

            return w >= Wurfbereich - Wurfbereich / 10
                ? Obergrenze
                : (int)Math.Floor((double)Startwert * Wurfbereich / (Wurfbereich - w));
        }

        /// <summary>Ab dieser Laufzeit ist es abgestuerzt - davor liegt jeder Multiplikator unter dem Absturzpunkt.</summary>
        public static TimeSpan Absturzzeit(int absturz)
            => absturz <= Startwert ? TimeSpan.Zero : Zeitkonstante * Math.Log(absturz / (double)Startwert);

        /// <summary>„×1,85".</summary>
        public static string Text(int hundertstel) => "×" + (hundertstel / 100.0).ToString("0.00", Deutsch);

        /// <summary>
        /// Ohne Einsatz: wer ausgestiegen ist, bekommt Feldpunkte × Multiplikator, wer noch drin war, nichts.
        /// Mit Einsatz: Ausstieg bringt Einsatz × (Multiplikator − 1), der Absturz kostet den Einsatz -
        /// gekappt am Kontostand (E2). Wer nicht dabei war, bekommt keinen Vorschlag.
        /// </summary>
        public static ZockerErgebnis Werte(
            IReadOnlyCollection<Guid> teilnehmer, IReadOnlyDictionary<Guid, int> ausstiege, int? absturz, ZockerWertung wertung)
        {
            ArgumentNullException.ThrowIfNull(teilnehmer);
            ArgumentNullException.ThrowIfNull(ausstiege);
            ArgumentNullException.ThrowIfNull(wertung);

            var gueltig = ausstiege.Where(a => teilnehmer.Contains(a.Key)).ToDictionary(a => a.Key, a => a.Value);
            var vorschlaege = new Dictionary<Guid, Punktevorschlag>();

            foreach (var id in teilnehmer)
            {
                if (Vorschlag(id, gueltig, absturz, wertung) is { } vorschlag)
                    vorschlaege[id] = vorschlag;
            }

            return new(teilnehmer.ToList(), gueltig, absturz, vorschlaege);
        }

        private static Punktevorschlag? Vorschlag(Guid id, Dictionary<Guid, int> ausstiege, int? absturz, ZockerWertung wertung)
        {
            if (ausstiege.TryGetValue(id, out var m))
            {
                var gewinn = wertung.Einsatz is { } einsatz
                    ? Gerundet(einsatz * (m - Startwert))
                    : Gerundet(wertung.Feldpunkte * m);

                return gewinn == 0 ? null : new(gewinn, $"Zocker: bei {Text(m)} ausgestiegen {Punkteanteil.Text(gewinn)}");
            }

            if (wertung.Einsatz is not { } verloren || verloren == 0)
                return null;

            var gekappt = wertung.Konten.Kappe(id, -verloren);
            var wo = absturz is { } a ? $" bei {Text(a)}" : string.Empty;

            return new(gekappt, $"Zocker: abgestürzt{wo}, Einsatz verloren {Punkteanteil.Text(gekappt)}"
                                + (gekappt != -verloren ? Punkteanteil.GekapptText : string.Empty));
        }

        /// <summary>Punkte × Hundertstel / 100, kaufmaennisch gerundet.</summary>
        private static int Gerundet(int punkteMalHundertstel)
            => (int)Math.Round(punkteMalHundertstel / 100.0, MidpointRounding.AwayFromZero);

        /// <summary>Was ein Spieler getan hat - fuer Beamer und Spielerkarte dieselben Worte.</summary>
        public static string Describe(ZockerErgebnis ergebnis, Guid spieler)
        {
            ArgumentNullException.ThrowIfNull(ergebnis);

            if (ergebnis.Ausstiege.TryGetValue(spieler, out var m))
                return $"bei {Text(m)} ausgestiegen";

            if (!ergebnis.Teilnehmer.Contains(spieler))
                return "nicht dabei";

            return ergebnis.Absturz is { } a ? $"abgestürzt bei {Text(a)}" : "kein Ausstieg";
        }
    }
}
