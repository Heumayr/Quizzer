using LocalBuzzer.Service.Base;
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
    /// Ziele im Teamspiel nur Gegner (M37 T8, F-M37-01: Klauen und Sperren treffen nur gegnerische Teams) - am Server, über
    /// die Aufstellung des Hubs, nie aus dem Spielgraphen: Räuber, Duell, Teilen, Kartoffel und Glücksrad bieten nur Gegner an
    /// und weisen einen Teamkollegen ab, auch wenn der Spielleiter ihn festlegt; das Glücksrad dreht ohne Robin Hood; beim
    /// Bluff zählen nur hereingelegte Gegner.
    /// </summary>
    [TestClass]
    public class MinispielZieleTeamUnitTests
    {
        private static readonly TimeSpan Vorlauf = TimeSpan.FromSeconds(3);

        private Player anna = null!;
        private Player bert = null!;
        private Player carl = null!;
        private Player dora = null!;
        private Game game = null!;
        private StellbareUhr uhr = null!;
        private TeamRoster aufstellung = TeamRoster.Empty;
        private MinigameState spiel = null!;
        private int wurf;

        [TestInitialize]
        public void Aufbau()
        {
            anna = Spieler("Anna");
            bert = Spieler("Bert");
            carl = Spieler("Carl");
            dora = Spieler("Dora");
            uhr = new StellbareUhr();
            game = new Game { Id = Guid.NewGuid(), IsTeamGame = true };
            var rot = new GameTeam { Id = Guid.NewGuid(), GameId = game.Id, Name = "Rot", ColorIndex = 5 };
            var blau = new GameTeam { Id = Guid.NewGuid(), GameId = game.Id, Name = "Blau", ColorIndex = 4 };
            game.GameTeams.AddRange([rot, blau]);

            // Anna und Bert sind Rot, Carl ist Blau, Dora ohne Team.
            foreach (var (p, team) in new[] { (anna, rot), (bert, rot), (carl, blau), (dora, (GameTeam?)null) })
                game.PlayerXGames.Add(new PlayerXGame { Id = Guid.NewGuid(), GameId = game.Id, PlayerId = p.Id, Player = p, GameTeamId = team?.Id });

            aufstellung = TeamRoster.From(game);

            // Grosse Wuerfe (Zuender der Kartoffel) nehmen eine feste Stelle, kleine (Rad, Mischen) den eingestellten Wurf.
            spiel = new MinigameState(new GameAccessor { GetGame = () => game }, () => aufstellung)
            {
                Zufall = (a, b) => b > 1000 ? 21_337 : b <= a ? a : Math.Clamp(wurf, a, b - 1),
                Uhr = uhr,
            };
        }

        private static Player Spieler(string name)
            => new() { Id = Guid.NewGuid(), Designation = name, DisplayName = name, ConnectionState = PlayerConnection.Connected };

        private void Starte(MinigameKind art, IReadOnlyList<MinispielZeile>? inhalt = null)
        {
            spiel.Configure(new MinigameSetup(art, "Spiel", "Regel", art == MinigameKind.Bluff ? 2 : 1, DurationSeconds: 60,
                TriggerPlayerId: anna.Id, Share: 50, Content: inhalt));
            spiel.Reset();
            Assert.IsTrue(spiel.StartNextRound(), $"{art} liess sich nicht starten.");
            uhr.Vor(Vorlauf);
        }

        private MinigameSubmitResult Gib(Player p, object nutzlast)
            => spiel.Submit(p, new MinigameAction
            {
                Kind = spiel.Setup.Kind,
                Durchgang = spiel.Durchgang,
                Payload = JsonSerializer.SerializeToElement(nutzlast),
            });

        private T Art<T>(Player p) => (T)((MinigameClientInfo)spiel.InfoFor(p)).KindInfo!;

        private static List<Guid> Ids(IEnumerable<MinigamePlayerOption> optionen) => optionen.Select(o => o.Id).ToList();

        // --- Räuber ---

        [TestMethod]
        public void TheRobberSeesOnlyOpponentsAndCannotRobATeammate()
        {
            Starte(MinigameKind.RobberAndGuard);

            CollectionAssert.AreEquivalent(new[] { carl.Id, dora.Id }, Ids(Art<RobberClientInfo>(anna).Options), "Anna werden Teamkollegen angeboten.");
            Assert.IsFalse(Gib(anna, new { victim = bert.Id }).Accepted, "Anna hat ihren Teamkollegen Bert bestohlen.");
            Assert.IsTrue(Gib(anna, new { victim = carl.Id }).Accepted, "Der Raub bei Carl aus dem anderen Team wurde verworfen.");
        }

        /// <summary>Wer das Team in die Nutzlast schreibt, ändert nichts: es zählt die Aufstellung des Hubs.</summary>
        [TestMethod]
        public void ATeamIdInTheRobberPayloadIsIgnored()
        {
            Starte(MinigameKind.RobberAndGuard);

            Assert.IsFalse(Gib(bert, new { victim = anna.Id, teamId = Guid.NewGuid(), playerId = carl.Id }).Accepted,
                "Mit fremder teamId und playerId in der Nutzlast trifft der Raub die Teamkollegin.");
        }

        [TestMethod]
        public void WithoutATeamGameTheRobberMayRobAnyone()
        {
            aufstellung = TeamRoster.Empty;
            Starte(MinigameKind.RobberAndGuard);

            CollectionAssert.AreEquivalent(new[] { bert.Id, carl.Id, dora.Id }, Ids(Art<RobberClientInfo>(anna).Options));
            Assert.IsTrue(Gib(anna, new { victim = bert.Id }).Accepted, "Ohne Teamspiel ist Bert kein Opfer.");
        }

        // --- Duell und Teilen: die Spielerwahl ---

        [TestMethod]
        [DataRow(MinigameKind.Duel)]
        [DataRow(MinigameKind.ShareOrTake)]
        public void TheTriggerChoosesOnlyAnOpponent(MinigameKind art)
        {
            Starte(art);

            var optionen = art == MinigameKind.Duel ? Art<DuelClientInfo>(anna).Options : Art<ShareOrTakeClientInfo>(anna).Options;
            CollectionAssert.AreEquivalent(new[] { carl.Id, dora.Id }, Ids(optionen), $"{art}: Anna werden Teamkollegen angeboten.");
            Assert.IsFalse(Gib(anna, new { chosen = bert.Id }).Accepted, $"{art}: Anna hat ihren Teamkollegen gewählt.");
            Assert.IsTrue(Gib(anna, new { chosen = carl.Id }).Accepted, $"{art}: die Wahl von Carl wurde verworfen.");
        }

        /// <summary>Auch der Spielleiter legt kein Paar aus einem Team fest - Punkte korrigiert er auf der Karte (E1).</summary>
        [TestMethod]
        [DataRow(MinigameKind.Duel)]
        [DataRow(MinigameKind.ShareOrTake)]
        [DataRow(MinigameKind.WheelOfFortune)]
        public void TheGameMasterCannotPairTeammates(MinigameKind art)
        {
            wurf = GluecksradEvaluator.Felder.ToList().IndexOf(WheelSegment.Steal);
            Starte(art);

            Assert.IsFalse(spiel.SetPair(anna.Id, bert.Id), $"{art}: das Paar Anna und Bert aus einem Team wurde festgelegt.");
            Assert.IsTrue(spiel.SetPair(anna.Id, dora.Id), $"{art}: Dora ohne Team ist kein Gegner.");
        }

        // --- Glücksrad ---

        [TestMethod]
        public void TheWheelStealsOnlyFromAnOpponent()
        {
            wurf = GluecksradEvaluator.Felder.ToList().IndexOf(WheelSegment.Steal);
            Starte(MinigameKind.WheelOfFortune);

            Assert.AreEqual(WheelSegment.Steal, ((WheelOfFortuneRules)spiel.Rules!).Segment, "Die Probe dreht nicht auf Klauen.");
            CollectionAssert.AreEquivalent(new[] { carl.Id, dora.Id }, Ids(Art<WheelClientInfo>(anna).Options), "Anna werden Teamkollegen angeboten.");
            Assert.IsFalse(Gib(anna, new { chosen = bert.Id }).Accepted, "Anna hat ihren Teamkollegen beklaut.");
            Assert.IsTrue(Gib(anna, new { chosen = carl.Id }).Accepted, "Das Klauen bei Carl wurde verworfen.");
        }

        /// <summary>Jeder Wurf des Rads im Teamspiel trifft ein Feld ohne Robin Hood - ohne Teamspiel liegt Robin Hood auf dem Rad.</summary>
        [TestMethod]
        public void TheWheelHasNoRobinHoodInATeamGame()
        {
            HashSet<WheelSegment> Felder()
            {
                var gedreht = new HashSet<WheelSegment>();

                for (wurf = 0; wurf < GluecksradEvaluator.Felder.Count + 2; wurf++)
                {
                    Starte(MinigameKind.WheelOfFortune);
                    gedreht.Add(((WheelOfFortuneRules)spiel.Rules!).Segment!.Value);
                }

                return gedreht;
            }

            CollectionAssert.AreEquivalent(
                new[] { WheelSegment.Double, WheelSegment.Steal, WheelSegment.Gift, WheelSegment.BadLuck }, Felder().ToList(),
                "Im Teamspiel dreht das Rad nicht genau die vier Felder ohne Robin Hood.");

            aufstellung = TeamRoster.Empty;
            CollectionAssert.Contains(Felder().ToList(), WheelSegment.RobinHood, "Ohne Teamspiel fehlt Robin Hood - die Probe misst nichts.");
        }

        // --- Kartoffel ---

        [TestMethod]
        public void ThePotatoGoesOnlyToAnOpponent()
        {
            Starte(MinigameKind.HotPotato, KartoffelPruefstand.Aussagen);
            var info = Art<HotPotatoClientInfo>(anna);
            Assert.IsNotNull(info.StatementText, "Anna hält die Kartoffel nicht.");

            uhr.Vor(TimeSpan.FromMilliseconds(800));
            Assert.IsTrue(Gib(anna, new { statementNumber = info.StatementNumber, answer = KartoffelPruefstand.Wahr(info.StatementText) }).Accepted,
                "Die richtige Antwort wurde verworfen.");

            var wahl = Art<HotPotatoClientInfo>(anna);
            CollectionAssert.AreEquivalent(new[] { carl.Id, dora.Id }, Ids(wahl.Options), "Anna werden Teamkollegen angeboten.");
            Assert.IsFalse(Gib(anna, new { statementNumber = wahl.StatementNumber, passTo = bert.Id }).Accepted, "Anna gab an ihren Teamkollegen weiter.");
            Assert.IsTrue(Gib(anna, new { statementNumber = wahl.StatementNumber, passTo = carl.Id }).Accepted, "Die Weitergabe an Carl wurde verworfen.");
        }

        // --- Bluff ---

        /// <summary>Anna (Rot) blufft; Bert (Rot) und Carl (Blau) fallen herein - am Telefon zählt nur Carl, ohne Teams beide.</summary>
        [TestMethod]
        public void TheBluffCountsOnlyFooledOpponentsOnThePhone()
        {
            int Reinfaelle()
            {
                Starte(MinigameKind.Bluff, [new MinispielZeile("Rom", false)]);
                Gib(anna, new { bluff = "Paris" });
                Gib(bert, new { bluff = "Wien" });
                Gib(carl, new { bluff = "Mailand" });
                Gib(dora, new { bluff = "Turin" });
                Assert.IsTrue(spiel.StartNextRound(), "Die Wahl liess sich nicht starten.");
                uhr.Vor(Vorlauf);

                var paris = Art<BluffClientInfo>(carl).Options.Single(o => o.Text == "Paris").Number;
                var rom = Art<BluffClientInfo>(carl).Options.Single(o => o.Text == "Rom").Number;
                Gib(bert, new { choice = paris });
                Gib(carl, new { choice = paris });
                Gib(anna, new { choice = rom });
                Assert.IsNotNull(Gib(dora, new { choice = rom }).Closed, "Die Wahl schliesst nicht, obwohl alle gewählt haben.");

                return Art<BluffClientInfo>(anna).FooledCount;
            }

            Assert.AreEqual(1, Reinfaelle(), "Im Teamspiel zählt Annas Teamkollege Bert als hereingelegt.");

            aufstellung = TeamRoster.Empty;
            Assert.AreEqual(2, Reinfaelle(), "Ohne Teamspiel zählen nicht beide - die Probe misst nichts.");
        }
    }
}
