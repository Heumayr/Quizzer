using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Helpers;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Questions.Sonderregeln;

namespace Quizzer.LogicUnitTests.DataModels.Sonderregeln
{
    /// <summary>
    /// Die Team-Abstimmung (M37 T7) ist eine Teamregel ohne Vorphase und braucht Tasten: gespielt, angeboten und verteilt nur
    /// an einer Multiple-Choice-Frage im Teamspiel - fail-closed an der einen Stelle <see cref="SpecialRuleProfiles.Applies"/>.
    /// An einer anderen Frage warnt der Spielstart und sagt, woran es liegt.
    /// </summary>
    [TestClass]
    public class TeamAbstimmungRegelUnitTests
    {
        private static GameGridCoordinate Zelle(QuestionType typ, CellSpecialRule regel = CellSpecialRule.None)
        {
            var frage = Factory.CreateNewQuestion(typ);
            frage.Id = Guid.NewGuid();
            return new() { Id = Guid.NewGuid(), QuestionBase = frage, QuestionBaseId = frage.Id, SpecialRule = regel };
        }

        [TestMethod]
        public void TheTeamVoteIsATeamRuleWithoutPrePhase()
        {
            Assert.IsTrue(SpecialRuleProfiles.TryFor(CellSpecialRule.TeamVote, out var profil) && profil.IstTeamregel, "Die Team-Abstimmung ist keine Teamregel.");
            Assert.AreEqual("Team-Abstimmung", profil.DisplayName);
            Assert.IsFalse(profil.HasPrePhase, "Die Team-Abstimmung spielt eine Vorphase - abgestimmt wird an der Frage selbst.");
            Assert.IsFalse(profil.EinmalJeSpiel);
            CollectionAssert.Contains(SpecialRuleProfiles.Angeboten(teamspiel: true).Select(p => p.Rule).ToList(), CellSpecialRule.TeamVote);
            CollectionAssert.DoesNotContain(SpecialRuleProfiles.Angeboten(teamspiel: false).Select(p => p.Rule).ToList(), CellSpecialRule.TeamVote);
        }

        /// <summary>Nur Multiple Choice - auch nicht die übrigen Typen mit Tasten, deren Stimme eine ganze Reihenfolge wäre.</summary>
        [TestMethod]
        public void TheTeamVoteIsPlayedOnlyOnAMultipleChoiceQuestionInATeamGame()
        {
            var mc = Zelle(QuestionType.MultipleChoice, CellSpecialRule.TeamVote);

            Assert.IsTrue(SpecialRuleProfiles.Applies(mc, mc.QuestionBase, teamspiel: true));
            Assert.IsFalse(SpecialRuleProfiles.Applies(mc, mc.QuestionBase, teamspiel: false), "Die Team-Abstimmung spielt ohne Teams.");

            foreach (var typ in Enum.GetValues<QuestionType>().Where(t => t != QuestionType.MultipleChoice))
            {
                var zelle = Zelle(typ, CellSpecialRule.TeamVote);
                Assert.IsFalse(SpecialRuleProfiles.Applies(zelle, zelle.QuestionBase, teamspiel: true), $"Die Team-Abstimmung spielt an {typ}.");
            }

            Assert.IsFalse(SpecialRuleProfiles.Applies(mc, null, teamspiel: true), "Ohne geladene Frage wird gespielt.");
        }

        /// <summary>Die übrigen Regeln kennen keine Einschränkung - sie spielen an jeder gewöhnlichen Frage weiter.</summary>
        [TestMethod]
        public void TheOtherRulesStillFitEveryOrdinaryQuestion()
        {
            var andere = SpecialRuleProfiles.All.Where(p => p.Rule != CellSpecialRule.TeamVote).ToList();

            Assert.IsTrue(andere.All(p => p.QuestionTypes == null), "Eine weitere Regel ist auf Fragetypen beschränkt: "
                + string.Join(", ", andere.Where(p => p.QuestionTypes != null).Select(p => p.DisplayName)));
            Assert.IsTrue(andere.All(p => p.PasstZuFrage(new DefaultQuestion())));
        }

        [TestMethod]
        public void TheGameStartSaysTheTeamVoteNeedsAMultipleChoiceQuestion()
        {
            var falsch = Zelle(QuestionType.Default, CellSpecialRule.TeamVote);
            var mc = Zelle(QuestionType.MultipleChoice, CellSpecialRule.TeamVote);

            var befund = Sonderregelraster.Startbefund(falsch, falsch.QuestionBase, teamspiel: true);

            Assert.AreEqual(Sonderregelraster.QuestionTypeRuleCode, befund?.Code);
            Assert.IsFalse(befund!.IsError, "Eine nicht gespielte Regel hält den Start an.");
            Assert.AreEqual("Zelle 1/1: Teamregel „Team-Abstimmung“ braucht eine Multiple-Choice-Frage – sie wird nicht gespielt.", befund.Message);
            Assert.IsNull(Sonderregelraster.Startbefund(mc, mc.QuestionBase, teamspiel: true));
            Assert.AreEqual(Sonderregelraster.TeamGameRuleCode, Sonderregelraster.Startbefund(mc, mc.QuestionBase, teamspiel: false)?.Code,
                "Ohne Teams liegt es am Spiel, nicht an der Frage.");
        }

        /// <summary>Verteilen im Teamspiel: die Team-Abstimmung landet nur auf Multiple Choice, die anderen Zellen bekommen eine andere Regel.</summary>
        [TestMethod]
        public void DistributingPlacesTheTeamVoteOnlyOnMultipleChoice()
        {
            var zellen = Enumerable.Range(0, 40).Select(i => Zelle(i % 2 == 0 ? QuestionType.MultipleChoice : QuestionType.Default)).ToList();
            var auftrag = new Verteilauftrag(40, false, [CellSpecialRule.TeamVote, CellSpecialRule.TeamCouncil], new Random(5).Next) { Teamspiel = true };

            var belegt = Sonderregelraster.Verteile(zellen, auftrag);

            Assert.AreEqual(40, belegt.Count, "Eine Zelle blieb frei, obwohl eine andere Regel passt.");
            Assert.IsTrue(belegt.Count(z => z.SpecialRule == CellSpecialRule.TeamVote) > 5, "Kaum Team-Abstimmungen verteilt - die Zusicherung darunter prüfte zu wenig.");
            var unpassend = belegt.Where(z => !SpecialRuleProfiles.Applies(z, z.QuestionBase, teamspiel: true)).ToList();
            Assert.AreEqual(0, unpassend.Count, $"{unpassend.Count} Regeln liegen, wo sie nicht gespielt werden.");
        }

        /// <summary>Passt keine der Regeln zur Frage, bleibt die Zelle frei.</summary>
        [TestMethod]
        public void ACellThatFitsNoRuleStaysFree()
        {
            var zellen = new[] { Zelle(QuestionType.Default), Zelle(QuestionType.MultipleChoice) };
            var auftrag = new Verteilauftrag(2, false, [CellSpecialRule.TeamVote], new Random(1).Next) { Teamspiel = true };

            var belegt = Sonderregelraster.Verteile(zellen, auftrag);

            Assert.AreEqual(1, belegt.Count);
            Assert.AreEqual(QuestionType.MultipleChoice, belegt[0].QuestionBase!.Typ);
            Assert.AreEqual(CellSpecialRule.None, zellen[0].SpecialRule);
        }
    }
}
