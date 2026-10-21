using Quizzer.Base;
using Quizzer.Views.GameViews.Sub;
using System.Windows;
using System.Windows.Input;

namespace Quizzer.Views.GameViews
{
    /// <summary>
    /// M11: Punkte mit weniger Klicks. Oben eine Leiste fuer den, der gebuzzert hat
    /// („Richtig +300" / „Falsch −30"), dazu Tastenkuerzel fuer die gewaehlte Karte.
    /// <para>
    /// <b>Alle Kuerzel mit Strg:</b> die Karten haben ein Zahlenfeld fuer eigene Werte. Eine
    /// nackte „1" oder ein „R" loeste beim Eintippen von „150" sonst eine Wertung aus.
    /// </para>
    /// <para>
    /// Gespeichert wird weiterhin nur ueber Speichern/Abschliessen (Strg+S, Strg+Enter) - eine
    /// Taste setzt die Bewertung, sie schreibt nichts. Ein Vertipper laesst sich also noch
    /// zuruecknehmen (Strg+0).
    /// </para>
    /// </summary>
    public partial class PlayersResultViewModel
    {
        public const string KeyHint =
            "Strg+1…9 Karte wählen · Strg+R richtig · Strg+F falsch · Strg+0 ohne Wertung · "
            + "Strg+S speichern · Strg+Enter speichern und Zelle abschließen";

        public PlayerResultContext? SelectedContext { get; private set; }

        public PlayerResultContext? WinnerContext =>
            CurrentBuzzerWinner == null ? null : PlayerResultContextList.FirstOrDefault(c => c.Player.Id == CurrentBuzzerWinner.Id);

        public Visibility WinnerBarVisibility => WinnerContext != null ? Visibility.Visible : Visibility.Collapsed;

        public string WinnerText => WinnerContext is { } w ? $"Gebuzzert: {w.Player.CalculatedDisplayName}" : string.Empty;

        public string WinnerRightText => WinnerContext is { } w ? $"Richtig  +{w.PreviewRightPoints}" : string.Empty;

        public string WinnerWrongText => WinnerContext is { } w ? $"Falsch  {w.PreviewWrongPoints}" : string.Empty;

        public void Select(PlayerResultContext? context)
        {
            foreach (var c in PlayerResultContextList)
                c.IsSelected = ReferenceEquals(c, context);

            SelectedContext = context;
            OnPropertyChanged(nameof(SelectedContext));
        }

        /// <summary>Nach neuem Kartensatz oder neuem Buzzer-Gewinner: Nummern vergeben, Auswahl setzen.</summary>
        internal void RefreshQuickRating()
        {
            for (var i = 0; i < PlayerResultContextList.Count; i++)
                PlayerResultContextList[i].CardNumber = i + 1;

            foreach (var c in PlayerResultContextList)
                c.RefreshPreview();

            // Wer gebuzzert hat, ist fast immer der, der bewertet wird.
            Select(WinnerContext ?? (PlayerResultContextList.Contains(SelectedContext!) ? SelectedContext : PlayerResultContextList.FirstOrDefault()));

            OnPropertyChanged(nameof(WinnerBarVisibility));
            OnPropertyChanged(nameof(WinnerText));
            OnPropertyChanged(nameof(WinnerRightText));
            OnPropertyChanged(nameof(WinnerWrongText));
        }

        private RelayCommand? selectCardCommand;

        /// <summary>Parameter: die Kartennummer als Text („1" … „9").</summary>
        public ICommand SelectCardCommand => selectCardCommand ??= new RelayCommand(p =>
        {
            if (int.TryParse(p?.ToString(), out var nummer) && nummer >= 1 && nummer <= PlayerResultContextList.Count)
                Select(PlayerResultContextList[nummer - 1]);
        });

        private RelayCommand? quickRightCommand;

        public ICommand QuickRightCommand => quickRightCommand ??= new RelayCommand(
            _ => SelectedContext?.RightSuggestionCommand.Execute(null));

        private RelayCommand? quickWrongCommand;

        public ICommand QuickWrongCommand => quickWrongCommand ??= new RelayCommand(
            _ => SelectedContext?.WrongSuggestionCommand.Execute(null));

        private RelayCommand? quickNoneCommand;

        public ICommand QuickNoneCommand => quickNoneCommand ??= new RelayCommand(
            _ => SelectedContext?.NoneSuggestionCommand.Execute(null));

        private RelayCommand? winnerRightCommand;

        /// <summary>Die Leiste oben: wirkt immer auf den, der gebuzzert hat.</summary>
        public ICommand WinnerRightCommand => winnerRightCommand ??= new RelayCommand(_ =>
        {
            Select(WinnerContext);
            WinnerContext?.RightSuggestionCommand.Execute(null);
        });

        private RelayCommand? winnerWrongCommand;

        public ICommand WinnerWrongCommand => winnerWrongCommand ??= new RelayCommand(_ =>
        {
            Select(WinnerContext);
            WinnerContext?.WrongSuggestionCommand.Execute(null);
        });
    }
}
