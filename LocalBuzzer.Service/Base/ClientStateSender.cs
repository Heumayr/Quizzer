using Microsoft.AspNetCore.SignalR;

namespace LocalBuzzer.Service.Base
{
    /// <summary>
    /// Verschickt den Zustand je Verbindung, jedem Telefon seinen eigenen (M35 B3). Vorher ging
    /// dasselbe DTO an alle (<c>Clients.All</c>) - ein Minispiel muss aber jedem seine eigene Abgabe
    /// und seine eigene Sperre zeigen.
    /// </summary>
    public static class ClientStateSender
    {
        public const string StateChanged = "StateChanged";

        /// <summary>Jeder eingetragenen Verbindung ihren Zustand.</summary>
        public static async Task SendToEachAsync(
            IHubClients<IClientProxy> clients, PlayerConnectionRegistry registry, LayoutStateManager states, CancellationToken ct = default)
        {
            foreach (var (verbindung, spieler) in registry.Snapshot())
                await clients.Client(verbindung).SendAsync(StateChanged, states.CreateClientState(spieler), ct);
        }

        /// <summary>Nur einer Verbindung ihren Zustand - nach einer Abgabe, die die Runde nicht schliesst.</summary>
        public static Task SendToAsync(
            IHubClients<IClientProxy> clients, string connectionId, PlayerConnectionRegistry registry, LayoutStateManager states, CancellationToken ct = default)
        {
            return registry.TryGetPlayer(connectionId, out var spieler)
                ? clients.Client(connectionId).SendAsync(StateChanged, states.CreateClientState(spieler), ct)
                : Task.CompletedTask;
        }
    }
}
