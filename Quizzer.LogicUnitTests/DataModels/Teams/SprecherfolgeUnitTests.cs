using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Questions.Teams;

namespace Quizzer.LogicUnitTests.DataModels.Teams
{
    /// <summary>
    /// Wer im Team spricht (M37 T5): nach Anzeigename, bei gleichem Namen nach Kennung, ab <c>turn mod n</c>; wer gesperrt
    /// oder getrennt ist, wird übersprungen, und „Sprecher zurück“ führt vom Anfang zum Letzten.
    /// </summary>
    [TestClass]
    public class SprecherfolgeUnitTests
    {
        private static readonly Teammitglied Anna = new(Guid.NewGuid(), "Anna");
        private static readonly Teammitglied Bert = new(Guid.NewGuid(), "Bert");
        private static readonly Teammitglied Clara = new(Guid.NewGuid(), "clara");

        /// <summary>Durcheinander übergeben - die Folge sortiert selbst, ohne auf Groß- und Kleinschreibung zu achten.</summary>
        private static readonly Teammitglied[] Team = [Clara, Anna, Bert];

        [TestMethod]
        [DataRow(0, new[] { "Anna", "Bert", "clara" })]
        [DataRow(1, new[] { "Bert", "clara", "Anna" })]
        [DataRow(2, new[] { "clara", "Anna", "Bert" })]
        [DataRow(3, new[] { "Anna", "Bert", "clara" })]
        [DataRow(7, new[] { "Bert", "clara", "Anna" })]
        [DataRow(-1, new[] { "clara", "Anna", "Bert" })]
        public void TheOrderGoesByNameFromTurnModN(int turn, string[] erwartet)
        {
            var folge = Sprecherfolge.Folge(Team, turn);

            CollectionAssert.AreEqual(erwartet, folge.Select(Name).ToArray(), $"Stand {turn}");
        }

        /// <summary>Gleiche Namen: die Kennung entscheidet - sonst wechselte der Sprecher mit der Reihenfolge der Übergabe.</summary>
        [TestMethod]
        public void EqualNamesAreOrderedById()
        {
            var a = new Teammitglied(Guid.Parse("00000000-0000-0000-0000-000000000001"), "Kim");
            var b = new Teammitglied(Guid.Parse("00000000-0000-0000-0000-000000000002"), "Kim");

            CollectionAssert.AreEqual(new[] { a.Id, b.Id }, Sprecherfolge.Folge([b, a], 0).ToArray());
            CollectionAssert.AreEqual(new[] { a.Id, b.Id }, Sprecherfolge.Folge([a, b], 0).ToArray());
        }

        [TestMethod]
        public void TheSpeakerIsTheFirstOfTheOrder()
        {
            Assert.AreEqual(Bert.Id, Sprecherfolge.Sprecher(Sprecherfolge.Folge(Team, 1)));
        }

        /// <summary>Per Joker gesperrt oder getrennt: der Nächste spricht, und nach dem Letzten kommt der Erste.</summary>
        [TestMethod]
        public void ASkippedSpeakerPassesToTheNextAndWrapsAround()
        {
            var folge = Sprecherfolge.Folge(Team, 2);

            Assert.AreEqual(Anna.Id, Sprecherfolge.Sprecher(folge, new HashSet<Guid> { Clara.Id }), "Clara ist gesperrt, und sie spricht trotzdem.");
            Assert.AreEqual(Bert.Id, Sprecherfolge.Sprecher(folge, new HashSet<Guid> { Clara.Id, Anna.Id }));
            Assert.IsNull(Sprecherfolge.Sprecher(folge, new HashSet<Guid> { Clara.Id, Anna.Id, Bert.Id }), "Alle gesperrt, und doch spricht einer.");
        }

        [TestMethod]
        public void AnEmptyTeamHasNoSpeaker()
        {
            Assert.AreEqual(0, Sprecherfolge.Folge([], 5).Count);
            Assert.IsNull(Sprecherfolge.Sprecher([]));
        }

        [TestMethod]
        public void ADuplicateMemberCountsOnce()
        {
            CollectionAssert.AreEqual(new[] { "Bert", "Anna" }, Sprecherfolge.Folge([Anna, Bert, Anna], 1).Select(Name).ToArray());
        }

        [TestMethod]
        public void ForwardAndBackStayAtOrAboveZero()
        {
            Assert.AreEqual(1, Sprecherfolge.Weiter(0));
            Assert.AreEqual(0, Sprecherfolge.Weiter(int.MaxValue), "Am Ende des Zählers wird er negativ.");
            Assert.AreEqual(0, Sprecherfolge.Weiter(-3), "Ein negativer Stand bleibt negativ.");

            Assert.AreEqual(4, Sprecherfolge.Zurueck(5, 3));
            Assert.AreEqual(2, Sprecherfolge.Zurueck(0, 3), "Vom Anfang zurück ist der Letzte der Folge dran.");
            Assert.AreEqual(0, Sprecherfolge.Zurueck(0, 0));

            var zurueck = Sprecherfolge.Folge(Team, Sprecherfolge.Zurueck(0, Team.Length));
            Assert.AreEqual("clara", Name(zurueck[0]), "„Sprecher zurück“ vom Anfang trifft nicht den Letzten.");
        }

        private static string Name(Guid id) => Team.Single(m => m.Id == id).Name;
    }
}
