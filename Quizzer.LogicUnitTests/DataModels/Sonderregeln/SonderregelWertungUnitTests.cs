using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Questions.Minispiele;
using Quizzer.DataModels.Questions.Sonderregeln;

namespace Quizzer.LogicUnitTests.DataModels.Sonderregeln
{
    /// <summary>
    /// Die Wertung der Sonderregeln Einsatz und Risiko (M35 C3) als reine Funktionen: richtig, falsch,
    /// Teilwert, nicht bewertet, Kappung an und aus (E2) und die Obergrenze des Einsatzes.
    /// </summary>
    [TestClass]
    public class SonderregelWertungUnitTests
    {
        private const int Feld = 100;

        private static SonderregelEingang Eingang(RatingKind bewertung, int grundwert, int kontostand = 1000, bool unterNull = true)
            => new(bewertung, grundwert, Feld, new Kappung(kontostand, unterNull));

        // --- Einsatz ---

        [TestMethod]
        public void RightWinsTheStakeInsteadOfTheFieldPoints()
        {
            var vorschlag = EinsatzRegel.Werte(250, Eingang(RatingKind.Right, Feld));

            Assert.AreEqual(new Punktevorschlag(250, "Einsatz 250"), vorschlag);
        }

        [TestMethod]
        public void WrongLosesTheStakeInsteadOfTheMinusPoints()
        {
            var vorschlag = EinsatzRegel.Werte(250, Eingang(RatingKind.Wrong, -50));

            Assert.AreEqual(-250, vorschlag!.Punkte);
            Assert.AreEqual("Einsatz 250", vorschlag.Grund);
        }

        [TestMethod]
        public void ALostStakeIsCappedAtTheBalanceWhenTheGameForbidsMinus()
        {
            var gekappt = EinsatzRegel.Werte(250, Eingang(RatingKind.Wrong, -50, kontostand: 80, unterNull: false));
            var frei = EinsatzRegel.Werte(250, Eingang(RatingKind.Wrong, -50, kontostand: 80, unterNull: true));

            Assert.AreEqual(-80, gekappt!.Punkte, "Kappung aus: mehr abgezogen, als der Spieler hat.");
            StringAssert.Contains(gekappt.Grund, Punkteanteil.GekapptText);
            Assert.AreEqual(-250, frei!.Punkte, "Kappung an, obwohl das Spiel ein Minus erlaubt.");
            Assert.AreEqual("Einsatz 250", frei.Grund);
        }

        [TestMethod]
        [DataRow(200, 50, 100)]
        [DataRow(100, 33, 33)]
        [DataRow(100, 67, 67)]
        [DataRow(150, 50, 75)]
        [DataRow(0, 50, 0)]
        public void APartialValueCountsItsShareOfTheStake(int einsatz, int teilwert, int erwartet)
        {
            var vorschlag = EinsatzRegel.Werte(einsatz, Eingang(RatingKind.Partial, teilwert));

            Assert.AreEqual(erwartet, vorschlag!.Punkte);
        }

        [TestMethod]
        public void APartialValueNeverExceedsTheStake()
        {
            Assert.AreEqual(100, EinsatzRegel.Anteil(100, 250, Feld));
            Assert.AreEqual(-100, EinsatzRegel.Anteil(100, -250, Feld));
            Assert.AreEqual(0, EinsatzRegel.Anteil(100, 50, 0), "Ohne Feldpunkte gibt es keinen Anteil.");
        }

        [TestMethod]
        public void WhoIsNotRatedGetsNothingFromTheStake()
        {
            Assert.IsNull(EinsatzRegel.Werte(250, Eingang(RatingKind.None, 0)));
        }

        [TestMethod]
        [DataRow(500, 100, 500)]
        [DataRow(40, 100, 100)]
        [DataRow(-300, 100, 100)]
        [DataRow(-300, 0, 0)]
        public void TheUpperLimitIsTheLargerOfBalanceAndFieldPoints(int kontostand, int feldpunkte, int erwartet)
        {
            Assert.AreEqual(erwartet, EinsatzRegel.Obergrenze(kontostand, feldpunkte));
        }

        [TestMethod]
        public void OnlyZeroUpToTheLimitIsAllowed()
        {
            Assert.IsTrue(EinsatzRegel.Zulaessig(0, 40, 100));
            Assert.IsTrue(EinsatzRegel.Zulaessig(100, 40, 100));
            Assert.IsFalse(EinsatzRegel.Zulaessig(101, 40, 100), "Ueber der Obergrenze angenommen.");
            Assert.IsFalse(EinsatzRegel.Zulaessig(-1, 40, 100), "Ein negativer Einsatz angenommen.");
        }

        // --- Risiko ---

        [TestMethod]
        public void DoublingDoublesPlusMinusAndAPartialValue()
        {
            Assert.AreEqual(new Punktevorschlag(200, RisikoRegel.DoppeltGrund), RisikoRegel.Werte(true, Eingang(RatingKind.Right, Feld)));
            Assert.AreEqual(-100, RisikoRegel.Werte(true, Eingang(RatingKind.Wrong, -50))!.Punkte);
            Assert.AreEqual(66, RisikoRegel.Werte(true, Eingang(RatingKind.Partial, 33))!.Punkte);
        }

        [TestMethod]
        public void ADoubledMinusIsCappedWhenTheGameForbidsMinus()
        {
            var gekappt = RisikoRegel.Werte(true, Eingang(RatingKind.Wrong, -50, kontostand: 70, unterNull: false));

            Assert.AreEqual(-70, gekappt!.Punkte);
            StringAssert.Contains(gekappt.Grund, Punkteanteil.GekapptText);
            Assert.AreEqual(-100, RisikoRegel.Werte(true, Eingang(RatingKind.Wrong, -50, kontostand: 70, unterNull: true))!.Punkte);
        }

        [TestMethod]
        public void NoOrUnratedLeavesTheRatingAsItIs()
        {
            Assert.IsNull(RisikoRegel.Werte(false, Eingang(RatingKind.Right, Feld)), "Nein hat trotzdem verdoppelt.");
            Assert.IsNull(RisikoRegel.Werte(false, Eingang(RatingKind.Wrong, -50)));
            Assert.IsNull(RisikoRegel.Werte(true, Eingang(RatingKind.None, 0)), "Nicht bewertet und trotzdem eine Bewegung.");
        }
    }
}
