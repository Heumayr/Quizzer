using Microsoft.EntityFrameworkCore;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using System;
using System.Collections.Generic;
using System.Text;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace Quizzer.Logic.Controller.TypedControllers
{
    public class GamesController : GenericController<Game>
    {
        public GamesController()
        {
        }

        public GamesController(ControllerBase other) : base(other)
        {
        }

        /// <summary>
        /// Liefert die Bezeichnungen der Spiele, die von der genannten Person geleitet werden.
        /// <para>
        /// Gebraucht wird das vor dem Loeschen eines Mitspielers: <c>Game.ModeratorPlayerId</c>
        /// steht auf NO ACTION (Migration <c>20260414180046_Moderator</c> legt den
        /// Fremdschluessel ohne <c>onDelete</c> an), das Loeschen scheitert also in der
        /// Datenbank. Ohne diese Abfrage bekaeme der Spielleiter einen rohen
        /// Fremdschluesselfehler zu sehen - dieselbe Stelle, die bei den Fragen schon
        /// <see cref="GameGridCoordinatesController.GameNamesUsingQuestionAsync"/> abfaengt.
        /// </para>
        /// </summary>
        public async Task<List<string>> GameNamesModeratedByAsync(Guid playerId)
        {
            if (playerId == Guid.Empty)
                return [];

            return await EntitySet
                .Where(g => g.ModeratorPlayerId == playerId)
                .Select(g => g.Designation)
                .Distinct()
                .ToListAsync();
        }

        protected override IQueryable<Game> SetQueryAttributes(IQueryable<Game> query, Actions action)
        {
            if (action == Actions.Get)
            {
                // Kein eigenes AsNoTracking hier: die Basisklasse setzt es samt
                // Identitaetsaufloesung, und ein zweiter Aufruf wuerde sie wieder abschalten.
                query = query.Include(q => q.Headers)
                 .Include(q => q.GameGridCoordinates).ThenInclude(t => t.QuestionBase).ThenInclude(q => q!.Category)
                 .Include(q => q.GameGridCoordinates).ThenInclude(t => t.QuestionResults)
                 .Include(q => q.PlayerXGames).ThenInclude(t => t.Player)
                 .Include(q => q.Moderator)
                 .Include(q => q.GameTheme);
            }

            return base.SetQueryAttributes(query, action);
        }

        protected override async Task<Game> BeforeActionAsync(Game entity, Actions action)
        {
            if ((action & Actions.DeleteActions) > 0)
            {
                using var ctrlPlayerXGame = new PlayerXGamesController(this);
                using var ctrlResults = new QuestionResultsController(this);
                using var ctrlCoordinate = new GameGridCoordinatesController(this);

                await ctrlPlayerXGame.DeleteByGameIdAsync(entity.Id);
                await ctrlResults.DeleteByGameIdAsync(entity.Id);
                await ctrlCoordinate.DeleteByGameIdAsync(entity.Id);
            }

            return await base.BeforeActionAsync(entity, action);
        }

        protected override Task<Game> AfterActionAsync(Game entity, Actions action)
        {
            if (action == Actions.Get)
            {
                foreach (var result in entity.QuestionResults)
                {
                    // Rueckverweise nach dem Laden setzen.
                    result.Game = entity;

                    // ACHTUNG: DAS KANN NULL SEIN, und der Kommentar hier behauptete bis
                    // 2026-09-07 das Gegenteil ("must be existend!"). Wer aus dem Spiel
                    // genommen wurde, steht nicht mehr in der Mannschaft - seine
                    // Ergebniszeilen bleiben aber. In der Spieldatenbank lagen drei solche
                    // Zeilen, und "Naechster waehlt aus" fiel darueber mit einer
                    // NullReferenceException.
                    // WER DIESE EIGENSCHAFT LIEST, FILTERT AUF null. So machen es
                    // GameGridCoordinateViewModel.WinnerEntries und
                    // CurrentQuestionViewModel.CoordinateCorrectedAnsweredPlayers.
                    result.Player = entity.Players.FirstOrDefault(p => p.Id == result.PlayerId)!;

                    // Diese beiden sind gedeckt, und zwar durch den Weg hierher:
                    // Game.QuestionResults rechnet sich aus GameGridCoordinates.SelectMany,
                    // jedes Ergebnis haengt also an einer geladenen Zelle und wird hier immer
                    // gefunden. GEMESSEN 2026-09-07: ein Null-Schutz an dieser Stelle wurde
                    // absichtlich ausgebaut und die Zusicherung blieb gruen - er waere eine
                    // Vorsicht ohne belegten Anlass.
                    // Die Frage der Zelle DARF null sein (leere Zelle), das traegt QuestionBase
                    // als nullbarer Rueckverweis.
                    var zelle = entity.GameGridCoordinates
                        .First(c => c.Id == result.GameGridCoordinateId);

                    result.GameGridCoordinate = zelle;
                    result.QuestionBase = zelle.QuestionBase!;
                }
            }

            return base.AfterActionAsync(entity, action);
        }
    }
}