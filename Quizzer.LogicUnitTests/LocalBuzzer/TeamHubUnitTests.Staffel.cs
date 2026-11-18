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
    /// Die Staffel über den echten Hub (M37 T11): wer ohne Team ist oder nicht dran, erreicht die Spielleitung nicht; Team und
    /// Kennung in der Nutzlast zählen nicht; wer läuft, sieht jedes Telefon sofort; keine Lösung erreicht ein Telefon vor dem Ende.
    /// Die Spielleitung lässt eine abgelehnte Antwort gelten und gibt weiter - jedes Telefon erfährt es.
    /// </summary>
    public partial class TeamHubUnitTests
    {
        private readonly List<MinigameSubmission> staffelAbgaben = new();

        /// <summary>Anna hat das Feld aufgedeckt: Rot beginnt mit Anna, dann Bert; Blau läuft mit Carl, Dora ohne Team schaut zu.</summary>
        private async Task StaffelAsync()
        {
            Steuerung.EventBus.MinigameSubmitted += z => { lock (staffelAbgaben) staffelAbgaben.Add(z); };
            Steuerung.StateManager.MinigameState.Configure(new MinigameSetup(MinigameKind.Relay, "Staffel", "Regel", 1, TriggerPlayerId: anna.Id,
                Content: [new("Merkur", false), new("Venus", false), new("Erde", false)]));
            await Steuerung.ResetRoundAsync(1, BuzzerControlsLayout.Minigame);
            Assert.IsTrue(await Steuerung.StartMinigameRoundAsync(), "Die Staffel liess sich nicht starten.");
        }

        private Task LaufeAsync(Telefon t, int zug, string antwort, object? dazu = null)
            => t.Verbindung.InvokeAsync("SpielAktion", new
            {
                kind = (int)MinigameKind.Relay,
                durchgang = Steuerung.StateManager.MinigameState.Durchgang,
                payload = dazu ?? new { turn = zug, answer = antwort },
            });

        private int Zug => Steuerung.StateManager.MinigameState.Lenke(r => ((RelayRules)r!).Stand.Zug);

        private static JsonElement? Staffelinfo(Telefon t)
            => Letzter(t)?.LayoutInfo is JsonElement info && info.TryGetProperty("kindInfo", out var art) && art.ValueKind == JsonValueKind.Object ? art : null;

        private static bool LaeuftAmTelefon(Telefon t) => Staffelinfo(t)?.GetProperty("isRunner").GetBoolean() == true;

        private static string? LaeuferAmTelefon(Telefon t)
            => Staffelinfo(t)?.GetProperty("runnerName") is { ValueKind: JsonValueKind.String } name ? name.GetString() : null;

        private static string AlleStaende(params Telefon[] telefone)
        {
            var texte = new List<string>();

            foreach (var t in telefone)
            {
                lock (t.Staende)
                    texte.AddRange(t.Staende.Select(s => JsonSerializer.Serialize(s)));
            }

            return string.Join("|", texte);
        }

        [TestMethod]
        public async Task TheRelayRunsOverTheHubAndNoSolutionReachesAPhoneEarly()
        {
            var a = await VerbindeAsync(anna);
            var b = await VerbindeAsync(bert);
            var c = await VerbindeAsync(carl);
            var d = await VerbindeAsync(dora);
            await StaffelAsync();

            Assert.IsTrue(await WarteAsync(() => LaeuftAmTelefon(a) && LaeuferAmTelefon(b) == "Anna"), "Annas Telefon läuft nicht, oder Bert weiß es nicht.");
            Assert.IsTrue(await WarteAsync(() => Letzter(d)?.CurrentLayoutLocked == true), "Doras Telefon ohne Team ist nicht gesperrt.");

            // Dora ohne Team und Bert, der nicht dran ist: verworfen, die Spielleitung sieht nichts.
            await LaufeAsync(d, Zug, "Merkur");
            await LaufeAsync(b, Zug, "Merkur");
            await Task.Delay(200);
            Assert.AreEqual(0, Zahl(staffelAbgaben), "Eine Abgabe von Dora oder Bert erreichte die Spielleitung.");

            // Anna schickt Blau und Carls Kennung mit - es zählt trotzdem für Rot, und Bert läuft als Nächster.
            await LaufeAsync(a, 0, string.Empty, new { turn = Zug, answer = "Merkur", teamId = blau.Id, playerId = carl.Id });
            Assert.IsTrue(await WarteAsync(() => Zahl(staffelAbgaben) == 1), "Annas Antwort erreicht die Spielleitung nicht.");
            MinigameSubmission erste;
            lock (staffelAbgaben) erste = staffelAbgaben[0];
            Assert.AreEqual((anna.Id, rot.Id), (erste.Player.Id, ((StaffelAntwort)erste.Value).TeamId), "Die Nachricht hat bestimmt, wer für wen antwortet.");
            Assert.IsTrue(await WarteAsync(() => LaeuftAmTelefon(b) && LaeuferAmTelefon(c) == "Bert"), "Carls Telefon erfährt nicht, dass Bert läuft.");

            // Bert liegt daneben: Carl ist dran.
            await LaufeAsync(b, Zug, "Pluto");
            Assert.IsTrue(await WarteAsync(() => LaeuftAmTelefon(c) && !LaeuftAmTelefon(b)), "Nach Berts Fehler läuft nicht Carl.");
            Assert.IsFalse(AlleStaende(a, b, c, d).Contains("Venus") || AlleStaende(a, b, c, d).Contains("Erde"), "Eine Lösung erreichte ein Telefon vor dem Ende.");

            // Carl läuft für Blau allein durch - danach steht die Auflösung an jedem Telefon.
            await LaufeAsync(c, Zug, "Venus");
            Assert.IsTrue(await WarteAsync(() => Zahl(staffelAbgaben) == 3));
            await LaufeAsync(c, Zug, "Erde");
            Assert.IsTrue(await WarteAsync(() => Staffelinfo(d)?.GetProperty("resolution").GetArrayLength() == 3), "Die Auflösung erreicht Doras Telefon nicht.");
            Assert.IsTrue(AlleStaende(a).Contains("Erde"), "Die Gegenprobe findet die Lösung auch nach dem Ende nicht - die Suche misst nichts.");
        }

        [TestMethod]
        public async Task TheGameMasterAcceptsAMissAndPassesForTheTeamOnTurn()
        {
            var a = await VerbindeAsync(anna);
            var b = await VerbindeAsync(bert);
            var c = await VerbindeAsync(carl);
            await StaffelAsync();

            await LaufeAsync(a, Zug, "Merkurr");
            Assert.IsTrue(await WarteAsync(() => LaeuftAmTelefon(c)), "Nach Annas Fehler läuft nicht Carl.");

            Assert.IsTrue(await Steuerung.AcceptLastRelayAnswerAsync(), "Die Spielleitung kann Annas Antwort nicht gelten lassen.");
            Assert.IsTrue(await WarteAsync(() => LaeuftAmTelefon(b) && !LaeuftAmTelefon(c)), "Nach dem Gelten lassen läuft nicht Bert für Rot weiter.");
            Assert.IsTrue(await WarteAsync(() => Staffelinfo(a)?.GetProperty("lastAccepted").GetBoolean() == true), "Anna erfährt nicht, dass ihre Antwort gilt.");
            Assert.IsFalse(await Steuerung.AcceptLastRelayAnswerAsync(), "Dieselbe Antwort gilt zweimal.");

            Assert.IsTrue(await Steuerung.PassRelayAsync(), "Die Spielleitung kann nicht weitergeben.");
            Assert.IsTrue(await WarteAsync(() => LaeuftAmTelefon(c) && !LaeuftAmTelefon(b)), "Nach dem Weitergeben läuft nicht Carl.");
            Assert.AreEqual(1, Zahl(staffelAbgaben), "Ein Eingriff der Spielleitung erscheint als Abgabe eines Telefons.");
        }
    }
}
