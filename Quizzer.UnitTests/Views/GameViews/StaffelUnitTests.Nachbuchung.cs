using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Logic.Controller.TypedControllers;
using Quizzer.UnitTests.PlayThrough;
using static Quizzer.Views.GameViews.Sub.PlayerResultContext;

namespace Quizzer.UnitTests.Views.GameViews
{
    /// <summary>
    /// V26: „Antwort gelten lassen“ nach dem Ende, wenn die Staffel schon gebucht ist. Der Unterschied zur gebuchten Wertung erscheint
    /// als neuer Vorschlag auf den Karten (E1) - nie als stille zweite Buchung, nie als Gelten lassen, das ins Leere geht.
    /// </summary>
    public partial class StaffelUnitTests
    {
        private async Task<Dictionary<int, int>> GebuchtAsync()
        {
            using var ctrl = new QuestionResultsController();
            var zeilen = await ctrl.GetAllResultsForCoordinate(world!.Coordinate.Id) ?? [];
            return Enumerable.Range(0, 4).ToDictionary(i => i, i => zeilen.Single(r => r.PlayerId == Id(i)).FinalScore);
        }

        [TestMethod]
        public async Task AcceptingAfterTheBookingProposesTheDifference()
        {
            var vm = await OeffneAsync();
            var feld = PunkteJetzt(world!.Coordinate, vm);
            await vm.StartMinispielRundeAsync();

            // Wie oben: das erste Team löst Teil 1 (gelten lassen), das andere Teil 2, beide verfehlen Teil 3 - Gleichstand.
            var a = await LaufeAsync("Hinweis 11");
            Assert.IsTrue(await WarteAsync(() => vm.StaffelGeltenVisibility == System.Windows.Visibility.Visible));
            await vm.LasseStaffelAntwortGeltenAsync();
            await vm.GibStaffelWeiterAsync();
            await LaufeAsync("Hinweis 2");
            await LaufeAsync("x");
            await LaufeAsync("Hinweis drei");
            Assert.IsTrue(await WarteAsync(() => vm.Reaktion.Kopfzeile.StartsWith("Ergebnis")), "Die Staffel endet nicht: " + vm.Reaktion.Kopfzeile);

            Assert.IsTrue(await vm.PlayersResultViewModel!.TrySaveAsync(), "Speichern scheiterte.");
            var gleichstand = await GebuchtAsync();
            Assert.IsTrue(Plaetze(a).Concat(Plaetze(IstRot(a) ? 2 : 0)).All(p => gleichstand[p] > 0), "Beim Gleichstand ist nicht jedem etwas gebucht - der Test misst nichts.");
            var fenster = vm.ShowResultWindowCalls;

            // Nach der Buchung: die letzte Antwort gilt doch - das erste Team gewinnt allein.
            Assert.IsTrue(await WarteAsync(() => vm.StaffelGeltenVisibility == System.Windows.Visibility.Visible), "Nach dem Ende fehlt der Knopf zum Gelten lassen.");
            await vm.LasseStaffelAntwortGeltenAsync();
            Assert.AreEqual($"Ergebnis – {Titel(a)} gewinnt die Staffel", vm.Reaktion.Kopfzeile);

            var verlierer = Plaetze(IstRot(a) ? 2 : 0);
            Assert.IsTrue(verlierer.All(p => Karte(vm, p).Suggestion == ScoreSuggestion.Correction && Karte(vm, p).CurrentScoreManipulation == -gleichstand[p]),
                "Das andere Team bekommt seinen gebuchten Anteil nicht als Gegenvorschlag: " + string.Join(", ", verlierer.Select(p => Karte(vm, p).SuggestionText)));
            Assert.IsTrue(vm.ShowResultWindowCalls > fenster, "Der neue Vorschlag steht auf Karten, die niemand sieht.");

            vm.SchliessenBucht = true;
            await vm.ClosedForTestAsync();

            var ende = await GebuchtAsync();
            Assert.AreEqual(feld, Plaetze(a).Sum(p => ende[p]), "Der Sieger hat nicht genau den Feldwert.");
            Assert.AreEqual(0, verlierer.Sum(p => ende[p]), "Das andere Team behält Punkte aus dem Gleichstand.");
        }
    }
}
