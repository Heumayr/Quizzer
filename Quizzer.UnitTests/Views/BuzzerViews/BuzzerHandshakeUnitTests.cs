using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.Buzzer;
using Quizzer.UnitTests.PlayThrough;
using Quizzer.Views.BuzzerViews;
using Quizzer.Views.StaticRessources;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;

namespace Quizzer.UnitTests.Views.BuzzerViews
{
    /// <summary>
    /// Der Weg, den das Programm wirklich geht: Serverstart ueber das
    /// <see cref="BuzzerServerViewModel"/>, ein Telefon am SignalR-Hub, und das Oeffnen einer
    /// Frage im <see cref="Quizzer.Views.GameViews.CurrentQuestionViewModel"/>.
    /// <para>
    /// Die vorhandenen Buzzer-Tests setzen am Controller an und ueberspringen damit genau die
    /// Naht, an der gemeldete Fehler auftraten - zwischen ViewModel und Hub.
    /// </para>
    /// </summary>
    [TestClass]
    public class BuzzerHandshakeUnitTests
    {
        /// <summary>Fest in <c>StartServerAsync</c> verdrahtet; hier nur zum Verbinden.</summary>
        private const int Port = 5000;

        private TestGameBuilder? world;
        private BuzzerServerViewModel? serverVm;
        private readonly List<HubConnection> phones = new();

        [TestInitialize]
        public void SetUp()
        {
            UserPrompt.Current = new RecordingUserPrompt(answer: true);
            TestEnvironment.ClearSwallowedExceptions();

            if (!PortIsFree(Port))
            {
                Assert.Inconclusive(
                    $"Port {Port} ist belegt - laeuft noch eine Quizzer-Instanz oder ein "
                    + "abgebrochener Testlauf? Der Buzzer-Server bindet fest auf diesen Port.");
            }
        }

        [TestCleanup]
        public async Task TearDown()
        {
            foreach (var phone in phones)
            {
                try { await phone.DisposeAsync(); } catch { }
            }
            phones.Clear();

            if (serverVm != null)
                await ((AsyncRelayCommand)serverVm.StopServerCommand).ExecuteAsync(null);

            StaticManager.Reset();
            serverVm = null;

            if (world != null)
            {
                await world.DisposeAsync();
                world = null;
            }

            UserPrompt.Reset();
        }

        private static bool PortIsFree(int port)
        {
            try
            {
                var listener = new TcpListener(IPAddress.Loopback, port);
                listener.Start();
                listener.Stop();
                return true;
            }
            catch (SocketException)
            {
                return false;
            }
        }

        /// <summary>Legt ein Spiel an und startet den Server so, wie es die Oberflaeche tut.</summary>
        private async Task StartServerAsync(QuestionType questionType, int playerCount = 2)
        {
            world = await TestGameBuilder.CreateAsync(
                questionType: questionType, normalStepCount: 2, playerCount: playerCount);

            serverVm = StaticManager.BuzzerServerViewModel;
            serverVm.Game = world.Game;

            await ((AsyncRelayCommand)serverVm.StartServerCommand).ExecuteAsync(null);
        }

        /// <summary>Verbindet sich wie ein Telefon und sammelt alles, was der Server schickt.</summary>
        private async Task<List<ClientLayoutStateDto>> ConnectPhoneAsync(Player player)
        {
            var received = new List<ClientLayoutStateDto>();

            var connection = new HubConnectionBuilder()
                .WithUrl($"http://127.0.0.1:{Port}/hub?playerid={player.Id}")
                .Build();

            connection.On<ClientLayoutStateDto>("StateChanged", dto =>
            {
                lock (received) received.Add(dto);
            });

            connection.On<ClientLayoutStateDto>("Assigned", dto =>
            {
                lock (received) received.Add(dto);
            });

            await connection.StartAsync();
            phones.Add(connection);

            return received;
        }

        private static async Task<bool> WaitForAsync(Func<bool> condition, int millis = 4000)
        {
            var until = DateTime.UtcNow.AddMilliseconds(millis);

            while (DateTime.UtcNow < until)
            {
                if (condition())
                    return true;

                await Task.Delay(50);
            }

            return condition();
        }

        [TestMethod]
        public async Task StartingTheServer_PutsItOnTheLan()
        {
            await StartServerAsync(QuestionType.Default);

            Assert.IsTrue(serverVm!.IsBuzzerServerRunning, "Der Server ist nicht hochgekommen.");

            var endpoint = serverVm._server?.GetBestListeningIpPort();

            Assert.IsNotNull(endpoint,
                "Ohne LAN-Endpunkt kann das QR-Fenster keine erreichbare Adresse anbieten.");
            Assert.IsFalse(endpoint!.StartsWith("127."),
                $"Der angebotene Endpunkt {endpoint} ist nur oertlich - ein Telefon kaeme nicht hin.");
        }

        [TestMethod]
        public async Task StartingTheServer_ServesThePlayerPage()
        {
            await StartServerAsync(QuestionType.Default);

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var response = await http.GetAsync($"http://127.0.0.1:{Port}/");

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode,
                "Die Buzzer-Seite wird nicht ausgeliefert - fehlt wwwroot im Ausgabeverzeichnis?");

            var body = await response.Content.ReadAsStringAsync();

            Assert.IsTrue(body.Contains("/JS/index.js"),
                "Die ausgelieferte Seite laedt das Startmodul nicht.");
        }

        [TestMethod]
        public async Task APhoneConnects_AndGetsTheCurrentState()
        {
            await StartServerAsync(QuestionType.Default);

            var received = await ConnectPhoneAsync(world!.Players[0]);

            Assert.IsTrue(await WaitForAsync(() =>
            {
                lock (received) return received.Count > 0;
            }), "Nach dem Verbinden kam kein Zustand beim Telefon an.");

            Assert.AreEqual(PlayerConnection.Connected, world.Players[0].ConnectionState,
                "Der Spieler gilt in der Oberflaeche weiterhin als nicht verbunden.");
        }

        /// <summary>
        /// Der Kern: das Oeffnen einer Frage muss das Layout ausspielen. Bleibt das aus, sieht es
        /// am Telefon aus wie ein Verbindungsabbruch - gemeldet fuer Schaetzfrage und
        /// Multiple Choice.
        /// </summary>
        [TestMethod]
        [DataRow(QuestionType.Default, BuzzerControlsLayout.Buzzer)]
        [DataRow(QuestionType.Properties, BuzzerControlsLayout.Buzzer)]
        [DataRow(QuestionType.MultipleChoice, BuzzerControlsLayout.KeySelect)]
        [DataRow(QuestionType.Appreciate, BuzzerControlsLayout.Input)]
        public async Task OpeningAQuestion_SendsItsLayoutToThePhone(
            QuestionType questionType, BuzzerControlsLayout expected)
        {
            await StartServerAsync(questionType);

            var received = await ConnectPhoneAsync(world!.Players[0]);

            var vm = new TestableCurrentQuestionViewModel { Coordinate = world.Coordinate };
            await vm.LoadForTestAsync();

            Assert.AreEqual(expected, vm.CurrentBuzzerLayout,
                "Die Frage bringt gar nicht das erwartete Layout mit.");

            Assert.IsTrue(await WaitForAsync(() =>
            {
                lock (received) return received.Any(s => s.Layout == expected);
            }), $"{expected} ist beim Oeffnen der Frage nie beim Telefon angekommen.");

            ClientLayoutStateDto dto;
            lock (received) dto = received.Last(s => s.Layout == expected);

            Assert.IsFalse(dto.CurrentLayoutLocked, $"{expected} kam gesperrt an.");
            Assert.IsFalse(dto.AllLocked, "Alle Spieler waeren gesperrt.");

            TestEnvironment.ThrowIfAnythingWasSwallowed();
        }

        /// <summary>
        /// Die Schaetzfrage braucht mehr als das blosse Layout: ohne Angaben weiss der Browser
        /// nicht, ob eine Zahl oder ein Datum erwartet wird.
        /// </summary>
        [TestMethod]
        public async Task OpeningAnAppreciateQuestion_CarriesTheInputSettings()
        {
            await StartServerAsync(QuestionType.Appreciate);

            var received = await ConnectPhoneAsync(world!.Players[0]);

            var vm = new TestableCurrentQuestionViewModel { Coordinate = world.Coordinate };
            await vm.LoadForTestAsync();

            Assert.IsTrue(await WaitForAsync(() =>
            {
                lock (received) return received.Any(s => s.Layout == BuzzerControlsLayout.Input);
            }), "Das Eingabe-Layout ist nie beim Telefon angekommen.");

            var state = serverVm!.BuzzerControlsViewModel!.BuzzerController!
                .StateManager.BuzzerInputState;

            Assert.AreEqual("number", state.Infos.InputType,
                "Ein Zahlenwert muss am Telefon als Zahlenfeld ankommen.");
            Assert.AreEqual(vm.Coordinate!.QuestionBaseId, state.Infos.QuestionId,
                "Ohne Fragekennung kann der Browser eine neue Runde nicht von der alten trennen.");
        }
    }
}
