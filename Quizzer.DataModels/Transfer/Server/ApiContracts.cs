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
        IReadOnlyList<GameStatistic> Games,
        Leaderboard Leaderboard);

    /// <summary>
    /// Die Bestenliste ueber Abende (M35 C9) - nur aus den Spielen, die <see cref="StatisticsResponse.Games"/> zeigt.
    /// </summary>
    /// <param name="Categories">Je Minispiel-Art mit Bestwert eine Liste, auch wenn sie noch leer ist; Kopfrechnen je Schwierigkeit.</param>
    /// <param name="MostSteals">Die meisten Klaus, summiert ueber die Spiele.</param>
    public sealed record Leaderboard(IReadOnlyList<LeaderboardCategory> Categories, IReadOnlyList<StealsEntry> MostSteals);

    /// <param name="Variant">Die Spielweise, wo die Art je Spielweise eine eigene Liste hat (Kopfrechnen je Schwierigkeit,
    /// M37 E05); sonst <c>null</c>. Aeltere WPF kennen das Feld nicht - deshalb nennt der Titel die Spielweise selbst.</param>
    /// <param name="Title">„Schnellste Reaktion", „Schnellster Rechner – leicht".</param>
    /// <param name="KindName">Der Name der Art, „Reaktion".</param>
    /// <param name="Unit">Die Einheit des Rohwerts, „ms" - der Klartext je Eintrag traegt sie schon.</param>
    public sealed record LeaderboardCategory(
        MinigameKind Kind, int? Variant, string Title, string KindName, string Unit, bool LowerIsBetter, IReadOnlyList<LeaderboardEntry> Entries);

    /// <summary>Ein Platz: wer, wie gut (Rohwert und Klartext mit Einheit), in welchem Spiel, wann.</summary>
    /// <param name="Rank">Gleichstand teilt den Platz.</param>
    public sealed record LeaderboardEntry(
        int Rank, Guid PlayerId, string PlayerName, int Value, string ValueText, Guid GameId, string GameName, DateTimeOffset? RecordedAt);

    public sealed record StealsEntry(int Rank, Guid PlayerId, string PlayerName, int Steals);

    public sealed record MediaFileInfo(string Path, long Size, string Sha256);

    /// <summary>
    /// Das bereitgestellte WPF-Paket (M36 A7), ohne Anmeldung abrufbar. <see cref="Version"/> im
    /// Muster von <see cref="PackageVersion"/>, <see cref="Sha256"/> als Hex.
    /// </summary>
    public sealed record ClientPackageInfo(string Version, long Size, string Sha256);
}
