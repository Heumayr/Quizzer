using Quizzer.DataModels.Questions.Minispiele;

namespace Quizzer.DataModels.Questions.Sonderregeln
{
    /// <summary>
    /// Sonderregel Kopfgeld (M35 C3b), ohne Vorphase: gesucht sind alle mit dem hoechsten Kontostand beim Oeffnen
    /// (alle gleichauf: kein Kopfgeld), Jaeger alle anderen. Je Paar - Jaeger richtig, Gesuchter nicht richtig - wandert
    /// das Kopfgeld vom Gesuchten zum Jaeger. Das Minus des Gesuchten wird insgesamt einmal gekappt (E2), ein Plus nie.
    /// </summary>
    public static class KopfgeldRegel
    {
        /// <summary>Die Gesuchten; leer, wenn alle gleichauf sind oder niemand spielt.</summary>
        public static IReadOnlyList<Guid> Gesuchte(IReadOnlyDictionary<Guid, int> kontostaende)
        {
            ArgumentNullException.ThrowIfNull(kontostaende);

            if (kontostaende.Count == 0)
                return [];

            var hoechster = kontostaende.Values.Max();
            var gesucht = kontostaende.Where(e => e.Value == hoechster).Select(e => e.Key).ToList();

            return gesucht.Count == kontostaende.Count ? [] : gesucht;
        }

        /// <summary>Die Vorschlaege fuer Jaeger und Gesuchte, die ein Paar bilden; alle anderen spielen wie immer.</summary>
        /// <param name="gesuchte">Aus <see cref="Gesuchte"/> beim Oeffnen.</param>
        /// <param name="eingaenge">Die Bewertungen aller Karten.</param>
        /// <param name="kopfgeld">Was je Paar wandert - der Anteil der Feldpunkte.</param>
        /// <param name="nameVon">Namen fuer die Gruende.</param>
        public static IReadOnlyDictionary<Guid, Punktevorschlag> Werte(
            IReadOnlyCollection<Guid> gesuchte, IReadOnlyDictionary<Guid, SonderregelEingang> eingaenge, int kopfgeld, Func<Guid, string> nameVon)
        {
            ArgumentNullException.ThrowIfNull(gesuchte);
            ArgumentNullException.ThrowIfNull(eingaenge);
            ArgumentNullException.ThrowIfNull(nameVon);

            var vorschlaege = new Dictionary<Guid, Punktevorschlag>();
            bool Richtig(Guid id) => eingaenge.GetValueOrDefault(id)?.Bewertung == RatingKind.Right;

            var jaeger = eingaenge.Keys.Where(id => !gesuchte.Contains(id) && Richtig(id)).ToList();
            var opfer = gesuchte.Where(id => eingaenge.ContainsKey(id) && !Richtig(id)).ToList();

            if (kopfgeld <= 0 || jaeger.Count == 0 || opfer.Count == 0)
                return vorschlaege;

            var beute = opfer.Count * kopfgeld;
            // Je Gesuchter, bei dem der Jaeger kassiert, ein Klau (M35 C9).
            foreach (var id in jaeger)
                vorschlaege[id] = new(eingaenge[id].Grundwert + beute, $"Kopfgeld von {Namen(opfer, nameVon)} ({Punkteanteil.Text(beute)})", opfer.Count);

            var verlust = jaeger.Count * kopfgeld;
            foreach (var id in opfer)
                vorschlaege[id] = Sonderregelwertung.Gekappt(eingaenge[id].Grundwert - verlust,
                    $"Kopfgeld an {Namen(jaeger, nameVon)} ({Punkteanteil.Text(-verlust)})", eingaenge[id].Konto);

            return vorschlaege;
        }

        private static string Namen(IEnumerable<Guid> ids, Func<Guid, string> nameVon) => string.Join(", ", ids.Select(nameVon));
    }
}
