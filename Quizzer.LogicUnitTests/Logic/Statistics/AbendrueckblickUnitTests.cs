using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Questions.Minispiele;
using Quizzer.Logic.Statistics;

namespace Quizzer.LogicUnitTests.Logic.Statistics
{
    /// <summary>
    /// Der Abendrückblick (M37 E04) als reine Rechnung: Bestwerte des Abends über die Rechnung der Bestenliste, meiste Klaus,
    /// eingelöste Joker, meiste richtige Antworten - und was nicht hineingehört (anderes Spiel, unplausibel, offener Joker).
    /// </summary>
    [TestClass]
    public class AbendrueckblickUnitTests
    {
        private static readonly DateTimeOffset Abend = new(2026, 11, 16, 20, 0, 0, TimeSpan.FromHours(1));

        private readonly Guid diesesSpiel = Guid.NewGuid();
        private readonly Guid anderesSpiel = Guid.NewGuid();
        private readonly Dictionary<string, Guid> ids = new();

        private Guid Id(string name)
        {
            if (!ids.TryGetValue(name, out var id))
                ids[name] = id = Guid.NewGuid();
            return id;
        }

        private LeaderboardRow Bestwert(string spieler, MinigameKind art, int? wert, int klaus = 0, Guid? spiel = null, int spielweise = 0, int minuten = 0)
            => new(Guid.NewGuid(), Id(spieler), spieler, spiel ?? diesesSpiel, "Abend", art, spielweise, wert, Abend.AddMinutes(minuten), klaus, Guid.NewGuid());

        private QuestionResult Richtige(string spieler, int anzahl, Guid? spiel = null)
            => new() { Id = Guid.NewGuid(), PlayerId = Id(spieler), GameId = spiel ?? diesesSpiel, RightCount = anzahl };

        private PlayerJoker Joker(string spieler, JokerKind art, int minuten, string? ziel = null, bool eingeloest = true, Guid? spiel = null)
            => new()
            {
                Id = Guid.NewGuid(), PlayerId = Id(spieler), GameId = spiel ?? diesesSpiel, Kind = art,
                TargetPlayerId = ziel == null ? null : Id(ziel),
                GameGridCoordinateId = eingeloest ? Guid.NewGuid() : null,
                RedeemedAt = eingeloest ? Abend.AddMinutes(minuten) : null,
            };

        private Rueckblick Rechne(IEnumerable<LeaderboardRow>? zeilen = null, IEnumerable<QuestionResult>? ergebnisse = null, IEnumerable<PlayerJoker>? joker = null)
        {
            var z = (zeilen ?? []).ToList();
            var e = (ergebnisse ?? []).ToList();
            var j = (joker ?? []).ToList();
            return Abendrueckblick.Rechne(new RueckblickDaten(diesesSpiel, z, e, j, ids.ToDictionary(p => p.Value, p => p.Key)));
        }

        [TestMethod]
        public void EachKindShowsTheBestValueOfThisGameWithItsUnit()
        {
            var r = Rechne(zeilen:
            [
                Bestwert("Anna", MinigameKind.Reaction, 240),
                Bestwert("Bert", MinigameKind.Reaction, 187),
                Bestwert("Anna", MinigameKind.TapSprint, 735),
                Bestwert("Bert", MinigameKind.TapSprint, 512),
            ]);

            CollectionAssert.AreEqual(
                new[] { "Schnellste Reaktion: Bert – 187 ms", "Schnellste Finger: Anna – 7,35 Tipps/s" },
                r.Bestwerte.Select(b => b.Zeile).ToArray(),
                "Die Richtung kommt aus der Art: bei der Reaktion gewinnt der kleinere, beim Tippsprint der groessere Wert.");
        }

        /// <summary>Gegenprobe zum Abend: ein besserer Wert aus einem anderen Spiel gehört nicht in diesen Rückblick.</summary>
        [TestMethod]
        public void AnotherGameDoesNotCount()
        {
            var r = Rechne(zeilen:
            [
                Bestwert("Anna", MinigameKind.Reaction, 240),
                Bestwert("Carl", MinigameKind.Reaction, 150, klaus: 9, spiel: anderesSpiel),
            ], ergebnisse: [Richtige("Anna", 1), Richtige("Carl", 9, spiel: anderesSpiel)]);

            Assert.AreEqual("Schnellste Reaktion: Anna – 240 ms", r.Bestwerte.Single().Zeile);
            Assert.IsNull(r.MeisteKlaus, "Die Klaus eines anderen Abends stehen im Rückblick.");
            Assert.AreEqual("Anna – 1 richtige Antwort", r.MeisteRichtige?.Zeile);
        }

        /// <summary>Dieselbe Rechnung wie die Bestenliste: ein Wert außerhalb des plausiblen Bereichs zählt auch hier nicht.</summary>
        [TestMethod]
        public void AnImplausibleValueIsNoBestValue()
        {
            var record = MinigameProfiles.For(MinigameKind.Reaction).Record!;

            var r = Rechne(zeilen: [Bestwert("Anna", MinigameKind.Reaction, record.Min - 1), Bestwert("Bert", MinigameKind.Reaction, record.Min + 50)]);

            Assert.AreEqual($"Schnellste Reaktion: Bert – {record.Min + 50} ms", r.Bestwerte.Single().Zeile);
        }

        [TestMethod]
        public void ATieNamesEveryoneWithTheBestValue()
        {
            var r = Rechne(zeilen:
            [
                Bestwert("Bert", MinigameKind.Reaction, 199, minuten: 1),
                Bestwert("Anna", MinigameKind.Reaction, 199, minuten: 2),
                Bestwert("Carl", MinigameKind.Reaction, 260),
            ]);

            Assert.AreEqual("Schnellste Reaktion: Bert und Anna – 199 ms", r.Bestwerte.Single().Zeile,
                "Bei Gleichstand fuehrt die Bestenliste den zuerst Gemessenen vorn.");
        }

        /// <summary>Kopfrechnen trennt nach Schwierigkeit (M37 E05) - im Rückblick ebenso.</summary>
        [TestMethod]
        public void MentalArithmeticKeepsOneLinePerDifficulty()
        {
            var r = Rechne(zeilen:
            [
                Bestwert("Anna", MinigameKind.MentalArithmetic, 1250, spielweise: 0),
                Bestwert("Bert", MinigameKind.MentalArithmetic, 600, spielweise: 2),
            ]);

            CollectionAssert.AreEqual(
                new[] { "Schnellster Rechner – leicht: Anna – 12,50 richtige/min", "Schnellster Rechner – schwer: Bert – 6,00 richtige/min" },
                r.Bestwerte.Select(b => b.Zeile).ToArray());
        }

        [TestMethod]
        public void TheMostStealsAreSummedOverTheGame()
        {
            var r = Rechne(zeilen:
            [
                Bestwert("Anna", MinigameKind.Reaction, null, klaus: 1),
                Bestwert("Anna", MinigameKind.Reaction, null, klaus: 2),
                Bestwert("Bert", MinigameKind.Reaction, null, klaus: 2),
            ]);

            Assert.AreEqual("Anna – 3 Klaus", r.MeisteKlaus?.Zeile);
            CollectionAssert.AreEqual(new[] { Id("Anna") }, r.MeisteKlaus!.PlayerIds.ToArray());
        }

        [TestMethod]
        public void OneStealIsOneKlau()
            => Assert.AreEqual("Bert – 1 Klau", Rechne(zeilen: [Bestwert("Bert", MinigameKind.Reaction, null, klaus: 1)]).MeisteKlaus?.Zeile);

        [TestMethod]
        public void RedeemedJokersShowWhoTookWhichAndWhomTheBlockHit()
        {
            var r = Rechne(joker:
            [
                Joker("Bert", JokerKind.Block, minuten: 30, ziel: "Carl"),
                Joker("Anna", JokerKind.Double, minuten: 10),
                Joker("Carl", JokerKind.Shield, minuten: 20, eingeloest: false),
                Joker("Dora", JokerKind.Shield, minuten: 5, spiel: anderesSpiel),
            ]);

            CollectionAssert.AreEqual(new[] { "Anna: Doppelt", "Bert: Sperre gegen Carl" }, r.Joker.Select(j => j.Zeile).ToArray(),
                "Nur eingeloeste Joker dieses Spiels, in der Reihenfolge des Einloesens.");
            Assert.AreEqual(Id("Carl"), r.Joker[1].ZielId);
        }

        /// <summary>Nur die Sperre trifft jemanden - ein Ziel an einem anderen Joker (Altbestand, fremder Push) bleibt ungenannt.</summary>
        [TestMethod]
        public void OnlyTheBlockNamesATarget()
        {
            var r = Rechne(joker: [Joker("Anna", JokerKind.Double, minuten: 10, ziel: "Bert")]);

            Assert.AreEqual("Anna: Doppelt", r.Joker.Single().Zeile);
            Assert.IsNull(r.Joker.Single().ZielId);
        }

        [TestMethod]
        public void TheMostRightAnswersAreSummedAndATieNamesBoth()
        {
            var r = Rechne(ergebnisse: [Richtige("Bert", 2), Richtige("Bert", 3), Richtige("Anna", 5), Richtige("Carl", 1), Richtige("Dora", 0)]);

            Assert.AreEqual("Anna und Bert – 5 richtige Antworten", r.MeisteRichtige?.Zeile);
        }

        [TestMethod]
        public void AGameWithoutAnyOfItIsEmpty()
        {
            var r = Rechne(zeilen: [Bestwert("Anna", MinigameKind.Reaction, null)], ergebnisse: [Richtige("Anna", 0)], joker: [Joker("Anna", JokerKind.Double, 0, eingeloest: false)]);

            Assert.IsTrue(r.IstLeer, "Ein Spiel ohne Bestwert, Klau, eingeloesten Joker und richtige Antwort hat nichts zurueckzublicken.");
            Assert.IsFalse(Rechne(ergebnisse: [Richtige("Anna", 1)]).IstLeer, "Gegenprobe: eine richtige Antwort ist schon ein Rueckblick.");
        }
    }
}
