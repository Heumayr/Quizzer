using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Quizzer.DataModels;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Auth;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Transfer.Server;
using Quizzer.Logic.Context;
using Quizzer.Server.Infrastructure;

namespace Quizzer.Server.Auth
{
    /// <summary>
    /// Anmelden, Erneuern, Abmelden und die Kontenverwaltung. Jede Methode oeffnet ihren eigenen
    /// <see cref="DataContext"/> - der Server haelt keinen Zustand zwischen zwei Anfragen.
    /// </summary>
    public sealed class AccountService(TokenService tokens, IOptions<ServerOptions> options, TimeProvider time, ILogger<AccountService> log)
    {
        private readonly ServerOptions settings = options.Value;

        /// <summary>
        /// Prueft Name und Kennwort. Jeder Fehlausgang - unbekannter Name, falsches Kennwort,
        /// gesperrt, deaktiviert - liefert <c>null</c>, damit die Antwort nichts verraet
        /// (standards-sicherheit A07: eine Meldung fuer alle Fehlausgaenge).
        /// </summary>
        public async Task<TokenResponse?> LoginAsync(string? userName, string? password, CancellationToken ct)
        {
            var now = time.GetUtcNow();
            await using var context = new DataContext();

            var normalized = Account.Normalize(userName);
            var account = await context.Accounts.Include(a => a.Player)
                .FirstOrDefaultAsync(a => a.NormalizedUserName == normalized, ct);

            if (account == null)
            {
                // Gleich viel Arbeit wie bei einem echten Konto, damit die Laufzeit nicht verraet,
                // ob es den Namen gibt.
                Session.HashPassword(password ?? "x");
                return null;
            }

            if (account.IsDisabled || (account.LockedUntil is { } until && until > now))
            {
                log.LogWarning("Anmeldung abgewiesen (gesperrt oder deaktiviert): Konto {AccountId}", account.Id);
                return null;
            }

            if (!VerifyPassword(account.PasswordHash, password))
            {
                account.FailedLoginCount++;

                if (account.FailedLoginCount >= settings.LockoutThreshold)
                {
                    account.LockedUntil = now + settings.LockoutDuration;
                    account.FailedLoginCount = 0;
                    log.LogWarning("Konto {AccountId} nach zu vielen Fehlversuchen kurz gesperrt", account.Id);
                }

                await context.SaveChangesAsync(ct);
                log.LogInformation("Anmeldung fehlgeschlagen: Konto {AccountId}", account.Id);
                return null;
            }

            account.FailedLoginCount = 0;
            account.LockedUntil = null;
            account.LastLoginAt = now;

            var response = await IssueAsync(context, account, Guid.NewGuid(), now, ct);
            await context.SaveChangesAsync(ct);

            log.LogInformation("Anmeldung erfolgreich: Konto {AccountId}", account.Id);
            return response;
        }

        /// <summary>
        /// Tauscht einen Erneuerungsschluessel gegen ein neues Paar. Ein bereits ersetzter oder
        /// widerrufener Schluessel widerruft die ganze Familie - er ist abgegriffen worden.
        /// </summary>
        public async Task<TokenResponse?> RefreshAsync(string? refreshToken, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
                return null;

            var now = time.GetUtcNow();
            var hash = TokenService.HashRefreshToken(refreshToken);

            await using var context = new DataContext();
            var stored = await context.RefreshTokens
                .Include(t => t.Account).ThenInclude(a => a.Player)
                .FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

            if (stored == null)
                return null;

            if (stored.RevokedAt != null || stored.ReplacedById != null)
            {
                await RevokeFamilyAsync(context, stored.FamilyId, now, ct);
                await context.SaveChangesAsync(ct);
                log.LogWarning("Wiederverwendeter Erneuerungsschlüssel - Familie widerrufen: Konto {AccountId}", stored.AccountId);
                return null;
            }

            if (stored.ExpiresAt <= now || stored.Account.IsDisabled)
                return null;

            // Verbrauchen in EINEM Schritt: nur wer die Zeile noch unverbraucht vorfindet, bekommt
            // einen Nachfolger. Pruefen und Schreiben lagen bis zur Sicherheitspruefung 2026-10-21
            // auseinander - zwei gleichzeitige Anfragen mit demselben (gestohlenen) Schluessel
            // bekamen beide einen, und der Diebstahl fiel nie auf.
            var verbraucht = await context.RefreshTokens
                .Where(t => t.Id == stored.Id && t.RevokedAt == null && t.ReplacedById == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);

            if (verbraucht == 0)
            {
                await RevokeFamilyAsync(context, stored.FamilyId, now, ct);
                await context.SaveChangesAsync(ct);
                log.LogWarning("Gleichzeitig wiederverwendeter Erneuerungsschlüssel - Familie widerrufen: Konto {AccountId}", stored.AccountId);
                return null;
            }

            var response = await IssueAsync(context, stored.Account, stored.FamilyId, now, ct);
            var successor = context.RefreshTokens.Local.Single(t => t.TokenHash == TokenService.HashRefreshToken(response.RefreshToken));

            stored.RevokedAt = now;
            stored.ReplacedById = successor.Id;

            await context.SaveChangesAsync(ct);
            return response;
        }

        /// <summary>Widerruft die Familie des Schluessels - die Abmeldung gilt auch serverseitig.</summary>
        public async Task LogoutAsync(Guid accountId, string? refreshToken, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
                return;

            var hash = TokenService.HashRefreshToken(refreshToken);

            await using var context = new DataContext();
            var stored = await context.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash && t.AccountId == accountId, ct);

            if (stored == null)
                return;

            await RevokeFamilyAsync(context, stored.FamilyId, time.GetUtcNow(), ct);
            await context.SaveChangesAsync(ct);
        }

        public async Task<AccountInfo?> GetInfoAsync(Guid accountId, CancellationToken ct)
        {
            await using var context = new DataContext();
            var account = await context.Accounts.AsNoTracking().Include(a => a.Player)
                .FirstOrDefaultAsync(a => a.Id == accountId, ct);

            return account == null ? null : ToInfo(account);
        }

        /// <summary>
        /// Ob das Konto noch gilt - und noch die Rolle hat, die im Ausweis steht. Wird bei jedem
        /// Ausweis geprueft: ein herabgestufter Admin verliert die Rechte sofort, nicht erst, wenn
        /// sein Ausweis ablaeuft (Durchsicht 2026-10-21, Fund 10).
        /// </summary>
        public async Task<bool> IsActiveAsync(Guid accountId, AccountRole role, CancellationToken ct)
        {
            await using var context = new DataContext();
            return await context.Accounts.AsNoTracking().AnyAsync(a => a.Id == accountId && !a.IsDisabled && a.Role == role, ct);
        }

        public async Task<string?> ChangePasswordAsync(Guid accountId, ChangePasswordRequest request, CancellationToken ct)
        {
            await using var context = new DataContext();
            var account = await context.Accounts.FirstOrDefaultAsync(a => a.Id == accountId, ct);

            if (account == null || !VerifyPassword(account.PasswordHash, request.CurrentPassword))
                return "Das bisherige Kennwort stimmt nicht.";

            var problem = CheckPassword(request.NewPassword, account.UserName);
            if (problem != null)
                return problem;

            account.PasswordHash = Session.HashPassword(request.NewPassword);
            await RevokeAllAsync(context, account.Id, time.GetUtcNow(), ct);
            await context.SaveChangesAsync(ct);
            return null;
        }

        public async Task<List<AccountListItem>> ListAsync(CancellationToken ct)
        {
            await using var context = new DataContext();
            return await context.Accounts.AsNoTracking().Include(a => a.Player)
                .OrderBy(a => a.UserName)
                .Select(a => new AccountListItem(a.Id, a.UserName, a.Role, a.PlayerId,
                    a.Player.DisplayName != "" ? a.Player.DisplayName : a.Player.Designation,
                    a.IsDisabled, a.CreatedAt, a.LastLoginAt))
                .ToListAsync(ct);
        }

        /// <summary>Legt ein Konto an. Liefert die Fehlermeldung oder das neue Konto.</summary>
        public async Task<(string? Error, AccountListItem? Created)> CreateAsync(CreateAccountRequest request, CancellationToken ct)
        {
            var userName = (request.UserName ?? string.Empty).Trim();

            if (userName.Length is < 3 or > 64)
                return ("Der Benutzername braucht 3 bis 64 Zeichen.", null);

            var problem = CheckPassword(request.Password, userName);
            if (problem != null)
                return (problem, null);

            await using var context = new DataContext();
            var normalized = Account.Normalize(userName);

            if (await context.Accounts.AnyAsync(a => a.NormalizedUserName == normalized, ct))
                return ("Diesen Benutzernamen gibt es schon.", null);

            Player player;
            if (request.PlayerId is { } playerId)
            {
                player = await context.Players.FirstOrDefaultAsync(p => p.Id == playerId, ct)
                    ?? throw new KeyNotFoundException();

                if (await context.Accounts.AnyAsync(a => a.PlayerId == playerId, ct))
                    return ("Dieser Mitspieler hat schon ein Konto.", null);
            }
            else
            {
                var name = (request.NewPlayerName ?? userName).Trim();
                player = new Player { Id = Guid.NewGuid(), Designation = name, DisplayName = name, IsModerator = true };
                context.Players.Add(player);
            }

            var account = new Account
            {
                Id = Guid.NewGuid(),
                UserName = userName,
                NormalizedUserName = normalized,
                PasswordHash = Session.HashPassword(request.Password),
                Role = request.Role,
                PlayerId = player.Id,
                CreatedAt = time.GetUtcNow(),
            };

            context.Accounts.Add(account);
            await context.SaveChangesAsync(ct);

            log.LogInformation("Konto {AccountId} angelegt ({Role})", account.Id, account.Role);
            return (null, new AccountListItem(account.Id, account.UserName, account.Role, player.Id,
                player.CalculatedDisplayName, false, account.CreatedAt, null));
        }

        public async Task<string?> UpdateAsync(Guid accountId, Guid actingAccountId, UpdateAccountRequest request, CancellationToken ct)
        {
            await using var context = new DataContext();
            var account = await context.Accounts.FirstOrDefaultAsync(a => a.Id == accountId, ct)
                ?? throw new KeyNotFoundException();

            if (accountId == actingAccountId && (request.IsDisabled == true || request.Role == AccountRole.Member))
                return "Das eigene Konto kann man weder sperren noch herabstufen.";

            if (request.NewPassword != null)
            {
                var problem = CheckPassword(request.NewPassword, account.UserName);
                if (problem != null)
                    return problem;

                account.PasswordHash = Session.HashPassword(request.NewPassword);
            }

            if (request.Role is { } role)
                account.Role = role;

            if (request.IsDisabled is { } disabled)
                account.IsDisabled = disabled;

            // Jede Aenderung an Kennwort, Rolle oder Sperre beendet die laufenden Sitzungen:
            // ein Ausweis mit der alten Rolle soll nicht weiter gelten.
            await RevokeAllAsync(context, account.Id, time.GetUtcNow(), ct);
            await context.SaveChangesAsync(ct);
            return null;
        }

        /// <summary>Legt das erste Admin-Konto an, solange es noch gar keines gibt.</summary>
        public async Task BootstrapAsync(CancellationToken ct)
        {
            var bootstrap = settings.Bootstrap;

            if (string.IsNullOrWhiteSpace(bootstrap.AdminUser) || string.IsNullOrWhiteSpace(bootstrap.AdminPassword))
                return;

            await using (var context = new DataContext())
            {
                if (await context.Accounts.AnyAsync(ct))
                    return;
            }

            var (error, created) = await CreateAsync(
                new CreateAccountRequest(bootstrap.AdminUser, bootstrap.AdminPassword, AccountRole.Admin, null, bootstrap.AdminUser), ct);

            if (error != null)
                throw new InvalidOperationException($"Das erste Admin-Konto liess sich nicht anlegen: {error}");

            log.LogInformation("Erstes Admin-Konto angelegt: {UserName}", created!.UserName);
        }

        /// <summary>NIST 800-63B: Laenge vor Komplexitaet, keine Zwangszeichen.</summary>
        public string? CheckPassword(string? password, string userName)
        {
            if (string.IsNullOrEmpty(password) || password.Length < settings.PasswordMinLength)
                return $"Das Kennwort braucht mindestens {settings.PasswordMinLength} Zeichen.";

            if (password.Length > 256)
                return "Das Kennwort darf höchstens 256 Zeichen lang sein.";

            if (string.Equals(password.Trim(), userName.Trim(), StringComparison.OrdinalIgnoreCase))
                return "Das Kennwort darf nicht der Benutzername sein.";

            if (CommonPasswords.Contains(password))
                return "Dieses Kennwort steht auf den Listen bekannter Kennwörter.";

            return null;
        }

        /// <summary>Eine kleine Auswahl der haeufigsten Kennwoerter ab 12 Zeichen.</summary>
        private static readonly HashSet<string> CommonPasswords = new(StringComparer.OrdinalIgnoreCase)
        {
            "123456789012", "1234567890123", "12345678901234", "qwertyuiopas", "passwort1234",
            "password1234", "password12345", "passwortpasswort", "passwordpassword", "aaaaaaaaaaaa",
            "000000000000", "111111111111", "qwertzuiopü", "qwertzuiopasd", "iloveyou1234",
            "hallohallo12", "hallo1234567", "quizzerquizzer", "quizzer12345", "admin1234567",
            "administrator", "123456123456", "abcdefghijkl", "abc123abc123",
        };

        private static bool VerifyPassword(string hash, string? password)
        {
            // Session.VerifyPassword laesst einen leeren Hash durch (lokale Spielleiter ohne
            // Kennwort). Ein Konto ohne Hash ist dagegen kaputt und darf nie hinein.
            if (string.IsNullOrEmpty(hash))
                return false;

            return Session.VerifyPassword(new Player { PasswordHash = hash }, password);
        }

        private async Task<TokenResponse> IssueAsync(DataContext context, Account account, Guid familyId, DateTimeOffset now, CancellationToken ct)
        {
            var playerName = account.Player?.CalculatedDisplayName
                ?? (await context.Players.AsNoTracking().FirstAsync(p => p.Id == account.PlayerId, ct)).CalculatedDisplayName;

            var (access, accessExpires) = tokens.CreateAccessToken(account, playerName, now);
            var refresh = TokenService.NewRefreshToken();
            var refreshExpires = now + tokens.Options.RefreshTokenLifetime;

            context.RefreshTokens.Add(new RefreshToken
            {
                Id = Guid.NewGuid(),
                AccountId = account.Id,
                FamilyId = familyId,
                TokenHash = TokenService.HashRefreshToken(refresh),
                CreatedAt = now,
                ExpiresAt = refreshExpires,
            });

            return new TokenResponse(access, accessExpires, refresh, refreshExpires,
                new AccountInfo(account.Id, account.UserName, account.Role, account.PlayerId, playerName));
        }

        private static async Task RevokeFamilyAsync(DataContext context, Guid familyId, DateTimeOffset now, CancellationToken ct)
        {
            var family = await context.RefreshTokens.Where(t => t.FamilyId == familyId && t.RevokedAt == null).ToListAsync(ct);
            family.ForEach(t => t.RevokedAt = now);
        }

        private static async Task RevokeAllAsync(DataContext context, Guid accountId, DateTimeOffset now, CancellationToken ct)
        {
            var all = await context.RefreshTokens.Where(t => t.AccountId == accountId && t.RevokedAt == null).ToListAsync(ct);
            all.ForEach(t => t.RevokedAt = now);
        }

        private static AccountInfo ToInfo(Account account)
            => new(account.Id, account.UserName, account.Role, account.PlayerId, account.Player.CalculatedDisplayName);
    }
}
