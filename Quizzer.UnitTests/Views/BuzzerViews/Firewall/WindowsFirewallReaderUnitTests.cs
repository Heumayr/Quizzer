using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Views.BuzzerViews.Firewall;

namespace Quizzer.UnitTests.Views.BuzzerViews.Firewall
{
    /// <summary>
    /// Der COM-Leser an der echten Firewall dieses Rechners. Nur lesend, ohne Verwalterrechte.
    /// <para>
    /// Diese Proben sichern keine Zahlen zu - die haengen vom Rechner ab. Sie halten fest, dass
    /// die Abfrage laeuft und ihre Werte in die Form passen, die
    /// <see cref="FirewallAssessment"/> erwartet.
    /// </para>
    /// </summary>
    [TestClass]
    public class WindowsFirewallReaderUnitTests
    {
        [TestMethod]
        public void TheFirewallCanBeAsked()
        {
            var reader = new WindowsFirewallReader();

            var rules = reader.ReadRules();

            if (!reader.IsAvailable)
                Assert.Inconclusive("Die Firewall liess sich auf diesem Rechner nicht befragen.");

            Assert.IsTrue(rules.Count > 0,
                "Kein einziger Regel-Eintrag - dann stimmt an der Abfrage etwas nicht.");
        }

        [TestMethod]
        public void TheActiveProfileIsOneOfTheThree()
        {
            var reader = new WindowsFirewallReader();

            var aktiv = reader.ActiveProfiles;

            if (!reader.IsAvailable)
                Assert.Inconclusive("Die Firewall liess sich auf diesem Rechner nicht befragen.");

            Assert.IsTrue(aktiv is >= 0 and <= 7,
                $"Die Profilmaske {aktiv} liegt ausserhalb von Domaene/Privat/Oeffentlich.");
        }

        /// <summary>
        /// Die Werte muessen so ankommen, dass die Bewertung sie einordnen kann: mindestens eine
        /// eingehende Erlaubnisregel mit einem Protokoll, das die Bewertung kennt.
        /// </summary>
        [TestMethod]
        public void TheRulesCarryTheFieldsTheAssessmentNeeds()
        {
            var reader = new WindowsFirewallReader();

            var rules = reader.ReadRules();

            if (!reader.IsAvailable || rules.Count == 0)
                Assert.Inconclusive("Die Firewall liess sich auf diesem Rechner nicht befragen.");

            Assert.IsTrue(rules.Any(r => r.Inbound && r.Allow),
                "Keine einzige eingehende Erlaubnisregel gelesen.");

            Assert.IsTrue(rules.Any(r => r.Protocol is 6 or 17 or 256),
                "Keine Regel mit einem bekannten Protokoll - die Uebersetzung stimmt nicht.");

            // Gemessen am 2026-09-05 an dieser Firewall: neben 1..7 kommt 0x7FFFFFFF vor, die
            // Windows-Konstante fuer "alle Profile" (291 der Regeln trugen sie). Die Bewertung
            // arbeitet mit Bitmasken und kommt damit zurecht; eine Zusicherung auf 0..7 waere
            // schlicht falsch gewesen.
            Assert.IsTrue(rules.All(r => (r.Profiles & 0x7) == r.Profiles || r.Profiles == 0x7FFFFFFF),
                "Eine Profilmaske traegt Bits, die weder zu den drei Profilen noch zu "
                + "\"alle Profile\" gehoeren.");
        }

        /// <summary>
        /// Der ganze Weg an der echten Firewall: die Bewertung muss ohne Ausnahme durchlaufen
        /// und ein Urteil liefern.
        /// </summary>
        [TestMethod]
        public void TheAssessmentRunsAgainstTheRealFirewall()
        {
            var reader = new WindowsFirewallReader();

            var rules = reader.ReadRules();

            if (!reader.IsAvailable)
                Assert.Inconclusive("Die Firewall liess sich auf diesem Rechner nicht befragen.");

            var verdict = FirewallAssessment.Assess(
                rules, reader.ActiveProfiles, Environment.ProcessPath, 5000);

            Assert.AreNotEqual(FirewallState.Unbekannt, verdict.State);

            if (verdict.NeedsAttention)
                Assert.IsFalse(string.IsNullOrWhiteSpace(verdict.Hint),
                    "Ein Hinweis ohne Text hilft dem Spielleiter nicht.");
        }
    }
}
