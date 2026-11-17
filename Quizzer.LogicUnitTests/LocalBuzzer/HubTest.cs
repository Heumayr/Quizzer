using LocalBuzzer.Service;
using LocalBuzzer.Service.Base;
using Quizzer.TestShared;

namespace Quizzer.LogicUnitTests.LocalBuzzer
{
    /// <summary>
    /// Was die Hub-Tests teilen. <see cref="StartAsync"/> ist der einzige Weg, auf dem ein Test einen
    /// Buzzer-Server startet (V14, gehalten von <c>TestServerLoopbackUnitTests</c>).
    /// </summary>
    internal static class HubTest
    {
        public static Task StartAsync(BuzzerServer server, int port)
            => server.StartAsync(new BuzzerServerOptions { Port = port, BindAddress = TestNetwork.Address });
    }
}
