using Quizzer.Client;
using Quizzer.DataModels;
using Quizzer.DataModels.Transfer.Server;
using Quizzer.Logic.Context;
using Quizzer.Logic.Controller.TypedControllers;
using Quizzer.Logic.Sync;
using System.IO;
using System.Text.Json;

namespace Quizzer.Server
{
    /// <summary>
    /// Die Verbindung des Programms zum Quizzer-Server (M9) - eine je Programmlauf.
    /// <para>
    /// <b>Im Servermodus arbeitet das Programm auf einer eigenen lokalen Datenbank je Konto</b>
    /// (<see cref="CacheDatabaseName"/>). Beim Anmelden wird alles Sichtbare dorthin geholt;
    /// danach laeuft jedes Fenster genau wie ohne Server, und <see cref="BackgroundSync"/>
    /// schickt Gespeichertes nebenher zurueck. Die bisherige lokale Spieldatenbank bleibt dabei
    /// unberuehrt.
    /// </para>
    /// </summary>
    public static class ServerSession
    {
        public static string LocalFolder => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Quizzer", "server");

        /// <summary>Ob in den Einstellungen der Servermodus eingeschaltet ist.</summary>
        public static bool IsConfigured
        {
            get
            {
                var werte = UserSettings.Load();
                return werte.UseServer && !string.IsNullOrWhiteSpace(werte.ServerUrl);
            }
        }

        public static QuizzerApiClient? Api { get; private set; }

        public static ClientSyncService? Sync { get; private set; }

        public static BackgroundSync? Background { get; private set; }

        public static AccountInfo? Account { get; private set; }

        /// <summary>Ohne Server weitergearbeitet - Aenderungen gehen beim naechsten Anmelden hinaus.</summary>
        public static bool IsOffline { get; private set; }

        public static bool IsActive => Account != null;

        public static bool IsAdmin => Account?.Role == DataModels.Enumerations.AccountRole.Admin && !IsOffline;

        /// <summary>Der Datenbankname der lokalen Zwischendatenbank eines Kontos.</summary>
        public static string CacheDatabaseName(Guid accountId) => "Quizzer_Server_" + accountId.ToString("N")[..12];

        /// <summary>Baut den Client aus den Einstellungen. Wirft bei einer ungueltigen Adresse.</summary>
        public static QuizzerApiClient CreateApi()
        {
            var werte = UserSettings.Load();
            var url = NormalizeUrl(werte.ServerUrl);
            Api?.Dispose();
            Api = new QuizzerApiClient(url, new DpapiTokenStore(url), werte.ServerCertificateSha256);
            return Api;
        }

        /// <summary>Aus „https://server:8443" wird „https://server:8443/api/v1/".</summary>
        public static Uri NormalizeUrl(string? text)
        {
            if (string.IsNullOrWhiteSpace(text) || !Uri.TryCreate(text.Trim(), UriKind.Absolute, out var uri))
                throw new ArgumentException("Die Serveradresse ist keine gültige Adresse.");

            if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Die Serveradresse muss mit https:// beginnen.");

            var root = uri.GetLeftPart(UriPartial.Authority);
            return new Uri(root + ApiInfo.BasePath + "/");
        }

        /// <summary>
        /// Nach erfolgreicher Anmeldung: auf die Zwischendatenbank des Kontos umschalten, sie auf
        /// Stand bringen und alles abholen.
        /// </summary>
        public static async Task<PullResult> ActivateAsync(QuizzerApiClient api, IProgress<string>? progress)
        {
            var account = api.Account ?? throw new InvalidOperationException("Nicht angemeldet.");

            progress?.Report("Lokale Datenbank wird vorbereitet …");
            await SwitchToCacheAsync(account);

            Sync = new ClientSyncService(api, () => new SyncStore(), new FileSyncStateStore(StatePath(api.BaseAddress, account.AccountId)), Settings.FilePathQuizzer);

            progress?.Report("Daten werden vom Server geladen …");
            var result = await Sync.PullAsync();

            await SignInLocallyAsync(account);
            RememberAccount(api.BaseAddress, account);
            IsOffline = false;

            UserSettings.SaveServer(true, UserSettings.Load().ServerUrl, UserSettings.Load().ServerCertificateSha256, account.UserName);
            return result;
        }

        /// <summary>Ohne Server weiterarbeiten - mit dem Stand der letzten Anmeldung auf diesem Rechner.</summary>
        public static async Task<bool> ActivateOfflineAsync(Uri server)
        {
            var account = LoadRememberedAccount(server);

            // Offline nur, wer sich auf diesem Rechner und unter diesem Windows-Konto schon einmal
            // angemeldet hat - der verschluesselte Erneuerungsschluessel ist der Beleg dafuer.
            if (account == null || new DpapiTokenStore(server).Load() == null)
                return false;

            await SwitchToCacheAsync(account);

            if (!DatabaseInitializer.Exists())
                return false;

            await SignInLocallyAsync(account);
            IsOffline = true;
            return true;
        }

        /// <summary>Ob es fuer diesen Server einen Stand gibt, mit dem man offline weiterarbeiten kann.</summary>
        public static bool CanWorkOffline(Uri server)
            => LoadRememberedAccount(server) != null && new DpapiTokenStore(server).Load() != null;

        /// <summary>Startet den Hintergrund-Abgleich. Ohne Server (offline) geschieht nichts.</summary>
        public static void StartBackground()
        {
            if (Sync == null || IsOffline)
                return;

            Background?.Dispose();
            Background = new BackgroundSync(Sync);
            Background.Start();
        }

        /// <summary>Vor dem Beenden: noch Offenes schicken, aber nicht ewig warten.</summary>
        public static void Shutdown()
        {
            try
            {
                // Auf dem Threadpool: auf dem UI-Thread warteten die Fortsetzungen auf genau den
                // Thread, der hier blockiert - das Programm hinge beim Beenden.
                if (Background is { } background)
                    Task.Run(background.FlushAsync).Wait(TimeSpan.FromSeconds(8));
            }
            catch (Exception)
            {
                // Beim Beenden darf nichts mehr werfen; lokal ist ohnehin alles gespeichert.
            }

            Background?.Dispose();
            Api?.Dispose();
        }

        /// <summary>
        /// Die lokale Verbindung von vor dem Umschalten auf die Zwischendatenbank. Die
        /// Einstellungsmaske zeigt und speichert diese - nie die der Zwischendatenbank.
        /// </summary>
        public static string? LocalConnectionString { get; private set; }

        private static async Task SwitchToCacheAsync(AccountInfo account)
        {
            LocalConnectionString ??= Settings.ConnectionString;
            var local = LocalConnectionString;
            Settings.ConnectionString = DatabaseInitializer.WithDatabase(local, CacheDatabaseName(account.AccountId));
            Account = account;

            await Task.Run(DatabaseInitializer.EnsureMigrated);
        }

        private static async Task SignInLocallyAsync(AccountInfo account)
        {
            using var players = new PlayersController();
            var player = await players.GetAsync(account.PlayerId)
                ?? throw new InvalidOperationException("Der eigene Mitspieler fehlt in den abgeholten Daten.");

            Session.SignIn(player);
        }

        private static string StatePath(Uri server, Guid accountId)
            => Path.Combine(LocalFolder, $"sync-{Hash(server)}-{accountId:N}.json");

        private static string AccountPath(Uri server) => Path.Combine(LocalFolder, $"account-{Hash(server)}.json");

        private static string Hash(Uri server)
            => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(server.GetLeftPart(UriPartial.Authority).ToLowerInvariant())))[..16];

        private static void RememberAccount(Uri server, AccountInfo account)
        {
            Directory.CreateDirectory(LocalFolder);
            File.WriteAllText(AccountPath(server), JsonSerializer.Serialize(account));
        }

        private static AccountInfo? LoadRememberedAccount(Uri server)
        {
            var path = AccountPath(server);
            if (!File.Exists(path))
                return null;

            try
            {
                return JsonSerializer.Deserialize<AccountInfo>(File.ReadAllText(path));
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}
