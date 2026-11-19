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

        /// <summary>Eine Tastenwahl ist angenommen - mit der Marke der Runde, in der sie zaehlt (P12-1, wie P11-3).</summary>
        public event Action<SelectionResult, int>? PlayerSelectedKeys;

        /// <summary>Die Tastenrunde ist zu - mit der Marke der Runde (<c>ResetCount</c>), die sie geschlossen hat (P11-3).</summary>
        public event Action<ConcurrentDictionary<Guid, SelectionResult>, int>? AllPlayersSelectedKeys;

        /// <summary>Ein Spieler hat seinen Schaetzwert abgegeben - mit der Marke der Runde, in der er zaehlt (P12-1).</summary>
        public event Action<InputResult, int>? PlayerSubmittedInput;

        /// <summary>
        /// Alle Mitspieler haben abgegeben - die Schaetzrunde ist zu. Die Marke nennt die Runde: das Woerterbuch lebt weiter, und
        /// ein Zuruecksetzen leert es, bevor ein anderer Faden es liest (P11-3).
        /// </summary>
        public event Action<ConcurrentDictionary<Guid, InputResult>, int>? AllPlayersSubmittedInput;

        public void OnAssigned(string name, Guid playerId) => ClientAssigned?.Invoke(name, playerId);

        public void OnWinner(Player? player, int round) => WinnerDeclared?.Invoke(player, round);

        public void OnReset(int round) => RoundReset?.Invoke(round);

        public void OnPlayerSelectedKeys(SelectionResult results, int resetCount) => PlayerSelectedKeys?.Invoke(results, resetCount);

        public void OnAllPlayersSelectedKeys(ConcurrentDictionary<Guid, SelectionResult> dic, int resetCount) => AllPlayersSelectedKeys?.Invoke(dic, resetCount);

        public void OnPlayerSubmittedInput(InputResult result, int resetCount) => PlayerSubmittedInput?.Invoke(result, resetCount);

        public void OnAllPlayersSubmittedInput(ConcurrentDictionary<Guid, InputResult> dic, int resetCount) => AllPlayersSubmittedInput?.Invoke(dic, resetCount);

        /// <summary>Ein Spieler hat im Minispiel abgegeben, und der Rahmen hat es angenommen (M35 B3).</summary>
        public event Action<MinigameSubmission>? MinigameSubmitted;

        /// <summary>Eine Runde des Minispiels ist zu - oder es wurde abgebrochen.</summary>
        public event Action<MinigameRoundClosed>? MinigameRoundClosed;

        public void OnMinigameSubmitted(MinigameSubmission abgabe) => MinigameSubmitted?.Invoke(abgabe);

        public void OnMinigameRoundClosed(MinigameRoundClosed runde) => MinigameRoundClosed?.Invoke(runde);
    }
}