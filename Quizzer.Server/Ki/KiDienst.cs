using Quizzer.DataModels.Transfer.Ki;
using Quizzer.Server.Auth;

namespace Quizzer.Server.Ki
{
    /// <summary>
    /// Fragenentwürfe per KI (M31, 2026-10-23): prüft Freigabe und Tageslimit, holt die Links,
    /// fragt das Modell und gibt die Entwürfe zurück. <b>Gespeichert wird am Server nichts</b> -
    /// der Entwurf geht an den Editor des Anfragenden, und erst dort entsteht eine Frage.
    /// </summary>
    public sealed class KiDienst(KiSpeicher speicher, IKiModellFabrik fabrik, Func<LinkAbruf> linkAbruf, ILogger<KiDienst> log)
    {
        public sealed record Ergebnis(KiEntwurfAntwort? Antwort, string? Fehler, int Status);

        private static readonly string[] Bildtypen = ["image/png", "image/jpeg", "image/gif", "image/webp"];

        public async Task<KiStatusDto> StatusAsync(CurrentAccount konto, CancellationToken ct)
        {
            var k = await speicher.LadeAsync(ct);
            var (frei, grund) = Freigabe(k, konto);
            var verbleibend = Math.Max(0, k.Tageslimit - await speicher.VerbrauchtAsync(konto.AccountId, ct));

            if (frei && verbleibend == 0)
                (frei, grund) = (false, "Das Tageslimit für KI-Entwürfe ist erreicht.");

            var beschreibung = k.Anbieter == KiAnbieter.Aus ? string.Empty : $"{k.Anbieter}: {k.Modell}";
            return new KiStatusDto(frei, grund, verbleibend, k.LinksAbrufen, beschreibung);
        }

        private static (bool, string) Freigabe(KiSpeicher.Konfiguration k, CurrentAccount konto)
        {
            if (k.Anbieter == KiAnbieter.Aus)
                return (false, "Am Server ist keine KI eingerichtet (Admin: Einstellungen → KI).");

            if (k.SchluesselUnlesbar)
                return (false, "Der API-Schlüssel ist nicht mehr lesbar (Signierschlüssel des Servers gewechselt). Der Admin muss ihn neu eingeben.");

            if (k.Anbieter == KiAnbieter.Claude && k.Schluessel == null)
                return (false, "Für Claude ist kein API-Schlüssel hinterlegt.");

            if (!konto.IsAdmin && !k.Freigegeben.Contains(konto.AccountId))
                return (false, "Dein Konto ist für KI-Entwürfe nicht freigeschaltet. Der Admin kann das unter „Konten verwalten“ tun.");

            return (true, string.Empty);
        }

        public async Task<Ergebnis> EntwurfAsync(CurrentAccount konto, KiEntwurfAnfrage anfrage, CancellationToken ct)
        {
            if (Pruefe(anfrage) is { } ungueltig)
                return new Ergebnis(null, ungueltig, StatusCodes.Status400BadRequest);

            var k = await speicher.LadeAsync(ct);
            var (frei, grund) = Freigabe(k, konto);

            if (!frei)
                return new Ergebnis(null, grund, StatusCodes.Status403Forbidden);

            if (await speicher.ZaehleAsync(konto.AccountId, k.Tageslimit, ct) is not { } verbleibend)
                return new Ergebnis(null, "Das Tageslimit für KI-Entwürfe ist erreicht.", StatusCodes.Status429TooManyRequests);

            var hinweise = new List<string>();
            var quellen = new List<LinkAbruf.Ergebnis>();

            if (anfrage.Links.Count > 0)
            {
                if (!k.LinksAbrufen)
                {
                    hinweise.Add("Links werden auf diesem Server nicht abgerufen - Inhalt bitte als Text einfügen.");
                }
                else
                {
                    using var abruf = linkAbruf();
                    foreach (var link in anfrage.Links.Where(l => !string.IsNullOrWhiteSpace(l)))
                    {
                        var q = await abruf.HoleAsync(link, ct);
                        quellen.Add(q);

                        if (q.Fehler != null)
                            hinweise.Add($"{link}: nicht gelesen ({q.Fehler}).");
                    }
                }
            }

            var auftrag = new KiAuftrag(KiAuftragsbau.System, KiAuftragsbau.Nutzer(anfrage, quellen), anfrage.Bilder, KiAuftragsbau.Schema());

            try
            {
                var json = await fabrik.Erzeuge(k).ErzeugeAsync(auftrag, ct);
                var antwort = KiAuftragsbau.Lies(json);

                var entwuerfe = antwort.Entwuerfe.Take(anfrage.Anzahl).ToList();

                if (anfrage.Typ is { } typ && entwuerfe.Any(e => e.Typ != typ))
                    hinweise.Add($"Die KI hat teils einen anderen Fragetyp gewählt als {typ}.");

                // Ein Bildverweis auf ein Bild, das es nicht gibt, fiele erst im Editor auf.
                foreach (var e in entwuerfe.Where(e => e.BildIndex is { } i && (i < 0 || i >= anfrage.Bilder.Count)))
                    e.BildIndex = null;

                log.LogInformation("KI-Entwurf: Konto {Konto}, {Anzahl} Entwürfe, Anbieter {Anbieter}", konto.AccountId, entwuerfe.Count, k.Anbieter);

                return new Ergebnis(new KiEntwurfAntwort
                {
                    Entwuerfe = entwuerfe,
                    Hinweise = [.. hinweise, .. antwort.Hinweise.Where(h => !string.IsNullOrWhiteSpace(h)).Take(10)],
                    VerbleibendHeute = verbleibend,
                }, null, StatusCodes.Status200OK);
            }
            catch (KiFehler e)
            {
                if (e.Erstatten)
                    await speicher.ErstatteAsync(konto.AccountId, CancellationToken.None);

                log.LogWarning("KI-Entwurf gescheitert: {Grund}", e.Message);
                return new Ergebnis(null, e.Message, StatusCodes.Status502BadGateway);
            }
        }

        /// <summary>Für den Admin: ein kleiner Aufruf, ob Anbieter, Modell und Schlüssel passen.</summary>
        public async Task<string?> ProbeAsync(CancellationToken ct)
        {
            var k = await speicher.LadeAsync(ct);

            if (k.Anbieter == KiAnbieter.Aus)
                return "Es ist keine KI eingerichtet.";

            try
            {
                var anfrage = new KiEntwurfAnfrage { Thema = "Hauptstädte Europas", Anzahl = 1, Typ = DataModels.Enumerations.QuestionType.MultipleChoice };
                var json = await fabrik.Erzeuge(k).ErzeugeAsync(new KiAuftrag(KiAuftragsbau.System, KiAuftragsbau.Nutzer(anfrage, []), [], KiAuftragsbau.Schema()), ct);
                return KiAuftragsbau.Lies(json).Entwuerfe.Count > 0 ? null : "Die KI hat geantwortet, aber keinen Entwurf geliefert.";
            }
            catch (KiFehler e)
            {
                return e.Message;
            }
        }

        private static string? Pruefe(KiEntwurfAnfrage a)
        {
            if (string.IsNullOrWhiteSpace(a.Thema) && string.IsNullOrWhiteSpace(a.Text) && a.Links.Count == 0 && a.Bilder.Count == 0)
                return "Bitte ein Thema, Text, Links oder Bilder angeben.";

            if (a.Anzahl is < 1 or > KiEntwurfAnfrage.MaxAnzahl)
                return $"Es gehen 1 bis {KiEntwurfAnfrage.MaxAnzahl} Entwürfe auf einmal.";

            if (a.Links.Count > KiEntwurfAnfrage.MaxLinks)
                return $"Höchstens {KiEntwurfAnfrage.MaxLinks} Links.";

            if (a.Bilder.Count > KiEntwurfAnfrage.MaxBilder)
                return $"Höchstens {KiEntwurfAnfrage.MaxBilder} Bilder.";

            if ((a.Text?.Length ?? 0) > KiEntwurfAnfrage.MaxTextZeichen || (a.Thema?.Length ?? 0) > 500 || (a.Wuensche?.Length ?? 0) > 1000)
                return "Der Text ist zu lang.";

            foreach (var bild in a.Bilder)
            {
                if (!Bildtypen.Contains(bild.MediaType))
                    return $"„{bild.Name}“: nur PNG, JPEG, GIF oder WebP.";

                if (bild.Base64.Length > KiEntwurfAnfrage.MaxBildBytes / 3 * 4 + 4)
                    return $"„{bild.Name}“ ist größer als 5 MB.";

                if (!Convert.TryFromBase64String(bild.Base64, new byte[bild.Base64.Length], out _))
                    return $"„{bild.Name}“ ist kein gültiges Bild.";
            }

            return null;
        }
    }
}
