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

        public BuzzerState(GameAccessor gameAccessor)
        {
            _gameAccessor = gameAccessor;
        }

        public BuzzerControlsLayout BuzzerControlsLayout => BuzzerControlsLayout.Buzzer;

        private Player? _winner;

        public bool Locked { get; set; }

        public Player? Winner => _winner;

        public object BuzzerStateInfo => Winner != null ? Winner.DisplayName : "";

        public bool TryBuzz(Player? player)
        {
            if (player == null) return false;

            lock (this)
            {
                if (Locked) return false;
                Locked = true;
                _winner = player;
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