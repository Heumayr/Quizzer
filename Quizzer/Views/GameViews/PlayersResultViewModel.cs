using Quizzer.Base;
using Quizzer.DataModels.Models.Base;
using Quizzer.Logic.Controller.TypedControllers;
using Quizzer.Views.GameViews.Sub;
using Quizzer.Views.StaticRessources;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Windows.Controls;
using System.Windows.Input;
using static LocalBuzzer.Service.Base.States.BuzzerKeySelector;

namespace Quizzer.Views.GameViews
{
    public partial class PlayersResultViewModel : ViewModelBase
    {
        private List<PlayerResultContext> playerResultContextList = new();
        private Player? currentBuzzerWinner;

        public List<QuestionResult> Results => PlayerResultContextList.Select(x => x.Result).ToList();

        public GameGridCoordinate? Coordinate { get; private set; }

        public int Columns => PlayerResultContextList.Count();

        public GamePlayerViewModel? GamePlayerViewModel { get; set; }

        public CurrentQuestionViewModel? CurrentQuestionViewModel { get; set; }

        public Player? CurrentBuzzerWinner
        {
            get => currentBuzzerWinner;
            set
            {
                currentBuzzerWinner = value;
                OnPropertyChanged();

                foreach (var context in PlayerResultContextList)
                {
                    context.CurrentBuzzerWinner = value;
                }

                GamePlayerViewModel?.CurrentBuzzerWinner = value;
            }
        }

        public List<PlayerResultContext> PlayerResultContextList
        {
            get => playerResultContextList;
            set
            {
                playerResultContextList = value;
                OnPropertyChanged();
            }
        }

        public ConcurrentDictionary<Guid, SelectionResult>? SelectionResultsDic
        {
            get => field;
            set
            {
                field = value;
                OnPropertyChanged();

                foreach (var ctx in PlayerResultContextList)
                {
                    SelectionResult? result = null;

                    value?.TryGetValue(ctx.Player.Id, out result);

                    ctx.SelectionResult = result;
                }
            }
        }

        public void SetSelectionResult(SelectionResult selectionResult)
        {
            if (selectionResult == null)
                return;

            var ctx = PlayerResultContextList.FirstOrDefault(ctx => ctx.Player.Id == selectionResult.PlayerId);

            if (ctx == null)
                return;

            ctx.SelectionResult = selectionResult;
        }

        public async Task SetCoordinateAsync(GameGridCoordinate coordinate)
        {
            if (coordinate == null) throw new ArgumentNullException(nameof(coordinate));
            if (coordinate.QuestionBaseId == null || coordinate.QuestionBaseId == Guid.Empty) throw new Exception("No question set to coordinate");

            var playersCount = coordinate.Game.Players.Count();

            if (playersCount == 0)
            {
                throw new Exception("No players loaded");
            }

            Coordinate = coordinate;

            using var ctrlResults = new QuestionResultsController();

            var results = await ctrlResults.GetAllResultsForCoordinate(Coordinate.Id) ?? new();

            // Ergebniszeilen von Spielern, die nicht (mehr) mitspielen, werden geraeumt statt
            // gezaehlt. Bis 2026-09-06 warf diese Stelle "Invalid results count", und die Zelle
            // war dauerhaft nicht mehr spielbar - es genuegte, waehrend eines Spiels einen Spieler
            // aus der Mannschaft zu nehmen, nachdem schon eine Zelle gespielt war.
            results = await RemoveResultsOfFormerPlayersAsync(ctrlResults, results);

            var contextList = new List<PlayerResultContext>();
            foreach (var player in Coordinate.Game.Players)
            {
                var result = results.FirstOrDefault(r => r.PlayerId == player.Id);

                if (result == null)
                {
                    result = new()
                    {
                        PlayerId = player.Id,
                        GameId = Coordinate.GameId,
                        GameGridCoordinateId = Coordinate.Id,
                        QuestionBaseId = Coordinate.QuestionBaseId.Value,
                    };

                    await ctrlResults.InsertAsync(result);
                    await ctrlResults.SaveChangesAsync();
                    results.Add(result);
                }

                result.Player = player;
                result.Game = Coordinate.Game;
                result.QuestionBase = Coordinate.QuestionBase!;
                result.GameGridCoordinate = Coordinate;

                var newContext = new PlayerResultContext()
                {
                    PlayersResultViewModel = this,
                    Player = player,
                    Result = result,
                    CurrentQuestionViewModel = CurrentQuestionViewModel ?? throw new Exception("CurrentQuestionViewModel not set"),
                };

                contextList.Add(newContext);
            }

            PlayerResultContextList = contextList;
            OnPropertyChanged(nameof(Columns));
        }

        /// <summary>
        /// Raeumt Ergebniszeilen weg, deren Spieler nicht mehr zur Mannschaft dieses Spiels
        /// gehoert, und gibt die verbleibenden zurueck.
        /// <para>
        /// Nur solche Zeilen: die Ergebnisse der aktuellen Mitspieler bleiben unberuehrt. Sonst
        /// kostete ein Oeffnen der Zelle die Punkte einer bereits gespielten Runde.
        /// </para>
        /// </summary>
        private async Task<List<QuestionResult>> RemoveResultsOfFormerPlayersAsync(
            QuestionResultsController ctrlResults, List<QuestionResult> results)
        {
            if (Coordinate == null || results.Count == 0)
                return results;

            var mannschaft = Coordinate.Game.Players.Select(p => p.Id).ToHashSet();
            var verwaist = results.Where(r => !mannschaft.Contains(r.PlayerId)).ToList();

            if (verwaist.Count == 0)
                return results;

            foreach (var zeile in verwaist)
                await ctrlResults.DeleteAsync(zeile.Id);

            await ctrlResults.SaveChangesAsync();

            return results.Where(r => mannschaft.Contains(r.PlayerId)).ToList();
        }

        public override async Task VMSaveAsync() => await TrySaveAsync();

        /// <summary>
        /// Schreibt die Punktevergabe und sagt, ob es gelungen ist.
        /// <para>
        /// Der Rueckgabewert ist der Grund fuer diese Methode: bis 2026-09-06 verschluckte der
        /// Fangblock jeden Schreibfehler, und der Aufrufer schloss das Fenster trotzdem. Die Zelle
        /// wurde danach in einem zweiten, erfolgreichen Schreibvorgang auf erledigt gesetzt - die
        /// Punkte fehlten, die Zelle war dunkel, und aus der Oberflaeche gab es keinen Weg zurueck.
        /// </para>
        /// </summary>
        public async Task<bool> TrySaveAsync()
        {
            try
            {
                using var ctrl = new QuestionResultsController();
                foreach (var ctx in PlayerResultContextList)
                {
                    ctx.SetManipulationToResult();

                    await ctrl.UpsertAsync(ctx.Result);
                }

                await ctrl.SaveChangesAsync();

                foreach (var ctx in PlayerResultContextList)
                {
                    ctx.RefreshUIOnModelSave();
                }

                return true;
            }
            catch (Exception ex)
            {
                // Der Fang bleibt: ohne ihn faellt der Fehler in eine async-void-Behandlung und
                // reisst die Anwendung mit. Neu ist nur, dass der Aufrufer davon erfaehrt.
                ExceptionManager.HandleException(ex);
                return false;
            }
        }

        protected override async Task OnloadAsync()
        {
        }

        private AsyncRelayCommand? saveCommand;
        public ICommand SaveCommand => saveCommand ??= new AsyncRelayCommand(SaveAsync);

        private async Task SaveAsync(object? commandParameter)
        {
            await VMSaveAsync();
        }

        private AsyncRelayCommand? saveAndCloseCommand;
        public ICommand SaveAndCloseCommand => saveAndCloseCommand ??= new AsyncRelayCommand(SaveAndCloseAsync);

        private async Task SaveAndCloseAsync(object? commandParameter)
        {
            // Nur schliessen, wenn die Punkte auch geschrieben sind - sonst waeren sie weg, und
            // der Spielleiter haette keine Gelegenheit mehr, es erneut zu versuchen.
            if (await TrySaveAsync())
                Window?.Close();
        }

        public bool IsDoneAndShowFinishState { get; private set; }

        private AsyncRelayCommand? saveIsDoneFinishStateCommand;
        public ICommand SaveIsDoneFinishStateCommand => saveIsDoneFinishStateCommand ??= new AsyncRelayCommand(SaveIsDoneFinishStateAsync);

        private async Task SaveIsDoneFinishStateAsync(object? commandParameter)
        {
            // Die Zelle darf erst als erledigt gelten, wenn die Punkte in der Datenbank stehen.
            // Andernfalls bleibt das Fenster offen und ein zweiter Versuch ist moeglich - er
            // vergibt nichts doppelt, weil die Bewertungen bereits im Ergebnis stehen.
            if (!await TrySaveAsync())
                return;

            IsDoneAndShowFinishState = true;

            Window?.Close();
        }
    }
}