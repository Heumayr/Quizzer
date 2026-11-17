using Quizzer.DataModels.Models.Base;

namespace Quizzer.Logic.Statistics
{
    /// <summary>Eine Linie des Verlaufs: der Kontostand vor der ersten und nach jeder gespielten Zelle.</summary>
    public sealed record VerlaufReihe(Guid PlayerId, string Name, IReadOnlyList<int> Punkte)
    {
        public int Endstand => Punkte[^1];
    }

    /// <summary>Der Punkteverlauf eines Spiels (M37 E06); <see cref="Zellen"/> in der Reihenfolge, in der sie gespielt wurden.</summary>
    public sealed record Verlauf(IReadOnlyList<Guid> Zellen, IReadOnlyList<VerlaufReihe> Reihen)
    {
        public int Minimum => Reihen.SelectMany(r => r.Punkte).DefaultIfEmpty(0).Min();

        public int Maximum => Reihen.SelectMany(r => r.Punkte).DefaultIfEmpty(0).Max();
    }

    /// <summary>
    /// Der Punkteverlauf am Spielende (M37 E06) als reine Rechnung: je Spieler der Kontostand über die gespielten Zellen, in
    /// der Reihenfolge von <see cref="GameGridCoordinate.PlayedAt"/>.
    /// <list type="bullet">
    /// <item>Gespielt ist eine Zelle mit Frage und <c>IsDone</c>; Ergebnisse an anderen Zellen zählen nicht.</item>
    /// <item>Fehlt einer gespielten Zelle der Zeitpunkt (vor E06 gespielt), gibt es keinen Verlauf - eine erfundene
    /// Reihenfolge wäre eine falsche Geschichte des Abends.</item>
    /// <item>Gleicher Zeitpunkt: nach Rasterstelle, damit die Linie fest bleibt.</item>
    /// </list>
    /// </summary>
    public static class Punkteverlauf
    {
        /// <returns><c>null</c> ohne gespielte Zelle oder wenn einer gespielten der Zeitpunkt fehlt.</returns>
        public static Verlauf? Rechne(IEnumerable<GameGridCoordinate> zellen, IEnumerable<QuestionResult> ergebnisse, IEnumerable<Player> spieler)
        {
            ArgumentNullException.ThrowIfNull(zellen);
            ArgumentNullException.ThrowIfNull(ergebnisse);
            ArgumentNullException.ThrowIfNull(spieler);

            var gespielt = zellen.Where(z => z.IsDone && z.HatFrage).ToList();
            if (gespielt.Count == 0 || gespielt.Any(z => z.PlayedAt == null))
                return null;

            var reihenfolge = gespielt
                .OrderBy(z => z.PlayedAt)
                .ThenBy(z => z.Y).ThenBy(z => z.X).ThenBy(z => z.Z).ThenBy(z => z.Id)
                .ToList();

            var jeZelle = ergebnisse
                .GroupBy(r => (Zelle: r.GameGridCoordinateId, Spieler: r.PlayerId))
                .ToDictionary(g => g.Key, g => g.Sum(r => r.FinalScore));

            var reihen = spieler
                .Select(p => new VerlaufReihe(p.Id, p.CalculatedDisplayName, Stand(p.Id, reihenfolge, jeZelle)))
                .OrderByDescending(r => r.Endstand)
                .ThenBy(r => r.Name, StringComparer.Ordinal)
                .ThenBy(r => r.PlayerId)
                .ToList();

            return new Verlauf(reihenfolge.Select(z => z.Id).ToList(), reihen);
        }

        private static List<int> Stand(Guid spieler, List<GameGridCoordinate> reihenfolge, Dictionary<(Guid Zelle, Guid Spieler), int> jeZelle)
        {
            var stand = 0;
            var punkte = new List<int> { 0 };

            foreach (var zelle in reihenfolge)
            {
                stand += jeZelle.GetValueOrDefault((zelle.Id, spieler));
                punkte.Add(stand);
            }

            return punkte;
        }
    }
}
