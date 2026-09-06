using LocalBuzzer.Service;
using LocalBuzzer.Service.Base;
using LocalBuzzer.Service.Base.States;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.Buzzer;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Quizzer.LogicUnitTests.LocalBuzzer
{
    /// <summary>
    /// Der Buzzer-Server mit einem echten SignalR-Client - so, wie ein Telefon sich verbindet.
    /// <para>
    /// Bis hierher endeten die Buzzer-Tests am Zustandsobjekt. Damit war nicht geprueft, was
    /// zwischen Hub und Browser tatsaechlich passiert: ob eine Verbindung zustande kommt, ob der
    /// Spieler zugeordnet wird und ob das gewaehlte Layout beim Client ankommt.
    /// </para>
    /// </summary>
    [TestClass]
    public class BuzzerEndToEndUnitTests
    {
        /// <summary>Eigener Port, damit ein laufendes Spielfenster auf 5000 nicht stoert.</summary>
        private const int TestPort = 5399;

        private BuzzerServer server = null!;
        private Game game = null!;
        private Player anna = null!;
        private Player bert = null!;

        [TestInitialize]
        public async Task StartServer()
        {
            anna = new Player { Id = Guid.NewGuid(), Designation = "Anna", DisplayName = "Anna" };
            bert = new Player { Id = Guid.NewGuid(), Designation = "Bert", DisplayName = "Bert" };

            game = new Game { Id = Guid.NewGuid(), Designation = "Testspiel" };

            foreach (var player in new[] { anna, bert })
            {
                game.PlayerXGames.Add(new PlayerXGame
                {
                    Id = Guid.NewGuid(),
                    GameId = game.Id,
                    PlayerId = player.Id,
                    Player = player,
                });
            }

            server = new BuzzerServer { GetGame = () => game };

            await server.StartAsync(new BuzzerServerOptions
            {
                Port = TestPort,
                BindAddress = "127.0.0.1",
            });

            Assert.AreEqual(ServerState.Running, server.ServerState,
                "Der Buzzer-Server ist nicht hochgekommen.");
        }

        [TestCleanup]
        public async Task StopServer()
        {
            if (server != null)
                await server.DisposeAsync();
        }

        /// <summary>Verbindet sich wie ein Telefon: mit playerid in der Abfrage.</summary>
        private static async Task<(HubConnection Connection, List<ClientLayoutStateDto> States)>
            ConnectAsync(Player player)
        {
            var states = new List<ClientLayoutStateDto>();

            var connection = new HubConnectionBuilder()
                .WithUrl($"http://127.0.0.1:{TestPort}/hub?playerid={player.Id}")
                .Build();

            connection.On<ClientLayoutStateDto>("StateChanged", dto =>
            {
                lock (states) states.Add(dto);
            });

            await connection.StartAsync();

            return (connection, states);
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
        public async Task APhoneCanConnect()
        {
            var (connection, _) = await ConnectAsync(anna);

            try
            {
                Assert.AreEqual(HubConnectionState.Connected, connection.State);
            }
            finally
            {
                await connection.DisposeAsync();
            }
        }

        [TestMethod]
        public async Task TheBuzzerLayoutReachesThePhone()
        {
            var (connection, states) = await ConnectAsync(anna);

            try
            {
                await server.BuzzerController!.ResetRoundAsync(1, BuzzerControlsLayout.Buzzer);

                Assert.IsTrue(await WaitForAsync(() =>
                {
                    lock (states) return states.Any(s => s.Layout == BuzzerControlsLayout.Buzzer);
                }), "Das Buzzer-Layout ist nie beim Client angekommen.");
            }
            finally
            {
                await connection.DisposeAsync();
            }
        }

        /// <summary>
        /// Der Fall, den der Nutzer gemeldet hat: bei der Schaetzfrage kommt am Telefon nichts an.
        /// </summary>
        [TestMethod]
        public async Task TheInputLayoutReachesThePhone()
        {
            var (connection, states) = await ConnectAsync(anna);

            try
            {
                server.BuzzerController!.StateManager.BuzzerInputState.Infos = new()
                {
                    InputType = "number",
                    Placeholder = "Wert in m",
                };

                await server.BuzzerController.ResetRoundAsync(1, BuzzerControlsLayout.Input);

                Assert.IsTrue(await WaitForAsync(() =>
                {
                    lock (states) return states.Any(s => s.Layout == BuzzerControlsLayout.Input);
                }), "Das Eingabe-Layout ist nie beim Client angekommen - "
                  + "am Telefon bliebe der Bildschirm leer.");

                ClientLayoutStateDto dto;
                lock (states) dto = states.Last(s => s.Layout == BuzzerControlsLayout.Input);

                Assert.IsFalse(dto.CurrentLayoutLocked,
                    "Das Eingabefeld waere gesperrt - der Spieler koennte nichts eintippen.");
                Assert.IsNotNull(dto.LayoutInfo,
                    "Ohne LayoutInfo weiss der Browser nicht, ob Zahl oder Datum gefragt ist.");
            }
            finally
            {
                await connection.DisposeAsync();
            }
        }

        [TestMethod]
        public async Task ASubmittedValueArrivesAtTheServer()
        {
            var (connection, _) = await ConnectAsync(anna);

            try
            {
                ConcurrentDictionary<Guid, BuzzerInputState.InputResult>? received = null;
                server.BuzzerController!.EventBus.AllPlayersSubmittedInput += dic => received = dic;

                await server.BuzzerController.ResetRoundAsync(1, BuzzerControlsLayout.Input);

                await connection.InvokeAsync("SubmitInput", new
                {
                    playerId = anna.Id,
                    value = "3798",
                });

                Assert.IsTrue(await WaitForAsync(() =>
                    server.BuzzerController.StateManager.BuzzerInputState.InputsForPlayer.Count > 0),
                    "Der eingetippte Wert ist nie am Server angekommen.");

                var stored = server.BuzzerController.StateManager
                    .BuzzerInputState.InputsForPlayer[anna.Id];

                Assert.AreEqual("3798", stored.Value);
                Assert.IsTrue(stored.CommittedResult);

                // Nur ein Spieler von zweien hat abgegeben - die Runde bleibt offen.
                Assert.IsNull(received, "Die Runde darf erst schliessen, wenn alle abgegeben haben.");
            }
            finally
            {
                await connection.DisposeAsync();
            }
        }

        [TestMethod]
        public async Task WhenEveryoneHasSubmittedTheRoundCloses()
        {
            var (connA, _) = await ConnectAsync(anna);
            var (connB, _) = await ConnectAsync(bert);

            try
            {
                ConcurrentDictionary<Guid, BuzzerInputState.InputResult>? received = null;
                server.BuzzerController!.EventBus.AllPlayersSubmittedInput += dic => received = dic;

                await server.BuzzerController.ResetRoundAsync(1, BuzzerControlsLayout.Input);

                await connA.InvokeAsync("SubmitInput", new { playerId = anna.Id, value = "3798" });
                await connB.InvokeAsync("SubmitInput", new { playerId = bert.Id, value = "3500" });

                Assert.IsTrue(await WaitForAsync(() => received != null),
                    "Nach der letzten Abgabe muss die Runde schliessen.");

                Assert.AreEqual(2, received!.Count);
            }
            finally
            {
                await connA.DisposeAsync();
                await connB.DisposeAsync();
            }
        }

        /// <summary>
        /// <b>Der Notausgang: eine Runde schliessen, obwohl ein Telefon fehlt.</b>
        /// <para>
        /// <b>Gemessen 2026-09-07.</b> Eine Runde schliesst sonst nur, wenn <b>restlos jeder</b>
        /// Mitspieler abgegeben hat. Ein leerer Akku, ein iPhone mit gesperrtem Bildschirm oder
        /// jemand ganz ohne Telefon genuegt, und die Schaetzfrage bekommt nie ihre Auswertung -
        /// kein "am naechsten dran"-Vorschlag, keine Bewertung, und die uebrigen koennen ihren
        /// Tipp weiter aendern, nachdem er vorgelesen wurde.
        /// </para>
        /// <para>
        /// <c>LockAllAsync</c> gab es dafuer schon - es hatte im ganzen Programm <b>keinen
        /// einzigen Aufrufer</b> und loeste ausserdem die Sammelereignisse nicht aus, an denen
        /// die Auswertung haengt.
        /// </para>
        /// </summary>
        [TestMethod]
        public async Task TheRoundCanBeClosedWhileAPhoneIsMissing()
        {
            var (connA, _) = await ConnectAsync(anna);

            try
            {
                ConcurrentDictionary<Guid, BuzzerInputState.InputResult>? received = null;
                server.BuzzerController!.EventBus.AllPlayersSubmittedInput += dic => received = dic;

                await server.BuzzerController.ResetRoundAsync(1, BuzzerControlsLayout.Input);

                // Bert hat kein Telefon in der Hand - nur Anna gibt ab.
                await connA.InvokeAsync("SubmitInput", new { playerId = anna.Id, value = "3798" });

                Assert.IsTrue(await WaitForAsync(() =>
                    server.BuzzerController.StateManager.BuzzerInputState.InputsForPlayer.Count == 1),
                    "Annas Abgabe ist gar nicht angekommen.");

                Assert.IsNull(received,
                    "Die Runde hat von allein geschlossen, obwohl eine Abgabe fehlt - dann misst "
                    + "dieser Test den Notausgang nicht.");

                await server.BuzzerController.LockAllAsync();

                Assert.IsTrue(await WaitForAsync(() => received != null),
                    "Die Runde liess sich nicht von Hand schliessen - der Abend haengt, sobald "
                    + "ein Telefon ausfaellt.");

                Assert.AreEqual(1, received!.Count,
                    "Die Auswertung bekam nicht die vorhandene Abgabe.");

                Assert.AreEqual("3798", received.Values.First().Value,
                    "Die Auswertung bekam einen anderen Wert als abgegeben.");
            }
            finally
            {
                await connA.DisposeAsync();
            }
        }

        /// <summary>
        /// Der Kern des gemeldeten Fehlers, hier als Regel festgehalten: Angaben zu setzen
        /// reicht nicht. Solange niemand die Runde ausspielt, bleibt CurrentLayout auf None,
        /// CurrentState null und jeder Spieler gesperrt - am Telefon sieht das aus, als
        /// bestuende keine Verbindung.
        /// </summary>
        [TestMethod]
        public async Task SettingTheInfosAloneReachesNobody()
        {
            var (connection, states) = await ConnectAsync(anna);

            try
            {
                server.BuzzerController!.StateManager.BuzzerInputState.Infos = new()
                {
                    InputType = "number",
                    Placeholder = "Wert in m",
                };

                await Task.Delay(300);

                lock (states)
                {
                    Assert.IsFalse(states.Any(s => s.Layout == BuzzerControlsLayout.Input),
                        "Ohne ResetRoundAsync darf nichts ankommen - genau das war der Fehler.");
                }

                Assert.AreEqual(BuzzerControlsLayout.None,
                    server.BuzzerController.StateManager.CurrentLayout);
                Assert.IsNull(server.BuzzerController.StateManager.CurrentState,
                    "Ohne ausgespielte Runde gibt es keinen aktiven Zustand.");
            }
            finally
            {
                await connection.DisposeAsync();
            }
        }

        [TestMethod]
        [DataRow(BuzzerControlsLayout.Buzzer)]
        [DataRow(BuzzerControlsLayout.KeySelect)]
        [DataRow(BuzzerControlsLayout.Input)]
        public async Task EveryLayoutUnlocksThePlayersWhenTheRoundIsPlayedOut(
            BuzzerControlsLayout layout)
        {
            var (connection, states) = await ConnectAsync(anna);

            try
            {
                await server.BuzzerController!.ResetRoundAsync(1, layout);

                Assert.IsTrue(await WaitForAsync(() =>
                {
                    lock (states) return states.Any(s => s.Layout == layout);
                }), $"{layout} ist nie beim Client angekommen.");

                ClientLayoutStateDto dto;
                lock (states) dto = states.Last(s => s.Layout == layout);

                Assert.IsFalse(dto.CurrentLayoutLocked, $"{layout} kam gesperrt an.");
                Assert.IsFalse(dto.AllLocked);
            }
            finally
            {
                await connection.DisposeAsync();
            }
        }

        [TestMethod]
        public async Task TheKeySelectLayoutReachesThePhone()
        {
            var (connection, states) = await ConnectAsync(anna);

            try
            {
                server.BuzzerController!.StateManager.BuzzerKeySelector.Infos = new()
                {
                    KeysAndDesignations = new() { ["A"] = "Wien", ["B"] = "Graz" },
                    MaxAllowedSelections = 1,
                };

                await server.BuzzerController.ResetRoundAsync(1, BuzzerControlsLayout.KeySelect);

                Assert.IsTrue(await WaitForAsync(() =>
                {
                    lock (states) return states.Any(s => s.Layout == BuzzerControlsLayout.KeySelect);
                }), "Das Tastenwahl-Layout ist nie beim Client angekommen.");
            }
            finally
            {
                await connection.DisposeAsync();
            }
        }

        /// <summary>
        /// Was der Spielleiter im Editor einstellt, muss auch am Telefon gelten. Bis 2026-09-05
        /// schickte der Hub statt der eingestellten Anzahl ein zweites, nie gesetztes Feld mit dem
        /// Wert 1 - eine Frage mit zwei richtigen Antworten liess sich am Telefon nicht abgeben.
        /// <para>
        /// Geprueft wird am empfangenen Zustand, nicht am Serverobjekt: die Feldnamen zwischen C#
        /// und JS haengen an Zeichenketten, und die Browser-Seite faellt bei jedem fehlenden Feld
        /// still auf einen Standardwert zurueck.
        /// </para>
        /// </summary>
        [TestMethod]
        public async Task TheKeySelectLayoutCarriesTheEditorSettings()
        {
            var (connection, states) = await ConnectAsync(anna);

            try
            {
                var frageId = Guid.NewGuid();

                server.BuzzerController!.StateManager.BuzzerKeySelector.Infos = new()
                {
                    KeysAndDesignations = new() { ["A"] = "Wien", ["B"] = "Graz" },
                    MaxAllowedSelections = 2,
                    ShowDesignations = false,
                    QuestionId = frageId,
                };

                await server.BuzzerController.ResetRoundAsync(1, BuzzerControlsLayout.KeySelect);

                Assert.IsTrue(await WaitForAsync(() =>
                {
                    lock (states) return states.Any(s => s.Layout == BuzzerControlsLayout.KeySelect);
                }), "Das Tastenwahl-Layout ist nie beim Client angekommen.");

                ClientLayoutStateDto dto;
                lock (states) dto = states.Last(s => s.Layout == BuzzerControlsLayout.KeySelect);

                Assert.IsNotNull(dto.LayoutInfo, "Ohne LayoutInfo weiss der Browser gar nichts.");

                var info = (JsonElement)dto.LayoutInfo!;

                Assert.AreEqual(2, info.GetProperty("maxAllowedSelections").GetInt32(),
                    "Am Telefon gilt eine andere Anzahl als der Spielleiter eingestellt hat.");

                Assert.IsFalse(info.GetProperty("showDesignations").GetBoolean(),
                    "Der Schalter 'Text auf Tasten anzeigen' erreicht das Telefon nicht.");

                Assert.AreEqual(frageId, info.GetProperty("questionId").GetGuid(),
                    "Ohne Fragekennung kann der Browser eine neue Runde nicht von der alten trennen.");
            }
            finally
            {
                await connection.DisposeAsync();
            }
        }

        /// <summary>
        /// Die andere Haelfte derselben Einstellung: der Server muss zwei Tasten auch annehmen.
        /// Frueher pruefte er gegen dasselbe tote Feld und leerte die Abgabe - die Runde konnte
        /// dann nie schliessen.
        /// </summary>
        [TestMethod]
        public async Task TwoKeysAreAcceptedWhenTwoAreAllowed()
        {
            var (connection, _) = await ConnectAsync(anna);

            try
            {
                server.BuzzerController!.StateManager.BuzzerKeySelector.Infos = new()
                {
                    KeysAndDesignations = new() { ["A"] = "Wien", ["B"] = "Graz", ["C"] = "Linz" },
                    MaxAllowedSelections = 2,
                };

                await server.BuzzerController.ResetRoundAsync(1, BuzzerControlsLayout.KeySelect);

                await connection.InvokeAsync("SelectionResults", new
                {
                    playerId = anna.Id,
                    selectedKeys = new[] { "A", "B" },
                    committedResult = true,
                });

                var keySelector = server.BuzzerController.StateManager.BuzzerKeySelector;

                Assert.IsTrue(await WaitForAsync(() => keySelector.KeyResultsForPlayer.Count > 0),
                    "Die Auswahl ist nie am Server angekommen.");

                var gespeichert = keySelector.KeyResultsForPlayer[anna.Id];

                CollectionAssert.AreEquivalent(new[] { "A", "B" }, gespeichert.SelectedKeys,
                    "Der Server hat die zweite Taste verworfen.");

                Assert.IsTrue(gespeichert.CommittedResult,
                    "Die Abgabe gilt nicht als abgegeben - die Runde koennte nie schliessen.");
            }
            finally
            {
                await connection.DisposeAsync();
            }
        }
    }
}
