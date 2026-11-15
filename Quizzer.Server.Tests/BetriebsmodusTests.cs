using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace Quizzer.Server.Tests
{
    /// <summary>
    /// Haertung vor der oeffentlichen Erreichbarkeit (M36 A5, standards-sicherheit A02/A10): ein
    /// unerwarteter Fehler im Betriebsmodus verraet nach aussen nichts vom Innenleben.
    /// </summary>
    [TestClass]
    public sealed class BetriebsmodusTests
    {
        private const string Innenleben = "SELECT * FROM auth.Account -- Probe-Innenleben";

        [TestMethod]
        public async Task AnUnexpectedErrorShowsNoInnards()
        {
            await using var basis = ServerTestHost.CreateFactory();
            await using var factory = basis.WithWebHostBuilder(web =>
            {
                web.UseEnvironment("Production");
                web.ConfigureServices(s => s.AddSingleton<IStartupFilter>(new WirftFilter()));
            });
            var admin = ServerTestHost.Authorized(factory,
                await ServerTestHost.LoginAsync(factory.CreateClient(), ServerTestHost.AdminUser, ServerTestHost.AdminPassword));

            var antwort = await admin.GetAsync("/api/v1/probe-wirft");
            var rumpf = await antwort.Content.ReadAsStringAsync();

            Assert.AreEqual(HttpStatusCode.InternalServerError, antwort.StatusCode);
            Assert.IsFalse(rumpf.Contains("Probe-Innenleben"), rumpf);
            Assert.IsFalse(rumpf.Contains("   at "), rumpf);
            Assert.IsFalse(rumpf.Contains(nameof(InvalidOperationException)), rumpf);
        }

        /// <summary>Haengt hinter die Pipeline des Servers einen Pfad, der wirft - hinter Ausweis und Riegel.</summary>
        private sealed class WirftFilter : IStartupFilter
        {
            public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
            {
                next(app);
                app.Map("/api/v1/probe-wirft", zweig => zweig.Run(_ => throw new InvalidOperationException(Innenleben)));
            };
        }
    }
}
