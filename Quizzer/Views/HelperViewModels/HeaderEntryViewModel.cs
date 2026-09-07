using Quizzer.Base;
using Quizzer.DataModels.Models.Base;
using System;
using System.Collections.Generic;
using System.Reflection.PortableExecutable;
using System.Text;

namespace Quizzer.Views.HelperViewModels
{
    public class HeaderEntryViewModel : ViewModelBase
    {
        private bool lockHeader;
        private Header Header { get; set; }

        public HeaderEntryViewModel(Header header, bool isColumnHeader, bool lockHeader = false)
        {
            Header = header;

            Index = header.Index;

            GridRow = isColumnHeader ? 0 : Index;
            GridColumn = isColumnHeader ? Index : 0;

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
            get => Header.Designation;
            set
            {
                Header.Designation = value;
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

        public override async Task VMSaveAsync()
        {
        }

        protected override async Task OnloadAsync()
        {
        }
    }
}