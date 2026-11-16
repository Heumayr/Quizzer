using Quizzer.DataModels.Transfer.Ki;
using Quizzer.DataModels.Transfer.Server;
using Quizzer.DataModels.Transfer.Sync;
using Quizzer.Server.Auth;
using Quizzer.Server.Infrastructure;
using Quizzer.Server.Media;
using Quizzer.Server.Statistics;
using Quizzer.Server.Sync;
using System.Reflection;

namespace Quizzer.Server
{
    /// <summary>
    /// Alle Endpunkte unter <c>/api/v1</c>. Anonym sind <b>nur</b> die in
    /// <see cref="AnonymousRoutes"/> - ein Deckungstest haelt diese Liste gegen die tatsaechlichen
    /// Routen (standards-sicherheit A01).
    /// </summary>
    public static class Endpoints
    {
        public static readonly IReadOnlySet<string> AnonymousRoutes = new HashSet<string>
        {
            "GET /api/v1/version",
            "POST /api/v1/auth/login",
            "POST /api/v1/auth/refresh",
            "GET /api/v1/client/latest",
            "GET /api/v1/client/package",
        };

        /// <summary>
        /// Was ein Admin ohne eingerichtetes TOTP darf (M36 A5) - sonst nichts. Ein Deckungstest
        /// haelt die Liste gegen die Regeln der tatsaechlichen Routen.
        /// </summary>
        public static readonly IReadOnlySet<string> TotpSetupRoutes = new HashSet<string>
        {
            "GET /api/v1/me",
            "POST /api/v1/me/totp/setup",
            "POST /api/v1/me/totp/confirm",
            "POST /api/v1/auth/logout",
        };

        private const string LoginFailed = "Benutzername oder Kennwort falsch.";

        private const string TotpRequired = "Bitte den Code aus der Authenticator-App eingeben (oder einen Wiederherstellungscode).";

        public static void Map(WebApplication app)
        {
            var api = app.MapGroup(ApiInfo.BasePath);

            api.MapGet("/version", () => new VersionResponse(
                    ApiInfo.ApiVersion,
                    SyncSnapshot.CurrentFormatVersion,
                    typeof(Endpoints).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "?"))
                .AllowAnonymous();

            Updates.ClientPackageEndpoints.Map(api);

            var auth = api.MapGroup("/auth");

            auth.MapPost("/login", async (LoginRequest request, AccountService accounts, CancellationToken ct) =>
                    await accounts.LoginAsync(request.UserName, request.Password, request.TotpCode, ct) switch
                    {
                        { Tokens: { } tokens } => Results.Ok(tokens),
                        { TotpRequired: true } => Results.Json(new ApiError(TotpRequired, ApiErrorCodes.TotpRequired), statusCode: StatusCodes.Status401Unauthorized),
                        _ => Results.Json(new ApiError(LoginFailed), statusCode: StatusCodes.Status401Unauthorized),
                    })
                .AllowAnonymous()
                .RequireRateLimiting(ServerSetup.AuthRateLimit);

            auth.MapPost("/refresh", async (RefreshRequest request, AccountService accounts, CancellationToken ct) =>
                    await accounts.RefreshAsync(request.RefreshToken, ct) is { } tokens
                        ? Results.Ok(tokens)
                        : Results.Json(new ApiError("Die Sitzung ist abgelaufen. Bitte neu anmelden."), statusCode: StatusCodes.Status401Unauthorized))
                .AllowAnonymous()
                .RequireRateLimiting(ServerSetup.AuthRateLimit);

            auth.MapPost("/logout", async (RefreshRequest request, HttpContext http, AccountService accounts, CancellationToken ct) =>
            {
                await accounts.LogoutAsync(CurrentAccount.From(http.User).AccountId, request.RefreshToken, ct);
                return Results.NoContent();
            }).RequireAuthorization(ServerSetup.TotpSetupPolicy);

            var me = api.MapGroup("/me");

            me.MapGet("", async (HttpContext http, AccountService accounts, CancellationToken ct) =>
                await accounts.GetInfoAsync(CurrentAccount.From(http.User).AccountId, ct) is { } info
                    ? Results.Ok(info)
                    : Results.NotFound())
                .RequireAuthorization(ServerSetup.TotpSetupPolicy);

            // Liefert neue Ausweise: das Aendern beendet alle Sitzungen, auch die eigene.
            me.MapPost("/password", async (ChangePasswordRequest request, HttpContext http, AccountService accounts, CancellationToken ct) =>
                    await accounts.ChangePasswordAsync(CurrentAccount.From(http.User).AccountId, request, ct) switch
                    {
                        { Tokens: { } tokens } => Results.Ok(tokens),
                        var failed => Results.BadRequest(new ApiError(failed.Error ?? "Das Kennwort ließ sich nicht ändern.")),
                    })
                .RequireRateLimiting(ServerSetup.AuthRateLimit);

            MapTwoFactor(me);

            me.MapGet("/statistics", async (HttpContext http, StatisticsService statistics, CancellationToken ct) =>
                Results.Ok(await statistics.ForAsync(CurrentAccount.From(http.User), ct)));

            var sync = api.MapGroup("/sync");

            sync.MapGet("/snapshot", async (HttpContext http, SnapshotService snapshots, CancellationToken ct) =>
                Results.Ok(await snapshots.CreateAsync(CurrentAccount.From(http.User), ct)));

            sync.MapPost("/push", async (SyncPushRequest request, HttpContext http, PushService push, CancellationToken ct) =>
            {
                try
                {
                    return Results.Ok(await push.PushAsync(CurrentAccount.From(http.User), request, ct));
                }
                catch (PushService.PushFailedException ex)
                {
                    return Results.Json(new ApiError(ex.Message), statusCode: StatusCodes.Status409Conflict);
                }
                catch (ArgumentException ex)
                {
                    return Results.BadRequest(new ApiError(ex.Message));
                }
            });

            var media = api.MapGroup("/media");

            media.MapGet("", async (HttpContext http, MediaService service, CancellationToken ct) =>
                Results.Ok(await service.ManifestAsync(CurrentAccount.From(http.User), ct)));

            // Unbekannt und verboten sehen gleich aus (404) - die Antwort verraet nicht, ob es eine
            // Datei gibt, die man nicht sehen darf.
            media.MapGet("/content", async (string path, HttpContext http, MediaService service, CancellationToken ct) =>
                await service.OpenPathAsync(CurrentAccount.From(http.User), path, ct) is { } full
                    ? Results.File(full, "application/octet-stream")
                    : Results.NotFound());

            media.MapPut("/content", async (string path, HttpRequest request, HttpContext http, MediaService service, CancellationToken ct) =>
            {
                // Erst das Recht, dann der Koerper: wer die Datei nicht verwenden darf, schickt
                // gar nicht erst 200 MB herein (Sicherheitspruefung 2026-10-21).
                if (!MediaPaths.TryNormalize(path, out var normalized))
                    return Results.BadRequest(new ApiError("Dieser Pfad oder Dateityp wird nicht angenommen."));

                if (!await service.UploadAllowedAsync(CurrentAccount.From(http.User), normalized, ct))
                    return Results.Json(new ApiError("Diese Datei verwendet keine Zeile, die dieses Konto ändern darf."), statusCode: StatusCodes.Status403Forbidden);

                // Kestrel nimmt von sich aus hoechstens 30 MB an; die eigene Grenze steht in
                // ServerOptions.MaxMediaBytes. Ohne das scheiterte jedes groessere Video - und mit
                // ihm jeder folgende Abgleich (Durchsicht 2026-10-21, Fund 3).
                if (request.HttpContext.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limit)
                    limit.MaxRequestBodySize = service.MaxBytes;

                return await service.UploadAsync(path, request.Body, ct) switch
                {
                    MediaService.UploadResult.Created => Results.StatusCode(StatusCodes.Status201Created),
                    MediaService.UploadResult.Unchanged => Results.NoContent(),
                    MediaService.UploadResult.TooLarge => Results.StatusCode(StatusCodes.Status413PayloadTooLarge),
                    MediaService.UploadResult.Exists => Results.Json(new ApiError("Unter diesem Namen liegt schon eine andere Datei."), statusCode: StatusCodes.Status409Conflict),
                    MediaService.UploadResult.Full => Results.Json(new ApiError("Die Medienablage des Servers ist voll."), statusCode: StatusCodes.Status507InsufficientStorage),
                    _ => Results.BadRequest(new ApiError("Dieser Pfad oder Dateityp wird nicht angenommen.")),
                };
            });

            // M31: KI-Entwürfe. Status und Entwurf für jedes angemeldete Konto (der Dienst prüft
            // die Freigabe), Einstellungen und Probe nur für den Admin.
            var ki = api.MapGroup("/ki");

            ki.MapGet("/status", async (HttpContext http, Ki.KiDienst dienst, CancellationToken ct) =>
                Results.Ok(await dienst.StatusAsync(CurrentAccount.From(http.User), ct)));

            ki.MapPost("/entwurf", async (KiEntwurfAnfrage anfrage, HttpContext http, Ki.KiDienst dienst, CancellationToken ct) =>
            {
                var ergebnis = await dienst.EntwurfAsync(CurrentAccount.From(http.User), anfrage, ct);
                return ergebnis.Antwort != null
                    ? Results.Ok(ergebnis.Antwort)
                    : Results.Json(new ApiError(ergebnis.Fehler ?? "Fehler"), statusCode: ergebnis.Status);
            });

            var kiAdmin = ki.MapGroup("/einstellungen").RequireAuthorization(ServerSetup.AdminPolicy);

            kiAdmin.MapGet("", async (Ki.KiSpeicher speicher, CancellationToken ct) => Results.Ok(await speicher.FuerAdminAsync(ct)));

            kiAdmin.MapPut("", async (KiEinstellungenDto einstellungen, Ki.KiSpeicher speicher, CancellationToken ct) =>
            {
                var fehler = await speicher.SpeichereAsync(einstellungen, ct);
                return fehler != null ? Results.BadRequest(new ApiError(fehler)) : Results.NoContent();
            });

            kiAdmin.MapPost("/probe", async (Ki.KiDienst dienst, CancellationToken ct) =>
            {
                var fehler = await dienst.ProbeAsync(ct);
                return fehler != null ? Results.Json(new ApiError(fehler), statusCode: StatusCodes.Status502BadGateway) : Results.NoContent();
            });

            var accounts = api.MapGroup("/accounts").RequireAuthorization(ServerSetup.AdminPolicy);

            accounts.MapGet("", async (AccountService service, CancellationToken ct) => Results.Ok(await service.ListAsync(ct)));

            accounts.MapPost("", async (CreateAccountRequest request, AccountService service, CancellationToken ct) =>
            {
                try
                {
                    var (error, created) = await service.CreateAsync(request, ct);
                    return error != null ? Results.BadRequest(new ApiError(error)) : Results.Ok(created);
                }
                catch (KeyNotFoundException)
                {
                    return Results.BadRequest(new ApiError("Diesen Mitspieler gibt es nicht."));
                }
            });

            accounts.MapPut("/{id:guid}", async (Guid id, UpdateAccountRequest request, HttpContext http, AccountService service, CancellationToken ct) =>
            {
                try
                {
                    var error = await service.UpdateAsync(id, CurrentAccount.From(http.User).AccountId, request, ct);
                    return error != null ? Results.BadRequest(new ApiError(error)) : Results.NoContent();
                }
                catch (KeyNotFoundException)
                {
                    return Results.NotFound();
                }
            });

            // Verlorenes Handy: der Admin setzt ein fremdes Konto zurueck, dessen Sitzungen enden.
            accounts.MapDelete("/{id:guid}/totp", async (Guid id, HttpContext http, TwoFactorService service, CancellationToken ct) =>
            {
                try
                {
                    var error = await service.ResetAsync(id, CurrentAccount.From(http.User).AccountId, ct);
                    return error != null ? Results.BadRequest(new ApiError(error)) : Results.NoContent();
                }
                catch (KeyNotFoundException)
                {
                    return Results.NotFound();
                }
            });
        }

        /// <summary>
        /// Einrichten und Bestaetigen sind auch einem Admin ohne TOTP erlaubt - sonst kaeme er nie
        /// heraus. Abschalten nur fuer Mitglieder; fuer Admins ist es Pflicht.
        /// </summary>
        private static void MapTwoFactor(RouteGroupBuilder me)
        {
            var totp = me.MapGroup("/totp").RequireRateLimiting(ServerSetup.AuthRateLimit);

            totp.MapPost("/setup", async (HttpContext http, TwoFactorService service, CancellationToken ct) =>
                    await service.BeginSetupAsync(CurrentAccount.From(http.User).AccountId, ct) switch
                    {
                        { Setup: { } setup } => Results.Ok(setup),
                        var failed => Results.Json(new ApiError(failed.Error ?? TwoFactorService.AlreadyEnabled), statusCode: StatusCodes.Status409Conflict),
                    })
                .RequireAuthorization(ServerSetup.TotpSetupPolicy);

            totp.MapPost("/confirm", async (TotpConfirmRequest request, HttpContext http, AccountService accounts, CancellationToken ct) =>
                    await accounts.ConfirmTotpAsync(CurrentAccount.From(http.User).AccountId, request.Code, ct) switch
                    {
                        { Result: { } result } => Results.Ok(result),
                        var failed => Results.BadRequest(new ApiError(failed.Error ?? "Die Einrichtung ist gescheitert.")),
                    })
                .RequireAuthorization(ServerSetup.TotpSetupPolicy);

            totp.MapPost("/disable", async (TotpDisableRequest request, HttpContext http, TwoFactorService service, CancellationToken ct) =>
            {
                var current = CurrentAccount.From(http.User);
                if (current.IsAdmin)
                    return Results.Json(new ApiError(TwoFactorService.AdminMustKeep), statusCode: StatusCodes.Status403Forbidden);

                var error = await service.DisableAsync(current.AccountId, request, ct);
                return error != null ? Results.BadRequest(new ApiError(error)) : Results.NoContent();
            });
        }
    }
}
