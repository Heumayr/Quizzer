namespace Quizzer.DataModels.Questions.Minispiele
{
    /// <summary>Wie weit ein Spieler beim Kopfrechnen ist.</summary>
    /// <param name="Richtig">So viele Antworten waren richtig.</param>
    /// <param name="Beantwortet">So viele Aufgaben hat er beantwortet, richtig oder falsch.</param>
    public sealed record KopfrechnenStand(int Richtig, int Beantwortet);

    /// <summary>Eine angenommene Antwort, gemeldet an den Spielleiter - ohne die Loesung.</summary>
    /// <param name="Aufgabe">Die Nummer der beantworteten Aufgabe.</param>
    /// <param name="Richtig">Ob sie richtig war.</param>
    /// <param name="Stand">Der Stand des Spielers danach.</param>
    public sealed record KopfrechnenAntwort(int Aufgabe, bool Richtig, KopfrechnenStand Stand);

    /// <summary>Was das Kopfrechnen ergeben hat.</summary>
    /// <param name="Staende">Je Spieler mit mindestens einer Antwort sein Stand.</param>
    /// <param name="Gewinner">Die meisten richtigen; leer, wenn keiner eine richtig hat.</param>
    /// <param name="Vorschlaege">Die Punkte der Gewinner, mit Grund (E1).</param>
    public sealed record KopfrechnenErgebnis(
        IReadOnlyDictionary<Guid, KopfrechnenStand> Staende, IReadOnlySet<Guid> Gewinner,
        IReadOnlyDictionary<Guid, Punktevorschlag> Vorschlaege)
    {
        public bool IstGewinner(Guid spieler) => Gewinner.Contains(spieler);

        public KopfrechnenStand? StandVon(Guid spieler) => Staende.GetValueOrDefault(spieler);
    }

    /// <summary>
    /// Das Kopfrechnen (M35 C5): die meisten richtigen Antworten in der Zeit gewinnen die Feldpunkte, bei
    /// Gleichstand alle. Eine falsche Antwort kostet nur Zeit - die naechste Aufgabe kommt trotzdem, so
    /// bringt Raten an derselben Aufgabe nichts.
    /// </summary>
    public static class KopfrechnenEvaluator
    {
        /// <summary>
        /// Plausibilitaetsgrenze am Server: lesen, rechnen, tippen und senden dauert laenger als eine
        /// Drittelsekunde; ein Skript wird hier gebremst, ein Mensch nie.
        /// </summary>
        public const int MaxAntwortenJeSekunde = 3;

        /// <summary>Die meisten richtigen gewinnen; null richtige gewinnen nie.</summary>
        public static IReadOnlySet<Guid> Gewinner(IReadOnlyDictionary<Guid, KopfrechnenStand> staende)
        {
            ArgumentNullException.ThrowIfNull(staende);

            var beste = staende.Values.Select(s => s.Richtig).DefaultIfEmpty(0).Max();

            return beste > 0
                ? staende.Where(s => s.Value.Richtig == beste).Select(s => s.Key).ToHashSet()
                : new HashSet<Guid>();
        }

        /// <summary>Die Wertung: jeder Gewinner bekommt die Feldpunkte als Vorschlag mit Grund; ein Plus wird nie gekappt (E2).</summary>
        public static KopfrechnenErgebnis Werte(
            IReadOnlyDictionary<Guid, KopfrechnenStand> staende, int feldpunkte, Kontostaende konten)
        {
            ArgumentNullException.ThrowIfNull(konten);

            var gewinner = Gewinner(staende);
            var vorschlaege = new Dictionary<Guid, Punktevorschlag>();

            foreach (var id in gewinner)
            {
                var punkte = konten.Kappe(id, feldpunkte);

                if (punkte != 0)
                    vorschlaege[id] = new(punkte, $"Kopfrechnen: {Describe(staende[id])} – die meisten {Punkteanteil.Text(punkte)}");
            }

            return new(staende, gewinner, vorschlaege);
        }

        /// <summary>„7 von 9 richtig" - fuer Beamer, Spielerkarte und Buzzer-Fenster dieselben Worte.</summary>
        public static string Describe(KopfrechnenStand stand)
        {
            ArgumentNullException.ThrowIfNull(stand);

            return $"{stand.Richtig} von {stand.Beantwortet} richtig";
        }

        /// <summary>Der Satz auf der Spielerkarte.</summary>
        public static string Describe(KopfrechnenErgebnis ergebnis, Guid spieler)
        {
            ArgumentNullException.ThrowIfNull(ergebnis);

            if (ergebnis.StandVon(spieler) is not { } stand)
                return "Keine Antwort – von Hand werten";

            return Describe(stand) + (ergebnis.IstGewinner(spieler) ? " – die meisten" : " – nicht die meisten");
        }
    }
}
