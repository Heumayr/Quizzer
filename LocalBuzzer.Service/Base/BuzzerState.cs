using System;
using System.Collections.Generic;
using System.Text;

namespace LocalBuzzer.Service.Base
{
    public sealed class BuzzerState
    {
        private int _round = 1;
        private bool _locked;
        private string? _winner;

        public int Round => _round;
        public bool Locked => _locked;
        public string? Winner => _winner;

        public bool TryBuzz(string name)
        {
            lock (this)
            {
                if (_locked) return false;
                _locked = true;
                _winner = name;
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