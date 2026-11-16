using LocalBuzzer.Service.Base.States.Sonderregeln;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Questions.Sonderregeln;
using System.Text.RegularExpressions;

namespace Quizzer.LogicUnitTests.LocalBuzzer
{
    /// <summary>
    /// Derselbe Vertrag fuer die Vorphase der Sonderregeln (M35 C3): was das Telefon liest und schickt,
    /// die Zahlen von <see cref="CellSpecialRule"/> in <c>constants.js</c>, und ein Modul je Regel.
    /// </summary>
    public partial class ClientStateContractUnitTests
    {
        [TestMethod]
        [DataRow(typeof(StakeClientInfo), "einsatz.js", "artInfo")]
        [DataRow(typeof(RiskClientInfo), "risiko.js", "artInfo")]
        [DataRow(typeof(AuctionClientInfo), "einsatz.js", "artInfo")] // die Auktion erbt das Modul, nur die Texte sind eigen
        [DataRow(typeof(ChoiceClientInfo), "wahl.js", "artInfo")]
        public void TheSpecialRuleModulesReadExactlyTheFieldsTheServerSends(Type typ, string datei, string variable)
        {
            var zugriffe = Zugriffe(ReadScript("sonderregeln", datei), variable);

            Assert.IsTrue(zugriffe.Count > 0,
                $"Kein Zugriff auf {variable} in {datei} - das Muster greift nicht, die Zusicherung waere immer gruen.");
            CollectionAssert.AreEquivalent(Felder(typ).ToList(), zugriffe,
                $"{datei} liest nicht genau, was {typ.Name} schickt: " + string.Join(", ", zugriffe));
        }

        [TestMethod]
        [DataRow(typeof(StakePayload), "einsatz.js")]
        [DataRow(typeof(RiskPayload), "risiko.js")]
        [DataRow(typeof(AuctionPayload), "einsatz.js")]
        public void TheSpecialRulePhoneSendsTheFieldsTheServerReads(Type typ, string datei)
        {
            // Ohne Kommentare: ein Feldname im Kommentar hielte die Zusicherung sonst gruen (gemessen am Rahmen, M35 C3).
            var skript = Regex.Replace(ReadScript("sonderregeln", datei), @"//.*", string.Empty);

            var fehlend = Felder(typ).Where(f => !Regex.IsMatch(skript, $@"\b{f}\s*[:,}}]")).ToList();

            Assert.AreEqual(0, fehlend.Count,
                $"{typ.Name} erwartet, aber {datei} schickt es nie - der Server liest den Standardwert: " + string.Join(", ", fehlend));
        }

        /// <summary>Jede Regel aus den Profilen hat im Rahmen ein Modul - sonst zeigt das Telefon nur „kennt das Telefon noch nicht".</summary>
        [TestMethod]
        public void EverySpecialRuleHasAPhoneModule()
        {
            var registriert = Regex.Matches(ReadScript("minispiele", "rahmen.js"), @"\[SpecialRules\.(\w+)\s*,")
                .Select(m => m.Groups[1].Value)
                .ToList();

            // Eine Regel ohne Vorphase (Kopfgeld, M35 C3b) braucht kein Modul - und darf keins haben, das nie kommt.
            var erwartet = SpecialRuleProfiles.All.Where(p => p.HasPrePhase).Select(p => p.Rule.ToString()).ToList();

            Assert.IsTrue(erwartet.Count > 0, "Keine Sonderregel - die Zusicherung waere immer gruen.");
            CollectionAssert.AreEquivalent(erwartet, registriert,
                "rahmen.js kennt nicht genau die Sonderregeln: " + string.Join(", ", registriert));
        }
    }
}
