using Microsoft.EntityFrameworkCore;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;

namespace Quizzer.Logic.Controller.TypedControllers
{
    /// <summary>
    /// Joker eines Spiels (M35 C7a): vergeben, zurücknehmen, aufräumen. Eingelöst wird in C7b.
    /// </summary>
    public class PlayerJokersController : GenericController<PlayerJoker>
    {
        public PlayerJokersController()
        {
        }

        public PlayerJokersController(ControllerBase other) : base(other)
        {
        }

        /// <summary>Alle Joker eines Spiels, älteste zuerst.</summary>
        public async Task<List<PlayerJoker>> GetForGameAsync(Guid gameId)
        {
            return await CreateReadQuery(Actions.GetAll)
                .Where(j => j.GameId == gameId)
                .OrderBy(j => j.GrantedAt)
                .ToListAsync();
        }

        /// <summary>
        /// Vergibt einen Joker. Der Grund ist Pflicht und wird auf <see cref="PlayerJoker.MaxReasonLength"/>
        /// geprüft, bevor die Datenbank es mit einem rohen Fehler tut. Schreibt nicht selbst.
        /// </summary>
        public async Task<PlayerJoker> GrantAsync(Guid gameId, Guid playerId, JokerKind kind, string reason, DateTimeOffset now)
        {
            var grund = (reason ?? string.Empty).Trim();

            if (grund.Length == 0)
                throw new ArgumentException("Ein Joker braucht einen Grund.", nameof(reason));

            if (grund.Length > PlayerJoker.MaxReasonLength)
                throw new ArgumentException($"Der Grund ist länger als {PlayerJoker.MaxReasonLength} Zeichen.", nameof(reason));

            if (!Enum.IsDefined(kind))
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unbekannte Joker-Art.");

            var joker = new PlayerJoker
            {
                Id = Guid.NewGuid(),
                GameId = gameId,
                PlayerId = playerId,
                Kind = kind,
                Reason = grund,
                GrantedAt = now,
            };

            await InsertAsync(joker);
            return joker;
        }

        /// <summary>
        /// Nimmt einen noch nicht eingelösten Joker zurück. Ein eingelöster hat schon gewirkt und
        /// bleibt stehen - <c>false</c>. Schreibt nicht selbst.
        /// </summary>
        public async Task<bool> WithdrawAsync(Guid jokerId)
        {
            var joker = await EntitySet.AsNoTracking().FirstOrDefaultAsync(j => j.Id == jokerId);

            if (joker == null || joker.IsRedeemed)
                return false;

            return await DeleteAsync(jokerId);
        }

        /// <summary>Für das Löschen eines Spiels: die Joker hängen mit NO ACTION an Spiel und Zelle.</summary>
        public async Task<int> DeleteByGameIdAsync(Guid gameId)
        {
            return await EntitySet
                .Where(j => j.GameId == gameId)
                .ExecuteDeleteAsync();
        }

        /// <summary>
        /// Für das Löschen eines Mitspielers: Sperren, die ihn treffen. <c>TargetPlayerId</c> steht auf
        /// NO ACTION - CASCADE und SET NULL lehnt SQL Server als zweiten Pfad vom Spieler ab (Msg 1785).
        /// </summary>
        public async Task<int> DeleteTargetingPlayerAsync(Guid playerId)
        {
            return await EntitySet
                .Where(j => j.TargetPlayerId == playerId)
                .ExecuteDeleteAsync();
        }

        /// <summary>Ob an einer der Zellen ein Joker eingelöst wurde (NO ACTION auf die Zelle).</summary>
        public async Task<bool> AnyRedeemedAtAsync(IEnumerable<Guid> cellIds)
        {
            var ids = cellIds?.Distinct().ToList() ?? [];

            if (ids.Count == 0)
                return false;

            return await EntitySet.AnyAsync(j => j.GameGridCoordinateId != null && ids.Contains(j.GameGridCoordinateId.Value));
        }
    }
}
