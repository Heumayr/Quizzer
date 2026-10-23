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

            // Ausdruecklich gegen die Zwischendatenbank, nicht gegen Settings.ConnectionString -
            // die globale Einstellung kann sich unter dem laufenden Abgleich aendern (Fund 1).
            var cache = CacheConnectionString!;
            Sync = new ClientSyncService(api, () => new SyncStore(cache), new FileSyncStateStore(StatePath(api.BaseAddress, account.AccountId)), Settings.FilePathQuizzer);

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

        private static Wiederverbindung? wiederverbindung;

        /// <summary>Nach dem Ende der Wiederverbindung (verbunden oder Anmeldung noetig) - auf einem Hintergrund-Thread.</summary>
        public static event EventHandler<WiederverbindungsErgebnis>? Wiederverbunden;

        /// <summary>Das letzte Ergebnis der Wiederverbindung - fuer die Statuszeile.</summary>
        public static WiederverbindungsErgebnis? LetzteWiederverbindung { get; private set; }

        /// <summary>
        /// Offline gestartet: im Hintergrund auf den Server warten und dann von selbst uebertragen
        /// (2026-10-22). Ohne Offline-Sitzung geschieht nichts.
        /// </summary>
        public static void StartReconnectWatch()
        {
            if (!IsOffline || wiederverbindung != null)
                return;

            wiederverbindung = new Wiederverbindung(TryReconnectAsync);
            wiederverbindung.Beendet += (_, ergebnis) =>
            {
                LetzteWiederverbindung = ergebnis;
                Wiederverbunden?.Invoke(null, ergebnis);
            };
            wiederverbindung.Start();
        }

        /// <summary>
        /// Ein Versuch: mit dem gespeicherten Erneuerungsschluessel anmelden und, wenn es dasselbe
        /// Konto ist, den Hintergrund-Abgleich starten. Auch von „Jetzt verbinden" gerufen.
        /// <para>
        /// <b>Nur zurueckschicken, nicht abholen.</b> Laeuft gerade ein Spiel, haelt das Fenster
        /// seinen Stand im Speicher; ein Abholen schriebe darunter in die Datenbank. Was der Server
        /// wegen eines Konflikts abweist, zeigt „Warum nicht übertragen?" (V4).
        /// </para>
        /// <para>
        /// <b>Nur dasselbe Konto.</b> Ein anderes hiesse eine andere Zwischendatenbank - die darf
        /// nicht unter offenen Fenstern wechseln. Dann bleibt es bei „Anmeldung noetig".
        /// </para>
        /// </summary>
        public static async Task<WiederverbindungsErgebnis> TryReconnectAsync(CancellationToken ct = default)
        {
            // Wächter und Knopf können gleichzeitig kommen - einer genügt.
            await reconnectGate.WaitAsync(ct);
            try
            {
                return await TryReconnectCoreAsync(ct);
            }
            finally
            {
                reconnectGate.Release();
            }
        }

        private static readonly SemaphoreSlim reconnectGate = new(1, 1);

        private static async Task<WiederverbindungsErgebnis> TryReconnectCoreAsync(CancellationToken ct)
        {
            if (!IsOffline)
                return WiederverbindungsErgebnis.Verbunden;

            if (Account is not { } offline || CacheConnectionString is not { } cache)
                return WiederverbindungsErgebnis.AnmeldungNoetig;

            QuizzerApiClient api;
            try
            {
                api = CreateApi();
            }
            catch (ArgumentException)
            {
                return WiederverbindungsErgebnis.AnmeldungNoetig;
            }

            try
            {
                if (!await api.TryResumeAsync(ct))
                    return WiederverbindungsErgebnis.AnmeldungNoetig;
            }
            catch (Exception ex) when (ex is ServerUnreachableException or IOException or System.Net.Http.HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
            {
                return WiederverbindungsErgebnis.NichtErreichbar;
            }

            if (api.Account is not { } account || account.AccountId != offline.AccountId)
                return WiederverbindungsErgebnis.AnmeldungNoetig;

            Sync = new ClientSyncService(api, () => new SyncStore(cache), new FileSyncStateStore(StatePath(api.BaseAddress, account.AccountId)), Settings.FilePathQuizzer);
            Account = account;
            RememberAccount(api.BaseAddress, account);
            IsOffline = false;

            // Der Hintergrund-Abgleich schickt beim Start von selbst, was offline gespeichert wurde.
            StartBackground();
            return WiederverbindungsErgebnis.Verbunden;
        }

        /// <summary>Ob es fuer diesen Server einen Stand gibt, mit dem man offline weiterarbeiten kann.</summary>
        public static bool CanWorkOffline(Uri server)
            => LoadRememberedAccount(server) != null && new DpapiTokenStore(server).Load() != null;

        /// <summary>Ein neuer Hintergrund-Abgleich - etwa nach der Wiederverbindung. Fuer die Statuszeile.</summary>
        public static event EventHandler? BackgroundStarted;

        /// <summary>Startet den Hintergrund-Abgleich. Ohne Server (offline) geschieht nichts.</summary>
        public static void StartBackground()
        {
            if (Sync == null || IsOffline)
                return;

            Background?.Dispose();
            Background = new BackgroundSync(Sync);
            Background.Start();
            BackgroundStarted?.Invoke(null, EventArgs.Empty);
        }

        /// <summary>Vor dem Beenden: noch Offenes schicken, aber nicht ewig warten.</summary>
        public static void Shutdown()
        {
            wiederverbindung?.Dispose();

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

        /// <summary>Die Zwischendatenbank des Kontos. Der Abgleich arbeitet <b>nur</b> hiergegen.</summary>
        public static string? CacheConnectionString { get; private set; }

        /// <summary>
        /// Nach dem Speichern oder Zuruecksetzen der Einstellungen: die laufende Sitzung bleibt auf
        /// der Zwischendatenbank. <c>UserSettings.Save</c> setzt <c>Settings.ConnectionString</c>
        /// auf die eigene lokale Datenbank - und ohne diesen Ruecksprung hielte der naechste Push
        /// jede Serverzeile fuer lokal geloescht und loeschte sie am Server (Durchsicht
        /// 2026-10-21, Fund 1).
        /// </summary>
        public static void KeepSessionDatabase()
        {
            if (CacheConnectionString == null)
                return;

            LocalConnectionString = Settings.ConnectionString;
            Settings.ConnectionString = CacheConnectionString;
        }

        private static async Task SwitchToCacheAsync(AccountInfo account)
        {
            LocalConnectionString ??= Settings.ConnectionString;
            var local = LocalConnectionString;
            CacheConnectionString = DatabaseInitializer.WithDatabase(local, CacheDatabaseName(account.AccountId));
            Settings.ConnectionString = CacheConnectionString;
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
