using LocalBuzzer.Service.Base.States.Minispiele;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Questions;
using Quizzer.DataModels.Questions.Sonderregeln;
using Quizzer.Logic.Spielablauf;
using Quizzer.UnitTests.PlayThrough;
using Quizzer.UnitTests.Views.BuzzerViews;
using Quizzer.Views.BuzzerViews;
using Quizzer.Views.GameViews.Sub;
using Quizzer.Views.StaticRessources;
using System.Net;
using System.Net.Sockets;
using System.Windows;
using static Quizzer.Views.GameViews.Sub.PlayerResultContext;

namespace Quizzer.UnitTests.Views.GameViews
{
    /// <summary>
    /// Sonderregeln beim Spielleiter (M35 C3), mit echtem Buzzer-Server und Telefonen als SignalR-Clients:
    /// erst die Vorphase im Minispiel-Rahmen, dann die gewoehnliche Frage, dann rechnet die Regel die
    /// Bewertung auf der Karte um (E1) - gekappt (E2) und mit der Ruecknahme vollstaendig zurueckzudrehen.
    /// </summary>
    [TestClass]
    public class SonderregelSpielleiterUnitTests : BuzzerServerTestBasis
    {
        protected override int Port => 5417;

        private TestGameBuilder? world;
        private BuzzerServerViewModel? serverVm;
        private readonly List<HubConnection> phones = new();

        [TestInitialize]
        public void SetUp()
        {
            UserPrompt.Current = new RecordingUserPrompt(answer: true);
            TestEnvironment.ClearSwallowedExceptions();
            Klang.Aktiv = false;

            Assert.IsTrue(PortIsFree(Port), $"Port {Port} ist belegt - er gehoert allein dieser Testklasse.");
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

        private async Task<TestableCurrentQuestionViewModel> OeffneAsync(CellSpecialRule regel, QuestionType typ = QuestionType.Default)
        {
            world = await TestGameBuilder.CreateAsync(typ, normalStepCount: 3, playerCount: 2);
            world.Coordinate.SpecialRule = regel;

            serverVm = StaticManager.BuzzerServerViewModel;
            serverVm.Game = world.Game;
            await StarteServerAsync(serverVm);

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

        private MinigameState Zustand => serverVm!.BuzzerControlsViewModel!.MinigameState!;

        private Task AbgebenAsync(int telefon, CellSpecialRule regel, object payload)
            => phones[telefon].InvokeAsync("SpielAktion", new
            {
                kind = 0,
                specialRule = (int)regel,
                durchgang = Zustand.Durchgang,
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

        private PlayerResultContext Karte(TestableCurrentQuestionViewModel vm, int spieler)
            => vm.PlayersResultViewModel!.PlayerResultContextList.Single(k => k.Player.Id == world!.Players[spieler].Id);

        [TestMethod]
        public async Task TheStakePhaseComesFirstAndTheStakeReplacesTheFieldPoints()
        {
            var vm = await OeffneAsync(CellSpecialRule.Stake);
            var feld = world!.Coordinate.CurrentPoints;
            var minus = world.Coordinate.CurrentMinusPoints;
            var frageLayout = vm.Question!.BuzzerControlsLayout;

            // --- Vorphase: Rahmen am Telefon, Beamer verdeckt die Frage ---
            Assert.AreEqual(BuzzerControlsLayout.Minigame, vm.CurrentBuzzerLayout, "Die Vorphase laeuft nicht auf den Telefonen.");
            Assert.AreEqual(MinigamePhase.Running, Zustand.Phase, "Die Vorphase ist nicht gestartet.");
            Assert.AreEqual(CellSpecialRule.Stake, Zustand.Setup.SpecialRule);
            Assert.AreEqual(Visibility.Visible, vm.Sonderregel.BeamerVisibility, "Der Beamer zeigt die Sonderregel nicht.");
            Assert.AreEqual("Sonderregel: Einsatz", vm.Sonderregel.Titel);
            Assert.AreEqual(Visibility.Collapsed, vm.MinispielSteuerungVisibility, "In der Vorphase stehen die Minispiel-Knoepfe.");

            await AbgebenAsync(0, CellSpecialRule.Stake, new { amount = feld + 1 });
            await AbgebenAsync(0, CellSpecialRule.Stake, new { amount = 40 });
            Assert.IsTrue(await WarteAsync(() => vm.Sonderregel.Stand.Contains("1 von 2")), "Der Stand zaehlt nicht: " + vm.Sonderregel.Stand);
            Assert.IsTrue(vm.VorphaseLaeuft, "Die Vorphase schloss, obwohl ein Telefon fehlt.");

            // --- Der Spielleiter schliesst ohne Bert (E1) - dann kommt die Frage ---
            await vm.SchliesseVorphaseAsync();
            Assert.IsTrue(await WarteAsync(() => !vm.VorphaseLaeuft), "Die Vorphase laesst sich nicht schliessen.");
            Assert.AreEqual(frageLayout, vm.CurrentBuzzerLayout, "Nach der Vorphase kommt die Frage nicht auf die Telefone.");
            Assert.IsTrue(await WarteAsync(() => serverVm!._server!.BuzzerController!.StateManager.CurrentLayout == frageLayout),
                "Am Server liegt noch die Vorphase.");
            Assert.AreEqual(Visibility.Collapsed, vm.Sonderregel.BeamerVisibility, "Die Sonderregel verdeckt die Frage weiter.");

            // --- Karten: Anna spielt um ihren Einsatz, Bert um die Feldpunkte ---
            var anna = Karte(vm, 0);
            var bert = Karte(vm, 1);

            Assert.AreEqual("Einsatz: 40", anna.SonderregelText);
            StringAssert.Contains(bert.SonderregelText, "Kein Einsatz");

            anna.Suggestion = ScoreSuggestion.Right;
            Assert.AreEqual(40, anna.CurrentScoreManipulation, "Richtig bringt nicht den Einsatz.");
            StringAssert.Contains(anna.SuggestionText, "Einsatz 40");

            anna.Suggestion = ScoreSuggestion.Wrong;
            Assert.AreEqual(-40, anna.CurrentScoreManipulation, "Falsch kostet nicht den Einsatz.");

            bert.Suggestion = ScoreSuggestion.Right;
            Assert.AreEqual(feld, bert.CurrentScoreManipulation, "Ohne Einsatz zaehlen nicht die Feldpunkte.");
            bert.Suggestion = ScoreSuggestion.Wrong;
            Assert.AreEqual(-minus, bert.CurrentScoreManipulation);

            // --- E2: ohne Minus kappt der verlorene Einsatz am Kontostand (hier 0) ---
            anna.Coordinate!.Game.AllowNegativeScore = false;
            anna.Suggestion = ScoreSuggestion.Wrong;
            Assert.AreEqual(0, anna.CurrentScoreManipulation, "Der verlorene Einsatz ist nicht gekappt.");
            StringAssert.Contains(anna.SuggestionText, "gekappt");
            anna.Coordinate.Game.AllowNegativeScore = true;

            // --- Ruecknahme: gebucht, dann vollstaendig zurueck ---
            var merk = LetzteWertung.Merke(world.Game, world.Coordinate);
            anna.Suggestion = ScoreSuggestion.Right;
            bert.Suggestion = ScoreSuggestion.Wrong;
            anna.SetManipulationToResult();
            bert.SetManipulationToResult();
            world.Coordinate.QuestionResults = vm.PlayersResultViewModel!.Results;

            CollectionAssert.AreEquivalent(new[] { (anna.Player.Id, 40), (bert.Player.Id, -minus) }, merk.Aenderungen().ToList(),
                "Die Ruecknahme sieht den Einsatz nicht.");

            await merk.NimmZurueckAsync(world.Game);

            Assert.IsTrue(world.Coordinate.QuestionResults.All(r => r.FinalScore == 0 && r.RightCount == 0 && r.WrongCount == 0),
                "Nach der Ruecknahme steht noch ein Teil der Sonderregel.");
        }

        [TestMethod]
        public async Task RiskDoublesOnlyForWhoChoseItAndClosesWhenAllHaveChosen()
        {
            var vm = await OeffneAsync(CellSpecialRule.Risk);
            var feld = world!.Coordinate.CurrentPoints;
            var minus = world.Coordinate.CurrentMinusPoints;

            // „Runde zuruecksetzen" mitten in der Vorphase: sie beginnt neu, die Wahl davor zaehlt nicht.
            await AbgebenAsync(0, CellSpecialRule.Risk, new { doubled = false });
            Assert.IsTrue(await WarteAsync(() => Zustand.Rules?.CurrentSubmissions.Count == 1), "Die erste Wahl kam nicht an.");
            await serverVm!.BuzzerControlsViewModel!.ResetRoundAsync(vm.CurrentBuzzerLayout);
            Assert.IsTrue(await WarteAsync(() => Zustand.Phase == MinigamePhase.Running && Zustand.Rules?.CurrentSubmissions.Count == 0),
                "Nach dem Zuruecksetzen laeuft die Vorphase nicht neu.");
            Assert.IsTrue(vm.VorphaseLaeuft);

            await AbgebenAsync(0, CellSpecialRule.Risk, new { doubled = true });
            await AbgebenAsync(1, CellSpecialRule.Risk, new { doubled = false });

            Assert.IsTrue(await WarteAsync(() => !vm.VorphaseLaeuft), "Nach der letzten Wahl ist die Vorphase nicht zu.");

            var anna = Karte(vm, 0);
            var bert = Karte(vm, 1);

            anna.Suggestion = ScoreSuggestion.Right;
            Assert.AreEqual(2 * feld, anna.CurrentScoreManipulation, "Doppelt spielt nicht doppelt.");
            StringAssert.Contains(anna.SuggestionText, RisikoRegel.DoppeltGrund);
            anna.Suggestion = ScoreSuggestion.Wrong;
            Assert.AreEqual(-2 * minus, anna.CurrentScoreManipulation, "Das Minus ist nicht verdoppelt.");

            bert.Suggestion = ScoreSuggestion.Right;
            Assert.AreEqual(feld, bert.CurrentScoreManipulation, "Nein hat trotzdem verdoppelt.");
            Assert.AreEqual("Risiko: einfach", bert.SonderregelText);
        }

        /// <summary>Wahr oder falsch: der Teilwert der Karte wird zum gleichen Anteil des Einsatzes.</summary>
        [TestMethod]
        public async Task APartialValueBecomesTheSameShareOfTheStake()
        {
            var vm = await OeffneAsync(CellSpecialRule.Stake, QuestionType.TrueFalse);
            var feld = world!.Coordinate.CurrentPoints;

            await AbgebenAsync(0, CellSpecialRule.Stake, new { amount = 30 });
            await vm.SchliesseVorphaseAsync();
            Assert.IsTrue(await WarteAsync(() => !vm.VorphaseLaeuft), "Die Vorphase laesst sich nicht schliessen.");

            var fenster = vm.PlayersResultViewModel!;
            var frage = fenster.Coordinate!.QuestionBase!;
            var tasten = frage.Steps
                .Where(s => !s.IsStart && !s.IsFinish && !string.IsNullOrEmpty(s.QuestionViewKey))
                .Select(s => s.QuestionViewKey)
                .ToList();
            Assert.AreEqual(3, tasten.Count, "Die Aussagen tragen keine Tasten - dann misst die Probe nichts.");

            var ohneRegel = WahrFalschEvaluator.Werte(frage, [tasten[0]], feld).Punkte;
            Assert.IsTrue(ohneRegel > 0 && ohneRegel < feld, $"Kein Teilwert ({ohneRegel}) - dann misst die Probe nichts.");

            foreach (var spieler in new[] { 0, 1 })
            {
                fenster.SetSelectionResult(new LocalBuzzer.Service.Base.States.BuzzerKeySelector.SelectionResult
                {
                    PlayerId = world.Players[spieler].Id,
                    SelectedKeys = [tasten[0]],
                    CommittedResult = true,
                });
            }

            Assert.AreEqual(ScoreSuggestion.Correction, Karte(vm, 0).Suggestion);
            Assert.AreEqual(EinsatzRegel.Anteil(30, ohneRegel, feld), Karte(vm, 0).CurrentScoreManipulation,
                "Der Teilwert ist nicht auf den Einsatz uebertragen.");
            StringAssert.Contains(Karte(vm, 0).SuggestionText, "Einsatz 30");
            Assert.AreEqual(ohneRegel, Karte(vm, 1).CurrentScoreManipulation, "Ohne Einsatz hat sich der Teilwert geaendert.");
        }

        /// <summary>Ein Minispiel spielt nie mit Sonderregel - auch wenn eine an der Zelle steht.</summary>
        [TestMethod]
        public async Task AMinigameCellIgnoresTheRule()
        {
            var vm = await OeffneAsync(CellSpecialRule.Stake, QuestionType.Minigame);

            Assert.IsFalse(vm.VorphaseLaeuft, "Auf einem Minispiel laeuft eine Vorphase.");
            Assert.AreEqual(Visibility.Collapsed, vm.Sonderregel.SpielleiterVisibility);
            Assert.AreEqual(CellSpecialRule.None, Zustand.Setup.SpecialRule, "Der Rahmen spielt die Sonderregel statt des Minispiels.");
        }
    }
}
