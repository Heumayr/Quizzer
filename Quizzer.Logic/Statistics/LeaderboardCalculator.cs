using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Questions.Minispiele;
using Quizzer.DataModels.Transfer.Server;

namespace Quizzer.Logic.Statistics
{
    /// <summary>Eine Ergebniszeile fuer die Bestenliste - die Rechte hat der Aufrufer schon angewandt.</summary>
    /// <param name="Kind">Die Art der Frage, wenn sie ein Minispiel ist; sonst <c>null</c>.</param>
    public sealed record LeaderboardRow(
        Guid ResultId, Guid PlayerId, string PlayerName, Guid GameId, string GameName,
        MinigameKind? Kind, int? RecordValue, DateTimeOffset? RecordedAt, int Steals);

    /// <summary>
    /// Die Bestenliste ueber Abende (M35 C9) als reine Rechnung - Server und Tests benutzen dieselbe.
    /// <list type="bullet">
    /// <item>Je Art mit Bestwert die besten <see cref="Top"/> Spieler, je Spieler sein bester Wert; die Richtung legt
    /// <see cref="MinigameRecord.LowerIsBetter"/> fest. Ein Wert ausserhalb des Bereichs zaehlt nicht.</item>
    /// <item>Gleichstand teilt den Platz; die Reihenfolge darin ist fest: wer den Wert zuerst hatte, dann Name, dann Zeile.</item>
    /// <item>Meiste Klaus: Summe je Spieler ueber alle Zeilen, nur wer mindestens einen hat.</item>
    /// </list>
    /// </summary>
    public static class LeaderboardCalculator
    {
        /// <summary>So viele Eintraege zeigt jede Liste.</summary>
        public const int Top = 5;

        public static Leaderboard Calculate(IEnumerable<LeaderboardRow> rows)
        {
            ArgumentNullException.ThrowIfNull(rows);

            var zeilen = rows.ToList();
            var kategorien = MinigameProfiles.All
                .Where(p => p.Record != null)
                .Select(p => Kategorie(p, zeilen))
                .ToList();

            return new Leaderboard(kategorien, MeisteKlaus(zeilen));
        }

        /// <summary>Ein gueltiger Rohwert samt seiner Zeile.</summary>
        private sealed record Messung(LeaderboardRow Zeile, int Wert);

        private static LeaderboardCategory Kategorie(MinigameProfile profil, List<LeaderboardRow> zeilen)
        {
            var record = profil.Record ?? throw new ArgumentException("Art ohne Bestwert.", nameof(profil));

            var bestJeSpieler = zeilen
                .Where(z => z.Kind == profil.Kind && z.RecordValue.HasValue)
                .Select(z => new Messung(z, z.RecordValue.GetValueOrDefault()))
                .Where(m => record.IsPlausible(m.Wert))
                .GroupBy(m => m.Zeile.PlayerId)
                .Select(g => Ordne(g, record).First());

            var liste = Ordne(bestJeSpieler, record).Take(Top).ToList();
            var eintraege = liste
                .Select(m => new LeaderboardEntry(
                    1 + liste.Count(a => Besser(a.Wert, m.Wert, record)),
                    m.Zeile.PlayerId, m.Zeile.PlayerName, m.Wert, record.Format(m.Wert),
                    m.Zeile.GameId, m.Zeile.GameName, m.Zeile.RecordedAt))
                .ToList();

            return new LeaderboardCategory(profil.Kind, record.Title, profil.DisplayName, record.Unit, record.LowerIsBetter, eintraege);
        }

        private static IOrderedEnumerable<Messung> Ordne(IEnumerable<Messung> messungen, MinigameRecord record)
        {
            var nachWert = record.LowerIsBetter
                ? messungen.OrderBy(m => m.Wert)
                : messungen.OrderByDescending(m => m.Wert);

            return nachWert
                .ThenBy(m => m.Zeile.RecordedAt ?? DateTimeOffset.MaxValue)
                .ThenBy(m => m.Zeile.PlayerName, StringComparer.Ordinal)
                .ThenBy(m => m.Zeile.ResultId);
        }

        private static bool Besser(int a, int b, MinigameRecord record) => record.LowerIsBetter ? a < b : a > b;

        private static List<StealsEntry> MeisteKlaus(List<LeaderboardRow> zeilen)
        {
            var summen = zeilen
                .GroupBy(z => z.PlayerId)
                .Select(g => (Spieler: g.Key, Name: g.First().PlayerName, Klaus: g.Sum(z => z.Steals)))
                .Where(s => s.Klaus > 0)
                .OrderByDescending(s => s.Klaus)
                .ThenBy(s => s.Name, StringComparer.Ordinal)
                .ThenBy(s => s.Spieler)
                .Take(Top)
                .ToList();

            return summen
                .Select(s => new StealsEntry(1 + summen.Count(a => a.Klaus > s.Klaus), s.Spieler, s.Name, s.Klaus))
                .ToList();
        }
    }
}
