using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Questions;

namespace Quizzer.LogicUnitTests.DataModels
{
    /// <summary>
    /// Die Formen einer Aufdeckfrage: Ecken, Drehen, Treffen.
    /// <para>
    /// <b>Gemessen wird gegen ein bewusst unquadratisches Bild</b> (800 × 200). Ein quadratisches
    /// Testbild deckt den Scherfehler nie auf - dann sähen Editor und Beamer beide gleich falsch
    /// aus, und niemand merkte es.
    /// </para>
    /// </summary>
    [TestClass]
    public class RevealFormenUnitTests
    {
        private static readonly Bildlage Breit = new(100, 50, 800, 200);

        /// <summary>
        /// <b>Die Zusicherung, wegen der Ecken statt eines Winkels gespeichert werden.</b>
        /// <para>
        /// Ein rechter Winkel, gedreht und zurückgedreht, muss wieder ein rechter Winkel sein -
        /// auf einem Bild im Verhältnis 4:1. Würde relativ gedreht, schert jede Drehung die Figur
        /// und der Rückweg träfe nicht mehr.
        /// </para>
        /// </summary>
        [TestMethod]
        public void RotatingThereAndBackKeepsTheShape()
        {
            var dreieck = RevealFormen.Dreieck(0.2, 0.1, 0.3, 0.4, schritt: 0);

            var vorher = RevealFormen.EckenAnzeige(dreieck, Breit);

            var hin = RevealFormen.Gedreht(dreieck, 37, Breit);
            var zurueck = RevealFormen.Gedreht(hin, -37, Breit);

            var nachher = RevealFormen.EckenAnzeige(zurueck, Breit);

            Assert.AreEqual(vorher.Length, nachher.Length);

            for (var i = 0; i < vorher.Length; i++)
            {
                Assert.AreEqual(vorher[i].X, nachher[i].X, 1e-6,
                    $"Ecke {i} ist waagrecht verrutscht - die Drehung schert die Figur.");
                Assert.AreEqual(vorher[i].Y, nachher[i].Y, 1e-6,
                    $"Ecke {i} ist senkrecht verrutscht - die Drehung schert die Figur.");
            }
        }

        /// <summary>
        /// Eine Drehung verändert die Figur, und die Hülle wächst mit. <b>Ohne diese Zusicherung
        /// prüfte die obige nur, dass zweimal nichts geschieht.</b>
        /// </summary>
        [TestMethod]
        public void RotatingActuallyChangesSomething()
        {
            var dreieck = RevealFormen.Dreieck(0.2, 0.1, 0.3, 0.4, schritt: 0);
            var gedreht = RevealFormen.Gedreht(dreieck, 37, Breit);

            var vorher = RevealFormen.EckenAnzeige(dreieck, Breit);
            var nachher = RevealFormen.EckenAnzeige(gedreht, Breit);

            var bewegt = vorher.Zip(nachher)
                .Count(p => Math.Abs(p.First.X - p.Second.X) > 1
                            || Math.Abs(p.First.Y - p.Second.Y) > 1);

            Assert.IsTrue(bewegt >= 2,
                $"Nur {bewegt} Ecken haben sich bewegt - die Drehung tut praktisch nichts, und "
                + "die Zusicherung ueber Hin und Zurueck sagt dann nichts aus.");
        }

        /// <summary>
        /// <b>Die Hülle enthält das Vieleck immer.</b> Sie ist der Notnagel für jeden Stand, der
        /// die Ecken nicht kennt - ist sie zu klein, verrät die Frage ein Stück Bild.
        /// </summary>
        [TestMethod]
        public void TheHullAlwaysContainsThePolygon()
        {
            var dreieck = RevealFormen.Dreieck(0.3, 0.1, 0.25, 0.3, schritt: 0);

            foreach (var winkel in new double[] { 0, 15, 45, 90, 137, 250 })
            {
                var gedreht = RevealAreas.Normalisiert([RevealFormen.Gedreht(dreieck, winkel, Breit)])
                    .Single();

                foreach (var (x, y) in RevealFormen.EckenAnzeige(gedreht, Breit))
                {
                    var relX = (x - Breit.Links) / Breit.Breite;
                    var relY = (y - Breit.Oben) / Breit.Hoehe;

                    Assert.IsTrue(relX >= gedreht.X - 1e-9 && relX <= gedreht.X + gedreht.W + 1e-9,
                        $"Bei {winkel} Grad ragt eine Ecke waagrecht aus der Huelle - ein aelterer "
                        + "Stand liesse dort ein Stueck Bild frei.");

                    Assert.IsTrue(relY >= gedreht.Y - 1e-9 && relY <= gedreht.Y + gedreht.H + 1e-9,
                        $"Bei {winkel} Grad ragt eine Ecke senkrecht aus der Huelle.");
                }
            }
        }

        /// <summary>
        /// Der Treffertest rechnet über die <b>echten Ecken</b>, nicht über die Hülle. Bei einem
        /// Dreieck ist fast die halbe Hülle leer - läge sie im Treffer, wäre die Nachbarfläche
        /// darunter unerreichbar.
        /// </summary>
        [TestMethod]
        public void HitTestingFollowsTheCornersNotTheHull()
        {
            var dreieck = RevealFormen.Dreieck(0.0, 0.0, 1.0, 1.0, schritt: 0);

            // Mitte unten liegt IM Dreieck (Grundlinie unten, Spitze oben Mitte).
            Assert.IsTrue(
                RevealFormen.Trifft(dreieck, Breit.Links + 400, Breit.Oben + 180, Breit),
                "Ein Punkt mitten im Dreieck gilt als nicht getroffen.");

            // Die obere linke Ecke der Huelle liegt AUSSERHALB des Dreiecks.
            Assert.IsFalse(
                RevealFormen.Trifft(dreieck, Breit.Links + 5, Breit.Oben + 5, Breit),
                "Die leere Ecke der Huelle gilt als getroffen - dann ist alles darunter "
                + "unerreichbar.");

            // Und ein Rechteck trifft dort sehr wohl.
            var rechteck = RevealFormen.Rechteck(0.0, 0.0, 1.0, 1.0, schritt: 0);

            Assert.IsTrue(
                RevealFormen.Trifft(rechteck, Breit.Links + 5, Breit.Oben + 5, Breit),
                "Ein Rechteck trifft in seiner eigenen Ecke nicht.");
        }

        /// <summary>
        /// Ohne brauchbare Ecken fällt alles auf die Hülle zurück - <b>lieber zu viel gedeckt als
        /// eine Lücke, die niemand meldet.</b>
        /// </summary>
        [TestMethod]
        public void UnusableCornersFallBackToTheBox()
        {
            var kaputt = new RevealArea(0.25, 0.25, 0.5, 0.5, Corners: [0.1, 0.2], Step: 0);

            var ecken = RevealFormen.EckenAnzeige(kaputt, Breit);

            Assert.AreEqual(4, ecken.Length,
                "Unbrauchbare Ecken ergeben kein Rechteck - dann deckt die Flaeche nichts ab.");

            Assert.IsTrue(RevealFormen.Trifft(kaputt, Breit.Links + 400, Breit.Oben + 100, Breit),
                "Die Rueckfallflaeche trifft in ihrer eigenen Mitte nicht.");
        }
    }
}
