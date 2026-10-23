using Quizzer.Base;
using Quizzer.DataModels;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Questions;
using Quizzer.DataModels.Transfer.Ki;
using Quizzer.Server;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace Quizzer.Views.QuestionTypes
{
    /// <summary>
    /// Entwürfe von der KI des Servers (M31, 2026-10-23). Der Dialog liefert Entwürfe, der
    /// <see cref="KiEntwurfUebersetzer"/> baut daraus Fragen - <b>ungespeichert</b>: die erste
    /// steht danach in diesem Editor, die übrigen kommen mit „Speichern und nächste" dran.
    /// <para>
    /// Nur für eine neue Frage: eine bestehende zu überschreiben wäre ein stiller Verlust.
    /// </para>
    /// </summary>
    public partial class EditQuestionViewModel
    {
        public Visibility KiVisibility
            => IstNeu && ServerSession.IsActive && !ServerSession.IsOffline ? Visibility.Visible : Visibility.Collapsed;

        private bool kiPruefungSichtbar;

        /// <summary>Der Haken „Fakten geprüft" - nur bei einer Frage, die als KI-Entwurf entstand (M32).</summary>
        public Visibility KiPruefungVisibility => kiPruefungSichtbar ? Visibility.Visible : Visibility.Collapsed;

        public bool FaktenGeprueft
        {
            get => Question?.KiUngeprueft == false;
            set
            {
                if (Question == null)
                    return;

                Question.KiUngeprueft = !value;
                OnPropertyChanged();
            }
        }

        /// <summary>Beim Laden einer Frage: der Haken bleibt sichtbar, auch nachdem er gesetzt wurde.</summary>
        private void MeldeKiPruefung()
        {
            kiPruefungSichtbar = Question?.KiUngeprueft == true;
            OnPropertyChanged(nameof(KiPruefungVisibility));
            OnPropertyChanged(nameof(FaktenGeprueft));
        }

        private string kiHinweis = string.Empty;

        /// <summary>Was beim Übernehmen auffiel - steht im Kopf, bis die nächste Frage kommt.</summary>
        public string KiHinweis
        {
            get => kiHinweis;
            private set
            {
                kiHinweis = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(KiHinweisVisibility));
            }
        }

        public Visibility KiHinweisVisibility => string.IsNullOrWhiteSpace(KiHinweis) ? Visibility.Collapsed : Visibility.Visible;

        /// <summary>„Falsche Antworten vorschlagen" - bei jeder Multiple-Choice-Frage, auch einer gespeicherten (M32).</summary>
        public Visibility AblenkerVisibility
            => Question?.Typ == DataModels.Enumerations.QuestionType.MultipleChoice && ServerSession.IsActive && !ServerSession.IsOffline
                ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>So viele Antworten soll eine Frage nach dem Vorschlag haben.</summary>
        internal const int AntwortenZiel = 4;

        private AsyncRelayCommand? ablenkerCommand;

        /// <summary>
        /// Holt nur die falschen Antworten von der KI (M32): Frage und richtige Antwort bleiben, wie
        /// sie sind; ergänzt wird bis vier Antworten, mindestens eine. Kostet einen Aufruf des Limits.
        /// </summary>
        public ICommand AblenkerCommand => ablenkerCommand ??= new AsyncRelayCommand(async _ =>
        {
            if (Question == null || Zeileneditor is not { } editor || ServerSession.Api is not { } api)
                return;

            var belegt = editor.Zeilen.Where(z => !string.IsNullOrWhiteSpace(z.Text)).ToList();
            var richtige = belegt.Where(z => z.IstRichtig).Select(z => z.Text.Trim()).ToList();
            var falsche = belegt.Where(z => !z.IstRichtig).Select(z => z.Text.Trim()).ToList();

            if (string.IsNullOrWhiteSpace(Question.QuestionText) || richtige.Count == 0)
            {
                UserPrompt.Inform("Erst den Fragetext und die richtige Antwort eintragen und anhaken - dazu passend schlägt die KI falsche vor.", "Falsche Antworten");
                return;
            }

            var anzahl = Math.Max(1, AntwortenZiel - belegt.Count);
            var anfrage = AblenkerAnfrage(Question.QuestionText, richtige, falsche, anzahl);

            System.Windows.Input.Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
            try
            {
                var antwort = await api.PostKiEntwurfAsync(anfrage);
                var vorschlaege = NeueAblenker(antwort.Entwuerfe, richtige.Concat(falsche), anzahl);

                if (vorschlaege.Count == 0)
                {
                    KiHinweis = "Die KI hat keine neuen falschen Antworten geliefert. Noch einmal versuchen oder selbst ergänzen.";
                    return;
                }

                foreach (var text in vorschlaege)
                {
                    var frei = editor.Zeilen.FirstOrDefault(z => string.IsNullOrWhiteSpace(z.Text));
                    if (frei == null)
                    {
                        editor.AddRowCommand.Execute(null);
                        frei = editor.Zeilen[^1];
                    }

                    frei.Text = text;
                    frei.IstRichtig = false;
                }

                // Eine „falsche" Antwort, die in Wahrheit stimmt, ist ein Faktenfehler wie jeder andere.
                Question.KiUngeprueft = true;
                MeldeKiPruefung();
                KiHinweis = $"{vorschlaege.Count} falsche Antwort(en) von der KI ergänzt - bitte prüfen, dass keine davon doch stimmt. Heute noch {antwort.VerbleibendHeute} Aufrufe.";
                Revalidate();
            }
            catch (Exception e) when (e is Client.ApiException or Client.ServerUnreachableException)
            {
                UserPrompt.Inform(e.Message, "Falsche Antworten");
            }
            finally
            {
                System.Windows.Input.Mouse.OverrideCursor = null;
            }
        });

        /// <summary>Die Anfrage für Ablenker - eigene Methode, damit sie ohne Server prüfbar ist.</summary>
        internal static KiEntwurfAnfrage AblenkerAnfrage(string fragetext, IReadOnlyList<string> richtige, IReadOnlyList<string> falsche, int anzahl) => new()
        {
            Thema = Kuerze(fragetext, 200),
            Typ = DataModels.Enumerations.QuestionType.MultipleChoice,
            Anzahl = 1,
            Text = $"Frage: {fragetext}\nRichtige Antwort(en): {string.Join("; ", richtige)}"
                + (falsche.Count > 0 ? $"\nSchon vorhandene falsche Antworten: {string.Join("; ", falsche)}" : string.Empty),
            Wuensche = $"Behalte genau diese Frage und die richtigen Antworten. Liefere zusätzlich {anzahl} neue, plausible, aber eindeutig falsche Antworten im Stil der vorhandenen.",
        };

        /// <summary>Die falschen Antworten des Entwurfs, die es noch nicht gibt - höchstens so viele wie bestellt.</summary>
        internal static List<string> NeueAblenker(IEnumerable<KiFrageEntwurf> entwuerfe, IEnumerable<string> vorhanden, int anzahl)
        {
            var bekannt = new HashSet<string>(vorhanden.Select(t => t.Trim()), StringComparer.CurrentCultureIgnoreCase);

            return entwuerfe
                .SelectMany(e => e.Zeilen)
                .Where(z => !z.Richtig && !string.IsNullOrWhiteSpace(z.Text))
                .Select(z => z.Text.Trim())
                .Where(bekannt.Add)
                .Take(anzahl)
                .ToList();
        }

        private AsyncRelayCommand? kiCommand;

        public ICommand KiCommand => kiCommand ??= new AsyncRelayCommand(_ =>
        {
            if (Question == null || !IstNeu)
                return Task.CompletedTask;

            var fenster = new KiEntwurfView { Owner = Window };

            if (fenster.ViewModel is { } dialog)
            {
                dialog.Vorgabe(Question.Typ, SelectedCategory, Categories, Question.Difficulty);
                fenster.ShowDialog();

                if (dialog.Gewaehlt.Count > 0)
                    Uebernimm(dialog.Gewaehlt, dialog.Bilder, dialog.Kategorie?.Id ?? Question.CategoryId);
            }

            return Task.CompletedTask;
        });

        private void Uebernimm(IReadOnlyList<KiFrageEntwurf> entwuerfe, IReadOnlyList<KiBild> bilder, Guid kategorie)
        {
            var dateien = new Dictionary<int, string?>();
            var hinweise = new List<string>();
            var fragen = new List<QuestionBase>();

            string? Bild(int index)
            {
                if (!dateien.TryGetValue(index, out var datei))
                    dateien[index] = datei = LegeBildAb(bilder, index, hinweise);

                return datei;
            }

            foreach (var entwurf in entwuerfe)
            {
                var ergebnis = KiEntwurfUebersetzer.Baue(entwurf, kategorie, Bild, DateTime.Today);
                var name = Kuerze(entwurf.Fragetext, 40);

                hinweise.AddRange(ergebnis.Hinweise.Select(h => $"„{name}“: {h}"));

                if (ergebnis.Frage is { } frage)
                    fragen.Add(frage);
            }

            if (fragen.Count == 0)
            {
                UserPrompt.Inform("Aus den Entwürfen ließ sich keine Frage bauen:\n\n" + string.Join("\n", hinweise), "KI-Entwurf");
                return;
            }

            warteschlange.Clear();

            if (EineFrage && fragen.Count > 1)
                hinweise.Insert(0, $"Für die Kachel zählt nur der erste Entwurf - die übrigen {fragen.Count - 1} bitte über die Fragenliste anlegen.");
            else
                foreach (var frage in fragen.Skip(1))
                    warteschlange.Enqueue(frage);

            Question = fragen[0];
            LoeseKategorie();
            Revalidate();

            KiHinweis = "KI-Entwurf - Fakten bitte prüfen, dann speichern."
                + (warteschlange.Count > 0 ? $" Noch {warteschlange.Count} weitere: „Speichern und nächste“." : string.Empty)
                + (hinweise.Count > 0 ? "\n" + string.Join("\n", hinweise.Take(6)) : string.Empty);

            OnPropertyChanged(nameof(NaechsteText));
        }

        /// <summary>
        /// Schreibt ein mitgeschicktes Bild in den Ressourcenordner - über denselben Weg wie der
        /// Medienknopf, damit Name und Umwandlung nach PNG gleich sind.
        /// </summary>
        private static string? LegeBildAb(IReadOnlyList<KiBild> bilder, int index, List<string> hinweise)
        {
            if (index < 0 || index >= bilder.Count)
                return null;

            var bild = bilder[index];
            var endung = bild.MediaType switch
            {
                "image/jpeg" => ".jpg",
                "image/gif" => ".gif",
                "image/webp" => ".webp",
                _ => ".png",
            };

            var ordner = Path.Combine(Path.GetTempPath(), "quizzer-ki-" + Guid.NewGuid().ToString("N"));
            var name = Path.GetFileNameWithoutExtension(bild.Name);
            var temp = Path.Combine(ordner, (string.IsNullOrWhiteSpace(name) ? "ki-bild" : name) + endung);

            try
            {
                Directory.CreateDirectory(ordner);
                File.WriteAllBytes(temp, Convert.FromBase64String(bild.Base64));
                return FileHelper.HandleSelectedResourceFile(temp, Settings.ResourceRootFolder).Filename;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or FormatException or NotSupportedException or ArgumentException)
            {
                hinweise.Add($"Bild „{bild.Name}“ ließ sich nicht ablegen: {e.Message}");
                return null;
            }
            finally
            {
                try { Directory.Delete(ordner, recursive: true); } catch (IOException) { }
            }
        }
    }
}
