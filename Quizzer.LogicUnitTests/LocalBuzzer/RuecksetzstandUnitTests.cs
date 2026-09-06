using LocalBuzzer.Service.Base;
using LocalBuzzer.Service.Hubs.Accessors;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;

namespace Quizzer.LogicUnitTests.LocalBuzzer
{
    /// <summary>
    /// Der Rücksetzstand, an dem die Telefonseite eine neue Runde erkennt.
    /// <para>
    /// <b>Warum nicht die Rundennummer.</b> <c>ResetRoundAsync</c> wird mit derselben
    /// <c>Game.CurrentRound</c> gerufen - beim Zurücksetzen ändert sich die Zahl nicht. Die
    /// Telefonseite baut ihr Layout aber nur neu auf, wenn sich dessen Kennung ändert; ohne ein
    /// unterscheidendes Merkmal entsperrte sie nur, und wer bei Multiple Choice schon abgegeben
    /// hatte, blieb für immer gesperrt.
    /// </para>
    /// </summary>
    [TestClass]
    public class RuecksetzstandUnitTests
    {
        private static LayoutStateManager Manager()
        {
            var manager = new LayoutStateManager(new GameAccessor());

            manager.ResetLayouts(1, BuzzerControlsLayout.KeySelect);

            return manager;
        }

        /// <summary>Jedes Zurücksetzen zählt hoch - auch bei gleicher Rundennummer.</summary>
        [TestMethod]
        public void EveryResetRaisesTheCounter()
        {
            var manager = Manager();

            var erster = manager.CreateClientState().ResetCount;

            manager.ResetLayouts(1, BuzzerControlsLayout.KeySelect);

            var zweiter = manager.CreateClientState().ResetCount;

            Assert.AreNotEqual(erster, zweiter,
                "Zweimal dasselbe Zuruecksetzen sieht auf dem Telefon gleich aus - dann baut es "
                + "das Layout nicht neu auf, und wer abgegeben hat, bleibt gesperrt.");

            Assert.AreEqual(erster + 1, zweiter, "Der Zaehler springt.");

            Assert.AreEqual(1, manager.CreateClientState().Round,
                "Die Rundennummer hat sich mitgeaendert - dann traegt sie die Unterscheidung "
                + "schon, und dieser Zaehler waere unnoetig.");
        }

        /// <summary>
        /// <b>Die Gegenrichtung.</b> Ohne sie wäre die obige auch dann grün, wenn der Zähler bei
        /// <i>jedem</i> Zustandsbericht stiege - dann baute die Telefonseite bei jeder Abgabe
        /// eines Mitspielers neu auf und verwürfe die eigene Auswahl.
        /// </summary>
        [TestMethod]
        public void MerelyReadingTheStateDoesNotRaiseIt()
        {
            var manager = Manager();

            var vorher = manager.CreateClientState().ResetCount;

            manager.CreateClientState();
            manager.LockAll();

            Assert.AreEqual(vorher, manager.CreateClientState().ResetCount,
                "Der Zaehler steigt, ohne dass zurueckgesetzt wurde - dann baut die "
                + "Telefonseite staendig neu auf und verwirft die begonnene Auswahl.");
        }
    }
}
