using Microsoft.EntityFrameworkCore;
using Quizzer.Logic.Context;
using Quizzer.Server.Auth;

namespace Quizzer.Server.Sync
{
    /// <summary>
    /// Was ein Konto sehen darf (M9, Rueckfrage „eigene + geteilte"). Die Regeln stehen
    /// <b>in den Abfragen</b>, nicht nachgelagert im Speicher (standards-sicherheit A01).
    /// <list type="bullet">
    /// <item>Spiele: als Gamemaster angelegt, oder als Mitspieler eingetragen.</item>
    /// <item>Fragen: eigene, besitzerlose, geteilte - und alle, die in einem sichtbaren Spiel
    /// stecken oder dort ein Ergebnis haben.</item>
    /// <item>Kategorien, Designs, Mitspieler: alle (ohne Kennwort-Hash).</item>
    /// </list>
    /// Der Admin sieht alles.
    /// </summary>
    internal static class Visibility
    {
        public static IQueryable<Guid> GameIds(DataContext context, CurrentAccount account)
        {
            if (account.IsAdmin)
                return context.Games.Select(g => g.Id);

            var me = account.PlayerId;
            return context.Games
                .Where(g => g.ModeratorPlayerId == me || context.PlayerXGames.Any(x => x.GameId == g.Id && x.PlayerId == me))
                .Select(g => g.Id);
        }

        public static IQueryable<Guid> QuestionIds(DataContext context, CurrentAccount account)
        {
            if (account.IsAdmin)
                return context.QuestionBases.Select(q => q.Id);

            var me = account.PlayerId;
            var games = GameIds(context, account);

            return context.QuestionBases
                .Where(q => q.OwnerPlayerId == null
                    || q.OwnerPlayerId == me
                    || q.IsShared
                    || context.GameGridCoordinates.Any(c => c.QuestionBaseId == q.Id && games.Contains(c.GameId))
                    || context.QuestionResults.Any(r => r.QuestionBaseId == q.Id && games.Contains(r.GameId)))
                .Select(q => q.Id);
        }
    }
}
