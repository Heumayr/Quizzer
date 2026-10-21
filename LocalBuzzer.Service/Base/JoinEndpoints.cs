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
        public sealed record CandidateDto(Guid PlayerId, string Name);

        public sealed record JoinRequestDto(Guid? PlayerId, string? GuestName);

        public sealed record JoinCreatedDto(Guid RequestId);

        public sealed record JoinStatusDto(string Status, Guid? PlayerId);

        public static void Map(IEndpointRouteBuilder app, JoinRequestRegistry registry, Func<IReadOnlyList<JoinCandidate>> candidates)
        {
            app.MapGet("/join/candidates", () =>
                candidates().Select(c => new CandidateDto(c.PlayerId, c.Name)).OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase).ToList());

            app.MapPost("/join/request", (JoinRequestDto dto, HttpContext http) =>
            {
                var remote = http.Connection.RemoteIpAddress?.ToString() ?? "unbekannt";
                var (result, request) = registry.Create(dto.PlayerId, dto.GuestName, candidates(), remote);

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
                    request.Status == JoinStatus.Accepted ? request.AssignedPlayerId : null));
            });
        }
    }
}
