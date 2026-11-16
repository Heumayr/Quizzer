namespace Quizzer.DataModels.Questions.Minispiele
{
    /// <summary>Was ein Spieler bei Raeuber und Waechter geheim waehlt: ein Opfer oder - ohne Opfer - das Schild.</summary>
    public sealed record RaeuberWahl(Guid? Opfer)
    {
        public bool Schild => Opfer == null;

        public static RaeuberWahl MitSchild { get; } = new((Guid?)null);

        public static RaeuberWahl Raub(Guid opfer) => new(opfer);
    }

    /// <summary>Ein Raubzug: wer bei wem, und ob das Schild des Opfers ihn abgewehrt hat.</summary>
    public sealed record Raubzug(Guid Dieb, Guid Opfer, bool Abgewehrt);

    /// <summary>Was Raeuber und Waechter ergeben hat.</summary>
    /// <param name="Wahlen">Die gueltigen Wahlen je Spieler.</param>
    /// <param name="Zuege">Jeder Raub fuer sich, in der Reihenfolge der Wahlen.</param>
    /// <param name="Vorschlaege">Je beteiligtem Spieler die Summe seiner Bewegungen, einmal gekappt.</param>
    public sealed record RaeuberErgebnis(
        IReadOnlyDictionary<Guid, RaeuberWahl> Wahlen,
        IReadOnlyList<Raubzug> Zuege,
        IReadOnlyDictionary<Guid, Punktevorschlag> Vorschlaege);

    /// <summary>
    /// Raeuber und Waechter (M35 C2): Raub ohne Schild - Dieb +B, Opfer −B; Raub auf ein Schild - der
    /// Dieb zahlt B an das Opfer. Mehrere Raeuber beim selben Opfer zaehlen je fuer sich.
    /// <para>
    /// <b>Erst die Summe je Spieler, dann einmal kappen (E2).</b> Die Wahlen fallen gleichzeitig; eine
    /// Kappung Raub fuer Raub haenge an einer Reihenfolge, die es nicht gibt, und je Raeuber bis zum
    /// Kontostand gekappt fiele das Opfer doch unter 0. Ein Plus wird nie gekappt - wie beim Duell
    /// bekommt der Dieb seine Beute, auch wenn das Opfer weniger hatte.
    /// </para>
    /// </summary>
    public static class RaeuberEvaluator
    {
        public static RaeuberErgebnis Werte(
            IReadOnlyDictionary<Guid, RaeuberWahl> wahlen, int beute, Kontostaende konten, Func<Guid, string> name)
        {
            ArgumentNullException.ThrowIfNull(wahlen);
            ArgumentNullException.ThrowIfNull(konten);
            ArgumentNullException.ThrowIfNull(name);

            // Sich selbst bestehlen prueft schon der Server - hier faellt es trotzdem heraus.
            var gueltig = wahlen.Where(w => w.Value.Opfer != w.Key).ToDictionary(w => w.Key, w => w.Value);

            var zuege = gueltig
                .Where(w => w.Value.Opfer.HasValue)
                .Select(w => new Raubzug(w.Key, w.Value.Opfer!.Value, gueltig.GetValueOrDefault(w.Value.Opfer.Value)?.Schild == true))
                .ToList();

            var teile = new Dictionary<Guid, List<(int Punkte, string Text)>>();

            void Buche(Guid spieler, int punkte, string text)
            {
                if (!teile.TryGetValue(spieler, out var liste))
                    teile[spieler] = liste = new();

                liste.Add((punkte, $"{text} {Punkteanteil.Text(punkte)}"));
            }

            foreach (var zug in zuege)
            {
                if (zug.Abgewehrt)
                {
                    Buche(zug.Dieb, -beute, $"am Schild von {name(zug.Opfer)} gescheitert");
                    Buche(zug.Opfer, beute, $"Schild hielt gegen {name(zug.Dieb)}");
                }
                else
                {
                    Buche(zug.Dieb, beute, $"{name(zug.Opfer)} bestohlen");
                    Buche(zug.Opfer, -beute, $"von {name(zug.Dieb)} bestohlen");
                }
            }

            var vorschlaege = teile.ToDictionary(t => t.Key,
                t => Vorschlag(t.Key, t.Value, konten, zuege.Count(z => z.Dieb == t.Key && !z.Abgewehrt)));

            return new(gueltig, zuege, vorschlaege);
        }

        /// <summary>Ein gelungener Raub ist ein Klau (M35 C9); am Schild gescheitert nicht.</summary>
        private static Punktevorschlag Vorschlag(Guid spieler, List<(int Punkte, string Text)> teile, Kontostaende konten, int klaus)
        {
            var summe = teile.Sum(t => t.Punkte);
            var gekappt = konten.Kappe(spieler, summe);

            return new(gekappt, "Räuber und Wächter: " + string.Join(", ", teile.Select(t => t.Text))
                                + (gekappt != summe ? Punkteanteil.GekapptText : string.Empty), klaus);
        }

        /// <summary>Was ein Spieler gewaehlt hat und was daraus wurde - fuer Beamer und Spielerkarte dieselben Worte.</summary>
        public static string Describe(RaeuberErgebnis ergebnis, Guid spieler, Func<Guid, string> name)
        {
            ArgumentNullException.ThrowIfNull(ergebnis);
            ArgumentNullException.ThrowIfNull(name);

            if (!ergebnis.Wahlen.TryGetValue(spieler, out var wahl))
                return "keine Abgabe";

            if (wahl.Schild)
            {
                var angreifer = ergebnis.Zuege.Where(z => z.Opfer == spieler).Select(z => name(z.Dieb)).ToList();
                return angreifer.Count == 0 ? "Schild – niemand kam" : $"Schild – hielt gegen {string.Join(", ", angreifer)}";
            }

            var zug = ergebnis.Zuege.Single(z => z.Dieb == spieler);
            return zug.Abgewehrt
                ? $"bestiehlt {name(zug.Opfer)} – am Schild gescheitert"
                : $"bestiehlt {name(zug.Opfer)} – erfolgreich";
        }
    }
}
