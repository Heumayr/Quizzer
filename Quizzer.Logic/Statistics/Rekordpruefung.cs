using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Questions.Minispiele;
using Quizzer.DataModels.Transfer.Server;

namespace Quizzer.Logic.Statistics
{
    /// <summary>Wo ein Bestwert gilt (M37 E02).</summary>
    public enum RekordUmfang
    {
        /// <summary>Dieses Spiel.</summary>
        Abend,

        /// <summary>Alle Spiele der lokalen Datenbank.</summary>
        AllerZeiten,
    }

    /// <summary>Die Zelle, deren Runde geprueft wird - ihre eigenen Zeilen sind nicht der bisherige Stand.</summary>
    /// <param name="Variant">Die Spielweise der Frage; trennt der Bestwert danach (Kopfrechnen), gilt der Rekord nur darin.</param>
    public sealed record RekordZelle(Guid GameId, Guid CoordinateId, MinigameKind Kind, int Variant);

    /// <summary>Ein geschlagener Bestwert.</summary>
    /// <param name="PlayerIds">Wer ihn jetzt haelt - bei Gleichstand in der Runde mehrere.</param>
    /// <param name="Bisher">Der bisherige erste Platz, wie ihn die Bestenliste ohne diese Zelle fuehrt.</param>
    public sealed record Rekord(RekordUmfang Umfang, IReadOnlyList<Guid> PlayerIds, int Value, string ValueText, LeaderboardEntry Bisher);

    /// <summary>Was Beamer und Spielleiter zeigen: eine Kopfzeile und wer mit welchem Wert wen abloest.</summary>
    public sealed record RekordAnsage(string Titel, string Zeile)
    {
        public string Text => $"{Titel} {Zeile}";
    }

    /// <summary>
    /// „Neuer Rekord!" (M37 E02) als reine Rechnung: schlaegt der beste Messwert der Runde den ersten Platz der
    /// Bestenliste - des Abends oder aller Zeiten?
    /// <list type="bullet">
    /// <item>Der bisherige Stand ist die Liste aus <see cref="LeaderboardCalculator"/> <b>ohne die Zeilen dieser Zelle</b> -
    /// eine wiederholte Zelle zaehlt nicht gegen sich selbst. Richtung, Plausibilitaet und Spielweise kommen von dort.</item>
    /// <item>Nur echt besser ist ein Rekord; Gleichstand nicht. Ohne bisherigen Wert gibt es nichts zu schlagen.</item>
    /// <item>Ein unplausibler Messwert der Runde zaehlt nicht.</item>
    /// </list>
    /// </summary>
    public static class Rekordpruefung
    {
        /// <summary>Die geschlagenen Bestwerte, aller Zeiten vor dem Abend; leer, wenn keiner faellt.</summary>
        /// <param name="bisher">Die Zeilen der Datenbank, wie sie <see cref="LeaderboardRows"/> liefert - auch die dieser Zelle.</param>
        /// <param name="messwerte">Die Rohwerte der Runde je Spieler, in der Einheit von <see cref="MinigameRecord"/>.</param>
        public static IReadOnlyList<Rekord> Pruefe(IEnumerable<LeaderboardRow> bisher, IReadOnlyDictionary<Guid, int> messwerte, RekordZelle zelle)
        {
            ArgumentNullException.ThrowIfNull(bisher);
            ArgumentNullException.ThrowIfNull(messwerte);
            ArgumentNullException.ThrowIfNull(zelle);

            if (!MinigameProfiles.TryFor(zelle.Kind, out var profil) || profil.Record is not { } record)
                return [];

            var gueltig = messwerte.Where(m => record.IsPlausible(m.Value)).ToList();
            if (gueltig.Count == 0)
                return [];

            var bester = record.LowerIsBetter ? gueltig.Min(m => m.Value) : gueltig.Max(m => m.Value);
            var halter = gueltig.Where(m => m.Value == bester).Select(m => m.Key).ToList();

            var ohneZelle = bisher.Where(z => z.CoordinateId != zelle.CoordinateId).ToList();
            var umfaenge = new[]
            {
                (Umfang: RekordUmfang.AllerZeiten, Zeilen: ohneZelle),
                (Umfang: RekordUmfang.Abend, Zeilen: ohneZelle.Where(z => z.GameId == zelle.GameId).ToList()),
            };

            var rekorde = new List<Rekord>();
            foreach (var (umfang, zeilen) in umfaenge)
            {
                if (ErsterPlatz(zeilen, zelle, record) is { } erster && Besser(bester, erster.Value, record))
                    rekorde.Add(new Rekord(umfang, halter, bester, record.Format(bester), erster));
            }

            return rekorde;
        }

        /// <summary>Die Ansage zu den Rekorden - einer aller Zeiten schlaegt den des Abends; <c>null</c> ohne Rekord.</summary>
        /// <param name="name">Der Anzeigename zu einer Spieler-Id, wie die Karten ihn fuehren.</param>
        public static RekordAnsage? Ansage(IReadOnlyList<Rekord> rekorde, Func<Guid, string> name)
        {
            ArgumentNullException.ThrowIfNull(rekorde);
            ArgumentNullException.ThrowIfNull(name);

            var rekord = rekorde.FirstOrDefault(r => r.Umfang == RekordUmfang.AllerZeiten) ?? rekorde.FirstOrDefault();
            if (rekord == null)
                return null;

            var titel = rekord.Umfang == RekordUmfang.AllerZeiten ? "Neuer Rekord aller Zeiten!" : "Neuer Rekord des Abends!";
            var wer = Aufzaehlung(rekord.PlayerIds.Select(name).Order(StringComparer.Ordinal).ToList());

            return new RekordAnsage(titel, $"{wer} – {rekord.ValueText} (bisher {rekord.Bisher.ValueText}, {rekord.Bisher.PlayerName})");
        }

        /// <summary>Der erste Platz der Liste, in der die Zelle zaehlt - bei Gleichstand der, den die Bestenliste vorn fuehrt.</summary>
        private static LeaderboardEntry? ErsterPlatz(List<LeaderboardRow> zeilen, RekordZelle zelle, MinigameRecord record)
        {
            int? spielweise = record.VariantTitles.Count == 0 ? null : LeaderboardCalculator.Spielweise(zelle.Variant, record);

            return LeaderboardCalculator.Calculate(zeilen).Categories
                .Single(c => c.Kind == zelle.Kind && c.Variant == spielweise)
                .Entries.FirstOrDefault();
        }

        private static bool Besser(int wert, int bisher, MinigameRecord record) => record.LowerIsBetter ? wert < bisher : wert > bisher;

        /// <summary>„Anna", „Anna und Bert", „Anna, Bert und Carl".</summary>
        private static string Aufzaehlung(IReadOnlyList<string> namen)
            => namen.Count <= 1 ? string.Concat(namen) : $"{string.Join(", ", namen.Take(namen.Count - 1))} und {namen[^1]}";
    }
}
