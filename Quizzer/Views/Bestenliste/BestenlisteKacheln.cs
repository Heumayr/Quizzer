using Quizzer.DataModels.Transfer.Server;
using System.Globalization;

namespace Quizzer.Views.Bestenliste
{
    /// <summary>Eine Kachel der Bestenliste (M35 C9): Titel und je Platz eine Zeile.</summary>
    public sealed record BestenlisteKachel(string Titel, IReadOnlyList<string> Zeilen);

    /// <summary>
    /// Aus der Bestenliste die Kacheln - geteilt von „Meine Statistik" und der Bestenliste ohne Server (M37 E03), damit
    /// beide dieselben Worte zeigen.
    /// </summary>
    public static class BestenlisteKacheln
    {
        /// <summary>Je Liste eine Kachel, dazu die meisten Klaus; eine leere sagt es, statt zu fehlen.</summary>
        internal static IEnumerable<BestenlisteKachel> Aus(Leaderboard liste)
        {
            foreach (var art in liste.Categories)
                yield return new BestenlisteKachel($"{art.Title} ({art.KindName})", OderLeer(art.Entries.Select(Zeile)));

            yield return new BestenlisteKachel("Meiste Klaus",
                OderLeer(liste.MostSteals.Select(s => $"{s.Rank}. {s.PlayerName} – {s.Steals} {(s.Steals == 1 ? "Klau" : "Klaus")}")));
        }

        /// <summary>„1. Anna – 187 ms (Spieleabend, 16.11.2026)" - das Spiel steht dabei, damit man sieht, woher ein Rekord kommt.</summary>
        internal static string Zeile(LeaderboardEntry eintrag)
        {
            var datum = eintrag.RecordedAt is { } am ? ", " + am.ToLocalTime().ToString("dd.MM.yyyy", CultureInfo.GetCultureInfo("de-DE")) : string.Empty;
            return $"{eintrag.Rank}. {eintrag.PlayerName} – {eintrag.ValueText} ({eintrag.GameName}{datum})";
        }

        private static IReadOnlyList<string> OderLeer(IEnumerable<string> zeilen)
        {
            var liste = zeilen.ToList();
            return liste.Count == 0 ? ["Noch kein Eintrag."] : liste;
        }
    }
}
