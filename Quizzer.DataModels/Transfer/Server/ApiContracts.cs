using Quizzer.DataModels.Enumerations;

namespace Quizzer.DataModels.Transfer.Server
{
    /// <summary>
    /// Die Vertraege der REST-API des Quizzer-Servers (M9). Server und WPF nutzen dieselben
    /// Klassen; ein Feld, das hier fehlt, gibt es im Draht nicht.
    /// </summary>
    public static class ApiInfo
    {
        /// <summary>Wird erhoeht, wenn ein aelterer Client nicht mehr mit dem Server sprechen kann.</summary>
        public const int ApiVersion = 1;

        public const string BasePath = "/api/v1";
    }

    public sealed record VersionResponse(int ApiVersion, int SyncFormatVersion, string ServerVersion);

    public sealed record LoginRequest(string UserName, string Password);

    public sealed record RefreshRequest(string RefreshToken);

    public sealed record AccountInfo(
        Guid AccountId,
        string UserName,
        AccountRole Role,
        Guid PlayerId,
        string PlayerName);

    public sealed record TokenResponse(
        string AccessToken,
        DateTimeOffset AccessTokenExpiresAt,
        string RefreshToken,
        DateTimeOffset RefreshTokenExpiresAt,
        AccountInfo Account);

    public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

    public sealed record AccountListItem(
        Guid AccountId,
        string UserName,
        AccountRole Role,
        Guid PlayerId,
        string PlayerName,
        bool IsDisabled,
        DateTimeOffset CreatedAt,
        DateTimeOffset? LastLoginAt);

    /// <summary>
    /// Ein neues Konto. Entweder <see cref="PlayerId"/> (ein bestehender Mitspieler ohne Konto,
    /// etwa ein Gast) oder <see cref="NewPlayerName"/> (es entsteht ein neuer Mitspieler).
    /// </summary>
    public sealed record CreateAccountRequest(
        string UserName,
        string Password,
        AccountRole Role,
        Guid? PlayerId,
        string? NewPlayerName);

    /// <summary>Nur die gesetzten Felder werden geaendert.</summary>
    public sealed record UpdateAccountRequest(string? NewPassword, AccountRole? Role, bool? IsDisabled);

    /// <summary>Eine Fehlermeldung, die der Client so anzeigen darf.</summary>
    public sealed record ApiError(string Message);

    public sealed record GameStatistic(
        Guid GameId,
        string GameName,
        string GameMasterName,
        GameState State,
        int PlayerCount,
        int Rank,
        int FinalScore,
        int RightCount,
        int WrongCount,
        int PlayedCells,
        int TotalCells,
        bool IsGameMaster);

    public sealed record StatisticsResponse(
        Guid PlayerId,
        string PlayerName,
        int GamesPlayed,
        int GamesWon,
        int TotalScore,
        int GamesAsGameMaster,
        IReadOnlyList<GameStatistic> Games);

    public sealed record MediaFileInfo(string Path, long Size, string Sha256);
}
