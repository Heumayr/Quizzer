using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LocalBuzzer.Service.Base
{
    /// <summary>
    /// Die drei Wege, auf denen sich ein Telefon selbst zum Spiel meldet (M10):
    /// <list type="number">
    /// <item><c>GET /join/candidates</c> - die Namen, unter denen man sich melden kann</item>
    /// <item><c>POST /join/request</c> - „ich bin X" oder „ich bin Gast Y"</item>
    /// <item><c>GET /join/status/{id}</c> - wartet der Gamemaster noch, oder darf ich hinein?</item>
    /// </list>
    /// Es geht nur der Name ueber das WLAN, nie ein Kennwort.
    /// </summary>
    public static class JoinEndpoints
    {
        /// <summary>
        /// Ein Name zur Auswahl. <b>Keine Spieler-Id</b> (Sicherheitsprüfung 2026-10-21): die
        /// Kennung ist abgeleitet und taugt nicht als Zugang zum Hub.
        /// </summary>
        public sealed record CandidateDto(string CandidateId, string Name);

        public sealed record JoinRequestDto(string? CandidateId, string? GuestName);

        public sealed record JoinCreatedDto(Guid RequestId);

        /// <summary>Bei „accepted" der Zugangsschlüssel - nur der Anfragende kennt die RequestId.</summary>
        public sealed record JoinStatusDto(string Status, string? AccessKey);

        public static void Map(IEndpointRouteBuilder app, JoinRequestRegistry registry, Func<IReadOnlyList<JoinCandidate>> candidates, BuzzerAccessKeys keys)
        {
            app.MapGet("/join/candidates", () =>
                candidates().Select(c => new CandidateDto(keys.KandidatFuer(c.PlayerId), c.Name)).OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase).ToList());

            app.MapPost("/join/request", (JoinRequestDto dto, HttpContext http) =>
            {
                var remote = http.Connection.RemoteIpAddress?.ToString() ?? "unbekannt";
                var liste = candidates();

                Guid? playerId = null;

                if (!string.IsNullOrWhiteSpace(dto.CandidateId))
                {
                    var gewaehlt = keys.FindeKandidat(dto.CandidateId, liste, c => c.PlayerId);

                    if (gewaehlt == null)
                        return Results.BadRequest();

                    playerId = gewaehlt.PlayerId;
                }

                var (result, request) = registry.Create(playerId, dto.GuestName, liste, remote);

                return result switch
                {
                    JoinRequestRegistry.CreateResult.Created => Results.Ok(new JoinCreatedDto(request!.Id)),
                    JoinRequestRegistry.CreateResult.TooMany => Results.StatusCode(StatusCodes.Status429TooManyRequests),
                    _ => Results.BadRequest(),
                };
            });

            app.MapGet("/join/status/{id:guid}", (Guid id) =>
            {
                var request = registry.Get(id);
                if (request == null)
                    return Results.NotFound();

                return Results.Ok(new JoinStatusDto(
                    request.Status.ToString().ToLowerInvariant(),
                    request.Status == JoinStatus.Accepted && request.AssignedPlayerId is Guid zugewiesen
                        ? keys.SchluesselFuer(zugewiesen)
                        : null));
            });
        }
    }
}
