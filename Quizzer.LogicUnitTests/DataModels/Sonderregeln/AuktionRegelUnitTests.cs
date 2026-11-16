using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Questions.Sonderregeln;

namespace Quizzer.LogicUnitTests.DataModels.Sonderregeln
{
    /// <summary>
    /// Auktion (M35 C3b) als reine Rechnung: Zuschlag (hoechstes Gebot, Gleichstand das fruehere, 0 = passen),
    /// Hoechst- und Zweitpreis, und der Vorschlag fuer den Gewinner (zahlt immer, richtig doppelt, Kappung E2).
    /// </summary>
    [TestClass]
    public class AuktionRegelUnitTests
    {
        private static readonly Guid Anna = Guid.NewGuid();
        private static readonly Guid Bert = Guid.NewGuid();
        private static readonly Guid Clara = Guid.NewGuid();

        private static SonderregelEingang Eingang(RatingKind bewertung, int grundwert, int kontostand = 500, bool unterNull = true)
            => new(bewertung, grundwert, 100, new Kappung(kontostand, unterNull));

        [TestMethod]
        public void TheHighestBidWinsAndATieGoesToTheEarlierBid()
        {
            var gebote = new Dictionary<Guid, Gebot> { [Anna] = new(80, 3), [Bert] = new(80, 2), [Clara] = new(40, 1) };

            var hoechstpreis = AuktionRegel.Ermittle(gebote, zweitpreis: false)!;
            Assert.AreEqual(Bert, hoechstpreis.Gewinner, "Bei Gleichstand gewinnt nicht das fruehere Gebot.");
            Assert.AreEqual(80, hoechstpreis.Preis, "Hoechstpreis: der Gewinner zahlt nicht sein Gebot.");

            // Zweitpreis: das zweithoechste Gebot - beim Gleichstand also dasselbe.
            Assert.AreEqual(80, AuktionRegel.Ermittle(gebote, zweitpreis: true)!.Preis);

            gebote[Anna] = new(70, 3);
            Assert.AreEqual(70, AuktionRegel.Ermittle(gebote, zweitpreis: true)!.Preis, "Zweitpreis: nicht das zweithoechste Gebot.");
        }

        [TestMethod]
        public void PassingIsNoBidAndASingleBidderPaysNothingAtSecondPrice()
        {
            var gebote = new Dictionary<Guid, Gebot> { [Anna] = new(0, 1), [Bert] = new(30, 2) };

            var zuschlag = AuktionRegel.Ermittle(gebote, zweitpreis: true)!;
            Assert.AreEqual(Bert, zuschlag.Gewinner);
            Assert.AreEqual(0, zuschlag.Preis, "Ein Passen (0) zaehlt als Gebot fuer den Zweitpreis.");

            Assert.IsNull(AuktionRegel.Ermittle(new Dictionary<Guid, Gebot> { [Anna] = new(0, 1) }, zweitpreis: false),
                "Niemand bietet mehr als 0 - trotzdem ein Zuschlag.");
            Assert.IsNull(AuktionRegel.Ermittle(new Dictionary<Guid, Gebot>(), zweitpreis: false));
        }

        [TestMethod]
        public void TheWinnerAlwaysPaysAndARightAnswerCountsDouble()
        {
            Assert.AreEqual(2 * 100 - 60, AuktionRegel.Werte(60, Eingang(RatingKind.Right, 100)).Punkte, "Richtig zaehlt nicht doppelt.");
            Assert.AreEqual(2 * 40 - 60, AuktionRegel.Werte(60, Eingang(RatingKind.Partial, 40)).Punkte, "Ein Teilwert zaehlt nicht doppelt.");
            Assert.AreEqual(-60, AuktionRegel.Werte(60, Eingang(RatingKind.Wrong, -50)).Punkte, "Falsch kostet mehr als den Preis.");
            Assert.AreEqual(-60, AuktionRegel.Werte(60, Eingang(RatingKind.None, 0)).Punkte, "Unbewertet zahlt der Gewinner nicht.");
            StringAssert.Contains(AuktionRegel.Werte(60, Eingang(RatingKind.None, 0)).Grund, "Preis 60");
        }

        [TestMethod]
        public void ThePriceIsCappedOnlyWhenTheGameForbidsMinus()
        {
            var gekappt = AuktionRegel.Werte(60, Eingang(RatingKind.Wrong, -50, kontostand: 25, unterNull: false));
            Assert.AreEqual(-25, gekappt.Punkte, "Der Preis ist nicht am Kontostand gekappt.");
            StringAssert.Contains(gekappt.Grund, "gekappt");

            Assert.AreEqual(-60, AuktionRegel.Werte(60, Eingang(RatingKind.Wrong, -50, kontostand: 25, unterNull: true)).Punkte,
                "Gekappt, obwohl das Spiel ein Minus erlaubt.");
        }
    }
}
