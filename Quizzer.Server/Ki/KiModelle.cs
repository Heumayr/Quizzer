using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;
using Quizzer.DataModels.Transfer.Ki;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Quizzer.Server.Ki
{
    /// <summary>Ein Modell, das zu einem Auftrag JSON im Schema liefert.</summary>
    public interface IKiModell
    {
        Task<string> ErzeugeAsync(KiAuftrag auftrag, CancellationToken ct);
    }

    /// <summary>Baut das Modell aus den Einstellungen. Tests ersetzen sie durch eine Attrappe.</summary>
    public interface IKiModellFabrik
    {
        IKiModell Erzeuge(KiSpeicher.Konfiguration konfiguration);
    }

    public sealed class KiModellFabrik(IHttpClientFactory http) : IKiModellFabrik
    {
        public IKiModell Erzeuge(KiSpeicher.Konfiguration k) => k.Anbieter switch
        {
            KiAnbieter.Claude => new ClaudeModell(k.Schluessel ?? throw new KiFehler("Für Claude ist kein API-Schlüssel hinterlegt."), k.Modell, k.Adresse),
            KiAnbieter.OpenAiKompatibel => new OpenAiModell(http.CreateClient(nameof(OpenAiModell)), k.Adresse!, k.Modell, k.Schluessel),
            _ => throw new KiFehler("Es ist keine KI eingerichtet."),
        };
    }

    /// <summary>
    /// Claude über das offizielle Anthropic-SDK. Strukturierte Ausgabe
    /// (<c>output_config.format</c>) statt Bitte um JSON - die Antwort passt dann zum Schema.
    /// Denken bleibt an (Vorgabe des Modells); Bilder gehen als Base64-Bildblöcke mit.
    /// </summary>
    public sealed class ClaudeModell(string schluessel, string modell, string? adresse) : IKiModell
    {
        public async Task<string> ErzeugeAsync(KiAuftrag auftrag, CancellationToken ct)
        {
            var client = string.IsNullOrWhiteSpace(adresse)
                ? new AnthropicClient { ApiKey = schluessel }
                : new AnthropicClient { ApiKey = schluessel, BaseUrl = adresse };

            var inhalt = new List<ContentBlockParam>();

            foreach (var bild in auftrag.Bilder)
            {
                inhalt.Add(new ImageBlockParam
                {
                    Source = new Base64ImageSource { Data = bild.Base64, MediaType = bild.MediaType },
                });
            }

            inhalt.Add(new TextBlockParam { Text = auftrag.Nutzer });

            Message antwort;
            try
            {
                antwort = await client.Messages.Create(new MessageCreateParams
                {
                    Model = modell,
                    MaxTokens = 16000,
                    System = new List<TextBlockParam> { new() { Text = auftrag.System, CacheControl = new CacheControlEphemeral() } },
                    OutputConfig = new OutputConfig
                    {
                        Format = new JsonOutputFormat
                        {
                            Schema = auftrag.Schema.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone()),
                        },
                    },
                    Messages = [new UserMessageParam { Content = inhalt }],
                }, ct);
            }
            catch (AnthropicRateLimitException)
            {
                throw new KiFehler("Claude ist gerade ausgelastet (Ratenlimit). Bitte gleich noch einmal versuchen.");
            }
            catch (AnthropicApiException e)
            {
                throw new KiFehler($"Claude hat die Anfrage abgewiesen: {e.Message}");
            }
            catch (HttpRequestException e)
            {
                throw new KiFehler($"Claude ist nicht erreichbar: {e.Message}");
            }

            if (antwort.StopReason == "refusal")
                throw new KiFehler("Claude hat diesen Entwurf abgelehnt. Thema oder Material anders formulieren.", erstatten: false);

            if (antwort.StopReason == "max_tokens")
                throw new KiFehler("Die Antwort wurde zu lang und abgeschnitten. Bitte weniger Entwürfe auf einmal anfordern.", erstatten: false);

            return string.Concat(antwort.Content.Select(b => b.Value).OfType<TextBlock>().Select(t => t.Text));
        }
    }

    /// <summary>
    /// Jede OpenAI-kompatible Schnittstelle (<c>/chat/completions</c>): OpenAI, Ollama, LM Studio,
    /// vLLM. Fordert <c>json_schema</c> an; kennt ein lokaler Server das nicht, einmal mit
    /// <c>json_object</c> und dem Schema im Text.
    /// </summary>
    public sealed class OpenAiModell(HttpClient http, string adresse, string modell, string? schluessel) : IKiModell
    {
        public async Task<string> ErzeugeAsync(KiAuftrag auftrag, CancellationToken ct)
        {
            var antwort = await SendeAsync(auftrag, streng: true, ct);

            // Ein Server ohne json_schema antwortet mit 400 - dann der einfachere Weg.
            if (antwort.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                antwort.Dispose();
                antwort = await SendeAsync(auftrag, streng: false, ct);
            }

            using (antwort)
            {
                if (!antwort.IsSuccessStatusCode)
                {
                    var text = await antwort.Content.ReadAsStringAsync(ct);
                    throw new KiFehler($"Die KI unter {Basis()} antwortet mit {(int)antwort.StatusCode}: {Kurz(text)}");
                }

                var json = await antwort.Content.ReadFromJsonAsync<JsonElement>(ct);

                try
                {
                    var wahl = json.GetProperty("choices")[0];

                    if (wahl.TryGetProperty("finish_reason", out var grund) && grund.GetString() == "length")
                        throw new KiFehler("Die Antwort wurde zu lang und abgeschnitten. Bitte weniger Entwürfe auf einmal anfordern.", erstatten: false);

                    return wahl.GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
                }
                catch (Exception e) when (e is KeyNotFoundException or InvalidOperationException or IndexOutOfRangeException)
                {
                    throw new KiFehler("Die Antwort der KI hat nicht das erwartete Format.", erstatten: false);
                }
            }
        }

        private async Task<HttpResponseMessage> SendeAsync(KiAuftrag auftrag, bool streng, CancellationToken ct)
        {
            var inhalt = new JsonArray { new JsonObject { ["type"] = "text", ["text"] = auftrag.Nutzer } };

            foreach (var bild in auftrag.Bilder)
            {
                inhalt.Add(new JsonObject
                {
                    ["type"] = "image_url",
                    ["image_url"] = new JsonObject { ["url"] = $"data:{bild.MediaType};base64,{bild.Base64}" },
                });
            }

            var system = streng
                ? auftrag.System
                : auftrag.System + "\n\nAntworte ausschließlich mit JSON nach diesem Schema:\n" + auftrag.Schema.GetRawText();

            var anfrage = new JsonObject
            {
                ["model"] = modell,
                ["messages"] = new JsonArray
                {
                    new JsonObject { ["role"] = "system", ["content"] = system },
                    new JsonObject { ["role"] = "user", ["content"] = inhalt },
                },
                ["response_format"] = streng
                    ? new JsonObject
                    {
                        ["type"] = "json_schema",
                        ["json_schema"] = new JsonObject
                        {
                            ["name"] = "fragenentwurf",
                            ["strict"] = true,
                            ["schema"] = JsonNode.Parse(auftrag.Schema.GetRawText()),
                        },
                    }
                    : new JsonObject { ["type"] = "json_object" },
            };

            using var nachricht = new HttpRequestMessage(HttpMethod.Post, Basis() + "/chat/completions")
            {
                Content = JsonContent.Create(anfrage),
            };

            if (!string.IsNullOrWhiteSpace(schluessel))
                nachricht.Headers.Authorization = new AuthenticationHeaderValue("Bearer", schluessel);

            try
            {
                return await http.SendAsync(nachricht, ct);
            }
            catch (HttpRequestException e)
            {
                throw new KiFehler($"Die KI unter {Basis()} ist nicht erreichbar: {e.Message}");
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                throw new KiFehler($"Die KI unter {Basis()} hat nicht rechtzeitig geantwortet.");
            }
        }

        private string Basis() => adresse.TrimEnd('/');

        private static string Kurz(string text) => text.Length <= 300 ? text : text[..300] + " …";
    }
}
