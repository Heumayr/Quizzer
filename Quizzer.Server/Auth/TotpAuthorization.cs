using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Transfer.Server;

namespace Quizzer.Server.Auth
{
    /// <summary>
    /// Pflicht fuer Admins (F-M36-02): ein Admin-Ausweis ohne TOTP-Anspruch erfuellt diese
    /// Anforderung nicht. Sie steht in der Rueckfallregel und in der Admin-Regel; frei davon sind
    /// nur die Endpunkte der Einrichtung (<see cref="Endpoints.TotpSetupRoutes"/>).
    /// </summary>
    public sealed class TotpForAdminRequirement : AuthorizationHandler<TotpForAdminRequirement>, IAuthorizationRequirement
    {
        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, TotpForAdminRequirement requirement)
        {
            if (!context.User.IsInRole(nameof(AccountRole.Admin)) || CurrentAccount.HasTotp(context.User))
                context.Succeed(requirement);

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Schreibt bei fehlender Einrichtung eine Antwort, an der das WPF den Grund erkennt; jede
    /// andere Ablehnung bleibt beim Standard.
    /// </summary>
    public sealed class TotpForbiddenResultHandler : IAuthorizationMiddlewareResultHandler
    {
        public const string Message = "Für Admin-Konten ist die Zwei-Faktor-Anmeldung Pflicht. Bitte zuerst einrichten.";

        private readonly AuthorizationMiddlewareResultHandler standard = new();

        public async Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
        {
            if (authorizeResult.Forbidden
                && authorizeResult.AuthorizationFailure?.FailedRequirements.OfType<TotpForAdminRequirement>().Any() == true)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.Headers.CacheControl = "no-store";
                await context.Response.WriteAsJsonAsync(new ApiError(Message, ApiErrorCodes.TotpSetupRequired));
                return;
            }

            await standard.HandleAsync(next, context, policy, authorizeResult);
        }
    }
}
