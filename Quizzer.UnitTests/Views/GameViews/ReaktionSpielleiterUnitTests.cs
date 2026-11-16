using LocalBuzzer.Service.Base.States.Minispiele;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.Buzzer;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Questions.Minispiele;
using Quizzer.UnitTests.PlayThrough;
using Quizzer.Views.BuzzerViews;
using Quizzer.Views.GameViews.Sub;
using Quizzer.Views.StaticRessources;
using System.Net;
using System.Net.Sockets;
using System.Web;
using static Quizzer.Views.GameViews.Sub.PlayerResultContext;

namespace Quizzer.UnitTests.Views.GameViews
{
    /// <summary>
    /// Minispiel Reaktion beim Spielleiter (M35 B3), mit echtem Buzzer-Server und Telefonen als
    /// SignalR-Clients: Runde starten, Abgaben, Zeiten am Beamer, Vorschlaege auf der Spielerkarte,
    /// Toene, Wiederholen und Abbrechen (E1) - und die Probetelefone (V1).
    /// </summary>
    [TestClass]
    public partial class ReaktionSpielleiterUnitTests
    {
        private const int Port = 5413;

        private TestGameBuilder? world;
        private BuzzerServerViewModel? serverVm;
        private readonly List<HubConnection> phones = new();
        private readonly List<string> toene = new();
        private RecordingUserPrompt prompt = null!;

        [TestInitialize]
        public void SetUp()
        {
            prompt = new RecordingUserPrompt(answer: true);
            UserPrompt.Current = prompt;
            TestEnvironment.ClearSwallowedExceptions();

            Klang.Aktiv = false;
            Klang.Mitschnitt = name => { lock (toene) toene.Add(name); };

            Assert.IsTrue(PortIsFree(Port), $"Port {Port} ist belegt - er gehoert allein dieser Testklasse.");
        }

        [TestCleanup]
        public async Task TearDown()
        {
            Klang.Mitschnitt = null;

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
                await world.DisposeAsync();

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

        private Task<TestableCurrentQuestionViewModel> OeffneReaktionAsync(int runden)
            => OeffneMinispielAsync(MinigameKind.Reaction, f => f.Rounds = runden);

        /// <summary>Server, zwei verbundene Telefone und das Fragefenster - fuer jede Art im Rahmen (M35 C1).</summary>
        /// <param name="spielerzahl">Raeuber und Waechter braucht drei (M35 C2).</param>
        /// <param name="ausloeser">Wer das Feld aufgedeckt hat, als Index in den Spielern - Duell und Teilen (M35 C2).</param>
        private async Task<TestableCurrentQuestionViewModel> OeffneMinispielAsync(
            MinigameKind art, Action<MinigameQuestion> einstellen, int spielerzahl = 2, int? ausloeser = null)
        {
            world = await TestGameBuilder.CreateAsync(QuestionType.Minigame, normalStepCount: 2, playerCount: spielerzahl,
                adjustQuestion: f =>
                {
                    MinigameProfiles.For(art).ApplyDefaults((MinigameQuestion)f);
                    einstellen((MinigameQuestion)f);
                });

            if (ausloeser is { } index)
                world.Game.CurrentChoosingPlayerId = world.Players[index].Id;

            serverVm = StaticManager.BuzzerServerViewModel;
            serverVm.Game = world.Game;
            serverVm.BuzzerPort = Port;
            await ((AsyncRelayCommand)serverVm.StartServerCommand).ExecuteAsync(null);

            foreach (var spieler in world.Players)
                await VerbindeAsync(spieler);

            var vm = new TestableCurrentQuestionViewModel { Coordinate = world.Coordinate };
            await vm.LoadForTestAsync();
            TestEnvironment.ThrowIfAnythingWasSwallowed();

            return vm;
        }

        private async Task VerbindeAsync(Player player)
        {
            var verbindung = new HubConnectionBuilder()
                .WithUrl($"http://127.0.0.1:{Port}/hub?key={Uri.EscapeDataString(serverVm!._server!.AccessKeys.SchluesselFuer(player.Id))}")
                .Build();

            await verbindung.StartAsync();
            phones.Add(verbindung);

            Assert.IsTrue(await WarteAsync(() => player.ConnectionState == PlayerConnection.Connected),
                $"{player.Designation} ist nicht verbunden.");
        }

        private Task AbgebenAsync(int telefon, object payload, MinigameKind art = MinigameKind.Reaction)
            => phones[telefon].InvokeAsync("SpielAktion", new
            {
                kind = (int)art,
                durchgang = serverVm!.BuzzerControlsViewModel!.MinigameState!.Durchgang,
                payload,
            });

        private static async Task<bool> WarteAsync(Func<bool> bedingung, int millis = 4000)
        {
            var bis = DateTime.UtcNow.AddMilliseconds(millis);

            while (DateTime.UtcNow < bis)
            {
                if (bedingung())
                    return true;

                await Task.Delay(30);
            }

            return bedingung();
        }

        private bool Gespielt(string ton) { lock (toene) return toene.Contains(ton); }

        [TestMethod]
        public async Task TheRoundsReachTheBeamerTheCardsAndTheSpeakers()
        {
            var vm = await OeffneReaktionAsync(runden: 2);
            var anna = world!.Players[0];
            var bert = world.Players[1];
            var karten = vm.PlayersResultViewModel!.PlayerResultContextList;
            PlayerResultContext Karte(Player p) => karten.Single(k => k.Player.Id == p.Id);

            Assert.AreEqual(BuzzerControlsLayout.Minigame, vm.CurrentBuzzerLayout);
            Assert.AreEqual(System.Windows.Visibility.Visible, vm.MinispielSteuerungVisibility, "Die Knoepfe des Minispiels fehlen.");

            // --- Runde 1: Start mit Ton, Abgaben, Zeiten ---
            await vm.StartMinispielRundeAsync();

            Assert.IsTrue(Gespielt("Buzzer"), "Beim Start der Runde kam kein Ton.");
            StringAssert.Contains(vm.Reaktion.Kopfzeile, "Runde 1 von 2");

            await AbgebenAsync(0, new { falseStart = false, milliseconds = 300 });
            await AbgebenAsync(1, new { falseStart = false, milliseconds = 250 });

            Assert.IsTrue(await WarteAsync(() => vm.Reaktion.Kopfzeile.Contains("die Zeiten")),
                "Nach der Runde stehen keine Zeiten am Beamer: " + vm.Reaktion.Kopfzeile);
            Assert.AreEqual(bert.CalculatedDisplayName, vm.Reaktion.Zeilen[0].Name, "Die schnellste Zeit steht nicht oben.");
            Assert.AreEqual("250 ms", vm.Reaktion.Zeilen[0].Zeit);
            Assert.IsTrue(await WarteAsync(() => Gespielt("Ende")), "Zum Ergebnis der Runde kam kein Ton.");
            Assert.AreEqual(ScoreSuggestion.Right, Karte(bert).Suggestion, "Die bisher beste Zeit hat keinen Vorschlag.");
            Assert.AreEqual(ScoreSuggestion.None, Karte(anna).Suggestion);

            // --- Runde 2: Anna schneller, Bert Fruehstart - die beste Einzelzeit gewinnt ---
            await vm.StartMinispielRundeAsync();
            await AbgebenAsync(0, new { falseStart = false, milliseconds = 200 });
            await AbgebenAsync(1, new { falseStart = true });

            Assert.IsTrue(await WarteAsync(() => vm.Reaktion.Kopfzeile.StartsWith("Ergebnis")),
                "Nach der letzten Runde steht kein Ergebnis am Beamer: " + vm.Reaktion.Kopfzeile);

            var oben = vm.Reaktion.Zeilen[0];
            Assert.AreEqual(anna.CalculatedDisplayName, oben.Name);
            Assert.AreEqual("★ Sieger", oben.SiegerText, "Der Sieger ist nicht mit Symbol und Text markiert.");
            Assert.AreEqual(ScoreSuggestion.Right, Karte(anna).Suggestion, "Die beste Einzelzeit hat keinen Vorschlag.");
            Assert.AreEqual(ScoreSuggestion.None, Karte(bert).Suggestion, "Der Zweite behaelt seinen Vorschlag.");
            Assert.AreEqual(world.Coordinate.CurrentPoints, Karte(anna).CurrentScoreManipulation, "Nicht die Feldpunkte.");
            StringAssert.Contains(Karte(bert).ReihenfolgeText, "250 ms");
            Assert.IsTrue(await WarteAsync(() => vm.ShowResultWindowCalls > 0), "Nach dem Ende geht das Bewertungsfenster nicht auf.");
            Assert.IsTrue(serverVm!.BuzzerControlsViewModel!.LivePlayers.Any(p => p.AnswerText == "200 ms"),
                "Die Uebersicht des Spielleiters zeigt die Zeit nicht.");

            // Die Mehrheitswertung haengt an jeder Tastenabgabe - bei der Reaktion darf sie nichts leeren.
            vm.PlayersResultViewModel.WerteMinispiel();
            Assert.AreEqual(ScoreSuggestion.Right, Karte(anna).Suggestion, "Die Mehrheitswertung hat den Vorschlag der Reaktion geloescht.");
            StringAssert.Contains(Karte(bert).ReihenfolgeText, "250 ms");
        }

        [TestMethod]
        public async Task TheGameMasterRepeatsAndAborts()
        {
            var vm = await OeffneReaktionAsync(runden: 3);
            var anna = world!.Players[0];
            var karten = vm.PlayersResultViewModel!.PlayerResultContextList;

            await vm.StartMinispielRundeAsync();
            var ersterDurchgang = serverVm!.BuzzerControlsViewModel!.MinigameState!.Durchgang;

            await vm.WiederholeMinispielRundeAsync();

            Assert.AreNotEqual(ersterDurchgang, serverVm.BuzzerControlsViewModel.MinigameState!.Durchgang,
                "Die Wiederholung hat keinen neuen Durchgang - eine alte Abgabe zaehlte mit.");
            StringAssert.Contains(vm.Reaktion.Kopfzeile, "Runde 1 von 3");

            await AbgebenAsync(0, new { falseStart = false, milliseconds = 300 });
            await AbgebenAsync(1, new { falseStart = false, milliseconds = 400 });
            Assert.IsTrue(await WarteAsync(() => karten.Single(k => k.Player.Id == anna.Id).Suggestion == ScoreSuggestion.Right));

            // Runde 1 noch einmal: ihre Zeiten zaehlen nicht mehr - der Vorschlag geht weg.
            await vm.WiederholeMinispielRundeAsync();
            Assert.AreEqual(ScoreSuggestion.None, karten.Single(k => k.Player.Id == anna.Id).Suggestion,
                "Nach dem Wiederholen bleibt der Vorschlag der verworfenen Runde stehen.");

            // "Runde zuruecksetzen" stellt das Minispiel am Server auf Anfang - die Wertung folgt.
            await AbgebenAsync(0, new { falseStart = false, milliseconds = 300 });
            await AbgebenAsync(1, new { falseStart = false, milliseconds = 400 });
            Assert.IsTrue(await WarteAsync(() => karten.Single(k => k.Player.Id == anna.Id).Suggestion == ScoreSuggestion.Right));

            await serverVm.BuzzerControlsViewModel.ResetRoundAsync(BuzzerControlsLayout.Minigame);

            Assert.AreEqual(ScoreSuggestion.None, karten.Single(k => k.Player.Id == anna.Id).Suggestion,
                "Nach dem Zuruecksetzen zaehlt eine Runde, die der Server vergessen hat.");
            StringAssert.Contains(vm.Reaktion.Kopfzeile, "gleich geht es los");
            Assert.AreEqual(0, serverVm.BuzzerControlsViewModel.MinigameState.Round);

            await vm.BrecheMinispielAbAsync();

            Assert.AreEqual(1, prompt.Confirms.Count, "Der Abbruch fragt nicht nach.");
            Assert.IsTrue(await WarteAsync(() => vm.Reaktion.Kopfzeile.Contains("abgebrochen")),
                "Der Abbruch erreicht den Beamer nicht: " + vm.Reaktion.Kopfzeile);
            Assert.AreEqual(MinigamePhase.Finished, serverVm.BuzzerControlsViewModel.MinigameState.Phase);
        }

        [TestMethod]
        public async Task ProbePhonesOpenOnePerPlayerWithTheDerivedKey()
        {
            await OeffneReaktionAsync(runden: 1);

            var geoeffnet = new List<Uri>();
            serverVm!.OeffneBrowser = geoeffnet.Add;

            serverVm.OpenProbePhones();

            Assert.AreEqual(1, prompt.Confirms.Count, "Verbundene Telefone werden ohne Rueckfrage abgeloest.");
            Assert.AreEqual(world!.Players.Count, geoeffnet.Count, "Nicht je Mitspieler ein Probetelefon.");

            foreach (var spieler in world.Players)
            {
                var url = geoeffnet.Single(u => serverVm._server!.AccessKeys
                    .FindeSchluessel(HttpUtility.ParseQueryString(u.Query)["key"], world.Players, p => p.Id)?.Id == spieler.Id);

                Assert.IsFalse(url.ToString().Contains(spieler.Id.ToString("N"), StringComparison.OrdinalIgnoreCase)
                               || url.ToString().Contains(spieler.Id.ToString(), StringComparison.OrdinalIgnoreCase),
                    "Die rohe Spieler-Id steht in der Adresse des Probetelefons.");
                Assert.AreEqual(Port, url.Port);
            }
        }
    }
}
