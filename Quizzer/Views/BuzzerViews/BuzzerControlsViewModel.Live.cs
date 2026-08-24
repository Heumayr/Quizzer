using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Questions;
using System.Collections.ObjectModel;
using System.Windows;

namespace Quizzer.Views.BuzzerViews
{
    /// <summary>
    /// Der Blick des Spielleiters auf die laufende Runde: welche Frage laeuft, was richtig
    /// waere, und wer schon abgegeben hat.
    /// </summary>
    public partial class BuzzerControlsViewModel
    {
        /// <summary>Ein Eintrag je Mitspieler, in der Reihenfolge des Spiels.</summary>
        public ObservableCollection<PlayerLiveContext> LivePlayers { get; } = new();

        private QuestionBase? roundQuestion;

        /// <summary>Kopfzeile ueber der Runde - Fragetyp und Rundennummer.</summary>
        public string RoundHeadline
        {
            get
            {
                if (roundQuestion == null)
                    return "Keine Frage offen";

                var typ = QuestionTypeProfiles.For(roundQuestion.Typ).DisplayName;
                var runde = Game?.CurrentRound ?? 0;

                return runde > 0 ? $"{typ} · Runde {runde}" : typ;
            }
        }

        /// <summary>
        /// Was richtig waere. Steht nur hier, in der Steuerung des Spielleiters - die
        /// Spieleransicht bindet dieses ViewModel nicht ein.
        /// </summary>
        public string SolutionText
        {
            get
            {
                if (roundQuestion is AppreciateQestion appreciate)
                    return AppreciateEvaluator.DescribeExpected(appreciate);

                if (roundQuestion?.Typ == QuestionType.MultipleChoice)
                    return DescribeCorrectKeys(roundQuestion);

                return string.Empty;
            }
        }

        public Visibility SolutionVisibility =>
            string.IsNullOrWhiteSpace(SolutionText) ? Visibility.Collapsed : Visibility.Visible;

        /// <summary>Die als Loesung markierten Antworttasten, etwa <c>B: Wien</c>.</summary>
        private static string DescribeCorrectKeys(QuestionBase question)
        {
            var richtige = question.Steps
                .Where(s => s.IsResult && !string.IsNullOrEmpty(s.QuestionViewKey))
                .OrderBy(s => s.QuestionViewKey, StringComparer.OrdinalIgnoreCase)
                .Select(s => string.IsNullOrWhiteSpace(s.Designation)
                    ? s.QuestionViewKey
                    : $"{s.QuestionViewKey}: {s.Designation}")
                .ToArray();

            return string.Join(" · ", richtige);
        }

        /// <summary>
        /// Stellt die Anzeige auf die geoeffnete Frage ein. Wird beim Vorbereiten des
        /// Buzzer-Layouts gerufen, also genau dann, wenn die Runde ausgespielt wird.
        /// </summary>
        public void SetRoundInfo(QuestionBase? question)
        {
            roundQuestion = question;

            RebuildLivePlayers();
            RaiseRoundChanged();
        }

        /// <summary>Baut die Spielerliste neu auf - je Mitspieler ein Eintrag.</summary>
        private void RebuildLivePlayers()
        {
            RunOnUi(() =>
            {
                LivePlayers.Clear();

                foreach (var player in Game?.Players ?? Enumerable.Empty<Player>())
                    LivePlayers.Add(new PlayerLiveContext(player));
            });
        }

        private PlayerLiveContext? Find(Guid playerId) =>
            LivePlayers.FirstOrDefault(p => p.PlayerId == playerId);

        /// <summary>Traegt den Tipp einer Schaetzfrage ein.</summary>
        internal void TrackInput(Guid playerId, string value) =>
            RunOnUi(() => Find(playerId)?.SetAnswer(value));

        /// <summary>Traegt die gewaehlten Antworttasten ein.</summary>
        internal void TrackSelection(Guid playerId, IEnumerable<string>? keys)
        {
            var text = keys == null ? string.Empty : string.Join(", ", keys);

            RunOnUi(() => Find(playerId)?.SetAnswer(text));
        }

        /// <summary>Haelt fest, wer die Runde gebuzzert hat; alle anderen verlieren die Marke.</summary>
        internal void TrackWinner(Guid? playerId)
        {
            RunOnUi(() =>
            {
                foreach (var entry in LivePlayers)
                    entry.SetWinner(playerId.HasValue && entry.PlayerId == playerId.Value);
            });
        }

        /// <summary>Neue Runde: die Abgaben der letzten fallen weg.</summary>
        internal void ClearRound()
        {
            RunOnUi(() =>
            {
                foreach (var entry in LivePlayers)
                    entry.ClearRound();
            });

            RaiseRoundChanged();
        }

        /// <summary>Der Verbindungszustand haengt am Spieler, nicht an diesem Eintrag.</summary>
        internal void RefreshConnections()
        {
            RunOnUi(() =>
            {
                foreach (var entry in LivePlayers)
                    entry.RaiseConnectionChanged();
            });
        }

        private void RaiseRoundChanged()
        {
            OnPropertyChanged(nameof(RoundHeadline));
            OnPropertyChanged(nameof(SolutionText));
            OnPropertyChanged(nameof(SolutionVisibility));
        }
    }
}
