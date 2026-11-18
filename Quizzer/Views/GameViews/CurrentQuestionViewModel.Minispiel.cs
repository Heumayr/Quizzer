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
            => startMinigameRoundCommand ??= new AsyncRelayCommand(_ => StartMinispielRundeAsync(), _ => !RundeGebucht && StartGesperrtWeil == null);

        public ICommand RepeatMinigameRoundCommand
            => repeatMinigameRoundCommand ??= new AsyncRelayCommand(_ => WiederholeMinispielRundeAsync(), _ => !RundeGebucht && WiederholenGesperrtWeil == null);

        /// <summary>
        /// R4-1: nach einer gebuchten Runde bucht ein Wiederholen ein zweites Mal aus derselben Zelle - gesperrt, bis die Wertung
        /// zurückgenommen ist. Die Rücknahme geht über das Spielfenster; die neu geöffnete Zelle hat dann nichts gebucht.
        /// </summary>
        private bool RundeGebucht => PlayersResultViewModel?.HatGebucht == true;

        public string RepeatMinigameRoundToolTip => RundeGebucht
            ? "Diese Runde ist schon gebucht – ein Wiederholen buchte sie ein zweites Mal. Erst die Wertung zurücknehmen: "
              + "Frage schließen, „Letzte Wertung zurücknehmen“, Zelle neu öffnen."
            : WiederholenGesperrtWeil ?? "Spielt die laufende oder zuletzt gespielte Runde neu. Ihre bisherigen Zeiten zählen nicht mehr.";

        /// <summary>P3-1: „Runde zurücksetzen“ stellt das Minispiel auf Anfang - ein neuer Start buchte dann wie R4-1 ein zweites Mal.</summary>
        public string StartMinigameRoundToolTip => RundeGebucht
            ? "Diese Runde ist schon gebucht – ein neuer Start buchte sie ein zweites Mal. Erst die Wertung zurücknehmen: "
              + "Frage schließen, „Letzte Wertung zurücknehmen“, Zelle neu öffnen."
            : StartGesperrtWeil ?? "Startet die nächste Runde auf allen Telefonen. Nach der letzten Runde geht nichts mehr – dann wiederholen oder abschließen.";

        private const string MinispielAbgebrochen = "Das Minispiel ist abgebrochen – es nimmt keine Runde mehr an. "
            + "„Runde zurücksetzen“ stellt es auf Anfang, danach geht es mit „Runde starten“ neu los.";

        /// <summary>P8-2: warum der Server einen Start jetzt ablehnte - <c>null</c>, wenn er ihn annähme (oder kein Minispiel läuft).</summary>
        private string? StartGesperrtWeil => BuzzerControlsViewModel?.MinigameState switch
        {
            null or { CanStartNextRound: true } => null,
            { Aborted: true } => MinispielAbgebrochen,
            { Phase: MinigamePhase.Running } => "Die Runde läuft noch – die nächste startet, wenn diese zu ist.",
            _ => "Alle Runden sind gespielt – „Runde wiederholen“ spielt die letzte neu, sonst die Zelle abschließen.",
        };

        /// <summary>P8-2: warum der Server ein Wiederholen jetzt ablehnte - <c>null</c>, wenn er es annähme.</summary>
        private string? WiederholenGesperrtWeil => BuzzerControlsViewModel?.MinigameState switch
        {
            null or { CanRepeatRound: true } => null,
            { Aborted: true } => MinispielAbgebrochen,
            _ => "Noch ist keine Runde gespielt – erst „Runde starten“.",
        };

        /// <summary>Aus dem Ergebnisfenster nach jeder Buchung: Knopf und Erklärung folgen dem gebuchten Stand.</summary>
        internal void NachBuchung()
        {
            OnPropertyChanged(nameof(RepeatMinigameRoundToolTip));
            OnPropertyChanged(nameof(StartMinigameRoundToolTip));
            CommandManager.InvalidateRequerySuggested();
        }

        public ICommand AbortMinigameCommand
            => abortMinigameCommand ??= new AsyncRelayCommand(_ => BrecheMinispielAbAsync(), _ => !MinispielVorbei);

        /// <summary>
        /// P6-3: nach dem Ende änderte ein Abbruch nur die Anzeige (Beamer, Telefon, zweites Bewertungsfenster) und nahm am Server
        /// „Runde wiederholen“ weg - Buchung und Vorschläge blieben. Deshalb gesperrt; eine Wiederholung öffnet ihn wieder.
        /// </summary>
        private bool MinispielVorbei => BuzzerControlsViewModel?.MinigameState?.Phase == MinigamePhase.Finished;

        public string AbortMinigameToolTip => MinispielVorbei
            ? "Das Minispiel ist schon zu Ende – ein Abbruch änderte an der Wertung nichts mehr, nur die Anzeige. "
              + "Was gilt, steht auf den Spielerkarten; „Runde wiederholen“ spielt die letzte Runde neu."
            : "Beendet das Minispiel sofort. Die laufende Runde zählt nicht, gespielte Runden bleiben als Vorschlag.";

        /// <summary>Nach jedem Wechsel der Phase: der Abbruch folgt ihr.</summary>
        private void NachMinispielPhase()
        {
            OnPropertyChanged(nameof(AbortMinigameToolTip));
            OnPropertyChanged(nameof(StartMinigameRoundToolTip));
            OnPropertyChanged(nameof(RepeatMinigameRoundToolTip));
            CommandManager.InvalidateRequerySuggested();
        }

        /// <summary>Stellt den Rahmen am Server auf diese Frage ein - vor dem Ausspielen.</summary>
        private void PrepareMinigame()
        {
            ablauf?.Beende();
            ablauf = null;

            if (Minispiel is not { } frage || BuzzerControlsViewModel == null)
                return;

            var titel = MinigameProfiles.TryFor(frage.Kind, out var art) ? art.DisplayName : MinispielCaption;

            // Eine Art ohne Runden spielt ihre feste Zahl (meist eine, der Bluff zwei Phasen), was auch immer in der Spalte steht.
            var runden = art is { ShowRounds: false } ? art.FixedRounds : Math.Max(1, frage.Rounds);

            BuzzerControlsViewModel.SetMinigameInfo(new MinigameSetup(
                frage.Kind, titel, MinigameProfiles.RulesFor(frage, Coordinate?.Game), runden, DurationSeconds: frage.DurationSeconds,
                TriggerPlayerId: Ausloeser, Variant: frage.Variant, Content: MinispielInhalt.Zeilen(frage),
                TeamScoring: Coordinate?.Game?.TeamMinigameScoring ?? TeamMinigameScoring.Average));
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
            NachMinispielPhase();
        }

        internal async Task StartMinispielRundeAsync()
        {
            if (RundeGebucht || !PaarBereit())
                return;

            if (BuzzerControlsViewModel == null || !await BuzzerControlsViewModel.StartMinigameRoundAsync())
                return;

            NachMinispielPhase();
            Klang.Buzzer(); // V5: Start einer Runde
            ZeigeLaeuft();
        }

        /// <summary>Spielt die laufende oder letzte Runde neu (E1) - ihre Zeiten zaehlen nicht mehr.</summary>
        internal async Task WiederholeMinispielRundeAsync()
        {
            var runde = BuzzerControlsViewModel?.MinigameState?.Round ?? 0;

            if (RundeGebucht || BuzzerControlsViewModel == null || !await BuzzerControlsViewModel.RepeatMinigameRoundAsync())
                return;

            Ablauf.Wiederhole(runde);
            NachMinispielPhase();

            Klang.Buzzer();
            ZeigeLaeuft();
        }

        /// <summary>Bricht ab (E1): die laufende Runde zaehlt nicht, gespielte bleiben als Vorschlag.</summary>
        internal async Task BrecheMinispielAbAsync()
        {
            if (BuzzerControlsViewModel == null || MinispielVorbei)
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
            NachMinispielPhase();

            Ablauf.TonZu(runde); // V5: Ergebnis, je Art

            if (!runde.Finished)
                return;

            await OpenResultsAsync();
            await PruefeRekordAsync(); // M37 E02: erst nach dem Bewertungsfenster - die Ansage haelt nichts an
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
