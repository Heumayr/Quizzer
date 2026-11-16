using LocalBuzzer.Service.Base.States.Minispiele;
using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Questions.Minispiele;
using Quizzer.Views.GameViews.Minispiele;
using Quizzer.Views.GameViews.Sub;
using System.Windows;
using System.Windows.Input;

namespace Quizzer.Views.GameViews
{
    /// <summary>
    /// Minispiel-Teil des Spielablaufs (M35 B3): der Spielleiter startet, wiederholt und bricht ab
    /// (E1), die geschlossenen Runden werden gewertet und auf Beamer und Spielerkarte gebracht.
    /// Was eine Art ausmacht, steht in ihrer Strategie unter <c>Minispiele/</c> (M35 C2).
    /// </summary>
    public partial class CurrentQuestionViewModel
    {
        private const string MinispielCaption = "Minispiel";

        /// <summary>„Runde n von m", danach die Zeiten - fuer Beamer und Spielleiter dasselbe Objekt.</summary>
        public ReaktionAnzeige Reaktion { get; } = new();

        public MinigameQuestion? Minispiel => Question as MinigameQuestion;

        private MinispielAblauf? ablauf;

        /// <summary>Die Strategie der laufenden Art - wechselt die Frage die Art, gilt eine neue.</summary>
        private MinispielAblauf Ablauf
            => ablauf is { } bisher && bisher.Art == Minispiel?.Kind
                ? bisher
                : ablauf = MinispielAblauf.Fuer(Minispiel?.Kind, this);

        /// <summary>Die Knoepfe des Minispiels - nur, wenn die Frage im Minispiel-Rahmen laeuft.</summary>
        public Visibility MinispielSteuerungVisibility
            => !VorphaseLaeuft && CurrentBuzzerLayout == BuzzerControlsLayout.Minigame ? Visibility.Visible : Visibility.Collapsed;

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
            ablauf?.Beende();
            ablauf = null;

            if (Minispiel is not { } frage || BuzzerControlsViewModel == null)
                return;

            var titel = MinigameProfiles.TryFor(frage.Kind, out var art) ? art.DisplayName : MinispielCaption;

            // Eine Art ohne Runden spielt genau eine, was auch immer in der Spalte steht.
            var runden = art is { ShowRounds: false } ? 1 : Math.Max(1, frage.Rounds);

            BuzzerControlsViewModel.SetMinigameInfo(new MinigameSetup(
                frage.Kind, titel, MinigameProfiles.RulesFor(frage), runden, DurationSeconds: frage.DurationSeconds,
                TriggerPlayerId: Ausloeser, Variant: frage.Variant, Content: MinispielInhalt.Zeilen(frage)));
            BereitePaarVor();
            BuzzerControlsViewModel.MinigameRoundClosed = OnMinigameRoundClosedAsync;
            BuzzerControlsViewModel.MinigameSubmitted = OnMinigameSubmittedAsync;
            BuzzerControlsViewModel.MinigameReset = OnMinigameReset;

            minispielBereit = $"{titel} · {runden} {(runden == 1 ? "Runde" : "Runden")} – gleich geht es los";
            Reaktion.Zeige(minispielBereit);
        }

        private string minispielBereit = string.Empty;

        /// <summary>"Runde zuruecksetzen" stellt das Minispiel am Server auf Anfang - hier ebenso.</summary>
        private void OnMinigameReset()
        {
            Ablauf.SetzeZurueck();
            Reaktion.Zeige(minispielBereit);
        }

        internal async Task StartMinispielRundeAsync()
        {
            if (!PaarBereit())
                return;

            if (BuzzerControlsViewModel == null || !await BuzzerControlsViewModel.StartMinigameRoundAsync())
                return;

            Klang.Buzzer(); // V5: Start einer Runde
            ZeigeLaeuft();
        }

        /// <summary>Spielt die laufende oder letzte Runde neu (E1) - ihre Zeiten zaehlen nicht mehr.</summary>
        internal async Task WiederholeMinispielRundeAsync()
        {
            var runde = BuzzerControlsViewModel?.MinigameState?.Round ?? 0;

            if (BuzzerControlsViewModel == null || !await BuzzerControlsViewModel.RepeatMinigameRoundAsync())
                return;

            Ablauf.Wiederhole(runde);

            Klang.Buzzer();
            ZeigeLaeuft();
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

        /// <summary>Eine Runde ist zu (oder abgebrochen): werten, Tafel an den Beamer, Ton.</summary>
        internal async Task OnMinigameRoundClosedAsync(MinigameRoundClosed runde)
        {
            Ablauf.RundeZu(runde);

            Ablauf.TonZu(runde); // V5: Ergebnis, je Art

            if (runde.Finished)
                await OpenResultsAsync();
        }

        /// <summary>Eine Abgabe ist angenommen - der Tippsprint treibt damit sein Rennen am Beamer.</summary>
        internal Task OnMinigameSubmittedAsync(MinigameSubmission abgabe)
        {
            Ablauf.Abgabe(abgabe);
            return Task.CompletedTask;
        }

        /// <summary>Was waehrend der Runde am Beamer steht - je Art.</summary>
        private void ZeigeLaeuft() => Ablauf.ZeigeLaeuft();

        internal List<(Guid Id, string Name)> MinispielSpieler()
            => PlayersResultViewModel?.PlayerResultContextList
                   .Select(k => (k.Player.Id, k.Player.CalculatedDisplayName)).ToList()
               ?? [];

        private void ClearMinigame()
        {
            ablauf?.Beende();
            ablauf = null;
            Reaktion.Zeige(string.Empty);

            if (BuzzerControlsViewModel == null)
                return;

            BuzzerControlsViewModel.MinigameRoundClosed = null;
            BuzzerControlsViewModel.MinigameSubmitted = null;
            BuzzerControlsViewModel.MinigameReset = null;
        }
    }
}
