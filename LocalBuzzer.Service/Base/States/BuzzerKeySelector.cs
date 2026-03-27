using LocalBuzzer.Service.Hubs.Accessors;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace LocalBuzzer.Service.Base.States
{
    public sealed class BuzzerKeySelector : IBuzzerLayoutState
    {
        private readonly GameAccessor _gameAccessor;

        public BuzzerKeySelector(GameAccessor gameAccessor)
        {
            _gameAccessor = gameAccessor;
        }

        public BuzzerControlsLayout BuzzerControlsLayout => BuzzerControlsLayout.KeySelect;

        public bool Locked { get; set; }

        public class BuzzerKeySelectorInfo()
        {
            public bool ShowDesignations { get; set; } = true;
            public ConcurrentDictionary<string, string> KeysAndDesignations { get; private set; } = new();
        }

        public class SelectionResult
        {
            public Guid PlayerId { get; set; } = Guid.Empty;

            public Player? Player { get; set; }

            public List<string> SelectedKeys { get; set; } = new();

            public bool CommittedResult { get; set; } = false;
        }

        public BuzzerKeySelectorInfo Infos { get; set; } = new();

        public ConcurrentDictionary<Guid, SelectionResult> KeyResultsForPlayer { get; private set; } = new();

        public int MaxAllowedKeySelectPerPlayer { get; set; } = 1;

        public object BuzzerStateInfo => Infos;

        public void SetSelectedKeys(SelectionResult results)
        {
            if (results.Player == null || Locked)
                return;

            lock (this)
            {
                if (results.SelectedKeys.Count() > MaxAllowedKeySelectPerPlayer)
                {
                    results.SelectedKeys.Clear();
                    results.CommittedResult = false;
                }
                else
                {
                    results.CommittedResult = true;
                }

                KeyResultsForPlayer.AddOrUpdate(results.Player.Id, k => results, (k, o) => results);

                var playersCount = _gameAccessor.GetGame?.Invoke()?.Players?.Count() ?? 0;

                if (playersCount <= 0)
                {
                    Locked = true;
                    return;
                }

                Locked = KeyResultsForPlayer.All(v => v.Value.CommittedResult) && KeyResultsForPlayer.Count() == playersCount;
            }
        }

        public void Reset()
        {
            foreach (var key in KeyResultsForPlayer.Keys)
            {
                KeyResultsForPlayer[key] = new();
            }

            Locked = false;
        }

        public void ClearAndLock()
        {
            Infos.KeysAndDesignations.Clear();
            KeyResultsForPlayer.Clear();

            Locked = false;
            Infos.ShowDesignations = true;
        }

        public void LockAll()
        {
            foreach (var item in KeyResultsForPlayer)
            {
                item.Value.CommittedResult = true;
            }
            Locked = true;
        }
    }
}