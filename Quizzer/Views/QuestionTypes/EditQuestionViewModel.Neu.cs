using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Helpers;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Questions;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace Quizzer.Views.QuestionTypes
{
    /// <summary>
    /// Fragen schnell ergänzen (E1, M31, 2026-10-23): der Typ wird im Editor gewählt, „Speichern
    /// und nächste" (Strg+Enter) öffnet gleich die nächste mit derselben Vorbelegung, und die
    /// Bezeichnung entsteht aus dem Fragetext, bis jemand sie selbst ändert.
    /// </summary>
    public partial class EditQuestionViewModel
    {
        /// <summary>Alle Fragetypen für die Auswahl im Kopf.</summary>
        public ObservableCollection<QuestionTypeProfile> TypProfile { get; } = new(QuestionTypeProfiles.All);

        /// <summary>Nur eine neue Frage wählt ihren Typ hier; eine gespeicherte wird umgewandelt.</summary>
        public bool IstNeu => Question != null && Question.Id == Guid.Empty;

        public Visibility TypwahlVisibility => IstNeu ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>Wird nach „Speichern und nächste" gerufen - die Ansicht setzt den Fokus.</summary>
        public event EventHandler? NaechsteGeladen;

        public QuestionTypeProfile? GewaehlterTyp
        {
            get => TypProfile.FirstOrDefault(p => p.Typ == Question?.Typ);
            set
            {
                if (value == null || Question == null || !IstNeu || value.Typ == Question.Typ)
                    return;

                WechsleTyp(value.Typ);
            }
        }

        /// <summary>
        /// Baut die ungespeicherte Frage im neuen Typ neu auf und nimmt mit, was sich übertragen
        /// lässt: Texte, Kategorie, Punkte, Zeilen samt Häkchen und Gegenstück.
        /// </summary>
        private void WechsleTyp(QuestionType typ)
        {
            var alt = Question!;
            var alterEditor = Zeileneditor;

            var neu = Factory.CreateNewQuestion(typ);
            neu.QuestionText = alt.QuestionText;
            neu.Designation = alt.Designation;
            neu.DesignationShort = alt.DesignationShort;
            neu.CategoryId = alt.CategoryId;
            neu.Category = alt.Category;
            neu.Points = alt.Points;
            neu.MinusPoints = typ == QuestionType.Ordering ? 0 : alt.MinusPoints;
            neu.Difficulty = alt.Difficulty;
            neu.Notes = alt.Notes;
            neu.IsShared = alt.IsShared;

            var (bezeichnung, kurz) = (automatischeBezeichnung, automatischeKurzform);
            Question = neu;
            (automatischeBezeichnung, automatischeKurzform) = (bezeichnung, kurz);

            if (alterEditor != null && Zeileneditor is { } editor)
                UebertrageZeilen(alterEditor, editor);

            OnPropertyChanged(nameof(GewaehlterTyp));
            Revalidate();
        }

        private static void UebertrageZeilen(Typed.ZeileneditorViewModel von, Typed.ZeileneditorViewModel nach)
        {
            if (von.Start is { } s && nach.Start is { } z && string.IsNullOrWhiteSpace(z.Text))
                z.Text = s.Text;

            if (von.Abschluss is { } a && nach.Abschluss is { } b && string.IsNullOrWhiteSpace(b.Text))
                b.Text = a.Text;

            if (nach.ZeilenAendernVisibility != Visibility.Visible)
                return;

            var i = 0;
            foreach (var alt in von.Zeilen.Where(z => !string.IsNullOrWhiteSpace(z.Text) || !string.IsNullOrWhiteSpace(z.Partner)))
            {
                if (i >= nach.Zeilen.Count)
                    nach.AddRowCommand.Execute(null);

                var ziel = nach.Zeilen[i++];
                ziel.Text = alt.Text;
                ziel.Partner = alt.Partner;
                ziel.IstRichtig = alt.IstRichtig;

                if (alt.HatMedium)
                {
                    ziel.Schritt.ResourceFileName = alt.Schritt.ResourceFileName;
                    ziel.Schritt.ResourceTyp = alt.Schritt.ResourceTyp;
                    ziel.MeldeAlles();
                }
            }
        }

        // ---- Bezeichnung aus dem Fragetext ----

        private string? automatischeBezeichnung;
        private string? automatischeKurzform;

        /// <summary>
        /// Der Fragetext - über das ViewModel, weil die Frage keine Änderungen meldet und die
        /// beiden Bezeichnungen mitlaufen sollen.
        /// </summary>
        public string Fragetext
        {
            get => Question?.QuestionText ?? string.Empty;
            set
            {
                if (Question == null)
                    return;

                Question.QuestionText = value;

                // Mitgeführt wird nur, solange niemand von Hand etwas anderes eingetragen hat.
                if (string.IsNullOrWhiteSpace(Question.Designation) || Question.Designation == automatischeBezeichnung)
                {
                    automatischeBezeichnung = Kuerze(value, DataModels.Questions.Bezeichnung.Lang);
                    Question.Designation = automatischeBezeichnung;
                    OnPropertyChanged(nameof(Bezeichnung));
                }

                if (string.IsNullOrWhiteSpace(Question.DesignationShort) || Question.DesignationShort == automatischeKurzform)
                {
                    automatischeKurzform = Kuerze(value, DataModels.Questions.Bezeichnung.Kurz);
                    Question.DesignationShort = automatischeKurzform;
                    OnPropertyChanged(nameof(Kurzbezeichnung));
                }

                OnPropertyChanged();
                Revalidate();
            }
        }

        public string Bezeichnung
        {
            get => Question?.Designation ?? string.Empty;
            set
            {
                if (Question == null)
                    return;

                Question.Designation = value;
                OnPropertyChanged();
                Revalidate();
            }
        }

        public string Kurzbezeichnung
        {
            get => Question?.DesignationShort ?? string.Empty;
            set
            {
                if (Question == null)
                    return;

                Question.DesignationShort = value;
                OnPropertyChanged();
                Revalidate();
            }
        }

        internal static string Kuerze(string text, int laenge) => DataModels.Questions.Bezeichnung.AusFragetext(text, laenge);

        private void MeldeGrunddaten()
        {
            automatischeBezeichnung = null;
            automatischeKurzform = null;
            OnPropertyChanged(nameof(Fragetext));
            OnPropertyChanged(nameof(Bezeichnung));
            OnPropertyChanged(nameof(Kurzbezeichnung));
            OnPropertyChanged(nameof(IstNeu));
            OnPropertyChanged(nameof(TypwahlVisibility));
            OnPropertyChanged(nameof(GewaehlterTyp));
            OnPropertyChanged(nameof(KiVisibility));
            OnPropertyChanged(nameof(AblenkerVisibility));
            OnPropertyChanged(nameof(NaechsteText));
            MeldeKiPruefung();
            OnPropertyChanged(nameof(NaechsteVisibility));
        }

        /// <summary>Löst die Kategorie aus <c>CategoryId</c> auf - nach jedem Austausch der Frage.</summary>
        private void LoeseKategorie()
        {
            if (Question != null && Question.CategoryId != Guid.Empty)
                SelectedCategory = Categories.FirstOrDefault(c => c.Id == Question.CategoryId);

            OnPropertyChanged(nameof(SelectedCategory));
        }

        // ---- Speichern und nächste ----

        /// <summary>
        /// Aus einer Kachel des Spielfelds geöffnet: genau eine Frage ist gemeint, „Speichern und
        /// nächste" entfällt.
        /// </summary>
        public bool EineFrage { get; set; }

        public Visibility NaechsteVisibility => EineFrage ? Visibility.Collapsed : Visibility.Visible;

        /// <summary>KI-Entwürfe, die nach der aktuellen Frage drankommen.</summary>
        private readonly Queue<QuestionBase> warteschlange = new();

        public string NaechsteText => warteschlange.Count > 0
            ? $"Speichern und nächster Entwurf ({warteschlange.Count})"
            : "Speichern und nächste";

        private AsyncRelayCommand? saveAndNextCommand;

        public ICommand SaveAndNextCommand => saveAndNextCommand ??= new AsyncRelayCommand(async _ =>
        {
            if (Question == null)
                return;

            await VMSaveAsync();

            if (!CanSave || Question.Id == Guid.Empty)
                return;

            var ausKi = warteschlange.Count > 0;
            var naechste = ausKi ? warteschlange.Dequeue() : Vorbelegung.NeueFrage(Question.Typ);

            // Eine KI-Frage hat ihre Kategorie aus dem Dialog; fehlt sie, gilt die von eben.
            if (naechste.CategoryId == Guid.Empty)
                naechste.CategoryId = Question.CategoryId;

            Question = naechste;
            LoeseKategorie();
            MeldeGrunddaten();

            KiHinweis = ausKi
                ? "KI-Entwurf - Fakten bitte prüfen, dann speichern." + (warteschlange.Count > 0 ? $" Danach noch {warteschlange.Count}." : string.Empty)
                : string.Empty;
            Revalidate();
            NaechsteGeladen?.Invoke(this, EventArgs.Empty);
        }, _ => CanSave && !EineFrage);
    }
}
