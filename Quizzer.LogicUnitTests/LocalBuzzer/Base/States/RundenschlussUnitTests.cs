using LocalBuzzer.Service.Base;
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

        /// <summary>
        /// Die Spielleitung schließt die Runde (<c>LockAll</c>), während Annas Abgabe innen ausgewertet wird (M37 P8, P6-1):
        /// die Runde bleibt zu, und Berts spätere Abgabe kommt nicht mehr an. Früher schrieb <c>LockAll</c> außerhalb der
        /// Sperre, und die Auswertung setzte danach <c>Locked = false</c> (eine von drei Abgaben).
        /// </summary>
        [TestMethod]
        public void ClosingWhileAVoteIsEvaluatedKeepsTheRoundClosed()
        {
            var (spiel, spieler) = Spiel();
            var tasten = (BuzzerKeySelector)SchliessenMittenInDerAbgabe(spiel,
                zugriff => new BuzzerKeySelector(zugriff) { Infos = new() { KeysAndDesignations = new() { ["A"] = "Rom", ["B"] = "Paris" } } },
                zustand => ((BuzzerKeySelector)zustand).SetSelectedKeys(Stimme(spieler[0], "A")));

            Assert.IsTrue(tasten.Locked, "Die Auswertung der Abgabe hat das Schließen der Spielleitung aufgehoben.");

            var spaeter = Stimme(spieler[1], "B");
            tasten.SetSelectedKeys(spaeter);
            Assert.IsFalse(spaeter.CommittedResult, "Nach dem Schließen kam noch eine Stimme an.");
        }

        /// <summary>Wie <see cref="ClosingWhileAVoteIsEvaluatedKeepsTheRoundClosed"/>, für den Tipp.</summary>
        [TestMethod]
        public void ClosingWhileAnInputIsEvaluatedKeepsTheRoundClosed()
        {
            var (spiel, spieler) = Spiel();
            var eingabe = SchliessenMittenInDerAbgabe(spiel,
                zugriff => new BuzzerInputState(zugriff),
                zustand => ((BuzzerInputState)zustand).SetInput(Tipp(spieler[0], "1")));

            Assert.IsTrue(eingabe.Locked, "Die Auswertung des Tipps hat das Schließen der Spielleitung aufgehoben.");
            Assert.IsFalse(((BuzzerInputState)eingabe).SetInput(Tipp(spieler[1], "2")), "Nach dem Schließen kam noch ein Tipp an.");
        }

        /// <summary>
        /// Hält die Abgabe innen an - am Spielzugriff, den der Zustand in der Sperre ruft -, schließt von einem zweiten
        /// Faden und lässt die Abgabe erst weiterlaufen, wenn das Schließen durch ist oder an der Sperre wartet.
        /// </summary>
        private static IBuzzerLayoutState SchliessenMittenInDerAbgabe(Game spiel, Func<GameAccessor, IBuzzerLayoutState> neu,
            Action<IBuzzerLayoutState> abgeben)
        {
            var frist = TimeSpan.FromSeconds(10);
            using var drinnen = new ManualResetEventSlim();
            using var weiter = new ManualResetEventSlim();
            var anhalten = 1;

            var zustand = neu(new GameAccessor
            {
                GetGame = () =>
                {
                    if (Interlocked.Exchange(ref anhalten, 0) == 1) { drinnen.Set(); weiter.Wait(frist); }
                    return spiel;
                }
            });

            var abgabe = Task.Run(() => abgeben(zustand));
            Assert.IsTrue(drinnen.Wait(frist), "Die Abgabe kam nie in der Sperre an.");

            var schliessen = new Thread(zustand.LockAll) { IsBackground = true };
            schliessen.Start();
            Assert.IsTrue(SpinWait.SpinUntil(() => !schliessen.IsAlive || (schliessen.ThreadState & ThreadState.WaitSleepJoin) != 0, frist),
                "Das Schließen lief weder durch noch wartete es an der Sperre.");

            weiter.Set();
            Assert.IsTrue(abgabe.Wait(frist) && schliessen.Join(frist), "Abgabe oder Schließen hängen.");

            return zustand;
        }
    }
}
