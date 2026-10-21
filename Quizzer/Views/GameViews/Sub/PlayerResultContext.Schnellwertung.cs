using Quizzer.Base;
using System.Windows.Input;
using System.Windows.Media;

namespace Quizzer.Views.GameViews.Sub
{
    /// <summary>
    /// M11 „leichtere Vergabe von Punkten": jede Karte hat eine Nummer (Strg+1 … Strg+9), die
    /// gewaehlte Karte einen goldenen Rahmen, und die Werte fuer richtig/falsch stehen schon vor
    /// dem Klick fest.
    /// </summary>
    public partial class PlayerResultContext
    {
        private bool isSelected;

        private static readonly Brush Normalrahmen = Freeze(new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)));
        private static readonly Brush Auswahlrahmen = Freeze(new SolidColorBrush(Color.FromRgb(0xFF, 0xC1, 0x07)));

        private static Brush Freeze(SolidColorBrush brush)
        {
            brush.Freeze();
            return brush;
        }

        /// <summary>1-basiert, wie die Taste, mit der man die Karte waehlt.</summary>
        public int CardNumber { get; set; }

        public string CardLabel => CardNumber is > 0 and < 10 ? $"{CardNumber} · {Player?.Designation}" : Player?.Designation ?? string.Empty;

        public bool IsSelected
        {
            get => isSelected;
            set
            {
                if (isSelected == value)
                    return;

                isSelected = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectionBorderBrush));
            }
        }

        public Brush SelectionBorderBrush => IsSelected ? Auswahlrahmen : Normalrahmen;

        /// <summary>Was „richtig" gerade bringt - dieselbe Rechnung wie beim Klick.</summary>
        public int PreviewRightPoints => GetCurrentPoints();

        /// <summary>Was „falsch" kostet (negativ).</summary>
        public int PreviewWrongPoints => -(Coordinate?.CurrentMinusPoints ?? 0);

        private RelayCommand? selectThisCommand;

        public ICommand SelectThisCommand => selectThisCommand ??= new RelayCommand(_ => PlayersResultViewModel?.Select(this));

        /// <summary>Nach jedem Hinweis aendert sich der Wert - die Vorschau muss nachziehen.</summary>
        public void RefreshPreview()
        {
            OnPropertyChanged(nameof(PreviewRightPoints));
            OnPropertyChanged(nameof(PreviewWrongPoints));
        }
    }
}
