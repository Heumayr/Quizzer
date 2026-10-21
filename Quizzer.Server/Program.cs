using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Quizzer.Server;
using Quizzer.Server.Auth;
using Quizzer.Server.Infrastructure;
using Quizzer.Server.Media;
using Quizzer.Server.Statistics;
using Quizzer.Server.Sync;
using System.Threading.RateLimiting;

// Quizzer.Server - die REST-API vor der Spieldatenbank (M9).
//
//   dotnet Quizzer.Server.dll migrate   Schema auf Stand bringen, Betriebsanmeldung einrichten
//   dotnet Quizzer.Server.dll           Server starten (verweigert den Start bei offenen Migrationen)

var builder = WebApplication.CreateBuilder(args.Where(a => a != "migrate").ToArray());

if (args.Contains("migrate"))
{
    var options = builder.Configuration.GetSection(ServerOptions.Section).Get<ServerOptions>() ?? new ServerOptions();
    using var loggerFactory = LoggerFactory.Create(l => l.AddSimpleConsole());
    var log = loggerFactory.CreateLogger("migrate");

    DatabaseSetup.UseConnection(builder.Configuration.GetConnectionString("QuizzerMigrator")
        ?? builder.Configuration.GetConnectionString("Quizzer"));

    await DatabaseSetup.MigrateAsync(options, log, CancellationToken.None);
    return 0;
}

ServerSetup.ConfigureServices(builder);

var app = builder.Build();

DatabaseSetup.UseConnection(app.Configuration.GetConnectionString("Quizzer"));
await DatabaseSetup.EnsureUpToDateAsync(CancellationToken.None);
await app.Services.GetRequiredService<AccountService>().BootstrapAsync(CancellationToken.None);

ServerSetup.ConfigurePipeline(app);
Endpoints.Map(app);

await app.RunAsync();
return 0;

/// <summary>Fuer WebApplicationFactory in den Tests.</summary>
public partial class Program;
