using Microsoft.Extensions.Options;
using Quizzer.DataModels.Transfer.Ki;
using Quizzer.Server.Auth;
using Quizzer.Server.Infrastructure;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Quizzer.Server.Ki
{
    /// <summary>
    /// Hält die KI-Einstellungen und den Tageszähler (M31, 2026-10-23) - als zwei kleine Dateien
    /// im Ordner <c>.ki</c> des Medien-Volumes, nicht in der Datenbank.
    /// <para>
    /// <b>Warum nicht in der Datenbank:</b> die Einstellungen gehören dem Server allein und dürfen
    /// nie in den Abgleich geraten; eine eigene Tabelle hätte eine Migration im gemeinsamen
    /// Schema gebraucht, die jeder WPF-Client mitzieht. Der Ordner beginnt mit einem Punkt, und
    /// <c>MediaService</c> liefert nur Dateien aus, auf die eine Zeile verweist - von außen ist er
    /// nicht erreichbar.
    /// </para>
    /// <para>
    /// <b>Der Schlüssel liegt verschlüsselt</b> (AES-GCM), mit einem Schlüssel, der aus dem
    /// Signierschlüssel des Servers abgeleitet ist. Der steht in <c>.env</c>, nicht im Volume -
    /// eine Sicherung des Volumes allein verrät den API-Schlüssel also nicht. Wird der
    /// Signierschlüssel gewechselt, ist der KI-Schlüssel nicht mehr lesbar und muss neu eingegeben
    /// werden; das wird gemeldet, nicht verschwiegen.
    /// </para>
    /// </summary>
    public sealed class KiSpeicher
    {
        private sealed class Gespeichert
        {
            public KiAnbieter Anbieter { get; set; }
            public string? Adresse { get; set; }
            public string Modell { get; set; } = string.Empty;
            public string? Schluessel { get; set; }
            public int Tageslimit { get; set; } = 20;
            public bool LinksAbrufen { get; set; } = true;
            public List<Guid> FreigegebeneKonten { get; set; } = new();
        }

        private sealed class Nutzung
        {
            public string Tag { get; set; } = string.Empty;
            public Dictionary<Guid, int> Zaehler { get; set; } = new();
        }

        private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

        private readonly SemaphoreSlim gate = new(1, 1);
        private readonly byte[] verschluesselung;
        private readonly TimeProvider zeit;

        public KiSpeicher(IOptions<ServerOptions> options, TokenService tokens, TimeProvider zeit)
        {
            var o = options.Value;
            Ordner = string.IsNullOrWhiteSpace(o.KiFolder) ? Path.Combine(o.MediaRoot, ".ki") : o.KiFolder;
            verschluesselung = HKDF.DeriveKey(HashAlgorithmName.SHA256, tokens.SigningKey.Key, 32, info: Encoding.UTF8.GetBytes("quizzer-ki-schluessel-v1"));
            this.zeit = zeit;
        }

        public string Ordner { get; }

        private string EinstellungenPfad => Path.Combine(Ordner, "einstellungen.json");

        private string NutzungPfad => Path.Combine(Ordner, "nutzung.json");

        /// <summary>Was der Dienst zum Arbeiten braucht - mit Klartextschlüssel, nie nach außen geben.</summary>
        public sealed record Konfiguration(KiAnbieter Anbieter, string? Adresse, string Modell, string? Schluessel, bool SchluesselUnlesbar, int Tageslimit, bool LinksAbrufen, IReadOnlySet<Guid> Freigegeben);

        public async Task<Konfiguration> LadeAsync(CancellationToken ct)
        {
            await gate.WaitAsync(ct);
            try
            {
                var g = await LiesAsync<Gespeichert>(EinstellungenPfad, ct) ?? new Gespeichert();
                var schluessel = Entschluessele(g.Schluessel, out var unlesbar);
                return new Konfiguration(g.Anbieter, g.Adresse, g.Modell, schluessel, unlesbar, g.Tageslimit, g.LinksAbrufen, g.FreigegebeneKonten.ToHashSet());
            }
            finally
            {
                gate.Release();
            }
        }

        /// <summary>Für die Maske des Admins - ohne Schlüssel.</summary>
        public async Task<KiEinstellungenDto> FuerAdminAsync(CancellationToken ct)
        {
            var k = await LadeAsync(ct);
            return new KiEinstellungenDto
            {
                Anbieter = k.Anbieter,
                Adresse = k.Adresse,
                Modell = k.Modell,
                SchluesselGesetzt = k.Schluessel != null,
                Tageslimit = k.Tageslimit,
                LinksAbrufen = k.LinksAbrufen,
                FreigegebeneKonten = k.Freigegeben.ToList(),
            };
        }

        /// <summary>Übernimmt die Maske des Admins. Gibt einen Fehlertext zurück oder <c>null</c>.</summary>
        public async Task<string?> SpeichereAsync(KiEinstellungenDto neu, CancellationToken ct)
        {
            if (neu.Anbieter == KiAnbieter.OpenAiKompatibel
                && !(Uri.TryCreate(neu.Adresse, UriKind.Absolute, out var adresse) && adresse.Scheme is "http" or "https"))
                return "Für eine OpenAI-kompatible Schnittstelle braucht es eine Adresse mit http:// oder https://, etwa http://ollama:11434/v1.";

            if (neu.Anbieter != KiAnbieter.Aus && string.IsNullOrWhiteSpace(neu.Modell))
                return "Bitte ein Modell angeben, etwa claude-opus-5-5 oder llama3.1.";

            if (neu.Tageslimit is < 0 or > 10_000)
                return "Das Tageslimit muss zwischen 0 und 10000 liegen.";

            await gate.WaitAsync(ct);
            try
            {
                var g = await LiesAsync<Gespeichert>(EinstellungenPfad, ct) ?? new Gespeichert();

                g.Anbieter = neu.Anbieter;
                g.Adresse = string.IsNullOrWhiteSpace(neu.Adresse) ? null : neu.Adresse.Trim();
                g.Modell = (neu.Modell ?? string.Empty).Trim();
                g.Tageslimit = neu.Tageslimit;
                g.LinksAbrufen = neu.LinksAbrufen;
                g.FreigegebeneKonten = neu.FreigegebeneKonten.Distinct().ToList();

                if (neu.NeuerSchluessel != null)
                    g.Schluessel = neu.NeuerSchluessel.Length == 0 ? null : Verschluessele(neu.NeuerSchluessel.Trim());

                await SchreibAsync(EinstellungenPfad, g, ct);
                return null;
            }
            finally
            {
                gate.Release();
            }
        }

        /// <summary>
        /// Zählt einen Aufruf, wenn das Limit es erlaubt. Liefert, wie viele heute noch gehen
        /// (nach dem Zählen) - oder <c>null</c>, wenn das Limit schon erreicht war.
        /// </summary>
        public async Task<int?> ZaehleAsync(Guid konto, int limit, CancellationToken ct)
        {
            await gate.WaitAsync(ct);
            try
            {
                var n = await HeuteAsync(ct);
                var bisher = n.Zaehler.GetValueOrDefault(konto);

                if (bisher >= limit)
                    return null;

                n.Zaehler[konto] = bisher + 1;
                await SchreibAsync(NutzungPfad, n, ct);
                return limit - bisher - 1;
            }
            finally
            {
                gate.Release();
            }
        }

        /// <summary>Gibt einen gezählten Aufruf zurück - die KI war nicht erreichbar, es kam nichts.</summary>
        public async Task ErstatteAsync(Guid konto, CancellationToken ct)
        {
            await gate.WaitAsync(ct);
            try
            {
                var n = await HeuteAsync(ct);
                if (n.Zaehler.GetValueOrDefault(konto) > 0)
                {
                    n.Zaehler[konto]--;
                    await SchreibAsync(NutzungPfad, n, ct);
                }
            }
            finally
            {
                gate.Release();
            }
        }

        public async Task<int> VerbrauchtAsync(Guid konto, CancellationToken ct)
        {
            await gate.WaitAsync(ct);
            try
            {
                return (await HeuteAsync(ct)).Zaehler.GetValueOrDefault(konto);
            }
            finally
            {
                gate.Release();
            }
        }

        private async Task<Nutzung> HeuteAsync(CancellationToken ct)
        {
            var tag = zeit.GetLocalNow().ToString("yyyy-MM-dd");
            var n = await LiesAsync<Nutzung>(NutzungPfad, ct);
            return n != null && n.Tag == tag ? n : new Nutzung { Tag = tag };
        }

        private string Verschluessele(string klartext)
        {
            var nonce = RandomNumberGenerator.GetBytes(12);
            var daten = Encoding.UTF8.GetBytes(klartext);
            var geheim = new byte[daten.Length];
            var tag = new byte[16];

            using (var aes = new AesGcm(verschluesselung, 16))
                aes.Encrypt(nonce, daten, geheim, tag);

            return Convert.ToBase64String([.. nonce, .. tag, .. geheim]);
        }

        private string? Entschluessele(string? gespeichert, out bool unlesbar)
        {
            unlesbar = false;

            if (string.IsNullOrEmpty(gespeichert))
                return null;

            try
            {
                var alles = Convert.FromBase64String(gespeichert);
                var klartext = new byte[alles.Length - 28];

                using var aes = new AesGcm(verschluesselung, 16);
                aes.Decrypt(alles.AsSpan(0, 12), alles.AsSpan(28), alles.AsSpan(12, 16), klartext);

                return Encoding.UTF8.GetString(klartext);
            }
            catch (Exception e) when (e is CryptographicException or FormatException or ArgumentException)
            {
                // Signierschlüssel gewechselt oder Datei beschädigt - nicht raten, melden.
                unlesbar = true;
                return null;
            }
        }

        private static async Task<T?> LiesAsync<T>(string pfad, CancellationToken ct) where T : class
        {
            if (!File.Exists(pfad))
                return null;

            try
            {
                await using var datei = File.OpenRead(pfad);
                return await JsonSerializer.DeserializeAsync<T>(datei, Json, ct);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private async Task SchreibAsync<T>(string pfad, T wert, CancellationToken ct)
        {
            Directory.CreateDirectory(Ordner);
            var temp = pfad + ".tmp";

            await using (var datei = File.Create(temp))
                await JsonSerializer.SerializeAsync(datei, wert, Json, ct);

            File.Move(temp, pfad, overwrite: true);

            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(pfad, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }
}
