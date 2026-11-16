using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Questions.Sonderregeln;

namespace Quizzer.LogicUnitTests.DataModels.Sonderregeln
{
    /// <summary>
    /// Die Wirkung der Joker als reine Funktion (M35 C7b): Doppelt verdoppelt Plus, Minus und Teilwert, gekappt
    /// nach E2; Schutz nimmt nur das Minus; die Sperre und eine fehlende Bewertung ändern nichts.
    /// </summary>
    [TestClass]
    public class JokerRegelUnitTests
    {
        private const int Feld = 100;

        private static SonderregelEingang Eingang(RatingKind bewertung, int grundwert, int kontostand = 1000, bool unterNull = true)
            => new(bewertung, grundwert, Feld, new Kappung(kontostand, unterNull));

        [TestMethod]
        [DataRow(RatingKind.Right, 100, 200)]
        [DataRow(RatingKind.Wrong, -40, -80)]
        [DataRow(RatingKind.Partial, 33, 66)]
        public void DoubleDoublesPlusMinusAndAPartialValue(RatingKind bewertung, int grundwert, int erwartet)
        {
            var vorschlag = JokerRegel.Werte(JokerKind.Double, Eingang(bewertung, grundwert));

            Assert.AreEqual(erwartet, vorschlag?.Punkte);
            Assert.AreEqual(JokerRegel.DoppeltGrund, vorschlag?.Grund);
        }

        [TestMethod]
        public void ADoubledMinusIsCappedAtTheBalanceWhenTheGameForbidsMinus()
        {
            var gekappt = JokerRegel.Werte(JokerKind.Double, Eingang(RatingKind.Wrong, -40, kontostand: 50, unterNull: false));
            var frei = JokerRegel.Werte(JokerKind.Double, Eingang(RatingKind.Wrong, -40, kontostand: 50, unterNull: true));

            Assert.AreEqual(-50, gekappt!.Punkte, "Das verdoppelte Minus ist nicht am Kontostand gekappt.");
            StringAssert.StartsWith(gekappt.Grund, JokerRegel.DoppeltGrund);
            Assert.AreNotEqual(JokerRegel.DoppeltGrund, gekappt.Grund, "Der Grund sagt nicht, dass gekappt wurde.");
            Assert.AreEqual(-80, frei!.Punkte, "Mit erlaubtem Minus ist trotzdem gekappt.");
        }

        [TestMethod]
        public void ShieldTakesTheMinusAndLeavesThePlus()
        {
            Assert.AreEqual(0, JokerRegel.Werte(JokerKind.Shield, Eingang(RatingKind.Wrong, -40))?.Punkte, "Das Minus bleibt.");
            Assert.IsNull(JokerRegel.Werte(JokerKind.Shield, Eingang(RatingKind.Right, 100)), "Der Schutz ändert das Plus.");
            Assert.IsNull(JokerRegel.Werte(JokerKind.Shield, Eingang(RatingKind.Partial, 33)), "Der Schutz ändert einen Teilwert.");
        }

        [TestMethod]
        public void BlockAndAMissingRatingChangeNothing()
        {
            foreach (var art in Enum.GetValues<JokerKind>())
                Assert.IsNull(JokerRegel.Werte(art, Eingang(RatingKind.None, 0)), $"{art} greift ohne Bewertung.");

            Assert.IsNull(JokerRegel.Werte(JokerKind.Block, Eingang(RatingKind.Right, 100)));
            Assert.IsNull(JokerRegel.Werte(JokerKind.Block, Eingang(RatingKind.Wrong, -40)));
        }
    }
}
