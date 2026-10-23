using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Questions;

namespace Quizzer.LogicUnitTests.DataModels
{
    /// <summary>M32: Dubletten erkennen, bevor dieselbe Frage zweimal im Bestand liegt.</summary>
    [TestClass]
    public class FragenAehnlichkeitUnitTests
    {
        [TestMethod]
        [DataRow("Wie hoch ist der Großglockner?", "wie hoch ist der grossglockner", true)]
        [DataRow("Wie hoch ist der Großglockner?", "Wie hoch ist der Großglockner in Metern?", true)]
        [DataRow("Welche Stadt ist die Hauptstadt von Peru?", "Hauptstadt von Peru?", true)]
        [DataRow("Wie hoch ist der Großglockner?", "Wie hoch ist der Ortler?", false)]
        [DataRow("Wann fiel die Berliner Mauer?", "Wann wurde die Berliner Mauer gebaut?", false)]
        [DataRow("Café oder Kaffee?", "Cafe oder Kaffee", true)]
        public void SimilarTextsAreFoundAndDifferentOnesAreNot(string a, string b, bool aehnlich)
            => Assert.AreEqual(aehnlich, FragenAehnlichkeit.Wert(a, b) >= FragenAehnlichkeit.Schwelle, $"{a} / {b}: {FragenAehnlichkeit.Wert(a, b):0.00}");

        [TestMethod]
        public void TheQuestionItselfAndEmptyTextsDoNotCount()
        {
            var selbst = Guid.NewGuid();
            var vorhanden = new[]
            {
                new FragenAehnlichkeit.Kandidat(selbst, "Hauptstadt von Peru?", "Peru"),
                new FragenAehnlichkeit.Kandidat(Guid.NewGuid(), "Die Hauptstadt von Peru?", "Peru 2"),
                new FragenAehnlichkeit.Kandidat(Guid.NewGuid(), "Hauptstadt von Chile?", "Chile"),
            };

            var treffer = FragenAehnlichkeit.Finde("Hauptstadt von Peru?", vorhanden, selbst);
            Assert.AreEqual(1, treffer.Count);
            Assert.AreEqual("Peru 2", treffer[0].Frage.Bezeichnung);

            Assert.AreEqual(0, FragenAehnlichkeit.Finde("", vorhanden, Guid.Empty).Count);
            Assert.AreEqual(0, FragenAehnlichkeit.Finde("Wer? Wie? Was?", vorhanden, Guid.Empty).Count, "Nur Füllwörter sind keine Frage.");
        }
    }
}
