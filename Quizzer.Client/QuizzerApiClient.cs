using Quizzer.DataModels.Transfer.Server;
using Quizzer.DataModels.Transfer.Sync;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

namespace Quizzer.Client
{
    /// <summary>
    /// Der Weg des WPF zum Quizzer-Server. Spricht <b>nur https</b>, prueft das Zertifikat
    /// vollstaendig und haelt den Zugangsausweis nur im Speicher.
    /// <para>
    /// <b>Fingerabdruck statt Ausnahme:</b> ein selbst erzeugtes Zertifikat wird nur angenommen,
    /// wenn sein SHA-256 als <see cref="PinnedCertificateSha256"/> eingetragen ist. Es gibt keinen
    /// Schalter „Zertifikat nicht pruefen".
    /// </para>
    /// </summary>
    public sealed class QuizzerApiClient : IDisposable
    {
        public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

        private readonly HttpClient http;
        private readonly ITokenStore tokenStore;
        private readonly SemaphoreSlim refreshLock = new(1, 1);
        private readonly TimeProvider time;

        private string? accessToken;
        private DateTimeOffset accessTokenExpiresAt;

        public QuizzerApiClient(Uri baseAddress, ITokenStore tokenStore, string? pinnedCertificateSha256 = null,
            HttpMessageHandler? handler = null, TimeProvider? time = null)
        {
            if (!string.Equals(baseAddress.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Der Server wird nur über https angesprochen.", nameof(baseAddress));

            this.tokenStore = tokenStore;
            this.time = time ?? TimeProvider.System;
            PinnedCertificateSha256 = NormalizeThumbprint(pinnedCertificateSha256);

            http = new HttpClient(handler ?? CreateHandler(PinnedCertificateSha256), disposeHandler: true)
            {
                BaseAddress = baseAddress,
                Timeout = TimeSpan.FromSeconds(100),
            };
        }

        public string? PinnedCertificateSha256 { get; }

        public Uri BaseAddress => http.BaseAddress!;

        /// <summary>Das angemeldete Konto, oder <c>null</c>.</summary>
        public AccountInfo? Account { get; private set; }

        public bool IsSignedIn => Account != null;

        public static SocketsHttpHandler CreateHandler(string? pinnedSha256)
        {
            var handler = new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                ConnectTimeout = TimeSpan.FromSeconds(15),
            };

            handler.SslOptions.RemoteCertificateValidationCallback = (_, certificate, _, errors) =>
                IsCertificateAccepted(certificate, errors, pinnedSha256);

            return handler;
        }

        /// <summary>
        /// Angenommen wird ein Zertifikat ohne jeden Fehler - oder genau das eine, dessen
        /// Fingerabdruck eingetragen ist.
        /// </summary>
        public static bool IsCertificateAccepted(X509Certificate? certificate, SslPolicyErrors errors, string? pinnedSha256)
        {
            if (errors == SslPolicyErrors.None)
                return true;

            if (certificate == null || string.IsNullOrEmpty(pinnedSha256))
                return false;

            return string.Equals(Thumbprint(certificate), pinnedSha256, StringComparison.Ordinal);
        }

        public static string Thumbprint(X509Certificate certificate)
            => Convert.ToHexString(SHA256.HashData(certificate.GetRawCertData()));

        public static string? NormalizeThumbprint(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            var hex = new string(text.Where(Uri.IsHexDigit).ToArray()).ToUpperInvariant();
            return hex.Length == 64 ? hex : throw new ArgumentException("Ein SHA-256-Fingerabdruck hat 64 Hex-Zeichen.");
        }

        public async Task<VersionResponse> GetVersionAsync(CancellationToken ct = default)
            => await SendAsync<VersionResponse>(HttpMethod.Get, "version", null, authorized: false, ct);

        public async Task<TokenResponse> LoginAsync(string userName, string password, CancellationToken ct = default)
        {
            var tokens = await SendAsync<TokenResponse>(HttpMethod.Post, "auth/login", new LoginRequest(userName, password), authorized: false, ct);
            Accept(tokens);
            return tokens;
        }

        /// <summary>Meldet mit dem gespeicherten Erneuerungsschluessel wieder an. <c>false</c>, wenn keiner gilt.</summary>
        public async Task<bool> TryResumeAsync(CancellationToken ct = default)
        {
            if (tokenStore.Load() == null)
                return false;

            try
            {
                await RefreshAsync(ct);
                return true;
            }
            catch (SessionExpiredException)
            {
                return false;
            }
        }

        public async Task LogoutAsync(CancellationToken ct = default)
        {
            var refresh = tokenStore.Load();

            try
            {
                if (refresh != null && accessToken != null)
                    await SendAsync<object>(HttpMethod.Post, "auth/logout", new RefreshRequest(refresh), authorized: true, ct);
            }
            finally
            {
                tokenStore.Clear();
                accessToken = null;
                Account = null;
            }
        }

        public Task<AccountInfo> GetMeAsync(CancellationToken ct = default)
            => SendAsync<AccountInfo>(HttpMethod.Get, "me", null, authorized: true, ct);

        public Task ChangePasswordAsync(string current, string next, CancellationToken ct = default)
            => SendAsync<object>(HttpMethod.Post, "me/password", new ChangePasswordRequest(current, next), authorized: true, ct);

        public Task<StatisticsResponse> GetStatisticsAsync(CancellationToken ct = default)
            => SendAsync<StatisticsResponse>(HttpMethod.Get, "me/statistics", null, authorized: true, ct);

        public Task<SyncSnapshot> GetSnapshotAsync(CancellationToken ct = default)
            => SendAsync<SyncSnapshot>(HttpMethod.Get, "sync/snapshot", null, authorized: true, ct);

        public Task<SyncPushResponse> PushAsync(SyncPushRequest request, CancellationToken ct = default)
            => SendAsync<SyncPushResponse>(HttpMethod.Post, "sync/push", request, authorized: true, ct);

        public Task<List<AccountListItem>> GetAccountsAsync(CancellationToken ct = default)
            => SendAsync<List<AccountListItem>>(HttpMethod.Get, "accounts", null, authorized: true, ct);

        public Task<AccountListItem> CreateAccountAsync(CreateAccountRequest request, CancellationToken ct = default)
            => SendAsync<AccountListItem>(HttpMethod.Post, "accounts", request, authorized: true, ct);

        public Task UpdateAccountAsync(Guid accountId, UpdateAccountRequest request, CancellationToken ct = default)
            => SendAsync<object>(HttpMethod.Put, $"accounts/{accountId}", request, authorized: true, ct);

        public Task<List<MediaFileInfo>> GetMediaManifestAsync(CancellationToken ct = default)
            => SendAsync<List<MediaFileInfo>>(HttpMethod.Get, "media", null, authorized: true, ct);

        /// <summary>Laedt eine Mediendatei herunter. <c>false</c>, wenn der Server sie nicht herausgibt.</summary>
        public async Task<bool> DownloadMediaAsync(string path, Stream target, CancellationToken ct = default)
        {
            using var response = await SendRawAsync(() => new HttpRequestMessage(HttpMethod.Get, "media/content?path=" + Uri.EscapeDataString(path)), ct);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return false;

            await EnsureSuccessAsync(response, ct);
            await response.Content.CopyToAsync(target, ct);
            return true;
        }

        /// <summary>Laedt eine Mediendatei hoch. <c>false</c>, wenn unter dem Namen schon eine andere liegt.</summary>
        public async Task<bool> UploadMediaAsync(string path, Func<Stream> open, CancellationToken ct = default)
        {
            // Jede Anfrage haelt ihren eigenen Datenstrom - und gibt ihn wieder frei. Sonst bliebe
            // die Datei unter Windows bis zur naechsten Speicherbereinigung gesperrt (Fund 9).
            var requests = new List<HttpRequestMessage>();
            try
            {
                using var response = await SendRawAsync(() =>
                {
                    var request = new HttpRequestMessage(HttpMethod.Put, "media/content?path=" + Uri.EscapeDataString(path))
                    {
                        Content = new StreamContent(open()),
                    };
                    request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                    requests.Add(request);
                    return request;
                }, ct);

                if (response.StatusCode == HttpStatusCode.Conflict)
                    return false;

                await EnsureSuccessAsync(response, ct);
                return true;
            }
            finally
            {
                foreach (var request in requests)
                    request.Dispose();
            }
        }


        private void Accept(TokenResponse tokens)
        {
            accessToken = tokens.AccessToken;
            accessTokenExpiresAt = tokens.AccessTokenExpiresAt;
            Account = tokens.Account;
            tokenStore.Save(tokens.RefreshToken);
        }

        private async Task RefreshAsync(CancellationToken ct)
        {
            await refreshLock.WaitAsync(ct);
            try
            {
                var refresh = tokenStore.Load() ?? throw new SessionExpiredException("Nicht angemeldet.");

                TokenResponse tokens;
                try
                {
                    tokens = await SendAsync<TokenResponse>(HttpMethod.Post, "auth/refresh", new RefreshRequest(refresh), authorized: false, ct);
                }
                catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
                {
                    tokenStore.Clear();
                    accessToken = null;
                    throw new SessionExpiredException(ex.Message);
                }

                Accept(tokens);
            }
            finally
            {
                refreshLock.Release();
            }
        }

        private async Task EnsureAccessTokenAsync(CancellationToken ct)
        {
            if (accessToken == null || accessTokenExpiresAt - time.GetUtcNow() < TimeSpan.FromSeconds(60))
                await RefreshAsync(ct);
        }

        private async Task<T> SendAsync<T>(HttpMethod method, string path, object? body, bool authorized, CancellationToken ct)
        {
            HttpRequestMessage Build()
            {
                var request = new HttpRequestMessage(method, path);
                if (body != null)
                    request.Content = JsonContent.Create(body, body.GetType(), options: Json);
                return request;
            }

            using var response = authorized ? await SendRawAsync(Build, ct) : await SendOnceAsync(Build(), ct);
            await EnsureSuccessAsync(response, ct);

            if (response.StatusCode == HttpStatusCode.NoContent || typeof(T) == typeof(object))
                return default!;

            return (await response.Content.ReadFromJsonAsync<T>(Json, ct))!;
        }

        /// <summary>Mit Ausweis; bei 401 einmal erneuern und wiederholen.</summary>
        private async Task<HttpResponseMessage> SendRawAsync(Func<HttpRequestMessage> build, CancellationToken ct)
        {
            await EnsureAccessTokenAsync(ct);

            var request = build();
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var response = await SendOnceAsync(request, ct);

            if (response.StatusCode != HttpStatusCode.Unauthorized)
                return response;

            response.Dispose();
            await RefreshAsync(ct);

            var retry = build();
            retry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            return await SendOnceAsync(retry, ct);
        }

        private async Task<HttpResponseMessage> SendOnceAsync(HttpRequestMessage request, CancellationToken ct)
        {
            try
            {
                return await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            }
            catch (HttpRequestException ex)
            {
                throw new ServerUnreachableException(DescribeUnreachable(ex), ex);
            }
            catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
            {
                throw new ServerUnreachableException("Der Server antwortet nicht (Zeitüberschreitung).", ex);
            }
        }

        private static string DescribeUnreachable(HttpRequestException ex)
            => ex.InnerException is System.Security.Authentication.AuthenticationException
                ? "Das Zertifikat des Servers wurde nicht angenommen. Ist die Adresse richtig, oder gehört ein Fingerabdruck in die Einstellungen?"
                : "Der Server ist nicht erreichbar: " + ex.Message;

        private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
        {
            if (response.IsSuccessStatusCode)
                return;

            string message;
            try
            {
                var error = await response.Content.ReadFromJsonAsync<ApiError>(Json, ct);
                message = string.IsNullOrWhiteSpace(error?.Message) ? DefaultMessage(response.StatusCode) : error.Message;
            }
            catch (Exception)
            {
                message = DefaultMessage(response.StatusCode);
            }

            throw new ApiException(response.StatusCode, message);
        }

        private static string DefaultMessage(HttpStatusCode status) => status switch
        {
            HttpStatusCode.Unauthorized => "Nicht angemeldet.",
            HttpStatusCode.Forbidden => "Dafür fehlt die Berechtigung.",
            HttpStatusCode.NotFound => "Nicht gefunden.",
            HttpStatusCode.TooManyRequests => "Zu viele Versuche. Bitte eine Minute warten.",
            _ => $"Der Server meldet einen Fehler ({(int)status}).",
        };

        public void Dispose()
        {
            http.Dispose();
            refreshLock.Dispose();
        }
    }
}
