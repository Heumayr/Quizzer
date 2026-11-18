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
        /// P8-1 (P10): „Runde zurücksetzen“, während Annas Stimme innen geprüft ist, aber noch nicht eingetragen - angehalten im
        /// Tastenvergleich der Prüfung. Früher setzte <c>Reset</c> außerhalb der Sperre zurück, und die gegen die alte Runde
        /// geprüfte Stimme stand danach in der neuen.
        /// </summary>
        [TestMethod]
        public void AResetWhileAVoteIsCheckedLeavesNoOldVoteInTheNewRound()
        {
            var (spiel, spieler) = Spiel();
            var tasten = (BuzzerKeySelector)EingriffMittenInDerAbgabe(
                halt => new BuzzerKeySelector(new GameAccessor { GetGame = () => spiel })
                {
                    Infos = new() { KeysAndDesignations = new(new AnhaltenderVergleich(halt)) { ["A"] = "Rom", ["B"] = "Paris" } }
                },
                zustand => ((BuzzerKeySelector)zustand).SetSelectedKeys(Stimme(spieler[0], "A")),
                zustand => zustand.Reset());

            Assert.IsFalse(tasten.KeyResultsForPlayer.ContainsKey(spieler[0].Id), "Annas Stimme aus der alten Runde steht in der neuen.");
            Assert.IsFalse(tasten.Locked, "Die neue Runde ist nicht offen.");
        }

        /// <summary>P8-1: ein Layoutwechsel (<c>ClearAndLock</c>) mitten in der Auswertung einer Stimme lässt den Zustand zu und leer.</summary>
        [TestMethod]
        public void ClearingWhileAVoteIsEvaluatedLeavesTheStateLockedAndEmpty()
        {
            var (spiel, spieler) = Spiel();
            var tasten = (BuzzerKeySelector)EingriffMittenInDerAbgabe(
                halt => new BuzzerKeySelector(new GameAccessor { GetGame = () => { halt(); return spiel; } })
                {
                    Infos = new() { KeysAndDesignations = new() { ["A"] = "Rom", ["B"] = "Paris" } }
                },
                zustand => ((BuzzerKeySelector)zustand).SetSelectedKeys(Stimme(spieler[0], "A")),
                zustand => zustand.ClearAndLock());

            Assert.IsTrue(tasten.Locked, "Die Auswertung der Stimme hat den abgelösten Zustand wieder geöffnet.");
            Assert.AreEqual(0, tasten.KeyResultsForPlayer.Count, "Im abgelösten Zustand steht noch eine Stimme.");
        }

        /// <summary>Wie <see cref="ClearingWhileAVoteIsEvaluatedLeavesTheStateLockedAndEmpty"/>, für den Tipp.</summary>
        [TestMethod]
        public void ClearingWhileAnInputIsEvaluatedLeavesTheStateLockedAndEmpty()
        {
            var (spiel, spieler) = Spiel();
            var eingabe = (BuzzerInputState)EingriffMittenInDerAbgabe(
                halt => new BuzzerInputState(new GameAccessor { GetGame = () => { halt(); return spiel; } }),
                zustand => ((BuzzerInputState)zustand).SetInput(Tipp(spieler[0], "1")),
                zustand => zustand.ClearAndLock());

            Assert.IsTrue(eingabe.Locked, "Die Auswertung des Tipps hat den abgelösten Zustand wieder geöffnet.");
            Assert.AreEqual(0, eingabe.InputsForPlayer.Count, "Im abgelösten Zustand steht noch ein Tipp.");
        }

        /// <summary>Ein Tastenvergleich, der beim ersten Aufruf nach dem Scharfstellen anhält - mitten in der Prüfung der Stimme.</summary>
        private sealed class AnhaltenderVergleich(Action halt) : IEqualityComparer<string>
        {
            public bool Equals(string? x, string? y) => string.Equals(x, y, StringComparison.Ordinal);

            public int GetHashCode(string obj)
            {
                halt();
                return StringComparer.Ordinal.GetHashCode(obj);
            }
        }

        private static IBuzzerLayoutState SchliessenMittenInDerAbgabe(Game spiel, Func<GameAccessor, IBuzzerLayoutState> neu,
            Action<IBuzzerLayoutState> abgeben)
            => EingriffMittenInDerAbgabe(halt => neu(new GameAccessor { GetGame = () => { halt(); return spiel; } }), abgeben, z => z.LockAll());

        /// <summary>
        /// Hält die Abgabe innen an - dort, wo <paramref name="neu"/> den Haken einbaut, in der Sperre -, greift von einem zweiten
        /// Faden ein und lässt die Abgabe erst weiterlaufen, wenn der Eingriff durch ist oder an der Sperre wartet. Scharf ist der
        /// Haken erst mit der Abgabe, damit der Aufbau ihn nicht auslöst.
        /// </summary>
        private static IBuzzerLayoutState EingriffMittenInDerAbgabe(Func<Action, IBuzzerLayoutState> neu,
            Action<IBuzzerLayoutState> abgeben, Action<IBuzzerLayoutState> eingriff)
        {
            var frist = TimeSpan.FromSeconds(10);
            using var drinnen = new ManualResetEventSlim();
            using var weiter = new ManualResetEventSlim();
            var anhalten = 0;

            var zustand = neu(() =>
            {
                if (Interlocked.Exchange(ref anhalten, 0) == 1) { drinnen.Set(); weiter.Wait(frist); }
            });

            Volatile.Write(ref anhalten, 1);
            var abgabe = Task.Run(() => abgeben(zustand));
            Assert.IsTrue(drinnen.Wait(frist), "Die Abgabe kam nie in der Sperre an.");

            var zweiter = new Thread(() => eingriff(zustand)) { IsBackground = true };
            zweiter.Start();
            Assert.IsTrue(SpinWait.SpinUntil(() => !zweiter.IsAlive || (zweiter.ThreadState & ThreadState.WaitSleepJoin) != 0, frist),
                "Der Eingriff lief weder durch noch wartete er an der Sperre.");

            weiter.Set();
            Assert.IsTrue(abgabe.Wait(frist) && zweiter.Join(frist), "Abgabe oder Eingriff hängen.");

            return zustand;
        }
    }
}
