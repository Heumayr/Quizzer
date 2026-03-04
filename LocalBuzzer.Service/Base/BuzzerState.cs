using Quizzer.DataModels.Models.Base;
using System;
using System.Collections.Generic;
using System.Text;

namespace LocalBuzzer.Service.Base
{
    public sealed class BuzzerState
    {
        private int _round = 1;
        private bool _locked;
        private Player? _winner;

        public int Round => _round;
        public bool Locked => _locked;
        public Player? Winner => _winner;

        public bool TryBuzz(Player? player)
        {
            if (player == null) return false;

            lock (this)
            {
                if (_locked) return false;
                _locked = true;
                _winner = player;
                return true;
            }
        }

        public int Reset()
        {
            lock (this)
            {
                _round++;
                _locked = false;
                _winner = null;
                return _round;
            }
        }
    }
}