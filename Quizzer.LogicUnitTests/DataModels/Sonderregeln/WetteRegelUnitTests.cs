using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Questions.Sonderregeln;

namespace Quizzer.LogicUnitTests.DataModels.Sonderregeln
{
    /// <summary>
    /// Wette (M35 C3b) als reine Rechnung: Wettkampf-Rang (Gleichauf teilen den besseren Platz), Quote 1 + Rang/Anzahl
    /// kaufmaennisch gerundet, nur „richtig" gewinnt, eigene Bewertung zaehlt dazu, Kappung (E2) an und aus.
    /// </summary>
    [TestClass]
    public class WetteRegelUnitTests
    {
        private static readonly Guid Anna = Guid.NewGuid();
        private static readonly Guid Bert = Guid.NewGuid();
        private static readonly Guid Clara = Guid.NewGuid();
        private static readonly Guid Dora = Guid.NewGuid();

        private static SonderregelEingang Eigener(RatingKind bewertung, int grundwert, int kontostand = 500, bool unterNull = true)
            => new(bewertung, grundwert, 100, new Kappung(kontostand, unterNull));

        [TestMethod]
        public void TiedPlayersShareTheBetterRankAndTheLastPaysMost()
        {
            var konten = new Dictionary<Guid, int> { [Anna] = 300, [Bert] = 100, [Clara] = 100, [Dora] = 0 };

            Assert.AreEqual(1, WetteRegel.Rang(Anna, konten));
            Assert.AreEqual(2, WetteRegel.Rang(Bert, konten), "Gleichauf teilen nicht den besseren Platz.");
            Assert.AreEqual(2, WetteRegel.Rang(Clara, konten));
            Assert.AreEqual(4, WetteRegel.Rang(Dora, konten), "Nach zwei Gleichauf kommt nicht Platz 4.");

            // Einsatz 50: Quote 1 + 1/4, 1 + 2/4, 1 + 4/4.
            Assert.AreEqual(63, WetteRegel.Auf(Anna, "Anna", 50, konten).Gewinn, "62,5 ist nicht kaufmaennisch auf 63 gerundet.");
            Assert.AreEqual(75, WetteRegel.Auf(Bert, "Bert", 50, konten).Gewinn);
            Assert.AreEqual(100, WetteRegel.Auf(Dora, "Dora", 50, konten).Gewinn, "Der Letzte bringt nicht das Doppelte.");
            Assert.AreEqual(50, WetteRegel.Einsatz(100, 50), "Der Einsatz ist nicht der Anteil der Feldpunkte.");
        }

        [TestMethod]
        public void OnlyARightAnswerOfTheChosenWinsAndTheOwnRatingCountsToo()
        {
            var wette = new Wette(Bert, "Bert", 50, 75);

            Assert.AreEqual(75, WetteRegel.Werte(wette, RatingKind.Right, Eigener(RatingKind.None, 0)).Punkte);
            Assert.AreEqual(-50, WetteRegel.Werte(wette, RatingKind.Partial, Eigener(RatingKind.None, 0)).Punkte, "Ein Teilwert gewinnt.");
            Assert.AreEqual(-50, WetteRegel.Werte(wette, RatingKind.None, Eigener(RatingKind.None, 0)).Punkte, "Unbewertet gewinnt.");
            Assert.AreEqual(-50, WetteRegel.Werte(wette, RatingKind.Wrong, Eigener(RatingKind.None, 0)).Punkte);

            // Die eigene Antwort zaehlt dazu: richtig +100 und Wette gewonnen.
            Assert.AreEqual(175, WetteRegel.Werte(wette, RatingKind.Right, Eigener(RatingKind.Right, 100)).Punkte, "Die eigene Bewertung fehlt.");
            StringAssert.Contains(WetteRegel.Werte(wette, RatingKind.Wrong, Eigener(RatingKind.None, 0)).Grund, "Wette auf Bert verloren");
        }

        [TestMethod]
        public void TheSumIsCappedOnceOnlyWhenTheGameForbidsMinus()
        {
            var wette = new Wette(Bert, "Bert", 50, 75);

            // Falsch -30 und Wette verloren -50 = -80, gekappt am Kontostand 60.
            var gekappt = WetteRegel.Werte(wette, RatingKind.Wrong, Eigener(RatingKind.Wrong, -30, kontostand: 60, unterNull: false));
            Assert.AreEqual(-60, gekappt.Punkte, "Die Summe ist nicht einmal am Kontostand gekappt.");
            StringAssert.Contains(gekappt.Grund, "gekappt");

            Assert.AreEqual(-80, WetteRegel.Werte(wette, RatingKind.Wrong, Eigener(RatingKind.Wrong, -30, kontostand: 60, unterNull: true)).Punkte,
                "Gekappt, obwohl das Spiel ein Minus erlaubt.");
        }
    }
}
