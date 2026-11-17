using Quizzer.DataModels.Anzeige;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;

namespace Quizzer.Logic.Statistics
{
    /// <summary>Was in den Rückblick auf ein Spiel geht (M37 E04).</summary>
    /// <param name="Bestwertzeilen">Wie <see cref="LeaderboardRows"/> sie liefert; Zeilen anderer Spiele zählen nicht.</param>
    /// <param name="Ergebnisse">Die Ergebniszeilen des Spiels - für die richtigen Antworten.</param>
    /// <param name="Joker">Die Joker des Spiels; nur eingelöste zählen.</param>
    /// <param name="Namen">Der Anzeigename je Spieler-Id, für Ergebnisse und Joker.</param>
    public sealed record RueckblickDaten(
        Guid GameId,
        IReadOnlyList<LeaderboardRow> Bestwertzeilen,
        IReadOnlyList<QuestionResult> Ergebnisse,
        IReadOnlyList<PlayerJoker> Joker,
        IReadOnlyDictionary<Guid, string> Namen);

    /// <summary>Der beste Wert des Abends in einer Liste der Bestenliste, etwa „Schnellste Reaktion: Anna – 187 ms".</summary>
    /// <param name="PlayerIds">Bei Gleichstand mehrere, in der Reihenfolge der Bestenliste.</param>
    public sealed record RueckblickBestwert(string Titel, IReadOnlyList<Guid> PlayerIds, string Namen, string WertText)
    {
        public string Zeile => $"{Titel}: {Namen} – {WertText}";
    }

    /// <summary>Wer am meisten hat - Klaus oder richtige Antworten; bei Gleichstand alle.</summary>
    public sealed record RueckblickSpitze(IReadOnlyList<Guid> PlayerIds, int Anzahl, string Zeile);

    /// <summary>Ein eingelöster Joker: wer welchen, und bei der Sperre wen sie traf.</summary>
    public sealed record RueckblickJoker(Guid PlayerId, JokerKind Art, Guid? ZielId, string Zeile);

    /// <summary>Der Rückblick; ein leerer Abschnitt ist eine leere Liste bzw. <c>null</c>.</summary>
    public sealed record Rueckblick(
        IReadOnlyList<RueckblickBestwert> Bestwerte,
        RueckblickSpitze? MeisteKlaus,
        IReadOnlyList<RueckblickJoker> Joker,
        RueckblickSpitze? MeisteRichtige)
    {
        public bool IstLeer => Bestwerte.Count == 0 && MeisteKlaus == null && Joker.Count == 0 && MeisteRichtige == null;
    }

    /// <summary>
    /// Der Rückblick auf ein Spiel in der Siegerehrung (M37 E04) als reine Rechnung.
    /// <list type="bullet">
    /// <item>Bestwerte und Klaus über <see cref="LeaderboardCalculator"/>, nur mit den Zeilen dieses Spiels - Richtung,
    /// Plausibilität, Spielweise und Gleichstand wie in der Bestenliste, keine zweite Rechnung.</item>
    /// <item>Richtige Antworten: Summe von <c>RightCount</c> je Spieler, nur wer mindestens eine hat.</item>
    /// <item>Joker: nur eingelöste, in der Reihenfolge des Einlösens.</item>
    /// </list>
    /// </summary>
    public static class Abendrueckblick
    {
        public static Rueckblick Rechne(RueckblickDaten daten)
        {
            ArgumentNullException.ThrowIfNull(daten);

            var liste = LeaderboardCalculator.Calculate(daten.Bestwertzeilen.Where(z => z.GameId == daten.GameId));

            return new Rueckblick(Bestwerte(liste), MeisteKlaus(liste), Joker(daten), MeisteRichtige(daten));
        }

        private static List<RueckblickBestwert> Bestwerte(DataModels.Transfer.Server.Leaderboard liste)
            => liste.Categories
                .Where(c => c.Entries.Count > 0)
                .Select(c =>
                {
                    var erste = c.Entries.Where(e => e.Rank == 1).ToList();
                    return new RueckblickBestwert(c.Title, erste.Select(e => e.PlayerId).ToList(),
                        Rekordpruefung.Aufzaehlung(erste.Select(e => e.PlayerName).ToList()), erste[0].ValueText);
                })
                .ToList();

        private static RueckblickSpitze? MeisteKlaus(DataModels.Transfer.Server.Leaderboard liste)
        {
            var erste = liste.MostSteals.Where(s => s.Rank == 1).ToList();
            if (erste.Count == 0)
                return null;

            var anzahl = erste[0].Steals;
            var namen = Rekordpruefung.Aufzaehlung(erste.Select(s => s.PlayerName).ToList());
            return new RueckblickSpitze(erste.Select(s => s.PlayerId).ToList(), anzahl, $"{namen} – {anzahl} {(anzahl == 1 ? "Klau" : "Klaus")}");
        }

        private static RueckblickSpitze? MeisteRichtige(RueckblickDaten daten)
        {
            var summen = daten.Ergebnisse
                .Where(r => r.GameId == daten.GameId)
                .GroupBy(r => r.PlayerId)
                .Select(g => (Spieler: g.Key, Name: Name(daten, g.Key), Richtige: g.Sum(r => r.RightCount)))
                .Where(s => s.Richtige > 0)
                .ToList();

            if (summen.Count == 0)
                return null;

            var meiste = summen.Max(s => s.Richtige);
            var erste = summen.Where(s => s.Richtige == meiste).OrderBy(s => s.Name, StringComparer.Ordinal).ThenBy(s => s.Spieler).ToList();
            var namen = Rekordpruefung.Aufzaehlung(erste.Select(s => s.Name).ToList());

            return new RueckblickSpitze(erste.Select(s => s.Spieler).ToList(), meiste,
                $"{namen} – {meiste} {(meiste == 1 ? "richtige Antwort" : "richtige Antworten")}");
        }

        private static List<RueckblickJoker> Joker(RueckblickDaten daten)
            => daten.Joker
                .Where(j => j.GameId == daten.GameId && j.IsRedeemed)
                .OrderBy(j => j.RedeemedAt ?? DateTimeOffset.MaxValue)
                .ThenBy(j => Name(daten, j.PlayerId), StringComparer.Ordinal)
                .ThenBy(j => j.Id)
                .Select(j =>
                {
                    // Nur die Sperre hat ein Ziel (WriteRules, RedeemAsync) - ein Ziel an einem anderen Joker bleibt ungenannt.
                    var ziel = j.Kind == JokerKind.Block ? j.TargetPlayerId : null;
                    var text = $"{Name(daten, j.PlayerId)}: {JokerTexte.Name(j.Kind)}";
                    return new RueckblickJoker(j.PlayerId, j.Kind, ziel, ziel is { } z ? $"{text} gegen {Name(daten, z)}" : text);
                })
                .ToList();

        private static string Name(RueckblickDaten daten, Guid spieler) => daten.Namen.GetValueOrDefault(spieler, "?");
    }
}
