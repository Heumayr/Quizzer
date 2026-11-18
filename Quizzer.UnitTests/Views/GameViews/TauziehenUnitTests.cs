using LocalBuzzer.Service.Base.States.Minispiele;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Questions.Minispiele;
using Quizzer.TestShared;
using Quizzer.UnitTests.PlayThrough;
using Quizzer.Views.GameViews;
using static Quizzer.Views.GameViews.Sub.PlayerResultContext;

namespace Quizzer.UnitTests.Views.GameViews
{
    /// <summary>
    /// Tauziehen im Fragefenster (M37 T9), echter Server und Telefone, die Serveruhr zum Vorstellen; Rot = Plätze in
    /// <see cref="TeamregelServerTestBasis.rotePlaetze"/>, die übrigen Blau. Das Seil wandert am Beamer mit jedem Stand, die
    /// Serveruhr schliesst die Runde, das stärkere Team bekommt die Feldpunkte aufgeteilt als Vorschlag (E1, F-M37-06). Summe
    /// oder Durchschnitt (F-M37-11) kommt vom Spiel beim Server an. Dazu der Spielstart ohne Teamspiel.
    /// </summary>
    [TestClass]
    public class TauziehenUnitTests : TeamregelServerTestBasis
    {
        protected override int Port => TestPorts.Tauziehen;

        private sealed class StellbareUhr : TimeProvider
        {
            private long ticks = TimeSpan.FromHours(1).Ticks;

            public override long TimestampFrequency => TimeSpan.TicksPerSecond;

            public override long GetTimestamp() => Interlocked.Read(ref ticks);

            public void Vor(TimeSpan dauer) => Interlocked.Add(ref ticks, dauer.Ticks);
        }

        private readonly StellbareUhr uhr = new();

        private async Task<TestableCurrentQuestionViewModel> OeffneAsync(TeamMinigameScoring wertung = TeamMinigameScoring.Average)
        {
            FensterEinrichten = _ =>
            {
                world!.Game.TeamMinigameScoring = wertung;
                return Task.CompletedTask;
            };

            var vm = await OeffneTeamMinispielAsync(MinigameKind.TugOfWar, f => f.DurationSeconds = 5);
            Zustand.Uhr = uhr;

            Assert.AreEqual(BuzzerControlsLayout.Minigame, vm.CurrentBuzzerLayout);
            return vm;
        }

        /// <summary>Startet und stellt die Serveruhr eine Sekunde hinter den Countdown.</summary>
        private async Task StarteAsync(TestableCurrentQuestionViewModel vm)
        {
            await vm.StartMinispielRundeAsync();
            uhr.Vor(TimeSpan.FromSeconds(4));
        }

        private Task ZieheAsync(int telefon, int tipps, bool ende = false, Guid? fuer = null)
            => phones[telefon].InvokeAsync("SpielAktion", new
            {
                kind = (int)MinigameKind.TugOfWar,
                durchgang = Zustand.Durchgang,
                payload = new { taps = tipps, isFinal = ende, playerId = fuer },
            });

        [TestMethod]
        public async Task TheRopeMovesOnTheBeamerAndTheStrongerTeamSplitsTheFieldValue()
        {
            var vm = await OeffneAsync();
            var feld = PunkteJetzt(world!.Coordinate, vm);

            await StarteAsync(vm);
            StringAssert.Contains(vm.Reaktion.Kopfzeile, "Tauziehen läuft");
            Assert.AreEqual(2, vm.Reaktion.Zeilen.Count, "Am Beamer stehen nicht beide Teams.");
            Assert.AreEqual("Gleichstand", vm.Reaktion.Seil?.Lage, "Zu Beginn steht das Seil nicht in der Mitte.");

            await ZieheAsync(0, 20);
            await ZieheAsync(2, 10);
            Assert.IsTrue(await WarteAsync(() => vm.Reaktion.Seil?.Lage == "✚ Rot zieht stärker"),
                "Das Seil wandert nicht zu Rot: " + vm.Reaktion.Seil?.Lage);
            // Rot steht links, weil es nach SortOrder vorn ist - nach Kennung liegt Blau vorn (V27). Der Knoten halb zu Rot.
            var seil = vm.Reaktion.Seil!;
            Assert.AreEqual(("✚ Rot", "★ Blau"), (seil.Links, seil.Rechts), "Am Beamer steht Rot nicht links.");
            Assert.AreEqual(-0.5 * TauziehenSeil.HalbeLaenge, seil.Versatz, 1e-9,
                $"Der Knoten steht nicht auf halbem Weg zu Rot ({seil.Links} | {seil.Rechts}).");
            Assert.AreEqual(("✚ Rot", "Ø 10,0 Tipps (2 ziehen)"), (vm.Reaktion.Zeilen[0].Name, vm.Reaktion.Zeilen[0].Zeit));
            Assert.IsTrue(vm.Reaktion.Zeilen.All(z => !z.Sieger), "Während der Runde ist ein Sieger markiert.");
            Assert.AreEqual(ScoreSuggestion.None, Karte(vm, 0).Suggestion, "Ein Zwischenstand macht schon einen Vorschlag.");
            Assert.IsTrue(await WarteAsync(() => serverVm!.BuzzerControlsViewModel!.LivePlayers.Any(p => p.AnswerText == "20 Tipps")),
                "Die Übersicht des Spielleiters zeigt die Tipps nicht.");

            // Platz 3 schickt die Kennung von Platz 1 mit - es zählt trotzdem für Blau.
            await ZieheAsync(3, 20, fuer: Id(1));
            Assert.IsTrue(await WarteAsync(() => vm.Reaktion.Seil?.Lage == "★ Blau zieht stärker"),
                "Die fremde Kennung hat für Rot gezogen: " + vm.Reaktion.Seil?.Lage);

            await ZieheAsync(1, 20);
            Assert.IsTrue(await WarteAsync(() => vm.Reaktion.Seil?.Lage == "✚ Rot zieht stärker"));

            // Kein Endstand: die Serveruhr schliesst nach Dauer und Nachlauf.
            uhr.Vor(TimeSpan.FromMilliseconds(5000 + TauziehenEvaluator.NachlaufMs + 1));
            Assert.IsTrue(await WarteAsync(() => vm.Reaktion.Kopfzeile.StartsWith("Ergebnis")),
                "Die Serveruhr schliesst das Tauziehen nicht: " + vm.Reaktion.Kopfzeile);

            Assert.AreEqual("Ergebnis – ✚ Rot gewinnt das Tauziehen", vm.Reaktion.Kopfzeile);
            Assert.AreEqual("✚ Rot", vm.Reaktion.Seil?.Links, "Mit dem Ergebnis wechselt Rot die Seite (V27).");
            Assert.AreEqual("★ Sieger", vm.Reaktion.Zeilen[0].SiegerText, "Das Siegerteam ist nicht mit Symbol und Text markiert.");
            Assert.AreEqual(feld, Karte(vm, 0).CurrentScoreManipulation + Karte(vm, 1).CurrentScoreManipulation,
                "Rot bekommt nicht genau den Feldwert.");
            Assert.AreEqual((ScoreSuggestion.Correction, ScoreSuggestion.Correction), (Karte(vm, 0).Suggestion, Karte(vm, 1).Suggestion));
            StringAssert.Contains(Karte(vm, 1).Grund, "Rot gewinnt das Tauziehen");
            StringAssert.Contains(Karte(vm, 1).ReihenfolgeText, "20 Tipps");
            Assert.AreEqual((ScoreSuggestion.None, ScoreSuggestion.None), (Karte(vm, 2).Suggestion, Karte(vm, 3).Suggestion), "Blau bekommt etwas.");
            StringAssert.Contains(Karte(vm, 2).ReihenfolgeText, "nicht stärker");
            Assert.IsTrue(await WarteAsync(() => vm.ShowResultWindowCalls > 0), "Nach dem Ende geht das Bewertungsfenster nicht auf.");

            // Wiederholen: die gewertete Runde zählt nicht mehr.
            await vm.WiederholeMinispielRundeAsync();
            Assert.AreEqual(ScoreSuggestion.None, Karte(vm, 0).Suggestion, "Nach dem Wiederholen bleibt der Vorschlag stehen.");
            StringAssert.Contains(vm.Reaktion.Kopfzeile, "Tauziehen läuft");
        }

        /// <summary>Drei in Rot, einer in Blau: die Summe am Spiel lässt Rot gewinnen, der Durchschnitt Blau - die Einstellung kommt beim Server an.</summary>
        [TestMethod]
        [DataRow(TeamMinigameScoring.Sum, "✚ Rot")]
        [DataRow(TeamMinigameScoring.Average, "★ Blau")]
        public async Task TheGameDecidesSumOrAverage(TeamMinigameScoring wertung, string sieger)
        {
            rotePlaetze = [0, 1, 2];
            var vm = await OeffneAsync(wertung);
            Assert.AreEqual(wertung, Zustand.Setup.TeamScoring, "Die Einstellung des Spiels kommt nicht beim Server an.");

            await StarteAsync(vm);
            foreach (var (telefon, tipps) in new[] { (0, 10), (1, 10), (2, 10), (3, 15) })
                await ZieheAsync(telefon, tipps, ende: true);

            Assert.IsTrue(await WarteAsync(() => vm.Reaktion.Kopfzeile.StartsWith("Ergebnis")), "Nach allen Endständen schliesst die Runde nicht.");
            Assert.AreEqual($"Ergebnis – {sieger} gewinnt das Tauziehen", vm.Reaktion.Kopfzeile);
            Assert.AreEqual($"{sieger} zieht stärker", vm.Reaktion.Seil?.Lage, "Das Seil steht nicht beim Siegerteam.");
        }

        /// <summary>
        /// R4-1, Wege Siegerteam (Tauziehen) und Teamanteil (Reaktion, zusammengefasst): nach der Buchung ist „Runde wiederholen“
        /// gesperrt - sonst bekäme ein Team den Feldwert zweimal, oder beide Teams je einmal.
        /// </summary>
        [TestMethod]
        [DataRow(MinigameKind.TugOfWar)]
        [DataRow(MinigameKind.Reaction)]
        public async Task ARepeatAfterABookedTeamRoundIsRefused(MinigameKind art)
        {
            var vm = art == MinigameKind.TugOfWar ? await OeffneAsync() : await OeffneTeamMinispielAsync(art, f => f.Rounds = 1);

            if (art == MinigameKind.TugOfWar)
                await StarteAsync(vm);
            else
                await vm.StartMinispielRundeAsync();

            for (var telefon = 0; telefon < 4; telefon++)
            {
                if (art == MinigameKind.TugOfWar)
                    await ZieheAsync(telefon, telefon < 2 ? 20 : 10, ende: true);
                else
                    await phones[telefon].InvokeAsync("SpielAktion", new { kind = (int)art, durchgang = Zustand.Durchgang, payload = new { falseStart = false, milliseconds = 200 + 50 * telefon } });
            }

            Assert.IsTrue(await WarteAsync(() => vm.Reaktion.Kopfzeile.StartsWith("Ergebnis")), "Die Runde schliesst nicht: " + vm.Reaktion.Kopfzeile);
            Assert.IsTrue(Karte(vm, 0).CurrentScoreManipulation > 0, "Rot bekommt keinen Vorschlag - der Test misst nichts.");
            Assert.IsTrue(await vm.PlayersResultViewModel!.TrySaveAsync(), "Speichern scheiterte.");

            Assert.IsFalse(vm.RepeatMinigameRoundCommand.CanExecute(null), "Nach der Buchung ist „Runde wiederholen“ nicht gesperrt.");
            await vm.WiederholeMinispielRundeAsync();
            StringAssert.StartsWith(vm.Reaktion.Kopfzeile, "Ergebnis", "Die gebuchte Runde wurde wiederholt.");
        }

        /// <summary>Ohne Teamspiel fragt der Spielstart - über den Weg, den der Start wirklich nimmt (E1: fragen, nicht abweisen).</summary>
        [TestMethod]
        public async Task WithoutATeamGameTheStartAsks()
        {
            var prompt = new RecordingUserPrompt(answer: true);
            Quizzer.Base.UserPrompt.Current = prompt;
            world = await TestGameBuilder.CreateAsync(QuestionType.Minigame, normalStepCount: 2, playerCount: 2,
                adjustQuestion: f => MinigameProfiles.For(MinigameKind.TugOfWar).ApplyDefaults((MinigameQuestion)f));
            var leiter = new Player { Id = Guid.NewGuid(), Designation = "Leo" };
            world.Game.Moderator = leiter;
            world.Game.ModeratorPlayerId = leiter.Id;

            Assert.IsTrue(await GameMasterViewModel.StartbarAsync(world.Game), "Der Start wurde abgewiesen statt gefragt (E1).");
            StringAssert.Contains(prompt.Confirms.LastOrDefault()?.Message, "gibt es nur im Teamspiel", "Das Tauziehen ohne Teamspiel fällt beim Start nicht auf.");
        }
    }
}
