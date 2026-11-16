using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Questions.Minispiele;

namespace Quizzer.LogicUnitTests.DataModels.Minispiele
{
    /// <summary>
    /// Das Kopfrechnen (M35 C5) als reine Funktion: die Aufgabenfolge haengt nur am Startwert, jede Loesung
    /// stimmt und ist nicht negativ, die Stufe zaehlt - und die Wertung mit Gleichstand und Kappung (E2).
    /// </summary>
    [TestClass]
    public class KopfrechnenUnitTests
    {
        private const int Feld = 200;

        private static readonly Guid Anna = Guid.NewGuid();
        private static readonly Guid Bert = Guid.NewGuid();
        private static readonly Guid Carl = Guid.NewGuid();

        private static readonly int[] Stufen = [KopfrechnenAufgaben.Leicht, KopfrechnenAufgaben.Mittel, KopfrechnenAufgaben.Schwer];

        /// <summary>Rechnet „a op b" aus dem Text nach - unabhaengig vom Erzeuger.</summary>
        private static int Nachgerechnet(string text)
        {
            var teile = text.Split(' ');
            Assert.AreEqual(3, teile.Length, $"Unerwartete Aufgabe: {text}");

            var a = int.Parse(teile[0]);
            var b = int.Parse(teile[2]);

            return teile[1] switch
            {
                "+" => a + b,
                "−" => a - b,
                "×" => a * b,
                "÷" when b != 0 && a % b == 0 => a / b,
                _ => throw new AssertFailedException($"Unbekannte oder unglatte Rechnung: {text}"),
            };
        }

        [TestMethod]
        public void TheSameStartValueGivesTheSameSequence()
        {
            foreach (var stufe in Stufen)
            {
                var erste = Enumerable.Range(1, 50).Select(n => KopfrechnenAufgaben.Aufgabe(4711, n, stufe)).ToList();
                var zweite = Enumerable.Range(1, 50).Select(n => KopfrechnenAufgaben.Aufgabe(4711, n, stufe)).ToList();
                var andere = Enumerable.Range(1, 50).Select(n => KopfrechnenAufgaben.Aufgabe(4712, n, stufe)).ToList();

                CollectionAssert.AreEqual(erste, zweite, "Derselbe Startwert gibt eine andere Folge.");
                CollectionAssert.AreNotEqual(erste, andere, "Ein anderer Startwert gibt dieselbe Folge.");
                Assert.IsTrue(erste.Select(a => a.Text).Distinct().Count() > 25, $"Stufe {stufe}: die Folge wiederholt sich zu oft.");
            }
        }

        [TestMethod]
        public void EverySolutionIsRightWholeAndNotNegative()
        {
            foreach (var stufe in Stufen)
            {
                for (var n = 1; n <= 3000; n++)
                {
                    var aufgabe = KopfrechnenAufgaben.Aufgabe(n * 7919, n, stufe);

                    Assert.AreEqual(Nachgerechnet(aufgabe.Text), aufgabe.Loesung, $"Stufe {stufe}: {aufgabe.Text}");
                    Assert.IsTrue(aufgabe.Loesung >= 0, $"Stufe {stufe}: negatives Ergebnis bei {aufgabe.Text}");
                }
            }
        }

        [TestMethod]
        public void TheLevelDecidesRangeAndOperations()
        {
            var leicht = Enumerable.Range(1, 2000).Select(n => KopfrechnenAufgaben.Aufgabe(99, n, KopfrechnenAufgaben.Leicht)).ToList();
            var mittel = Enumerable.Range(1, 2000).Select(n => KopfrechnenAufgaben.Aufgabe(99, n, KopfrechnenAufgaben.Mittel)).ToList();
            var schwer = Enumerable.Range(1, 2000).Select(n => KopfrechnenAufgaben.Aufgabe(99, n, KopfrechnenAufgaben.Schwer)).ToList();

            Assert.IsTrue(leicht.All(a => a.Loesung <= 20 && (a.Text.Contains('+') || a.Text.Contains('−'))), "Leicht geht ueber 20 oder kennt mehr als Plus und Minus.");
            Assert.IsTrue(mittel.All(a => a.Loesung <= 100), "Mittel geht ueber 100.");
            Assert.IsTrue(mittel.Any(a => a.Text.Contains('×')), "Mittel kennt kein Einmaleins.");
            Assert.IsTrue(schwer.Any(a => a.Text.Contains('÷')) && schwer.Any(a => a.Loesung > 100), "Schwer ist nicht schwerer.");
        }

        [TestMethod]
        public void MostCorrectWinAndATieGivesAllTheFieldPoints()
        {
            var ergebnis = KopfrechnenEvaluator.Werte(new Dictionary<Guid, KopfrechnenStand>
            {
                [Anna] = new(7, 9),
                [Bert] = new(7, 12),
                [Carl] = new(5, 5),
            }, Feld, Kontostaende.OhneGrenze);

            CollectionAssert.AreEquivalent(new[] { Anna, Bert }, ergebnis.Gewinner.ToList());
            Assert.AreEqual(Feld, ergebnis.Vorschlaege[Anna].Punkte);
            Assert.AreEqual(Feld, ergebnis.Vorschlaege[Bert].Punkte);
            Assert.IsFalse(ergebnis.Vorschlaege.ContainsKey(Carl), "Wer nicht vorn ist, bekommt einen Vorschlag.");
            Assert.AreEqual("Kopfrechnen: 7 von 9 richtig – die meisten +200", ergebnis.Vorschlaege[Anna].Grund);
            Assert.AreEqual("5 von 5 richtig – nicht die meisten", KopfrechnenEvaluator.Describe(ergebnis, Carl));
        }

        [TestMethod]
        public void NobodyWinsWithoutACorrectAnswer()
        {
            var ergebnis = KopfrechnenEvaluator.Werte(new Dictionary<Guid, KopfrechnenStand>
            {
                [Anna] = new(0, 4),
            }, Feld, Kontostaende.OhneGrenze);

            Assert.AreEqual(0, ergebnis.Gewinner.Count, "Null richtige gewinnen.");
            Assert.AreEqual(0, ergebnis.Vorschlaege.Count);
            Assert.AreEqual("Keine Antwort – von Hand werten", KopfrechnenEvaluator.Describe(ergebnis, Bert));
        }

        /// <summary>E2: ein Plus wird nie gekappt - auch nicht bei einem Kontostand unter 0 in einem Spiel ohne Minus.</summary>
        [TestMethod]
        public void APlusIsNeverCapped()
        {
            var konten = new Kontostaende(new Dictionary<Guid, int> { [Anna] = -150 }, UnterNullErlaubt: false);

            var ergebnis = KopfrechnenEvaluator.Werte(new Dictionary<Guid, KopfrechnenStand> { [Anna] = new(3, 3) }, Feld, konten);

            Assert.AreEqual(Feld, ergebnis.Vorschlaege[Anna].Punkte, "Der Gewinn wurde am Kontostand gekappt.");
        }

        [TestMethod]
        public void TheBoardMarksTheWinnersAndNamesThoseWithoutAnswer()
        {
            var ergebnis = KopfrechnenEvaluator.Werte(new Dictionary<Guid, KopfrechnenStand> { [Bert] = new(4, 6) }, Feld, Kontostaende.OhneGrenze);

            var zeilen = KopfrechnenTafel.Ende(ergebnis, [(Anna, "Anna"), (Bert, "Bert")]);

            Assert.AreEqual("Bert", zeilen[0].Name);
            Assert.IsTrue(zeilen[0].Sieger);
            Assert.AreEqual("4 von 6 richtig", zeilen[0].Zeit);
            Assert.AreEqual("keine Antwort", zeilen[1].Zeit);
            Assert.AreEqual(0, zeilen[1].BalkenAnteil);
        }
    }
}
