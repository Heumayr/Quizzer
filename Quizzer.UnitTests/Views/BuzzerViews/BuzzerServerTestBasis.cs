using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.TestShared;
using Quizzer.Views.BuzzerViews;

namespace Quizzer.UnitTests.Views.BuzzerViews
{
    /// <summary>
    /// Basis jedes WPF-Tests, der den Buzzer-Server startet (V10). <see cref="StarteServerAsync"/> ist der einzige
    /// Weg dorthin: eigener Port, nur 127.0.0.1 (V14, gehalten von <c>TestServerLoopbackUnitTests</c>).
    /// </summary>
    public abstract class BuzzerServerTestBasis
    {
        /// <summary>Der Port gehoert allein der abgeleiteten Klasse; er steht in <see cref="TestPorts"/>.</summary>
        protected abstract int Port { get; }

        /// <summary>
        /// Kein <c>Assert.Inconclusive</c>: ein belegter Port ist kein Umstand, unter dem der Testlauf gruen sein
        /// darf - meist haelt ihn ein abgebrochener Testlauf.
        /// </summary>
        [TestInitialize]
        public void PortPruefen()
            => Assert.IsTrue(TestNetwork.PortIsFree(Port), $"Port {Port} ist belegt - er gehoert allein dieser Testklasse.");

        protected Task StarteServerAsync(BuzzerServerViewModel vm)
        {
            vm.BuzzerPort = Port;
            vm.BuzzerBindAddress = TestNetwork.Address;
            return ((AsyncRelayCommand)vm.StartServerCommand).ExecuteAsync(null);
        }

        protected static Task<bool> WarteAsync(Func<bool> bedingung, int millis = 4000) => TestNetwork.WarteAsync(bedingung, millis);
    }
}
