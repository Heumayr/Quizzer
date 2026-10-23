using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Helpers;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Transfer.Ki;
using System.Globalization;

namespace Quizzer.DataModels.Questions
{
    /// <summary>
    /// Macht aus einem KI-Entwurf (<see cref="KiFrageEntwurf"/>) eine Frage, wie sie der Editor
    /// öffnet (M31, 2026-10-23). <b>Gespeichert wird hier nichts</b> - der Entwurf landet
    /// ungespeichert im Editor, und erst der Spielleiter entscheidet.
    /// <para>
    /// Gebaut wie die Typenschau (<c>DemoDataSeeder</c>): dort erzeugt dieselbe Schrittfolge je
    /// Typ nachweislich spielbare Fragen. <b>Was die KI liefert, ist nicht vertrauenswürdig</b> -
    /// Längen werden gekappt, Zahlen begrenzt, Unmögliches wird zum Hinweis statt zur Frage.
    /// </para>
    /// <para>WPF-frei, damit jede Regel ohne Oberflächen-Thread prüfbar bleibt.</para>
    /// </summary>
    public static class KiEntwurfUebersetzer
    {
        public const int MaxFragetext = 600;
        public const int MaxZeile = 300;
        public const int MaxZeilen = 8;

        /// <summary>Das Ergebnis: die Frage - oder <c>null</c> mit dem Grund, warum keine daraus wird.</summary>
        public sealed record Ergebnis(QuestionBase? Frage, IReadOnlyList<string> Hinweise);

        /// <param name="entwurf">Was die KI geliefert hat.</param>
        /// <param name="kategorieId">Die Kategorie, in die der Entwurf gehört (der Editor kann sie ändern).</param>
        /// <param name="bildDatei">
        /// Legt das mitgeschickte Bild mit diesem Index im Ressourcenordner ab und liefert den
        /// Dateinamen - <c>null</c>, wenn es das Bild nicht gibt.
        /// </param>
        /// <param name="heute">Für den Vermerk in den Notizen.</param>
        public static Ergebnis Baue(KiFrageEntwurf entwurf, Guid kategorieId, Func<int, string?> bildDatei, DateTime heute)
        {
            ArgumentNullException.ThrowIfNull(entwurf);

            var hinweise = new List<string>();
            var fragetext = Kappe(entwurf.Fragetext, MaxFragetext);

            if (fragetext.Length == 0)
                return new Ergebnis(null, ["Der Entwurf hat keinen Fragetext."]);

            var zeilen = entwurf.Zeilen
                .Where(z => z != null && !string.IsNullOrWhiteSpace(z.Text))
                .Take(MaxZeilen)
                .Select(z => new KiZeile { Text = Kappe(z.Text, MaxZeile), Richtig = z.Richtig, Partner = Kappe(z.Partner, MaxZeile) })
                .ToList();

            if (entwurf.Zeilen.Count > MaxZeilen)
                hinweise.Add($"Nur die ersten {MaxZeilen} Zeilen übernommen.");

            var frage = Factory.CreateNewQuestion(entwurf.Typ);
            frage.Id = Guid.NewGuid();
            frage.CategoryId = kategorieId;
            frage.QuestionText = fragetext;
            frage.Designation = Kappe(string.IsNullOrWhiteSpace(entwurf.Bezeichnung) ? fragetext : entwurf.Bezeichnung, 100);
            frage.DesignationShort = Kappe(string.IsNullOrWhiteSpace(entwurf.Kurz) ? frage.Designation : entwurf.Kurz, 20);
            frage.Points = Math.Clamp(entwurf.Punkte, 10, 5000);
            frage.MinusPoints = frage.Points / 2;
            frage.Difficulty = Enum.IsDefined(typeof(Difficulty), entwurf.Schwierigkeit)
                ? (Difficulty)entwurf.Schwierigkeit
                : Difficulty.Level1;

            var aufloesung = Kappe(entwurf.Aufloesung, MaxZeile);
            var problem = entwurf.Typ switch
            {
                QuestionType.Default or QuestionType.Properties => Hinweisfrage(frage, zeilen, aufloesung, entwurf.BildIndex, bildDatei),
                QuestionType.MultipleChoice => MultipleChoice((MultipleChoiceQuestion)frage, zeilen, aufloesung),
                QuestionType.Appreciate => Schaetzfrage((AppreciateQestion)frage, entwurf, aufloesung),
                QuestionType.Reveal => Aufdeckfrage((RevealQuestion)frage, aufloesung, entwurf.BildIndex, bildDatei),
                QuestionType.Ordering => Reihenfolge(frage, zeilen),
                QuestionType.Matching => Zuordnung(frage, zeilen),
                QuestionType.TrueFalse => WahrFalsch(frage, zeilen),
                _ => $"Unbekannter Fragetyp {entwurf.Typ}.",
            };

            if (problem != null)
                return new Ergebnis(null, [.. hinweise, problem]);

            if (entwurf.BildIndex != null && entwurf.Typ is not (QuestionType.Default or QuestionType.Properties or QuestionType.Reveal))
                hinweise.Add("Das Bild wurde nicht übernommen - bei diesem Fragetyp hat es keinen festen Platz. Im Editor einem Schritt zuordnen.");

            frage.Notes = Notizen(entwurf, heute);
            frage.KiUngeprueft = true;

            return new Ergebnis(frage, hinweise);
        }

        private static string? Hinweisfrage(QuestionBase frage, List<KiZeile> zeilen, string aufloesung, int? bildIndex, Func<int, string?> bildDatei)
        {
            if (aufloesung.Length == 0)
                return "Der Entwurf hat keine Auflösung.";

            for (var i = 0; i < zeilen.Count; i++)
                frage.Steps.Add(Schritt(frage.Id, (i + 1) * 10, zeilen[i].Text));

            if (bildIndex is { } index && bildDatei(index) is { } datei)
            {
                // Das Bild gehört an den ersten Hinweis - sonst stünde es erst mit der Lösung da.
                if (frage.Steps.Count == 0)
                    frage.Steps.Add(Schritt(frage.Id, 10, string.Empty));

                MitBild(frage.Steps[0], datei);
            }

            frage.Steps.Add(Schritt(frage.Id, 900, aufloesung, istLoesung: true, istAbschluss: true));
            return null;
        }

        private static string? MultipleChoice(MultipleChoiceQuestion frage, List<KiZeile> zeilen, string aufloesung)
        {
            if (zeilen.Count < 2)
                return "Multiple Choice braucht mindestens zwei Antworten.";

            if (!zeilen.Any(z => z.Richtig))
                return "Multiple Choice: keine Antwort ist als richtig markiert.";

            if (zeilen.All(z => z.Richtig))
                return "Multiple Choice: alle Antworten sind als richtig markiert.";

            frage.ShowTextOnKeySelect = true;
            frage.BuzzerMaxAllowedKeySelect = zeilen.Count(z => z.Richtig);

            for (var i = 0; i < zeilen.Count; i++)
                frage.Steps.Add(Schritt(frage.Id, (i + 1) * 10, zeilen[i].Text, istLoesung: zeilen[i].Richtig));

            // Kein IsResult am Abschluss - er zählte sonst als weitere Lösung (siehe Typenschau).
            frage.Steps.Add(Schritt(frage.Id, 900, aufloesung, istAbschluss: true));
            return null;
        }

        private static string? Schaetzfrage(AppreciateQestion frage, KiFrageEntwurf entwurf, string aufloesung)
        {
            if (!string.IsNullOrWhiteSpace(entwurf.SollDatum))
            {
                if (!DateTime.TryParseExact(entwurf.SollDatum.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var datum))
                    return $"Schätzfrage: „{entwurf.SollDatum}\" ist kein Datum (yyyy-MM-dd).";

                frage.ValueKind = AppreciateValueKind.Date;
                frage.Unit = AppreciateUnit.Datum;
                frage.ExpectedDate = datum;
            }
            else if (entwurf.Sollwert is { } wert && double.IsFinite(wert))
            {
                var einheit = Enum.TryParse<AppreciateUnit>(entwurf.Einheit?.Trim(), ignoreCase: true, out var e) && e != AppreciateUnit.Datum
                    ? e
                    : AppreciateUnit.Stueck;

                frage.Unit = einheit;
                frage.ValueKind = AppreciateUnits.Info(einheit).Kind;
                frage.ExpectedValue = wert;
            }
            else
            {
                return "Schätzfrage ohne Sollwert und ohne Datum.";
            }

            frage.Steps.Add(Schritt(frage.Id, 900,
                aufloesung.Length > 0 ? aufloesung : AppreciateEvaluator.DescribeExpected(frage),
                istLoesung: true, istAbschluss: true));
            return null;
        }

        private static string? Aufdeckfrage(RevealQuestion frage, string aufloesung, int? bildIndex, Func<int, string?> bildDatei)
        {
            if (bildIndex is not { } index || bildDatei(index) is not { } datei)
                return "Eine Aufdeckfrage braucht ein Bild - keines mitgeschickt oder angegeben.";

            if (aufloesung.Length == 0)
                return "Der Entwurf hat keine Auflösung.";

            // Wie die Typenschau: ein Raster aus 3 × 2 Flächen, im Aufdeck-Editor änderbar.
            frage.Mode = RevealMode.Areas;
            frage.ImageFileName = datei;

            var flaechen = new List<RevealArea>();
            for (var zeile = 0; zeile < 2; zeile++)
                for (var spalte = 0; spalte < 3; spalte++)
                    flaechen.Add(new RevealArea(spalte / 3.0, zeile / 2.0, 1 / 3.0, 0.5));

            frage.AreasJson = RevealAreas.ToJson(flaechen);

            for (var i = 0; i < RevealAreas.Schrittzahl(flaechen); i++)
            {
                var schritt = Schritt(frage.Id, (i + 1) * 10, string.Empty);
                schritt.Designation = $"Aufdecken {i + 1}";
                frage.Steps.Add(schritt);
            }

            frage.Steps.Add(Schritt(frage.Id, 900, aufloesung, istAbschluss: true));
            return null;
        }

        private static string? Reihenfolge(QuestionBase frage, List<KiZeile> zeilen)
        {
            if (zeilen.Count < 3)
                return "Eine Reihenfolge braucht mindestens drei Elemente.";

            frage.MinusPoints = 0;
            frage.ShowTextOnKeySelect = true;

            for (var i = 0; i < zeilen.Count; i++)
            {
                var schritt = Schritt(frage.Id, (i + 1) * 10, zeilen[i].Text);
                schritt.SolutionNumber = i + 1;
                frage.Steps.Add(schritt);
            }

            frage.BuzzerMaxAllowedKeySelect = zeilen.Count;
            return null;
        }

        private static string? Zuordnung(QuestionBase frage, List<KiZeile> zeilen)
        {
            var paare = zeilen.Where(z => !string.IsNullOrWhiteSpace(z.Partner)).ToList();

            if (paare.Count < 3)
                return "Eine Zuordnung braucht mindestens drei Paare mit Gegenstück.";

            frage.MinusPoints = 0;
            frage.ShowTextOnKeySelect = true;

            for (var i = 0; i < paare.Count; i++)
            {
                // Wie im Spiel: rechts (StepText) das Gegenstück, links (PartnerText) der Begriff.
                var schritt = Schritt(frage.Id, (i + 1) * 10, paare[i].Partner!);
                schritt.PartnerText = paare[i].Text;
                schritt.SolutionNumber = i + 1;
                frage.Steps.Add(schritt);
            }

            frage.BuzzerMaxAllowedKeySelect = paare.Count;
            return null;
        }

        private static string? WahrFalsch(QuestionBase frage, List<KiZeile> zeilen)
        {
            if (zeilen.Count < 2)
                return "Wahr oder falsch braucht mindestens zwei Aussagen.";

            frage.MinusPoints = 0;
            frage.ShowTextOnKeySelect = true;

            for (var i = 0; i < zeilen.Count; i++)
                frage.Steps.Add(Schritt(frage.Id, (i + 1) * 10, zeilen[i].Text, istLoesung: zeilen[i].Richtig));

            frage.BuzzerMaxAllowedKeySelect = zeilen.Count;
            return null;
        }

        /// <summary>
        /// Der Vermerk für den Spielleiter: dass es ein Entwurf ist, was zu prüfen ist, woher er kommt.
        /// </summary>
        private static string Notizen(KiFrageEntwurf entwurf, DateTime heute)
        {
            var teile = new List<string> { $"KI-Entwurf vom {heute:dd.MM.yyyy} - Fakten bitte prüfen." };

            if (!string.IsNullOrWhiteSpace(entwurf.Notizen))
                teile.Add(Kappe(entwurf.Notizen, 1000));

            var quellen = entwurf.Quellen.Where(q => !string.IsNullOrWhiteSpace(q)).Select(q => Kappe(q, 300)).Distinct().Take(10).ToList();
            if (quellen.Count > 0)
                teile.Add("Quellen: " + string.Join(", ", quellen));

            return string.Join(Environment.NewLine, teile);
        }

        private static QuestionStepResource Schritt(Guid frageId, int nummer, string text,
            bool istStart = false, bool istLoesung = false, bool istAbschluss = false)
            => new()
            {
                Id = Guid.NewGuid(),
                QuestionBaseId = frageId,
                SequenceNumber = nummer,
                Designation = text.Length <= 40 ? text : text[..40],
                StepText = text,
                IsStart = istStart,
                IsResult = istLoesung,
                IsFinish = istAbschluss,
            };

        private static QuestionStepResource MitBild(QuestionStepResource schritt, string datei)
        {
            schritt.ResourceFileName = datei;
            schritt.ResourceTyp = ResourceType.Image;
            return schritt;
        }

        private static string Kappe(string? text, int laenge)
        {
            var t = (text ?? string.Empty).Trim();
            return t.Length <= laenge ? t : t[..laenge].TrimEnd();
        }
    }
}
