using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Questions.Sonderregeln;
using Quizzer.DataModels.Questions.Teams;

namespace Quizzer.LogicUnitTests.DataModels.Teams
{
    /// <summary>
    /// „Alle oder keiner" (M37 T6): alle, die antworten konnten, richtig - der Feldwert aufgeteilt; einer falsch oder nur
    /// teilweise richtig - das Minus einmal, aufgeteilt; vorher steht nichts fest. Wer nicht antworten konnte, zählt nicht mit,
    /// bekommt aber seinen Anteil; konnte niemand antworten, gibt es weder Plus noch Minus.
    /// </summary>
    [TestClass]
    public class AlleOderKeinerUnitTests
    {
        private static Guid[] Ids(int anzahl) => Enumerable.Range(0, anzahl).Select(_ => Guid.NewGuid()).ToArray();

        private static readonly IReadOnlySet<Guid> Niemand = new HashSet<Guid>();

        private static Dictionary<Guid, RatingKind> Bewertet(Guid[] team, params RatingKind[] bewertungen)
            => team.Zip(bewertungen).ToDictionary(p => p.First, p => p.Second);

        [TestMethod]
        public void AllRightBringsTheFieldValueSplitOverTheTeam()
        {
            var team = Ids(3);

            var ergebnis = AlleOderKeiner.Werte(100, 60, team, Niemand, Bewertet(team, RatingKind.Right, RatingKind.Right, RatingKind.Right));

            Assert.AreEqual(RatingKind.Right, ergebnis?.Bewertung);
            CollectionAssert.AreEqual(new[] { 34, 33, 33 }, team.Select(m => ergebnis!.Anteile[m]).ToArray());
        }

        [TestMethod]
        [DataRow(RatingKind.Wrong)]
        [DataRow(RatingKind.Partial)]
        public void OneNotRightCostsTheTeamTheMinusOnce(RatingKind einer)
        {
            var team = Ids(3);

            var ergebnis = AlleOderKeiner.Werte(100, 60, team, Niemand, Bewertet(team, RatingKind.Right, einer, RatingKind.None));

            Assert.AreEqual(RatingKind.Wrong, ergebnis?.Bewertung, $"{einer} gilt als richtig.");
            Assert.AreEqual(-60, ergebnis!.Anteile.Values.Sum(), "Das Team verliert nicht genau einmal die Minuspunkte.");
            Assert.IsTrue(ergebnis.Anteile.Values.All(a => a == -20));
        }

        [TestMethod]
        public void WhileSomeoneIsUnratedNothingIsSettled()
        {
            var team = Ids(3);

            var ergebnis = AlleOderKeiner.Werte(100, 60, team, Niemand, Bewertet(team, RatingKind.Right, RatingKind.None));

            Assert.AreEqual(RatingKind.None, ergebnis?.Bewertung);
            Assert.AreEqual(2, ergebnis!.Offen, "Ein fehlender Eintrag zählt nicht als unbewertet.");
            Assert.AreEqual(0, ergebnis.Anteile.Count, "Vor der letzten Bewertung steht ein Anteil fest.");
        }

        /// <summary>Wer gesperrt oder getrennt ist, muss nicht richtig sein - seine Karte zählt nicht, sein Anteil schon.</summary>
        [TestMethod]
        public void WhoCouldNotAnswerDoesNotCountButGetsHisShare()
        {
            var team = Ids(3);
            var gesperrt = new HashSet<Guid> { team[2] };

            var ergebnis = AlleOderKeiner.Werte(90, 60, team, gesperrt, Bewertet(team, RatingKind.Right, RatingKind.Right, RatingKind.Wrong));

            Assert.AreEqual(RatingKind.Right, ergebnis?.Bewertung, "Die Karte des Gesperrten zählt.");
            Assert.AreEqual(30, ergebnis!.Anteile[team[2]], "Der Gesperrte bekommt seinen Anteil nicht.");
        }

        /// <summary>A06: konnte niemand antworten, ist „alle richtig" nicht wahr - kein Plus, und ohne Abgabe kein Minus.</summary>
        [TestMethod]
        public void WhenNobodyCouldAnswerThereIsNothing()
        {
            var team = Ids(2);
            var alle = team.ToHashSet();

            Assert.IsNull(AlleOderKeiner.Werte(100, 60, team, alle, Bewertet(team, RatingKind.Right, RatingKind.Right)), "Ein ganz gesperrtes Team gewinnt.");
            Assert.IsNull(AlleOderKeiner.Werte(100, 60, [], Niemand, new Dictionary<Guid, RatingKind>()), "Ein leeres Team gewinnt.");
        }

        /// <summary>A01: die Bewertung eines Fremden zählt nicht fürs Team; ein doppelt genanntes Mitglied zählt einmal.</summary>
        [TestMethod]
        public void OnlyTheMembersRatingsCount()
        {
            var team = Ids(2);
            var bewertungen = Bewertet(team, RatingKind.Right, RatingKind.Right);
            bewertungen[Guid.NewGuid()] = RatingKind.Wrong;

            var ergebnis = AlleOderKeiner.Werte(100, 60, [team[0], team[1], team[1]], Niemand, bewertungen);

            Assert.AreEqual(RatingKind.Right, ergebnis?.Bewertung, "Ein Fremder macht das Team falsch.");
            Assert.AreEqual(2, ergebnis!.Anteile.Count);
            Assert.AreEqual(100, ergebnis.Anteile.Values.Sum());
        }

        [TestMethod]
        public void TheMinusIsNegativeHoweverItIsPassed()
        {
            var team = Ids(2);
            var falsch = Bewertet(team, RatingKind.Wrong, RatingKind.Right);

            Assert.AreEqual(-60, AlleOderKeiner.Werte(100, 60, team, Niemand, falsch)!.Anteile.Values.Sum());
            Assert.AreEqual(-60, AlleOderKeiner.Werte(100, -60, team, Niemand, falsch)!.Anteile.Values.Sum());
        }

        /// <summary>A08 über viele Bewertungen: das Team bekommt genau den Feldwert, genau das Minus oder nichts - nie etwas dazwischen.</summary>
        [TestMethod]
        public void TheTeamGetsExactlyTheFieldValueTheMinusOrNothing()
        {
            var arten = new[] { RatingKind.None, RatingKind.Right, RatingKind.Wrong, RatingKind.Partial };
            var zufall = new Random(37);

            for (var groesse = 1; groesse <= TeamGrenzen.MaxTeams; groesse++)
            for (var runde = 0; runde < 40; runde++)
            {
                var team = Ids(groesse);
                var bewertungen = team.ToDictionary(m => m, _ => arten[zufall.Next(arten.Length)]);
                var gesperrt = team.Where(_ => zufall.Next(4) == 0).ToHashSet();

                var ergebnis = AlleOderKeiner.Werte(300, 120, team, gesperrt, bewertungen);
                var summe = ergebnis?.Anteile.Values.Sum() ?? 0;
                var fall = $"{string.Join("/", team.Select(m => bewertungen[m] + (gesperrt.Contains(m) ? "(gesperrt)" : "")))}";

                Assert.IsTrue(summe is 300 or -120 or 0, $"Das Team bekommt {summe}. {fall}");
                Assert.AreEqual(summe == 0 ? 0 : groesse, ergebnis?.Anteile.Count ?? 0, "Nicht jedes Mitglied hat seinen Anteil. " + fall);
            }
        }

        /// <summary>R3-3: ein negativer Feldwert (Altbestand, fremdes Bündel) macht aus „alle richtig" kein Minus - wie die Siegerteam-Arten.</summary>
        [TestMethod]
        public void ANegativeFieldValueMakesAllRightNoMinus()
        {
            var team = Ids(3);

            var ergebnis = AlleOderKeiner.Werte(-300, 60, team, Niemand, Bewertet(team, RatingKind.Right, RatingKind.Right, RatingKind.Right));

            Assert.IsNotNull(ergebnis);
            Assert.AreEqual(RatingKind.Right, ergebnis.Bewertung);
            Assert.IsTrue(ergebnis.Anteile.Values.All(a => a == 0), "Alle richtig kostet Punkte: " + string.Join(", ", ergebnis.Anteile.Values));
        }
    }
}
