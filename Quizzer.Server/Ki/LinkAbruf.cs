using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace Quizzer.Server.Ki
{
    /// <summary>
    /// Holt öffentliche Seiten für einen KI-Entwurf (M31) - und <b>nur</b> öffentliche.
    /// <para>
    /// <b>Die Gefahr:</b> der Server steht im Heimnetz. Ein Link auf <c>http://192.168.1.1/</c>,
    /// <c>http://localhost:8080/</c> oder <c>http://sql:1433</c> ließe ihn Geräte abfragen, die von
    /// außen nicht erreichbar sind, und deren Antwort landete im Entwurf (SSRF).
    /// </para>
    /// <para>
    /// <b>Geprüft wird beim Verbinden, nicht beim Lesen der Adresse</b>
    /// (<see cref="SocketsHttpHandler.ConnectCallback"/>): jede Verbindung - auch nach einer
    /// Weiterleitung, auch wenn ein Name zwischen Prüfen und Verbinden auf eine andere Adresse
    /// zeigt - geht nur an eine öffentliche Adresse. Dazu nur http/https, höchstens drei
    /// Weiterleitungen, 2 MB, 10 Sekunden.
    /// </para>
    /// </summary>
    public sealed class LinkAbruf : IDisposable
    {
        public const int MaxBytes = 2 * 1024 * 1024;
        public const int MaxZeichen = 20_000;

        private readonly HttpClient http;

        /// <param name="erlaubt">Welche Adressen verbunden werden dürfen. Vorgabe: <see cref="IstOeffentlich"/>. Tests setzen hier ihre eigene Regel.</param>
        public LinkAbruf(Func<IPAddress, bool>? erlaubt = null)
        {
            var pruefe = erlaubt ?? IstOeffentlich;

            var handler = new SocketsHttpHandler
            {
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 3,
                AutomaticDecompression = DecompressionMethods.All,
                UseProxy = false,
                ConnectCallback = async (kontext, ct) =>
                {
                    var adressen = await Dns.GetHostAddressesAsync(kontext.DnsEndPoint.Host, ct);
                    var ziel = adressen.FirstOrDefault(pruefe)
                        ?? throw new HttpRequestException($"{kontext.DnsEndPoint.Host} ist keine öffentliche Adresse.");

                    // Alle aufgelösten Adressen müssen erlaubt sein - sonst entschiede die Reihenfolge.
                    if (adressen.Any(a => !pruefe(a)))
                        throw new HttpRequestException($"{kontext.DnsEndPoint.Host} zeigt (auch) ins interne Netz.");

                    var socket = new Socket(ziel.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                    try
                    {
                        await socket.ConnectAsync(new IPEndPoint(ziel, kontext.DnsEndPoint.Port), ct);
                        return new NetworkStream(socket, ownsSocket: true);
                    }
                    catch
                    {
                        socket.Dispose();
                        throw;
                    }
                },
            };

            http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("Quizzer-Fragenentwurf/1.0");
        }

        /// <summary>Ein abgerufener Link: Text oder der Grund, warum nicht.</summary>
        public sealed record Ergebnis(string Link, string? Text, string? Fehler);

        public async Task<Ergebnis> HoleAsync(string link, CancellationToken ct)
        {
            if (!Uri.TryCreate(link?.Trim(), UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
                return new Ergebnis(link ?? string.Empty, null, "kein http- oder https-Link");

            try
            {
                using var antwort = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);

                if (!antwort.IsSuccessStatusCode)
                    return new Ergebnis(link!, null, $"Antwort {(int)antwort.StatusCode}");

                var typ = antwort.Content.Headers.ContentType?.MediaType ?? "text/html";
                if (typ is not ("text/html" or "text/plain" or "application/xhtml+xml"))
                    return new Ergebnis(link!, null, $"Inhaltstyp {typ} wird nicht gelesen");

                if (antwort.Content.Headers.ContentLength > MaxBytes)
                    return new Ergebnis(link!, null, "größer als 2 MB");

                await using var strom = await antwort.Content.ReadAsStreamAsync(ct);
                var puffer = new MemoryStream();
                var block = new byte[16 * 1024];
                int gelesen;

                while ((gelesen = await strom.ReadAsync(block, ct)) > 0)
                {
                    if (puffer.Length + gelesen > MaxBytes)
                        return new Ergebnis(link!, null, "größer als 2 MB");

                    puffer.Write(block, 0, gelesen);
                }

                var roh = Encoding.UTF8.GetString(puffer.ToArray());
                var text = typ == "text/plain" ? roh : HtmlZuText(roh);

                return new Ergebnis(link!, text.Length <= MaxZeichen ? text : text[..MaxZeichen], null);
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException or IOException or SocketException)
            {
                return new Ergebnis(link!, null, e is TaskCanceledException && !ct.IsCancellationRequested ? "Zeitüberschreitung" : e.Message);
            }
        }

        /// <summary>
        /// Ob eine Adresse öffentlich ist: nicht Loopback, nicht privat (RFC 1918), nicht
        /// link-local, nicht CGNAT/Tailscale (100.64/10), nicht ULA (fc00::/7), nicht Multicast
        /// und nicht unbestimmt.
        /// </summary>
        public static bool IstOeffentlich(IPAddress adresse)
        {
            if (adresse.IsIPv4MappedToIPv6)
                adresse = adresse.MapToIPv4();

            if (IPAddress.IsLoopback(adresse) || adresse.Equals(IPAddress.Any) || adresse.Equals(IPAddress.IPv6Any) || adresse.Equals(IPAddress.IPv6None))
                return false;

            if (adresse.AddressFamily == AddressFamily.InterNetwork)
            {
                var b = adresse.GetAddressBytes();
                return !(b[0] == 0
                    || b[0] == 10
                    || b[0] == 127
                    || (b[0] == 100 && b[1] >= 64 && b[1] <= 127)
                    || (b[0] == 169 && b[1] == 254)
                    || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
                    || (b[0] == 192 && b[1] == 168)
                    || (b[0] == 192 && b[1] == 0 && b[2] == 0)
                    || (b[0] == 198 && (b[1] == 18 || b[1] == 19))
                    || b[0] >= 224);
            }

            if (adresse.AddressFamily == AddressFamily.InterNetworkV6)
            {
                var b = adresse.GetAddressBytes();
                return !(adresse.IsIPv6LinkLocal || adresse.IsIPv6SiteLocal || adresse.IsIPv6Multicast
                    || (b[0] & 0xFE) == 0xFC);
            }

            return false;
        }

        private static readonly Regex Unsichtbar = new(@"<(script|style|noscript|svg|head|nav|footer|form)\b[^>]*>.*?</\1>", RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex Absatz = new(@"<(br|/p|/div|/li|/h[1-6]|/tr)\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex Marke = new(@"<[^>]+>", RegexOptions.Compiled);
        private static readonly Regex Leerraum = new(@"[ \t\r\f\v]+", RegexOptions.Compiled);
        private static readonly Regex Leerzeilen = new(@"\n\s*\n+", RegexOptions.Compiled);

        /// <summary>Grob, aber genug für eine KI: Skripte und Stile weg, Absätze als Zeilen, Marken weg.</summary>
        public static string HtmlZuText(string html)
        {
            var t = Unsichtbar.Replace(html, " ");
            t = Absatz.Replace(t, "\n");
            t = Marke.Replace(t, " ");
            t = WebUtility.HtmlDecode(t);
            t = Leerraum.Replace(t, " ");
            t = Leerzeilen.Replace(t, "\n\n");
            return t.Trim();
        }

        public void Dispose() => http.Dispose();
    }
}
