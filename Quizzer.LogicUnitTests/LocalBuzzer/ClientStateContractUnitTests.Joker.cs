using LocalBuzzer.Service.Base.States.Joker;
using LocalBuzzer.Service.Base.States.Minispiele;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.RegularExpressions;

namespace Quizzer.LogicUnitTests.LocalBuzzer
{
    /// <summary>
    /// Derselbe Vertrag fuer die Joker-Runde (M35 C7b): was <c>joker.js</c> liest und schickt, und dass der Rahmen
    /// das Modul ueber <c>prePhase</c> waehlt. Die Zahlen von <see cref="MinigamePrePhase"/> haelt
    /// <see cref="TheConstantsMirrorTheServerEnums"/>.
    /// </summary>
    public partial class ClientStateContractUnitTests
    {
        [TestMethod]
        [DataRow(typeof(JokerClientInfo), "artInfo")]
        [DataRow(typeof(JokerOptionInfo), "option")]
        [DataRow(typeof(JokerResultInfo), "eintrag")]
        [DataRow(typeof(MinigamePlayerOption), "ziel")]
        public void TheJokerModuleReadsExactlyTheFieldsTheServerSends(Type typ, string variable)
        {
            var zugriffe = Zugriffe(ReadScript("joker", "joker.js"), variable);

            Assert.IsTrue(zugriffe.Count > 0,
                $"Kein Zugriff auf {variable} in joker.js - das Muster greift nicht, die Zusicherung waere immer gruen.");
            CollectionAssert.AreEquivalent(Felder(typ).ToList(), zugriffe,
                $"joker.js liest nicht genau, was {typ.Name} schickt: " + string.Join(", ", zugriffe));
        }

        [TestMethod]
        public void TheJokerPhoneSendsTheFieldsTheServerReads()
        {
            var skript = Regex.Replace(ReadScript("joker", "joker.js"), @"//.*", string.Empty);

            var fehlend = Felder(typeof(JokerPayload)).Where(f => !Regex.IsMatch(skript, $@"\b{f}\s*[:,}}]")).ToList();

            Assert.AreEqual(0, fehlend.Count, "JokerPayload erwartet, aber joker.js schickt es nie: " + string.Join(", ", fehlend));
        }

        /// <summary>Der Rahmen kennt die Joker-Runde - sonst zeigte das Telefon die Vorphase als unbekanntes Minispiel.</summary>
        [TestMethod]
        public void TheFrameOpensTheJokerModuleForThePrePhase()
        {
            var rahmen = Regex.Replace(ReadScript("minispiele", "rahmen.js"), @"//.*", string.Empty);

            StringAssert.Contains(rahmen, "import { JokerModul } from \"../joker/joker.js\"");
            Assert.IsTrue(Regex.IsMatch(rahmen, @"prePhase\s*===\s*PrePhases\.Joker"), "rahmen.js fragt die Joker-Runde nicht ab.");
            Assert.IsTrue(Regex.IsMatch(rahmen, @"new JokerModul\(\)"), "rahmen.js baut das Joker-Modul nie.");
        }
    }
}
