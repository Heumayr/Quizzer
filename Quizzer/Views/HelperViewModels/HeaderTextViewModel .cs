using Quizzer.Base;
using System;
using System.Collections.Generic;
using System.Text;

namespace Quizzer.Views.HelperViewModels
{
    public class HeaderEntryViewModel : ViewModelBase
    {
        private readonly Dictionary<int, string> _dict;
        private bool lockHeader;

        public HeaderEntryViewModel(Dictionary<int, string> dict, int index, bool isColumnHeader, bool lockHeader = false)
        {
            _dict = dict ?? throw new ArgumentNullException(nameof(dict));
            Index = index;

            GridRow = isColumnHeader ? 0 : index + 1;
            GridColumn = isColumnHeader ? index + 1 : 0;

            LockHeader = lockHeader;
        }

        public int Index { get; }

        // These are used for Grid positioning (offset because row/col 0 are headers)
        public int GridRow { get; }

        public int GridColumn { get; }

        public bool LockHeader
        {
            get => lockHeader;
            set
            {
                lockHeader = value;
                OnPropertyChanged(nameof(LockHeader));
            }
        }

        public string Text
        {
            get => _dict.TryGetValue(Index, out var v) ? (v ?? "") : "";
            set
            {
                value ??= "";
                if (_dict.TryGetValue(Index, out var old) && old == value) return;

                _dict[Index] = value;
                OnPropertyChanged(nameof(Text));
            }
        }

        public override Task VMSaveAsync()
        {
            return Task.CompletedTask;
        }
    }
}