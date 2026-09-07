using Quizzer.DataModels.Enumerations;
using Microsoft.EntityFrameworkCore;
using Quizzer.DataModels.Models.Base;
using System;
using System.Collections.Generic;
using System.Text;

namespace Quizzer.Logic.Controller.TypedControllers
{
    public class PlayersController : GenericController<Player>
    {
        public PlayersController()
        {
        }

        public PlayersController(ControllerBase other) : base(other)
        {
        }

        /// <summary>
        /// Laedt die Punktehistorie mit.
        /// <para>
        /// <b>Bis 2026-09-07 stand hier nur ein Durchreichen.</b> Die Sammlung
        /// <c>Player.CurrentQuestionResults</c> wurde damit NIRGENDS gefuellt - kein einziges
        /// <c>Include</c> im ganzen Baum. Das Mitspieler-Fenster zeigte deshalb immer
        /// "Punkte gesamt: 0" und ein leeres Raster, ganz gleich, wie viel jemand erzielt hatte.
        /// Eine Anzeige, die dauerhaft eine falsche Zahl nennt, ist schlechter als keine.
        /// </para>
        /// <para>
        /// <c>Game</c> und <c>QuestionBase</c> kommen mit, weil die beiden Spalten des Rasters
        /// sie brauchen - ohne sie stuenden dort leere Zellen.
        /// </para>
        /// </summary>
        protected override IQueryable<Player> SetQueryAttributes(IQueryable<Player> query, Actions action)
        {
            if (action == Actions.Get)
            {
                query = query
                    .Include(p => p.CurrentQuestionResults).ThenInclude(r => r.Game)
                    .Include(p => p.CurrentQuestionResults).ThenInclude(r => r.QuestionBase);
            }

            return base.SetQueryAttributes(query, action);
        }

        protected override Task<Player> BeforeActionAsync(Player entity, Actions action)
        {
            return base.BeforeActionAsync(entity, action);
        }

        protected override Task<Player> AfterActionAsync(Player entity, Actions action)
        {
            return base.AfterActionAsync(entity, action);
        }
    }
}