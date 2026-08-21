using LocalBuzzer.Service.Base;
using LocalBuzzer.Service.Hubs.Accessors;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;

namespace Quizzer.LogicUnitTests.LocalBuzzer.Base
{
    /// <summary>
    /// Der Umschalter zwischen den Buzzer-Layouts.
    /// </summary>
    [TestClass]
    public class LayoutStateManagerUnitTests
    {
        private static LayoutStateManager NewManager() => new(new GameAccessor());

        /// <summary>
        /// Der Kern des Fehlers, an dem die Schaetzfrage haengen blieb: fuer
        /// <see cref="BuzzerControlsLayout.Input"/> war gar kein Zustand angemeldet. ResetLayouts
        /// fand nichts, CurrentState blieb null - und damit blieben die Spieler dauerhaft
        /// gesperrt, ohne dass irgendwo ein Fehler auftauchte.
        /// </summary>
        [TestMethod]
        [DataRow(BuzzerControlsLayout.Buzzer)]
        [DataRow(BuzzerControlsLayout.KeySelect)]
        [DataRow(BuzzerControlsLayout.Input)]
        public void EveryUsableLayoutHasAState(BuzzerControlsLayout layout)
        {
            var manager = NewManager();

            manager.ResetLayouts(round: 1, layout);

            Assert.IsNotNull(manager.CurrentState,
                $"Fuer {layout} ist kein Zustand angemeldet - die Spieler blieben gesperrt.");
            Assert.AreEqual(layout, manager.CurrentState.BuzzerControlsLayout);
        }

        [TestMethod]
        public void ResetOpensTheChosenLayoutAndLocksTheOthers()
        {
            var manager = NewManager();

            manager.ResetLayouts(round: 3, BuzzerControlsLayout.Input);

            Assert.IsFalse(manager.BuzzerInputState.Locked);
            Assert.IsTrue(manager.BuzzerState.Locked);
            Assert.IsTrue(manager.BuzzerKeySelector.Locked);
            Assert.AreEqual(3, manager.Round);
            Assert.IsFalse(manager.AllLocked);
        }

        [TestMethod]
        public void SwitchingLayoutsClosesThePreviousOne()
        {
            var manager = NewManager();
            manager.ResetLayouts(round: 1, BuzzerControlsLayout.Input);

            manager.ResetLayouts(round: 2, BuzzerControlsLayout.Buzzer);

            Assert.IsTrue(manager.BuzzerInputState.Locked);
            Assert.IsFalse(manager.BuzzerState.Locked);
        }

        [TestMethod]
        public void LockAllClosesEverything()
        {
            var manager = NewManager();
            manager.ResetLayouts(round: 1, BuzzerControlsLayout.Input);

            manager.LockAll();

            Assert.IsTrue(manager.AllLocked);
            Assert.IsTrue(manager.BuzzerInputState.Locked);
        }

        [TestMethod]
        public void TheClientStateCarriesTheCurrentLayoutAndItsInfo()
        {
            var manager = NewManager();
            manager.BuzzerInputState.Infos = new()
            { InputType = "date", Placeholder = "Datum" };
            manager.ResetLayouts(round: 7, BuzzerControlsLayout.Input);

            var dto = manager.CreateClientState();

            Assert.AreEqual(BuzzerControlsLayout.Input, dto.Layout);
            Assert.AreEqual(7, dto.Round);
            Assert.IsNotNull(dto.LayoutInfo);
        }

        [TestMethod]
        public void WithoutALayoutNothingIsOpen()
        {
            var manager = NewManager();

            manager.ResetLayouts(round: 1, BuzzerControlsLayout.None);

            Assert.IsNull(manager.CurrentState);
            Assert.IsTrue(manager.BuzzerInputState.Locked);
            Assert.IsTrue(manager.BuzzerState.Locked);
            Assert.IsTrue(manager.BuzzerKeySelector.Locked);
        }
    }
}
