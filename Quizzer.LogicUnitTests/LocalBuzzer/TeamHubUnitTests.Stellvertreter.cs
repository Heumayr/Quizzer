using LocalBuzzer.Service.Base.States.Minispiele;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Buzzer;
using Quizzer.DataModels.Questions.Minispiele;
using System.Text.Json;
using static Quizzer.TestShared.TestNetwork;

namespace Quizzer.LogicUnitTests.LocalBuzzer
{
    /// <summary>
    /// Das Stellvertreter-Duell über den echten Hub (M37 T10): wer ohne Team ist, wählt nicht, und seine Wahl erreicht die
    /// Spielleitung nicht; eine Wahl sehen alle Telefone sofort; Team und Kennung in der Nutzlast zählen nicht; mit der letzten
    /// Wahl bekommt jedes Telefon die neue Stufe - der Stellvertreter spielt, sein Team schaut zu. Der Spielleiter lost offene Teams.
    /// </summary>
    public partial class TeamHubUnitTests
    {
        private readonly List<MinigameSubmission> wahlen = new();

        private async Task StellvertreterAsync()
        {
            Steuerung.EventBus.MinigameSubmitted += z => { lock (wahlen) wahlen.Add(z); };
            Steuerung.StateManager.MinigameState.Configure(new MinigameSetup(MinigameKind.ProxyDuel, "Stellvertreter-Duell", "Regel", 1));
            await Steuerung.ResetRoundAsync(1, BuzzerControlsLayout.Minigame);
            Assert.IsTrue(await Steuerung.StartMinigameRoundAsync(), "Das Stellvertreter-Duell liess sich nicht starten.");
        }

        private Task GibAsync(Telefon t, object nutzlast)
            => t.Verbindung.InvokeAsync("SpielAktion", new
            {
                kind = (int)MinigameKind.ProxyDuel,
                durchgang = Steuerung.StateManager.MinigameState.Durchgang,
                payload = nutzlast,
            });

        private static JsonElement? Duellinfo(Telefon t)
            => Letzter(t)?.LayoutInfo is JsonElement info && info.TryGetProperty("kindInfo", out var art) && art.ValueKind == JsonValueKind.Object ? art : null;

        private static string? Zuschauertext(Telefon t)
            => Letzter(t)?.LayoutInfo is JsonElement info && info.TryGetProperty("watcherText", out var text) && text.ValueKind == JsonValueKind.String
                ? text.GetString()
                : null;

        /// <summary>Wen das Team schickt, wie dieses Telefon es sieht; <c>null</c>, solange es wählt.</summary>
        private static string? VertreterAmTelefon(Telefon t, string team)
            => Duellinfo(t)?.GetProperty("teams").EnumerateArray()
                .Where(e => e.GetProperty("name").GetString() == team)
                .Select(e => e.GetProperty("proxyName").ValueKind == JsonValueKind.String ? e.GetProperty("proxyName").GetString() : null)
                .FirstOrDefault();

        private static bool SpieltAmTelefon(Telefon t) => Duellinfo(t)?.GetProperty("isProxy").GetBoolean() == true;

        [TestMethod]
        public async Task TheProxiesAreChosenOverTheHubAndOnlyTheyPlay()
        {
            var a = await VerbindeAsync(anna);
            var b = await VerbindeAsync(bert);
            var c = await VerbindeAsync(carl);
            var d = await VerbindeAsync(dora);
            await StellvertreterAsync();

            // Dora ohne Team: verworfen, die Spielleitung sieht nichts.
            Assert.IsTrue(await WarteAsync(() => Letzter(d)?.CurrentLayoutLocked == true), "Doras Telefon ohne Team ist nicht gesperrt.");
            await GibAsync(d, new { chosen = dora.Id, lot = false });
            await Task.Delay(200);
            Assert.AreEqual(0, Zahl(wahlen), "Doras Wahl ohne Team erreichte die Spielleitung.");

            // Anna wählt Bert - Carls Telefon sieht es sofort, Annas sagt, wen Rot schickt.
            await GibAsync(a, new { chosen = bert.Id, lot = false });
            Assert.IsTrue(await WarteAsync(() => Zahl(wahlen) == 1), "Annas Wahl erreicht die Spielleitung nicht.");
            Assert.IsTrue(await WarteAsync(() => VertreterAmTelefon(c, "Rot") == "Bert"), "Carls Telefon erfährt nicht, wen Rot schickt.");
            Assert.IsTrue(await WarteAsync(() => Letzter(a)?.CurrentLayoutLocked == true
                                                 && Zuschauertext(a) == "Euer Stellvertreter: Bert – die anderen Teams wählen noch."),
                "Annas Telefon sagt nicht, wen Rot schickt: " + Zuschauertext(a));

            // Carl schickt Rot und Annas Kennung mit - es zählt trotzdem für Blau.
            await GibAsync(c, new { chosen = carl.Id, lot = false, teamId = rot.Id, playerId = anna.Id });
            Assert.IsTrue(await WarteAsync(() => Zahl(wahlen) == 2));
            MinigameSubmission letzte;
            lock (wahlen) letzte = wahlen[^1];
            Assert.AreEqual(carl.Id, letzte.Player.Id, "Die Nachricht hat bestimmt, wer wählt.");
            CollectionAssert.AreEquivalent(new[] { new Stellvertreter(rot.Id, bert.Id, false), new Stellvertreter(blau.Id, carl.Id, false) },
                ((ProxiesComplete)letzte.Value).Vertreter.ToList());
            Assert.AreEqual(new Stellvertreter(blau.Id, carl.Id, false), ((ProxiesComplete)letzte.Value).Zuletzt, "Die letzte Wahl fehlt.");

            // Die neue Stufe erreicht jedes Telefon: Bert spielt, Anna schaut zu.
            Assert.IsTrue(await WarteAsync(() => SpieltAmTelefon(b) && Letzter(b)?.CurrentLayoutLocked == false), "Berts Telefon spielt nicht.");
            Assert.IsTrue(await WarteAsync(() => Zuschauertext(a) == "Bert spielt für euer Team – du schaust zu."), "Anna erfährt nicht, dass Bert spielt.");
            Assert.IsFalse(SpieltAmTelefon(a));

            // Annas Zeit erreicht die Spielleitung nicht, Berts schon.
            await GibAsync(a, new { falseStart = false, milliseconds = 150 });
            await Task.Delay(200);
            Assert.AreEqual(2, Zahl(wahlen), "Annas Zeit als Zuschauerin erreichte die Spielleitung.");
            await GibAsync(b, new { falseStart = false, milliseconds = 250 });
            Assert.IsTrue(await WarteAsync(() => Zahl(wahlen) == 3), "Berts Zeit als Stellvertreter erreicht die Spielleitung nicht.");
        }

        [TestMethod]
        public async Task TheGameMasterDrawsForATeamThatDoesNotChoose()
        {
            var a = await VerbindeAsync(anna);
            var b = await VerbindeAsync(bert);
            var c = await VerbindeAsync(carl);
            await StellvertreterAsync();

            await GibAsync(c, new { lot = true });
            Assert.IsTrue(await WarteAsync(() => VertreterAmTelefon(a, "Blau") == "Carl"), "Das Los für Blau kommt nicht an.");

            Assert.IsTrue(await Steuerung.DrawOpenProxiesAsync(), "Der Spielleiter konnte nicht losen.");

            var gelost = Steuerung.StateManager.MinigameState.Lenke(r => ((ProxyDuelRules)r!).Vertreter).Single(v => v.TeamId == rot.Id);
            var (spielt, schaut) = gelost.SpielerId == anna.Id ? (a, b) : (b, a);
            Assert.IsTrue(gelost.Gelost && (gelost.SpielerId == anna.Id || gelost.SpielerId == bert.Id), "Rot bekam keinen der Seinen gelost.");
            Assert.IsTrue(await WarteAsync(() => SpieltAmTelefon(spielt) && Letzter(schaut)?.CurrentLayoutLocked == true && SpieltAmTelefon(c)),
                "Nach dem Losen spielen nicht genau die Stellvertreter.");
            Assert.IsFalse(await Steuerung.DrawOpenProxiesAsync(), "Nach dem Schluss der Wahl wurde noch einmal gelost.");
        }
    }
}
