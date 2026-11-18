using LocalBuzzer.Service.Base;
using LocalBuzzer.Service.Hubs.Accessors;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;

namespace Quizzer.LogicUnitTests.LocalBuzzer.Base
{
    /// <summary>
    /// Aufstellung und Team-Sperre am Server ohne Netz (M37 T4, F-M37-09): je Frage buzzert jedes Team einmal, geprüft in
    /// <c>BuzzerState.TryBuzz</c> unter derselben Sperre. Die Sperre überlebt „Runde zurücksetzen“, gilt ohne Teamspiel nie,
    /// und ein Spieler ohne Team ist sein eigenes Team.
    /// </summary>
    [TestClass]
    public class TeamSperreStateUnitTests
    {
        private Player anna = null!;
        private Player bert = null!;
        private Player carl = null!;
        private Player dora = null!;
        private GameTeam rot = null!;
        private GameTeam blau = null!;
        private Game game = null!;
        private LayoutStateManager manager = null!;

        [TestInitialize]
        public void Aufbau()
        {
            anna = Spieler("Anna");
            bert = Spieler("Bert");
            carl = Spieler("Carl");
            dora = Spieler("Dora");
            game = new Game { Id = Guid.NewGuid(), IsTeamGame = true };
            rot = new GameTeam { Id = Guid.NewGuid(), GameId = game.Id, Name = "Rot", ColorIndex = 5 };
            blau = new GameTeam { Id = Guid.NewGuid(), GameId = game.Id, Name = "Blau", ColorIndex = 4, SortOrder = 1 };
            game.GameTeams.AddRange([rot, blau]);

            // Anna und Bert sind Rot, Carl ist Blau, Dora ohne Team.
            foreach (var (p, team) in new[] { (anna, rot), (bert, rot), (carl, blau), (dora, (GameTeam?)null) })
                game.PlayerXGames.Add(new PlayerXGame { Id = Guid.NewGuid(), GameId = game.Id, PlayerId = p.Id, Player = p, GameTeamId = team?.Id });

            manager = new LayoutStateManager(new GameAccessor { GetGame = () => game });
            manager.SetTeams(TeamRoster.From(game));
            manager.ResetLayouts(1, BuzzerControlsLayout.Buzzer);
        }

        private static Player Spieler(string name) => new() { Id = Guid.NewGuid(), Designation = name, DisplayName = name };

        private void Zuruecksetzen() => manager.ResetLayouts(1, BuzzerControlsLayout.Buzzer);

        [TestMethod]
        public void TheRosterCarriesNameColorAndSymbolAndOnlyTeamsOfThisGame()
        {
            var fremd = new GameTeam { Id = Guid.NewGuid(), GameId = Guid.NewGuid(), Name = "Fremd", ColorIndex = 1 };
            game.PlayerXGames.Single(x => x.PlayerId == dora.Id).GameTeamId = fremd.Id;
            var aufstellung = TeamRoster.From(game);

            Assert.AreEqual(new TeamRosterEntry(rot.Id, "Rot", 5, "✚") { Speaker = "Anna", SpeakerId = anna.Id, SortOrder = rot.SortOrder }, aufstellung.TeamOf(anna.Id));
            Assert.AreEqual(new TeamRosterEntry(blau.Id, "Blau", 4, "★") { Speaker = "Carl", SpeakerId = carl.Id, SortOrder = blau.SortOrder }, aufstellung.TeamOf(carl.Id));
            Assert.IsNull(aufstellung.TeamOf(dora.Id), "Ein Team aus einem anderen Spiel gilt.");
            Assert.AreEqual(dora.Id, aufstellung.KeyFor(dora.Id), "Ohne Team ist Dora nicht ihr eigenes Team.");
            Assert.AreEqual(rot.Id, aufstellung.KeyFor(bert.Id));

            rot.ColorIndex = 8;
            Assert.AreEqual(new TeamRosterEntry(rot.Id, "Rot", -1, string.Empty) { Speaker = "Anna", SpeakerId = anna.Id, SortOrder = rot.SortOrder }, TeamRoster.From(game).TeamOf(anna.Id),
                "Eine Farbe außerhalb der Palette geht als Farbe ans Telefon.");
        }

        [TestMethod]
        public void WithoutATeamGameThereIsNoRosterAndNoKey()
        {
            game.IsTeamGame = false;
            var aufstellung = TeamRoster.From(game);

            Assert.IsFalse(aufstellung.IsTeamGame);
            Assert.IsNull(aufstellung.TeamOf(anna.Id), "Ohne Teamspiel hat Anna ein Team.");
            Assert.IsNull(aufstellung.KeyFor(anna.Id), "Ohne Teamspiel greift die Team-Sperre.");
            Assert.AreSame(TeamRoster.Empty, TeamRoster.From(null));
        }

        [TestMethod]
        public void ATeammateCannotBuzzAfterTheResetButTheOtherTeamCan()
        {
            Assert.IsTrue(manager.BuzzerState.TryBuzz(anna), "Anna kann nicht buzzern.");
            CollectionAssert.AreEquivalent(new[] { rot.Id }, manager.TeamBuzzLock.LockedTeams.ToList(), "Annas Buzzer sperrt nicht ihr Team.");

            Zuruecksetzen();

            Assert.IsFalse(manager.BuzzerState.TryBuzz(bert), "Bert buzzert, obwohl sein Team schon gebuzzert hat.");
            Assert.IsFalse(manager.BuzzerState.TryBuzz(anna), "Anna buzzert ein zweites Mal.");
            Assert.IsNull(manager.BuzzerState.Winner, "Ein Teamkollege wurde Gewinner.");
            Assert.IsTrue(manager.BuzzerState.TryBuzz(carl), "Carl aus dem anderen Team kann nicht buzzern.");
            CollectionAssert.AreEquivalent(new[] { rot.Id, blau.Id }, manager.TeamBuzzLock.LockedTeams.ToList());
        }

        [TestMethod]
        public void ThePlayerWithoutATeamLocksOnlyHimself()
        {
            Assert.IsTrue(manager.BuzzerState.TryBuzz(dora));
            Zuruecksetzen();

            Assert.IsFalse(manager.BuzzerState.TryBuzz(dora), "Dora ohne Team buzzert ein zweites Mal - fail-closed verletzt.");
            Assert.IsTrue(manager.BuzzerState.TryBuzz(anna), "Doras Buzzer sperrte ein Team, dem sie nicht angehört.");
        }

        [TestMethod]
        public void WithoutATeamGameTheSamePlayerBuzzesAgainAfterTheReset()
        {
            game.IsTeamGame = false;
            manager.SetTeams(TeamRoster.From(game));

            Assert.IsTrue(manager.BuzzerState.TryBuzz(anna));
            Zuruecksetzen();

            Assert.IsTrue(manager.BuzzerState.TryBuzz(anna), "Ohne Teamspiel sperrt der Buzzer den Spieler für die Frage.");
            Assert.AreEqual(0, manager.TeamBuzzLock.LockedTeams.Count, "Ohne Teamspiel steht eine Team-Sperre.");
        }

        [TestMethod]
        public void ClearingLetsTheTeamBuzzAgain()
        {
            Assert.IsTrue(manager.BuzzerState.TryBuzz(anna));
            Zuruecksetzen();
            manager.TeamBuzzLock.Clear();

            Assert.IsTrue(manager.BuzzerState.TryBuzz(bert), "Nach dem Freigeben darf Bert nicht buzzern.");
        }

        [TestMethod]
        public void TheLockFollowsTheTeamWhenTheRosterIsReplaced()
        {
            Assert.IsTrue(manager.BuzzerState.TryBuzz(anna));
            Zuruecksetzen();

            // Carl wechselt zu Rot - die Sperre hängt am Team, nicht an der alten Aufstellung.
            game.PlayerXGames.Single(x => x.PlayerId == carl.Id).GameTeamId = rot.Id;
            manager.SetTeams(TeamRoster.From(game));

            Assert.IsFalse(manager.BuzzerState.TryBuzz(carl), "Carl buzzert nach dem Wechsel für das gesperrte Team.");
        }

        /// <summary>Die Wettlauf-Probe: zwei drücken gleichzeitig - genau ein Gewinner, und nur sein Team ist gesperrt.</summary>
        [TestMethod]
        public void TwoBuzzingAtOnceGiveExactlyOneWinnerAndLockOnlyHisTeam()
        {
            for (var i = 0; i < 300; i++)
            {
                manager.TeamBuzzLock.Clear();
                Zuruecksetzen();

                var ergebnisse = Gleichzeitig(anna, bert, carl);
                var gewinner = manager.BuzzerState.Winner!;

                Assert.AreEqual(1, ergebnisse.Count(e => e), $"Durchlauf {i}: nicht genau ein Gewinner.");
                CollectionAssert.AreEquivalent(new[] { manager.Teams.KeyFor(gewinner.Id)!.Value }, manager.TeamBuzzLock.LockedTeams.ToList(),
                    $"Durchlauf {i}: gesperrt ist nicht genau das Team des Gewinners {gewinner.Designation}.");
            }
        }

        private bool[] Gleichzeitig(params Player[] spieler)
        {
            using var start = new Barrier(spieler.Length);
            var ergebnisse = new bool[spieler.Length];

            Parallel.For(0, spieler.Length, new ParallelOptions { MaxDegreeOfParallelism = spieler.Length }, i =>
            {
                start.SignalAndWait();
                ergebnisse[i] = manager.BuzzerState.TryBuzz(spieler[i]);
            });

            return ergebnisse;
        }

        [TestMethod]
        public void ThePhoneOfATeammateSaysWhyOnceTheRoundIsOpenAgain()
        {
            Assert.IsTrue(manager.BuzzerState.TryBuzz(anna));

            // Solange Anna dran ist, zeigt Berts Telefon den Gewinner, nicht die Sperre.
            Assert.IsFalse(manager.CreateClientState(bert).ExcludedFromQuestion, "Bert ist schon gesperrt, während Anna antwortet.");

            Zuruecksetzen();
            var berts = manager.CreateClientState(bert);
            var carls = manager.CreateClientState(carl);

            Assert.IsTrue(berts.ExcludedFromQuestion, "Berts Telefon sagt nicht, dass sein Team gesperrt ist.");
            Assert.IsTrue(berts.CurrentLayoutLocked, "Berts Buzzer ist offen.");
            Assert.AreEqual("Dein Team hat schon gebuzzert.", berts.ExcludedReason, "Berts Telefon nennt nicht den Satz der Team-Sperre.");
            Assert.IsFalse(carls.ExcludedFromQuestion, "Carl aus dem anderen Team ist gesperrt.");
            Assert.IsFalse(carls.CurrentLayoutLocked, "Carls Buzzer ist zu.");

            // Bei der Tastenwahl antworten alle zugleich - dort sperrt das Team nichts.
            manager.ResetLayouts(1, BuzzerControlsLayout.KeySelect);
            Assert.IsFalse(manager.CreateClientState(bert).ExcludedFromQuestion, "Die Team-Sperre greift bei der Tastenwahl.");
        }

        [TestMethod]
        public void EachPhoneGetsItsOwnTeamAndWithoutTeamNone()
        {
            var annas = manager.CreateClientState(anna).Team;

            Assert.IsNotNull(annas, "Annas Telefon bekommt kein Team.");
            Assert.AreEqual(("Rot", "✚", 5), (annas.Name, annas.Symbol, annas.ColorIndex));
            Assert.AreEqual("Blau", manager.CreateClientState(carl).Team?.Name);
            Assert.IsNull(manager.CreateClientState(dora).Team, "Dora ohne Team bekommt eins.");
            Assert.IsNull(manager.CreateClientState(null).Team);

            manager.SetTeams(TeamRoster.Empty);
            Assert.IsNull(manager.CreateClientState(anna).Team, "Ohne Teamspiel bekommt das Telefon ein Team.");
        }
    }
}
