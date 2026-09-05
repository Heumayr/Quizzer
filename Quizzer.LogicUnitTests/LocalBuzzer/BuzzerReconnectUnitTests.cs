using LocalBuzzer.Service;
using LocalBuzzer.Service.Base;
using LocalBuzzer.Service.Hubs;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.Buzzer;

namespace Quizzer.LogicUnitTests.LocalBuzzer
{
    /// <summary>
    /// Was geschieht, wenn dasselbe Telefon ein zweites Mal anklopft. Bis 2026-09-05 wies der Hub
    /// es mit <c>HubException</c> ab, solange der Spieler noch als verbunden galt - und weil ein
    /// abgerissener WebSocket erst nach dem Server-Zeitablauf auffaellt, traf das genau das eigene
    /// Geraet nach Bildschirmsperre oder WLAN-Wechsel. Jetzt gewinnt die neue Verbindung.
    /// </summary>
    [TestClass]
    public class BuzzerReconnectUnitTests
    {
        /// <summary>Eigener Port, damit ein laufendes Spielfenster auf 5000 nicht stoert.</summary>
        private const int TestPort = 5401;

        private BuzzerServer server = null!;
        private Game game = null!;
        private Player anna = null!;

        [TestInitialize]
        public async Task StartServer()
        {
            anna = new Player { Id = Guid.NewGuid(), Designation = "Anna", DisplayName = "Anna" };

            game = new Game { Id = Guid.NewGuid(), Designation = "Testspiel" };
            game.PlayerXGames.Add(new PlayerXGame
            {
                Id = Guid.NewGuid(),
                GameId = game.Id,
                PlayerId = anna.Id,
                Player = anna,
            });

            server = new BuzzerServer { GetGame = () => game };

            await server.StartAsync(new BuzzerServerOptions
            {
                Port = TestPort,
                BindAddress = "127.0.0.1",
            });
        }

        [TestCleanup]
        public async Task StopServer()
        {
            if (server != null)
                await server.DisposeAsync();
        }

        /// <summary>
        /// Ein Telefon am Hub. Schreibt mit, was der Server schickt und warum er zumacht.
        /// <para>
        /// <c>StartAsync</c> allein beweist nichts: ein Fehler aus <c>OnConnectedAsync</c> kommt
        /// erst mit dem Schliessen der Verbindung, also nach einem gelungenen Verbinden.
        /// Deshalb halten diese Proben den Verlauf fest statt eines Augenblicks.
        /// </para>
        /// </summary>
        private sealed class TestPhone : IAsyncDisposable
        {
            public required HubConnection Connection { get; init; }

            public List<string> Replaced { get; } = new();

            public List<ClientLayoutStateDto> Assigned { get; } = new();

            public Exception? CloseError { get; set; }

            public bool Closed { get; set; }

            public ValueTask DisposeAsync() => Connection.DisposeAsync();
        }

        private static async Task<TestPhone> ConnectAsync(Player player)
        {
            var connection = new HubConnectionBuilder()
                .WithUrl($"http://127.0.0.1:{TestPort}/hub?playerid={player.Id}")
                .Build();

            var phone = new TestPhone { Connection = connection };

            connection.On<string>("Replaced", text =>
            {
                lock (phone.Replaced) phone.Replaced.Add(text);
            });

            connection.On<ClientLayoutStateDto>("Assigned", dto =>
            {
                lock (phone.Assigned) phone.Assigned.Add(dto);
            });

            connection.Closed += ex =>
            {
                phone.CloseError = ex;
                phone.Closed = true;
                return Task.CompletedTask;
            };

            await connection.StartAsync();

            return phone;
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

        /// <summary>
        /// Der gemeldete Fall: das Telefon meldet sich neu an, waehrend der Server die alte
        /// Verbindung noch fuer lebendig haelt. Frueher scheiterte das mit "bereits verbunden".
        /// </summary>
        [TestMethod]
        public async Task ASecondConnectionOfTheSamePlayerIsAccepted()
        {
            await using var erstes = await ConnectAsync(anna);
            await using var zweites = await ConnectAsync(anna);

            // Die Anmeldung ist erst gelungen, wenn "Assigned" da ist. Ein blosses
            // "Connection.State == Connected" gleich nach dem Verbinden ist auch dann noch wahr,
            // wenn der Hub die Verbindung im naechsten Augenblick wegen eines Fehlers zumacht.
            Assert.IsTrue(await WaitForAsync(() =>
            {
                lock (zweites.Assigned) return zweites.Assigned.Count > 0;
            }), "Das wiederverbindende Telefon bekam keine Zuordnung - genau der gemeldete Fall.");

            await Task.Delay(300);

            Assert.IsFalse(zweites.Closed,
                $"Der Hub hat die neue Verbindung wieder geschlossen: {zweites.CloseError?.Message}");

            Assert.AreEqual(HubConnectionState.Connected, zweites.Connection.State);
            Assert.AreEqual(PlayerConnection.Connected, anna.ConnectionState);
        }

        /// <summary>Das abgeloeste Geraet erfaehrt, warum es still wird.</summary>
        [TestMethod]
        public async Task TheReplacedPhoneIsTold()
        {
            await using var erstes = await ConnectAsync(anna);
            await using var zweites = await ConnectAsync(anna);

            Assert.IsTrue(await WaitForAsync(() =>
            {
                lock (erstes.Replaced) return erstes.Replaced.Count > 0;
            }), "Das abgeloeste Telefon bekam keine Meldung und bliebe stumm stehen.");

            string text;
            lock (erstes.Replaced) text = erstes.Replaced[0];

            Assert.AreEqual(BuzzerHub.ReplacedMessage, text);
        }

        /// <summary>
        /// Die Haelfte, die leicht vergessen wird: die abgeloeste Verbindung endet spaeter und
        /// darf den Spieler dabei nicht als getrennt melden - sonst zeigt die Spielerliste
        /// "nicht verbunden", obwohl das neue Telefon dranhaengt.
        /// </summary>
        [TestMethod]
        public async Task ClosingTheReplacedConnectionLeavesThePlayerConnected()
        {
            var erstes = await ConnectAsync(anna);
            var zweites = await ConnectAsync(anna);

            try
            {
                Assert.IsTrue(await WaitForAsync(() =>
                {
                    lock (zweites.Assigned) return zweites.Assigned.Count > 0;
                }), "Die neue Verbindung kam nicht zustande - dann sagt dieser Test nichts aus.");

                await erstes.DisposeAsync();

                // Der Abmeldeweg des Hubs laeuft asynchron; kurz Zeit geben und danach messen.
                await Task.Delay(300);

                Assert.AreEqual(PlayerConnection.Connected, anna.ConnectionState,
                    "Die abgeloeste Verbindung hat den Spieler auf getrennt gesetzt.");
            }
            finally
            {
                await zweites.DisposeAsync();
            }

            Assert.IsTrue(await WaitForAsync(() => anna.ConnectionState == PlayerConnection.Disconnected),
                "Nach dem Ende der aktuellen Verbindung muss der Spieler als getrennt gelten.");
        }

        /// <summary>
        /// Ein QR-Code aus einem anderen Spiel muss sagen, was zu tun ist.
        /// <para>
        /// Gemessen am 2026-09-05: <c>StartAsync</c> gelingt, der Fehler aus
        /// <c>OnConnectedAsync</c> kommt erst mit dem Schliessen der Verbindung. Am Telefon
        /// landet er deshalb im <c>onclose</c>-Zweig und nicht im Fangblock des Verbindens -
        /// genau dort muss die Seite also einen Ausweg anbieten.
        /// </para>
        /// </summary>
        [TestMethod]
        public async Task AForeignPlayerIsRejectedWithAGermanHint()
        {
            var connection = new HubConnectionBuilder()
                .WithUrl($"http://127.0.0.1:{TestPort}/hub?playerid={Guid.NewGuid()}")
                .Build();

            Exception? closeError = null;
            var closed = new TaskCompletionSource();

            connection.Closed += ex =>
            {
                closeError = ex;
                closed.TrySetResult();
                return Task.CompletedTask;
            };

            try
            {
                await connection.StartAsync();

                var beendet = await Task.WhenAny(closed.Task, Task.Delay(4000));

                Assert.AreSame(closed.Task, beendet,
                    "Der Hub hat die Verbindung eines fremden Spielers gar nicht beendet.");

                Assert.IsNotNull(closeError, "Die Verbindung endete ohne Begruendung.");

                StringAssert.Contains(closeError!.Message, "QR-Code",
                    "Die Meldung nennt dem Spieler nicht, was er tun soll.");
            }
            finally
            {
                await connection.DisposeAsync();
            }
        }
        /// <summary>
        /// Der Abendfall: das Telefon faellt mitten in einer laufenden Runde weg - Bildschirm
        /// gesperrt, WLAN gewechselt - und kommt zurueck. Es muss die laufende Runde
        /// wiederbekommen, nicht eine leere Anzeige.
        /// <para>
        /// Gemessen 2026-09-06. Bis dahin war nur geprueft, <b>dass</b> die neue Verbindung
        /// angenommen wird - nicht, <b>was</b> sie zurueckbekommt. Ein Gast vor einem leeren
        /// Telefon merkt den Unterschied sofort, der Spielleiter erst, wenn niemand buzzert.
        /// </para>
        /// </summary>
        [TestMethod]
        public async Task AReconnectingPhoneGetsTheRunningRoundBack()
        {
            await using var erstes = await ConnectAsync(anna);

            server.BuzzerController!.StateManager.BuzzerInputState.Infos = new()
            {
                InputType = "number",
                Placeholder = "Wert in m",
                QuestionId = Guid.NewGuid(),
            };

            await server.BuzzerController.ResetRoundAsync(7, BuzzerControlsLayout.Input);

            // Jetzt faellt das Telefon weg und meldet sich neu an.
            await using var zweites = await ConnectAsync(anna);

            Assert.IsTrue(await WaitForAsync(() =>
            {
                lock (zweites.Assigned) return zweites.Assigned.Count > 0;
            }), "Das wiederverbindende Telefon bekam keine Zuordnung.");

            ClientLayoutStateDto stand;
            lock (zweites.Assigned) stand = zweites.Assigned[0];

            Assert.AreEqual(BuzzerControlsLayout.Input, stand.Layout,
                "Das Telefon bekam nicht das laufende Layout zurueck - der Gast saehe eine "
                + "leere Anzeige, bis der Spielleiter weiterschaltet.");

            Assert.AreEqual(7, stand.Round,
                "Die Rundennummer stimmt nicht - eine Abgabe wuerde der falschen Runde "
                + "zugeordnet.");

            Assert.IsFalse(stand.CurrentLayoutLocked,
                "Die laufende Runde kam als gesperrt an - das Telefon liesse keine Eingabe zu.");

            Assert.IsNotNull(stand.LayoutInfo,
                "Ohne LayoutInfo weiss das Telefon nicht, welche Eingabeart es zeigen soll.");

            Assert.AreEqual(anna.CalculatedDisplayName, stand.PlayerName,
                "Das Telefon zeigt den falschen Spieler an.");
        }
        /// <summary>
        /// Die Gegenrichtung zur vorigen Probe: ist die Runde schon geschlossen, muss das
        /// wiederverbundene Telefon sie als gesperrt zurueckbekommen.
        /// <para>
        /// Ohne diese Zusicherung waere die vorige auch dann gruen, wenn
        /// <c>CurrentLayoutLocked</c> fest auf "offen" stuende - und der Gast tippte in ein
        /// Feld, dessen Eingabe der Server verwirft.
        /// </para>
        /// </summary>
        [TestMethod]
        public async Task AReconnectAfterTheRoundClosedComesBackLocked()
        {
            var erstes = await ConnectAsync(anna);

            try
            {
                server.BuzzerController!.StateManager.BuzzerInputState.Infos = new()
                {
                    InputType = "number",
                    Placeholder = "Wert in m",
                    QuestionId = Guid.NewGuid(),
                };

                await server.BuzzerController.ResetRoundAsync(7, BuzzerControlsLayout.Input);

                // Anna ist die einzige Mitspielerin - mit ihrer Abgabe schliesst die Runde.
                await erstes.Connection.InvokeAsync("SubmitInput", new
                {
                    playerId = anna.Id,
                    value = "3798",
                });

                Assert.IsTrue(await WaitForAsync(() =>
                    server.BuzzerController.StateManager.BuzzerInputState.Locked),
                    "Die Runde hat nach der letzten Abgabe nicht geschlossen - dann misst "
                    + "dieser Test nichts.");
            }
            finally
            {
                await erstes.DisposeAsync();
            }

            await using var zweites = await ConnectAsync(anna);

            Assert.IsTrue(await WaitForAsync(() =>
            {
                lock (zweites.Assigned) return zweites.Assigned.Count > 0;
            }), "Das wiederverbindende Telefon bekam keine Zuordnung.");

            ClientLayoutStateDto stand;
            lock (zweites.Assigned) stand = zweites.Assigned[0];

            Assert.IsTrue(stand.CurrentLayoutLocked,
                "Die geschlossene Runde kam als offen an - der Gast tippt in ein Feld, dessen "
                + "Eingabe der Server verwirft.");
        }
    }
}