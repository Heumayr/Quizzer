using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Questions.Sonderregeln;

namespace Quizzer.LogicUnitTests.DataModels.Sonderregeln
{
    /// <summary>
    /// Experte (M35 C3b) als reine Rechnung: die Meistgewaehlten in Spielerreihenfolge, niemand waehlt = keiner, und
    /// nur die Waehler des Experten bekommen den Anteil - nur wenn er richtig ist.
    /// </summary>
    [TestClass]
    public class ExperteRegelUnitTests
    {
        private static readonly Guid Anna = Guid.NewGuid();
        private static readonly Guid Bert = Guid.NewGuid();
        private static readonly Guid Clara = Guid.NewGuid();
        private static readonly Guid Dora = Guid.NewGuid();
        private static readonly IReadOnlyList<Guid> Reihenfolge = [Anna, Bert, Clara, Dora];

        private static SonderregelEingang Eingang(RatingKind bewertung, int grundwert = 0)
            => new(bewertung, grundwert, 100, new Kappung(500, true));

        [TestMethod]
        public void TheMostChosenAreTheCandidatesInPlayerOrder()
        {
            var wahlen = new Dictionary<Guid, Guid> { [Anna] = Clara, [Bert] = Clara, [Clara] = Bert, [Dora] = Bert };

            CollectionAssert.AreEqual(new[] { Bert, Clara }, ExperteRegel.Kandidaten(wahlen, Reihenfolge).ToList(),
                "Bei Gleichstand stehen die Kandidaten nicht in Spielerreihenfolge.");

            wahlen[Dora] = Clara;
            CollectionAssert.AreEqual(new[] { Clara }, ExperteRegel.Kandidaten(wahlen, Reihenfolge).ToList(), "Nicht der Meistgewaehlte.");
            Assert.AreEqual(0, ExperteRegel.Kandidaten(new Dictionary<Guid, Guid>(), Reihenfolge).Count, "Niemand waehlt - trotzdem ein Kandidat.");
        }

        [TestMethod]
        public void OnlyTheVotersOfARightExpertGetTheShare()
        {
            var wahlen = new Dictionary<Guid, Guid> { [Anna] = Clara, [Bert] = Clara, [Clara] = Bert };
            var eingaenge = new Dictionary<Guid, SonderregelEingang>
            {
                [Anna] = Eingang(RatingKind.None), [Bert] = Eingang(RatingKind.None), [Clara] = Eingang(RatingKind.Right, 100),
            };

            var vorschlaege = ExperteRegel.Werte(Clara, wahlen, eingaenge, 25);

            CollectionAssert.AreEquivalent(new[] { Anna, Bert }, vorschlaege.Keys.ToList(), "Nicht genau die Waehler des Experten.");
            Assert.AreEqual(25, vorschlaege[Anna].Punkte);
            StringAssert.Contains(vorschlaege[Anna].Grund, "Experte");

            eingaenge[Clara] = Eingang(RatingKind.Partial, 40);
            Assert.AreEqual(0, ExperteRegel.Werte(Clara, wahlen, eingaenge, 25).Count, "Ein Teilwert des Experten bringt den Waehlern etwas.");
            eingaenge[Clara] = Eingang(RatingKind.Wrong, -50);
            Assert.AreEqual(0, ExperteRegel.Werte(Clara, wahlen, eingaenge, 25).Count, "Ein falscher Experte bringt den Waehlern etwas.");
        }
    }
}
