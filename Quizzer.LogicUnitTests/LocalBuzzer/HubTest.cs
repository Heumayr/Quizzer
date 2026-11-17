using LocalBuzzer.Service;
using LocalBuzzer.Service.Base;
using Quizzer.DataModels.Models.Buzzer;
using Quizzer.TestShared;
using System.Text.Json;

namespace Quizzer.LogicUnitTests.LocalBuzzer
{
    /// <summary>
    /// Was die Hub-Tests teilen (V10), eingebunden per <c>using static</c>. <see cref="StartAsync"/> ist der
    /// einzige Weg, auf dem ein Test einen Buzzer-Server startet (V14, gehalten von
    /// <c>TestServerLoopbackUnitTests</c>). Warten und Portprüfung stehen in <see cref="TestNetwork"/>.
    /// </summary>
    internal static class HubTest
    {
        public static Task StartAsync(BuzzerServer server, int port)
            => server.StartAsync(new BuzzerServerOptions { Port = port, BindAddress = TestNetwork.Address });

        /// <summary>Die Minispiel-Phase aus der LayoutInfo, -1 ohne eine.</summary>
        public static int Phase(ClientLayoutStateDto? dto)
            => dto?.LayoutInfo is JsonElement { ValueKind: JsonValueKind.Object } info && info.TryGetProperty("phase", out var p) ? p.GetInt32() : -1;

        public static JsonElement Info(ClientLayoutStateDto dto) => (JsonElement)dto.LayoutInfo!;

        /// <summary>Der Teil der LayoutInfo, den nur die jeweilige Minispiel-Art kennt.</summary>
        public static JsonElement Art(ClientLayoutStateDto dto) => Info(dto).GetProperty("kindInfo");
    }
}
