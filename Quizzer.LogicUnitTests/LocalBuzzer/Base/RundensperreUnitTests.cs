using LocalBuzzer.Service.Base;
using LocalBuzzer.Service.Base.States;
using LocalBuzzer.Service.Hubs.Accessors;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;

namespace Quizzer.LogicUnitTests.LocalBuzzer.Base
{
    /// <summary>
    /// Die Rundenmarke einer Abgabe ist die Runde, in die sie ging (M37 P11, P10-3): „Runde zurücksetzen“ wartet, bis eine
    /// laufende Abgabe durch ist. Sonst ging die Abgabe in die neue Runde, trug aber die Marke der alten - schloss sie die neue,
    /// wurde nichts aufgelöst, und die Runde stand zu, ohne Ergebnis.
    /// </summary>
    [TestClass]
    public class RundensperreUnitTests
    {
        [TestMethod]
        public void AResetWaitsForASubmissionUnderWay()
        {
            var anna = new Player { Id = Guid.NewGuid(), Designation = "Anna" };
            var spiel = new Game { Id = Guid.NewGuid() };
            spiel.PlayerXGames.Add(new PlayerXGame { Id = Guid.NewGuid(), GameId = spiel.Id, PlayerId = anna.Id, Player = anna });

            var zustand = new LayoutStateManager(new GameAccessor { GetGame = () => spiel });
            zustand.ResetLayouts(1, BuzzerControlsLayout.Input);
            var alt = zustand.ResetCount;

            var frist = TimeSpan.FromSeconds(10);
            using var drinnen = new ManualResetEventSlim();
            using var weiter = new ManualResetEventSlim();
            var runde = -1;
            var geschlossen = false;

            // Angehalten nach dem Lesen der Marke, vor dem Eintragen - dort greift die Spielleitung ein.
            var abgabe = Task.Run(() => zustand.TrySubmit(() =>
            {
                drinnen.Set();
                weiter.Wait(frist);
                return zustand.BuzzerInputState.SetInput(new BuzzerInputState.InputResult { PlayerId = anna.Id, Player = anna, Value = "1" }, out geschlossen);
            }, out runde));
            Assert.IsTrue(drinnen.Wait(frist), "Die Abgabe kam nie an.");

            var zuruecksetzen = new Thread(() => zustand.ResetLayouts(1, BuzzerControlsLayout.Input)) { IsBackground = true };
            zuruecksetzen.Start();
            Assert.IsTrue(SpinWait.SpinUntil(() => !zuruecksetzen.IsAlive || (zuruecksetzen.ThreadState & ThreadState.WaitSleepJoin) != 0, frist),
                "Das Zurücksetzen lief weder durch noch wartete es.");

            weiter.Set();
            Assert.IsTrue(abgabe.Wait(frist) && zuruecksetzen.Join(frist), "Abgabe oder Zurücksetzen hängen.");

            Assert.IsTrue(abgabe.Result && geschlossen, "Annas Tipp wurde nicht angenommen - dann misst der Test nichts.");
            Assert.AreEqual(alt, runde, "Die Abgabe trägt nicht die Marke der Runde, in der sie lief.");
            Assert.AreEqual(0, zustand.BuzzerInputState.InputsForPlayer.Count, "Der Tipp der alten Runde steht in der neuen.");
            Assert.IsFalse(zustand.LockRound(runde), "Die alte Marke schließt die neue Runde.");
            Assert.IsFalse(zustand.BuzzerInputState.Locked, "Die neue Runde ist zu.");
        }

        /// <summary>Und umgekehrt: läuft das Schließen der alten Runde schon, wartet das Zurücksetzen - die neue bleibt offen.</summary>
        [TestMethod]
        public void AResetWaitsForClosingUnderWay()
        {
            var zustand = new LayoutStateManager(new GameAccessor { GetGame = () => new Game() });
            var frist = TimeSpan.FromSeconds(10);
            using var drinnen = new ManualResetEventSlim();
            using var weiter = new ManualResetEventSlim();
            var bremse = new BremsenderZustand(() => { drinnen.Set(); weiter.Wait(frist); });

            zustand.States.Add(bremse); // als letzter: angehalten ist das Schließen nach dem Sperren, vor AllLocked
            zustand.ResetLayouts(1, BuzzerControlsLayout.Input);
            var runde = zustand.ResetCount;

            bremse.Scharf = true;
            var schliessen = Task.Run(() => zustand.LockRound(runde));
            Assert.IsTrue(drinnen.Wait(frist), "Das Schließen kam nie an.");

            var zuruecksetzen = new Thread(() => zustand.ResetLayouts(1, BuzzerControlsLayout.Input)) { IsBackground = true };
            zuruecksetzen.Start();
            Assert.IsTrue(SpinWait.SpinUntil(() => !zuruecksetzen.IsAlive || (zuruecksetzen.ThreadState & ThreadState.WaitSleepJoin) != 0, frist),
                "Das Zurücksetzen lief weder durch noch wartete es.");

            weiter.Set();
            Assert.IsTrue(schliessen.Wait(frist) && zuruecksetzen.Join(frist), "Schließen oder Zurücksetzen hängen.");

            Assert.IsTrue(schliessen.Result, "Die laufende Runde ließ sich nicht schließen - dann misst der Test nichts.");
            Assert.IsFalse(zustand.AllLocked || zustand.BuzzerInputState.Locked, "Das Schließen der alten Runde hat die neue geschlossen.");
        }

        /// <summary>Ein Zustand, dessen <c>LockAll</c> anhält, sobald er scharf ist.</summary>
        private sealed class BremsenderZustand(Action halt) : IBuzzerLayoutState
        {
            public bool Scharf { get; set; }
            public BuzzerControlsLayout BuzzerControlsLayout => BuzzerControlsLayout.Minigame + 100;
            public bool Locked { get; set; }
            public object BuzzerStateInfo => new();
            public void Reset() => Locked = false;
            public void ClearAndLock() => Locked = true;

            public void LockAll()
            {
                if (Scharf) { Scharf = false; halt(); }
                Locked = true;
            }
        }
    }
}
