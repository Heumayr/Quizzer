using Microsoft.EntityFrameworkCore;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Transfer.Sync;

namespace Quizzer.Server.Sync
{
    /// <summary>Rechte am Joker (M35 C7) - eigene Teildatei, damit <c>PushService.cs</c> unter 500 Zeilen bleibt.</summary>
    internal sealed partial class WriteRules
    {
        /// <summary>
        /// M35 C7: ein Joker gehört einem Mitspieler des Spiels, eine Sperre trifft nur einen Mitspieler,
        /// und die Art ist eine bekannte - eine fremde Zahl spielte C7b sonst als irgendetwas.
        /// </summary>
        private async Task<string?> CheckJokerAsync(SyncRow row, Guid gameId, CancellationToken ct)
        {
            if (!row.Values.TryGetValue(nameof(PlayerJoker.Kind), out var kind)
                || !kind.TryGetInt32(out var art) || !Enum.IsDefined((JokerKind)art))
                return "Unbekannte Joker-Art.";

            if (!await IsMemberAsync(GetGuid(row, nameof(PlayerJoker.PlayerId)), gameId, ct))
                return "Der Joker gehört keinem Mitspieler dieses Spiels.";

            if (GetNullableGuid(row, nameof(PlayerJoker.TargetPlayerId)) is not { } target)
                return null;

            // Wie PlayerJokersController.RedeemAsync: ein Ziel hat nur die Sperre, und nie gegen sich selbst.
            if ((JokerKind)art != JokerKind.Block)
                return "Nur die Sperre hat ein Ziel.";

            if (target == GetGuid(row, nameof(PlayerJoker.PlayerId)))
                return "Eine Sperre gegen sich selbst gibt es nicht.";

            if (!await IsMemberAsync(target, gameId, ct))
                return "Die Sperre trifft keinen Mitspieler dieses Spiels.";

            return null;
        }

        /// <summary>Ob der Spieler im Spiel mitspielt - am Server oder neu in diesem Push.</summary>
        private async Task<bool> IsMemberAsync(Guid playerId, Guid gameId, CancellationToken ct)
        {
            if (await context.PlayerXGames.AsNoTracking().AnyAsync(x => x.GameId == gameId && x.PlayerId == playerId, ct))
                return true;

            return request.Upserts.Any(u => u.Row.Type == nameof(PlayerXGame)
                && GetGuid(u.Row, nameof(PlayerXGame.GameId)) == gameId
                && GetGuid(u.Row, nameof(PlayerXGame.PlayerId)) == playerId);
        }
    }
}
