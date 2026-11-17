using LocalBuzzer.Service.Base.States;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Questions.Teams;
using Quizzer.TestShared;
using Quizzer.UnitTests.PlayThrough;
using Quizzer.Views.GameViews.Teamregeln;
using System.Windows;
using static Quizzer.Views.GameViews.Sub.PlayerResultContext;

namespace Quizzer.UnitTests.Views.GameViews
{
    /// <summary>
    /// „Gleich gedacht" im Fragefenster (M37 T6), echter Server und Telefone: jedes Mitglied schreibt, immer per Text; erst wenn
    /// die Antworten zu sind, wird verglichen - dann tragen die Karten die Anteile, und der Beamer zeigt je Team die Gruppen. Nur
    /// die Übereinstimmung zählt (F-M37-12), nie über Teamgrenzen; die Spielleitung legt Gruppen eines Teams zusammen (E1).
    /// </summary>
    [TestClass]
    public class GleichGedachtUnitTests : TeamregelServerTestBasis
    {
        protected override int Port => TestPorts.GleichGedacht;

        private Task<TestableCurrentQuestionViewModel> OeffneAsync(int spieler = 4, QuestionType typ = QuestionType.Default)
            => OeffneTeamregelAsync(CellSpecialRule.SameThought, spieler, typ);

        private IReadOnlyDictionary<Guid, int> AnteileRot(params int[] plaetze)
            => Aufteilung.Verteile(world!.Coordinate.CurrentPoints, plaetze.Select(Id));

        private async Task ZuAsync(TestableCurrentQuestionViewModel vm)
            => Assert.IsTrue(await WarteAsync(() => vm.GleichGedachtVisibility == Visibility.Visible), "Nach der letzten Antwort wird nicht verglichen.");

        [TestMethod]
        public async Task EveryMemberWritesAndOnlyTheMatchWithinTheTeamCounts()
        {
            rotePlaetze = [0, 1, 2];
            var vm = await OeffneAsync(spieler: 5);
            var lang = new string('W', 30 * 1024);

            Assert.AreEqual(BuzzerControlsLayout.Input, vm.CurrentBuzzerLayout);
            Assert.IsTrue(await WarteAsync(() => Hub.CurrentLayout == BuzzerControlsLayout.Input), "Die Telefone zeigen keine Eingabe.");
            Assert.AreEqual(GleichGedachtAblauf.Eingabe, Hub.BuzzerInputState.Infos.Placeholder);
            Assert.AreEqual(5, Hub.AnswerLock.Answerers?.Count, "Nicht jedes Mitglied darf schreiben.");
            Assert.IsTrue(await WarteAsync(() => Stand(1) is { ExcludedFromQuestion: false, CurrentLayoutLocked: false }), "Ein Mitglied, das nicht spricht, ist gesperrt.");
            Assert.AreEqual("Teamregel: Gleich gedacht", vm.Sonderregel.Titel);
            Assert.AreEqual("Antwortet für Rot", Karte(vm, 1).SonderregelText);

            await SchreibeAsync(vm, 0, "Paris");
            await SchreibeAsync(vm, 1, "  paris");
            await SchreibeAsync(vm, 2, "Rom");
            await SchreibeAsync(vm, 3, "Paris");

            // --- Vor dem Vergleich: am Beamer nichts, auf den Karten nichts ---
            Assert.AreEqual(Visibility.Collapsed, vm.GleichGedachtVisibility);
            Assert.AreEqual(0, vm.Sonderregel.Aufloesung.Count, "Der Beamer zeigt Antworten, bevor alle abgegeben haben.");
            Assert.AreEqual(0, Karte(vm, 0).CurrentScoreManipulation, "Vor dem Vergleich steht ein Anteil auf der Karte.");

            await SchreibeAsync(vm, 4, lang);
            await ZuAsync(vm);

            // --- Rot: das Paar bekommt seinen Anteil am ganzen Team, Rom allein nichts; Blau: Paris zählt nicht mit Rots Paris ---
            var anteile = AnteileRot(0, 1, 2);
            CollectionAssert.AreEqual(new[] { anteile[Id(0)], anteile[Id(1)], 0, 0, 0 }, Enumerable.Range(0, 5).Select(i => Karte(vm, i).CurrentScoreManipulation).ToArray(),
                "Nicht genau das Paar aus Rot bekommt seinen Anteil.");
            Assert.AreEqual(ScoreSuggestion.Correction, Karte(vm, 0).Suggestion, "Die unbewertete Karte trägt den Anteil nicht als Vorschlag.");
            StringAssert.Contains(Karte(vm, 0).SuggestionText, "Gleich gedacht Rot");

            // --- Der Beamer: je Team eine Zeile, Teamtexte höchstens 80 Zeichen ---
            CollectionAssert.AreEqual(new[]
                {
                    $"✚ Rot – gleich gedacht: „Paris“ / „paris“ ({Name(0)}, {Name(1)}) · außerdem: „Rom“ ({Name(2)})",
                    $"★ Blau – keine Übereinstimmung: „Paris“ ({Name(3)}) · „{BuzzerInputState.Kuerze(lang)}“ ({Name(4)})",
                },
                vm.Sonderregel.Aufloesung.ToArray());
            Assert.AreEqual(BuzzerInputState.MaxLength, Karte(vm, 4).InputResult!.Value!.Length, "Der 30-KB-Text kam ungekürzt an.");
            Assert.AreEqual(4, vm.GleichGedachtGruppen.Count);
            StringAssert.StartsWith(vm.Sonderregel.Stand, "Verglichen: in 1 von 2 Teams");

            await vm.ClosedForTestAsync();
            Assert.AreEqual(Visibility.Collapsed, vm.GleichGedachtVisibility, "Nach der Frage steht die Gruppenliste noch.");
            Assert.AreEqual(0, vm.Sonderregel.Aufloesung.Count, "Nach der Frage steht die Auflösung noch am Beamer.");
            TestEnvironment.ThrowIfAnythingWasSwallowed();
        }

        /// <summary>E1: „Pariss" zu „Paris" gelegt - das Paar zählt; über Teamgrenzen nie; Aufheben nimmt es zurück.</summary>
        [TestMethod]
        public async Task TheMasterMergesGroupsOfOneTeamOnly()
        {
            var vm = await OeffneAsync();
            await SchreibeAsync(vm, 0, "Paris");
            await SchreibeAsync(vm, 1, "Pariss");
            await SchreibeAsync(vm, 2, "Pariss ");
            await SchreibeAsync(vm, 3, "Wien");
            await ZuAsync(vm);

            Assert.AreEqual(0, Karte(vm, 0).CurrentScoreManipulation + Karte(vm, 1).CurrentScoreManipulation, "Ohne Zusammenlegen bekommt Rot etwas.");
            var rotPaar = vm.GleichGedachtGruppen.Where(e => e.TeamId == rot.Id).ToList();
            var blauPariss = vm.GleichGedachtGruppen.First(e => e.TeamId == blau.Id);

            // Blaus „Pariss" heißt wie Rots - zusammengelegt würde sonst Rots Paar, das niemand angehakt hat.
            Assert.IsFalse(vm.LegeGruppenZusammen([rotPaar[0], blauPariss]), "Gruppen verschiedener Teams wurden zusammengelegt.");
            Assert.IsFalse(vm.LegeGruppenZusammen([rotPaar[0]]), "Eine Gruppe allein wurde zusammengelegt.");
            Assert.AreEqual(0, Karte(vm, 0).CurrentScoreManipulation + Karte(vm, 1).CurrentScoreManipulation, "Ein abgelehntes Zusammenlegen wirkt.");

            rotPaar.ForEach(e => e.Markiert = true);
            vm.GruppenZusammenlegenCommand.Execute(null);

            var anteile = AnteileRot(0, 1);
            Assert.AreEqual(anteile[Id(0)], Karte(vm, 0).CurrentScoreManipulation, "Das Zusammenlegen wirkt nicht auf die Karte.");
            Assert.AreEqual(world!.Coordinate.CurrentPoints, Karte(vm, 0).CurrentScoreManipulation + Karte(vm, 1).CurrentScoreManipulation);
            StringAssert.Contains(vm.Sonderregel.Aufloesung[0], "gleich gedacht: „Paris“ / „Pariss“", "Der Beamer zeigt das Zusammenlegen nicht.");
            Assert.AreEqual(3, vm.GleichGedachtGruppen.Count, "Die Liste zeigt die zusammengelegten Gruppen noch einzeln.");
            Assert.AreEqual(0, Karte(vm, 2).CurrentScoreManipulation, "Blaus „Pariss“ zählt mit Rots Gruppe.");

            Assert.IsTrue(vm.HebeZusammenlegenAuf());
            Assert.AreEqual(0, Karte(vm, 0).CurrentScoreManipulation, "Nach dem Aufheben bleibt der Anteil stehen.");
            Assert.IsFalse(vm.HebeZusammenlegenAuf(), "Ohne Zusammengelegtes gab es etwas aufzuheben.");
        }

        /// <summary>F-M37-12: nur die Übereinstimmung zählt - ein richtig bewerteter Einzelner bekommt 0, ein falsch bewertetes Paar seinen Anteil.</summary>
        [TestMethod]
        public async Task TheRatingDoesNotCountOnlyTheMatch()
        {
            var vm = await OeffneAsync();
            await SchreibeAsync(vm, 0, "Paris");
            await SchreibeAsync(vm, 1, "Rom");
            await SchreibeAsync(vm, 2, "Wien");
            await SchreibeAsync(vm, 3, "wien");
            await ZuAsync(vm);

            Karte(vm, 0).Suggestion = ScoreSuggestion.Right;
            Karte(vm, 2).Suggestion = ScoreSuggestion.Wrong;

            Assert.AreEqual(0, Karte(vm, 0).CurrentScoreManipulation, "Ohne Übereinstimmung zählt die Bewertung.");
            StringAssert.Contains(Karte(vm, 0).SuggestionText, "keine Übereinstimmung");
            Assert.AreEqual(Aufteilung.Verteile(world!.Coordinate.CurrentPoints, [Id(2), Id(3)])[Id(2)], Karte(vm, 2).CurrentScoreManipulation,
                "Ein falsch bewertetes Paar verliert seinen Anteil - Gleich gedacht kennt kein Minus.");
        }

        /// <summary>A01: ein per Joker Gesperrter schreibt umsonst; eine fremde Kennung in der Nutzlast zählt nicht - es gilt die Verbindung.</summary>
        [TestMethod]
        public async Task ABlockedMemberWritesInVainAndAForeignIdInThePayloadIsIgnored()
        {
            jokerFuer = (2, JokerKind.Block);
            var vm = await OeffneAsync();
            Assert.IsTrue(vm.VorphaseLaeuft, "Vor Gleich gedacht lief keine Joker-Runde.");

            await JokerAsync(2, joker!.Id, ziel: Id(0));
            await NachDerJokerRundeAsync(vm, antwortende: 3);
            Assert.IsFalse(Hub.AnswerLock.Allows(Id(0)), "Der Gesperrte darf schreiben.");
            Assert.IsTrue(await WarteAsync(() => Stand(0)?.ExcludedReason?.Contains("per Joker gesperrt") == true), "Der Gesperrte liest nicht, warum.");
            StringAssert.StartsWith(Karte(vm, 0).SonderregelText, "Antwortet diesmal nicht");

            // Ohne Sprecher dieser Frage bleibt das Banner beim Sprecher des Stands - es rückt niemand nach.
            await Task.Delay(200);
            Assert.AreEqual(Name(0), Stand(1)?.Team?.Speaker, "Das Telefon nennt einen Sprecher dieser Frage, obwohl jeder selbst schreibt.");

            await phones[0].InvokeAsync("SubmitInput", new { value = "Paris" });
            await phones[1].InvokeAsync("SubmitInput", new { playerId = Id(0), value = "Paris" });
            await SchreibeAsync(vm, 2, "Rom");
            await SchreibeAsync(vm, 3, "Rom");
            await ZuAsync(vm);

            Assert.IsNull(Karte(vm, 0).InputResult, "Die Antwort des Gesperrten kam an - oder die fremde Kennung zählte.");
            Assert.AreEqual("Paris", Karte(vm, 1).InputResult?.Value, "Die Antwort zählt nicht für den, der sie schickte.");
            Assert.AreEqual(0, Karte(vm, 0).CurrentScoreManipulation + Karte(vm, 1).CurrentScoreManipulation, "Rot hat mit einem Gesperrten gleich gedacht.");
            Assert.IsTrue(Karte(vm, 2).CurrentScoreManipulation > 0, "Blau bekommt für sein Paar nichts.");
        }

        /// <summary>Auch wo die Frage Tasten hat, wird geschrieben - verglichen werden Texte.</summary>
        [TestMethod]
        public async Task AQuestionWithKeysIsStillWritten()
        {
            var vm = await OeffneAsync(typ: QuestionType.MultipleChoice);

            Assert.AreEqual(BuzzerControlsLayout.Input, vm.CurrentBuzzerLayout, "Bei Gleich gedacht wählt das Telefon Tasten.");
            Assert.IsTrue(await WarteAsync(() => Hub.CurrentLayout == BuzzerControlsLayout.Input));
            Assert.AreEqual(4, Hub.AnswerLock.Answerers?.Count);
        }

        /// <summary>Erst die Teamregel, dann der Joker (Entwurf Abschnitt 3): „Doppelt" verdoppelt den eigenen Anteil am Paar.</summary>
        [TestMethod]
        public async Task ADoubleJokerDoublesTheOwnShareOfTheMatch()
        {
            jokerFuer = (1, JokerKind.Double);
            var vm = await OeffneAsync();

            await JokerAsync(1, joker!.Id);
            await NachDerJokerRundeAsync(vm, antwortende: 4);
            await SchreibeAsync(vm, 0, "Paris");
            await SchreibeAsync(vm, 1, "Paris");
            await SchreibeAsync(vm, 2, "Rom");
            await SchreibeAsync(vm, 3, "Wien");
            await ZuAsync(vm);

            var anteile = AnteileRot(0, 1);
            Assert.AreEqual(2 * anteile[Id(1)], Karte(vm, 1).CurrentScoreManipulation, "Der Joker verdoppelt nicht den eigenen Anteil.");
            Assert.AreEqual(anteile[Id(0)], Karte(vm, 0).CurrentScoreManipulation, "Der Joker eines anderen wirkt auf den Partner.");
            StringAssert.Contains(Karte(vm, 1).SuggestionText, "Joker");
        }
    }
}
