using LocalBuzzer.Service.Hubs.Accessors;
using Microsoft.AspNetCore.Routing;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using System;
using System.Collections.Generic;
using System.Text;

namespace LocalBuzzer.Service.Base.States
{
    public sealed class BuzzerState : IBuzzerLayoutState
    {
        private readonly GameAccessor _gameAccessor;
        private readonly AnswerLock _answerLock;
        private readonly TeamBuzzLock _teamLock;
        private readonly Func<TeamRoster> _teams;

        public BuzzerState(GameAccessor gameAccessor, AnswerLock? answerLock = null, TeamBuzzLock? teamLock = null, Func<TeamRoster>? teams = null)
        {
            _gameAccessor = gameAccessor;
            _answerLock = answerLock ?? new();
            _teamLock = teamLock ?? new();
            _teams = teams ?? (() => TeamRoster.Empty);
        }

        public BuzzerControlsLayout BuzzerControlsLayout => BuzzerControlsLayout.Buzzer;

        private Player? _winner;

        public bool Locked { get; set; }

        public Player? Winner => _winner;

        public object BuzzerStateInfo => Winner != null ? Winner.DisplayName : "";

        public bool TryBuzz(Player? player)
        {
            // M35 C3b: wer die Frage nicht beantworten darf, buzzert ins Leere.
            if (player == null || !_answerLock.Allows(player.Id)) return false;

            lock (this)
            {
                if (Locked) return false;

                // M37 T4: hat das Team bei dieser Frage schon gebuzzert, zaehlt es nicht. Geprueft und gesetzt unter dieser
                // Sperre: nur der Gewinner sperrt sein Team, nie einer, der zu spaet kam.
                var team = _teams().KeyFor(player.Id);
                if (team is { } schon && _teamLock.IsLocked(schon)) return false;

                Locked = true;
                _winner = player;

                if (team is { } neu)
                    _teamLock.Lock(neu);

                return true;
            }
        }

        public void Reset()
        {
            lock (this)
            {
                Locked = false;
                _winner = null;
            }
        }

        public void ClearAndLock()
        {
            lock (this)
            {
                Locked = true;
                _winner = null;
            }
        }

        public void LockAll()
        {
            Locked = true;
        }
    }
}