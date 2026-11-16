using LocalBuzzer.Service.Base.States.Minispiele;
using LocalBuzzer.Service.Base.States.Sonderregeln;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;

namespace Quizzer.LogicUnitTests.LocalBuzzer.Base
{
    /// <summary>Die Vorphasen mit Spielerwahl (Wette, Experte, Allianz): nur Mitspieler, Selbstwahl je Regel, geheim.</summary>
    public partial class SonderregelVorphaseC3bUnitTests
    {
        // --- Wette ---

        [TestMethod]
        public void ABetGoesOnAnyPlayerIncludingOneselfButNeverOnAStranger()
        {
            Starte(CellSpecialRule.Bet, anteil: 50);

            var info = Info<ChoiceClientInfo>(anna);
            Assert.AreEqual(3, info.Options.Count, "Die Wette bietet nicht alle an, auch einen selbst.");
            StringAssert.Contains(info.Prompt, "Einsatz 50", "Die Frage nennt den Einsatz nicht.");
            StringAssert.Contains(info.Options.Single(o => o.Id == clara.Id).Name, "Gewinn 100", "Die Letzte bringt nicht das Doppelte.");

            Assert.IsFalse(Gib(bert, new { chosen = Guid.NewGuid() }, CellSpecialRule.Bet).Accepted, "Eine Wette auf einen Fremden angenommen.");
            Assert.IsFalse(Gib(bert, new { amount = 5 }, CellSpecialRule.Bet).Accepted, "Eine kaputte Wahl angenommen.");
            Assert.IsTrue(Gib(bert, new { chosen = bert.Id }, CellSpecialRule.Bet).Accepted, "Die Wette auf sich selbst abgewiesen.");
            Assert.IsFalse(Gib(bert, new { chosen = anna.Id }, CellSpecialRule.Bet).Accepted, "Zweite Wette angenommen.");

            Assert.IsNull(Info<ChoiceClientInfo>(anna).OwnChoice, "Anna sieht Berts Wette.");
            Assert.AreEqual(bert.Id, Info<ChoiceClientInfo>(bert).OwnChoice, "Bert sieht seine eigene Wette nicht.");
            Assert.IsNull(((ChoiceClientInfo)zustand.Rules!.InfoFor(null, MinigamePhase.Running)!).OwnChoice, "Der Beamer sieht eine Wette.");
        }

        // --- Experte ---

        [TestMethod]
        public void AnExpertIsNeverOneselfAndNeverAStranger()
        {
            Starte(CellSpecialRule.Expert);

            var info = Info<ChoiceClientInfo>(anna);
            Assert.AreEqual(2, info.Options.Count, "Der Experte bietet nicht genau die anderen an.");
            Assert.IsFalse(info.Options.Any(o => o.Id == anna.Id), "Anna kann sich selbst waehlen.");

            Assert.IsFalse(Gib(anna, new { chosen = anna.Id }, CellSpecialRule.Expert).Accepted, "Sich selbst gewaehlt.");
            Assert.IsFalse(Gib(anna, new { chosen = Guid.NewGuid() }, CellSpecialRule.Expert).Accepted, "Einen Fremden gewaehlt.");
            Assert.IsFalse(Gib(anna, new { chosen = bert.Id }, CellSpecialRule.Bet).Accepted, "Abgabe fuer eine fremde Regel angenommen.");
            Assert.IsTrue(Gib(anna, new { chosen = bert.Id }, CellSpecialRule.Expert).Accepted);
            Assert.IsNull(Info<ChoiceClientInfo>(bert).OwnChoice, "Bert sieht Annas Wahl.");
        }
    }
}
