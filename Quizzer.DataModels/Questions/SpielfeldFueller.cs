using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;

namespace Quizzer.DataModels.Questions
{
    /// <summary>
    /// Leere Zellen eines Spielfelds mit passenden Fragen belegen (M32, 2026-10-23) - ein
    /// <b>Vorschlag</b>, den der Spielleiter bestätigt; gesetzt wird hier nichts.
    /// <para>
    /// <b>Welche Kategorie eine Spalte hat:</b> die Spaltenüberschrift, wenn sie wie eine Kategorie
    /// heißt - sonst die Kategorie, die in der Spalte schon am häufigsten liegt. Eine Spalte ohne
    /// beides bleibt leer; quer über alle Kategorien zu würfeln ergäbe keine Jeopardy-Spalte.
    /// </para>
    /// <para>
    /// <b>Welche Frage in welche Zeile:</b> oben die leichten, unten die schweren - die Fragen der
    /// Kategorie werden nach Punkten und Schwierigkeit gereiht, und jede leere Zeile nimmt die,
    /// deren Rang ihrer Höhe im Feld am nächsten liegt. Nie: eine Frage, die schon im Feld liegt,
    /// und ein ungeprüfter KI-Entwurf.
    /// </para>
    /// </summary>
    public static class SpielfeldFueller
    {
        public sealed record Belegung(GameGridCoordinate Zelle, QuestionBase Frage);

        public sealed record Ergebnis(IReadOnlyList<Belegung> Belegungen, IReadOnlyList<string> Hinweise);

        public static Ergebnis Vorschlag(Game spiel, IEnumerable<QuestionBase> fragen, IEnumerable<Category> kategorien)
        {
            var belegt = spiel.GameGridCoordinates
                .Where(c => c.QuestionBaseId is { } id && id != Guid.Empty)
                .Select(c => c.QuestionBaseId!.Value)
                .ToHashSet();

            var frei = fragen
                .Where(q => q.Id != Guid.Empty && !belegt.Contains(q.Id) && !q.KiUngeprueft)
                .ToList();

            var nachName = kategorien
                .Where(k => !string.IsNullOrWhiteSpace(k.Designation))
                .GroupBy(k => k.Designation.Trim(), StringComparer.CurrentCultureIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.CurrentCultureIgnoreCase);

            var hoehe = Math.Max(1, spiel.GameGridCoordinates.Select(c => c.Y).DefaultIfEmpty(0).Max() + 1);
            var belegungen = new List<Belegung>();
            var hinweise = new List<string>();
            var vergeben = new HashSet<Guid>();

            foreach (var spalte in spiel.GameGridCoordinates.GroupBy(c => c.X).OrderBy(g => g.Key))
            {
                var leer = spalte.Where(c => c.QuestionBaseId is null || c.QuestionBaseId == Guid.Empty)
                    .OrderBy(c => c.Y).ThenBy(c => c.Z).ToList();

                if (leer.Count == 0)
                    continue;

                var kopf = spiel.Headers.FirstOrDefault(h => h.HeaderType == HeaderType.Column && h.Index == spalte.Key)?.Designation?.Trim();
                var name = string.IsNullOrWhiteSpace(kopf) ? $"Spalte {spalte.Key + 1}" : $"„{kopf}“";

                if (KategorieDerSpalte(kopf, spalte, nachName) is not { } kategorie)
                {
                    hinweise.Add($"{name}: keine Kategorie dieses Namens und noch keine Frage in der Spalte - bleibt leer.");
                    continue;
                }

                var reihe = frei
                    .Where(q => q.CategoryId == kategorie && !vergeben.Contains(q.Id))
                    .OrderBy(q => q.Points).ThenBy(q => q.Difficulty).ThenBy(q => q.Designation, StringComparer.CurrentCulture)
                    .ToList();

                var genommen = new HashSet<int>();

                foreach (var zelle in leer)
                {
                    if (genommen.Count == reihe.Count)
                        break;

                    // Wo die Zeile im Feld steht, so weit hinten in der Reihe wird gesucht.
                    var ziel = hoehe > 1 ? (double)zelle.Y / (hoehe - 1) * (reihe.Count - 1) : 0;
                    var index = Enumerable.Range(0, reihe.Count)
                        .Where(i => !genommen.Contains(i))
                        .OrderBy(i => Math.Abs(i - ziel)).ThenBy(i => i)
                        .First();

                    genommen.Add(index);
                    vergeben.Add(reihe[index].Id);
                    belegungen.Add(new Belegung(zelle, reihe[index]));
                }

                if (genommen.Count < leer.Count)
                    hinweise.Add($"{name}: nur {genommen.Count} von {leer.Count} leeren Zellen gefüllt - mehr freie Fragen dieser Kategorie gibt es nicht.");
            }

            return new Ergebnis(belegungen, hinweise);
        }

        private static Guid? KategorieDerSpalte(string? kopf, IEnumerable<GameGridCoordinate> spalte, IReadOnlyDictionary<string, Guid> nachName)
        {
            if (!string.IsNullOrWhiteSpace(kopf) && nachName.TryGetValue(kopf, out var id))
                return id;

            var haeufigste = spalte
                .Where(c => c.QuestionBase != null && c.QuestionBase.CategoryId != Guid.Empty)
                .GroupBy(c => c.QuestionBase!.CategoryId)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault();

            return haeufigste?.Key;
        }
    }
}
