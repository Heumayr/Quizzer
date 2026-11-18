using LocalBuzzer.Service.Base;
using LocalBuzzer.Service.Base.States.Minispiele;
using LocalBuzzer.Service.Hubs.Accessors;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Questions.Minispiele;
using Quizzer.DataModels.Questions.Teams;

namespace Quizzer.LogicUnitTests.LocalBuzzer.Base
{
    /// <summary>
    /// Sprecher und Teamreihenfolge am Hub (M37 P1, V27). Drei Teams, deren Reihenfolge nach <c>SortOrder</c>, nach Name und nach
    /// Kennung jeweils eine andere ist - Zeppelin (0), Mitte (1), Anton (2) -, und in Zeppelin zwei Mitglieder namens „Anna“: der
    /// Sprecher ist die zweite. Wer sich auf Name oder Kennung stützt, fällt hier auf.
    /// </summary>
    [TestClass]
    public class TeamreihenfolgeUnitTests
    {
        private readonly Player anna1 = Spieler("00000000-0000-0000-0000-0000000000a1", "Anna");
        private readonly Player anna2 = Spieler("00000000-0000-0000-0000-0000000000a2", "Anna");
        private readonly Player bert = Spieler("00000000-0000-0000-0000-0000000000b0", "Bert");
        private readonly Player carl = Spieler("00000000-0000-0000-0000-0000000000c0", "Carl");
        private readonly Player dora = Spieler("00000000-0000-0000-0000-0000000000d0", "Dora");
        private Game game = null!;
        private GameTeam zeppelin = null!;
        private GameTeam mitte = null!;
        private GameTeam anton = null!;
        private MinigameState spiel = null!;

        [TestInitialize]
        public void Aufbau()
        {
            game = new Game { Id = Guid.NewGuid(), IsTeamGame = true };
            mitte = Team("10000000-0000-0000-0000-000000000000", "Mitte", 1, 4);
            anton = Team("20000000-0000-0000-0000-000000000000", "Anton", 2, 3);

            // SpeakerTurn 1: die Folge ist Anna (a1), Anna (a2), Bert - ab Platz 1 spricht die zweite Anna.
            zeppelin = Team("30000000-0000-0000-0000-000000000000", "Zeppelin", 0, 5);
            zeppelin.SpeakerTurn = 1;
            game.GameTeams.AddRange([anton, mitte, zeppelin]);

            foreach (var (p, team) in new[] { (anna1, zeppelin), (anna2, zeppelin), (bert, zeppelin), (carl, mitte), (dora, anton) })
                game.PlayerXGames.Add(new PlayerXGame { Id = Guid.NewGuid(), GameId = game.Id, PlayerId = p.Id, Player = p, GameTeamId = team.Id });

            var aufstellung = TeamRoster.From(game);
            spiel = new MinigameState(new GameAccessor { GetGame = () => game }, () => aufstellung) { Uhr = new StellbareUhr() };
        }

        private static Player Spieler(string id, string name)
            => new() { Id = new Guid(id), Designation = name, DisplayName = name, ConnectionState = PlayerConnection.Connected };

        private GameTeam Team(string id, string name, int sortOrder, int farbe)
            => new() { Id = new Guid(id), GameId = game.Id, Name = name, SortOrder = sortOrder, ColorIndex = farbe };

        private void Starte(MinigameSetup setup)
        {
            spiel.Configure(setup);
            spiel.Reset();
            Assert.IsTrue(spiel.StartNextRound(), $"{setup.Kind} liess sich nicht starten.");
        }

        private void StarteStaffel()
            => Starte(new MinigameSetup(MinigameKind.Relay, "Staffel", "Regel", 1, TriggerPlayerId: anna1.Id,
                Content: [.. new[] { "Merkur", "Venus", "Erde" }.Select(t => new MinispielZeile(t, false))]));

        private IReadOnlyList<Guid> NachSortOrder => [zeppelin.Id, mitte.Id, anton.Id];

        /// <summary>Teamberatung (WPF und Hub über Kennungen) und Staffel finden denselben Sprecher - nicht den ersten Namensvetter.</summary>
        [TestMethod]
        public void TheRelayStartsWithTheSpeakerEvenWhenAnotherMemberSharesTheName()
        {
            Assert.AreEqual(anna2.Id, Sprecherfolge.Sprecher(Sprecherfolge.Folge(game, zeppelin)), "Vorbedingung: die Teamberatung findet die zweite Anna.");
            Assert.AreEqual(anna2.Id, TeamRoster.From(game).TeamOf(bert.Id)?.SpeakerId, "Die Aufstellung am Hub kennt den Sprecher nicht als Kennung.");

            StarteStaffel();

            Assert.AreEqual(anna2.Id, spiel.Lenke(r => ((RelayRules)r!).Stand).Laeufer, "Die Staffel beginnt nicht beim Sprecher, sondern bei seinem Namensvetter.");
        }

        /// <summary>Tauziehen, Stellvertreter-Duell und Staffel reihen die Teams nach <c>SortOrder</c> - nie nach Kennung oder Name.</summary>
        [TestMethod]
        public void EveryTeamKindListsTheTeamsBySortOrder()
        {
            Starte(new MinigameSetup(MinigameKind.TugOfWar, "Tauziehen", "Regel", 1, DurationSeconds: 10));
            var tauziehen = (TugOfWarClientInfo)((MinigameClientInfo)spiel.InfoFor(anna1)).KindInfo!;
            CollectionAssert.AreEqual(new[] { "Zeppelin", "Mitte", "Anton" }, tauziehen.Teams.Select(t => t.Name).ToList(), "Tauziehen am Telefon.");
            CollectionAssert.AreEqual(NachSortOrder.ToList(), spiel.Lenke(r => ((TugOfWarRules)r!).Stand).Teams.Select(t => t.TeamId).ToList(), "Tauziehen, Stand am Hub.");

            Starte(new MinigameSetup(MinigameKind.ProxyDuel, "Stellvertreter-Duell", "Regel", 1));
            CollectionAssert.AreEqual(NachSortOrder.ToList(), spiel.Lenke(r => ((ProxyDuelRules)r!).Teams).ToList(), "Stellvertreter-Duell am Hub.");

            StarteStaffel();
            CollectionAssert.AreEqual(NachSortOrder.ToList(), spiel.Lenke(r => ((RelayRules)r!).Stand).Teams.Select(t => t.TeamId).ToList(), "Staffel am Hub.");
        }

        /// <summary>Der Beamer übernimmt die Reihenfolge des Hubs - die Wahltafel des Stellvertreter-Duells sortiert nicht nach Namen um.</summary>
        [TestMethod]
        public void TheProxyBoardKeepsTheOrderOfTheHub()
        {
            string Name(Guid id) => game.GameTeams.Single(t => t.Id == id).Name;

            var tafel = StellvertreterTafel.Wahl(NachSortOrder, [], Name, Name);

            CollectionAssert.AreEqual(new[] { "Zeppelin", "Mitte", "Anton" }, tafel.Select(z => z.Name).ToList());
        }
    }
}
