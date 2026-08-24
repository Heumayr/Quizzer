using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using System.Windows;
using System.Windows.Media;

namespace Quizzer.Views.BuzzerViews
{
    /// <summary>
    /// Was der Spielleiter waehrend einer laufenden Runde ueber einen Mitspieler sehen muss:
    /// ob dessen Telefon verbunden ist und was er abgegeben hat.
    /// <para>
    /// Bis hierher stand das erst im Ergebnisfenster - also erst, wenn die Runde schon zu war.
    /// Waehrend der Runde war nicht zu erkennen, auf wen man noch wartet.
    /// </para>
    /// </summary>
    public class PlayerLiveContext : ViewCommonBase
    {
        public PlayerLiveContext(Player player)
        {
            Player = player;
        }

        public Player Player { get; }

        public Guid PlayerId => Player.Id;

        public string DisplayName => Player.CalculatedDisplayName;

        public bool IsConnected => Player.ConnectionState == PlayerConnection.Connected;

        /// <summary>Was der Spieler abgegeben hat, im Klartext; leer, solange nichts kam.</summary>
        public string AnswerText
        {
            get => field;
            private set
            {
                if (field == value)
                    return;

                field = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasAnswered));
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(StatusBrush));
                OnPropertyChanged(nameof(AnswerVisibility));
            }
        } = string.Empty;

        public bool HasAnswered => !string.IsNullOrWhiteSpace(AnswerText);

        public Visibility AnswerVisibility =>
            HasAnswered ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>Der Spieler hat als Erster gebuzzert.</summary>
        public bool IsWinner
        {
            get => field;
            private set
            {
                if (field == value)
                    return;

                field = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(StatusBrush));
            }
        }

        /// <summary>
        /// Ein Wort statt einer blossen Farbe - ein farbiger Punkt allein ist fuer einen Teil
        /// der Leute keine Meldung (Standards, Abschnitt Bedienbarkeit).
        /// </summary>
        public string StatusText
        {
            get
            {
                if (!IsConnected)
                    return "nicht verbunden";

                if (IsWinner)
                    return "gebuzzert";

                return HasAnswered ? "abgegeben" : "wartet";
            }
        }

        public Brush StatusBrush
        {
            get
            {
                if (!IsConnected)
                    return Brushes.DarkGray;

                if (IsWinner)
                    return Brushes.Gold;

                return HasAnswered ? Brushes.LightGreen : Brushes.Wheat;
            }
        }

        public void SetAnswer(string answer) => AnswerText = answer ?? string.Empty;

        public void SetWinner(bool isWinner) => IsWinner = isWinner;

        /// <summary>Neue Runde: Abgabe und Buzzer-Ausgang fallen weg, die Verbindung bleibt.</summary>
        public void ClearRound()
        {
            AnswerText = string.Empty;
            IsWinner = false;
        }

        public void RaiseConnectionChanged()
        {
            OnPropertyChanged(nameof(IsConnected));
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(StatusBrush));
        }
    }
}
