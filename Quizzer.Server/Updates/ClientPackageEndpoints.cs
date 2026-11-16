using Quizzer.DataModels.Transfer.Server;
using Quizzer.Server.Infrastructure;

namespace Quizzer.Server.Updates
{
    /// <summary>
    /// Aktualisierung des WPF (M36 A7). Ohne Anmeldung: gerade ein vom Versionsriegel abgewiesenes
    /// Programm muss hierher kommen, und das Paket enthaelt nichts Geheimes. Die Anmeldebremse
    /// begrenzt je Adresse, wie oft 100 MB uebers Netz gehen.
    /// </summary>
    public static class ClientPackageEndpoints
    {
        public const string LatestPath = ApiInfo.BasePath + "/client/latest";

        public const string PackagePath = ApiInfo.BasePath + "/client/package";

        internal const string NoPackage = "Auf dem Server liegt kein Quizzer-Paket.";

        public static void Map(RouteGroupBuilder api)
        {
            var client = api.MapGroup("/client")
                .AllowAnonymous()
                .RequireRateLimiting(ServerSetup.AuthRateLimit);

            client.MapGet("/latest", async (ClientPackageStore store, CancellationToken ct) =>
                await store.InfoAsync(ct) is { } info
                    ? Results.Ok(info)
                    : Results.NotFound(new ApiError(NoPackage)));

            client.MapGet("/package", (ClientPackageStore store) =>
                store.Find() is { } package
                    ? Results.File(package.FullPath, "application/zip", ClientPackageStore.FileName(package.Version))
                    : Results.NotFound(new ApiError(NoPackage)));
        }
    }
}
