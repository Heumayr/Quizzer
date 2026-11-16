using LocalBuzzer.Service.Base.States.Minispiele;
using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Questions.Minispiele;
using Quizzer.Views.GameViews.Sub;
using System.Windows;
using System.Windows.Input;

namespace Quizzer.Views.GameViews
{
    /// <summary>
    /// Minispiel-Teil des Spielablaufs (M35 B3): der Spielleiter startet, wiederholt und bricht ab
    /// (E1), die geschlossenen Runden werden gewertet und auf Beamer und Spielerkarte gebracht.
    /// </summary>
    public partial class CurrentQuestionViewModel
    {
        private const string MinispielCaption = "Minispiel";

        /// <summary>Die gezaehlten Runden der Reaktion; eine Wiederholung ersetzt ihre Runde.</summary>
        private readonly SortedDictionary<int, IReadOnlyDictionary<Guid, ReactionAttempt>> reaktionsRunden = new();

        /// <summary>„Runde n von m", danach die Zeiten - fuer Beamer und Spielleiter dasselbe Objekt.</summary>
        public ReaktionAnzeige Reaktion { get; } = new();

        public MinigameQuestion? Minispiel => Question as MinigameQuestion;

        /// <summary>Die Knoepfe des Minispiels - nur, wenn die Frage im Minispiel-Rahmen laeuft.</summary>
        public Visibility MinispielSteuerungVisibility
            => CurrentBuzzerLayout == BuzzerControlsLayout.Minigame ? Visibility.Visible : Visibility.Collapsed;

        private AsyncRelayCommand? startMinigameRoundCommand;
        private AsyncRelayCommand? repeatMinigameRoundCommand;
        private AsyncRelayCommand? abortMinigameCommand;

        public ICommand StartMinigameRoundCommand
            => startMinigameRoundCommand ??= new AsyncRelayCommand(_ => StartMinispielRundeAsync());

        public ICommand RepeatMinigameRoundCommand
            => repeatMinigameRoundCommand ??= new AsyncRelayCommand(_ => WiederholeMinispielRundeAsync());

        public ICommand AbortMinigameCommand
            => abortMinigameCommand ??= new AsyncRelayCommand(_ => BrecheMinispielAbAsync());

        /// <summary>Stellt den Rahmen am Server auf diese Frage ein - vor dem Ausspielen.</summary>
        private void PrepareMinigame()
        {
            reaktionsRunden.Clear();

            if (Minispiel is not { } frage || BuzzerControlsViewModel == null)
                return;

            var titel = MinigameProfiles.TryFor(frage.Kind, out var art) ? art.DisplayName : MinispielCaption;
            var runden = Math.Max(1, frage.Rounds);

            BuzzerControlsViewModel.SetMinigameInfo(
                new MinigameSetup(frage.Kind, titel, MinigameProfiles.RulesFor(frage), runden));
            BuzzerControlsViewModel.MinigameRoundClosed = OnMinigameRoundClosedAsync;
            BuzzerControlsViewModel.MinigameReset = OnMinigameReset;

            minispielBereit = $"{titel} · {runden} {(runden == 1 ? "Runde" : "Runden")} – gleich geht es los";
            Reaktion.Zeige(minispielBereit);
        }

        private string minispielBereit = string.Empty;

        /// <summary>"Runde zuruecksetzen" stellt das Minispiel am Server auf Anfang - hier ebenso.</summary>
        private void OnMinigameReset()
        {
            if (reaktionsRunden.Count > 0)
            {
                reaktionsRunden.Clear();
                WerteReaktion();
            }

            Reaktion.Zeige(minispielBereit);
        }

        internal async Task StartMinispielRundeAsync()
        {
            if (BuzzerControlsViewModel == null || !await BuzzerControlsViewModel.StartMinigameRoundAsync())
                return;

            Klang.Buzzer(); // V5: Start einer Runde
            Reaktion.Zeige(LaeuftText());
        }

        /// <summary>Spielt die laufende oder letzte Runde neu (E1) - ihre Zeiten zaehlen nicht mehr.</summary>
        internal async Task WiederholeMinispielRundeAsync()
        {
            var runde = BuzzerControlsViewModel?.MinigameState?.Round ?? 0;

            if (BuzzerControlsViewModel == null || !await BuzzerControlsViewModel.RepeatMinigameRoundAsync())
                return;

            reaktionsRunden.Remove(runde);
            WerteReaktion();

            Klang.Buzzer();
            Reaktion.Zeige(LaeuftText());
        }

        /// <summary>Bricht ab (E1): die laufende Runde zaehlt nicht, gespielte bleiben als Vorschlag.</summary>
        internal async Task BrecheMinispielAbAsync()
        {
            if (BuzzerControlsViewModel == null)
                return;

            if (!UserPrompt.Confirm(
                    "Minispiel abbrechen? Die laufende Runde zählt nicht; die schon gespielten Runden "
                    + "bleiben als Vorschlag auf den Spielerkarten.", MinispielCaption))
                return;

            await BuzzerControlsViewModel.AbortMinigameAsync();
        }

        /// <summary>Eine Runde ist zu (oder abgebrochen): werten, Zeiten an den Beamer, Ton.</summary>
        internal async Task OnMinigameRoundClosedAsync(MinigameRoundClosed runde)
        {
            if (!runde.Aborted)
            {
                reaktionsRunden[runde.Round] = runde.Submissions
                    .Where(a => a.Value is ReactionAttempt)
                    .ToDictionary(a => a.Key, a => (ReactionAttempt)a.Value);
            }

            var ergebnis = WerteReaktion();
            var spieler = MinispielSpieler();

            if (runde.Aborted)
                Reaktion.Zeige("Minispiel abgebrochen – die besten Zeiten", ReaktionTafel.Ende(ergebnis, spieler));
            else if (runde.Finished)
                Reaktion.Zeige("Ergebnis – die beste Zeit gewinnt", ReaktionTafel.Ende(ergebnis, spieler));
            else
                Reaktion.Zeige($"Runde {runde.Round} von {runde.TotalRounds} – die Zeiten",
                    ReaktionTafel.Runde(reaktionsRunden[runde.Round], spieler));

            Klang.Ende(); // V5: Ergebnis

            if (runde.Finished)
                await OpenResultsAsync();
        }

        private ReaktionErgebnis WerteReaktion()
        {
            var ergebnis = ReaktionEvaluator.Werte(reaktionsRunden.Values);
            PlayersResultViewModel?.WerteReaktion(ergebnis);
            return ergebnis;
        }

        private string LaeuftText()
        {
            var zustand = BuzzerControlsViewModel?.MinigameState;
            return $"Runde {zustand?.Round ?? 0} von {zustand?.Setup.TotalRounds ?? 0} – wartet auf Grün!";
        }

        private List<(Guid Id, string Name)> MinispielSpieler()
            => PlayersResultViewModel?.PlayerResultContextList
                   .Select(k => (k.Player.Id, k.Player.CalculatedDisplayName)).ToList()
               ?? [];

        private void ClearMinigame()
        {
            reaktionsRunden.Clear();
            Reaktion.Zeige(string.Empty);

            if (BuzzerControlsViewModel == null)
                return;

            BuzzerControlsViewModel.MinigameRoundClosed = null;
            BuzzerControlsViewModel.MinigameReset = null;
        }
    }
}
