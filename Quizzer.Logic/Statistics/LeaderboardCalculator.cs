using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Questions.Minispiele;
using Quizzer.DataModels.Transfer.Server;

namespace Quizzer.Logic.Statistics
{
    /// <summary>Eine Ergebniszeile fuer die Bestenliste - die Rechte hat der Aufrufer schon angewandt.</summary>
    /// <param name="Kind">Die Art der Frage, wenn sie ein Minispiel ist; sonst <c>null</c>.</param>
    /// <param name="Variant">Die Spielweise der Frage (<c>MinigameQuestion.Variant</c>), ohne Minispiel 0 (M37 E05).</param>
    public sealed record LeaderboardRow(
        Guid ResultId, Guid PlayerId, string PlayerName, Guid GameId, string GameName,
        MinigameKind? Kind, int Variant, int? RecordValue, DateTimeOffset? RecordedAt, int Steals);

    /// <summary>
    /// Die Bestenliste ueber Abende (M35 C9) als reine Rechnung - Server und Tests benutzen dieselbe.
    /// <list type="bullet">
    /// <item>Je Art mit Bestwert die besten <see cref="Top"/> Spieler, je Spieler sein bester Wert; die Richtung legt
    /// <see cref="MinigameRecord.LowerIsBetter"/> fest. Ein Wert ausserhalb des Bereichs zaehlt nicht.</item>
    /// <item>Wo <see cref="MinigameRecord.VariantTitles"/> es verlangt, je Spielweise eine eigene Liste (M37 E05).</item>
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
                .SelectMany(p => Listen(p, zeilen))
                .ToList();

            return new Leaderboard(kategorien, MeisteKlaus(zeilen));
        }

        /// <summary>Ein gueltiger Rohwert samt seiner Zeile.</summary>
        private sealed record Messung(LeaderboardRow Zeile, int Wert);

        /// <summary>Eine Liste je Art; trennt der Bestwert nach Spielweise, eine je Spielweise.</summary>
        private static IEnumerable<LeaderboardCategory> Listen(MinigameProfile profil, List<LeaderboardRow> zeilen)
        {
            var record = profil.Record ?? throw new ArgumentException("Art ohne Bestwert.", nameof(profil));
            var derArt = zeilen.Where(z => z.Kind == profil.Kind).ToList();

            if (record.VariantTitles.Count == 0)
                return [Kategorie(profil, record, derArt, null)];

            return record.VariantTitles.Select((_, spielweise) =>
                Kategorie(profil, record, derArt.Where(z => Spielweise(z, record) == spielweise), spielweise));
        }

        /// <summary>Ein unbekannter Index zaehlt zur ersten Spielweise - so spielt ihn die Runde (wie <see cref="MinigameProfile.VariantFor"/>).</summary>
        private static int Spielweise(LeaderboardRow zeile, MinigameRecord record)
            => zeile.Variant >= 0 && zeile.Variant < record.VariantTitles.Count ? zeile.Variant : 0;

        private static LeaderboardCategory Kategorie(MinigameProfile profil, MinigameRecord record, IEnumerable<LeaderboardRow> zeilen, int? spielweise)
        {
            var bestJeSpieler = zeilen
                .Where(z => z.RecordValue.HasValue)
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

            var titel = spielweise is { } i ? $"{record.Title} – {record.VariantTitles[i]}" : record.Title;
            return new LeaderboardCategory(profil.Kind, spielweise, titel, profil.DisplayName, record.Unit, record.LowerIsBetter, eintraege);
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
