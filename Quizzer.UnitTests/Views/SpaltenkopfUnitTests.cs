using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Models.Base;
using Quizzer.Views.HelperViewModels;

namespace Quizzer.UnitTests.Views
{
    /// <summary>
    /// Die Kopfzeile einer Spalte oder Zeile — und ihre Kurzform in eckigen Klammern.
    /// <para>
    /// „Musik der 80er [Musik]" ergibt am Beamer die lange Form und auf der schmalen Kachel die
    /// kurze. Das ist die einzige Rechnung in diesem ViewModel, sie stand <b>ohne jede
    /// Zusicherung</b> da, und sie hat mehrere Sonderfälle: keine Klammer, leere Klammer,
    /// verdrehte Klammern, mehrere Paare.
    /// </para>
    /// <para>
    /// <b>Was hier auffällt, sieht man am Quizabend</b> — die Kopfzeilen stehen die ganze Zeit
    /// auf der Leinwand.
    /// </para>
    /// </summary>
    [TestClass]
    public class SpaltenkopfUnitTests
    {
        private static HeaderEntryViewModel Mit(string text)
            => new(new Header { Index = 1, Designation = text }, isColumnHeader: true);

        /// <summary>Der Regelfall: lange Form vorn, Kurzform in der Klammer.</summary>
        [TestMethod]
        public void TheBracketHoldsTheShortForm()
        {
            var vm = Mit("Musik der 80er [Musik]");

            Assert.AreEqual("Musik der 80er", vm.TextLong, "Die lange Form stimmt nicht.");
            Assert.AreEqual("Musik", vm.TextShort, "Die Kurzform stimmt nicht.");
        }

        /// <summary>
        /// <b>Ohne Klammer sind beide gleich.</b> Sonst stünde auf der Kachel nichts, und die
        /// Spalte wäre am Abend namenlos.
        /// </summary>
        [TestMethod]
        public void WithoutABracketBothAreTheSame()
        {
            var vm = Mit("  Geschichte  ");

            Assert.AreEqual("Geschichte", vm.TextLong);
            Assert.AreEqual("Geschichte", vm.TextShort);
        }

        /// <summary>
        /// Verdrehte oder halbe Klammern gelten als keine — <c>]Musik[</c> ist ein Titel, kein
        /// Kürzel.
        /// </summary>
        [TestMethod]
        public void BrokenBracketsCountAsNone()
        {
            foreach (var text in new[] { "]Musik[", "Musik [", "Musik ]" })
            {
                var vm = Mit(text);

                Assert.AreEqual(text.Trim(), vm.TextLong, $"Bei '{text}' stimmt die lange Form nicht.");
                Assert.AreEqual(text.Trim(), vm.TextShort, $"Bei '{text}' stimmt die Kurzform nicht.");
            }
        }

        /// <summary>Bei mehreren Paaren zählt das letzte — der Rest bleibt Teil des Titels.</summary>
        [TestMethod]
        public void TheLastBracketWins()
        {
            var vm = Mit("Film [1980] [Film]");

            Assert.AreEqual("Film", vm.TextShort, "Nicht die letzte Klammer wurde genommen.");
            Assert.AreEqual("Film [1980]", vm.TextLong, "Die erste Klammer gehoert zum Titel.");
        }

        /// <summary>
        /// <b>Das Ändern des Textes zieht beide Formen nach.</b> Ohne die Meldungen bliebe auf
        /// der Kachel der alte Name stehen, während oben schon der neue steht.
        /// </summary>
        [TestMethod]
        public void ChangingTheTextUpdatesBothForms()
        {
            var vm = Mit("Alt [A]");

            var gemeldet = new List<string>();

            vm.PropertyChanged += (_, e) => gemeldet.Add(e.PropertyName ?? string.Empty);

            vm.Text = "Neu [N]";

            Assert.AreEqual("Neu", vm.TextLong);
            Assert.AreEqual("N", vm.TextShort);

            CollectionAssert.Contains(gemeldet, nameof(HeaderEntryViewModel.TextLong),
                "Die lange Form wurde nicht gemeldet - die Anzeige bliebe auf dem alten Stand.");

            CollectionAssert.Contains(gemeldet, nameof(HeaderEntryViewModel.TextShort),
                "Die Kurzform wurde nicht gemeldet - die Kachel bliebe auf dem alten Stand.");
        }

        /// <summary>
        /// Ein Spaltenkopf sitzt in Zeile 0, ein Zeilenkopf in Spalte 0 — sonst liegt das ganze
        /// Raster verschoben.
        /// </summary>
        [TestMethod]
        public void HeadersSitOnTheirOwnEdge()
        {
            var spalte = new HeaderEntryViewModel(
                new Header { Index = 3, Designation = "Spalte" }, isColumnHeader: true);

            Assert.AreEqual(0, spalte.GridRow, "Ein Spaltenkopf gehoert in Zeile 0.");
            Assert.AreEqual(3, spalte.GridColumn, "Ein Spaltenkopf steht in seiner eigenen Spalte.");

            var zeile = new HeaderEntryViewModel(
                new Header { Index = 3, Designation = "Zeile" }, isColumnHeader: false);

            Assert.AreEqual(3, zeile.GridRow, "Ein Zeilenkopf steht in seiner eigenen Zeile.");
            Assert.AreEqual(0, zeile.GridColumn, "Ein Zeilenkopf gehoert in Spalte 0.");
        }
    }
}
