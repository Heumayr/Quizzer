using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.Logic.Controller.TypedControllers;
using Quizzer.UnitTests.PlayThrough;
using Quizzer.Views.GameViews;
using System.Windows;
using static Quizzer.Views.GameViews.Sub.PlayerResultContext;
using PrePhase = LocalBuzzer.Service.Base.States.Joker.MinigamePrePhase;

namespace Quizzer.UnitTests.Views.GameViews
{
    /// <summary>
    /// Die Joker-Runde im Fragefenster (M35 C7b), mit echtem Buzzer-Server und Telefonen: sie oeffnet nur mit offenen
    /// Jokern und nur bei einer gewoehnlichen Frage, schliesst, sobald alle Halter entschieden haben, loest sofort ein,
    /// wirkt als Vorschlag auf der Karte (E1, E2) und ueber die Antwortsperre, und ohne Wertung gehen die Joker wieder auf.
    /// </summary>
    [TestClass]
    public class JokerRundeUnitTests : SonderregelServerTestBasis
    {
        protected override int Port => 5445;

        private readonly Dictionary<int, PlayerJoker> joker = new();

        /// <summary>Vergibt vor dem Oeffnen Joker: je Spielerindex eine Art.</summary>
        private void Vergib(params (int Spieler, JokerKind Art)[] wer)
            => VorDemOeffnen = async welt =>
            {
                using var ctrl = new PlayerJokersController();

                foreach (var (spieler, art) in wer)
                    joker[spieler] = await ctrl.GrantAsync(welt.Game.Id, welt.Players[spieler].Id, art, "verdient", DateTimeOffset.Now);

                await ctrl.SaveChangesAsync();
            };

        private Task JokerAsync(int telefon, Guid? jokerId, Guid? ziel = null)
            => phones[telefon].InvokeAsync("SpielAktion", new
            {
                kind = 0,
                specialRule = 0,
                prePhase = (int)PrePhase.Joker,
                durchgang = Zustand.Durchgang,
                payload = new { jokerId, target = ziel },
            });

        private static async Task<PlayerJoker> AusDerDatenbankAsync(PlayerJoker j)
        {
            using var ctrl = new PlayerJokersController();
            return (await ctrl.GetAsync(j.Id))!;
        }

        private string Name(int spieler) => world!.Players[spieler].CalculatedDisplayName;

        private int Feld => world!.Coordinate.CurrentPoints;

        private int Minus => world!.Coordinate.CurrentMinusPoints;

        [TestMethod]
        public async Task WithoutOpenJokersThereIsNoRoundAndNoExtraStep()
        {
            var vm = await OeffneAsync(CellSpecialRule.None);

            Assert.IsFalse(vm.VorphaseLaeuft, "Ohne Joker laeuft eine Joker-Runde.");
            Assert.AreNotEqual(BuzzerControlsLayout.Minigame, vm.CurrentBuzzerLayout, "Ohne Joker liegt der Rahmen auf den Telefonen.");
            Assert.IsFalse(vm.Sonderregel.Aktiv, "Ohne Joker zeigt der Spielleiter einen Joker-Bereich.");
        }

        [TestMethod]
        public async Task TheRoundClosesWhenAllHoldersDecidedAndRedeemsAtOnce()
        {
            Vergib((0, JokerKind.Double), (1, JokerKind.Block));
            var vm = await OeffneAsync(CellSpecialRule.None);

            Assert.IsTrue(vm.VorphaseLaeuft, "Mit offenen Jokern oeffnet keine Joker-Runde.");
            Assert.AreEqual(BuzzerControlsLayout.Minigame, vm.CurrentBuzzerLayout);
            Assert.AreEqual(PrePhase.Joker, Zustand.Setup.PrePhase);
            Assert.AreEqual("Joker", vm.Sonderregel.Bereich);

            var vorher = DateTimeOffset.Now.AddSeconds(-1);
            await JokerAsync(0, joker[0].Id);
            Assert.IsTrue(await WarteAsync(() => vm.Sonderregel.Stand.Contains("1 von 2")), "Der Stand zaehlt die Entscheidung nicht: " + vm.Sonderregel.Stand);
            Assert.IsTrue(vm.VorphaseLaeuft, "Die Runde schloss, bevor alle Halter entschieden haben.");

            // Geheim bis zum Schluss - auch der Spielleiter- und Beamerbereich nennt niemanden.
            Assert.AreEqual(string.Empty, vm.Sonderregel.Hinweis, "Der Beamer zeigt vor dem Schliessen einen Joker.");
            Assert.IsFalse(vm.Sonderregel.Stand.Contains(Name(0)), "Der Stand nennt, wer entschieden hat.");

            await JokerAsync(1, joker[1].Id, world!.Players[2].Id);
            Assert.IsTrue(await WarteAsync(() => !vm.VorphaseLaeuft), "Die Runde schloss nicht, als alle Halter entschieden hatten.");

            var doppelt = await AusDerDatenbankAsync(joker[0]);
            var sperre = await AusDerDatenbankAsync(joker[1]);
            Assert.AreEqual(world.Coordinate.Id, doppelt.GameGridCoordinateId, "Annas Joker ist nicht an der Zelle eingeloest.");
            Assert.IsTrue(doppelt.RedeemedAt >= vorher, "Die Einloesezeit fehlt.");
            Assert.AreEqual((world.Coordinate.Id, world.Players[2].Id), (sperre.GameGridCoordinateId, sperre.TargetPlayerId), "Die Sperre traegt Zelle oder Ziel nicht.");

            // Der Dialog aus C7a zeigt sie als eingeloest.
            var dialog = new JokerViewModel { GameId = world.Game.Id };
            await dialog.LoadForTestAsync();
            Assert.AreEqual("eingelöst", dialog.Players.Single(p => p.Player.Id == world.Players[0].Id).Jokers.Single().Status);

            StringAssert.Contains(vm.Sonderregel.Hinweis, $"{Name(0)} – Doppelt", "Der Beamer zeigt den Joker nach dem Schliessen nicht.");
            StringAssert.Contains(vm.Sonderregel.Hinweis, $"Sperre gegen {Name(2)}");
            Assert.AreEqual("Joker: Doppelt", Karte(vm, 0).SonderregelText, "Annas Karte zeigt ihren Joker nicht.");
            StringAssert.Contains(Karte(vm, 2).SonderregelText, $"Gesperrt per Joker von {Name(1)}");
        }

        [TestMethod]
        public async Task DoubleDoublesRightAndWrong()
        {
            Vergib((0, JokerKind.Double));
            var vm = await OeffneAsync(CellSpecialRule.None);
            await JokerAsync(0, joker[0].Id);
            Assert.IsTrue(await WarteAsync(() => !vm.VorphaseLaeuft), "Die Runde schloss nicht.");

            var anna = Karte(vm, 0);
            anna.Suggestion = ScoreSuggestion.Right;
            Assert.AreEqual(2 * Feld, anna.CurrentScoreManipulation, "Doppelt verdoppelt das Plus nicht.");
            anna.Suggestion = ScoreSuggestion.Wrong;
            Assert.AreEqual(-2 * Minus, anna.CurrentScoreManipulation, "Doppelt verdoppelt das Minus nicht.");

            var bert = Karte(vm, 1);
            bert.Suggestion = ScoreSuggestion.Right;
            Assert.AreEqual(Feld, bert.CurrentScoreManipulation, "Ohne Joker hat sich der Wert geaendert.");
        }

        [TestMethod]
        public async Task ShieldTakesTheMinusAndLeavesThePlus()
        {
            Vergib((0, JokerKind.Shield));
            var vm = await OeffneAsync(CellSpecialRule.None);
            await JokerAsync(0, joker[0].Id);
            Assert.IsTrue(await WarteAsync(() => !vm.VorphaseLaeuft));

            var anna = Karte(vm, 0);
            Assert.IsTrue(Minus > 0, "Die Zelle hat kein Minus - die Probe misst sonst nichts.");
            anna.Suggestion = ScoreSuggestion.Wrong;
            Assert.AreEqual(0, anna.CurrentScoreManipulation, "Der Schutz nimmt das Minus nicht.");
            anna.Suggestion = ScoreSuggestion.Right;
            Assert.AreEqual(Feld, anna.CurrentScoreManipulation, "Der Schutz aendert das Plus.");
        }

        [TestMethod]
        public async Task TheMasterOverridesTheJokerSuggestion()
        {
            Vergib((0, JokerKind.Double));
            var vm = await OeffneAsync(CellSpecialRule.None);
            await JokerAsync(0, joker[0].Id);
            Assert.IsTrue(await WarteAsync(() => !vm.VorphaseLaeuft));

            var anna = Karte(vm, 0);
            anna.Suggestion = ScoreSuggestion.Right;
            anna.Suggestion = ScoreSuggestion.Correction;
            anna.CurrentScoreManipulation = 7;
            Karte(vm, 1).Suggestion = ScoreSuggestion.Right;

            Assert.AreEqual(7, anna.CurrentScoreManipulation, "Der Joker hat den eigenen Wert des Spielleiters ueberschrieben.");
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task ADoubledMinusFollowsAllowNegativeScore(bool unterNull)
        {
            Vergib((0, JokerKind.Double));
            var vm = await OeffneAsync(CellSpecialRule.None, kontostaende: [80, 0, 0]);
            world!.Game.AllowNegativeScore = unterNull;
            await JokerAsync(0, joker[0].Id);
            Assert.IsTrue(await WarteAsync(() => !vm.VorphaseLaeuft));

            // Zwischen einfachem und doppeltem Minus: ohne Joker griffe die Kappung nicht, mit Joker schon (gemessen: bei
            // einem Kontostand unter dem einfachen Minus kappt E2 beides gleich, die Zeile bliebe ohne Joker gruen).
            Assert.IsTrue(Minus < 80 && 2 * Minus > 80, $"Minus {Minus} liegt nicht um den Kontostand 80 - die Probe misst sonst nichts.");
            var anna = Karte(vm, 0);
            anna.Suggestion = ScoreSuggestion.Wrong;

            Assert.AreEqual(unterNull ? -2 * Minus : -80, anna.CurrentScoreManipulation,
                unterNull ? "Mit erlaubtem Minus ist trotzdem gekappt." : "Das verdoppelte Minus ist nicht am Kontostand gekappt (E2).");
        }

        [TestMethod]
        public async Task TheBlockedPlayerCannotBuzzReadsWhoBlockedHimAndTheMasterCanLiftIt()
        {
            Vergib((1, JokerKind.Block));
            var vm = await OeffneAsync(CellSpecialRule.None);
            Assert.AreEqual(BuzzerControlsLayout.Buzzer, vm.Question!.BuzzerControlsLayout, "Die Testfrage ist keine Buzzerfrage.");

            await JokerAsync(1, joker[1].Id, world!.Players[2].Id);
            Assert.IsTrue(await WarteAsync(() => !vm.VorphaseLaeuft));

            Assert.IsTrue(await WarteAsync(() => Stand(2) is { ExcludedFromQuestion: true }), "Claras Telefon ist nicht gesperrt.");
            StringAssert.Contains(Stand(2)!.ExcludedReason, Name(1), "Clara liest nicht, wer sie gesperrt hat.");
            Assert.IsFalse(Stand(0)!.ExcludedFromQuestion, "Anna ist gesperrt, obwohl sie niemand gesperrt hat.");
            Assert.AreEqual(Visibility.Visible, vm.JokerSperreVisibility, "Der Spielleiter kann die Sperre nicht aufheben.");

            var steuerung = serverVm!.BuzzerControlsViewModel!.BuzzerController!.StateManager;
            await phones[2].InvokeAsync("Buzz");
            await Task.Delay(150);
            Assert.IsNull(steuerung.BuzzerState.Winner, "Die Gesperrte hat gebuzzert.");

            await vm.HebeJokerSperreAufAsync();
            Assert.IsTrue(await WarteAsync(() => Stand(2) is { ExcludedFromQuestion: false }), "Nach dem Aufheben ist Clara noch gesperrt.");
            Assert.AreEqual(Visibility.Collapsed, vm.JokerSperreVisibility);

            await phones[2].InvokeAsync("Buzz");
            Assert.IsTrue(await WarteAsync(() => steuerung.BuzzerState.Winner?.Id == world.Players[2].Id), "Nach dem Aufheben kann Clara nicht buzzern.");
        }

        [TestMethod]
        public async Task WhenTheMasterClosesEarlyNobodyWithoutAChoiceUsesAJoker()
        {
            Vergib((0, JokerKind.Double), (1, JokerKind.Shield));
            var vm = await OeffneAsync(CellSpecialRule.None);

            await JokerAsync(0, joker[0].Id);
            Assert.IsTrue(await WarteAsync(() => vm.Sonderregel.Stand.Contains("1 von 2")));
            await vm.SchliesseVorphaseAsync();
            Assert.IsTrue(await WarteAsync(() => !vm.VorphaseLaeuft), "Der Spielleiter konnte die Joker-Runde nicht schliessen.");

            Assert.IsTrue((await AusDerDatenbankAsync(joker[0])).IsRedeemed, "Annas Wahl wurde nicht eingeloest.");
            Assert.IsFalse((await AusDerDatenbankAsync(joker[1])).IsRedeemed, "Bert hat nichts gewaehlt und trotzdem einen Joker gesetzt.");
            Assert.AreEqual(string.Empty, Karte(vm, 1).SonderregelText);
        }

        [TestMethod]
        [DataRow("ohne Wertung", false)]
        [DataRow("bewertet", true)]
        [DataRow("abgeschlossen", true)]
        public async Task LeavingWithoutRatingReopensTheJoker(string wie, bool bleibtEingeloest)
        {
            Vergib((0, JokerKind.Double));
            var vm = await OeffneAsync(CellSpecialRule.None);
            await JokerAsync(0, joker[0].Id);
            Assert.IsTrue(await WarteAsync(() => !vm.VorphaseLaeuft));
            Assert.IsTrue((await AusDerDatenbankAsync(joker[0])).IsRedeemed);

            if (wie == "bewertet")
                Karte(vm, 0).Suggestion = ScoreSuggestion.Right;

            if (wie == "abgeschlossen")
                vm.IsDone = true;

            await vm.ClosedForTestAsync();

            Assert.AreEqual(bleibtEingeloest, (await AusDerDatenbankAsync(joker[0])).IsRedeemed, $"Frage {wie} verlassen.");
        }

        [TestMethod]
        [DataRow("Sonderregel")]
        [DataRow("Minispiel")]
        [DataRow("gespielt")]
        public async Task NoJokerRoundOnASpecialRuleAMinigameOrAPlayedCell(string fall)
        {
            Vergib((0, JokerKind.Double));
            ZelleEinrichten = zelle => zelle.IsDone = fall == "gespielt";

            // Kopfgeld hat keine Vorphase - die Joker-Runde waere gleich danach dran, wenn sie es nicht pruefte.
            var vm = await OeffneAsync(fall == "Sonderregel" ? CellSpecialRule.Bounty : CellSpecialRule.None,
                typ: fall == "Minispiel" ? QuestionType.Minigame : QuestionType.Default);

            Assert.AreEqual(PrePhase.None, Zustand.Setup.PrePhase, $"Bei {fall} oeffnet eine Joker-Runde.");
            Assert.AreNotEqual("Joker", vm.Sonderregel.Bereich);
            Assert.IsFalse((await AusDerDatenbankAsync(joker[0])).IsRedeemed);
        }

        [TestMethod]
        public async Task AFailedRedeemCountsNoJokerAndSaysSo()
        {
            Vergib((0, JokerKind.Double), (1, JokerKind.Block));
            var vm = await OeffneAsync(CellSpecialRule.None);
            vm.JokerEinloesen = _ => throw new InvalidOperationException("Datenbank weg");

            await JokerAsync(0, joker[0].Id);
            await JokerAsync(1, joker[1].Id, world!.Players[2].Id);
            Assert.IsTrue(await WarteAsync(() => !vm.VorphaseLaeuft), "Die Runde schloss nicht.");

            var meldungen = ((RecordingUserPrompt)UserPrompt.Current).Informations;
            Assert.IsTrue(meldungen.Any(m => m.Contains("nicht speichern")), "Die Stoerung wurde nicht gemeldet: " + string.Join(" | ", meldungen));

            Karte(vm, 0).Suggestion = ScoreSuggestion.Right;
            Assert.AreEqual(Feld, Karte(vm, 0).CurrentScoreManipulation, "Ein nicht gespeicherter Joker wirkt trotzdem.");
            Assert.IsFalse(Stand(2)?.ExcludedFromQuestion ?? false, "Eine nicht gespeicherte Sperre sperrt trotzdem.");
            Assert.IsFalse((await AusDerDatenbankAsync(joker[0])).IsRedeemed);
        }

        [TestMethod]
        public async Task AJokerRedeemedBeforeACrashStillCountsWhenTheCellOpensAgain()
        {
            VorDemOeffnen = async welt =>
            {
                using var ctrl = new PlayerJokersController();
                // Der Schreibweg klont - die Einloesung steht deshalb gleich im neuen Objekt, nicht nachtraeglich.
                joker[0] = new PlayerJoker
                {
                    Id = Guid.NewGuid(), GameId = welt.Game.Id, PlayerId = welt.Players[0].Id, Kind = JokerKind.Double, Reason = "verdient",
                    GrantedAt = DateTimeOffset.Now, GameGridCoordinateId = welt.Coordinate.Id, RedeemedAt = DateTimeOffset.Now,
                };
                await ctrl.InsertAsync(joker[0]);
                joker[1] = await ctrl.GrantAsync(welt.Game.Id, welt.Players[1].Id, JokerKind.Shield, "verdient", DateTimeOffset.Now);
                await ctrl.SaveChangesAsync();
            };

            var vm = await OeffneAsync(CellSpecialRule.None);

            Assert.IsTrue(vm.VorphaseLaeuft, "Bert mit offenem Joker bekommt keine Runde.");
            Assert.IsFalse(Zustand.Rules!.Plays(world!.Players[0].Id), "Anna entscheidet ein zweites Mal fuer dieselbe Frage.");

            await JokerAsync(1, null);
            Assert.IsTrue(await WarteAsync(() => !vm.VorphaseLaeuft));

            Karte(vm, 0).Suggestion = ScoreSuggestion.Right;
            Assert.AreEqual(2 * Feld, Karte(vm, 0).CurrentScoreManipulation, "Der vor dem Absturz eingeloeste Joker wirkt nicht mehr.");

            // Ohne Wertung verlassen: frei wird nur, was in diesem Fenster eingeloest wurde.
            Karte(vm, 0).Suggestion = ScoreSuggestion.None;
            await vm.ClosedForTestAsync();
            Assert.IsTrue((await AusDerDatenbankAsync(joker[0])).IsRedeemed, "Ein Joker aus einem frueheren Fenster ging beim Verlassen wieder auf.");
        }
    }
}
