using Quizzer.DataModels.Enumerations;
using System.Security.Claims;

namespace Quizzer.Server.Auth
{
    /// <summary>Wer gerade fragt - aus den Ansprueche des geprueften Ausweises.</summary>
    public sealed record CurrentAccount(Guid AccountId, Guid PlayerId, AccountRole Role)
    {
        public const string PlayerIdClaim = "player_id";

        public const string RoleClaim = "role";

        public bool IsAdmin => Role == AccountRole.Admin;

        public static CurrentAccount From(ClaimsPrincipal user)
        {
            var accountId = Guid.Parse(user.FindFirstValue("sub")
                ?? user.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? throw new InvalidOperationException("Ausweis ohne Konto."));

            var playerId = Guid.Parse(user.FindFirstValue(PlayerIdClaim)
                ?? throw new InvalidOperationException("Ausweis ohne Mitspieler."));

            var role = Enum.Parse<AccountRole>(user.FindFirstValue(RoleClaim) ?? nameof(AccountRole.Member));

            return new CurrentAccount(accountId, playerId, role);
        }
    }
}
