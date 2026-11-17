using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Questions.Teams;

namespace Quizzer.LogicUnitTests.DataModels.Teams
{
    /// <summary>
    /// Team-Abstimmung (M37 T7): die Mehrheit im Team ist seine Antwort; bei Gleichstand die Stimme des Sprechers, wenn sie
    /// darunter ist, sonst entscheidet die Spielleitung - die auch einen Sprecherentscheid ändern kann (E1). Ohne Stimme keine
    /// Antwort. Eine Stimme ist die ganze Auswahl; nur Mitglieder zählen.
    /// </summary>
    [TestClass]
    public class TeamMehrheitUnitTests
    {
        private static Guid[] Ids(int anzahl) => Enumerable.Range(0, anzahl).Select(_ => Guid.NewGuid()).ToArray();

        /// <summary>Je Mitglied seine Stimme; <c>null</c> heißt: hat nicht abgestimmt.</summary>
        private static Dictionary<Guid, string> Stimmen(Guid[] team, params string?[] stimmen)
            => team.Zip(stimmen).Where(p => p.Second != null).ToDictionary(p => p.First, p => p.Second!);

        [TestMethod]
        public void TheMajorityIsTheAnswerOfTheTeam()
        {
            var team = Ids(3);

            var ergebnis = TeamMehrheit.Ermittle(Stimmen(team, "B", "A", "B"), team, team[1]);

            Assert.AreEqual("B", ergebnis?.Antwort);
            Assert.AreEqual(TeamMehrheitEntscheid.Mehrheit, ergebnis!.Entscheid);
            Assert.AreEqual((2, 3), (ergebnis.Stimmen, ergebnis.Abgegeben));
            Assert.IsFalse(ergebnis.Gleichstand);
        }

        [TestMethod]
        public void ATieGoesToTheSpeakerWhenHisVoteIsAmongTheTied()
        {
            var team = Ids(4);

            var ergebnis = TeamMehrheit.Ermittle(Stimmen(team, "A", "B", "B", "A"), team, team[1]);

            Assert.AreEqual("B", ergebnis?.Antwort, "Der Gleichstand fiel nicht auf die Stimme des Sprechers.");
            Assert.AreEqual(TeamMehrheitEntscheid.Sprecher, ergebnis!.Entscheid);
            CollectionAssert.AreEqual(new[] { "A", "B" }, ergebnis.Gleichauf.ToArray());
            Assert.AreEqual(2, ergebnis.Stimmen);
        }

        /// <summary>Der Sprecher stimmte für keine der Gleichauf - oder gar nicht: dann entscheidet die Spielleitung, bis dahin nichts.</summary>
        [TestMethod]
        [DataRow("C", DisplayName = "Sprecher für eine dritte")]
        [DataRow(null, DisplayName = "Sprecher ohne Stimme")]
        public void ATieWithoutTheSpeakerAmongTheTiedIsLeftToTheMaster(string? sprecherstimme)
        {
            var team = Ids(5);

            var ergebnis = TeamMehrheit.Ermittle(Stimmen(team, sprecherstimme, "A", "B", "B", "A"), team, team[0]);

            Assert.IsNull(ergebnis?.Antwort, "Ein offener Gleichstand hat eine Antwort: " + ergebnis?.Antwort);
            Assert.AreEqual(TeamMehrheitEntscheid.Offen, ergebnis!.Entscheid);
            CollectionAssert.AreEqual(new[] { "A", "B" }, ergebnis.Gleichauf.ToArray());
        }

        [TestMethod]
        public void TheMasterDecidesATieAndMayOverruleTheSpeaker()
        {
            var team = Ids(4);
            var stimmen = Stimmen(team, "A", "B", "B", "A");

            var dreifach = TeamMehrheit.Ermittle(Stimmen(team, "C", "A", "B"), team, team[0], entscheid: "B");
            var ueberstimmt = TeamMehrheit.Ermittle(stimmen, team, team[1], entscheid: "A");

            Assert.AreEqual(("B", TeamMehrheitEntscheid.Spielleitung), (dreifach?.Antwort, dreifach!.Entscheid));
            Assert.AreEqual(("A", TeamMehrheitEntscheid.Spielleitung), (ueberstimmt?.Antwort, ueberstimmt!.Entscheid), "Die Spielleitung kann den Sprecher nicht überstimmen.");
        }

        /// <summary>Was die Spielleitung wählt, zählt nur unter den Gleichauf - und nie gegen eine klare Mehrheit.</summary>
        [TestMethod]
        public void AMasterChoiceOutsideTheTiedIsIgnored()
        {
            var team = Ids(4);

            var gleichstand = TeamMehrheit.Ermittle(Stimmen(team, "A", "B", "B", "A"), team, team[1], entscheid: "C");
            var mehrheit = TeamMehrheit.Ermittle(Stimmen(team, "A", "B", "B", "B"), team, team[0], entscheid: "A");

            Assert.AreEqual(("B", TeamMehrheitEntscheid.Sprecher), (gleichstand?.Antwort, gleichstand!.Entscheid));
            Assert.AreEqual(("B", TeamMehrheitEntscheid.Mehrheit), (mehrheit?.Antwort, mehrheit!.Entscheid), "Die Spielleitung überstimmt eine klare Mehrheit.");
        }

        [TestMethod]
        public void WithoutAVoteThereIsNoAnswer()
        {
            var team = Ids(3);

            Assert.IsNull(TeamMehrheit.Ermittle(Stimmen(team, null, null, null), team, team[0]));
            Assert.IsNull(TeamMehrheit.Ermittle(Stimmen(team, "", "", null), team, team[0]), "Eine leere Auswahl zählt als Stimme.");
        }

        /// <summary>Fail-closed: eine Stimme aus einem anderen Team kippt die Mehrheit nicht.</summary>
        [TestMethod]
        public void AVoteFromOutsideTheTeamDoesNotCount()
        {
            var team = Ids(2);
            var fremde = Ids(3);
            var stimmen = Stimmen(team, "A", null);

            foreach (var f in fremde)
                stimmen[f] = "B";

            var ergebnis = TeamMehrheit.Ermittle(stimmen, team, team[1]);

            Assert.AreEqual(("A", 1, 1), (ergebnis?.Antwort, ergebnis!.Stimmen, ergebnis.Abgegeben));
            Assert.IsNull(TeamMehrheit.Ermittle(Stimmen(fremde, "B", "B", "B"), team, team[0]), "Nur fremde Stimmen ergeben eine Antwort.");
        }

        [TestMethod]
        public void AVoteIsTheWholeSelectionInAnyOrder()
        {
            Assert.AreEqual("A,C", TeamMehrheit.Schluessel(["C", "A", "A", " "]));
            Assert.AreEqual(string.Empty, TeamMehrheit.Schluessel([]));
            Assert.AreEqual(string.Empty, TeamMehrheit.Schluessel(null));
            Assert.AreEqual("A + C", TeamMehrheit.Anzeige("A,C"));

            var team = Ids(3);
            var stimmen = new Dictionary<Guid, string>
            {
                [team[0]] = TeamMehrheit.Schluessel(["C", "A"]),
                [team[1]] = TeamMehrheit.Schluessel(["A", "C"]),
                [team[2]] = TeamMehrheit.Schluessel(["A"]),
            };

            Assert.AreEqual("A,C", TeamMehrheit.Ermittle(stimmen, team, team[2])?.Antwort, "Dieselbe Auswahl in anderer Reihenfolge zählt nicht zusammen.");
        }

        /// <summary>
        /// Über viele Muster: die Antwort ist immer eine abgegebene Stimme und eine der meistgewählten; ein Gleichstand fällt nur
        /// an den Sprecher, wenn seine Stimme darunter ist, sonst bleibt er offen.
        /// </summary>
        [TestMethod]
        public void OverManyPatternsTheAnswerIsAlwaysAMostVotedVote()
        {
            var zufall = new Random(37);
            string?[] tasten = [null, "A", "B", "C"];
            var gleichstaende = 0;

            for (var lauf = 0; lauf < 2000; lauf++)
            {
                var team = Ids(zufall.Next(1, 7));
                var stimmen = Stimmen(team, team.Select(_ => tasten[zufall.Next(tasten.Length)]).ToArray());
                var sprecher = team[zufall.Next(team.Length)];

                var ergebnis = TeamMehrheit.Ermittle(stimmen, team, sprecher);

                if (stimmen.Count == 0)
                {
                    Assert.IsNull(ergebnis, $"Lauf {lauf}: ohne Stimme eine Antwort.");
                    continue;
                }

                var meiste = stimmen.Values.GroupBy(s => s).Max(g => g.Count());
                var gleichauf = stimmen.Values.GroupBy(s => s).Where(g => g.Count() == meiste).Select(g => g.Key).ToHashSet();
                var sprecherGleichauf = stimmen.TryGetValue(sprecher, out var s) && gleichauf.Contains(s);

                Assert.AreEqual(meiste, ergebnis!.Stimmen, $"Lauf {lauf}");
                CollectionAssert.AreEquivalent(gleichauf.ToList(), ergebnis.Gleichauf.ToList(), $"Lauf {lauf}");

                if (gleichauf.Count == 1)
                    Assert.AreEqual(gleichauf.Single(), ergebnis.Antwort, $"Lauf {lauf}: nicht die Mehrheit.");
                else if (sprecherGleichauf)
                    Assert.AreEqual(s, ergebnis.Antwort, $"Lauf {lauf}: der Gleichstand fiel nicht auf den Sprecher.");
                else
                    Assert.IsNull(ergebnis.Antwort, $"Lauf {lauf}: ein offener Gleichstand hat eine Antwort.");

                gleichstaende += gleichauf.Count > 1 ? 1 : 0;
            }

            Assert.IsTrue(gleichstaende > 100, "Zu wenige Gleichstände - die Zusicherung prüfte den Zweig kaum: " + gleichstaende);
        }
    }
}
