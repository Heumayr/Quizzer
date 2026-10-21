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
    /// Alle Endpunkte unter <c>/api/v1</c>. Anonym sind <b>nur</b> die drei in
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
        };

        private const string LoginFailed = "Benutzername oder Kennwort falsch.";

        public static void Map(WebApplication app)
        {
            var api = app.MapGroup(ApiInfo.BasePath);

            api.MapGet("/version", () => new VersionResponse(
                    ApiInfo.ApiVersion,
                    SyncSnapshot.CurrentFormatVersion,
                    typeof(Endpoints).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "?"))
                .AllowAnonymous();

            var auth = api.MapGroup("/auth");

            auth.MapPost("/login", async (LoginRequest request, AccountService accounts, CancellationToken ct) =>
                    await accounts.LoginAsync(request.UserName, request.Password, ct) is { } tokens
                        ? Results.Ok(tokens)
                        : Results.Json(new ApiError(LoginFailed), statusCode: StatusCodes.Status401Unauthorized))
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
            });

            var me = api.MapGroup("/me");

            me.MapGet("", async (HttpContext http, AccountService accounts, CancellationToken ct) =>
                await accounts.GetInfoAsync(CurrentAccount.From(http.User).AccountId, ct) is { } info
                    ? Results.Ok(info)
                    : Results.NotFound());

            me.MapPost("/password", async (ChangePasswordRequest request, HttpContext http, AccountService accounts, CancellationToken ct) =>
                    await accounts.ChangePasswordAsync(CurrentAccount.From(http.User).AccountId, request, ct) is { } error
                        ? Results.BadRequest(new ApiError(error))
                        : Results.NoContent())
                .RequireRateLimiting(ServerSetup.AuthRateLimit);

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

            media.MapPut("/content", async (string path, HttpRequest request, MediaService service, CancellationToken ct) =>
            {
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
                    _ => Results.BadRequest(new ApiError("Dieser Pfad oder Dateityp wird nicht angenommen.")),
                };
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
        }
    }
}
