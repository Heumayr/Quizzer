using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Models.Base;
using Quizzer.Logic.Controller.TypedControllers;

namespace Quizzer.LogicUnitTests.Logic
{
    /// <summary>
    /// Ein geladener Graph enthält jeden Datensatz genau einmal.
    /// <para>
    /// <c>AsNoTracking()</c> allein erzeugt je Ergebniszeile eine eigene Instanz. Steht derselbe
    /// Datensatz zweimal im Graphen - ein Spieler als Moderator <b>und</b> als Mitspieler -,
    /// kommen zwei Objekte mit derselben Id zurück. Hängt später etwas eines davon wieder an,
    /// wirft EF „another instance with the same key value is already being tracked".
    /// </para>
    /// <para>
    /// <b>Gemessen 2026-09-06, und es war ein erreichbarer Absturz:</b> beim Öffnen eines Spiels
    /// legt der Rasteraufbau fehlende Zellen nach und setzt dabei <c>cell.Game = game</c>. Damit
    /// zieht die nächste Sicherung den ganzen Spielgraphen in die Nachverfolgung - samt beider
    /// Spieler-Instanzen. Das Spiel ließ sich dann nicht mehr öffnen.
    /// </para>
    /// </summary>
    [TestClass]
    public class IdentityResolutionUnitTests
    {
        private Player spieler = null!;
        private Game spiel = null!;

        [TestInitialize]
        public async Task SetUp()
        {
            TestDatabase.ClearDiscardedChanges();

            spieler = new Player
            {
                Id = Guid.NewGuid(),
                Designation = "Anna",
                DisplayName = "Anna",
            };

            using (var ctrl = new PlayersController())
            {
                await ctrl.InsertAsync(spieler);
                await ctrl.SaveChangesAsync();
            }

            spiel = new Game
            {
                Id = Guid.NewGuid(),
                Designation = "Moderator spielt mit",
                Phase = 1,
                SuggestedPhases = 1,
                ModeratorPlayerId = spieler.Id,
            };

            using (var ctrl = new GamesController())
            {
                await ctrl.UpsertAsync(spiel);
                await ctrl.SaveChangesAsync();
            }

            // Derselbe Spieler noch einmal - diesmal als Mitspieler.
            using (var ctrl = new PlayerXGamesController())
            {
                await ctrl.InsertAsync(new PlayerXGame
                {
                    Id = Guid.NewGuid(),
                    GameId = spiel.Id,
                    PlayerId = spieler.Id,
                });

                await ctrl.SaveChangesAsync();
            }
        }

        [TestCleanup]
        public async Task TearDown()
        {
            using (var ctrl = new GamesController())
            {
                await ctrl.DeleteAsync(spiel.Id);
                await ctrl.SaveChangesAsync();
            }

            using (var ctrl = new PlayersController())
            {
                await ctrl.DeleteAsync(spieler.Id);
                await ctrl.SaveChangesAsync();
            }

            TestDatabase.ClearDiscardedChanges();
        }

        /// <summary>
        /// Der Moderator und der gleichnamige Mitspieler sind <b>dasselbe</b> Objekt.
        /// </summary>
        [TestMethod]
        public async Task TheModeratorAndTheParticipantAreTheSameInstance()
        {
            using var ctrl = new GamesController();

            var geladen = await ctrl.GetAsync(spiel.Id);

            Assert.IsNotNull(geladen, "Das Spiel wurde nicht gefunden.");
            Assert.IsNotNull(geladen!.Moderator, "Der Moderator kam nicht mit.");

            var alsMitspieler = geladen.PlayerXGames
                .Select(x => x.Player)
                .FirstOrDefault(p => p != null && p.Id == spieler.Id);

            Assert.IsNotNull(alsMitspieler,
                "Der Mitspieler kam nicht mit - dann misst dieser Test nichts.");

            Assert.IsTrue(ReferenceEquals(geladen.Moderator, alsMitspieler),
                "Derselbe Spieler kam als zwei Objekte zurück. Wird eines davon wieder "
                + "angehängt, wirft EF - und das Spiel lässt sich nicht mehr öffnen.");
        }

        /// <summary>
        /// Zur Verwechslung mit einer Zusicherung: <b>hier stand eine zweite</b>, die den
        /// Graphen an einen Schreibvorgang hängte und „wirft nicht" prüfte. Bei der Gegenprobe
        /// blieb sie grün, weil dieser Weg den Zusammenstoß gar nicht auslöst - sie hätte
        /// Sicherheit vorgetäuscht und ist deshalb entfernt.
        /// <para>
        /// Den Absturz selbst fängt
        /// <c>GameMasterLoadUnitTests.ACompleteGameLoadsWithoutComplaint</c> in
        /// <c>Quizzer.UnitTests</c> - dort läuft der echte Weg über <c>GridBuilder</c>. Mit
        /// ausgebauter Identitätsauflösung wird er rot, mit „another instance with the same key
        /// value is already being tracked".
        /// </para>
        /// </summary>
        [TestMethod]
        public async Task TheLoadedGraphHasOneInstancePerKey()
        {
            using var ctrl = new GamesController();

            var geladen = await ctrl.GetAsync(spiel.Id);

            var spielerImGraphen = new List<Player>();

            if (geladen!.Moderator != null)
                spielerImGraphen.Add(geladen.Moderator);

            spielerImGraphen.AddRange(geladen.PlayerXGames.Select(x => x.Player).Where(p => p != null)!);

            Assert.IsTrue(spielerImGraphen.Count >= 2,
                $"Nur {spielerImGraphen.Count} Spielerverweise im Graphen - dann kann sich "
                + "gar nichts doppeln, und der Test misst nichts.");

            var verschiedeneObjekte = spielerImGraphen.Distinct(ReferenceEqualityComparer.Instance).Count();

            Assert.AreEqual(1, verschiedeneObjekte,
                $"{spielerImGraphen.Count} Verweise auf denselben Spieler ergaben "
                + $"{verschiedeneObjekte} verschiedene Objekte.");
        }
    }
}
