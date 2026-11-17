using Quizzer.Base;
using Quizzer.TestShared;
using Quizzer.Views.BuzzerViews;

namespace Quizzer.UnitTests.Views.BuzzerViews
{
    /// <summary>
    /// Basis jedes WPF-Tests, der den Buzzer-Server startet. <see cref="StarteServerAsync"/> ist der einzige
    /// Weg dorthin: eigener Port, nur 127.0.0.1 (V14, gehalten von <c>TestServerLoopbackUnitTests</c>).
    /// </summary>
    public abstract class BuzzerServerTestBasis
    {
        /// <summary>Der Port gehoert allein der abgeleiteten Klasse.</summary>
        protected abstract int Port { get; }

        protected Task StarteServerAsync(BuzzerServerViewModel vm)
        {
            vm.BuzzerPort = Port;
            vm.BuzzerBindAddress = TestNetwork.Address;
            return ((AsyncRelayCommand)vm.StartServerCommand).ExecuteAsync(null);
        }
    }
}
