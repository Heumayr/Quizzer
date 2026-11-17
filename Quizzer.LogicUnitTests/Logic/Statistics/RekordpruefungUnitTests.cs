using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Questions.Minispiele;
using Quizzer.Logic.Statistics;

namespace Quizzer.LogicUnitTests.Logic.Statistics
{
    /// <summary>
    /// „Neuer Rekord!" (M37 E02) als reine Rechnung: Richtung je Art, unplausible Werte, Gleichstand, die eigene Zelle,
    /// Abend gegen aller Zeiten, Kopfrechnen je Schwierigkeit und der Wortlaut der Ansage.
    /// </summary>
    [TestClass]
    public class RekordpruefungUnitTests
    {
        private static readonly DateTimeOffset Abend = new(2026, 11, 16, 20, 0, 0, TimeSpan.FromHours(1));

        private readonly Guid diesesSpiel = Guid.NewGuid();
        private readonly Guid anderesSpiel = Guid.NewGuid();
        private readonly Guid dieseZelle = Guid.NewGuid();
        private readonly Dictionary<string, Guid> ids = new();

        private Guid Id(string name)
        {
            if (!ids.TryGetValue(name, out var id))
                ids[name] = id = Guid.NewGuid();
            return id;
        }

        private string Name(Guid id) => ids.Single(e => e.Value == id).Key;

        /// <summary>Eine Zeile der Datenbank - ohne Angabe in diesem Spiel, in einer anderen Zelle.</summary>
        private LeaderboardRow Zeile(string spieler, MinigameKind art, int wert, Guid? spiel = null, Guid? zelle = null, int spielweise = 0)
            => new(Guid.NewGuid(), Id(spieler), spieler, spiel ?? diesesSpiel, "Abend", art, spielweise, wert, Abend, 0, zelle ?? Guid.NewGuid());

        private RekordZelle Zelle(MinigameKind art, int spielweise = 0) => new(diesesSpiel, dieseZelle, art, spielweise);

        private Dictionary<Guid, int> Runde(params (string Spieler, int Wert)[] werte) => werte.ToDictionary(w => Id(w.Spieler), w => w.Wert);

        public static IEnumerable<object[]> ArtenMitBestwert()
            => MinigameProfiles.All.Where(p => p.Record != null).Select(p => new object[] { p.Kind });

        [TestMethod]
        [DynamicData(nameof(ArtenMitBestwert))]
        public void TheDirectionFollowsTheKind(MinigameKind art)
        {
            var record = MinigameProfiles.For(art).Record!;
            var mitte = (record.Min + record.Max) / 2;
            var schritt = record.LowerIsBetter ? -1 : 1;
            var bisher = new[] { Zeile("Bert", art, mitte) };

            Assert.AreEqual(2, Rekordpruefung.Pruefe(bisher, Runde(("Anna", mitte + schritt)), Zelle(art)).Count,
                $"{art}: der bessere Wert ist kein Rekord - des Abends und aller Zeiten.");
            Assert.AreEqual(0, Rekordpruefung.Pruefe(bisher, Runde(("Anna", mitte - schritt)), Zelle(art)).Count,
                $"{art}: der schlechtere Wert gilt als Rekord.");
        }

        [TestMethod]
        public void LessIsBetterForTheReactionAndMoreForTheTapSprint()
        {
            var reaktion = Rekordpruefung.Pruefe([Zeile("Bert", MinigameKind.Reaction, 203)],
                Runde(("Anna", 187), ("Carl", 250)), Zelle(MinigameKind.Reaction));
            var tipps = Rekordpruefung.Pruefe([Zeile("Bert", MinigameKind.TapSprint, 610)],
                Runde(("Anna", 730), ("Carl", 500)), Zelle(MinigameKind.TapSprint));

            CollectionAssert.AreEqual(new[] { Id("Anna") }, reaktion[0].PlayerIds.ToList(), "Die kuerzeste Zeit haelt den Rekord nicht.");
            Assert.AreEqual((187, 203), (reaktion[0].Value, reaktion[0].Bisher.Value));
            CollectionAssert.AreEqual(new[] { Id("Anna") }, tipps[0].PlayerIds.ToList(), "Die meisten Tipps halten den Rekord nicht.");
            Assert.AreEqual((730, 610), (tipps[0].Value, tipps[0].Bisher.Value));
        }

        [TestMethod]
        public void AnImplausibleValueOfTheRoundIsNoRecord()
        {
            var record = MinigameRecords.Reaktion;
            var bisher = new[] { Zeile("Bert", MinigameKind.Reaction, 203) };

            Assert.AreEqual(0, Rekordpruefung.Pruefe(bisher, Runde(("Anna", record.Min - 1)), Zelle(MinigameKind.Reaction)).Count,
                "Ein Fruehstart unter der Grenze ist ein Rekord.");

            var rekord = Rekordpruefung.Pruefe(bisher, Runde(("Anna", record.Min - 1), ("Carl", 190)), Zelle(MinigameKind.Reaction));
            CollectionAssert.AreEqual(new[] { Id("Carl") }, rekord[0].PlayerIds.ToList(), "Der unplausible Wert verdeckt den gueltigen.");
        }

        [TestMethod]
        public void AnImplausibleEarlierValueIsNotTheOneToBeat()
        {
            var bisher = new[]
            {
                Zeile("Zora", MinigameKind.Reaction, MinigameRecords.Reaktion.Min - 1, spiel: anderesSpiel),
                Zeile("Bert", MinigameKind.Reaction, 203),
            };

            var rekorde = Rekordpruefung.Pruefe(bisher, Runde(("Anna", 187)), Zelle(MinigameKind.Reaction));

            Assert.AreEqual(2, rekorde.Count, "Ein unplausibler Altwert verhindert den Rekord.");
            Assert.IsTrue(rekorde.All(r => r.Bisher.PlayerName == "Bert"));
        }

        [TestMethod]
        public void ATieIsNoRecord()
        {
            Assert.AreEqual(0, Rekordpruefung.Pruefe([Zeile("Bert", MinigameKind.Reaction, 203)],
                Runde(("Anna", 203)), Zelle(MinigameKind.Reaction)).Count, "Gleich schnell gilt als neuer Rekord.");
            Assert.AreEqual(0, Rekordpruefung.Pruefe([Zeile("Bert", MinigameKind.TapSprint, 610)],
                Runde(("Anna", 610)), Zelle(MinigameKind.TapSprint)).Count, "Gleich viele Tipps gelten als neuer Rekord.");
        }

        [TestMethod]
        public void TheCellsOwnRowsDoNotCountAgainstIt()
        {
            // Die Zelle wurde schon einmal gespielt und gespeichert: Bert 180 ms. Jetzt die Wiederholung.
            var bisher = new[]
            {
                Zeile("Bert", MinigameKind.Reaction, 180, zelle: dieseZelle),
                Zeile("Anna", MinigameKind.Reaction, 203),
            };

            var rekorde = Rekordpruefung.Pruefe(bisher, Runde(("Bert", 190)), Zelle(MinigameKind.Reaction));

            Assert.AreEqual(2, rekorde.Count, "Die Wiederholung misst sich an ihrem eigenen frueheren Wert.");
            Assert.IsTrue(rekorde.All(r => r.Bisher.Value == 203 && r.Bisher.PlayerName == "Anna"));
        }

        [TestMethod]
        public void TheEveningCountsOnlyThisGame()
        {
            var bisher = new[]
            {
                Zeile("Carl", MinigameKind.Reaction, 195, spiel: anderesSpiel),
                Zeile("Anna", MinigameKind.Reaction, 203),
            };

            var schneller = Rekordpruefung.Pruefe(bisher, Runde(("Bert", 187)), Zelle(MinigameKind.Reaction));
            var nurAbend = Rekordpruefung.Pruefe(bisher, Runde(("Bert", 199)), Zelle(MinigameKind.Reaction));

            Assert.AreEqual((RekordUmfang.AllerZeiten, "Carl"), (schneller[0].Umfang, schneller[0].Bisher.PlayerName));
            Assert.AreEqual((RekordUmfang.Abend, "Anna"), (schneller[1].Umfang, schneller[1].Bisher.PlayerName));
            Assert.AreEqual(1, nurAbend.Count, "199 ms schlaegt Carls 195 ms aus dem anderen Spiel.");
            Assert.AreEqual((RekordUmfang.Abend, 203), (nurAbend[0].Umfang, nurAbend[0].Bisher.Value));
        }

        [TestMethod]
        public void WithoutAnEarlierValueThereIsNothingToBeat()
        {
            Assert.AreEqual(0, Rekordpruefung.Pruefe([], Runde(("Anna", 187)), Zelle(MinigameKind.Reaction)).Count);

            var nurAllerZeiten = Rekordpruefung.Pruefe([Zeile("Carl", MinigameKind.Reaction, 203, spiel: anderesSpiel)],
                Runde(("Anna", 187)), Zelle(MinigameKind.Reaction));
            Assert.AreEqual(RekordUmfang.AllerZeiten, nurAllerZeiten.Single().Umfang, "Ohne Wert des Abends gibt es keinen Abendrekord.");
        }

        [TestMethod]
        public void MentalArithmeticCountsOnlyWithinItsDifficulty()
        {
            var bisher = new[]
            {
                Zeile("Carl", MinigameKind.MentalArithmetic, 2000, spielweise: 0),
                Zeile("Anna", MinigameKind.MentalArithmetic, 900, spielweise: 2),
            };

            var schwer = Rekordpruefung.Pruefe(bisher, Runde(("Bert", 1000)), Zelle(MinigameKind.MentalArithmetic, spielweise: 2));
            var leicht = Rekordpruefung.Pruefe(bisher, Runde(("Bert", 1000)), Zelle(MinigameKind.MentalArithmetic, spielweise: 0));

            Assert.AreEqual(2, schwer.Count, "Schwer misst sich an leicht.");
            Assert.IsTrue(schwer.All(r => r.Bisher.PlayerName == "Anna"));
            Assert.AreEqual(0, leicht.Count, "Leicht misst sich an schwer.");
        }

        [TestMethod]
        public void AKindWithoutBestValueHasNoRecords()
            => Assert.AreEqual(0, Rekordpruefung.Pruefe([Zeile("Bert", MinigameKind.Majority, 5)],
                Runde(("Anna", 9)), Zelle(MinigameKind.Majority)).Count);

        [TestMethod]
        public void TheRecordOfAllTimeBeatsTheAnnouncementOfTheEvening()
        {
            var bisher = new[]
            {
                Zeile("Carl", MinigameKind.Reaction, 195, spiel: anderesSpiel),
                Zeile("Bert", MinigameKind.Reaction, 203),
            };

            var allerZeiten = Rekordpruefung.Ansage(Rekordpruefung.Pruefe(bisher, Runde(("Anna", 187)), Zelle(MinigameKind.Reaction)), Name);
            var abend = Rekordpruefung.Ansage(Rekordpruefung.Pruefe(bisher, Runde(("Anna", 199)), Zelle(MinigameKind.Reaction)), Name);

            Assert.AreEqual(new RekordAnsage("Neuer Rekord aller Zeiten!", "Anna – 187 ms (bisher 195 ms, Carl)"), allerZeiten);
            Assert.AreEqual(new RekordAnsage("Neuer Rekord des Abends!", "Anna – 199 ms (bisher 203 ms, Bert)"), abend);
            Assert.AreEqual("Neuer Rekord des Abends! Anna – 199 ms (bisher 203 ms, Bert)", abend!.Text);
            Assert.IsNull(Rekordpruefung.Ansage([], Name), "Ohne Rekord gibt es eine Ansage.");
        }

        [TestMethod]
        public void ATieInTheRoundNamesEveryHolder()
        {
            var rekorde = Rekordpruefung.Pruefe([Zeile("Zora", MinigameKind.Reaction, 203)],
                Runde(("Carl", 187), ("Anna", 187), ("Bert", 187), ("Dora", 250)), Zelle(MinigameKind.Reaction));

            Assert.AreEqual("Anna, Bert und Carl – 187 ms (bisher 203 ms, Zora)", Rekordpruefung.Ansage(rekorde, Name)!.Zeile);
        }
    }
}
