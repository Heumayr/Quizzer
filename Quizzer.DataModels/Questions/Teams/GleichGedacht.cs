using Quizzer.DataModels.Questions.Minispiele;

namespace Quizzer.DataModels.Questions.Teams
{
    /// <summary>Gleich gedachte Antworten in einem Team: die Vergleichsform, was geschrieben wurde, und wer.</summary>
    /// <param name="Schluessel">Die Vergleichsform (<see cref="BuchstabensalatBegriffe.Normalisiere"/>) - oder die Gruppe, zu der die Spielleitung gelegt hat.</param>
    /// <param name="Texte">Was in dieser Gruppe geschrieben wurde, je Schreibweise einmal, in Sprecherfolge.</param>
    /// <param name="Mitglieder">Wer so geschrieben hat, in Sprecherfolge.</param>
    /// <param name="Zaehlt">Eine größte Gruppe ab <see cref="GleichGedacht.Mindestgroesse"/> Mitgliedern.</param>
    public sealed record GleichGedachtGruppe(string Schluessel, IReadOnlyList<string> Texte, IReadOnlyList<Guid> Mitglieder, bool Zaehlt);

    /// <summary>Die Gruppen eines Teams, größte zuerst, und je passendem Mitglied sein Anteil.</summary>
    public sealed record GleichGedachtErgebnis(IReadOnlyList<GleichGedachtGruppe> Gruppen, IReadOnlyDictionary<Guid, int> Anteile);

    /// <summary>
    /// „Gleich gedacht" (M37 T6, Entwurf Abschnitt 3, F-M37-12): jedes Mitglied schreibt für sich; Punkte bringt nur die
    /// Übereinstimmung, nicht ob es stimmt. Eine Gruppe zählt ab zwei Mitgliedern, und nur die größte - gleich große größte
    /// zählen alle. Je passendem Mitglied sein Anteil am Feldwert, aufgeteilt über das <b>ganze</b> Team; ein Minus gibt es
    /// nicht. Die Spielleitung kann Gruppen zusammenlegen (E1), etwa bei einem Tippfehler.
    /// </summary>
    public static class GleichGedacht
    {
        /// <summary>Ab so vielen gleichen Antworten zählt eine Gruppe - einer allein hat mit niemandem gleich gedacht.</summary>
        public const int Mindestgroesse = 2;

        /// <summary>Die Gruppen eines Teams und die Anteile der passenden Mitglieder.</summary>
        /// <param name="feldwert">Was die Zelle bringt; aufgeteilt über alle in <paramref name="folge"/>.</param>
        /// <param name="folge">Das Team in Sprecherfolge, auch wer nicht antworten konnte - der Rest der Aufteilung geht nach vorn.</param>
        /// <param name="antworten">Je Spieler seine Antwort; wer nicht zum Team gehört oder leer schreibt, ist in keiner Gruppe.</param>
        /// <param name="zusammengelegt">Je Mitglied die Gruppe, zu der die Spielleitung es gelegt hat (<see cref="LegeZusammen"/>).</param>
        public static GleichGedachtErgebnis Werte(
            int feldwert, IReadOnlyList<Guid> folge, IReadOnlyDictionary<Guid, string?> antworten, IReadOnlyDictionary<Guid, string>? zusammengelegt = null)
        {
            ArgumentNullException.ThrowIfNull(folge);
            ArgumentNullException.ThrowIfNull(antworten);

            var mitglieder = folge.Distinct().ToList();
            // Wer leer schreibt, ist in keiner Gruppe - auch nicht über ein Zusammenlegen: zwei leere Antworten sind nicht gleich gedacht.
            var geschrieben = mitglieder
                .Select(m => (Id: m, Text: antworten.GetValueOrDefault(m)?.Trim() ?? string.Empty))
                .Where(a => BuchstabensalatBegriffe.Normalisiere(a.Text).Length > 0)
                .Select(a => (a.Id, a.Text, Schluessel: zusammengelegt?.GetValueOrDefault(a.Id) ?? BuchstabensalatBegriffe.Normalisiere(a.Text)))
                .ToList();

            var gruppen = geschrieben.GroupBy(a => a.Schluessel).ToList();
            var groesste = gruppen.Select(g => g.Count()).DefaultIfEmpty(0).Max();
            var zaehlt = groesste >= Mindestgroesse ? groesste : int.MaxValue;

            var ergebnis = gruppen
                .Select(g => new GleichGedachtGruppe(
                    g.Key,
                    g.Select(a => a.Text).Where(t => t.Length > 0).Distinct().ToList(),
                    g.Select(a => a.Id).ToList(),
                    g.Count() == zaehlt))
                .OrderByDescending(g => g.Mitglieder.Count)
                .ThenBy(g => mitglieder.IndexOf(g.Mitglieder[0]))
                .ToList();

            // R3-3: ein negativer Feldwert bringt nichts - wie die Siegerteam-Arten; ein Minus gibt es hier nicht.
            var aufgeteilt = Aufteilung.Verteile(Math.Max(0, feldwert), mitglieder);
            var anteile = ergebnis.Where(g => g.Zaehlt).SelectMany(g => g.Mitglieder).ToDictionary(m => m, m => aufgeteilt[m]);

            return new(ergebnis, anteile);
        }

        /// <summary>
        /// Die Spielleitung legt Gruppen zusammen (E1): wer in einer der weiteren Gruppen steht, zählt ab jetzt zur ersten.
        /// Weniger als zwei Gruppen ändern nichts. Ob alle aus einem Team sind, prüft der Aufrufer.
        /// </summary>
        public static IReadOnlyDictionary<Guid, string> LegeZusammen(IReadOnlyList<GleichGedachtGruppe> gruppen, IReadOnlyDictionary<Guid, string>? bisher = null)
        {
            ArgumentNullException.ThrowIfNull(gruppen);

            var neu = bisher?.ToDictionary() ?? new Dictionary<Guid, string>();

            if (gruppen.Count < 2)
                return neu;

            foreach (var mitglied in gruppen.Skip(1).SelectMany(g => g.Mitglieder))
                neu[mitglied] = gruppen[0].Schluessel;

            return neu;
        }
    }
}
