using LocalBuzzer.Service.Base.States.Minispiele;
using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Questions.Minispiele;
using Quizzer.DataModels.Questions.Sonderregeln;
using Quizzer.Logic.Controller.TypedControllers;
using Quizzer.Views.GameViews.Sonderregeln;
using Quizzer.Views.StaticRessources;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;

namespace Quizzer.Views.GameViews
{
    /// <summary>
    /// Joker-Runde vor einer gewoehnlichen Frage (M35 C7b): sie oeffnet von selbst, wenn ein Mitspieler einen offenen
    /// Joker haelt und die Zelle weder Sonderregel noch Minispiel traegt. Sie laeuft im Minispiel-Rahmen wie die
    /// Vorphase einer Sonderregel; beim Schliessen wird sofort eingeloest, danach wirken Doppelt und Schutz als
    /// Vorschlag auf der Karte (E1) und die Sperre ueber die Antwortsperre.
    /// </summary>
    public partial class CurrentQuestionViewModel
    {
        private JokerAblauf? joker;
        private bool jokerRundeGespielt;

        /// <summary>Woher die Joker des Spiels kommen.</summary>
        internal Func<Guid, Task<List<PlayerJoker>>> JokerLaden { get; set; } = LadeJokerAsync;

        /// <summary>Wie die Wahlen eingeloest werden - ein Test setzt eine Stoerung ein (A10).</summary>
        internal Func<IReadOnlyList<JokerRedemption>, Task<List<PlayerJoker>>> JokerEinloesen { get; set; } = LoeseJokerEinAsync;

        /// <summary>
        /// Startet die Joker-Runde, wenn es eine gibt. <c>true</c>: die Frage geht erst danach auf die Telefone. Sind
        /// nur schon eingeloeste Joker da (Zelle erneut geoeffnet), wirken sie ohne Runde.
        /// </summary>
        private async Task<bool> StarteJokerRundeAsync()
        {
            if (jokerRundeGespielt || Coordinate is not { IsDone: false } zelle || Question is not { } frage
                || frage.Typ == DataModels.Enumerations.QuestionType.Minigame || zelle.SpecialRule != CellSpecialRule.None
                || BuzzerControlsViewModel is not { MinigameState: not null } steuerung)
                return false;

            jokerRundeGespielt = true;

            if (await ErstelleJokerAblaufAsync(zelle) is not { } ablauf)
                return false;

            joker = ablauf;

            if (!ablauf.HatRunde)
            {
                ablauf.Schliesse([]);
                await WendeJokerAnAsync();
                return false;
            }

            VorphaseLaeuft = true;
            steuerung.SetMinigameInfo(ablauf.Setup());
            steuerung.MinigameRoundClosed = OnJokerRundeZuAsync;
            steuerung.MinigameSubmitted = OnJokerAbgabeAsync;
            ZeigeJoker();

            await steuerung.ResetRoundAsync(BuzzerControlsLayout.Minigame);
            await steuerung.StartMinigameRoundAsync();

            // Erst jetzt: das Ausspielen oben setzt selbst zurueck.
            steuerung.MinigameReset = OnJokerRundeReset;
            OnPropertyChanged(nameof(CurrentBuzzerLayout));
            return true;
        }

        /// <summary>Liest die Joker; <c>null</c> ohne Joker oder bei einer Stoerung - dann gibt es keine Runde, und das wird gesagt.</summary>
        private async Task<JokerAblauf?> ErstelleJokerAblaufAsync(GameGridCoordinate zelle)
        {
            var namen = (PlayersResultViewModel?.PlayerResultContextList ?? [])
                .Where(k => k.Player != null)
                .ToDictionary(k => k.Player.Id, k => k.Player.CalculatedDisplayName);

            try
            {
                var ablauf = new JokerAblauf(await JokerLaden(zelle.GameId), zelle.Id, namen);
                return ablauf.HatRunde || ablauf.HatEingeloeste ? ablauf : null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                UserPrompt.Inform("Die Joker dieses Spiels ließen sich nicht lesen – diese Frage läuft ohne Joker-Runde.", "Joker");
                return null;
            }
        }

        /// <summary>Die Runde ist zu: sofort einloesen und schreiben, dann wirken lassen, dann die Frage.</summary>
        internal async Task OnJokerRundeZuAsync(MinigameRoundClosed runde)
        {
            if (joker == null || !VorphaseLaeuft || Coordinate is not { } zelle)
                return;

            var neu = await EinloesenOderMeldenAsync(joker.Einloesungen(runde.Submissions, zelle.GameId, zelle.Id));

            // V12: erst jetzt erfahren die Telefone, was gilt - eine hier abgelehnte Wahl stand sonst kurz als eingesetzt da.
            if (BuzzerControlsViewModel != null)
                await BuzzerControlsViewModel.ConfirmJokersAsync(neu.Select(j => j.Id).ToList());

            joker.Schliesse(neu);
            VorphaseLaeuft = false;
            LoeseVorphase();
            await WendeJokerAnAsync();

            OnPropertyChanged(nameof(CurrentBuzzerLayout));
            OnPropertyChanged(nameof(MinispielSteuerungVisibility));

            await PrepareBuzzerlayoutAsync();
        }

        /// <summary>Fail closed (A10): scheitert das Schreiben, gilt kein Joker - und der Spielleiter erfaehrt, dass es eine Stoerung war.</summary>
        private async Task<List<PlayerJoker>> EinloesenOderMeldenAsync(IReadOnlyList<JokerRedemption> einloesungen)
        {
            if (einloesungen.Count == 0)
                return [];

            try
            {
                return await JokerEinloesen(einloesungen);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                UserPrompt.Inform("Die Joker dieser Frage ließen sich nicht speichern – sie gelten nicht und bleiben offen.", "Joker");
                return [];
            }
        }

        private static async Task<List<PlayerJoker>> LadeJokerAsync(Guid spiel)
        {
            using var ctrl = new PlayerJokersController();
            return await ctrl.GetForGameAsync(spiel);
        }

        /// <summary>Loest ein, was der Controller selbst zulaesst (zweiter Riegel), schreibt sofort und liest es zurueck.</summary>
        private static async Task<List<PlayerJoker>> LoeseJokerEinAsync(IReadOnlyList<JokerRedemption> einloesungen)
        {
            var jetzt = DateTimeOffset.Now;
            var angenommen = new HashSet<Guid>();

            using var ctrl = new PlayerJokersController();

            foreach (var einloesung in einloesungen)
            {
                if (await ctrl.RedeemAsync(einloesung, jetzt) == JokerRedeemOutcome.Redeemed)
                    angenommen.Add(einloesung.JokerId);
            }

            await ctrl.SaveChangesAsync();

            return angenommen.Count == 0 ? [] : (await ctrl.GetForGameAsync(einloesungen[0].GameId)).Where(j => angenommen.Contains(j.Id)).ToList();
        }

        /// <summary>Karten, Beamer und Antwortsperre folgen den eingeloesten Jokern.</summary>
        private async Task WendeJokerAnAsync()
        {
            if (joker is not { Zu: true } ablauf)
                return;

            ZeigeJoker();

            foreach (var karte in PlayersResultViewModel?.PlayerResultContextList ?? [])
                karte.SonderregelGeaendert();

            WerteSonderregel();

            var alle = (PlayersResultViewModel?.PlayerResultContextList ?? []).Where(k => k.Player != null).Select(k => k.Player.Id);

            if (ablauf.Antwortende(alle) is { } antwortende && BuzzerControlsViewModel != null)
                await BuzzerControlsViewModel.SetAnswerersAsync(antwortende, ablauf.Sperrsaetze);

            OnPropertyChanged(nameof(JokerSperreVisibility));
        }

        private Task OnJokerAbgabeAsync(MinigameSubmission abgabe)
        {
            ZeigeJoker(BuzzerControlsViewModel?.MinigameState?.Rules?.CurrentSubmissions.Count ?? 0);
            return Task.CompletedTask;
        }

        /// <summary>„Runde zuruecksetzen" in der Joker-Runde: sie beginnt neu, eine Wahl davor zaehlt nicht.</summary>
        private void OnJokerRundeReset() => _ = NeustartJokerRundeAsync();

        private async Task NeustartJokerRundeAsync()
        {
            try
            {
                if (VorphaseLaeuft && BuzzerControlsViewModel != null)
                    await BuzzerControlsViewModel.StartMinigameRoundAsync();

                ZeigeJoker();
            }
            catch (Exception ex)
            {
                ExceptionManager.HandleException(ex);
            }
        }

        /// <summary>Beamer und Spielleiter: wie viele entschieden haben - nie, was; danach, wer welchen Joker setzt.</summary>
        private void ZeigeJoker(int entschieden = 0)
        {
            if (joker == null)
                return;

            var stand = VorphaseLaeuft
                ? $"Joker-Runde: {entschieden} von {joker.Halter} haben entschieden"
                : joker.Hinweis.Length > 0 ? "Joker-Runde zu" : "Joker-Runde zu – kein Joker im Spiel";

            Sonderregel.Zeige(JokerAblauf.Titel, JokerAblauf.Regeltext, stand, VorphaseLaeuft, joker.Hinweis, JokerAblauf.Bereich);
        }

        // --- Der Spielleiter hebt eine Joker-Sperre auf (E1) ---

        private bool jokerSperreAufgehoben;

        public Visibility JokerSperreVisibility
            => joker is { Zu: true, Gesperrte.Count: > 0 } && !jokerSperreAufgehoben ? Visibility.Visible : Visibility.Collapsed;

        private AsyncRelayCommand? jokerSperreAufhebenCommand;

        public ICommand JokerSperreAufhebenCommand => jokerSperreAufhebenCommand ??= new AsyncRelayCommand(_ => HebeJokerSperreAufAsync());

        internal async Task HebeJokerSperreAufAsync()
        {
            if (BuzzerControlsViewModel == null || JokerSperreVisibility != Visibility.Visible)
                return;

            await BuzzerControlsViewModel.SetAnswerersAsync(null);
            jokerSperreAufgehoben = true;
            OnPropertyChanged(nameof(JokerSperreVisibility));
        }

        // --- Die Frage wird verlassen ---

        /// <summary>
        /// Verlaesst der Spielleiter die Frage ohne Wertung (Fenster zu, ohne Bewertung auf einer Karte und ohne
        /// abgeschlossene Zelle), gehen die hier eingeloesten Joker wieder auf. Die Sperre gilt nur dieser Frage.
        /// </summary>
        private async Task RaeumeJokerAufAsync()
        {
            if (joker is not { } ablauf || Coordinate is not { } zelle)
                return;

            joker = null;

            if (ablauf.Gesperrte.Count > 0)
                BuzzerControlsViewModel?.ClearAnswerers();

            if (ablauf.NeuEingeloest.Count == 0 || IsDone || PlayersResultViewModel?.HatOffeneBewertungen == true)
                return;

            try
            {
                using var ctrl = new PlayerJokersController();
                await ctrl.ReleaseAsync(zelle.Id, ablauf.NeuEingeloest);
                await ctrl.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                ExceptionManager.HandleException(ex);
            }
        }

        /// <summary>Was auf den Karten wirkt: die Sonderregel nach ihrer Vorphase, sonst die Joker nach ihrer Runde.</summary>
        private Func<IReadOnlyDictionary<Guid, SonderregelEingang>, IReadOnlyDictionary<Guid, Punktevorschlag>>? ZellenWertung()
            => sonderregel is { VorphaseZu: true } regel ? regel.WerteAlle
                : joker is { Zu: true } ablauf ? ablauf.WerteAlle
                : null;
    }
}
