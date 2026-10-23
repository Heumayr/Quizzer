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
