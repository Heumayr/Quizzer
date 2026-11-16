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
    /// Tippsprint und Stoppuhr (C1) haben je eine Teildatei; hier wird nur nach der Art verteilt.
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
            LeereZeitspiele();

            if (Minispiel is not { } frage || BuzzerControlsViewModel == null)
                return;

            var titel = MinigameProfiles.TryFor(frage.Kind, out var art) ? art.DisplayName : MinispielCaption;

            // Eine Art ohne Runden spielt genau eine, was auch immer in der Spalte steht.
            var runden = art is { ShowRounds: false } ? 1 : Math.Max(1, frage.Rounds);

            BuzzerControlsViewModel.SetMinigameInfo(new MinigameSetup(
                frage.Kind, titel, MinigameProfiles.RulesFor(frage), runden, DurationSeconds: frage.DurationSeconds));
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
            if (!VergissZeitspiel() && reaktionsRunden.Count > 0)
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
            ZeigeLaeuft();
        }

        /// <summary>Spielt die laufende oder letzte Runde neu (E1) - ihre Zeiten zaehlen nicht mehr.</summary>
        internal async Task WiederholeMinispielRundeAsync()
        {
            var runde = BuzzerControlsViewModel?.MinigameState?.Round ?? 0;

            if (BuzzerControlsViewModel == null || !await BuzzerControlsViewModel.RepeatMinigameRoundAsync())
                return;

            if (!VergissZeitspiel())
            {
                reaktionsRunden.Remove(runde);
                WerteReaktion();
            }

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
            switch (Minispiel?.Kind)
            {
                case MinigameKind.TapSprint:
                    TippsprintRundeZu(runde);
                    break;

                case MinigameKind.Stopwatch:
                    StoppuhrRundeZu(runde);
                    break;

                default:
                    ReaktionRundeZu(runde);
                    break;
            }

            Klang.Ende(); // V5: Ergebnis

            if (runde.Finished)
                await OpenResultsAsync();
        }

        private void ReaktionRundeZu(MinigameRoundClosed runde)
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
        }

        private ReaktionErgebnis WerteReaktion()
        {
            var ergebnis = ReaktionEvaluator.Werte(reaktionsRunden.Values);
            PlayersResultViewModel?.WerteReaktion(ergebnis);
            return ergebnis;
        }

        /// <summary>Was waehrend der Runde am Beamer steht - je Art.</summary>
        private void ZeigeLaeuft()
        {
            switch (Minispiel?.Kind)
            {
                case MinigameKind.TapSprint:
                    ZeigeRennen();
                    break;

                case MinigameKind.Stopwatch:
                    Reaktion.Zeige($"Stoppuhr läuft – stoppt nach genau {Minispiel.DurationSeconds} Sekunden!");
                    break;

                default:
                    var zustand = BuzzerControlsViewModel?.MinigameState;
                    Reaktion.Zeige($"Runde {zustand?.Round ?? 0} von {zustand?.Setup.TotalRounds ?? 0} – wartet auf Grün!");
                    break;
            }
        }

        /// <summary>
        /// Tippsprint und Stoppuhr spielen eine Runde: Wiederholen und Zuruecksetzen vergessen sie, und war
        /// sie gewertet, auch ihre Vorschlaege. <c>false</c> bei jeder anderen Art - dann gilt der Weg der Reaktion.
        /// </summary>
        private bool VergissZeitspiel()
        {
            switch (Minispiel?.Kind)
            {
                case MinigameKind.TapSprint:
                    if (LeereTippsprint())
                        WerteTippsprint();
                    return true;

                case MinigameKind.Stopwatch:
                    if (LeereStoppuhr())
                        WerteStoppuhr();
                    return true;

                default:
                    return false;
            }
        }

        private void LeereZeitspiele()
        {
            LeereTippsprint();
            LeereStoppuhr();
        }

        private List<(Guid Id, string Name)> MinispielSpieler()
            => PlayersResultViewModel?.PlayerResultContextList
                   .Select(k => (k.Player.Id, k.Player.CalculatedDisplayName)).ToList()
               ?? [];

        private void ClearMinigame()
        {
            reaktionsRunden.Clear();
            LeereZeitspiele();
            Reaktion.Zeige(string.Empty);

            if (BuzzerControlsViewModel == null)
                return;

            BuzzerControlsViewModel.MinigameRoundClosed = null;
            BuzzerControlsViewModel.MinigameSubmitted = null;
            BuzzerControlsViewModel.MinigameReset = null;
        }
    }
}
