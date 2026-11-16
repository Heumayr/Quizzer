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
    /// Die Spiele mit Spielerwahl am Server (M35 C2) ohne Netz: die Wahl kommt aus dem WLAN und muss ein
    /// Mitspieler sein, nie der Waehlende; nur die Gewaehlten spielen; fremde Wahlen bleiben bis zur
    /// Aufloesung am Server.
    /// </summary>
    [TestClass]
    public class SpielerwahlStateUnitTests
    {
        private Player anna = null!;
        private Player bert = null!;
        private Player carl = null!;
        private MinigameState spiel = null!;

        [TestInitialize]
        public void Aufbau()
        {
            anna = Spieler("Anna");
            bert = Spieler("Bert");
            carl = Spieler("Carl");

            var game = new Game { Id = Guid.NewGuid() };
            foreach (var p in new[] { anna, bert, carl })
                game.PlayerXGames.Add(new PlayerXGame { Id = Guid.NewGuid(), GameId = game.Id, PlayerId = p.Id, Player = p });

            spiel = new MinigameState(new GameAccessor { GetGame = () => game }) { Zufall = (_, _) => 3000 };
        }

        private static Player Spieler(string name)
            => new() { Id = Guid.NewGuid(), Designation = name, DisplayName = name, ConnectionState = PlayerConnection.Connected };

        private void Starte(MinigameKind art, int runden = 1, Guid? ausloeser = null, int variante = 0)
        {
            spiel.Configure(new MinigameSetup(art, "Spiel", "Regel", runden, TriggerPlayerId: ausloeser, Variant: variante));
            spiel.Reset();
            Assert.IsTrue(spiel.StartNextRound(), "Die Runde liess sich nicht starten.");
        }

        private MinigameSubmitResult Gib(Player p, object nutzlast, MinigameKind art, int? durchgang = null)
            => spiel.Submit(p, new MinigameAction
            {
                Kind = art,
                Durchgang = durchgang ?? spiel.Durchgang,
                Payload = JsonSerializer.SerializeToElement(nutzlast),
            });

        private T Art<T>(Player p) => (T)((MinigameClientInfo)spiel.InfoFor(p)).KindInfo!;

        // --- Duell ---

        [TestMethod]
        public void OnlyTheTriggerChoosesAndOnlyAFellowPlayerWhoIsNotHimself()
        {
            Starte(MinigameKind.Duel, runden: 3, ausloeser: anna.Id);

            Assert.IsTrue(Art<DuelClientInfo>(anna).ChoosesOpponent, "Der Ausloeser bekommt keine Wahl.");
            CollectionAssert.AreEquivalent(new[] { "Bert", "Carl" }, Art<DuelClientInfo>(anna).Options.Select(o => o.Name).ToList(),
                "Die Tasten sind nicht genau die anderen Mitspieler.");
            Assert.AreEqual(0, Art<DuelClientInfo>(bert).Options.Count, "Ein anderer bekommt die Tasten der Wahl.");

            Assert.IsFalse(Gib(bert, new { chosen = carl.Id }, MinigameKind.Duel).Accepted, "Ein anderer als der Ausloeser hat gewaehlt.");
            Assert.IsFalse(Gib(anna, new { chosen = anna.Id }, MinigameKind.Duel).Accepted, "Der Ausloeser hat sich selbst gewaehlt.");
            Assert.IsFalse(Gib(anna, new { chosen = Guid.NewGuid() }, MinigameKind.Duel).Accepted, "Ein Fremder wurde als Gegner angenommen.");
            Assert.IsFalse(Gib(anna, new { chosen = "kaputt" }, MinigameKind.Duel).Accepted, "Eine kaputte Wahl wurde angenommen.");

            var vorher = spiel.Durchgang;
            var wahl = Gib(anna, new { chosen = bert.Id }, MinigameKind.Duel);

            Assert.IsTrue(wahl.Accepted && wahl.Restarted, "Die Wahl eines Mitspielers oeffnet die naechste Stufe nicht.");
            Assert.AreEqual(new PlayerChosen(anna.Id, bert.Id), wahl.Value);
            Assert.AreNotEqual(vorher, spiel.Durchgang, "Nach der Wahl gilt kein neuer Durchgang.");
            Assert.IsFalse(Gib(anna, new { move = 1 }, MinigameKind.Duel, durchgang: vorher).Accepted, "Ein Zug im alten Durchgang zaehlt.");
        }

        [TestMethod]
        public void OnlyTheDuellistsPlayTheOthersWatchAndNobodySubmitsTwice()
        {
            Starte(MinigameKind.Duel, runden: 3, ausloeser: anna.Id);

            Assert.IsTrue(spiel.IsLockedFor(bert) && spiel.IsLockedFor(carl), "Vor der Wahl ist jemand anderes als der Ausloeser offen.");
            StringAssert.Contains(((MinigameClientInfo)spiel.InfoFor(carl)).WatcherText, "Anna wählt einen Gegner");

            Gib(anna, new { chosen = bert.Id }, MinigameKind.Duel);

            Assert.IsFalse(spiel.IsLockedFor(anna) || spiel.IsLockedFor(bert), "Ein Duellant ist gesperrt.");
            Assert.IsTrue(spiel.IsLockedFor(carl), "Der Zuschauer ist offen.");
            StringAssert.Contains(((MinigameClientInfo)spiel.InfoFor(carl)).WatcherText, "Anna und Bert spielen");
            Assert.IsFalse(Gib(carl, new { move = 2 }, MinigameKind.Duel).Accepted, "Ein Zuschauer hat gezogen.");

            Assert.IsTrue(Gib(anna, new { move = 2 }, MinigameKind.Duel).Accepted);
            Assert.IsFalse(Gib(anna, new { move = 3 }, MinigameKind.Duel).Accepted, "Ein zweiter Zug wurde angenommen.");
            Assert.IsFalse(Gib(bert, new { move = 7 }, MinigameKind.Duel).Accepted, "Ein Zug, den es nicht gibt, wurde angenommen.");

            // Die Runde schliesst, sobald beide Duellanten gezogen haben - der Zuschauer wird nicht abgewartet.
            var zu = Gib(bert, new { move = 1 }, MinigameKind.Duel);
            Assert.IsNotNull(zu.Closed, "Die Runde wartet auf den Zuschauer.");
        }

        /// <summary>Geheim bis zur Aufloesung: kein Telefon erfaehrt den fremden Zug, solange die Runde laeuft.</summary>
        [TestMethod]
        public void TheOpponentsMoveStaysSecretUntilTheRoundCloses()
        {
            Starte(MinigameKind.Duel, runden: 3, ausloeser: anna.Id);
            Gib(anna, new { chosen = bert.Id }, MinigameKind.Duel);
            Gib(anna, new { move = (int)SchereSteinPapier.Stein }, MinigameKind.Duel);

            Assert.AreEqual(SchereSteinPapier.Stein, Art<DuelClientInfo>(anna).OwnMove, "Die eigene Abgabe kommt nicht zurueck.");
            Assert.IsNull(Art<DuelClientInfo>(bert).OtherMove, "Bert erfaehrt Annas Zug, bevor er zieht.");
            Assert.IsNull(Art<DuelClientInfo>(carl).OwnMove ?? Art<DuelClientInfo>(carl).OtherMove, "Der Zuschauer erfaehrt einen Zug.");
            Assert.IsFalse(JsonSerializer.Serialize(spiel.InfoFor(bert)).Contains("Move\":2"), "Annas Zug steht in Berts Zustand.");

            Gib(bert, new { move = (int)SchereSteinPapier.Schere }, MinigameKind.Duel);

            Assert.AreEqual(MinigamePhase.RoundOver, spiel.Phase);
            Assert.AreEqual(SchereSteinPapier.Schere, Art<DuelClientInfo>(anna).OtherMove, "Nach der Runde fehlt der Zug des Gegners.");
            Assert.AreEqual(1, Art<DuelClientInfo>(anna).OwnWins);
        }

        [TestMethod]
        public void BestOfThreeEndsAsSoonAsItIsDecided()
        {
            Starte(MinigameKind.Duel, runden: 3, ausloeser: anna.Id);
            Gib(anna, new { chosen = bert.Id }, MinigameKind.Duel);

            Gib(anna, new { move = (int)SchereSteinPapier.Stein }, MinigameKind.Duel);
            Assert.IsFalse(Gib(bert, new { move = (int)SchereSteinPapier.Schere }, MinigameKind.Duel).Closed!.Finished, "1:0 ist noch nicht entschieden.");

            spiel.StartNextRound();
            Gib(anna, new { move = (int)SchereSteinPapier.Papier }, MinigameKind.Duel);
            var zweite = Gib(bert, new { move = (int)SchereSteinPapier.Stein }, MinigameKind.Duel);

            Assert.IsTrue(zweite.Closed!.Finished, "2:0 bei Best of 3 spielt noch eine dritte Runde.");
            Assert.AreEqual(MinigamePhase.Finished, spiel.Phase);
            Assert.IsTrue(Art<DuelClientInfo>(anna).IsWinner);
            Assert.AreEqual("Anna", Art<DuelClientInfo>(carl).WinnerName, "Der Zuschauer erfaehrt nicht, wer gewonnen hat.");
        }

        [TestMethod]
        public void WithoutTriggerTheGameMasterChoosesThePair()
        {
            Starte(MinigameKind.Duel, runden: 1, ausloeser: Guid.NewGuid());

            Assert.IsTrue(new[] { anna, bert, carl }.All(spiel.IsLockedFor), "Ohne Ausloeser darf jemand spielen.");
            StringAssert.Contains(((MinigameClientInfo)spiel.InfoFor(anna)).WatcherText, "Spielleiter");

            Assert.IsFalse(spiel.SetPair(anna.Id, anna.Id), "Ein Paar aus einem Spieler wurde angenommen.");
            Assert.IsFalse(spiel.SetPair(anna.Id, Guid.NewGuid()), "Ein Fremder wurde ins Paar genommen.");

            var vorher = spiel.Durchgang;
            Assert.IsTrue(spiel.SetPair(carl.Id, bert.Id));
            Assert.AreNotEqual(vorher, spiel.Durchgang, "Mitten in der Runde gilt nach dem Festlegen kein neuer Durchgang.");
            Assert.IsFalse(spiel.IsLockedFor(carl) || spiel.IsLockedFor(bert));
            Assert.IsTrue(spiel.IsLockedFor(anna));
        }

        [TestMethod]
        public void TheReactionDuelTakesTimesOnlyFromTheDuellists()
        {
            Starte(MinigameKind.Duel, runden: 1, ausloeser: anna.Id, variante: DuellEvaluator.ReaktionVariant);
            Gib(anna, new { chosen = bert.Id }, MinigameKind.Duel);

            Assert.AreEqual(3000, Art<DuelClientInfo>(anna).Reaction!.WaitMs, "Die Wartezeit der Reaktion kommt nicht an.");
            Assert.IsFalse(Gib(carl, new { falseStart = false, milliseconds = 150 }, MinigameKind.Duel).Accepted);
            Assert.IsTrue(Gib(anna, new { falseStart = false, milliseconds = 300 }, MinigameKind.Duel).Accepted);

            var zu = Gib(bert, new { falseStart = false, milliseconds = 250 }, MinigameKind.Duel);
            Assert.IsTrue(zu.Closed!.Finished);
            Assert.IsTrue(Art<DuelClientInfo>(bert).IsWinner, "Die schnellere Zeit gewinnt das Duell nicht.");
        }

        // --- Raeuber und Waechter ---

        [TestMethod]
        public void TheRobberMustNameAFellowPlayerWhoIsNotHimself()
        {
            Starte(MinigameKind.RobberAndGuard);

            Assert.IsFalse(Gib(anna, new { shield = false, victim = anna.Id }, MinigameKind.RobberAndGuard).Accepted, "Sich selbst bestehlen wurde angenommen.");
            Assert.IsFalse(Gib(anna, new { shield = false, victim = Guid.NewGuid() }, MinigameKind.RobberAndGuard).Accepted, "Ein Fremder wurde als Opfer angenommen.");
            Assert.IsFalse(Gib(anna, new { shield = false }, MinigameKind.RobberAndGuard).Accepted, "Ein Raub ohne Opfer wurde angenommen.");

            Assert.AreEqual(RaeuberWahl.Raub(bert.Id), Gib(anna, new { shield = false, victim = bert.Id }, MinigameKind.RobberAndGuard).Value);
            Assert.IsFalse(Gib(anna, new { shield = true }, MinigameKind.RobberAndGuard).Accepted, "Eine zweite Wahl wurde angenommen.");
        }

        /// <summary>Geheim bis zur Aufloesung: wer wen bestehlen will, erfaehrt kein Telefon vorher.</summary>
        [TestMethod]
        public void NoPhoneLearnsAnotherChoiceBeforeTheResolution()
        {
            Starte(MinigameKind.RobberAndGuard);

            Gib(anna, new { shield = false, victim = bert.Id }, MinigameKind.RobberAndGuard);
            Gib(bert, new { shield = true }, MinigameKind.RobberAndGuard);

            Assert.AreEqual(MinigamePhase.Running, spiel.Phase, "Die Runde ist zu, obwohl Carl fehlt.");

            foreach (var p in new[] { anna, bert, carl })
                Assert.AreEqual(0, Art<RobberClientInfo>(p).OwnResults.Count, $"{p.Designation} erfaehrt vor der Aufloesung, was andere gewaehlt haben.");

            Assert.IsTrue(Art<RobberClientInfo>(bert).OwnShield && Art<RobberClientInfo>(bert).OwnVictimName == null);
            Assert.AreEqual(2, Art<RobberClientInfo>(carl).Options.Count, "Carl bekommt nicht die beiden anderen zur Wahl.");
            Assert.IsFalse(JsonSerializer.Serialize(spiel.InfoFor(bert)).Contains("Anna"), "Annas Raub steht in Berts Zustand.");

            Gib(carl, new { shield = false, victim = bert.Id }, MinigameKind.RobberAndGuard);

            Assert.AreEqual(MinigamePhase.Finished, spiel.Phase);
            CollectionAssert.Contains(Art<RobberClientInfo>(bert).OwnResults, "Dein Schild hielt gegen Anna – Anna zahlt dir.");
            CollectionAssert.Contains(Art<RobberClientInfo>(anna).OwnResults, "Bert hatte ein Schild – du zahlst.");
        }

        // --- Teilen oder Nehmen ---

        [TestMethod]
        public void ShareOrTakeLetsOnlyThePairDecideAndKeepsThePartnerSecret()
        {
            Starte(MinigameKind.ShareOrTake, ausloeser: bert.Id);

            Assert.IsFalse(Gib(bert, new { chosen = bert.Id }, MinigameKind.ShareOrTake).Accepted, "Bert hat sich selbst gewaehlt.");
            Assert.IsTrue(Gib(bert, new { chosen = carl.Id }, MinigameKind.ShareOrTake).Restarted);

            Assert.IsTrue(spiel.IsLockedFor(anna), "Wer nicht gewaehlt ist, darf entscheiden.");
            Assert.IsFalse(Gib(anna, new { take = true }, MinigameKind.ShareOrTake).Accepted);
            Assert.IsFalse(Gib(bert, new { }, MinigameKind.ShareOrTake).Accepted, "Eine Abgabe ohne Entscheidung wurde angenommen.");

            Gib(bert, new { take = true }, MinigameKind.ShareOrTake);
            Assert.IsNull(Art<ShareOrTakeClientInfo>(carl).OtherTake, "Carl erfaehrt Berts Wahl, bevor er entscheidet.");
            Assert.IsTrue(Art<ShareOrTakeClientInfo>(bert).OwnTake == true);

            var zu = Gib(carl, new { take = false }, MinigameKind.ShareOrTake);
            Assert.IsTrue(zu.Closed!.Finished);
            Assert.AreEqual(true, Art<ShareOrTakeClientInfo>(carl).OtherTake, "Nach der Aufloesung fehlt die Wahl des anderen.");
        }
    }
}
