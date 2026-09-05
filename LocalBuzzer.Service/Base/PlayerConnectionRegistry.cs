using Quizzer.DataModels.Models.Base;
using System.Diagnostics.CodeAnalysis;

namespace LocalBuzzer.Service.Base
{
    public sealed class PlayerConnectionRegistry
    {
        private readonly object _gate = new();
        private readonly Dictionary<string, Player> _playerByConnection = new();
        private readonly Dictionary<Guid, string> _connectionByPlayer = new();

        /// <summary>Liefert den Spieler hinter einer Verbindung - nur solange sie seine aktuelle ist.</summary>
        public bool TryGetPlayer(string connectionId, [NotNullWhen(true)] out Player? player)
        {
            lock (_gate)
            {
                return _playerByConnection.TryGetValue(connectionId, out player);
            }
        }

        /// <summary>
        /// Traegt die Verbindung eines Spielers ein. Hatte er schon eine, verliert diese ihre
        /// Zuordnung und ihre Kennung kommt zurueck - der Letzte gewinnt.
        /// </summary>
        public string? Register(string connectionId, Player player)
        {
            lock (_gate)
            {
                _connectionByPlayer.TryGetValue(player.Id, out var previous);

                if (previous == connectionId)
                    return null;

                if (previous != null)
                    _playerByConnection.Remove(previous);

                _playerByConnection[connectionId] = player;
                _connectionByPlayer[player.Id] = connectionId;

                return previous;
            }
        }

        /// <summary>
        /// Loest eine Verbindung. Liefert nur dann <c>true</c>, wenn sie noch die aktuelle des
        /// Spielers war - eine laengst ersetzte darf ihn nicht auf getrennt setzen.
        /// </summary>
        public bool Unregister(string connectionId, [NotNullWhen(true)] out Player? player)
        {
            lock (_gate)
            {
                if (!_playerByConnection.Remove(connectionId, out player))
                    return false;

                _connectionByPlayer.Remove(player.Id);
                return true;
            }
        }
    }
}
