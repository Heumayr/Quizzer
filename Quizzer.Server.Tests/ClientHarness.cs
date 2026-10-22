using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Quizzer.Client;
using Quizzer.Logic.Context;
using Quizzer.Logic.Sync;

namespace Quizzer.Server.Tests
{
    /// <summary>
    /// Ein WPF-Client ohne WPF: eigene lokale Datenbank, eigener Medienordner, derselbe
    /// <see cref="ClientSyncService"/> wie im Programm. Zwei davon spielen „PC" und „Laptop".
    /// </summary>
    internal sealed class ClientHarness : IAsyncDisposable
    {
        private ClientHarness(string connectionString, string mediaRoot, QuizzerApiClient api, MemoryTokenStore tokens)
        {
            ConnectionString = connectionString;
            MediaRoot = mediaRoot;
            Api = api;
            Tokens = tokens;
            Sync = new ClientSyncService(api, () => new SyncStore(connectionString), State, mediaRoot);
        }

        /// <summary>Was ein Neustart des Programms überlebt: Erneuerungsschlüssel und Abgleichszustand.</summary>
        public MemoryTokenStore Tokens { get; }

        public MemorySyncStateStore State { get; } = new();

        /// <summary>
        /// Wie <c>ServerSession.TryReconnectAsync</c>: ein neuer API-Client mit dem gespeicherten
        /// Erneuerungsschlüssel, ein neuer Abgleich auf demselben Zustand.
        /// </summary>
        public async Task<(bool Fortgesetzt, ClientSyncService Sync)> ReconnectAsync(WebApplicationFactory<Program> factory)
        {
            var api = new QuizzerApiClient(new Uri("https://localhost/api/v1/"), Tokens, handler: factory.Server.CreateHandler());
            var fortgesetzt = await api.TryResumeAsync();
            return (fortgesetzt, new ClientSyncService(api, () => new SyncStore(ConnectionString), State, MediaRoot));
        }

        public string ConnectionString { get; }

        public string MediaRoot { get; }

        public QuizzerApiClient Api { get; }

        public ClientSyncService Sync { get; }

        public static async Task<ClientHarness> CreateAsync(WebApplicationFactory<Program> factory, string name)
        {
            var builder = new SqlConnectionStringBuilder(ServerTestHost.ConnectionString)
            {
                InitialCatalog = $"Quizzer_Client_{name}_Tests",
            };

            await using (var context = new DataContext(builder.ConnectionString))
            {
                await context.Database.EnsureDeletedAsync();
                await context.Database.MigrateAsync();
            }

            var media = Path.Combine(ServerTestHost.MediaRoot, "..", Path.GetFileName(ServerTestHost.MediaRoot) + "-client-" + name);
            Directory.CreateDirectory(media);

            var tokens = new MemoryTokenStore();
            var api = new QuizzerApiClient(new Uri("https://localhost/api/v1/"), tokens, handler: factory.Server.CreateHandler());
            return new ClientHarness(builder.ConnectionString, Path.GetFullPath(media), api, tokens);
        }

        /// <summary>Ein Kontext auf die lokale Datenbank - so, wie die Controller im WPF schreiben.</summary>
        public DataContext Local() => new(ConnectionString);

        public async ValueTask DisposeAsync()
        {
            Api.Dispose();
            if (Directory.Exists(MediaRoot))
                Directory.Delete(MediaRoot, recursive: true);
            await Task.CompletedTask;
        }
    }
}
