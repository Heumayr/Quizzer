using LocalBuzzer.Service.Base;
using LocalBuzzer.Service.Base.States.Joker;
using LocalBuzzer.Service.Base.States.Minispiele;
using LocalBuzzer.Service.Hubs.Accessors;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using System.Text.Json;

namespace Quizzer.LogicUnitTests.LocalBuzzer.Base
{
    /// <summary>
    /// Die Joker-Sperre im Teamspiel (M37 T4, F-M37-01: Klauen und Sperren treffen nur Gegner) - erster Riegel am Server,
    /// über <c>MinigameRulesContext.TeamVon</c> aus der Aufstellung des Hubs, nie aus dem Spielgraphen. Den zweiten Riegel
    /// hält <c>PlayerJokersRedeemTeamUnitTests</c>.
    /// </summary>
    [TestClass]
    public class JokerTeamStateUnitTests
    {
        private Player anna = null!;
        private Player bert = null!;
        private Player carl = null!;
        private Player dora = null!;
        private Game game = null!;
        private TeamRoster aufstellung = TeamRoster.Empty;
        private readonly JokerOffer annasSperre = new(Guid.NewGuid(), JokerKind.Block);
        private readonly JokerOffer dorasSperre = new(Guid.NewGuid(), JokerKind.Block);

        [TestInitialize]
        public void Aufbau()
        {
            anna = Spieler("Anna");
            bert = Spieler("Bert");
            carl = Spieler("Carl");
            dora = Spieler("Dora");
            game = new Game { Id = Guid.NewGuid(), IsTeamGame = true };
            var rot = new GameTeam { Id = Guid.NewGuid(), GameId = game.Id, Name = "Rot", ColorIndex = 5 };
            var blau = new GameTeam { Id = Guid.NewGuid(), GameId = game.Id, Name = "Blau", ColorIndex = 4 };
            game.GameTeams.AddRange([rot, blau]);

            // Anna und Bert sind Rot, Carl ist Blau, Dora ohne Team.
            foreach (var (p, team) in new[] { (anna, rot), (bert, rot), (carl, blau), (dora, (GameTeam?)null) })
                game.PlayerXGames.Add(new PlayerXGame { Id = Guid.NewGuid(), GameId = game.Id, PlayerId = p.Id, Player = p, GameTeamId = team?.Id });

            aufstellung = TeamRoster.From(game);
        }

        private static Player Spieler(string name)
            => new() { Id = Guid.NewGuid(), Designation = name, DisplayName = name, ConnectionState = PlayerConnection.Connected };

        /// <summary>Anna und Dora halten je eine Sperre - die Runde läuft mit der Aufstellung, die der Hub gerade hat.</summary>
        private MinigameState Runde()
        {
            var zustand = new MinigameState(new GameAccessor { GetGame = () => game }, () => aufstellung);
            zustand.Configure(new MinigameSetup(default, "Joker-Runde", "Wer setzt einen Joker?", 1, CountdownSeconds: 0,
                Jokers: new Dictionary<Guid, IReadOnlyList<JokerOffer>> { [anna.Id] = [annasSperre], [dora.Id] = [dorasSperre] }));
            zustand.Reset();
            Assert.IsTrue(zustand.StartNextRound(), "Die Joker-Runde liess sich nicht starten.");
            return zustand;
        }

        private static bool Sperre(MinigameState z, Player wer, Guid joker, Guid ziel, object? extra = null)
            => z.Submit(wer, new MinigameAction
            {
                PrePhase = MinigamePrePhase.Joker,
                Durchgang = z.Durchgang,
                Payload = JsonSerializer.SerializeToElement(extra ?? new { jokerId = joker, target = ziel }),
            }).Accepted;

        private static List<Guid> Ziele(MinigameState z, Player wer)
            => ((JokerClientInfo)((MinigameClientInfo)z.InfoFor(wer)).KindInfo!).Targets.Select(t => t.Id).ToList();

        [TestMethod]
        public void ABlockAgainstATeammateIsRejected()
        {
            var z = Runde();

            Assert.IsFalse(Sperre(z, anna, annasSperre.Id, bert.Id), "Anna hat ihren Teamkollegen Bert gesperrt.");
            Assert.IsTrue(Sperre(z, anna, annasSperre.Id, carl.Id), "Die Sperre gegen Carl aus dem anderen Team wurde verworfen.");
        }

        /// <summary>Wer das Team in die Nutzlast schreibt, ändert nichts: es zählt die Aufstellung des Hubs.</summary>
        [TestMethod]
        public void ATeamIdInThePayloadIsIgnored()
        {
            var z = Runde();

            Assert.IsFalse(Sperre(z, anna, annasSperre.Id, bert.Id, new { jokerId = annasSperre.Id, target = bert.Id, teamId = Guid.NewGuid() }),
                "Mit einer fremden teamId in der Nutzlast trifft die Sperre den Teamkollegen.");
        }

        [TestMethod]
        public void ThePhoneOffersOnlyOpponents()
        {
            var z = Runde();

            CollectionAssert.AreEquivalent(new[] { carl.Id, dora.Id }, Ziele(z, anna), "Anna werden Teamkollegen oder niemand angeboten.");
            CollectionAssert.AreEquivalent(new[] { anna.Id, bert.Id, carl.Id }, Ziele(z, dora), "Dora ohne Team ist nicht allen ein Gegner.");
        }

        [TestMethod]
        public void APlayerWithoutTeamIsEveryonesOpponent()
        {
            var z = Runde();

            Assert.IsTrue(Sperre(z, dora, dorasSperre.Id, anna.Id), "Dora ohne Team darf Anna nicht sperren.");
            Assert.IsTrue(Sperre(z, anna, annasSperre.Id, dora.Id), "Anna darf Dora ohne Team nicht sperren.");
        }

        [TestMethod]
        public void WithoutATeamGameEveryOtherPlayerIsATarget()
        {
            aufstellung = TeamRoster.Empty;
            var z = Runde();

            CollectionAssert.AreEquivalent(new[] { bert.Id, carl.Id, dora.Id }, Ziele(z, anna));
            Assert.IsTrue(Sperre(z, anna, annasSperre.Id, bert.Id), "Ohne Teamspiel ist Bert kein Ziel.");
        }

        /// <summary>Die Aufstellung wird beim Prüfen gelesen, nicht beim Einrichten - nach dem Umsetzen gilt die neue.</summary>
        [TestMethod]
        public void TheCheckReadsTheRosterTheHubHasNow()
        {
            var z = Runde();

            game.PlayerXGames.Single(x => x.PlayerId == bert.Id).GameTeamId = game.GameTeams[1].Id;
            aufstellung = TeamRoster.From(game);

            Assert.IsTrue(Sperre(z, anna, annasSperre.Id, bert.Id), "Nach dem Umsetzen gilt noch die alte Aufstellung.");
        }

        [TestMethod]
        public void TeammatesAreOnlyThoseWithTheSameTeam()
        {
            var kontext = new MinigameRulesContext((a, b) => a, _ => null, () => [], TeamVon: id => aufstellung.TeamOf(id)?.TeamId);

            Assert.IsTrue(kontext.Teamkollegen(anna.Id, bert.Id));
            Assert.IsFalse(kontext.Teamkollegen(anna.Id, carl.Id));
            Assert.IsFalse(kontext.Teamkollegen(dora.Id, dora.Id), "Ohne Team zählt einer als sein eigener Teamkollege.");
            Assert.IsFalse(new MinigameRulesContext((a, b) => a, _ => null, () => []).Teamkollegen(anna.Id, bert.Id),
                "Ohne Aufstellung gibt es Teamkollegen.");
        }
    }
}
