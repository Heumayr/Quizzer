using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.DataModels.Questions;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Quizzer.UnitTests.Views
{
    /// <summary>
    /// Die Verpixelung der Aufdeckfrage.
    /// <para>
    /// <b>Nutzerwunsch vom 2026-09-06 abends:</b> „zuerst wählt man unschärfe verpixelt bzw.
    /// aufdecken" - und vom Vormittag zur Betriebsart selbst: „jeder step macht es schärfer".
    /// </para>
    /// <para>
    /// <b>Gemessen wird die Blockzahl, nicht das Aussehen.</b> WPF hat keinen Raster-Effekt; die
    /// Verpixelung entsteht dadurch, dass das Bild klein gerechnet und ungeglättet wieder groß
    /// gezogen wird. Wie klein - das ist der ganze Effekt, und nur das ist prüfbar.
    /// </para>
    /// </summary>
    [TestClass]
    public class BildrasterUnitTests
    {
        /// <summary>Ein Bild mit bekannten Maßen - der Inhalt spielt keine Rolle.</summary>
        private static BitmapSource Bild(int breite, int hoehe)
        {
            var zeilenbreite = breite * 4;
            var daten = new byte[zeilenbreite * hoehe];

            for (var i = 0; i < daten.Length; i++)
                daten[i] = (byte)(i % 251);

            var quelle = BitmapSource.Create(
                breite, hoehe, 96, 96, PixelFormats.Bgra32, null, daten, zeilenbreite);

            quelle.Freeze();

            return quelle;
        }

        /// <summary>Wie viele Blöcke nebeneinander stehen - das Original zählt als volle Auflösung.</summary>
        private static int Bloecke(BitmapSource quelle, double kante, double anzeigebreite)
        {
            var gerastert = Bildraster.Rastere(quelle, kante, anzeigebreite);

            return gerastert == null || ReferenceEquals(gerastert, quelle)
                ? quelle.PixelWidth
                : gerastert.PixelWidth;
        }

        /// <summary>
        /// <b>Die tragende Zusicherung:</b> je Schritt wird das Bild feiner, und der letzte
        /// Schritt zeigt es in voller Auflösung.
        /// <para>
        /// Sie fällt, wenn die Rampe in <see cref="RevealAreas.Rasterung"/> die Richtung wechselt
        /// oder das Ende nicht erreicht - genau die beiden Fehler, die man am Bild erst am
        /// Quizabend sieht.
        /// </para>
        /// </summary>
        [TestMethod]
        public void EachStepMakesTheImageFiner()
        {
            var quelle = Bild(400, 300);

            const int schritte = 4;

            var verlauf = Enumerable.Range(0, schritte + 1)
                .Select(i => Bloecke(quelle, RevealAreas.Rasterung(40, schritte, i), 800))
                .ToArray();

            CollectionAssert.AreEqual(new[] { 20, 27, 40, 80, 400 }, verlauf,
                "Der Verlauf der Blockzahl stimmt nicht: " + string.Join(", ", verlauf));

            for (var i = 1; i < verlauf.Length; i++)
            {
                Assert.IsTrue(verlauf[i] > verlauf[i - 1],
                    $"Schritt {i} ist nicht feiner als Schritt {i - 1} "
                    + $"({verlauf[i - 1]} -> {verlauf[i]} Bloecke).");
            }

            Assert.AreEqual(quelle.PixelWidth, verlauf[^1],
                "Der letzte Schritt zeigt das Bild nicht in voller Aufloesung - dann bleibt die "
                + "Frage bis zuletzt unlesbar.");
        }

        /// <summary>
        /// Die Blockkante meint Punkte der <b>Anzeige</b>, nicht Bildpunkte der Datei.
        /// <para>
        /// Deshalb rastert dasselbe Bild auf dem Beamer gröber als in der kleinen Vorschau - und
        /// beide Male sieht der Betrachter gleich große Klötzchen.
        /// </para>
        /// </summary>
        [TestMethod]
        public void TheBlockEdgeIsMeasuredOnScreen()
        {
            var quelle = Bild(400, 300);

            Assert.AreEqual(20, Bloecke(quelle, 40, 800),
                "800 Punkte breit, 40 Punkte je Block - das sind 20 Bloecke.");

            Assert.AreEqual(5, Bloecke(quelle, 40, 200),
                "Dieselbe Kante in einem viertel so breiten Fenster ergibt ein Viertel der "
                + "Bloecke.");

            var schmal = Bildraster.Rastere(quelle, 40, 800);

            Assert.IsNotNull(schmal, "Bei Kante 40 auf 800 Punkten muss gerastert werden.");

            Assert.AreEqual(15, schmal.PixelHeight,
                "Das Seitenverhaeltnis bleibt nicht erhalten - das Bild waere verzerrt.");
        }

        /// <summary>
        /// Die drei Fälle, in denen nichts gerastert wird. <b>Am Original erkennbar:</b> kommt
        /// dieselbe Instanz zurück, weiß der Aufrufer, dass er nicht auf ungeglättetes Skalieren
        /// umstellen muss.
        /// </summary>
        [TestMethod]
        public void NothingHappensWhenThereIsNothingToDo()
        {
            var quelle = Bild(400, 300);

            Assert.AreSame(quelle, Bildraster.Rastere(quelle, 0, 800),
                "Kante 0 heisst scharf - das Bild darf nicht angefasst werden.");

            Assert.AreSame(quelle, Bildraster.Rastere(quelle, 40, 0),
                "Ohne ausgelegte Breite gibt es keinen Bezug; erst das Layout weiss ihn.");

            Assert.IsNull(Bildraster.Rastere(null, 40, 800),
                "Ohne Bild darf nichts entstehen.");

            Assert.AreSame(quelle, Bildraster.Rastere(quelle, 1, 800),
                "Eine Kante feiner als das Bild selbst wuerde es vergroessern statt rastern.");
        }

        /// <summary>
        /// Eine übergroße Kante bleibt bei zwei Blöcken stehen. Bei einem einzigen wäre das Bild
        /// eine einfarbige Fläche - die trägt keinen Hinweis mehr, und der erste Schritt wäre
        /// wertlos.
        /// </summary>
        [TestMethod]
        public void ASingleBlockIsNeverReached()
        {
            var quelle = Bild(400, 300);

            Assert.AreEqual(2, Bloecke(quelle, 100_000, 800),
                "Eine uebergrosse Kante rastert das Bild auf einen einzigen Block.");
        }
    }
}
