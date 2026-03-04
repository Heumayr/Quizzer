using Quizzer.DataModels.Models.Base;
using System;
using System.Collections.Generic;
using System.Text;

namespace LocalBuzzer.Service.Base
{
    public sealed class BuzzerEventBus
    {
        public event Action<string>? ClientAssigned;

        public event Action<Player?, int>? WinnerDeclared;

        public event Action<int>? RoundReset;

        public void OnAssigned(string name) => ClientAssigned?.Invoke(name);

        public void OnWinner(Player? player, int round) => WinnerDeclared?.Invoke(player, round);

        public void OnReset(int round) => RoundReset?.Invoke(round);
    }
}