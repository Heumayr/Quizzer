using LocalBuzzer.Service.Base.States.Minispiele;
using LocalBuzzer.Service.Hubs;
using Microsoft.AspNetCore.SignalR;
using Quizzer.DataModels.Models.Base;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using static LocalBuzzer.Service.Base.States.BuzzerKeySelector;
using static LocalBuzzer.Service.Base.States.BuzzerInputState;

namespace LocalBuzzer.Service.Base
{
    public sealed class BuzzerEventBus
    {
        public event Action<string, Guid>? ClientAssigned;

        public event Action<Player?, int>? WinnerDeclared;

        public event Action<int>? RoundReset;

        public event Action<SelectionResult>? PlayerSelectedKeys;

        public event Action<ConcurrentDictionary<Guid, SelectionResult>>? AllPlayersSelectedKeys;

        /// <summary>Ein Spieler hat seinen Schaetzwert abgegeben.</summary>
        public event Action<InputResult>? PlayerSubmittedInput;

        /// <summary>Alle Mitspieler haben abgegeben - die Schaetzrunde ist zu.</summary>
        public event Action<ConcurrentDictionary<Guid, InputResult>>? AllPlayersSubmittedInput;

        public void OnAssigned(string name, Guid playerId) => ClientAssigned?.Invoke(name, playerId);

        public void OnWinner(Player? player, int round) => WinnerDeclared?.Invoke(player, round);

        public void OnReset(int round) => RoundReset?.Invoke(round);

        public void OnPlayerSelectedKeys(SelectionResult results) => PlayerSelectedKeys?.Invoke(results);

        public void OnAllPlayersSelectedKeys(ConcurrentDictionary<Guid, SelectionResult> dic) => AllPlayersSelectedKeys?.Invoke(dic);

        public void OnPlayerSubmittedInput(InputResult result) => PlayerSubmittedInput?.Invoke(result);

        public void OnAllPlayersSubmittedInput(ConcurrentDictionary<Guid, InputResult> dic) => AllPlayersSubmittedInput?.Invoke(dic);

        /// <summary>Ein Spieler hat im Minispiel abgegeben, und der Rahmen hat es angenommen (M35 B3).</summary>
        public event Action<MinigameSubmission>? MinigameSubmitted;

        /// <summary>Eine Runde des Minispiels ist zu - oder es wurde abgebrochen.</summary>
        public event Action<MinigameRoundClosed>? MinigameRoundClosed;

        public void OnMinigameSubmitted(MinigameSubmission abgabe) => MinigameSubmitted?.Invoke(abgabe);

        public void OnMinigameRoundClosed(MinigameRoundClosed runde) => MinigameRoundClosed?.Invoke(runde);
    }
}