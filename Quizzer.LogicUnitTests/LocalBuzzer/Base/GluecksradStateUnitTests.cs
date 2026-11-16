using LocalBuzzer.Service.Base.States.Minispiele;
using LocalBuzzer.Service.Hubs.Accessors;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Questions.Minispiele;
using System.Text.Json;

namespace Quizzer.LogicUnitTests.LocalBuzzer.Base
{
    /// <summary>
    /// Das Gluecksrad am Server (M35 C4) ohne Netz: das Feld wird hier gewuerfelt, landet an der Serveruhr,
    /// nur der Dreher waehlt ein Ziel (ein Mitspieler, nie er selbst), und das Feld sieht vor der Aufloesung
    /// nur, wer gerade waehlt.
    /// </summary>
    [TestClass]
    public class GluecksradStateUnitTests
    {
        private static readonly TimeSpan Vorlauf = TimeSpan.FromSeconds(3);

        private Player anna = null!;
        private Player bert = null!;
        private Player carl = null!;
        private StellbareUhr uhr = null!;
        private MinigameState spiel = null!;
        private WheelSegment feld = WheelSegment.Steal;

        [TestInitialize]
        public void Aufbau()
        {
            anna = Spieler("Anna");
            bert = Spieler("Bert");
            carl = Spieler("Carl");
            uhr = new StellbareUhr();

            var game = new Game { Id = Guid.NewGuid() };
            foreach (var p in new[] { anna, bert, carl })
                game.PlayerXGames.Add(new PlayerXGame { Id = Guid.NewGuid(), GameId = game.Id, PlayerId = p.Id, Player = p });

            spiel = new MinigameState(new GameAccessor { GetGame = () => game })
            {
                Zufall = (_, _) => GluecksradEvaluator.Felder.ToList().IndexOf(feld),
                Uhr = uhr,
            };
        }

        private static Player Spieler(string name)
            => new() { Id = Guid.NewGuid(), Designation = name, DisplayName = name, ConnectionState = PlayerConnection.Connected };

        private WheelOfFortuneRules Regeln => (WheelOfFortuneRules)spiel.Rules!;

        private void Starte(WheelSegment gedreht, Guid? ausloeser = null)
        {
            feld = gedreht;
            spiel.Configure(new MinigameSetup(MinigameKind.WheelOfFortune, "Glücksrad", "Regel", 1, TriggerPlayerId: ausloeser ?? anna.Id));
            spiel.Reset();
            Assert.IsTrue(spiel.StartNextRound(), "Das Rad liess sich nicht drehen.");
        }

        private MinigameSubmitResult Waehle(Player p, Guid ziel, int? durchgang = null)
            => spiel.Submit(p, new MinigameAction
            {
                Kind = MinigameKind.WheelOfFortune,
                Durchgang = durchgang ?? spiel.Durchgang,
                Payload = JsonSerializer.SerializeToElement(new { chosen = ziel }),
            });

        private WheelClientInfo Art(Player? p) => (WheelClientInfo)((MinigameClientInfo)spiel.InfoFor(p)).KindInfo!;

        [TestMethod]
        public void TheFieldIsRolledAtTheServerAndLandsAtTheServerClock()
        {
            Starte(WheelSegment.Double);

            Assert.AreEqual(WheelSegment.Double, Regeln.Segment);
            Assert.IsFalse(Regeln.Gelandet, "Das Rad steht schon im Countdown.");
            Assert.IsNull(spiel.CloseIfDue(), "Die Runde schliesst, bevor das Rad steht.");
            Assert.IsTrue(new[] { anna, bert, carl }.All(spiel.IsLockedFor), "Ohne Ziel darf ein Telefon abgeben.");

            uhr.Vor(Vorlauf);

            Assert.IsTrue(Regeln.Gelandet);
            Assert.IsNotNull(spiel.CloseIfDue(), "Ohne Ziel schliesst die Runde nicht, wenn das Rad steht.");
            Assert.AreEqual("Verdoppeln – Anna bekommt den doppelten Anteil.", Art(carl).ResultText);
            Assert.AreEqual("Verdoppeln", Art(carl).SegmentName);
        }

        [TestMethod]
        public void OnlyTheSpinnerChoosesAFellowPlayerWhoIsNotHimself()
        {
            Starte(WheelSegment.Steal);
            uhr.Vor(Vorlauf);

            Assert.IsTrue(Art(anna).ChoosesTarget, "Der Dreher bekommt keine Zielwahl.");
            Assert.AreEqual("Wen beklaust du?", Art(anna).ChoicePrompt);
            CollectionAssert.AreEquivalent(new[] { "Bert", "Carl" }, Art(anna).Options.Select(o => o.Name).ToList());
            Assert.IsFalse(Art(bert).ChoosesTarget);
            Assert.IsTrue(spiel.IsLockedFor(bert), "Ein Zuschauer ist offen.");

            Assert.IsFalse(Waehle(bert, carl.Id).Accepted, "Ein anderer als der Dreher hat gewaehlt.");
            Assert.IsFalse(Waehle(anna, anna.Id).Accepted, "Der Dreher hat sich selbst gewaehlt.");
            Assert.IsFalse(Waehle(anna, Guid.NewGuid()).Accepted, "Ein Fremder wurde gewaehlt.");
            Assert.IsFalse(Waehle(anna, bert.Id, spiel.Durchgang - 1).Accepted, "Ein alter Durchgang wurde angenommen.");

            var wahl = Waehle(anna, bert.Id);

            Assert.IsTrue(wahl.Accepted);
            Assert.AreEqual(new WheelTargetChosen(anna.Id, bert.Id), wahl.Value);
            Assert.IsNotNull(wahl.Closed, "Die Zielwahl schliesst die Runde nicht.");
            Assert.AreEqual(bert.Id, Regeln.PartnerId);
            Assert.AreEqual("Klauen – Anna beklaut Bert.", Art(bert).ResultText);
        }

        [TestMethod]
        public void ASecondChoiceIsRejected()
        {
            Starte(WheelSegment.Gift);

            // Die Wahl schliesst die Runde; danach nimmt nichts mehr an.
            Assert.IsTrue(Waehle(anna, bert.Id).Accepted);
            Assert.IsFalse(Waehle(anna, carl.Id).Accepted, "Eine zweite Wahl wurde angenommen.");
            Assert.AreEqual(bert.Id, Regeln.PartnerId);
        }

        /// <summary>Vor der Aufloesung kennt das Feld nur, wer gerade waehlt - sonst verriete ein Telefon den Beamer.</summary>
        [TestMethod]
        public void OnlyTheChoosingPhoneSeesTheFieldBeforeTheEnd()
        {
            Starte(WheelSegment.Steal);

            Assert.AreEqual("Klauen", Art(anna).SegmentName);
            Assert.IsNull(Art(bert).SegmentName, "Ein Zuschauer kennt das Feld vor der Aufloesung.");
            Assert.IsNull(Art(null).SegmentName, "Der Blick von aussen kennt das Feld vor der Aufloesung.");
            Assert.IsNull(Art(bert).ResultText);

            Starte(WheelSegment.BadLuck);

            Assert.IsNull(Art(anna).SegmentName, "Ohne Zielwahl kennt der Dreher das Feld vor dem Beamer.");
        }

        /// <summary>Ein getrennter Dreher haelt die Runde nicht auf: der Spielleiter legt das Ziel fest (E1), die Fristwache schliesst.</summary>
        [TestMethod]
        public void TheGameMasterCanSetTheTargetAndTheRoundClosesAtTheServer()
        {
            Starte(WheelSegment.Steal);
            uhr.Vor(Vorlauf);

            Assert.IsTrue(spiel.SetPair(anna.Id, carl.Id));

            Assert.IsTrue(spiel.IsLockedFor(anna), "Nach der Vorgabe des Spielleiters waehlt das Telefon noch.");
            Assert.IsFalse(Waehle(anna, bert.Id).Accepted, "Das Telefon ueberschreibt die Vorgabe des Spielleiters.");
            Assert.IsNotNull(spiel.CloseIfDue(), "Mit Vorgabe schliesst die Runde nicht.");
            Assert.AreEqual(carl.Id, Regeln.PartnerId);
        }

        /// <summary>Getrennt haelt der Dreher die Runde offen; wiederverbunden waehlt er weiter - und danach nicht noch einmal.</summary>
        [TestMethod]
        public void AReconnectingSpinnerStillChoosesAndAfterwardsNotAgain()
        {
            Starte(WheelSegment.Gift);
            uhr.Vor(Vorlauf);

            anna.ConnectionState = PlayerConnection.Disconnected;
            Assert.IsNull(spiel.CloseIfComplete(), "Die Trennung des Drehers schliesst die Runde ohne Ziel.");
            Assert.IsNull(spiel.CloseIfDue(), "Die Fristwache schliesst ohne Ziel.");

            anna.ConnectionState = PlayerConnection.Connected;
            Assert.IsTrue(Art(anna).ChoosesTarget, "Der wiederverbundene Dreher bekommt seine Zielwahl nicht zurueck.");
            Assert.IsTrue(Waehle(anna, carl.Id).Accepted);

            var info = (MinigameClientInfo)spiel.InfoFor(anna);
            Assert.IsTrue(info.OwnSubmitted, "Nach dem Wiederverbinden darf der Dreher noch einmal waehlen.");
            Assert.IsFalse(((WheelClientInfo)info.KindInfo!).ChoosesTarget, "Nach der Wahl bekommt der Dreher wieder die Tasten.");
        }

        [TestMethod]
        public void SpinningAgainForgetsTheTargetUnlessTheGameMasterSetIt()
        {
            Starte(WheelSegment.Steal);
            Assert.IsTrue(Waehle(anna, bert.Id).Accepted);

            Assert.IsTrue(spiel.RepeatRound());
            Assert.IsNull(Regeln.PartnerId, "Neu gedreht - und das alte Ziel gilt noch.");

            Assert.IsTrue(spiel.SetPair(anna.Id, carl.Id));
            Assert.IsTrue(spiel.RepeatRound());
            Assert.AreEqual(carl.Id, Regeln.PartnerId, "Die Vorgabe des Spielleiters ging beim Neudrehen verloren.");
        }
    }
}
