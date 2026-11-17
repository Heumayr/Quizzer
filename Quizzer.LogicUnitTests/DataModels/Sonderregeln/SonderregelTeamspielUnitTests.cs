using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Questions.Sonderregeln;

namespace Quizzer.LogicUnitTests.DataModels.Sonderregeln
{
    /// <summary>
    /// Sonderregeln im Teamspiel (M37 T5, Entwurf Abschnitt 7): geprüft an einer Stelle, <see cref="SpecialRuleProfiles.Applies"/>,
    /// fail-closed. Auktion, Wette, Experte, Allianz und Kopfgeld rechnen Spieler gegeneinander und werden im Teamspiel weder
    /// gespielt noch angeboten noch verteilt; der Spielstart warnt.
    /// </summary>
    [TestClass]
    public class SonderregelTeamspielUnitTests
    {
        private static readonly CellSpecialRule[] NieImTeamspiel =
            [CellSpecialRule.Auction, CellSpecialRule.Bet, CellSpecialRule.Expert, CellSpecialRule.Alliance, CellSpecialRule.Bounty];

        private static GameGridCoordinate Zelle(CellSpecialRule regel = CellSpecialRule.None)
        {
            var frage = new DefaultQuestion { Id = Guid.NewGuid() };
            return new() { Id = Guid.NewGuid(), QuestionBase = frage, QuestionBaseId = frage.Id, SpecialRule = regel };
        }

        [TestMethod]
        public void TheRulesThatPitPlayersAgainstEachOtherAreNeverPlayedInATeamGame()
        {
            foreach (var regel in NieImTeamspiel)
            {
                var zelle = Zelle(regel);

                Assert.IsFalse(SpecialRuleProfiles.Applies(zelle, zelle.QuestionBase, teamspiel: true), $"{regel} wird im Teamspiel gespielt.");
                Assert.IsTrue(SpecialRuleProfiles.Applies(zelle, zelle.QuestionBase, teamspiel: false), $"{regel} wird ohne Teams nicht mehr gespielt.");
            }
        }

        /// <summary>Einsatz und Risiko wirken je Person - sie bleiben im Teamspiel.</summary>
        [TestMethod]
        [DataRow(CellSpecialRule.Stake)]
        [DataRow(CellSpecialRule.Risk)]
        public void StakeAndRiskArePlayedWithAndWithoutTeams(CellSpecialRule regel)
        {
            var zelle = Zelle(regel);

            Assert.IsTrue(SpecialRuleProfiles.Applies(zelle, zelle.QuestionBase, teamspiel: true));
            Assert.IsTrue(SpecialRuleProfiles.Applies(zelle, zelle.QuestionBase, teamspiel: false));
        }

        /// <summary>Eine Eignung, die es nicht gibt (eine neuere Fassung), spielt weder mit noch ohne Teams.</summary>
        [TestMethod]
        public void AnUnknownSuitabilityIsNeverPlayed()
        {
            var profil = new SpecialRuleProfile(CellSpecialRule.Stake, "x", "x") { Teamspiel = (TeamspielEignung)9 };

            Assert.IsFalse(profil.PasstZu(true));
            Assert.IsFalse(profil.PasstZu(false));
        }

        [TestMethod]
        public void TheTeamGameIsOfferedOnlyWhatFits()
        {
            var angeboten = SpecialRuleProfiles.Angeboten(teamspiel: true).Select(p => p.Rule).ToList();

            Assert.IsTrue(angeboten.Count > 0, "Im Teamspiel wird gar nichts angeboten - die Zusicherung darunter wäre leer.");
            CollectionAssert.Contains(angeboten, CellSpecialRule.Stake);
            Assert.IsFalse(angeboten.Intersect(NieImTeamspiel).Any(), "Angeboten im Teamspiel: " + string.Join(", ", angeboten.Intersect(NieImTeamspiel)));
            CollectionAssert.IsSubsetOf(NieImTeamspiel, SpecialRuleProfiles.Angeboten(teamspiel: false).Select(p => p.Rule).ToList());
        }

        /// <summary>Verteilen im Teamspiel: auch wer alle Regeln übergibt, bekommt nur passende ins Raster (fail-closed).</summary>
        [TestMethod]
        public void DistributingInATeamGameNeverPlacesARuleThatDoesNotFit()
        {
            var zellen = Enumerable.Range(0, 40).Select(_ => Zelle()).ToList();
            var wuerfe = new Random(7);
            var auftrag = new Verteilauftrag(40, false, Enum.GetValues<CellSpecialRule>().ToList(), wuerfe.Next) { Teamspiel = true };

            var belegt = Sonderregelraster.Verteile(zellen, auftrag);

            Assert.AreEqual(40, belegt.Count, "Es wurde nicht alles belegt - die Zusicherung darunter prüfte zu wenig.");
            var unpassend = belegt.Where(z => !SpecialRuleProfiles.Applies(z, z.QuestionBase, teamspiel: true)).Select(z => z.SpecialRule).Distinct().ToList();
            Assert.AreEqual(0, unpassend.Count, "Im Teamspiel verteilt: " + string.Join(", ", unpassend));
        }

        /// <summary>Die Teamberatung (M37 T5) ist eine Teamregel: nur im Teamspiel gespielt, angeboten und verteilt.</summary>
        [TestMethod]
        public void TheTeamCouncilIsATeamRuleAndOnlyPlayedInATeamGame()
        {
            var zelle = Zelle(CellSpecialRule.TeamCouncil);

            Assert.IsTrue(SpecialRuleProfiles.TryFor(CellSpecialRule.TeamCouncil, out var profil) && profil.IstTeamregel);
            Assert.IsFalse(profil.HasPrePhase, "Die Teamberatung spielt keine Vorphase - sie läuft über Sprecher und Countdown.");
            Assert.IsTrue(SpecialRuleProfiles.Applies(zelle, zelle.QuestionBase, teamspiel: true));
            Assert.IsFalse(SpecialRuleProfiles.Applies(zelle, zelle.QuestionBase, teamspiel: false), "Die Teamberatung spielt ohne Teams.");
            CollectionAssert.Contains(SpecialRuleProfiles.Angeboten(teamspiel: true).Select(p => p.Rule).ToList(), CellSpecialRule.TeamCouncil);
            CollectionAssert.DoesNotContain(SpecialRuleProfiles.Angeboten(teamspiel: false).Select(p => p.Rule).ToList(), CellSpecialRule.TeamCouncil);

            var befund = Sonderregelraster.Startbefund(zelle, zelle.QuestionBase, teamspiel: false);
            Assert.AreEqual(Sonderregelraster.TeamGameRuleCode, befund?.Code);
            StringAssert.Contains(befund!.Message, "nur im Teamspiel");
        }

        [TestMethod]
        public void DistributingWithoutTeamsNeverPlacesATeamRule()
        {
            var zellen = Enumerable.Range(0, 40).Select(_ => Zelle()).ToList();
            var auftrag = new Verteilauftrag(40, false, [CellSpecialRule.TeamCouncil, CellSpecialRule.Stake], new Random(3).Next);

            var belegt = Sonderregelraster.Verteile(zellen, auftrag);

            Assert.AreEqual(40, belegt.Count);
            Assert.IsTrue(belegt.All(z => z.SpecialRule == CellSpecialRule.Stake), "Ohne Teams wurde eine Teamregel verteilt.");
        }

        [TestMethod]
        public void TheGameStartWarnsAboutARuleThatIsNotPlayedInATeamGame()
        {
            var auktion = Zelle(CellSpecialRule.Auction);

            var befund = Sonderregelraster.Startbefund(auktion, auktion.QuestionBase, teamspiel: true);

            Assert.AreEqual(Sonderregelraster.TeamGameRuleCode, befund?.Code);
            Assert.IsFalse(befund!.IsError, "Eine nicht gespielte Regel hält den Start an.");
            StringAssert.Contains(befund.Message, "Auktion");
            Assert.IsNull(Sonderregelraster.Startbefund(auktion, auktion.QuestionBase, teamspiel: false));
            Assert.IsNull(Sonderregelraster.Startbefund(Zelle(CellSpecialRule.Stake), new DefaultQuestion(), teamspiel: true));
        }
    }
}
