using Quizzer.DataModels.Transfer.Server;
using System.Net;
using System.Net.Http.Json;

namespace Quizzer.Client
{
    /// <summary>Das WPF-Paket zum Aktualisieren (M36 A7) - beides ohne Ausweis, auch fuer ein abgewiesenes Programm.</summary>
    public sealed partial class QuizzerApiClient
    {
        /// <summary>Version, Groesse und Pruefsumme des bereitgestellten Pakets; <c>null</c>, wenn der Server keins hat.</summary>
        public async Task<ClientPackageInfo?> GetClientPackageAsync(CancellationToken ct = default)
        {
            using var response = await SendOnceAsync(new HttpRequestMessage(HttpMethod.Get, "client/latest"), ct);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;

            await EnsureSuccessAsync(response, ct);
            return await response.Content.ReadFromJsonAsync<ClientPackageInfo>(Json, ct);
        }

        /// <summary>
        /// Laedt das Paket nach <paramref name="target"/> und meldet die bisher geladenen Bytes.
        /// <c>false</c>, wenn der Server keins hat. Geprueft wird beim Aufrufer.
        /// </summary>
        public async Task<bool> DownloadClientPackageAsync(Stream target, IProgress<long>? progress = null, CancellationToken ct = default)
        {
            using var response = await SendOnceAsync(new HttpRequestMessage(HttpMethod.Get, "client/package"), ct);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return false;

            await EnsureSuccessAsync(response, ct);

            await using var source = await response.Content.ReadAsStreamAsync(ct);
            var buffer = new byte[81920];
            long total = 0;
            int read;

            while ((read = await source.ReadAsync(buffer, ct)) > 0)
            {
                await target.WriteAsync(buffer.AsMemory(0, read), ct);
                total += read;
                progress?.Report(total);
            }

            return true;
        }
    }
}
