using Microsoft.EntityFrameworkCore;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Auth;
using Quizzer.DataModels.Transfer.Server;
using Quizzer.Logic.Context;
using Quizzer.Server.Infrastructure;
using System.Security.Cryptography;
using System.Text;

namespace Quizzer.Server.Auth
{
    /// <summary>
    /// Zwei-Faktor-Anmeldung (M36 A5, F-M36-02): je Konto einstellbar, Pflicht fuer Admins.
    /// Die eigene Uhr (<see cref="ServerSetup.TotpClockKey"/>) laesst Tests die Zeitschritte setzen,
    /// ohne die Ausweise zu verschieben.
    /// </summary>
    public sealed class TwoFactorService(
        TotpSecretProtector protector,
        [FromKeyedServices(ServerSetup.TotpClockKey)] TimeProvider clock,
        TimeProvider time,
        ILogger<TwoFactorService> log)
    {
        public const string Issuer = "Quizzer";

        public const int RecoveryCodeCount = 10;

        private const int RecoveryCodeBytes = 10;

        public const string AlreadyEnabled = "Die Zwei-Faktor-Anmeldung ist schon eingerichtet.";

        public const string AdminMustKeep = "Für Admin-Konten ist die Zwei-Faktor-Anmeldung Pflicht und lässt sich nicht abschalten.";

        /// <summary>Neues Geheimnis, noch nicht aktiv. Ein schon aktives wird nicht ersetzt.</summary>
        public async Task<(string? Error, TotpSetupResponse? Setup)> BeginSetupAsync(Guid accountId, CancellationToken ct)
        {
            await using var context = new DataContext();
            var account = await context.Accounts.FirstOrDefaultAsync(a => a.Id == accountId, ct);

            if (account == null)
                return ("Dieses Konto gibt es nicht mehr.", null);

            if (account.TotpEnabled)
                return (AlreadyEnabled, null);

            var secret = Totp.NewSecret();
            account.TotpSecretProtected = protector.Protect(account.Id, secret);
            account.TotpLastStep = null;
            await context.SaveChangesAsync(ct);

            var base32 = Totp.ToBase32(secret);
            return (null, new TotpSetupResponse(base32, Totp.KeyUri(Issuer, account.UserName, base32)));
        }

        /// <summary>
        /// Prueft den Code gegen das begonnene Geheimnis und schaltet ein. Liefert die
        /// Wiederherstellungscodes im Klartext; gespeichert wird erst mit dem SaveChanges des Aufrufers.
        /// </summary>
        internal async Task<(string? Error, IReadOnlyList<string>? Codes)> ActivateAsync(DataContext context, Account account, string? code, CancellationToken ct)
        {
            if (account.TotpEnabled)
                return (AlreadyEnabled, null);

            if (protector.Unprotect(account.Id, account.TotpSecretProtected) is not { } secret)
                return ("Bitte die Einrichtung zuerst beginnen.", null);

            if (Totp.Match(secret, code, clock.GetUtcNow(), account.TotpLastStep) is not { } step)
                return ("Der Code stimmt nicht. Stimmt die Uhrzeit auf Handy und Server?", null);

            account.TotpEnabled = true;
            account.TotpLastStep = step;

            var codes = await ReplaceRecoveryCodesAsync(context, account.Id, ct);
            log.LogInformation("Zwei-Faktor-Anmeldung eingerichtet: Konto {AccountId}", account.Id);
            return (null, codes);
        }

        /// <summary>
        /// Ein TOTP-Code oder ein Wiederherstellungscode fuer ein Konto mit eingeschaltetem TOTP.
        /// Beides wird in einem Schritt verbraucht - zwei gleichzeitige Anmeldungen, ein Gewinner.
        /// </summary>
        internal async Task<bool> VerifyAsync(DataContext context, Account account, string? code, CancellationToken ct)
        {
            if (!account.TotpEnabled || string.IsNullOrWhiteSpace(code))
                return false;

            if (!Totp.IsCodeFormat(code))
                return await ConsumeRecoveryCodeAsync(context, account.Id, code, ct);

            if (protector.Unprotect(account.Id, account.TotpSecretProtected) is not { } secret)
            {
                log.LogWarning("TOTP-Geheimnis nicht lesbar (Signierschlüssel gewechselt?): Konto {AccountId}", account.Id);
                return false;
            }

            if (Totp.Match(secret, code, clock.GetUtcNow(), account.TotpLastStep) is not { } step)
                return false;

            var taken = await context.Accounts
                .Where(a => a.Id == account.Id && (a.TotpLastStep == null || a.TotpLastStep < step))
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.TotpLastStep, step), ct);

            if (taken == 0)
                return false;

            // ExecuteUpdate hat die Zeilenversion geaendert; ohne Neuladen scheitert das naechste Speichern.
            await context.Entry(account).ReloadAsync(ct);
            return true;
        }

        /// <summary>Abschalten fuer Mitglieder, mit Kennwort und Code. <c>null</c> heisst erledigt.</summary>
        public async Task<string?> DisableAsync(Guid accountId, TotpDisableRequest request, CancellationToken ct)
        {
            await using var context = new DataContext();
            var account = await context.Accounts.FirstOrDefaultAsync(a => a.Id == accountId, ct);

            if (account == null)
                return "Dieses Konto gibt es nicht mehr.";

            if (account.Role == AccountRole.Admin)
                return AdminMustKeep;

            if (!account.TotpEnabled)
                return "Die Zwei-Faktor-Anmeldung ist gar nicht eingeschaltet.";

            if (!AccountService.VerifyPassword(account.PasswordHash, request.Password)
                || !await VerifyAsync(context, account, request.Code, ct))
                return "Kennwort oder Code stimmt nicht.";

            await ClearAsync(context, account, ct);
            await context.SaveChangesAsync(ct);
            log.LogInformation("Zwei-Faktor-Anmeldung abgeschaltet: Konto {AccountId}", account.Id);
            return null;
        }

        /// <summary>Der Admin setzt ein fremdes Konto zurueck (verlorenes Handy); dessen Sitzungen enden.</summary>
        public async Task<string?> ResetAsync(Guid accountId, Guid actingAccountId, CancellationToken ct)
        {
            if (accountId == actingAccountId)
                return "Die eigene Zwei-Faktor-Anmeldung setzt ein anderer Admin zurück.";

            await using var context = new DataContext();
            var account = await context.Accounts.FirstOrDefaultAsync(a => a.Id == accountId, ct)
                ?? throw new KeyNotFoundException();

            await ClearAsync(context, account, ct);
            await AccountService.RevokeAllAsync(context, account.Id, time.GetUtcNow(), ct);
            await context.SaveChangesAsync(ct);

            log.LogWarning("Zwei-Faktor-Anmeldung zurückgesetzt: Konto {AccountId} durch {ActingAccountId}", accountId, actingAccountId);
            return null;
        }

        public static string HashRecoveryCode(string code)
            => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(NormalizeRecoveryCode(code))));

        private static string NormalizeRecoveryCode(string? code)
            => new string((code ?? string.Empty).Where(char.IsAsciiLetterOrDigit).ToArray()).ToUpperInvariant();

        /// <summary>80 Bit Zufall, in Vierergruppen: ABCD-EFGH-JKLM-NPQR.</summary>
        private static string NewRecoveryCode()
        {
            var text = Totp.ToBase32(RandomNumberGenerator.GetBytes(RecoveryCodeBytes));
            return string.Join('-', Enumerable.Range(0, text.Length / 4).Select(i => text.Substring(i * 4, 4)));
        }

        private async Task<IReadOnlyList<string>> ReplaceRecoveryCodesAsync(DataContext context, Guid accountId, CancellationToken ct)
        {
            context.AccountRecoveryCodes.RemoveRange(await context.AccountRecoveryCodes.Where(c => c.AccountId == accountId).ToListAsync(ct));

            var now = time.GetUtcNow();
            var codes = Enumerable.Range(0, RecoveryCodeCount).Select(_ => NewRecoveryCode()).ToList();

            context.AccountRecoveryCodes.AddRange(codes.Select(c => new AccountRecoveryCode
            {
                Id = Guid.NewGuid(),
                AccountId = accountId,
                CodeHash = HashRecoveryCode(c),
                CreatedAt = now,
            }));

            return codes;
        }

        private async Task<bool> ConsumeRecoveryCodeAsync(DataContext context, Guid accountId, string code, CancellationToken ct)
        {
            if (NormalizeRecoveryCode(code).Length != RecoveryCodeBytes * 8 / 5)
                return false;

            var hash = HashRecoveryCode(code);
            var used = await context.AccountRecoveryCodes
                .Where(c => c.AccountId == accountId && c.CodeHash == hash && c.UsedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.UsedAt, time.GetUtcNow()), ct);

            if (used > 0)
                log.LogWarning("Wiederherstellungscode benutzt: Konto {AccountId}", accountId);

            return used > 0;
        }

        private static async Task ClearAsync(DataContext context, Account account, CancellationToken ct)
        {
            account.TotpEnabled = false;
            account.TotpSecretProtected = null;
            account.TotpLastStep = null;
            context.AccountRecoveryCodes.RemoveRange(await context.AccountRecoveryCodes.Where(c => c.AccountId == account.Id).ToListAsync(ct));
        }
    }
}
