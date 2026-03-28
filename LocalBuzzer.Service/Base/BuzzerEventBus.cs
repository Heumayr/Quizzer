using LocalBuzzer.Service.Hubs;
using Microsoft.AspNetCore.SignalR;
using Quizzer.DataModels.Models.Base;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using static LocalBuzzer.Service.Base.States.BuzzerKeySelector;

namespace LocalBuzzer.Service.Base
{
    public sealed class BuzzerEventBus
    {
        public event Action<string, Guid>? ClientAssigned;

        public event Action<Player?, int>? WinnerDeclared;

        public event Action<int>? RoundReset;

        public event Action<SelectionResult>? PlayerSelectedKeys;

        public event Action<ConcurrentDictionary<Guid, SelectionResult>>? AllPlayersSelectedKeys;

        public void OnAssigned(string name, Guid playerId) => ClientAssigned?.Invoke(name, playerId);

        public void OnWinner(Player? player, int round) => WinnerDeclared?.Invoke(player, round);

        public void OnReset(int round) => RoundReset?.Invoke(round);

        public void OnPlayerSelectedKeys(SelectionResult results) => PlayerSelectedKeys?.Invoke(results);

        public void OnAllPlayersSelectedKeys(ConcurrentDictionary<Guid, SelectionResult> dic) => AllPlayersSelectedKeys?.Invoke(dic);
    }
}