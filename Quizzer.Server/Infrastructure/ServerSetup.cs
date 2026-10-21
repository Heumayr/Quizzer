using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Quizzer.Server.Auth;
using Quizzer.Server.Media;
using Quizzer.Server.Statistics;
using Quizzer.Server.Sync;
using System.Threading.RateLimiting;

namespace Quizzer.Server.Infrastructure
{
    public static class ServerSetup
    {
        public const string AuthRateLimit = "auth";

        public const string AdminPolicy = "admin";

        public static void ConfigureServices(WebApplicationBuilder builder)
        {
            var services = builder.Services;

            services.Configure<ServerOptions>(builder.Configuration.GetSection(ServerOptions.Section));
            services.AddSingleton(TimeProvider.System);
            services.AddSingleton<TokenService>();
            services.AddSingleton<AccountService>();
            services.AddSingleton<SnapshotService>();
            services.AddSingleton<PushService>();
            services.AddSingleton<MediaService>();
            services.AddSingleton<StatisticsService>();

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

            // Die Ausweisparameter haengen am Signierschluessel aus den Optionen - deshalb hier
            // und nicht im AddJwtBearer-Rumpf, der vor dem Aufbau der Dienste laeuft.
            services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
                .Configure<TokenService, AccountService>((jwt, tokens, accounts) =>
                {
                    jwt.MapInboundClaims = false;
                    jwt.TokenValidationParameters = tokens.ValidationParameters();
                    jwt.Events = new JwtBearerEvents
                    {
                        // Ein gesperrtes Konto verliert seinen Zugang sofort, nicht erst, wenn der
                        // Ausweis ablaeuft.
                        OnTokenValidated = async context =>
                        {
                            var account = CurrentAccount.From(context.Principal!);
                            if (!await accounts.IsActiveAsync(account.AccountId, account.Role, context.HttpContext.RequestAborted))
                                context.Fail("Konto gesperrt oder Rolle geändert.");
                        },
                    };
                });

            services.AddAuthorizationBuilder()
                // Deny by default: jeder Endpunkt ohne ausdrueckliches AllowAnonymous braucht einen
                // gueltigen Ausweis (standards-sicherheit A01).
                .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
                .AddPolicy(AdminPolicy, p => p.RequireAuthenticatedUser().RequireRole(nameof(DataModels.Enumerations.AccountRole.Admin)));

            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.AddPolicy(AuthRateLimit, http => RateLimitPartition.GetFixedWindowLimiter(
                    http.Connection.RemoteIpAddress?.ToString() ?? "unbekannt",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = builder.Configuration.GetValue("Quizzer:AuthRequestsPerMinute", 10),
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    }));
            });

            services.AddProblemDetails();
            services.ConfigureHttpJsonOptions(o =>
            {
                o.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
            });
            services.AddHsts(o => o.MaxAge = TimeSpan.FromDays(180));
        }

        public static void ConfigurePipeline(WebApplication app)
        {
            if (!app.Environment.IsDevelopment())
                app.UseHsts();

            app.UseExceptionHandler();
            app.UseRateLimiter();
            app.UseAuthentication();
            app.UseAuthorization();

            app.Use(async (context, next) =>
            {
                // Antworten mit Ausweisen oder Daten gehoeren in keinen Zwischenspeicher.
                context.Response.Headers.CacheControl = "no-store";
                context.Response.Headers["X-Content-Type-Options"] = "nosniff";
                await next();
            });
        }
    }
}
