using LocalBuzzer.Service.Hubs.Accessors;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using System.Collections.Concurrent;

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

        public sealed class BuzzerKeySelectorInfo
        {
            public bool ShowDesignations { get; set; } = true;
            public Dictionary<string, string> KeysAndDesignations { get; set; } = new();
            public int MaxAllowedSelections { get; set; } = 1;
        }

        public sealed class SelectionResult
        {
            public Guid PlayerId { get; set; } = Guid.Empty;
            public Player? Player { get; set; }
            public List<string> SelectedKeys { get; set; } = new();
            public bool CommittedResult { get; set; }
        }

        public BuzzerKeySelectorInfo Infos { get; set; } = new();

        public ConcurrentDictionary<Guid, SelectionResult> KeyResultsForPlayer { get; } = new();

        public int MaxAllowedKeySelectPerPlayer { get; set; } = 1;

        public object BuzzerStateInfo => new BuzzerKeySelectorInfo
        {
            ShowDesignations = Infos.ShowDesignations,
            KeysAndDesignations = Infos.KeysAndDesignations,
            MaxAllowedSelections = MaxAllowedKeySelectPerPlayer
        };

        public void SetSelectedKeys(SelectionResult results)
        {
            if (results.Player == null || Locked)
                return;

            lock (this)
            {
                if (results.SelectedKeys.Count > MaxAllowedKeySelectPerPlayer)
                {
                    results.SelectedKeys.Clear();
                    results.CommittedResult = false;
                }
                else
                {
                    results.CommittedResult = true;
                }

                KeyResultsForPlayer.AddOrUpdate(results.Player.Id, _ => results, (_, _) => results);

                var playersCount = _gameAccessor.GetGame?.Invoke()?.Players?.Count() ?? 0;

                if (playersCount <= 0)
                {
                    Locked = true;
                    return;
                }

                Locked = KeyResultsForPlayer.Count == playersCount
                      && KeyResultsForPlayer.All(v => v.Value.CommittedResult);
            }
        }

        public void Reset()
        {
            KeyResultsForPlayer.Clear();
            Locked = false;
        }

        public void ClearAndLock()
        {
            Infos.KeysAndDesignations.Clear();
            KeyResultsForPlayer.Clear();
            Infos.ShowDesignations = true;
            Locked = true;
        }

        public void LockAll()
        {
            foreach (var item in KeyResultsForPlayer)
                item.Value.CommittedResult = true;

            Locked = true;
        }
    }
}