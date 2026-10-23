using Microsoft.Extensions.DependencyInjection;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Transfer.Ki;
using Quizzer.Server.Ki;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Quizzer.Server.Tests
{
    /// <summary>
    /// KI-Entwürfe (M31, 2026-10-23) - ohne echte KI: eine Attrappe statt des Modells. Geprüft
    /// wird, was der Server verantwortet: Freigabe, Tageslimit, Erstattung, und dass der
    /// API-Schlüssel nie zurückkommt und nicht im Klartext liegt.
    /// </summary>
    [TestClass]
    public sealed class KiTests
    {
        private sealed class Attrappe : IKiModellFabrik, IKiModell
        {
            public Func<KiAuftrag, string> Antwort { get; set; } = _ => Entwurf(QuestionType.MultipleChoice);

            public List<KiAuftrag> Auftraege { get; } = new();

            public IKiModell Erzeuge(KiSpeicher.Konfiguration k) => this;

            public Task<string> ErzeugeAsync(KiAuftrag auftrag, CancellationToken ct)
            {
                Auftraege.Add(auftrag);
                return Task.FromResult(Antwort(auftrag));
            }
        }

        private static string Entwurf(QuestionType typ, int anzahl = 1) => JsonSerializer.Serialize(new KiAuftragsbau.Antwort
        {
            Entwuerfe = Enumerable.Range(0, anzahl).Select(i => new KiFrageEntwurf
            {
                Typ = typ,
                Fragetext = $"Welche Stadt ist die Hauptstadt von Frankreich? ({i})",
                Zeilen = [new KiZeile { Text = "Paris", Richtig = true }, new KiZeile { Text = "Lyon" }],
                BildIndex = 7,
            }).ToList(),
        }, KiAuftragsbau.Json);

        private static (Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> Factory, Attrappe Ki) Server(string? kiOrdner = null, string? signierschluessel = null)
        {
            var ki = new Attrappe();
            var extra = new Dictionary<string, string?>
            {
                ["Quizzer:KiFolder"] = kiOrdner ?? Path.Combine(ServerTestHost.MediaRoot, "ki-" + Guid.NewGuid().ToString("N")),
            };

            if (signierschluessel != null)
                extra["Quizzer:Jwt:SigningKey"] = signierschluessel;

            return (ServerTestHost.CreateFactory(extra: extra, services: s => s.AddSingleton<IKiModellFabrik>(ki)), ki);
        }

        private static async Task<HttpClient> AdminAsync(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory)
            => ServerTestHost.Authorized(factory, await ServerTestHost.LoginAsync(factory.CreateClient(), ServerTestHost.AdminUser, ServerTestHost.AdminPassword));

        private static KiEinstellungenDto Claude(int limit = 20, string? schluessel = "sk-ant-geheim-123", bool links = true, params Guid[] frei) => new()
        {
            Anbieter = KiAnbieter.Claude, Modell = "claude-opus-5-5", NeuerSchluessel = schluessel, Tageslimit = limit, LinksAbrufen = links, FreigegebeneKonten = [.. frei],
        };

        private static async Task SetzeAsync(HttpClient admin, KiEinstellungenDto e)
        {
            var antwort = await admin.PutAsJsonAsync("/api/v1/ki/einstellungen", e);
            Assert.AreEqual(HttpStatusCode.NoContent, antwort.StatusCode, await antwort.Content.ReadAsStringAsync());
        }

        private static Task<HttpResponseMessage> EntwurfAsync(HttpClient c, int anzahl = 1, QuestionType? typ = null)
            => c.PostAsJsonAsync("/api/v1/ki/entwurf", new KiEntwurfAnfrage { Thema = "Hauptstädte", Anzahl = anzahl, Typ = typ });

        [TestMethod]
        public async Task WithoutSetupNobodyGetsDrafts()
        {
            var (factory, ki) = Server();
            await using var _ = factory;
            var admin = await AdminAsync(factory);

            var status = (await admin.GetFromJsonAsync<KiStatusDto>("/api/v1/ki/status"))!;
            Assert.IsFalse(status.Verfuegbar);
            StringAssert.Contains(status.Grund, "keine KI");

            Assert.AreEqual(HttpStatusCode.Forbidden, (await EntwurfAsync(admin)).StatusCode);
            Assert.AreEqual(0, ki.Auftraege.Count);
        }

        [TestMethod]
        public async Task OnlyTheAdminSeesAndChangesTheSettings()
        {
            var (factory, _) = Server();
            await using var __ = factory;
            var (mitglied, _) = await ServerTestHost.NewMemberAsync(factory, "Mitglied");

            Assert.AreEqual(HttpStatusCode.Forbidden, (await mitglied.GetAsync("/api/v1/ki/einstellungen")).StatusCode);
            Assert.AreEqual(HttpStatusCode.Forbidden, (await mitglied.PutAsJsonAsync("/api/v1/ki/einstellungen", Claude())).StatusCode);
            Assert.AreEqual(HttpStatusCode.Forbidden, (await mitglied.PostAsync("/api/v1/ki/einstellungen/probe", null)).StatusCode);
        }

        [TestMethod]
        public async Task TheKeyNeverComesBackAndIsNotStoredInPlainText()
        {
            var ordner = Path.Combine(ServerTestHost.MediaRoot, "ki-" + Guid.NewGuid().ToString("N"));
            var (factory, _) = Server(ordner);
            await using var _ = factory;
            var admin = await AdminAsync(factory);

            await SetzeAsync(admin, Claude());

            var roh = await admin.GetStringAsync("/api/v1/ki/einstellungen");
            Assert.IsFalse(roh.Contains("geheim"), "Der Schlüssel kam zurück: " + roh);

            var gelesen = JsonSerializer.Deserialize<KiEinstellungenDto>(roh, KiAuftragsbau.Json)!;
            Assert.IsTrue(gelesen.SchluesselGesetzt);
            Assert.AreEqual("claude-opus-5-5", gelesen.Modell);

            foreach (var datei in Directory.GetFiles(ordner))
                Assert.IsFalse(File.ReadAllText(datei).Contains("geheim"), $"{Path.GetFileName(datei)} enthält den Schlüssel im Klartext.");

            // Ohne neuen Schlüssel bleibt der alte; leer löscht ihn.
            await SetzeAsync(admin, new KiEinstellungenDto { Anbieter = KiAnbieter.Claude, Modell = "claude-opus-5-5" });
            Assert.IsTrue((await admin.GetFromJsonAsync<KiEinstellungenDto>("/api/v1/ki/einstellungen", KiAuftragsbau.Json))!.SchluesselGesetzt);

            await SetzeAsync(admin, new KiEinstellungenDto { Anbieter = KiAnbieter.Claude, Modell = "claude-opus-5-5", NeuerSchluessel = "" });
            Assert.IsFalse((await admin.GetFromJsonAsync<KiEinstellungenDto>("/api/v1/ki/einstellungen", KiAuftragsbau.Json))!.SchluesselGesetzt);
        }

        [TestMethod]
        public async Task InvalidSettingsAreRefused()
        {
            var (factory, _) = Server();
            await using var _ = factory;
            var admin = await AdminAsync(factory);

            var ohneAdresse = await admin.PutAsJsonAsync("/api/v1/ki/einstellungen", new KiEinstellungenDto { Anbieter = KiAnbieter.OpenAiKompatibel, Modell = "llama3.1", Adresse = "file:///etc" });
            Assert.AreEqual(HttpStatusCode.BadRequest, ohneAdresse.StatusCode);

            var ohneModell = await admin.PutAsJsonAsync("/api/v1/ki/einstellungen", new KiEinstellungenDto { Anbieter = KiAnbieter.Claude });
            Assert.AreEqual(HttpStatusCode.BadRequest, ohneModell.StatusCode);
        }

        [TestMethod]
        public async Task AMemberNeedsToBeEnabledAndTheDailyLimitHolds()
        {
            var (factory, ki) = Server();
            await using var _ = factory;
            var admin = await AdminAsync(factory);
            var (mitglied, tokens) = await ServerTestHost.NewMemberAsync(factory, "Mitglied");

            await SetzeAsync(admin, Claude(limit: 2));

            var gesperrt = await EntwurfAsync(mitglied);
            Assert.AreEqual(HttpStatusCode.Forbidden, gesperrt.StatusCode);
            Assert.IsFalse((await mitglied.GetFromJsonAsync<KiStatusDto>("/api/v1/ki/status"))!.Verfuegbar);

            await SetzeAsync(admin, Claude(2, null, true, tokens.Account.AccountId));

            var erste = await EntwurfAsync(mitglied, anzahl: 1);
            Assert.AreEqual(HttpStatusCode.OK, erste.StatusCode, await erste.Content.ReadAsStringAsync());
            var antwort = (await erste.Content.ReadFromJsonAsync<KiEntwurfAntwort>(KiAuftragsbau.Json))!;
            Assert.AreEqual(1, antwort.Entwuerfe.Count);
            Assert.AreEqual(1, antwort.VerbleibendHeute);
            Assert.IsNull(antwort.Entwuerfe[0].BildIndex, "Ein Verweis auf ein nicht mitgeschicktes Bild blieb stehen.");

            Assert.AreEqual(HttpStatusCode.OK, (await EntwurfAsync(mitglied)).StatusCode);
            Assert.AreEqual(HttpStatusCode.TooManyRequests, (await EntwurfAsync(mitglied)).StatusCode);
            Assert.AreEqual(2, ki.Auftraege.Count, "Nach dem Limit wurde die KI trotzdem gefragt.");

            // Das Limit gilt je Konto: der Admin hat seine eigenen zwei.
            Assert.AreEqual(HttpStatusCode.OK, (await EntwurfAsync(admin)).StatusCode);
        }

        [TestMethod]
        public async Task AFailedCallIsRefundedAndTheCountIsCut()
        {
            var (factory, ki) = Server();
            await using var _ = factory;
            var admin = await AdminAsync(factory);
            await SetzeAsync(admin, Claude(limit: 1));

            ki.Antwort = _ => throw new KiFehler("Die KI ist überlastet.");
            var fehler = await EntwurfAsync(admin);
            Assert.AreEqual(HttpStatusCode.BadGateway, fehler.StatusCode);
            StringAssert.Contains(await fehler.Content.ReadAsStringAsync(), "überlastet");

            ki.Antwort = _ => Entwurf(QuestionType.Default, anzahl: 4);
            var gut = await EntwurfAsync(admin, anzahl: 2, typ: QuestionType.MultipleChoice);
            Assert.AreEqual(HttpStatusCode.OK, gut.StatusCode, "Der gescheiterte Aufruf wurde nicht erstattet.");

            var antwort = (await gut.Content.ReadFromJsonAsync<KiEntwurfAntwort>(KiAuftragsbau.Json))!;
            Assert.AreEqual(2, antwort.Entwuerfe.Count, "Mehr Entwürfe als angefordert.");
            Assert.IsTrue(antwort.Hinweise.Any(h => h.Contains("anderen Fragetyp")), string.Join("; ", antwort.Hinweise));
        }

        [TestMethod]
        public async Task InvalidRequestsAreRefusedBeforeTheModelIsAsked()
        {
            var (factory, ki) = Server();
            await using var _ = factory;
            var admin = await AdminAsync(factory);
            await SetzeAsync(admin, Claude());

            async Task<HttpStatusCode> Sende(KiEntwurfAnfrage a) => (await admin.PostAsJsonAsync("/api/v1/ki/entwurf", a)).StatusCode;

            Assert.AreEqual(HttpStatusCode.BadRequest, await Sende(new KiEntwurfAnfrage()));
            Assert.AreEqual(HttpStatusCode.BadRequest, await Sende(new KiEntwurfAnfrage { Thema = "x", Anzahl = 9 }));
            Assert.AreEqual(HttpStatusCode.BadRequest, await Sende(new KiEntwurfAnfrage { Thema = "x", Bilder = [new KiBild { Name = "a.svg", MediaType = "image/svg+xml", Base64 = "AAAA" }] }));
            Assert.AreEqual(HttpStatusCode.BadRequest, await Sende(new KiEntwurfAnfrage { Thema = "x", Bilder = [new KiBild { Name = "a.png", MediaType = "image/png", Base64 = "kein base64!" }] }));
            Assert.AreEqual(0, ki.Auftraege.Count);
        }

        [TestMethod]
        public async Task LinksAreNotFetchedWhenTheAdminSwitchedItOff()
        {
            var (factory, ki) = Server();
            await using var _ = factory;
            var admin = await AdminAsync(factory);
            await SetzeAsync(admin, Claude(links: false));

            var antwort = await admin.PostAsJsonAsync("/api/v1/ki/entwurf", new KiEntwurfAnfrage { Thema = "x", Links = ["http://127.0.0.1:1/geheim"] });
            var gelesen = (await antwort.Content.ReadFromJsonAsync<KiEntwurfAntwort>(KiAuftragsbau.Json))!;

            Assert.IsTrue(gelesen.Hinweise.Any(h => h.Contains("nicht abgerufen")));
            Assert.IsFalse(ki.Auftraege[0].Nutzer.Contains("<quelle"), "Ein Link wurde trotzdem eingebunden.");
        }

        [TestMethod]
        public async Task AChangedSigningKeyMakesTheStoredKeyUnreadable()
        {
            var ordner = Path.Combine(ServerTestHost.MediaRoot, "ki-" + Guid.NewGuid().ToString("N"));

            var (erster, _) = Server(ordner);
            await using (erster)
                await SetzeAsync(await AdminAsync(erster), Claude());

            var (zweiter, ki) = Server(ordner, Convert.ToBase64String(Enumerable.Range(0, 32).Select(i => (byte)(255 - i)).ToArray()));
            await using var _ = zweiter;
            var admin = await AdminAsync(zweiter);

            var status = (await admin.GetFromJsonAsync<KiStatusDto>("/api/v1/ki/status"))!;
            Assert.IsFalse(status.Verfuegbar);
            StringAssert.Contains(status.Grund, "nicht mehr lesbar");
            Assert.AreEqual(HttpStatusCode.Forbidden, (await EntwurfAsync(admin)).StatusCode);
            Assert.AreEqual(0, ki.Auftraege.Count);
        }

        /// <summary>Der Weg des WPF: derselbe Client wie im Programm, Hin- und Rückweg der Enums.</summary>
        [TestMethod]
        public async Task TheClientRoundTripsSettingsAndDrafts()
        {
            var (factory, ki) = Server();
            await using var _ = factory;
            ki.Antwort = _ => Entwurf(QuestionType.Appreciate);

            using var api = new Quizzer.Client.QuizzerApiClient(new Uri("https://localhost/api/v1/"), new Quizzer.Client.MemoryTokenStore(), handler: factory.Server.CreateHandler());
            await api.LoginAsync(ServerTestHost.AdminUser, ServerTestHost.AdminPassword);

            await api.PutKiEinstellungenAsync(new KiEinstellungenDto { Anbieter = KiAnbieter.OpenAiKompatibel, Adresse = "http://ollama:11434/v1", Modell = "llama3.1", Tageslimit = 5 });
            var gelesen = await api.GetKiEinstellungenAsync();
            Assert.AreEqual(KiAnbieter.OpenAiKompatibel, gelesen.Anbieter);
            Assert.IsFalse(gelesen.SchluesselGesetzt);

            Assert.IsTrue((await api.GetKiStatusAsync()).Verfuegbar, "Ohne Schlüssel geht eine lokale KI.");

            var antwort = await api.PostKiEntwurfAsync(new KiEntwurfAnfrage { Thema = "Mondlandung", Typ = QuestionType.Appreciate, Schwierigkeit = Difficulty.Level2 });
            Assert.AreEqual(QuestionType.Appreciate, antwort.Entwuerfe.Single().Typ);
            Assert.AreEqual(4, antwort.VerbleibendHeute);
            StringAssert.Contains(ki.Auftraege.Single().Nutzer, "Mondlandung");

            await Assert.ThrowsExactlyAsync<Quizzer.Client.ApiException>(() => api.PostKiEntwurfAsync(new KiEntwurfAnfrage()));
        }

        // ---- Links: der Schutz vor Abrufen ins eigene Netz ----

        [TestMethod]
        [DataRow("127.0.0.1", false)]
        [DataRow("10.1.2.3", false)]
        [DataRow("172.20.0.5", false)]
        [DataRow("192.168.1.10", false)]
        [DataRow("169.254.169.254", false)]
        [DataRow("100.100.1.1", false)]
        [DataRow("0.0.0.0", false)]
        [DataRow("::1", false)]
        [DataRow("fe80::1", false)]
        [DataRow("fd12:3456::1", false)]
        [DataRow("::ffff:192.168.1.1", false)]
        [DataRow("93.184.215.14", true)]
        [DataRow("2606:4700::1111", true)]
        public void OnlyPublicAddressesAreAllowed(string adresse, bool erwartet)
            => Assert.AreEqual(erwartet, LinkAbruf.IstOeffentlich(IPAddress.Parse(adresse)), adresse);

        [TestMethod]
        public async Task ALocalServerIsBlockedByDefault()
        {
            using var web = new LokalerServer("<html><head><script>alert(1)</script><style>p{}</style></head><body><h1>Titel</h1><p>Der Inhalt &amp; mehr.</p></body></html>", "text/html");

            using (var standard = new LinkAbruf())
            {
                var blockiert = await standard.HoleAsync(web.Adresse, CancellationToken.None);
                Assert.IsNull(blockiert.Text);
                Assert.IsNotNull(blockiert.Fehler);
            }

            using var offen = new LinkAbruf(_ => true);
            var gelesen = await offen.HoleAsync(web.Adresse, CancellationToken.None);
            Assert.IsNull(gelesen.Fehler, gelesen.Fehler);
            StringAssert.Contains(gelesen.Text, "Der Inhalt & mehr.");
            Assert.IsFalse(gelesen.Text!.Contains("alert"), "Skript blieb im Text.");
        }

        [TestMethod]
        public async Task OnlyHttpLinksAndTextContentAreRead()
        {
            using var offen = new LinkAbruf(_ => true);
            Assert.IsNotNull((await offen.HoleAsync("file:///etc/passwd", CancellationToken.None)).Fehler);
            Assert.IsNotNull((await offen.HoleAsync("ftp://example.org/x", CancellationToken.None)).Fehler);

            using var bild = new LokalerServer("PNG...", "image/png");
            Assert.IsNotNull((await offen.HoleAsync(bild.Adresse, CancellationToken.None)).Fehler);
        }

        // ---- OpenAI-kompatibel: Rückfall, wenn json_schema fehlt ----

        [TestMethod]
        public async Task OpenAiCompatibleFallsBackToJsonObject()
        {
            var bodies = new List<string>();
            var handler = new Antworter(async anfrage =>
            {
                var body = await anfrage.Content!.ReadAsStringAsync();
                bodies.Add(body);

                if (body.Contains("json_schema"))
                    return new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("{\"error\":\"response_format json_schema not supported\"}") };

                var inhalt = JsonSerializer.Serialize(new { choices = new[] { new { finish_reason = "stop", message = new { content = "{\"entwuerfe\":[]}" } } } });
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(inhalt, Encoding.UTF8, "application/json") };
            });

            var modell = new OpenAiModell(new HttpClient(handler), "http://ollama:11434/v1/", "llama3.1", schluessel: null);
            var schema = KiAuftragsbau.Schema();
            var text = await modell.ErzeugeAsync(new KiAuftrag("System", "Nutzer", [new KiBild { Name = "a.png", MediaType = "image/png", Base64 = "AAAA" }], schema), CancellationToken.None);

            Assert.AreEqual("{\"entwuerfe\":[]}", text);
            Assert.AreEqual(2, bodies.Count);
            StringAssert.Contains(bodies[1], "json_object");
            StringAssert.Contains(bodies[1], "data:image/png;base64,AAAA");
            Assert.IsNull(handler.Letzte!.Headers.Authorization, "Ohne Schlüssel darf kein Bearer-Kopf mitgehen.");
            Assert.AreEqual("http://ollama:11434/v1/chat/completions", handler.Letzte.RequestUri!.ToString());
        }

        [TestMethod]
        public async Task OpenAiCompatibleReportsACutOffAnswer()
        {
            var handler = new Antworter(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"choices\":[{\"finish_reason\":\"length\",\"message\":{\"content\":\"{\"}}]}", Encoding.UTF8, "application/json"),
            }));

            var modell = new OpenAiModell(new HttpClient(handler), "https://api.openai.com/v1", "gpt", "sk-x");
            var fehler = await Assert.ThrowsExactlyAsync<KiFehler>(() => modell.ErzeugeAsync(new KiAuftrag("S", "N", [], KiAuftragsbau.Schema()), CancellationToken.None));

            Assert.IsFalse(fehler.Erstatten, "Eine abgeschnittene Antwort hat Rechenzeit gekostet - sie zählt.");
            Assert.AreEqual("Bearer", handler.Letzte!.Headers.Authorization!.Scheme);
        }

        private sealed class Antworter(Func<HttpRequestMessage, Task<HttpResponseMessage>> antwort) : HttpMessageHandler
        {
            public HttpRequestMessage? Letzte { get; private set; }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            {
                Letzte = request;
                return antwort(request);
            }
        }

        /// <summary>Ein echter Webserver auf 127.0.0.1 - der Schutz greift beim Verbinden, nicht beim Namen.</summary>
        private sealed class LokalerServer : IDisposable
        {
            private readonly HttpListener listener = new();

            public string Adresse { get; }

            public LokalerServer(string inhalt, string typ)
            {
                var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
                probe.Start();
                var port = ((IPEndPoint)probe.LocalEndpoint).Port;
                probe.Stop();

                Adresse = $"http://127.0.0.1:{port}/";
                listener.Prefixes.Add(Adresse);
                listener.Start();

                _ = Task.Run(async () =>
                {
                    while (listener.IsListening)
                    {
                        try
                        {
                            var ctx = await listener.GetContextAsync();
                            var bytes = Encoding.UTF8.GetBytes(inhalt);
                            ctx.Response.ContentType = typ + "; charset=utf-8";
                            await ctx.Response.OutputStream.WriteAsync(bytes);
                            ctx.Response.Close();
                        }
                        catch (Exception) when (!listener.IsListening)
                        {
                            return;
                        }
                    }
                });
            }

            public void Dispose() => listener.Close();
        }
    }
}
