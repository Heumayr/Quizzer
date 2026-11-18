using LocalBuzzer.Service.Base.States;
using LocalBuzzer.Service.Hubs.Accessors;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Models.Base;
using System.Collections.Concurrent;

namespace Quizzer.LogicUnitTests.LocalBuzzer.Base.States
{
    /// <summary>
    /// Wer schließt die Runde (M37 P6, R2-4)? Umstimmen und letzte Abgabe gleichzeitig, oder zwei letzte Abgaben gleichzeitig:
    /// genau eine Abgabe schließt, und keine landet in einer schon geschlossenen Runde. Der Hub feuert das Ereignis nur für
    /// die, die geschlossen hat - früher las er danach <c>Locked</c>, und beide feuerten.
    /// </summary>
    [TestClass]
    public class RundenschlussUnitTests
    {
        private const int Laeufe = 2000;

        private static Player NewPlayer(string name) => new() { Id = Guid.NewGuid(), Designation = name };

        /// <summary>Ein Spiel mit drei Spielern. Den Zugriff darauf ruft der Zustand innen in der Sperre - dort schreibt der
        /// Tastentest den Verlauf mit: sieht ein Aufruf die Runde schon geschlossen, hat er in sie geschrieben.</summary>
        private static (Game Spiel, List<Player> Spieler) Spiel()
        {
            var spieler = new[] { "Anna", "Bert", "Carl" }.Select(NewPlayer).ToList();
            var spiel = new Game { Id = Guid.NewGuid() };

            foreach (var s in spieler)
                spiel.PlayerXGames.Add(new PlayerXGame { Id = Guid.NewGuid(), GameId = spiel.Id, PlayerId = s.Id, Player = s });

            return (spiel, spieler);
        }

        /// <summary>Wie vom Telefon: es schickt <c>committedResult: true</c> mit - verworfen muss der Zustand es zurücknehmen.</summary>
        private static BuzzerKeySelector.SelectionResult Stimme(Player p, string taste) => new() { PlayerId = p.Id, Player = p, SelectedKeys = [taste], CommittedResult = true };

        private static BuzzerInputState.InputResult Tipp(Player p, string wert) => new() { PlayerId = p.Id, Player = p, Value = wert };

        /// <summary>Beide zugleich losschicken; je Lauf ein neues Paar.</summary>
        private static (T A, T B) Gleichzeitig<T>(Func<T> a, Func<T> b)
        {
            using var start = new Barrier(2);
            var ta = Task.Run(() => { start.SignalAndWait(); return a(); });
            var tb = Task.Run(() => { start.SignalAndWait(); return b(); });
            return (ta.Result, tb.Result);
        }

        [TestMethod]
        public void UmstimmenAndTheLastVoteTogetherCloseTheRoundExactlyOnce()
        {
            var falsch = new List<string>();

            for (var lauf = 0; lauf < Laeufe; lauf++)
            {
                var (spiel, spieler) = Spiel();
                var verlauf = new ConcurrentQueue<bool>();
                BuzzerKeySelector? tasten = null;
                tasten = new BuzzerKeySelector(new GameAccessor { GetGame = () => { verlauf.Enqueue(tasten!.Locked); return spiel; } })
                {
                    Infos = new() { KeysAndDesignations = new() { ["A"] = "Rom", ["B"] = "Paris", ["C"] = "Wien" } }
                };

                tasten.SetSelectedKeys(Stimme(spieler[0], "A"));
                tasten.SetSelectedKeys(Stimme(spieler[1], "A"));
                verlauf.Clear();

                var umstimmen = Stimme(spieler[0], "C");
                var (zuCarl, zuAnna) = Gleichzeitig(
                    () => tasten.SetSelectedKeys(Stimme(spieler[2], "B")),
                    () => tasten.SetSelectedKeys(umstimmen));

                var gezaehlt = tasten.KeyResultsForPlayer[spieler[0].Id].SelectedKeys.Single();

                if (zuCarl == zuAnna) falsch.Add($"Lauf {lauf}: {(zuCarl ? "zwei" : "keine")} schlossen die Runde");
                if (verlauf.Any(war => war)) falsch.Add($"Lauf {lauf}: eine Stimme landete in der geschlossenen Runde");
                if (gezaehlt != (umstimmen.CommittedResult ? "C" : "A")) falsch.Add($"Lauf {lauf}: angenommen {umstimmen.CommittedResult}, gezählt {gezaehlt}");
            }

            Assert.AreEqual(0, falsch.Count, $"{falsch.Count} Befunde in {Laeufe} Läufen, etwa: " + string.Join("; ", falsch.Take(3)));
        }

        [TestMethod]
        public void TwoLastInputsTogetherCloseTheRoundExactlyOnce()
        {
            var falsch = 0;

            for (var lauf = 0; lauf < Laeufe; lauf++)
            {
                var (spiel, spieler) = Spiel();
                var eingabe = new BuzzerInputState(new GameAccessor { GetGame = () => spiel });

                eingabe.SetInput(Tipp(spieler[0], "1"));

                var (zuBert, zuCarl) = Gleichzeitig(
                    () => eingabe.SetInput(Tipp(spieler[1], "2"), out var zu) && zu,
                    () => eingabe.SetInput(Tipp(spieler[2], "3"), out var zu) && zu);

                if (zuBert == zuCarl) falsch++;
            }

            Assert.AreEqual(0, falsch, $"In {falsch} von {Laeufe} Läufen schlossen nicht genau ein Tipp die Runde.");
        }
    }
}
