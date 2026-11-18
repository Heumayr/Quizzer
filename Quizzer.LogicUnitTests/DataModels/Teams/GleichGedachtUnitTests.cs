using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Questions.Teams;

namespace Quizzer.LogicUnitTests.DataModels.Teams
{
    /// <summary>
    /// „Gleich gedacht" (M37 T6, F-M37-12): nur die Übereinstimmung bringt Punkte - eine Gruppe ab zwei, nur die größte, gleich
    /// große größte alle; je passendem Mitglied sein Anteil am Feldwert über das ganze Team. Die Spielleitung legt zusammen.
    /// </summary>
    [TestClass]
    public class GleichGedachtUnitTests
    {
        private static Guid[] Ids(int anzahl) => Enumerable.Range(0, anzahl).Select(_ => Guid.NewGuid()).ToArray();

        private static Dictionary<Guid, string?> Antworten(Guid[] folge, params string?[] texte)
            => folge.Zip(texte).ToDictionary(p => p.First, p => p.Second);

        [TestMethod]
        public void ThePairCountsAndTheOneAloneDoesNot()
        {
            var team = Ids(3);

            var ergebnis = GleichGedacht.Werte(300, team, Antworten(team, "Paris", "  paris ", "Rom"));

            CollectionAssert.AreEquivalent(new[] { team[0], team[1] }, ergebnis.Anteile.Keys.ToArray(), "Nicht genau das Paar bekommt etwas.");
            Assert.AreEqual(100, ergebnis.Anteile[team[0]]);
            Assert.AreEqual(100, ergebnis.Anteile[team[1]]);
            Assert.AreEqual(2, ergebnis.Gruppen.Count);
            Assert.IsTrue(ergebnis.Gruppen[0] is { Zaehlt: true, Schluessel: "paris" }, "Die größte Gruppe steht nicht vorn oder zählt nicht.");
            CollectionAssert.AreEqual(new[] { "Paris", "paris" }, ergebnis.Gruppen[0].Texte.ToArray(), "Die Schreibweisen der Gruppe fehlen.");
            Assert.IsFalse(ergebnis.Gruppen[1].Zaehlt, "Einer allein hat gleich gedacht.");
        }

        /// <summary>Mindestgröße 2: lauter verschiedene Antworten, ein Team aus einem - niemand bekommt etwas.</summary>
        [TestMethod]
        public void NobodyAloneHasThoughtAlike()
        {
            var team = Ids(3);
            var allein = Ids(1);

            Assert.AreEqual(0, GleichGedacht.Werte(300, team, Antworten(team, "Paris", "Rom", "Wien")).Anteile.Count, "Lauter Einzelne zählen.");
            Assert.AreEqual(0, GleichGedacht.Werte(300, allein, Antworten(allein, "Paris")).Anteile.Count, "Wer allein im Team ist, bekommt etwas.");
            Assert.IsFalse(GleichGedacht.Werte(300, team, Antworten(team, "Paris", "Rom", "Wien")).Gruppen.Any(g => g.Zaehlt));
        }

        [TestMethod]
        public void EquallyLargeLargestGroupsAllCountASmallerOneDoesNot()
        {
            var vier = Ids(4);
            var fuenf = Ids(5);

            var gleich = GleichGedacht.Werte(400, vier, Antworten(vier, "Paris", "Rom", "paris", "ROM"));
            var kleiner = GleichGedacht.Werte(500, fuenf, Antworten(fuenf, "Paris", "Rom", "paris", "Rom", "PARIS"));

            Assert.AreEqual(4, gleich.Anteile.Count, "Gleich große größte Gruppen zählen nicht alle.");
            Assert.AreEqual(400, gleich.Anteile.Values.Sum());
            CollectionAssert.AreEquivalent(new[] { fuenf[0], fuenf[2], fuenf[4] }, kleiner.Anteile.Keys.ToArray(), "Die kleinere Gruppe zählt mit.");
        }

        /// <summary>Aufgeteilt wird über das ganze Team: zwei von drei bekommen ihren Drittelanteil, nicht die Hälfte.</summary>
        [TestMethod]
        public void TheShareIsTheMembersPartOfTheWholeTeam()
        {
            var team = Ids(3);
            var verteilt = Aufteilung.Verteile(100, team);

            var ergebnis = GleichGedacht.Werte(100, team, Antworten(team, "Rom", "Paris", "paris"));

            Assert.AreEqual(verteilt[team[1]], ergebnis.Anteile[team[1]]);
            Assert.AreEqual(verteilt[team[2]], ergebnis.Anteile[team[2]]);
            Assert.AreEqual(66, ergebnis.Anteile.Values.Sum(), "Zwei von dreien bekommen mehr als ihren Anteil.");
        }

        [TestMethod]
        [DataRow("Müller", "mueller")]
        [DataRow("Straße", "STRASSE")]
        [DataRow("New  York", " new york")]
        public void ComparedLikeTheLetterSalad(string eins, string zwei)
        {
            var team = Ids(2);

            Assert.AreEqual(2, GleichGedacht.Werte(200, team, Antworten(team, eins, zwei)).Anteile.Count, $"„{eins}“ und „{zwei}“ gelten nicht als gleich.");
        }

        /// <summary>A06: zwei leere Antworten sind nicht gleich gedacht - auch nicht, wenn sie zu einer Gruppe gelegt wurden.</summary>
        [TestMethod]
        public void EmptyAnswersAreNeverAlike()
        {
            var team = Ids(3);
            var leer = Antworten(team, "", "   ", null);
            var gelegt = team.ToDictionary(m => m, _ => "paris");

            Assert.AreEqual(0, GleichGedacht.Werte(300, team, leer).Gruppen.Count, "Leere Antworten bilden eine Gruppe.");
            Assert.AreEqual(0, GleichGedacht.Werte(300, team, leer, gelegt).Anteile.Count, "Leere Antworten zählen über das Zusammenlegen.");
        }

        /// <summary>A01: nur Mitglieder zählen - die gleiche Antwort eines Fremden macht keinen zum Paar; doppelte Kennungen zählen einmal.</summary>
        [TestMethod]
        public void OnlyMembersCountAndEachOnce()
        {
            var team = Ids(2);
            var fremd = Guid.NewGuid();
            var antworten = new Dictionary<Guid, string?> { [team[0]] = "Paris", [team[1]] = "Rom", [fremd] = "Paris" };

            Assert.AreEqual(0, GleichGedacht.Werte(200, team, antworten).Anteile.Count, "Die Antwort eines Fremden zählt fürs Team.");
            Assert.AreEqual(0, GleichGedacht.Werte(200, [team[0], team[0]], antworten).Anteile.Count, "Ein doppelt genanntes Mitglied ist ein Paar.");
        }

        /// <summary>E1: die Spielleitung legt „Pariss" zu „Paris" - danach ist es ein Paar.</summary>
        [TestMethod]
        public void MergingPutsTheFurtherGroupsIntoTheFirst()
        {
            var team = Ids(3);
            var antworten = Antworten(team, "Paris", "Pariss", "Rom");
            var vorher = GleichGedacht.Werte(300, team, antworten);
            Assert.AreEqual(0, vorher.Anteile.Count);

            var gelegt = GleichGedacht.LegeZusammen([vorher.Gruppen[0], vorher.Gruppen[1]]);
            var nachher = GleichGedacht.Werte(300, team, antworten, gelegt);

            CollectionAssert.AreEquivalent(new[] { team[0], team[1] }, nachher.Anteile.Keys.ToArray(), "Das Zusammenlegen wirkt nicht.");
            Assert.AreEqual("paris", nachher.Gruppen[0].Schluessel, "Die zweite Gruppe ging nicht in die erste.");
            CollectionAssert.AreEqual(new[] { "Paris", "Pariss" }, nachher.Gruppen[0].Texte.ToArray(), "Die Gruppe nennt nicht beide Schreibweisen.");
        }

        [TestMethod]
        public void MergingFurtherKeepsWhatWasMergedAndOneGroupChangesNothing()
        {
            var team = Ids(4);
            var antworten = Antworten(team, "Paris", "Pariss", "Parsi", "Rom");
            var erste = GleichGedacht.Werte(400, team, antworten);

            Assert.AreEqual(0, GleichGedacht.LegeZusammen([erste.Gruppen[0]]).Count, "Eine einzelne Gruppe wurde zusammengelegt.");

            var gelegt = GleichGedacht.LegeZusammen([erste.Gruppen[0], erste.Gruppen[1]]);
            var zweite = GleichGedacht.Werte(400, team, antworten, gelegt);
            gelegt = GleichGedacht.LegeZusammen([zweite.Gruppen[1], zweite.Gruppen[0]], gelegt);

            var dritte = GleichGedacht.Werte(400, team, antworten, gelegt);
            CollectionAssert.AreEquivalent(new[] { team[0], team[1], team[2] }, dritte.Anteile.Keys.ToArray(), "Das zweite Zusammenlegen verliert das erste.");
        }

        /// <summary>
        /// A08 über viele Antwortmuster: ein Team bekommt nie mehr als den Feldwert, nur Mitglieder bekommen etwas, und jeder genau
        /// seinen Anteil am ganzen Team; alle gleich heißt der ganze Feldwert. Ein leeres Team wirft nicht.
        /// </summary>
        [TestMethod]
        public void ATeamNeverGetsMoreThanTheFieldValue()
        {
            var woerter = new[] { "Paris", "Rom", "", "paris", "Wien" };
            var zufall = new Random(37);
            var geprueft = 0;

            for (var groesse = 1; groesse <= TeamGrenzen.MaxTeams; groesse++)
            for (var runde = 0; runde < 40; runde++)
            {
                var team = Ids(groesse);
                var antworten = team.ToDictionary(m => m, _ => (string?)woerter[zufall.Next(woerter.Length)]);
                antworten[Guid.NewGuid()] = "Paris";
                var feld = zufall.Next(0, 1000);
                var verteilt = Aufteilung.Verteile(feld, team);

                var ergebnis = GleichGedacht.Werte(feld, team, antworten);
                var fall = $"{feld} auf {groesse}: {string.Join("/", team.Select(m => antworten[m]))}";

                Assert.IsTrue(ergebnis.Anteile.Values.Sum() <= feld, "Mehr als der Feldwert. " + fall);
                Assert.IsTrue(ergebnis.Anteile.All(a => team.Contains(a.Key) && a.Value == verteilt[a.Key]), "Ein Anteil ist falsch oder geht an einen Fremden. " + fall);
                geprueft++;
            }

            var alle = Ids(3);
            Assert.AreEqual(300, GleichGedacht.Werte(300, alle, alle.ToDictionary(m => m, _ => (string?)"Rom")).Anteile.Values.Sum());
            Assert.AreEqual(0, GleichGedacht.Werte(300, [], new Dictionary<Guid, string?>()).Gruppen.Count);
            Assert.IsTrue(geprueft > 300);
        }

        /// <summary>R3-3: ein negativer Feldwert (Altbestand, fremdes Bündel) bringt nichts - „Gleich gedacht" kennt kein Minus.</summary>
        [TestMethod]
        public void ANegativeFieldValueBringsNothingNotAMinus()
        {
            var team = Ids(3);

            var ergebnis = GleichGedacht.Werte(-300, team, Antworten(team, "Paris", "Paris", "Rom"));

            CollectionAssert.AreEquivalent(new[] { team[0], team[1] }, ergebnis.Anteile.Keys.ToArray(), "Das Paar zählt nicht mehr.");
            Assert.IsTrue(ergebnis.Anteile.Values.All(a => a == 0), "Gleich gedacht teilt Minus aus: " + string.Join(", ", ergebnis.Anteile.Values));
        }
    }
}
