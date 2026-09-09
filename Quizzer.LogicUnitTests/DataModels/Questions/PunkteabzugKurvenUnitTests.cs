using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Questions;

namespace Quizzer.LogicUnitTests.DataModels.Questions
{
    /// <summary>
    /// <b>F16.</b> Die beiden Punktekurven, mit den Zahlen des Nutzers.
    /// <para>
    /// Wortlaut vom 2026-09-09: „bei der vollen aufoesung sollen sich dann die verbleibenden
    /// punkte zb halbieren ... so waeren 100 erste 67 ... 34 ... 17 ... 0" und „oder jeder step
    /// halbiert und rundet auf .. als 2 option ... 100 ... 50 ... 25 ... 13 ... 0".
    /// </para>
    /// <para>
    /// <b>Die Zahlen stehen hier absichtlich als Literale.</b> Sie aus der Formel zu rechnen
    /// hiesse, die Formel gegen sich selbst zu pruefen - dann bliebe die Zusicherung gruen, egal
    /// was sie tut.
    /// </para>
    /// </summary>
    [TestClass]
    public class PunkteabzugKurvenUnitTests
    {
        private static int[] Verlauf(int punkte, int schritte, ScoreReductionMode modus)
            => Enumerable.Range(1, schritte)
                .Select(i => Punkteabzug.Verbleibend(punkte, schritte, i, modus))
                .ToArray();

        /// <summary>Gleichmaessig, und der letzte Hinweis halbiert - genau seine Reihe.</summary>
        [TestMethod]
        public void TheLinearCurveMatchesTheNumbersTheUserWrote()
        {
            var verlauf = Verlauf(100, 3, ScoreReductionMode.Linear);

            CollectionAssert.AreEqual(new[] { 67, 34, 17 }, verlauf,
                "Erwartet war 67, 34, 17 - gerechnet wurde " + string.Join(", ", verlauf));
        }

        /// <summary>Jeder Schritt halbiert und rundet auf - ebenfalls seine Reihe.</summary>
        [TestMethod]
        public void TheHalvingCurveMatchesTheNumbersTheUserWrote()
        {
            var verlauf = Verlauf(100, 4, ScoreReductionMode.Halving);

            CollectionAssert.AreEqual(new[] { 50, 25, 13, 7 }, verlauf,
                "Erwartet war 50, 25, 13 (und weiter 7) - gerechnet wurde "
                + string.Join(", ", verlauf));
        }

        /// <summary>
        /// <b>Der Kern der Entscheidung:</b> nach dem letzten Hinweis ist noch etwas zu holen.
        /// <para>
        /// „auch wenn alles erkennbar ist kann noch geraten werden" - die Null gehoert dem
        /// Aufloesungsschritt, nicht dem letzten Hinweis. Vor dem 2026-09-09 blieb hier ein
        /// zufaelliger Rest aus der ganzzahligen Division stehen: bei 100 Punkten genau 1.
        /// </para>
        /// </summary>
        [TestMethod]
        public void AfterTheLastHintThereIsStillSomethingToWin()
        {
            foreach (var modus in Enum.GetValues<ScoreReductionMode>())
            {
                foreach (var punkte in new[] { 100, 120, 200, 999 })
                {
                    foreach (var schritte in new[] { 1, 3, 4, 10 })
                    {
                        var letzter = Punkteabzug.Verbleibend(punkte, schritte, schritte, modus);

                        Assert.IsTrue(letzter > 0,
                            $"{modus} mit {punkte} Punkten und {schritte} Hinweisen gibt nach "
                            + $"dem letzten Hinweis {letzter} - dann ist die Frage vorbei, "
                            + "bevor die Aufloesung kommt.");

                        Assert.IsTrue(letzter < punkte,
                            $"{modus} mit {punkte} Punkten und {schritte} Hinweisen nimmt "
                            + "ueberhaupt nichts weg.");
                    }
                }
            }
        }

        /// <summary>Der Verlauf faellt, Schritt fuer Schritt, in beiden Kurven.</summary>
        [TestMethod]
        public void EachHintCostsSomething()
        {
            foreach (var modus in Enum.GetValues<ScoreReductionMode>())
            {
                var verlauf = Verlauf(1000, 6, modus);

                for (var i = 1; i < verlauf.Length; i++)
                {
                    Assert.IsTrue(verlauf[i] < verlauf[i - 1],
                        $"{modus}: Schritt {i + 1} kostet nichts - " + string.Join(", ", verlauf));
                }
            }
        }

        /// <summary>
        /// Der Faktor wirkt - und ein unbrauchbarer faellt auf die Vorgabe zurueck, statt die
        /// Frage flach zu machen.
        /// </summary>
        [TestMethod]
        public void TheFactorIsAdjustablePerQuestion()
        {
            Assert.AreEqual(17, Punkteabzug.Verbleibend(100, 3, 3, ScoreReductionMode.Linear, 0.5),
                "Die Haelfte von 34 ist 17.");

            Assert.AreEqual(28, Punkteabzug.Verbleibend(100, 3, 3, ScoreReductionMode.Linear, 0.8),
                "Ein grosszuegiger Faktor laesst mehr stehen: 80 Prozent von 34.");

            Assert.AreEqual(
                Punkteabzug.Verbleibend(100, 3, 3, ScoreReductionMode.Linear),
                Punkteabzug.Verbleibend(100, 3, 3, ScoreReductionMode.Linear, 0),
                "Faktor 0 ist keine Kurve - dann gilt die Vorgabe.");

            Assert.AreEqual(
                Punkteabzug.Verbleibend(100, 3, 3, ScoreReductionMode.Linear),
                Punkteabzug.Verbleibend(100, 3, 3, ScoreReductionMode.Linear, 1),
                "Faktor 1 naehme nichts weg - dann gilt die Vorgabe.");
        }

        /// <summary>Ohne Schritte oder ohne Punkte gibt es nichts zu rechnen.</summary>
        [TestMethod]
        public void NothingToReduceStaysZero()
        {
            Assert.AreEqual(0, Punkteabzug.Verbleibend(100, 0, 0));
            Assert.AreEqual(0, Punkteabzug.Verbleibend(0, 3, 1));
        }
    }
}
