using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Questions;

namespace Quizzer.LogicUnitTests.DataModels
{
    /// <summary>E1 (M31): die Bezeichnung entsteht aus dem Fragetext.</summary>
    [TestClass]
    public class BezeichnungUnitTests
    {
        [TestMethod]
        [DataRow("Wie hoch ist der Großglockner?", 80, "Wie hoch ist der Großglockner")]
        [DataRow("  Hauptstadt von Peru?  ", 24, "Hauptstadt von Peru")]
        [DataRow("Erste Zeile?\nZweite Zeile", 80, "Erste Zeile")]
        [DataRow("", 24, "")]
        [DataRow(null, 24, "")]
        [DataRow("Welcher Komponist schrieb die Oper Die Zauberflöte", 24, "Welcher Komponist …")]
        [DataRow("Donaudampfschifffahrtsgesellschaftskapitän", 10, "Donaudampf …")]
        public void TheDesignationIsTheFirstLineCutAtAWord(string? text, int laenge, string erwartet)
            => Assert.AreEqual(erwartet, Bezeichnung.AusFragetext(text, laenge));

        [TestMethod]
        public void ItNeverGetsLongerThanAsked()
        {
            var lang = string.Join(" ", Enumerable.Repeat("Wort", 50));
            Assert.IsTrue(Bezeichnung.AusFragetext(lang, Bezeichnung.Lang).Length <= Bezeichnung.Lang + 2);
        }
    }
}
