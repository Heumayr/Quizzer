using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Quizzer.Client;
using Quizzer.DataModels;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Questions;
using Quizzer.Logic.Context;
using Quizzer.Logic.Demo;
using Quizzer.Logic.Sync;
using Quizzer.Server.Auth;

namespace Quizzer.Server.Tests
{
    /// <summary>
    /// Rauchtest gegen einen <b>laufenden</b> Stapel (Caddy → API → SQL Server), nicht gegen den
    /// Server im Prozess: „am PC vorbereiten, am Laptop spielen" über die echte Leitung.
    /// <para>
    /// Läuft nur, wenn <c>QUIZZER_E2E_URL</c> gesetzt ist, sonst „nicht schlüssig". Dazu
    /// <c>QUIZZER_E2E_USER</c> (der Admin aus <c>.env</c>), <c>QUIZZER_E2E_PASSWORD</c> und - bei
    /// selbst erzeugtem Zertifikat - <c>QUIZZER_E2E_PIN</c> (SHA-256 des Zertifikats). Die
    /// beiden Client-Datenbanken legt er auf dem SQL Server aus <c>QUIZZER_SERVER_TEST_SQL</c> an.
    /// </para>
    /// <para>
    /// <b>Er schreibt in den Stapel:</b> ein Spiel „[Demo] Alle Fragetypen" samt Fragen und
    /// Flaggenbildern. Nicht gegen den Abend-Server laufen lassen, auf dem die Freunde spielen.
    /// </para>
    /// </summary>
    [TestClass]
    public sealed class StackSmokeTests
    {
        [TestMethod]
        public async Task TheTypeShowTravelsFromPcToLaptopThroughTheRealStack()
        {
            var url = Environment.GetEnvironmentVariable("QUIZZER_E2E_URL");

            if (string.IsNullOrWhiteSpace(url))
                Assert.Inconclusive("QUIZZER_E2E_URL ist nicht gesetzt - kein laufender Stapel angegeben.");

            var user = Environment.GetEnvironmentVariable("QUIZZER_E2E_USER")
                ?? throw new InvalidOperationException("QUIZZER_E2E_USER fehlt (QUIZZER_ADMIN_USER aus deploy/.env).");
            var password = Environment.GetEnvironmentVariable("QUIZZER_E2E_PASSWORD")
                ?? throw new InvalidOperationException("QUIZZER_E2E_PASSWORD fehlt.");
            var pin = Environment.GetEnvironmentVariable("QUIZZER_E2E_PIN");

            var basis = new Uri(url.TrimEnd('/') + "/api/v1/");

            var pc = await ClientAsync("E2EPC", basis, pin);
            var laptop = await ClientAsync("E2ELaptop", basis, pin);

            var totp = new StackTotp(Environment.GetEnvironmentVariable("QUIZZER_E2E_TOTP_FILE"));
            await totp.SignInAsync(pc.Api, user, password);
            await totp.SignInAsync(laptop.Api, user, password);

            await pc.Sync.PullAsync();

            // Die Typenschau auf dem "PC" anlegen - mit denselben Controllern wie im Programm.
            Settings.ConnectionString = pc.ConnectionString;
            Settings.FilePathQuizzer = pc.MediaRoot;

            var typenschau = await DemoDataSeeder.CreateTypenschauAsync();

            Assert.IsFalse(typenschau.OhneBilder, "Die Flaggen liessen sich lokal nicht schreiben.");

            var push = await pc.Sync.PushAsync();

            Assert.AreEqual(0, push.Rejected.Count,
                "Der Server hat Zeilen der Typenschau abgelehnt: "
                + string.Join("; ", push.Rejected.Select(r => $"{r.Type} {r.Reason} {r.Message}")));

            // Auf dem "Laptop" abholen und nachsehen.
            await laptop.Sync.PullAsync();

            await using var lokal = new DataContext(laptop.ConnectionString);

            var spiel = await lokal.Games
                .Include(g => g.GameGridCoordinates)
                .SingleAsync(g => g.Id == typenschau.Spiel.Id);

            var fragenIds = spiel.GameGridCoordinates.Select(z => z.QuestionBaseId!.Value).ToList();

            var fragen = await lokal.QuestionBases
                .Include(q => q.Steps)
                .Where(q => fragenIds.Contains(q.Id))
                .ToListAsync();

            CollectionAssert.AreEquivalent(
                QuestionTypeProfiles.All.Select(p => p.Typ).ToList(),
                fragen.Select(f => f.Typ).Distinct().ToList(),
                "Nicht jeder Fragetyp ist auf dem Laptop angekommen.");

            foreach (var frage in fragen.Where(f => f.Typ is QuestionType.Ordering or QuestionType.Matching))
                Assert.IsNotNull(ReihenfolgeEvaluator.Loesung(frage), $"{frage.Designation}: die Loesung kam nicht mit.");

            Assert.IsTrue(fragen.Where(f => f.Typ == QuestionType.Matching).SelectMany(f => f.Steps)
                    .All(s => !string.IsNullOrWhiteSpace(s.PartnerText)),
                "Die Begriffe der Zuordnung kamen nicht mit.");

            var bilder = fragen.SelectMany(f => f.Steps).Select(s => s.ResourceFileName)
                .Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().ToList();

            Assert.IsTrue(bilder.Count > 0, "Die Typenschau traegt keine Bilder - dann sagt die Probe nichts.");

            foreach (var bild in bilder)
                Assert.IsTrue(File.Exists(Path.Combine(laptop.MediaRoot, MediaPathsFolder, bild)),
                    $"Das Bild {bild} ist nicht auf dem Laptop angekommen.");
        }

        private const string MediaPathsFolder = DataModels.Transfer.Server.MediaPaths.ResourcesFolder;

        private sealed record Client(string ConnectionString, string MediaRoot, QuizzerApiClient Api, ClientSyncService Sync);

        private static async Task<Client> ClientAsync(string name, Uri basis, string? pin)
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

            var media = Path.Combine(Path.GetTempPath(), $"quizzer-e2e-{name}-{Guid.NewGuid():N}");
            Directory.CreateDirectory(media);

            var api = new QuizzerApiClient(basis, new MemoryTokenStore(), pin);
            var sync = new ClientSyncService(api, () => new SyncStore(builder.ConnectionString), new MemorySyncStateStore(), media);

            return new Client(builder.ConnectionString, media, api, sync);
        }

        /// <summary>
        /// Admin-Konten brauchen seit M36 A5 Zwei-Faktor. Mit QUIZZER_E2E_TOTP_FILE richtet der Rauchtest
        /// es beim ersten Lauf selbst ein und legt das Geheimnis dort ab - nur fuer den Pruefstapel.
        /// </summary>
        private sealed class StackTotp(string? datei)
        {
            // Zeile 1: Geheimnis (Base32), Zeile 2: zuletzt benutzter Zeitschritt - auch ueber Laeufe hinweg,
            // sonst traefe ein schneller zweiter Lauf denselben Schritt (gemessen 2026-11-16).
            private readonly string[] gespeichert = datei != null && File.Exists(datei) ? File.ReadAllLines(datei) : [];

            private string? GeheimnisText { get; set; }

            private long letzterSchritt = -1;

            public async Task SignInAsync(QuizzerApiClient api, string user, string password)
            {
                if (GeheimnisText == null && gespeichert.Length > 0)
                {
                    GeheimnisText = gespeichert[0];
                    letzterSchritt = gespeichert.Length > 1 ? long.Parse(gespeichert[1], System.Globalization.CultureInfo.InvariantCulture) : -1;
                }

                if (GeheimnisText != null)
                {
                    await api.LoginAsync(user, password, await NaechsterCodeAsync());
                    return;
                }

                await api.LoginAsync(user, password);
                if (datei == null)
                    return;

                GeheimnisText = (await api.BeginTotpSetupAsync()).Secret;
                await api.ConfirmTotpAsync(await NaechsterCodeAsync());
            }

            /// <summary>Derselbe Zeitschritt zweimal ist eine Wiederholung - der Server weist sie ab.</summary>
            private async Task<string> NaechsterCodeAsync()
            {
                while (Totp.StepAt(DateTimeOffset.UtcNow) <= letzterSchritt)
                    await Task.Delay(1000);

                letzterSchritt = Totp.StepAt(DateTimeOffset.UtcNow);
                if (datei != null)
                    File.WriteAllLines(datei, [GeheimnisText!, letzterSchritt.ToString(System.Globalization.CultureInfo.InvariantCulture)]);

                return Totp.Code(Totp.FromBase32(GeheimnisText!), letzterSchritt);
            }
        }
    }
}
