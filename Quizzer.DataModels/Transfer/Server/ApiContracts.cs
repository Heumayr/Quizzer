using Quizzer.DataModels.Enumerations;
using System.Text.Json.Serialization;

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

        /// <summary>Kopf, mit dem das WPF sein Abgleichformat nennt (Versionsriegel, M36 A4).</summary>
        public const string SyncFormatHeader = "X-Quizzer-Sync-Format";
    }

    public sealed record VersionResponse(int ApiVersion, int SyncFormatVersion, string ServerVersion);

    /// <summary><see cref="TotpCode"/>: sechs Ziffern oder ein Wiederherstellungscode (M36 A5); fehlt bei alten Clients.</summary>
    public sealed record LoginRequest(string UserName, string Password, string? TotpCode = null);

    public sealed record RefreshRequest(string RefreshToken);

    public sealed record AccountInfo(
        Guid AccountId,
        string UserName,
        AccountRole Role,
        Guid PlayerId,
        string PlayerName,
        bool TotpEnabled = false);

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
        DateTimeOffset? LastLoginAt,
        bool TotpEnabled = false);

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

    /// <summary>
    /// Eine Fehlermeldung, die der Client so anzeigen darf. <see cref="Code"/> ist maschinenlesbar
    /// (<see cref="ApiErrorCodes"/>) und fehlt im Draht, wenn leer - ein altes WPF liest nur die Meldung.
    /// </summary>
    public sealed record ApiError(
        string Message,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Code = null);

    public static class ApiErrorCodes
    {
        /// <summary>Kennwort stimmt, das Konto verlangt aber einen Code (401).</summary>
        public const string TotpRequired = "totp_required";

        /// <summary>Admin ohne eingerichtete Zwei-Faktor-Anmeldung: erst einrichten (403).</summary>
        public const string TotpSetupRequired = "totp_setup_required";
    }

    /// <summary>Begonnene Einrichtung: Geheimnis (Base32) zum Abtippen und dieselbe Angabe als otpauth-URI.</summary>
    public sealed record TotpSetupResponse(string Secret, string OtpAuthUri);

    public sealed record TotpConfirmRequest(string Code);

    /// <summary>Die Codes stehen nur hier im Klartext; die Ausweise tragen den neuen Stand.</summary>
    public sealed record TotpConfirmResponse(IReadOnlyList<string> RecoveryCodes, TokenResponse Tokens);

    public sealed record TotpDisableRequest(string Password, string Code);

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
