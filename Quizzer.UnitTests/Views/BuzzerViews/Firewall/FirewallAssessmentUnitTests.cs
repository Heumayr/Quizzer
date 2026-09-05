using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Views.BuzzerViews.Firewall;

namespace Quizzer.UnitTests.Views.BuzzerViews.Firewall
{
    /// <summary>
    /// Die Bewertung der Firewall-Lage, ohne Firewall geprueft.
    /// <para>
    /// Der erste Fall ist der an diesem Rechner gemessene: zwei Regeln fuer quizzer.exe, beide
    /// nur fuer das Profil Oeffentlich (4), waehrend das aktive Netz Privat (2) ist.
    /// </para>
    /// </summary>
    [TestClass]
    public class FirewallAssessmentUnitTests
    {
        private const int Port = 5000;
        private const string ExePath = @"E:\quizapp\quizzer\quizzer\bin\debug\net10.0-windows\quizzer.exe";

        private const int Domaene = 1;
        private const int Privat = 2;
        private const int Oeffentlich = 4;

        private static FirewallRule ProgramRule(int profiles, bool enabled = true, int protocol = 6) =>
            new("Quizzer", ExePath, enabled, Inbound: true, Allow: true, profiles, protocol, "*");

        private static FirewallRule PortRule(string ports, int profiles) =>
            new("Quizzer Buzzer", null, Enabled: true, Inbound: true, Allow: true, profiles, 6, ports);

        [TestMethod]
        public void ARuleForPublicOnlyLeavesAPrivateNetworkOut()
        {
            var verdict = FirewallAssessment.Assess(
                new[] { ProgramRule(Oeffentlich) }, Privat, ExePath, Port);

            Assert.AreEqual(FirewallState.FalschesProfil, verdict.State);
            CollectionAssert.AreEqual(new[] { "Privat" }, verdict.MissingProfiles.ToArray());
            StringAssert.Contains(verdict.Hint, "Privat");
            Assert.IsTrue(verdict.NeedsAttention);
        }

        [TestMethod]
        public void ARuleForBothProfilesIsEnough()
        {
            var verdict = FirewallAssessment.Assess(
                new[] { ProgramRule(Privat | Oeffentlich) }, Privat | Oeffentlich, ExePath, Port);

            Assert.AreEqual(FirewallState.Freigegeben, verdict.State);
            Assert.IsFalse(verdict.NeedsAttention);
            Assert.AreEqual(string.Empty, verdict.Hint);
        }

        [TestMethod]
        public void WithoutAnyRuleTheVerdictSaysSo()
        {
            var verdict = FirewallAssessment.Assess(Array.Empty<FirewallRule>(), Privat, ExePath, Port);

            Assert.AreEqual(FirewallState.KeineRegel, verdict.State);
            Assert.IsTrue(verdict.NeedsAttention);
            StringAssert.Contains(verdict.Hint, "keine Firewall-Freigabe");
        }

        [TestMethod]
        public void APortRuleWithoutAProgramCounts()
        {
            var verdict = FirewallAssessment.Assess(
                new[] { PortRule("5000", Domaene | Privat | Oeffentlich) }, Privat, ExePath, Port);

            Assert.AreEqual(FirewallState.Freigegeben, verdict.State);
        }

        [TestMethod]
        public void ADisabledRuleDoesNotCount()
        {
            var verdict = FirewallAssessment.Assess(
                new[] { ProgramRule(Privat, enabled: false) }, Privat, ExePath, Port);

            Assert.AreEqual(FirewallState.KeineRegel, verdict.State);
        }

        [TestMethod]
        public void ABlockingRuleDoesNotCountAsAllowance()
        {
            var sperre = new FirewallRule("Sperre", ExePath, Enabled: true, Inbound: true,
                Allow: false, Privat, 6, "*");

            var verdict = FirewallAssessment.Assess(new[] { sperre }, Privat, ExePath, Port);

            Assert.AreEqual(FirewallState.KeineRegel, verdict.State);
        }

        [TestMethod]
        public void AnOutboundRuleDoesNotCount()
        {
            var ausgehend = new FirewallRule("Raus", ExePath, Enabled: true, Inbound: false,
                Allow: true, Privat, 6, "*");

            var verdict = FirewallAssessment.Assess(new[] { ausgehend }, Privat, ExePath, Port);

            Assert.AreEqual(FirewallState.KeineRegel, verdict.State);
        }

        [TestMethod]
        public void AUdpRuleDoesNotCoverTheWebServer()
        {
            var verdict = FirewallAssessment.Assess(
                new[] { ProgramRule(Privat, protocol: 17) }, Privat, ExePath, Port);

            Assert.AreEqual(FirewallState.KeineRegel, verdict.State,
                "Der Buzzer-Server spricht TCP; eine UDP-Regel hilft ihm nicht.");
        }

        [TestMethod]
        public void ARuleForAnotherProgramDoesNotCount()
        {
            var fremd = new FirewallRule("Etwas anderes", @"C:\anders\app.exe", Enabled: true,
                Inbound: true, Allow: true, Privat, 6, "*");

            var verdict = FirewallAssessment.Assess(new[] { fremd }, Privat, ExePath, Port);

            Assert.AreEqual(FirewallState.KeineRegel, verdict.State);
        }

        [TestMethod]
        [DataRow("*", true)]
        [DataRow("5000", true)]
        [DataRow("80,5000,8080", true)]
        [DataRow("4000-6000", true)]
        [DataRow("5001", false)]
        [DataRow("80,443", false)]
        [DataRow("6000-7000", false)]
        [DataRow("", false)]
        public void PortRangesAreReadCorrectly(string ports, bool erwartet)
        {
            Assert.AreEqual(erwartet, FirewallAssessment.CoversPort(ports, Port));
        }

        /// <summary>
        /// Zwei Regeln, die sich ergaenzen: eine fuer Privat, eine fuer Oeffentlich - zusammen
        /// decken sie beide aktiven Netze ab.
        /// </summary>
        [TestMethod]
        public void TwoRulesTogetherCanCoverEverything()
        {
            var verdict = FirewallAssessment.Assess(
                new[] { ProgramRule(Privat), ProgramRule(Oeffentlich) },
                Privat | Oeffentlich, ExePath, Port);

            Assert.AreEqual(FirewallState.Freigegeben, verdict.State);
        }

        /// <summary>
        /// Windows traegt fuer "alle Profile" nicht 7 ein, sondern 0x7FFFFFFF. An dieser Firewall
        /// gemessen: 291 der Regeln tragen diesen Wert. Er muss jedes aktive Profil abdecken.
        /// </summary>
        [TestMethod]
        public void TheWindowsAllProfilesValueCoversEverything()
        {
            const int AlleProfile = 0x7FFFFFFF;

            var verdict = FirewallAssessment.Assess(
                new[] { ProgramRule(AlleProfile) }, Domaene | Privat | Oeffentlich, ExePath, Port);

            Assert.AreEqual(FirewallState.Freigegeben, verdict.State,
                "Eine Regel fuer alle Profile muss auch fuer jedes aktive gelten.");
        }

        /// <summary>Ohne aktives Netz gibt es nichts zu warnen.</summary>
        [TestMethod]
        public void WithoutAnActiveNetworkNothingIsReported()
        {
            var verdict = FirewallAssessment.Assess(Array.Empty<FirewallRule>(), 0, ExePath, Port);

            Assert.AreEqual(FirewallState.Freigegeben, verdict.State);
        }

        /// <summary>
        /// Die Gegenrichtung zur Programmregel: ohne bekannten eigenen Pfad darf eine fremde
        /// Programmregel nicht plötzlich als Freigabe zaehlen.
        /// </summary>
        [TestMethod]
        public void WithoutAKnownExePathAProgramRuleDoesNotCount()
        {
            var verdict = FirewallAssessment.Assess(
                new[] { ProgramRule(Privat) }, Privat, exePath: null, Port);

            Assert.AreEqual(FirewallState.KeineRegel, verdict.State);
        }
    }
}
