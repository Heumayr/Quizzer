using LocalBuzzer.Service.Base;
using LocalBuzzer.Service.Base.States.Minispiele;
using Quizzer.DataModels.Questions.Minispiele;
using Quizzer.Views.StaticRessources;

namespace Quizzer.Views.BuzzerViews
{
    /// <summary>
    /// Minispiel-Teil der Buzzer-Steuerung (M35 B3): stellt den Rahmen am Server ein, reicht die
    /// Befehle des Spielleiters durch und die Abgaben auf den Oberflaechen-Thread zurueck.
    /// </summary>
    public partial class BuzzerControlsViewModel
    {
        /// <summary>Ein Spieler hat im Minispiel abgegeben.</summary>
        public Func<MinigameSubmission, Task>? MinigameSubmitted;

        /// <summary>Eine Runde ist zu oder das Minispiel abgebrochen.</summary>
        public Func<MinigameRoundClosed, Task>? MinigameRoundClosed;

        /// <summary>"Runde zuruecksetzen" hat das Minispiel am Server auf Anfang gestellt.</summary>
        public Action? MinigameReset;

        /// <summary>Der Rahmen am Server, solange er laeuft.</summary>
        public MinigameState? MinigameState => BuzzerController?.StateManager.MinigameState;

        public void SetMinigameInfo(MinigameSetup? setup)
        {
            if (setup != null)
                MinigameState?.Configure(setup);
        }

        public Task<bool> StartMinigameRoundAsync()
            => BuzzerController?.StartMinigameRoundAsync() ?? Task.FromResult(false);

        public Task<bool> RepeatMinigameRoundAsync()
            => BuzzerController?.RepeatMinigameRoundAsync() ?? Task.FromResult(false);

        public Task AbortMinigameAsync()
            => BuzzerController?.AbortMinigameAsync() ?? Task.CompletedTask;

        /// <summary>Schliesst die laufende Runde - beim Erklaerbaer nach „Verraten" (M35 C6).</summary>
        public Task CloseMinigameRoundAsync()
            => BuzzerController?.CloseMinigameRoundAsync() ?? Task.CompletedTask;

        /// <summary>Nur diese duerfen die gewoehnliche Frage beantworten (M35 C3b); <c>null</c>: alle. Mit Satz je Gesperrtem (C7b).</summary>
        public Task SetAnswerersAsync(IReadOnlyCollection<Guid>? antwortende, IReadOnlyDictionary<Guid, string>? saetze = null)
            => BuzzerController?.SetAnswerersAsync(antwortende, saetze) ?? Task.CompletedTask;

        /// <summary>Die Joker-Runde ist eingeloest - die Telefone erfahren, was gilt und was abgelehnt ist (V12).</summary>
        public Task<bool> ConfirmJokersAsync(IReadOnlyCollection<Guid> eingeloest)
            => BuzzerController?.ConfirmJokersAsync(eingeloest) ?? Task.FromResult(false);

        /// <summary>Hebt die Sperre auf, ohne zu senden - das tut das Zuruecksetzen danach.</summary>
        public void ClearAnswerers() => BuzzerController?.StateManager.AnswerLock.Clear();

        /// <summary>Der Spielleiter legt fest, wer spielt (M35 C2, E1).</summary>
        public Task<bool> SetMinigamePairAsync(Guid chooserId, Guid? partnerId)
            => BuzzerController?.SetMinigamePairAsync(chooserId, partnerId) ?? Task.FromResult(false);

        internal void SubscribeMinigameEvents(BuzzerController? ctrl)
        {
            if (ctrl == null)
                return;

            UnsubscribeMinigameEvents(ctrl);
            ctrl.EventBus.MinigameSubmitted += OnMinigameSubmitted;
            ctrl.EventBus.MinigameRoundClosed += OnMinigameRoundClosed;
        }

        internal void UnsubscribeMinigameEvents(BuzzerController? ctrl)
        {
            if (ctrl == null)
                return;

            ctrl.EventBus.MinigameSubmitted -= OnMinigameSubmitted;
            ctrl.EventBus.MinigameRoundClosed -= OnMinigameRoundClosed;
        }

        public async void OnMinigameSubmitted(MinigameSubmission abgabe)
        {
            try
            {
                TrackInput(abgabe.Player.Id, abgabe.Value switch
                {
                    ReactionAttempt versuch => ReaktionEvaluator.Describe(versuch),
                    TippsprintStand stand => TippsprintEvaluator.Describe(stand.Tipps),
                    TauziehenZug zug => TippsprintEvaluator.Describe(zug.Tipps),
                    StoppuhrVersuch stopp => StoppuhrEvaluator.Describe(stopp),
                    ZockerAusstieg aus => $"bei {ZockerEvaluator.Text(aus.Multiplikator)} ausgestiegen",
                    KopfrechnenAntwort antwort => KopfrechnenEvaluator.Describe(antwort.Stand),
                    BlitzrundeFortschritt fortschritt => $"{fortschritt.Beantwortet} beantwortet",
                    BuchstabensalatVersuch versuch => versuch.Richtig ? $"Begriff {versuch.Begriff} gelöst" : "daneben",
                    ErklaerbaerVersuch versuch => versuch.Richtig ? "Begriff erraten" : "daneben",
                    BluffAbgabe bluff => bluff.Phase == 1 ? "Bluff abgegeben" : "gewählt",
                    KartoffelAntwort antwort => antwort.Richtig ? "richtig – gibt gleich weiter" : "daneben",
                    KartoffelWeitergabe => "hat weitergegeben",
                    MemoryZug zug => $"{zug.Stand.Paare} {(zug.Stand.Paare == 1 ? "Paar" : "Paare")}" + (zug.Stand.Fertig != null ? " – fertig" : string.Empty),

                    // M35 C2: geheime Wahlen bleiben auch hier „abgegeben" - der Beamer loest sie auf.
                    PlayerChosen or WheelTargetChosen => "hat gewählt",
                    _ => "abgegeben",
                });

                await RunOnUiAsync(async () =>
                {
                    if (MinigameSubmitted != null)
                        await MinigameSubmitted.Invoke(abgabe);
                });
            }
            catch (Exception ex)
            {
                ExceptionManager.HandleException(ex);
            }
        }

        public async void OnMinigameRoundClosed(MinigameRoundClosed runde)
        {
            try
            {
                await RunOnUiAsync(async () =>
                {
                    if (MinigameRoundClosed != null)
                        await MinigameRoundClosed.Invoke(runde);
                });
            }
            catch (Exception ex)
            {
                ExceptionManager.HandleException(ex);
            }
        }
    }
}
