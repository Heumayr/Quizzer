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
                OnPropertyChanged(nameof(TextShort));
                OnPropertyChanged(nameof(TextLong));
            }
        }

        private (string Long, string Short) SplitLastBracketText(string? text)
        {
            text ??= string.Empty;

            int open = text.LastIndexOf('[');
            int close = text.LastIndexOf(']');

            // no valid pair
            if (open < 0 || close < 0 || close <= open)
                return (text.Trim(), text.Trim());

            // short inside brackets
            var shortPart = text.Substring(open + 1, close - open - 1);

            // long = remove the bracket part (including brackets) and trim
            var longPart = (text.Remove(open, close - open + 1)).Trim();

            return (longPart, shortPart);
        }

        public string TextLong => SplitLastBracketText(Text).Long;
        public string TextShort => SplitLastBracketText(Text).Short;

        public override Task VMSaveAsync()
        {
            return Task.CompletedTask;
        }

        protected override Task Onload()
        {
            throw new NotImplementedException();
        }
    }
}