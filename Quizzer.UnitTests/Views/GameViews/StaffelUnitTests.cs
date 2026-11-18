using LocalBuzzer.Service.Base.States.Minispiele;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.TestShared;
using Quizzer.UnitTests.PlayThrough;
using Quizzer.Views.GameViews;
using System.Windows;
using static Quizzer.Views.GameViews.Sub.PlayerResultContext;

namespace Quizzer.UnitTests.Views.GameViews
{
    /// <summary>
    /// Staffel im Fragefenster (M37 T11), echter Server und Telefone; Rot = Plätze 0 und 1, Blau = 2 und 3. Die Teile sind die drei
    /// Zeilen „Hinweis 1“ bis „Hinweis 3“. Am Beamer stehen die Teams mit ihren Teilen und wer läuft; das Team mit den meisten Teilen
    /// bekommt die Feldpunkte aufgeteilt als Vorschlag (E1, F-M37-06). Die Spielleitung lässt gelten und gibt weiter.
    /// </summary>
    [TestClass]
    public class StaffelUnitTests : TeamregelServerTestBasis
    {
        protected override int Port => TestPorts.Staffel;

        private Task<TestableCurrentQuestionViewModel> OeffneAsync() => OeffneTeamMinispielAsync(MinigameKind.Relay);

        private int Zug => Zustand.Lenke(r => ((RelayRules)r!).Stand.Zug);

        /// <summary>Der Platz, der gerade läuft - welches Mitglied zuerst läuft, sagt die Sprecherfolge am Server.</summary>
        private int Laeufer()
        {
            var id = Zustand.Lenke(r => ((RelayRules)r!).Stand.Laeufer);
            return Enumerable.Range(0, 4).Single(i => Id(i) == id);
        }

        /// <summary>Wer gerade läuft, antwortet - und der Server hat danach gezählt.</summary>
        private async Task<int> LaufeAsync(string antwort)
        {
            var wer = Laeufer();
            var zug = Zug;

            await phones[wer].InvokeAsync("SpielAktion", new
            {
                kind = (int)MinigameKind.Relay,
                durchgang = Zustand.Durchgang,
                payload = new { turn = zug, answer = antwort },
            });

            Assert.IsTrue(await WarteAsync(() => Zug > zug), $"Die Antwort von Platz {wer} wurde nicht gezählt.");
            return wer;
        }

        private static bool IstRot(int platz) => platz is 0 or 1;

        /// <summary>„✚ Rot“ bzw. „★ Blau“ - das Team eines Platzes, wie Beamer und Karten es nennen.</summary>
        private static string Titel(int platz) => IstRot(platz) ? "✚ Rot" : "★ Blau";

        /// <summary>Die Plätze eines Teams.</summary>
        private static List<int> Plaetze(int platz) => IstRot(platz) ? [0, 1] : [2, 3];

        [TestMethod]
        public async Task TheTeamWithTheMostPartsSplitsTheFieldValue()
        {
            var vm = await OeffneAsync();
            var feld = PunkteJetzt(world!.Coordinate, vm);

            await vm.StartMinispielRundeAsync();
            StringAssert.StartsWith(vm.Reaktion.Kopfzeile, "Staffel – Teil 1 von 3", "Die Staffel beginnt am Beamer nicht.");
            Assert.AreEqual(Visibility.Visible, vm.StaffelWeitergebenVisibility, "Während der Staffel fehlt der Knopf zum Weitergeben.");
            Assert.AreEqual(Visibility.Collapsed, vm.StaffelGeltenVisibility, "Ohne abgelehnte Antwort steht der Knopf zum Gelten lassen da.");

            // Das erste Team löst Teil 1; sein zweites Mitglied liegt bei Teil 2 daneben.
            var erster = await LaufeAsync("hinweis 1");
            Assert.IsTrue(await WarteAsync(() => vm.Reaktion.Zeilen.Any(z => z.Name == "Teil 1: Hinweis 1" && z.Zeit == Titel(erster))),
                "Der Beamer zeigt den gelösten Teil nicht.");

            var zweiter = await LaufeAsync("falsch");
            Assert.IsTrue(Plaetze(erster).Contains(zweiter) && zweiter != erster, "Nach dem Treffer lief nicht das andere Mitglied desselben Teams.");

            var andere = IstRot(erster) ? 2 : 0;
            Assert.IsTrue(await WarteAsync(() => vm.Reaktion.Kopfzeile.Contains("läuft für " + Titel(andere))),
                "Nach dem Fehler läuft nicht das andere Team: " + vm.Reaktion.Kopfzeile);
            Assert.IsFalse(vm.Reaktion.Zeilen.Any(z => z.Name.Contains("Hinweis 2")), "Der Beamer zeigt die Lösung des offenen Teils.");
            Assert.IsTrue(await WarteAsync(() => vm.StaffelGeltenVisibility == Visibility.Visible), "Nach dem Fehler fehlt der Knopf zum Gelten lassen.");
            Assert.IsTrue(await WarteAsync(() => serverVm!.BuzzerControlsViewModel!.LivePlayers.Any(p => p.AnswerText == "Teil 2 daneben: falsch")),
                "Die Übersicht des Spielleiters zeigt die abgelehnte Antwort nicht.");

            // Das andere Team löst Teil 2 und 3 - zwei gegen einen.
            Assert.IsTrue(Plaetze(andere).Contains(await LaufeAsync("Hinweis 2")));
            Assert.IsTrue(Plaetze(andere).Contains(await LaufeAsync("Hinweis 3")));

            Assert.IsTrue(await WarteAsync(() => vm.Reaktion.Kopfzeile.StartsWith("Ergebnis")), "Die Staffel endet nicht: " + vm.Reaktion.Kopfzeile);
            Assert.AreEqual($"Ergebnis – {Titel(andere)} gewinnt die Staffel", vm.Reaktion.Kopfzeile);
            Assert.IsTrue(vm.Reaktion.Zeilen.Any(z => z.Name == Titel(andere) && z.Zeit == "2 Teile" && z.Sieger), "Der Sieger ist am Beamer nicht markiert.");
            Assert.IsTrue(vm.Reaktion.Zeilen.Any(z => z.Name == "Teil 3: Hinweis 3"), "Am Ende fehlt die Auflösung.");

            var (s1, s2) = (Plaetze(andere)[0], Plaetze(andere)[1]);
            var (v1, v2) = (Plaetze(erster)[0], Plaetze(erster)[1]);
            Assert.AreEqual(feld, Karte(vm, s1).CurrentScoreManipulation + Karte(vm, s2).CurrentScoreManipulation, "Der Sieger bekommt nicht genau den Feldwert.");
            Assert.AreEqual((ScoreSuggestion.Correction, ScoreSuggestion.Correction), (Karte(vm, s1).Suggestion, Karte(vm, s2).Suggestion));
            Assert.AreEqual((ScoreSuggestion.None, ScoreSuggestion.None), (Karte(vm, v1).Suggestion, Karte(vm, v2).Suggestion), "Der Verlierer bekommt etwas.");
            StringAssert.Contains(Karte(vm, s1).Grund, "gewinnt die Staffel");
            Assert.AreEqual($"{Titel(andere)}: 2 Teile von 3 – gewonnen, selbst 1 gelöst", Karte(vm, s1).ReihenfolgeText);
            Assert.AreEqual($"{Titel(erster)}: 1 Teil von 3 – verloren", Karte(vm, zweiter).ReihenfolgeText);
            Assert.IsTrue(await WarteAsync(() => vm.ShowResultWindowCalls > 0), "Nach dem Ende geht das Bewertungsfenster nicht auf.");
            Assert.AreEqual(Visibility.Collapsed, vm.StaffelWeitergebenVisibility, "Nach dem Ende steht der Knopf zum Weitergeben noch da.");

            // Wiederholen: die gewertete Staffel zählt nicht mehr.
            await vm.WiederholeMinispielRundeAsync();
            Assert.AreEqual(ScoreSuggestion.None, Karte(vm, s1).Suggestion, "Nach dem Wiederholen bleibt der Vorschlag stehen.");
            Assert.AreEqual(string.Empty, Karte(vm, s1).ReihenfolgeText, "Nach dem Wiederholen steht der alte Satz noch da.");
            StringAssert.StartsWith(vm.Reaktion.Kopfzeile, "Staffel – Teil 1 von 3", "Nach dem Wiederholen läuft die Staffel nicht von vorn.");
        }

        [TestMethod]
        public async Task TheGameMasterAcceptsAMissPassesAndAcceptsTheLastMissAfterTheEnd()
        {
            var vm = await OeffneAsync();
            await vm.StartMinispielRundeAsync();

            // Das erste Team tippt sich bei Teil 1 vertan - die Spielleitung lässt es gelten, es läuft weiter.
            var a = await LaufeAsync("Hinweis 11");
            var b = IstRot(a) ? 2 : 0;
            Assert.IsTrue(await WarteAsync(() => vm.StaffelGeltenVisibility == Visibility.Visible));

            await vm.LasseStaffelAntwortGeltenAsync();
            StringAssert.Contains(vm.Reaktion.Kopfzeile, "Teil 2 von 3", "Nach dem Gelten lassen läuft nicht Teil 2: " + vm.Reaktion.Kopfzeile);
            StringAssert.Contains(vm.Reaktion.Kopfzeile, "läuft für " + Titel(a), "Nach dem Gelten lassen läuft nicht dasselbe Team weiter.");
            Assert.AreEqual(Visibility.Collapsed, vm.StaffelGeltenVisibility, "Nach dem Gelten lassen steht der Knopf noch da.");

            var prompt = (RecordingUserPrompt)UserPrompt.Current;
            await vm.LasseStaffelAntwortGeltenAsync();
            StringAssert.Contains(prompt.Informs.LastOrDefault()?.Message, "Gelten lassen geht nur", "Ein zweites Gelten lassen sagt nichts.");

            // Ein Telefon fehlt: die Spielleitung gibt weiter, das andere Team ist mit Teil 2 dran.
            await vm.GibStaffelWeiterAsync();
            StringAssert.Contains(vm.Reaktion.Kopfzeile, "läuft für " + Titel(b), "Nach dem Weitergeben läuft nicht das andere Team: " + vm.Reaktion.Kopfzeile);
            Assert.AreEqual(Visibility.Collapsed, vm.StaffelGeltenVisibility, "Ein Weitergeben lässt sich gelten lassen.");

            // Das andere Team löst Teil 2 und verfehlt Teil 3, das erste auch - Gleichstand.
            Assert.IsTrue(Plaetze(b).Contains(await LaufeAsync("Hinweis 2")));
            Assert.IsTrue(Plaetze(b).Contains(await LaufeAsync("x")));
            Assert.IsTrue(Plaetze(a).Contains(await LaufeAsync("Hinweis drei")));
            Assert.IsTrue(await WarteAsync(() => vm.Reaktion.Kopfzeile.StartsWith("Ergebnis")), "Die Staffel endet nicht: " + vm.Reaktion.Kopfzeile);
            Assert.AreEqual("Ergebnis – Gleichstand – die Teams vorn gewinnen", vm.Reaktion.Kopfzeile);
            Assert.IsTrue(vm.Reaktion.Zeilen.Any(z => z.Name == "Teil 3: Hinweis 3" && z.Zeit == "verfehlt"), "Am Ende fehlt der verfehlte Teil mit Lösung.");

            // Nach dem Ende: die letzte Antwort gilt doch - das erste Team gewinnt, die Karten folgen.
            Assert.IsTrue(await WarteAsync(() => vm.StaffelGeltenVisibility == Visibility.Visible), "Nach dem Ende fehlt der Knopf zum Gelten lassen.");
            await vm.LasseStaffelAntwortGeltenAsync();

            Assert.AreEqual($"Ergebnis – {Titel(a)} gewinnt die Staffel", vm.Reaktion.Kopfzeile);
            Assert.AreEqual((ScoreSuggestion.Correction, ScoreSuggestion.Correction), (Karte(vm, Plaetze(a)[0]).Suggestion, Karte(vm, Plaetze(a)[1]).Suggestion),
                "Der neue Sieger bekommt nichts.");
            Assert.AreEqual((ScoreSuggestion.None, ScoreSuggestion.None), (Karte(vm, Plaetze(b)[0]).Suggestion, Karte(vm, Plaetze(b)[1]).Suggestion),
                "Der bisherige Gleichstand behält seinen Anteil.");
        }
    }
}
