using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Buzzer;
using Quizzer.DataModels.Questions.Teams;
using Quizzer.TestShared;
using Quizzer.UnitTests.PlayThrough;
using Quizzer.Views.GameViews;
using System.Windows;
using static Quizzer.Views.GameViews.Sub.PlayerResultContext;

namespace Quizzer.UnitTests.Views.GameViews
{
    /// <summary>
    /// Team-Abstimmung im Fragefenster (M37 T7), echter Server und Telefone, Rot = Plätze 0-2, Blau = 3-4: jedes Mitglied wählt
    /// eine Taste, die Mehrheit im Team ist seine Antwort - bei Gleichstand die Stimme des Sprechers, sonst entscheidet die
    /// Spielleitung (E1). Ohne Stimme nicht bewertet. Geheim bis zum Schluss (F-M37-13): vorher sieht jedes Telefon nur die eigene
    /// Stimme und den Stand seines Teams, danach die Antwort seines Teams; alle Teams zeigt nur der Beamer.
    /// </summary>
    [TestClass]
    public class TeamAbstimmungUnitTests : TeamregelServerTestBasis
    {
        protected override int Port => TestPorts.TeamAbstimmung;

        private async Task<TestableCurrentQuestionViewModel> OeffneAsync()
        {
            rotePlaetze = [0, 1, 2];
            var vm = await OeffneTeamregelAsync(CellSpecialRule.TeamVote, spieler: 5, typ: QuestionType.MultipleChoice);

            Assert.AreEqual(BuzzerControlsLayout.KeySelect, vm.CurrentBuzzerLayout);
            Assert.IsTrue(await WarteAsync(() => Hub.CurrentLayout == BuzzerControlsLayout.KeySelect), "Die Telefone zeigen keine Tastenwahl.");

            // Taste B ist die richtige - die Karte schlägt dann von selbst „richtig" vor.
            vm.PlayersResultViewModel!.Coordinate!.QuestionBase!.Steps.Single(s => s.QuestionViewKey == "B").IsResult = true;
            return vm;
        }

        private async Task StimmeAsync(TestableCurrentQuestionViewModel vm, int telefon, string taste, int? platz = null)
        {
            await phones[telefon].InvokeAsync("SelectionResults", new { selectedKeys = new[] { taste } });
            Assert.IsTrue(await WarteAsync(() => Karte(vm, platz ?? telefon).SelectionResult?.SelectedKeys.SequenceEqual([taste]) == true),
                $"Die Stimme von Telefon {telefon} kam nicht auf die Karte.");
        }

        private async Task ZuAsync(TestableCurrentQuestionViewModel vm)
            => Assert.IsTrue(await WarteAsync(() => vm.Sonderregel.Aufloesung.Count == 2), "Nach dem Schluss wird nicht ausgewertet.");

        private static TeamVoteClientInfo? Abstimmung(ClientLayoutStateDto? stand) => stand?.Team?.Vote;

        /// <summary>Was ein Telefon je an Antworten eines Teams gelesen hat.</summary>
        private List<string> Gelesen(int telefon)
        {
            lock (staende[telefon])
                return staende[telefon].Select(s => Abstimmung(s)?.Result ?? string.Empty).Where(r => r.Length > 0).Distinct().ToList();
        }

        private int[] Punkte(TestableCurrentQuestionViewModel vm, params int[] plaetze) => plaetze.Select(i => Karte(vm, i).CurrentScoreManipulation).ToArray();

        private int[] Anteile(int wert, params int[] plaetze)
        {
            var anteile = Aufteilung.Verteile(wert, plaetze.Select(Id));
            return plaetze.Select(i => anteile[Id(i)]).ToArray();
        }

        [TestMethod]
        public async Task TheMajorityIsTheTeamsAnswerAndOnlyTheBeamerShowsAllTeams()
        {
            var vm = await OeffneAsync();

            Assert.AreEqual(5, Hub.AnswerLock.Answerers?.Count, "Nicht jedes Mitglied darf abstimmen.");
            Assert.AreEqual("Teamregel: Team-Abstimmung", vm.Sonderregel.Titel);
            Assert.AreEqual("Stimmt ab für Rot", Karte(vm, 1).SonderregelText);
            Assert.IsTrue(await WarteAsync(() => Abstimmung(Stand(1)) is { Voted: 0, Of: 3, Closed: false }), "Das Telefon zeigt den Stand von Rot nicht.");

            await StimmeAsync(vm, 0, "B");
            await StimmeAsync(vm, 1, "B");
            await StimmeAsync(vm, 2, "A");
            await StimmeAsync(vm, 3, "A");

            // --- Vor dem Schluss: jedes Telefon die eigene Stimme und den Stand seines Teams; Beamer und Karten nichts ---
            Assert.IsTrue(await WarteAsync(() => Abstimmung(Stand(2)) is { Voted: 3, Of: 3 }), "Rot sieht nicht, dass alle abgestimmt haben.");
            CollectionAssert.AreEqual(new[] { "A" }, Abstimmung(Stand(2))!.Own, "Platz 2 sieht nicht seine eigene Stimme.");
            Assert.IsTrue(await WarteAsync(() => Abstimmung(Stand(4)) is { Voted: 1, Of: 2 }), "Blau zählt nicht nur Blau.");
            Assert.AreEqual(0, Abstimmung(Stand(4))!.Own.Count, "Platz 4 sieht eine Stimme, die nicht seine ist.");
            Assert.AreEqual(0, vm.Sonderregel.Aufloesung.Count, "Der Beamer zeigt Antworten vor dem Schluss.");
            Assert.AreEqual(0, Karte(vm, 0).CurrentScoreManipulation, "Vor dem Schluss steht die eigene Wertung als Anteil auf der Karte.");
            StringAssert.Contains(Karte(vm, 0).SuggestionText, "die Abstimmung läuft");

            await StimmeAsync(vm, 4, "C");
            await ZuAsync(vm);

            // --- Der Beamer: alle Teams. Blau: Gleichstand A/C, der Sprecher (Platz 3) stimmte A ---
            CollectionAssert.AreEqual(new[]
                {
                    "✚ Rot – B (2 von 3)",
                    "★ Blau – A (1 von 2) – Gleichstand, die Stimme des Sprechers zählt",
                },
                vm.Sonderregel.Aufloesung.ToArray());
            Assert.AreEqual(Name(3), Stand(4)?.Team?.Speaker, "Das Telefon nennt nicht den Sprecher, dessen Stimme zählt.");

            // --- Rot: B ist richtig, der Feldwert geht aufgeteilt an alle - auch an den, der A wählte ---
            CollectionAssert.AreEqual(Anteile(world!.Coordinate.CurrentPoints, 0, 1, 2), Punkte(vm, 0, 1, 2), "Rot bekommt nicht den aufgeteilten Feldwert.");
            Assert.AreEqual($"Stimme: A · Rot: B (2 von 3)", Karte(vm, 2).SonderregelText);

            // --- Blau: A ist nicht von selbst richtig - erst eine Wertung einer A-Karte zählt, nicht die einer C-Karte ---
            Assert.AreEqual(0, Karte(vm, 3).CurrentScoreManipulation);
            Karte(vm, 4).Suggestion = ScoreSuggestion.Wrong;
            CollectionAssert.AreEqual(new[] { 0, 0 }, Punkte(vm, 3, 4), "Die Wertung einer Minderheitsstimme gilt fürs Team.");
            StringAssert.Contains(Karte(vm, 4).SuggestionText, "noch nicht bewertet");
            Karte(vm, 3).Suggestion = ScoreSuggestion.Wrong;
            CollectionAssert.AreEqual(Anteile(-world.Coordinate.CurrentMinusPoints, 3, 4), Punkte(vm, 3, 4), "Blau verliert nicht das aufgeteilte Minus.");

            // --- Die Telefone: jedes nur die Antwort seines Teams, zu keinem Zeitpunkt die des anderen ---
            Assert.IsTrue(await WarteAsync(() => Abstimmung(Stand(0))?.Result == "B (2 von 3)"), "Rot liest seine Antwort nicht: " + Abstimmung(Stand(0))?.Result);
            Assert.IsTrue(await WarteAsync(() => Abstimmung(Stand(4))?.Result == "A (1 von 2) – Gleichstand, die Stimme des Sprechers zählt"),
                "Blau liest seine Antwort nicht: " + Abstimmung(Stand(4))?.Result);

            foreach (var rot in new[] { 0, 1, 2 })
                CollectionAssert.AreEqual(new[] { "B (2 von 3)" }, Gelesen(rot), $"Telefon {rot} (Rot) las: " + string.Join(" | ", Gelesen(rot)));
            foreach (var blau in new[] { 3, 4 })
                Assert.IsFalse(Gelesen(blau).Any(r => r.Contains("B (")), $"Telefon {blau} (Blau) las die Antwort von Rot.");

            await vm.ClosedForTestAsync();
            Assert.AreEqual(0, vm.Sonderregel.Aufloesung.Count, "Nach der Frage steht die Auflösung noch am Beamer.");
            Assert.IsFalse(Hub.TeamVote.Active, "Nach der Frage läuft die Abstimmung am Hub weiter.");
            TestEnvironment.ThrowIfAnythingWasSwallowed();
        }

        /// <summary>Gleichstand ohne Sprecherstimme: nichts ist bewertet, bis die Spielleitung wählt - die Telefone erfahren es.</summary>
        [TestMethod]
        public async Task ATieWithoutTheSpeakersVoteIsDecidedByTheMaster()
        {
            var vm = await OeffneAsync();

            await StimmeAsync(vm, 1, "A");
            await StimmeAsync(vm, 2, "B");
            await StimmeAsync(vm, 3, "B");
            await serverVm!.BuzzerControlsViewModel!.CloseRoundAsync(null);
            await ZuAsync(vm);

            CollectionAssert.AreEqual(new[] { "✚ Rot – Gleichstand A / B – die Spielleitung entscheidet", "★ Blau – B (1 von 2)" }, vm.Sonderregel.Aufloesung.ToArray());
            Assert.AreEqual(Visibility.Visible, vm.TeamAbstimmungVisibility);
            CollectionAssert.AreEqual(new[] { $"✚ Rot: A ({Name(1)})", $"✚ Rot: B ({Name(2)})" }, vm.TeamAbstimmungGleichstaende.Select(e => e.Beschriftung).ToArray());
            CollectionAssert.AreEqual(new[] { 0, 0, 0 }, Punkte(vm, 0, 1, 2), "Ein offener Gleichstand bewegt Punkte.");
            StringAssert.Contains(Karte(vm, 2).SuggestionText, "die Spielleitung entscheidet");
            Assert.IsTrue(await WarteAsync(() => Abstimmung(Stand(0))?.Result == "Gleichstand A / B – die Spielleitung entscheidet"));
            CollectionAssert.AreEqual(Anteile(world!.Coordinate.CurrentPoints, 3, 4), Punkte(vm, 3, 4), "Blau bekommt den Feldwert nicht.");

            // --- Die Spielleitung wählt B: Rot hat eine Antwort, die richtige B-Karte zählt ---
            Assert.IsTrue(await vm.EntscheideTeamAbstimmungAsync(vm.TeamAbstimmungGleichstaende[1]));

            Assert.AreEqual("✚ Rot – B (1 von 3) – Gleichstand, die Spielleitung hat entschieden", vm.Sonderregel.Aufloesung[0]);
            Assert.AreEqual($"✚ Rot: B ({Name(2)}) – gilt", vm.TeamAbstimmungGleichstaende[1].Beschriftung);
            CollectionAssert.AreEqual(Anteile(world.Coordinate.CurrentPoints, 0, 1, 2), Punkte(vm, 0, 1, 2), "Nach dem Entscheid bekommt Rot nichts.");
            Assert.IsTrue(await WarteAsync(() => Abstimmung(Stand(1))?.Result == "B (1 von 3) – Gleichstand, die Spielleitung hat entschieden"),
                "Das Telefon erfährt den Entscheid nicht: " + Abstimmung(Stand(1))?.Result);

            // --- Und zurück auf A: die Spielleitung hat das letzte Wort, die Karte von A ist unbewertet ---
            Assert.IsTrue(await vm.EntscheideTeamAbstimmungAsync(vm.TeamAbstimmungGleichstaende[0]));
            CollectionAssert.AreEqual(new[] { 0, 0, 0 }, Punkte(vm, 0, 1, 2), "Eine unbewertete A-Karte bringt Rot Punkte.");
            TestEnvironment.ThrowIfAnythingWasSwallowed();
        }

        /// <summary>Was nicht unter den Gleichauf ist, kann die Spielleitung nicht wählen - und ein Team ohne Gleichstand gar nichts.</summary>
        [TestMethod]
        public async Task TheMasterCanOnlyChooseAmongTheTied()
        {
            var vm = await OeffneAsync();
            await StimmeAsync(vm, 1, "A");
            await StimmeAsync(vm, 2, "B");
            await StimmeAsync(vm, 3, "B");
            await serverVm!.BuzzerControlsViewModel!.CloseRoundAsync(null);
            await ZuAsync(vm);

            Assert.IsFalse(await vm.EntscheideTeamAbstimmungAsync(new TeamAbstimmungEintrag(rot.Id, "C", "C")),
                "Eine Antwort, für die niemand stimmte, ließ sich wählen.");
            Assert.IsFalse(await vm.EntscheideTeamAbstimmungAsync(new TeamAbstimmungEintrag(blau.Id, "A", "A")), "Ein Team ohne Gleichstand ließ sich überstimmen.");
            Assert.AreEqual("★ Blau – B (1 von 2)", vm.Sonderregel.Aufloesung[1]);
        }

        /// <summary>Ohne Stimme wird das Team nicht bewertet - auch nicht, wenn die Spielleitung eine Karte bewertet.</summary>
        [TestMethod]
        public async Task ATeamWithoutAVoteIsNotRated()
        {
            var vm = await OeffneAsync();
            await StimmeAsync(vm, 0, "B");
            await serverVm!.BuzzerControlsViewModel!.CloseRoundAsync(null);
            await ZuAsync(vm);

            Assert.AreEqual("★ Blau – keine Stimme – nicht bewertet", vm.Sonderregel.Aufloesung[1]);
            Karte(vm, 3).Suggestion = ScoreSuggestion.Right;

            CollectionAssert.AreEqual(new[] { 0, 0 }, Punkte(vm, 3, 4), "Ein Team ohne Stimme bekommt Punkte.");
            StringAssert.Contains(Karte(vm, 3).SuggestionText, "keine Stimme – nicht bewertet");
            CollectionAssert.AreEqual(Anteile(world!.Coordinate.CurrentPoints, 0, 1, 2), Punkte(vm, 0, 1, 2), "Rot mit einer Stimme bekommt nichts.");
        }

        /// <summary>
        /// Die Gegenprobe aus dem Entwurf: ein Gegnertelefon verbindet mitten in der Runde neu und bekommt den vollen Zustand - darin
        /// nichts von Rot; nach dem Schluss und erneutem Verbinden nur die Antwort von Blau.
        /// </summary>
        [TestMethod]
        public async Task AnOpponentPhoneReconnectingMidRoundLearnsNothingOfTheOtherTeam()
        {
            var vm = await OeffneAsync();
            await StimmeAsync(vm, 0, "B");
            await StimmeAsync(vm, 1, "C");

            await VerbindeAsync(world!.Players[3]);
            Assert.IsTrue(await WarteAsync(() => Stand(5) != null), "Das neu verbundene Telefon bekam keinen Zustand.");
            Assert.IsTrue(Abstimmung(Stand(5)) is { Voted: 0, Of: 2, Closed: false, Result: "" }, "Das Gegnertelefon liest nach dem Neuverbinden mehr als Blau.");
            Assert.AreEqual(0, Abstimmung(Stand(5))!.Own.Count);

            await StimmeAsync(vm, 5, "A", platz: 3);
            await StimmeAsync(vm, 4, "A");
            await StimmeAsync(vm, 2, "B");
            await ZuAsync(vm);

            Assert.IsTrue(await WarteAsync(() => Abstimmung(Stand(5))?.Result == "A (2 von 2)"), "Blau liest seine Antwort nicht: " + Abstimmung(Stand(5))?.Result);

            await VerbindeAsync(world.Players[4]);
            Assert.IsTrue(await WarteAsync(() => Stand(6) != null));
            Assert.AreEqual("A (2 von 2)", Abstimmung(Stand(6))?.Result, "Nach dem Neuverbinden liest Blau eine fremde Antwort.");
            CollectionAssert.AreEqual(new[] { "A" }, Abstimmung(Stand(6))!.Own);

            foreach (var blau in new[] { 3, 4, 5, 6 })
                Assert.IsFalse(Gelesen(blau).Any(r => r.Contains("B (")), $"Telefon {blau} (Blau) las die Antwort von Rot: " + string.Join(" | ", Gelesen(blau)));
        }

        /// <summary>Ein getrennter Sprecher stimmt nicht ab und zählt nicht mit - die Telefone nennen den, dessen Stimme zählt.</summary>
        [TestMethod]
        public async Task ADisconnectedSpeakerPassesTheTieBreakToTheNext()
        {
            FensterEinrichten = async _ =>
            {
                await phones[3].DisposeAsync();
                Assert.IsTrue(await WarteAsync(() => world!.Players[3].ConnectionState == PlayerConnection.Disconnected), "Das Telefon trennt nicht.");
            };

            var vm = await OeffneAsync();

            Assert.AreEqual(4, Hub.AnswerLock.Answerers?.Count, "Ein getrennter Sprecher darf abstimmen.");
            Assert.IsTrue(await WarteAsync(() => Stand(4)?.Team?.Speaker == Name(4)), "Das Telefon nennt den getrennten Sprecher: " + Stand(4)?.Team?.Speaker);
            Assert.IsTrue(await WarteAsync(() => Abstimmung(Stand(4)) is { Of: 1 }), "Der getrennte Sprecher zählt mit.");
            Assert.AreEqual("Stimmt diesmal nicht ab", Karte(vm, 3).SonderregelText);
        }
    }
}
