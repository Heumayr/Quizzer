using Microsoft.EntityFrameworkCore;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;

namespace Quizzer.Logic.Controller.TypedControllers
{
    /// <summary>
    /// Joker eines Spiels (M35 C7): vergeben, zurücknehmen, einlösen, freigeben, aufräumen.
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

        /// <summary>
        /// Löst einen Joker für eine Zelle ein (M35 C7b) - der zweite Riegel hinter der Joker-Runde am Server, er prüft
        /// selbst: eigener, offener Joker dieses Spiels, Zelle dieses Spiels, höchstens einer je Spieler und Zelle, und
        /// bei der Sperre ein Ziel, das Mitspieler und nicht man selbst ist. Schreibt nicht selbst.
        /// </summary>
        public async Task<JokerRedeemOutcome> RedeemAsync(JokerRedemption einloesung, DateTimeOffset now)
        {
            ArgumentNullException.ThrowIfNull(einloesung);

            var joker = await EntitySet.FirstOrDefaultAsync(j => j.Id == einloesung.JokerId);
            var fehler = joker == null ? JokerRedeemOutcome.NotFound : await PruefeAsync(joker, einloesung);

            if (joker == null || fehler != JokerRedeemOutcome.Redeemed)
                return fehler;

            joker.GameGridCoordinateId = einloesung.CellId;
            joker.RedeemedAt = now;
            joker.TargetPlayerId = einloesung.TargetPlayerId;
            return JokerRedeemOutcome.Redeemed;
        }

        private async Task<JokerRedeemOutcome> PruefeAsync(PlayerJoker joker, JokerRedemption einloesung)
        {
            if (joker.PlayerId != einloesung.PlayerId)
                return JokerRedeemOutcome.NotOwner;

            if (joker.GameId != einloesung.GameId)
                return JokerRedeemOutcome.WrongGame;

            if (joker.IsRedeemed)
                return JokerRedeemOutcome.AlreadyRedeemed;

            if (!await CurrentContext.GameGridCoordinates.AnyAsync(c => c.Id == einloesung.CellId && c.GameId == einloesung.GameId))
                return JokerRedeemOutcome.CellNotInGame;

            if (await EntitySet.AnyAsync(j => j.PlayerId == einloesung.PlayerId && j.GameGridCoordinateId == einloesung.CellId))
                return JokerRedeemOutcome.SecondJokerForCell;

            return await ZielPasstAsync(joker.Kind, einloesung) ? JokerRedeemOutcome.Redeemed : JokerRedeemOutcome.TargetInvalid;
        }

        private async Task<bool> ZielPasstAsync(JokerKind art, JokerRedemption einloesung)
        {
            if (art != JokerKind.Block)
                return einloesung.TargetPlayerId == null;

            return einloesung.TargetPlayerId is { } ziel && ziel != einloesung.PlayerId
                && await CurrentContext.PlayerXGames.AnyAsync(x => x.GameId == einloesung.GameId && x.PlayerId == ziel);
        }

        /// <summary>
        /// Gibt Joker einer Zelle wieder frei (M35 C7b): der Spielleiter hat die Frage ohne Wertung verlassen. Nur die
        /// genannten und nur, wenn sie wirklich an dieser Zelle hängen. Schreibt nicht selbst.
        /// </summary>
        public async Task<int> ReleaseAsync(Guid cellId, IEnumerable<Guid> jokerIds)
        {
            var ids = jokerIds?.Distinct().ToList() ?? [];

            if (ids.Count == 0)
                return 0;

            var joker = await EntitySet.Where(j => ids.Contains(j.Id) && j.GameGridCoordinateId == cellId).ToListAsync();

            foreach (var j in joker)
            {
                j.GameGridCoordinateId = null;
                j.RedeemedAt = null;
                j.TargetPlayerId = null;
            }

            return joker.Count;
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
